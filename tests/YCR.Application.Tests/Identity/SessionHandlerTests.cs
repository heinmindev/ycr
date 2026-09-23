using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Identity;
using YCR.Application.Identity.ChangeOwnPassword;
using YCR.Application.Identity.GetCurrentUser;
using YCR.Application.Identity.Logout;
using YCR.Application.Identity.RefreshSession;
using YCR.Application.Identity.ResolveSessionPrincipal;
using YCR.Application.Tests.Network;
using YCR.Domain.Common;
using YCR.Domain.Identity;

namespace YCR.Application.Tests.Identity;

/// <summary>
/// Logout (S15), own password change (S16, S19d), principal resolution (plan P3; S13, S17, S21),
/// <c>/auth/me</c>, and secrets at rest (S31).
/// </summary>
public sealed class SessionHandlerTests(SqlServerFixture fixture) : IdentityHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "sessions";

    [Fact]
    public async Task Logout_RevokesOnlyTheCallersSessionAndAudits()
    {
        await using var seed = BuildProvider();
        var userId = await SeedUserAsync(seed, "hein.min", [RoleNames.StationManager]);
        var a = await SessionOfAsync(seed, "hein.min");
        var b = await SessionOfAsync(seed, "hein.min");

        await using var provider = BuildProvider(As(userId, RoleNames.StationManager));
        var result = await WithScopeAsync(provider, services =>
            services.GetRequiredService<LogoutHandler>().Handle(new LogoutCommand(a), CancellationToken));

        Assert.True(result.IsSuccess);
        Assert.Equal("Logout", await ScalarAsync<string>($"SELECT [RevocationReason] FROM [identity].[AuthSessions] WHERE [Id] = '{a}';"));
        Assert.Equal(1, await ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].[AuthSessions] WHERE [Id] = '{b}' AND [RevokedAtUtc] IS NULL;"));
        var row = Assert.Single(await AuditRowsAsync(IdentityAuditActions.LoggedOut));
        Assert.Equal(userId, row.ActorUserId);
        Assert.Equal(userId, row.SubjectId);
        Assert.Equal("""["StationManager"]""", row.ActorRole);
        Assert.Contains("\"revocationReason\":\"Logout\"", row.AfterJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Logout_AnotherUsersSession_ChangesNothing()
    {
        await using var seed = BuildProvider();
        await SeedUserAsync(seed, "hein.min", [RoleNames.StationManager]);
        var other = await SeedUserAsync(seed, "other.user", [RoleNames.StationManager]);
        var session = await SessionOfAsync(seed, "hein.min");

        await using var provider = BuildProvider(As(other));
        await WithScopeAsync(provider, services =>
            services.GetRequiredService<LogoutHandler>().Handle(new LogoutCommand(session), CancellationToken));

        Assert.Equal(1, await ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].[AuthSessions] WHERE [Id] = '{session}' AND [RevokedAtUtc] IS NULL;"));
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.LoggedOut));
    }

    /// <summary>Plan P15: logout racing a family revocation — one revocation, one audit row.</summary>
    [Fact]
    public async Task Logout_RacingFamilyRevocation_WritesOneRevocation()
    {
        await using var seed = BuildProvider();
        var userId = await SeedUserAsync(seed, "hein.min", [RoleNames.StationManager]);
        var login = (await LoginAsync(seed, "hein.min")).Value;
        var session = FakeAccessTokenIssuer.Parse(login.AccessToken).SessionId;
        var rotated = await WithScopeAsync(seed, services =>
            services.GetRequiredService<RefreshSessionHandler>().Handle(new RefreshSessionCommand(login.RefreshToken), CancellationToken));
        Assert.True(rotated.IsSuccess);
        Clock.Advance(TimeSpan.FromMinutes(1));

        await using var provider = BuildProvider(As(userId, RoleNames.StationManager));
        await Task.WhenAll(
            WithScopeAsync(provider, services =>
                services.GetRequiredService<LogoutHandler>().Handle(new LogoutCommand(session), CancellationToken)),
            WithScopeAsync(seed, async services =>
                (Result)await services.GetRequiredService<RefreshSessionHandler>().Handle(new RefreshSessionCommand(login.RefreshToken), CancellationToken)));

        var revocations = (await AuditRowsAsync(IdentityAuditActions.LoggedOut)).Count
            + (await AuditRowsAsync(IdentityAuditActions.RefreshFamilyRevoked)).Count;
        Assert.Equal(1, revocations);
        Assert.Equal(1, await ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].[AuthSessions] WHERE [Id] = '{session}' AND [RevokedAtUtc] IS NOT NULL;"));
    }

    [Fact]
    public async Task ChangeOwnPassword_WithCorrectCurrentPassword_RevokesOtherSessionsAndKeepsCurrent()
    {
        await using var seed = BuildProvider();
        var userId = await SeedUserAsync(seed, "hein.min", [RoleNames.StationManager]);
        var a = await SessionOfAsync(seed, "hein.min");
        var b = await SessionOfAsync(seed, "hein.min");

        await using var provider = BuildProvider(As(userId, RoleNames.StationManager));
        var result = await ChangeAsync(provider, a, Password, "new.password.12");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].[AuthSessions] WHERE [Id] = '{a}' AND [RevokedAtUtc] IS NULL;"));
        Assert.Equal("PasswordChanged", await ScalarAsync<string>($"SELECT [RevocationReason] FROM [identity].[AuthSessions] WHERE [Id] = '{b}';"));
        var row = Assert.Single(await AuditRowsAsync(IdentityAuditActions.PasswordChanged));
        Assert.Equal(userId, row.ActorUserId);
        Assert.Equal(userId, row.SubjectId);
        Assert.DoesNotContain("new.password", row.AfterJson, StringComparison.Ordinal);

        Assert.Equal(IdentityErrors.InvalidCredentials, (await LoginAsync(seed, "hein.min")).Error);
        Assert.True((await LoginAsync(seed, "hein.min", "new.password.12")).IsSuccess);
    }

    [Theory]
    [InlineData("new.pass.11")]
    [InlineData("passwordpassword")]
    public async Task ChangeOwnPassword_WithPolicyViolation_ReturnsAuthPasswordRejected(string newPassword)
    {
        await using var seed = BuildProvider();
        var userId = await SeedUserAsync(seed, "hein.min", [RoleNames.StationManager]);
        var a = await SessionOfAsync(seed, "hein.min");
        var b = await SessionOfAsync(seed, "hein.min");
        var hash = await ScalarAsync<string>($"SELECT [PasswordHash] FROM [identity].[Users] WHERE [Id] = '{userId}';");

        await using var provider = BuildProvider(As(userId));
        var result = await ChangeAsync(provider, a, Password, newPassword);

        Assert.Equal(IdentityErrors.AuthPasswordRejected, result.Error);
        Assert.Equal("Auth.PasswordRejected", result.Error.Code);
        Assert.Equal(hash, await ScalarAsync<string>($"SELECT [PasswordHash] FROM [identity].[Users] WHERE [Id] = '{userId}';"));
        Assert.Equal(1, await ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].[AuthSessions] WHERE [Id] = '{b}' AND [RevokedAtUtc] IS NULL;"));
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.PasswordChanged));
    }

    [Fact]
    public async Task ChangeOwnPassword_With129ScalarValues_ReturnsAuthPasswordRejected()
    {
        await using var seed = BuildProvider();
        var userId = await SeedUserAsync(seed, "hein.min", [RoleNames.StationManager]);
        var a = await SessionOfAsync(seed, "hein.min");

        await using var provider = BuildProvider(As(userId));
        var result = await ChangeAsync(provider, a, Password, new string('m', 121) + "yangon.1");

        Assert.Equal(IdentityErrors.AuthPasswordRejected, result.Error);
    }

    [Fact]
    public async Task ChangeOwnPassword_WithWrongCurrentPassword_ReturnsCurrentPasswordIncorrect()
    {
        await using var seed = BuildProvider();
        var userId = await SeedUserAsync(seed, "hein.min", [RoleNames.StationManager]);
        var a = await SessionOfAsync(seed, "hein.min");

        await using var provider = BuildProvider(As(userId));
        var result = await ChangeAsync(provider, a, "not.the.password", "new.password.12");

        Assert.Equal(IdentityErrors.CurrentPasswordIncorrect, result.Error);
        Assert.Equal(ErrorType.BusinessRule, result.Error.Type);
        Assert.Equal((0, (DateTimeOffset?)null), await LockoutStateAsync(userId));
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.PasswordChanged));
    }

    [Fact]
    public async Task ChangeOwnPassword_WhenMustChange_ClearsFlag()
    {
        await using var seed = BuildProvider();
        var userId = await SeedUserAsync(seed, "hein.min", [RoleNames.StationManager], mustChangePassword: true);
        var a = await SessionOfAsync(seed, "hein.min");

        await using var provider = BuildProvider(As(userId));
        Assert.True((await ChangeAsync(provider, a, Password, "new.password.12")).IsSuccess);

        Assert.False(await ScalarAsync<bool>($"SELECT [MustChangePassword] FROM [identity].[Users] WHERE [Id] = '{userId}';"));
    }

    [Fact]
    public async Task Resolve_ActiveSession_ReturnsUserRolesAndPermissionUnion()
    {
        await using var provider = BuildProvider();
        var userId = await SeedUserAsync(provider, "hein.min", [RoleNames.StationManager, RoleNames.SystemAdministrator]);
        var session = await SessionOfAsync(provider, "hein.min");

        var principal = await ResolveAsync(provider, userId, session);

        Assert.NotNull(principal);
        Assert.Equal([RoleNames.SystemAdministrator, RoleNames.StationManager], principal.Roles);
        Assert.Equal(
            ["auth-sessions.revoke", "stations.manage", "stations.read", "users.manage", "users.read", "users.roles.manage"],
            principal.Permissions);
        Assert.False(principal.MustChangePassword);
        Assert.Equal(Clock.GetUtcNow().AddHours(12), principal.SessionExpiresAtUtc);
    }

    [Fact]
    public async Task Resolve_RevokedExpiredOrUnknownSession_ReturnsNone()
    {
        await using var provider = BuildProvider();
        var userId = await SeedUserAsync(provider, "hein.min", [RoleNames.StationManager]);
        var other = await SeedUserAsync(provider, "other.user", [RoleNames.StationManager]);
        var revoked = await SessionOfAsync(provider, "hein.min");
        var live = await SessionOfAsync(provider, "hein.min");
        await ExecuteAsMigratorAsync($"UPDATE [identity].[AuthSessions] SET [RevokedAtUtc] = SYSUTCDATETIME() AT TIME ZONE 'UTC', [RevocationReason] = N'Logout' WHERE [Id] = '{revoked}';");

        Assert.Null(await ResolveAsync(provider, userId, revoked));
        Assert.Null(await ResolveAsync(provider, userId, Guid.NewGuid()));
        Assert.Null(await ResolveAsync(provider, other, live));
        Assert.NotNull(await ResolveAsync(provider, userId, live));

        Clock.Advance(TimeSpan.FromHours(12));
        Assert.Null(await ResolveAsync(provider, userId, live));
    }

    [Fact]
    public async Task Resolve_DisabledUser_ReturnsNone()
    {
        await using var provider = BuildProvider();
        var userId = await SeedUserAsync(provider, "hein.min", [RoleNames.StationManager]);
        var session = await SessionOfAsync(provider, "hein.min");
        await ExecuteAsMigratorAsync($"UPDATE [identity].[Users] SET [IsDisabled] = 1, [DisabledAtUtc] = SYSUTCDATETIME() AT TIME ZONE 'UTC' WHERE [Id] = '{userId}';");

        Assert.Null(await ResolveAsync(provider, userId, session));
    }

    [Fact]
    public async Task Resolve_MustChangeUser_FlagsPrincipal()
    {
        await using var provider = BuildProvider();
        var userId = await SeedUserAsync(provider, "hein.min", [RoleNames.StationManager], mustChangePassword: true);
        var session = await SessionOfAsync(provider, "hein.min");

        Assert.True((await ResolveAsync(provider, userId, session))!.MustChangePassword);
    }

    [Fact]
    public async Task Resolve_UserWithNoRole_HasNoPermission()
    {
        await using var provider = BuildProvider();
        var userId = await SeedUserAsync(provider, "hein.min", []);
        var session = await SessionOfAsync(provider, "hein.min");

        var principal = await ResolveAsync(provider, userId, session);

        Assert.Empty(principal!.Roles);
        Assert.Empty(principal.Permissions);
    }

    [Fact]
    public async Task GetCurrentUser_ReturnsUserNameRolesAndPermissions()
    {
        await using var seed = BuildProvider();
        var userId = await SeedUserAsync(seed, "hein.min", [RoleNames.RailwayAdministrator]);

        await using var provider = BuildProvider(As(userId));
        var me = await WithScopeAsync(provider, services =>
            services.GetRequiredService<GetCurrentUserHandler>().Handle(new GetCurrentUserQuery(), CancellationToken));

        Assert.NotNull(me);
        Assert.Equal(userId, me.UserId);
        Assert.Equal("hein.min", me.UserName);
        Assert.Equal([RoleNames.RailwayAdministrator], me.Roles);
        Assert.Equal(["stations.manage", "stations.read"], me.Permissions);

        await using var anonymous = BuildProvider();
        Assert.Null(await WithScopeAsync(anonymous, services =>
            services.GetRequiredService<GetCurrentUserHandler>().Handle(new GetCurrentUserQuery(), CancellationToken)));
    }

    /// <summary>S31: after sign-in and refresh no raw token or password is stored anywhere in identity.</summary>
    [Fact]
    public async Task IdentityTables_AfterSignInAndRefresh_ContainNoRawTokenOrPassword()
    {
        await using var provider = BuildProvider();
        await SeedUserAsync(provider, "hein.min", [RoleNames.StationManager]);
        var login = (await LoginAsync(provider, "hein.min")).Value;
        var refreshed = (await WithScopeAsync(provider, services =>
            services.GetRequiredService<RefreshSessionHandler>().Handle(new RefreshSessionCommand(login.RefreshToken), CancellationToken))).Value;

        foreach (var secret in new[] { Password, login.RefreshToken, refreshed.RefreshToken, login.AccessToken })
        {
            Assert.Equal(0, await ScalarAsync<int>(
                $"""
                 SELECT
                   (SELECT COUNT(*) FROM [identity].[Users] WHERE CONCAT([PasswordHash], [SecurityStamp], [UserName]) LIKE N'%{secret}%')
                 + (SELECT COUNT(*) FROM [identity].[RefreshTokens] WHERE CONVERT(nvarchar(max), [TokenHash], 1) LIKE N'%{secret}%' OR CAST([TokenHash] AS varbinary(32)) = CAST(N'{secret}' AS varbinary(max)))
                 + (SELECT COUNT(*) FROM [audit].[AuditEvents] WHERE CONCAT([BeforeJson], [AfterJson], [ActorRole]) LIKE N'%{secret}%');
                 """));
        }
    }

    private async Task<Guid> SessionOfAsync(ServiceProvider provider, string userName)
    {
        var login = await LoginAsync(provider, userName);
        Assert.True(login.IsSuccess);
        return FakeAccessTokenIssuer.Parse(login.Value.AccessToken).SessionId;
    }

    private Task<Result> ChangeAsync(ServiceProvider provider, Guid session, string current, string next) =>
        WithScopeAsync(provider, services =>
            services.GetRequiredService<ChangeOwnPasswordHandler>().Handle(new ChangeOwnPasswordCommand(session, current, next), CancellationToken));

    private Task<SessionPrincipal?> ResolveAsync(ServiceProvider provider, Guid userId, Guid sessionId) =>
        WithScopeAsync(provider, services =>
            services.GetRequiredService<ResolveSessionPrincipalHandler>().Handle(new ResolveSessionPrincipalQuery(userId, sessionId), CancellationToken));

    private static TestCurrentUser As(Guid userId, params string[] roles) =>
        new() { UserId = userId, Roles = roles, CorrelationId = "corr-session" };
}

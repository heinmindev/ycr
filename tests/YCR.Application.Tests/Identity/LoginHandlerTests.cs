using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Tests.Network;
using YCR.Application.Identity;
using YCR.Domain.Identity;

namespace YCR.Application.Tests.Identity;

/// <summary>
/// Sign-in (spec S1–S4, S27, S31; R12, R18, R22, R23) against real SQL Server under <c>ycr_app</c>.
/// </summary>
/// <remarks>
/// Equalised timing is asserted by counting full hash verifications on the registered hasher, not
/// by measuring time (spec S2; hein, 2026-09-23: one test per case in the unknown-user style).
/// </remarks>
public sealed class LoginHandlerTests(SqlServerFixture fixture) : IdentityHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "login";

    [Fact]
    public async Task Handle_WithValidCredentials_CreatesSessionWithHashedTokenAndAuditsLoginSucceeded()
    {
        await using var provider = BuildProvider();
        var userId = await SeedUserAsync(provider, "hein.min", [RoleNames.StationManager]);
        var now = Clock.GetUtcNow();

        var result = await LoginAsync(provider, "hein.min");

        Assert.True(result.IsSuccess);
        var (tokenUser, sessionId) = FakeAccessTokenIssuer.Parse(result.Value.AccessToken);
        Assert.Equal(userId, tokenUser);
        Assert.Equal(now.AddMinutes(15), result.Value.AccessTokenExpiresAtUtc);
        Assert.Equal(now.AddHours(12), result.Value.SessionExpiresAtUtc);

        Assert.Equal(1, await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM [identity].[AuthSessions] WHERE [Id] = '{sessionId}' AND [UserId] = '{userId}' AND [RevokedAtUtc] IS NULL AND [ExpiresAtUtc] = DATEADD(HOUR, 12, [CreatedAtUtc]);"));

        // S1 / S31: only the SHA-256 of the raw token is stored.
        var storedHash = await ScalarAsync<byte[]>($"SELECT [TokenHash] FROM [identity].[RefreshTokens] WHERE [SessionId] = '{sessionId}';");
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(result.Value.RefreshToken)), storedHash);

        var row = Assert.Single(await AuditRowsAsync(IdentityAuditActions.LoginSucceeded));
        Assert.Equal(IdentityAuditSubjects.User, row.SubjectType);
        Assert.Equal(userId, row.SubjectId);
        Assert.Contains(sessionId.ToString(), row.AfterJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, (await LockoutStateAsync(userId)).Count);
    }

    [Fact]
    public async Task Handle_WithWrongPassword_ReturnsInvalidCredentialsAndAuditsLoginFailed()
    {
        await using var provider = BuildProvider();
        var userId = await SeedUserAsync(provider, "hein.min", [RoleNames.StationManager]);

        var result = await LoginAsync(provider, "hein.min", "kyauk.tan.13");

        Assert.Equal(IdentityErrors.InvalidCredentials, result.Error);
        Assert.Equal(1, Hasher.Verifications);
        var row = Assert.Single(await AuditRowsAsync(IdentityAuditActions.LoginFailed));
        Assert.Equal(userId, row.SubjectId);
        Assert.Null(row.ActorUserId);
        Assert.Null(row.AfterJson);
        Assert.Equal(1, (await LockoutStateAsync(userId)).Count);
        Assert.Equal(0, await ScalarAsync<int>("SELECT COUNT(*) FROM [identity].[AuthSessions];"));
    }

    [Fact]
    public async Task Handle_WithUnknownUser_VerifiesDummyHashAndAuditsNullSubject()
    {
        await using var provider = BuildProvider();

        var result = await LoginAsync(provider, "no.such.user", "whatever.typed.here");

        Assert.Equal(IdentityErrors.InvalidCredentials, result.Error);
        Assert.Equal(1, Hasher.Verifications);
        var row = Assert.Single(await AuditRowsAsync(IdentityAuditActions.LoginFailed));
        Assert.Null(row.SubjectId);
        Assert.Null(row.ActorUserId);
        Assert.Null(row.BeforeJson);
        Assert.Null(row.AfterJson);
        // R22: nothing the caller typed is stored anywhere in the row.
        Assert.Equal(0, await ScalarAsync<int>(
            "SELECT COUNT(*) FROM [audit].[AuditEvents] WHERE CONCAT([ActorRole], [BeforeJson], [AfterJson], [ReasonCode]) LIKE N'%no.such.user%' OR CONCAT([BeforeJson], [AfterJson]) LIKE N'%whatever%';"));
    }

    /// <summary>
    /// hein, 2026-09-23 (ADR-0023 item 3): a disabled account with the correct password gets the
    /// uniform 401 after a full hash verification — no early return — and its failure count is
    /// neither incremented nor reset.
    /// </summary>
    [Fact]
    public async Task Handle_WithDisabledUserAndCorrectPassword_ReturnsInvalidCredentials()
    {
        await using var provider = BuildProvider();
        var userId = await SeedUserAsync(provider, "hein.min", [RoleNames.StationManager]);
        await LoginAsync(provider, "hein.min", "wrong.password.1");
        await LoginAsync(provider, "hein.min", "wrong.password.2");
        await ExecuteAsMigratorAsync($"UPDATE [identity].[Users] SET [IsDisabled] = 1, [DisabledAtUtc] = SYSUTCDATETIME() AT TIME ZONE 'UTC' WHERE [Id] = '{userId}';");
        Hasher.Reset();

        var result = await LoginAsync(provider, "hein.min");

        Assert.Equal(IdentityErrors.InvalidCredentials, result.Error);
        Assert.Equal(1, Hasher.Verifications);
        Assert.Equal((2, (DateTimeOffset?)null), await LockoutStateAsync(userId));
        Assert.Equal(3, (await AuditRowsAsync(IdentityAuditActions.LoginFailed)).Count(row => row.SubjectId == userId));
        Assert.Equal(0, await ScalarAsync<int>("SELECT COUNT(*) FROM [identity].[AuthSessions];"));
    }

    [Fact]
    public async Task Handle_WithDisabledUserAndWrongPassword_VerifiesInFullAndDoesNotCountTheFailure()
    {
        await using var provider = BuildProvider();
        var userId = await SeedUserAsync(provider, "hein.min", [RoleNames.StationManager]);
        await ExecuteAsMigratorAsync($"UPDATE [identity].[Users] SET [IsDisabled] = 1, [DisabledAtUtc] = SYSUTCDATETIME() AT TIME ZONE 'UTC' WHERE [Id] = '{userId}';");

        var result = await LoginAsync(provider, "hein.min", "wrong.password.1");

        Assert.Equal(IdentityErrors.InvalidCredentials, result.Error);
        Assert.Equal(1, Hasher.Verifications);
        Assert.Equal((0, (DateTimeOffset?)null), await LockoutStateAsync(userId));
        Assert.Single(await AuditRowsAsync(IdentityAuditActions.LoginFailed));
    }

    /// <summary>
    /// hein, 2026-09-23 (ADR-0023 item 3): a locked account with the correct password gets the
    /// uniform 401 after a full hash verification; the lockout is not lifted, not extended, and
    /// the count is not reset.
    /// </summary>
    [Fact]
    public async Task Handle_WithLockedUserAndCorrectPassword_ReturnsInvalidCredentials()
    {
        await using var provider = BuildProvider();
        var userId = await SeedUserAsync(provider, "hein.min", [RoleNames.StationManager]);
        await FailAsync(provider, "hein.min", AccountLockoutPolicy.MaxFailedAttempts);
        var lockedState = await LockoutStateAsync(userId);
        Assert.NotNull(lockedState.LockoutEnd);
        Clock.Advance(TimeSpan.FromMinutes(5));
        Hasher.Reset();

        var result = await LoginAsync(provider, "hein.min");

        Assert.Equal(IdentityErrors.InvalidCredentials, result.Error);
        Assert.Equal(1, Hasher.Verifications);
        Assert.Equal(lockedState, await LockoutStateAsync(userId));
        Assert.Equal(0, await ScalarAsync<int>("SELECT COUNT(*) FROM [identity].[AuthSessions];"));
    }

    [Fact]
    public async Task Handle_WithLockedUserAndWrongPassword_VerifiesInFullAndDoesNotCountTheFailure()
    {
        await using var provider = BuildProvider();
        var userId = await SeedUserAsync(provider, "hein.min", [RoleNames.StationManager]);
        await FailAsync(provider, "hein.min", AccountLockoutPolicy.MaxFailedAttempts);
        var lockedState = await LockoutStateAsync(userId);
        Hasher.Reset();

        var result = await LoginAsync(provider, "hein.min", "wrong.password.x");

        Assert.Equal(IdentityErrors.InvalidCredentials, result.Error);
        Assert.Equal(1, Hasher.Verifications);
        Assert.Equal(lockedState, await LockoutStateAsync(userId));
    }

    [Fact]
    public async Task Handle_TenthConsecutiveFailure_AuditsLoginFailedThenLockedOut()
    {
        await using var provider = BuildProvider();
        var userId = await SeedUserAsync(provider, "hein.min", [RoleNames.StationManager]);

        await FailAsync(provider, "hein.min", 9);
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.LockedOut));
        await FailAsync(provider, "hein.min", 1);

        Assert.Equal(10, (await AuditRowsAsync(IdentityAuditActions.LoginFailed)).Count);
        var lockedOut = Assert.Single(await AuditRowsAsync(IdentityAuditActions.LockedOut));
        Assert.Equal(userId, lockedOut.SubjectId);
        Assert.Null(lockedOut.ActorUserId);
        Assert.Null(lockedOut.ActorRole);
        Assert.Contains("\"userName\":\"hein.min\"", lockedOut.AfterJson, StringComparison.Ordinal);
        Assert.Equal((0, (DateTimeOffset?)Clock.GetUtcNow().AddMinutes(15)), await LockoutStateAsync(userId));

        // For the next 15 minutes even the right password is refused (S4).
        Clock.Advance(TimeSpan.FromMinutes(15) - TimeSpan.FromSeconds(1));
        Assert.Equal(IdentityErrors.InvalidCredentials, (await LoginAsync(provider, "hein.min")).Error);
    }

    [Fact]
    public async Task Handle_NineFailuresThenSuccess_ResetsTheCount()
    {
        await using var provider = BuildProvider();
        var userId = await SeedUserAsync(provider, "hein.min", [RoleNames.StationManager]);
        await FailAsync(provider, "hein.min", 9);

        Assert.True((await LoginAsync(provider, "hein.min")).IsSuccess);

        Assert.Equal(0, (await LockoutStateAsync(userId)).Count);
    }

    [Fact]
    public async Task Handle_AfterLockoutExpires_Succeeds()
    {
        await using var provider = BuildProvider();
        await SeedUserAsync(provider, "hein.min", [RoleNames.StationManager]);
        await FailAsync(provider, "hein.min", AccountLockoutPolicy.MaxFailedAttempts);

        Clock.Advance(TimeSpan.FromMinutes(15));

        Assert.True((await LoginAsync(provider, "hein.min")).IsSuccess);
    }

    [Fact]
    public async Task Handle_WithUnknownUser_NeverLocksAnything()
    {
        await using var provider = BuildProvider();
        var userId = await SeedUserAsync(provider, "hein.min", [RoleNames.StationManager]);

        await FailAsync(provider, "hein.mim", AccountLockoutPolicy.MaxFailedAttempts + 2);

        Assert.Equal((0, (DateTimeOffset?)null), await LockoutStateAsync(userId));
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.LockedOut));
    }

    [Fact]
    public async Task Handle_WithParallelWrongPasswords_CountsEveryFailure()
    {
        await using var provider = BuildProvider();
        var userId = await SeedUserAsync(provider, "hein.min", [RoleNames.StationManager]);

        var attempts = Enumerable.Range(0, 6).Select(_ => LoginAsync(provider, "hein.min", "wrong.password.1"));
        var results = await Task.WhenAll(attempts);

        Assert.All(results, result => Assert.Equal(IdentityErrors.InvalidCredentials, result.Error));
        Assert.Equal(6, (await LockoutStateAsync(userId)).Count);
        Assert.Equal(6, (await AuditRowsAsync(IdentityAuditActions.LoginFailed)).Count);
    }

    [Fact]
    public async Task Handle_UserNameInOtherCase_MatchesTheNormalizedName()
    {
        await using var provider = BuildProvider();
        await SeedUserAsync(provider, "hein.min", [RoleNames.StationManager]);

        Assert.True((await LoginAsync(provider, "Hein.Min")).IsSuccess);
    }

    /// <summary>S27 / U4: the actor of LoginSucceeded is the user just verified, with their roles.</summary>
    [Fact]
    public async Task Handle_LoginSucceeded_AuditActorIsSignedInUserWithRoles()
    {
        var ambient = new TestCurrentUser
        {
            UserId = Guid.NewGuid(),
            Roles = [RoleNames.SystemAdministrator],
            CorrelationId = "corr-login",
            AuthorizedByPermission = "users.manage",
        };
        await using var provider = BuildProvider(ambient);
        var userId = await SeedUserAsync(provider, "hein.min", [RoleNames.TicketOperator, RoleNames.StationManager]);

        Assert.True((await LoginAsync(provider, "hein.min")).IsSuccess);

        var row = Assert.Single(await AuditRowsAsync(IdentityAuditActions.LoginSucceeded));
        Assert.Equal(userId, row.ActorUserId);
        Assert.Equal("""["StationManager","TicketOperator"]""", row.ActorRole);
        Assert.Null(row.AuthorizedByPermission);
    }

    /// <summary>S27 / U4: failures and lockouts have no actor, whatever principal the request carried.</summary>
    [Fact]
    public async Task Handle_LoginFailedWithAmbientPrincipal_AuditActorIsNull()
    {
        var ambient = new TestCurrentUser
        {
            UserId = Guid.NewGuid(),
            Roles = [RoleNames.SystemAdministrator],
            CorrelationId = "corr-login",
            AuthorizedByPermission = "users.manage",
        };
        await using var provider = BuildProvider(ambient);
        await SeedUserAsync(provider, "hein.min", [RoleNames.StationManager]);

        await FailAsync(provider, "hein.min", AccountLockoutPolicy.MaxFailedAttempts);
        await LoginAsync(provider, "no.such.user");

        var rows = (await AuditRowsAsync(IdentityAuditActions.LoginFailed)).Concat(await AuditRowsAsync(IdentityAuditActions.LockedOut)).ToList();
        Assert.Equal(12, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.Null(row.ActorUserId);
            Assert.Null(row.ActorRole);
            Assert.Null(row.AuthorizedByPermission);
        });
    }

    private async Task FailAsync(ServiceProvider provider, string userName, int times)
    {
        for (var attempt = 0; attempt < times; attempt++)
        {
            Assert.Equal(IdentityErrors.InvalidCredentials, (await LoginAsync(provider, userName, $"wrong.password.{attempt}")).Error);
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Common.Pagination;
using YCR.Application.Identity;
using YCR.Application.Identity.BootstrapAdministrator;
using YCR.Application.Identity.CreateUser;
using YCR.Application.Identity.DisableUser;
using YCR.Application.Identity.EnableUser;
using YCR.Application.Identity.GetUser;
using YCR.Application.Identity.ListRoles;
using YCR.Application.Identity.ListUsers;
using YCR.Application.Identity.ListUserSessions;
using YCR.Application.Identity.ReplaceUserRoles;
using YCR.Application.Identity.ResetUserPassword;
using YCR.Application.Identity.RevokeSession;
using YCR.Application.Tests.Network;
using YCR.Domain.Common;
using YCR.Domain.Identity;

namespace YCR.Application.Tests.Identity;

/// <summary>
/// The administration handlers of spec §6.2 and the bootstrap (D10): S4, S17, S18, S19–S19e,
/// S32, S32a; R20, R22, R26, R27; G1, G2. Real SQL Server under <c>ycr_app</c>.
/// </summary>
public sealed class AdministrationHandlerTests(SqlServerFixture fixture) : IdentityHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "administration";

    // CreateUser

    [Fact]
    public async Task CreateUser_WithValidCommand_CreatesMustChangeUserAndAudits()
    {
        var admin = Guid.NewGuid();
        await using var provider = BuildProvider(Admin(admin));

        var result = await CreateAsync(provider, "new.operator", [RoleNames.TicketOperator]);

        Assert.True(result.IsSuccess);
        Assert.True(await ScalarAsync<bool>($"SELECT [MustChangePassword] FROM [identity].[Users] WHERE [Id] = '{result.Value}';"));
        var row = Assert.Single(await AuditRowsAsync(IdentityAuditActions.UserCreated));
        Assert.Equal(result.Value, row.SubjectId);
        Assert.Equal(admin, row.ActorUserId);
        Assert.Equal("""["SystemAdministrator"]""", row.ActorRole);
        Assert.Equal("users.manage", row.AuthorizedByPermission);
        Assert.Contains("\"userName\":\"new.operator\"", row.AfterJson, StringComparison.Ordinal);
        Assert.Contains("\"roles\":[\"TicketOperator\"]", row.AfterJson, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, row.AfterJson, StringComparison.Ordinal);
        Assert.DoesNotContain("passwordHash", row.AfterJson, StringComparison.OrdinalIgnoreCase);

        // U2: the account signs in, and its session is must-change.
        var login = await LoginAsync(provider, "new.operator");
        Assert.True(login.IsSuccess);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("New.Operator")]
    [InlineData("new_operator")]
    public async Task CreateUser_WithInvalidUserName_CreatesNothing(string userName)
    {
        await using var provider = BuildProvider(Admin(Guid.NewGuid()));

        var result = await CreateAsync(provider, userName, [RoleNames.TicketOperator]);

        Assert.Equal(IdentityErrors.InvalidUserName, result.Error);
        Assert.Equal(0, await ScalarAsync<int>("SELECT COUNT(*) FROM [identity].[Users];"));
    }

    [Fact]
    public async Task CreateUser_WithUnknownRole_CreatesNothing()
    {
        await using var provider = BuildProvider(Admin(Guid.NewGuid()));

        var result = await CreateAsync(provider, "new.operator", ["Superuser"]);

        Assert.Equal(IdentityErrors.UnknownRole, result.Error);
        Assert.Equal(0, await ScalarAsync<int>("SELECT COUNT(*) FROM [identity].[Users];"));
    }

    [Fact]
    public async Task CreateUser_WithDuplicateUserName_ReturnsConflict()
    {
        await using var provider = BuildProvider(Admin(Guid.NewGuid()));
        Assert.True((await CreateAsync(provider, "new.operator", [])).IsSuccess);

        var second = await CreateAsync(provider, "new.operator", []);

        Assert.Equal(IdentityErrors.UserNameAlreadyExists, second.Error);
        Assert.Single(await AuditRowsAsync(IdentityAuditActions.UserCreated));
    }

    [Fact]
    public async Task CreateUser_WithParallelDuplicateUserNames_CreatesOne()
    {
        await using var provider = BuildProvider(Admin(Guid.NewGuid()));

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => CreateAsync(provider, "race.user", [])));

        Assert.Single(results, result => result.IsSuccess);
        Assert.All(results.Where(result => result.IsFailure), result => Assert.Equal(IdentityErrors.UserNameAlreadyExists, result.Error));
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM [identity].[Users];"));
        Assert.Single(await AuditRowsAsync(IdentityAuditActions.UserCreated));
    }

    [Theory]
    [InlineData("short.pass1")]
    [InlineData("passwordpassword")]
    public async Task CreateUser_WithPolicyViolation_ReturnsIdentityPasswordRejected(string password)
    {
        await using var provider = BuildProvider(Admin(Guid.NewGuid()));

        var result = await CreateAsync(provider, "new.operator", [], password);

        Assert.Equal(IdentityErrors.IdentityPasswordRejected, result.Error);
        Assert.Equal("Identity.PasswordRejected", result.Error.Code);
        Assert.Equal(0, await ScalarAsync<int>("SELECT COUNT(*) FROM [identity].[Users];"));
    }

    // DisableUser / EnableUser

    [Fact]
    public async Task DisableUser_RevokesAllSessionsAndAudits()
    {
        await using var seed = BuildProvider();
        var target = await SeedUserAsync(seed, "target.user", [RoleNames.StationManager]);
        await LoginAsync(seed, "target.user");
        await LoginAsync(seed, "target.user");
        var admin = Guid.NewGuid();
        await using var provider = BuildProvider(Admin(admin));

        var result = await RunAsync<DisableUserHandler, Result>(provider, handler => handler.Handle(new DisableUserCommand(target), CancellationToken));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, await ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].[AuthSessions] WHERE [UserId] = '{target}' AND [RevocationReason] = N'UserDisabled';"));
        var row = Assert.Single(await AuditRowsAsync(IdentityAuditActions.UserDisabled));
        Assert.Equal(target, row.SubjectId);
        Assert.Equal(admin, row.ActorUserId);
        Assert.Contains("\"isDisabled\":false", row.BeforeJson, StringComparison.Ordinal);
        Assert.Contains("\"isDisabled\":true", row.AfterJson, StringComparison.Ordinal);
        Assert.Equal(IdentityErrors.InvalidCredentials, (await LoginAsync(seed, "target.user")).Error);
    }

    [Fact]
    public async Task DisableUser_Self_ReturnsCannotDisableOwnAccount()
    {
        await using var seed = BuildProvider();
        var admin = await SeedUserAsync(seed, "admin.one", [RoleNames.SystemAdministrator]);
        await using var provider = BuildProvider(Admin(admin));

        var result = await RunAsync<DisableUserHandler, Result>(provider, handler => handler.Handle(new DisableUserCommand(admin), CancellationToken));

        Assert.Equal(IdentityErrors.CannotDisableOwnAccount, result.Error);
        Assert.False(await ScalarAsync<bool>($"SELECT [IsDisabled] FROM [identity].[Users] WHERE [Id] = '{admin}';"));
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.UserDisabled));
    }

    /// <summary>G2 (hein): disabling a disabled account, enabling an active one — 204, no change, no audit.</summary>
    [Fact]
    public async Task DisableAndEnable_Repeated_AreNoOpsWithoutAudit()
    {
        await using var seed = BuildProvider();
        var target = await SeedUserAsync(seed, "target.user", [RoleNames.StationManager]);
        await using var provider = BuildProvider(Admin(Guid.NewGuid()));

        Assert.True((await RunAsync<EnableUserHandler, Result>(provider, handler => handler.Handle(new EnableUserCommand(target), CancellationToken))).IsSuccess);
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.UserEnabled));

        Assert.True((await RunAsync<DisableUserHandler, Result>(provider, handler => handler.Handle(new DisableUserCommand(target), CancellationToken))).IsSuccess);
        var disabledAt = await ScalarAsync<DateTimeOffset>($"SELECT [DisabledAtUtc] FROM [identity].[Users] WHERE [Id] = '{target}';");
        Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True((await RunAsync<DisableUserHandler, Result>(provider, handler => handler.Handle(new DisableUserCommand(target), CancellationToken))).IsSuccess);

        Assert.Single(await AuditRowsAsync(IdentityAuditActions.UserDisabled));
        Assert.Equal(disabledAt, await ScalarAsync<DateTimeOffset>($"SELECT [DisabledAtUtc] FROM [identity].[Users] WHERE [Id] = '{target}';"));
    }

    [Fact]
    public async Task EnableUser_WhenDisabled_EnablesAndAudits()
    {
        await using var seed = BuildProvider();
        var target = await SeedUserAsync(seed, "target.user", [RoleNames.StationManager]);
        await using var provider = BuildProvider(Admin(Guid.NewGuid()));
        await RunAsync<DisableUserHandler, Result>(provider, handler => handler.Handle(new DisableUserCommand(target), CancellationToken));

        var result = await RunAsync<EnableUserHandler, Result>(provider, handler => handler.Handle(new EnableUserCommand(target), CancellationToken));

        Assert.True(result.IsSuccess);
        Assert.Single(await AuditRowsAsync(IdentityAuditActions.UserEnabled));
        Assert.True((await LoginAsync(seed, "target.user")).IsSuccess);
    }

    [Fact]
    public async Task DisableUser_UnknownUser_ReturnsNotFound()
    {
        await using var provider = BuildProvider(Admin(Guid.NewGuid()));

        Assert.Equal(IdentityErrors.UserNotFound, (await RunAsync<DisableUserHandler, Result>(provider, handler => handler.Handle(new DisableUserCommand(Guid.NewGuid()), CancellationToken))).Error);
        Assert.Equal(IdentityErrors.UserNotFound, (await RunAsync<EnableUserHandler, Result>(provider, handler => handler.Handle(new EnableUserCommand(Guid.NewGuid()), CancellationToken))).Error);
    }

    // R27 / S19e

    /// <summary>S19e at application level: the self-target rule is bypassed by a caller who is not the target.</summary>
    [Fact]
    public async Task DisableUser_LastActiveAdministrator_ReturnsLastAdministrator()
    {
        await using var seed = BuildProvider();
        var onlyAdmin = await SeedUserAsync(seed, "admin.one", [RoleNames.SystemAdministrator]);
        var disabledAdmin = await SeedUserAsync(seed, "admin.two", [RoleNames.SystemAdministrator]);
        await using var provider = BuildProvider(Admin(Guid.NewGuid()));
        await RunAsync<DisableUserHandler, Result>(provider, handler => handler.Handle(new DisableUserCommand(disabledAdmin), CancellationToken));

        var result = await RunAsync<DisableUserHandler, Result>(provider, handler => handler.Handle(new DisableUserCommand(onlyAdmin), CancellationToken));

        Assert.Equal(IdentityErrors.LastAdministrator, result.Error);
        Assert.False(await ScalarAsync<bool>($"SELECT [IsDisabled] FROM [identity].[Users] WHERE [Id] = '{onlyAdmin}';"));
        Assert.Single(await AuditRowsAsync(IdentityAuditActions.UserDisabled));

        // S19e: enabling and disabling B again is allowed while A remains.
        Assert.True((await RunAsync<EnableUserHandler, Result>(provider, handler => handler.Handle(new EnableUserCommand(disabledAdmin), CancellationToken))).IsSuccess);
        Assert.True((await RunAsync<DisableUserHandler, Result>(provider, handler => handler.Handle(new DisableUserCommand(disabledAdmin), CancellationToken))).IsSuccess);
    }

    [Fact]
    public async Task DisableUser_TwoAdministratorsDisablingEachOtherConcurrently_ExactlyOneSucceeds()
    {
        await using var seed = BuildProvider();
        var a = await SeedUserAsync(seed, "admin.one", [RoleNames.SystemAdministrator]);
        var b = await SeedUserAsync(seed, "admin.two", [RoleNames.SystemAdministrator]);
        await using var asA = BuildProvider(Admin(a));
        await using var asB = BuildProvider(Admin(b));

        var results = await Task.WhenAll(
            RunAsync<DisableUserHandler, Result>(asA, handler => handler.Handle(new DisableUserCommand(b), CancellationToken)),
            RunAsync<DisableUserHandler, Result>(asB, handler => handler.Handle(new DisableUserCommand(a), CancellationToken)));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Equal(IdentityErrors.LastAdministrator, Assert.Single(results, result => result.IsFailure).Error);
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM [identity].[Users] WHERE [IsDisabled] = 0;"));
        Assert.Single(await AuditRowsAsync(IdentityAuditActions.UserDisabled));
    }

    // ReplaceUserRoles

    [Fact]
    public async Task ReplaceUserRoles_ReplacesRolesAndAuditsBeforeAndAfter()
    {
        await using var seed = BuildProvider();
        var target = await SeedUserAsync(seed, "target.user", [RoleNames.StationManager]);
        var admin = Guid.NewGuid();
        await using var provider = BuildProvider(Admin(admin, "users.roles.manage"));

        var result = await ReplaceAsync(provider, target, []);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, await ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].[UserRoles] WHERE [UserId] = '{target}';"));
        var row = Assert.Single(await AuditRowsAsync(IdentityAuditActions.RolesChanged));
        Assert.Equal("""{"roles":["StationManager"]}""", row.BeforeJson);
        Assert.Equal("""{"roles":[]}""", row.AfterJson);
        Assert.Equal(admin, row.ActorUserId);
        Assert.Equal("users.roles.manage", row.AuthorizedByPermission);

        Assert.True((await ReplaceAsync(provider, target, [RoleNames.Auditor, RoleNames.TicketOperator])).IsSuccess);
        Assert.Equal("""{"roles":["TicketOperator","Auditor"]}""", (await AuditRowsAsync(IdentityAuditActions.RolesChanged))[1].AfterJson);
    }

    [Fact]
    public async Task ReplaceUserRoles_Self_ReturnsCannotChangeOwnRoles()
    {
        await using var seed = BuildProvider();
        var admin = await SeedUserAsync(seed, "admin.one", [RoleNames.SystemAdministrator]);
        await using var provider = BuildProvider(Admin(admin, "users.roles.manage"));

        Assert.Equal(IdentityErrors.CannotChangeOwnRoles, (await ReplaceAsync(provider, admin, [RoleNames.SystemAdministrator, RoleNames.Auditor])).Error);
        Assert.Equal(IdentityErrors.CannotChangeOwnRoles, (await ReplaceAsync(provider, admin, ["NotARole"])).Error);
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.RolesChanged));
    }

    [Fact]
    public async Task ReplaceUserRoles_RemovingRoleFromLastAdministrator_ReturnsLastAdministrator()
    {
        await using var seed = BuildProvider();
        var onlyAdmin = await SeedUserAsync(seed, "admin.one", [RoleNames.SystemAdministrator]);
        await using var provider = BuildProvider(Admin(Guid.NewGuid(), "users.roles.manage"));

        var result = await ReplaceAsync(provider, onlyAdmin, [RoleNames.Auditor]);

        Assert.Equal(IdentityErrors.LastAdministrator, result.Error);
        Assert.Equal(1, await ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].[UserRoles] WHERE [UserId] = '{onlyAdmin}';"));
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.RolesChanged));
    }

    [Fact]
    public async Task ReplaceUserRoles_ConcurrentDemotionsOfTheLastTwoAdministrators_ExactlyOneSucceeds()
    {
        await using var seed = BuildProvider();
        var a = await SeedUserAsync(seed, "admin.one", [RoleNames.SystemAdministrator]);
        var b = await SeedUserAsync(seed, "admin.two", [RoleNames.SystemAdministrator]);
        await using var asA = BuildProvider(Admin(a, "users.roles.manage"));
        await using var asB = BuildProvider(Admin(b, "users.roles.manage"));

        var results = await Task.WhenAll(ReplaceAsync(asA, b, []), ReplaceAsync(asB, a, []));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Equal(IdentityErrors.LastAdministrator, Assert.Single(results, result => result.IsFailure).Error);
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM [identity].[UserRoles];"));
    }

    // ResetUserPassword

    [Fact]
    public async Task ResetUserPassword_RevokesAllSessionsSetsMustChangeAndAudits()
    {
        await using var seed = BuildProvider();
        var target = await SeedUserAsync(seed, "target.user", [RoleNames.StationManager]);
        await LoginAsync(seed, "target.user");
        await using var provider = BuildProvider(Admin(Guid.NewGuid()));

        var result = await ResetAsync(provider, target, "reset.password.1");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].[AuthSessions] WHERE [UserId] = '{target}' AND [RevocationReason] = N'AdministratorPasswordReset';"));
        Assert.True(await ScalarAsync<bool>($"SELECT [MustChangePassword] FROM [identity].[Users] WHERE [Id] = '{target}';"));
        var row = Assert.Single(await AuditRowsAsync(IdentityAuditActions.PasswordReset));
        Assert.DoesNotContain("reset.password", row.AfterJson, StringComparison.Ordinal);
        Assert.Equal(IdentityErrors.InvalidCredentials, (await LoginAsync(seed, "target.user")).Error);
        Assert.True((await LoginAsync(seed, "target.user", "reset.password.1")).IsSuccess);
    }

    [Fact]
    public async Task ResetUserPassword_Self_ReturnsCannotResetOwnPassword()
    {
        await using var seed = BuildProvider();
        var admin = await SeedUserAsync(seed, "admin.one", [RoleNames.SystemAdministrator]);
        await using var provider = BuildProvider(Admin(admin));

        Assert.Equal(IdentityErrors.CannotResetOwnPassword, (await ResetAsync(provider, admin, "reset.password.1")).Error);
        Assert.False(await ScalarAsync<bool>($"SELECT [MustChangePassword] FROM [identity].[Users] WHERE [Id] = '{admin}';"));
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.PasswordReset));
    }

    [Fact]
    public async Task ResetUserPassword_WithPolicyViolation_ReturnsIdentityPasswordRejected()
    {
        await using var seed = BuildProvider();
        var target = await SeedUserAsync(seed, "target.user", [RoleNames.StationManager]);
        var session = FakeAccessTokenIssuer.Parse((await LoginAsync(seed, "target.user")).Value.AccessToken).SessionId;
        await using var provider = BuildProvider(Admin(Guid.NewGuid()));

        Assert.Equal(IdentityErrors.IdentityPasswordRejected, (await ResetAsync(provider, target, "123456789012")).Error);
        Assert.False(await ScalarAsync<bool>($"SELECT [MustChangePassword] FROM [identity].[Users] WHERE [Id] = '{target}';"));
        Assert.Equal(1, await ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].[AuthSessions] WHERE [Id] = '{session}' AND [RevokedAtUtc] IS NULL;"));
    }

    // Unlock (U3)

    [Fact]
    public async Task UnlockUser_WhenLocked_ClearsAndAudits()
    {
        await using var seed = BuildProvider();
        var target = await SeedUserAsync(seed, "target.user", [RoleNames.StationManager]);
        for (var attempt = 0; attempt < AccountLockoutPolicy.MaxFailedAttempts; attempt++)
        {
            await LoginAsync(seed, "target.user", $"wrong.password.{attempt}");
        }

        await using var provider = BuildProvider(Admin(Guid.NewGuid()));
        var result = await RunAsync<YCR.Application.Identity.UnlockUser.UnlockUserHandler, Result>(provider, handler =>
            handler.Handle(new YCR.Application.Identity.UnlockUser.UnlockUserCommand(target), CancellationToken));

        Assert.True(result.IsSuccess);
        Assert.Equal((0, (DateTimeOffset?)null), await LockoutStateAsync(target));
        Assert.Single(await AuditRowsAsync(IdentityAuditActions.UserUnlocked));
        Assert.True((await LoginAsync(seed, "target.user")).IsSuccess);
    }

    [Fact]
    public async Task UnlockUser_WhenNotLocked_ChangesNothingAndWritesNoAudit()
    {
        await using var seed = BuildProvider();
        var target = await SeedUserAsync(seed, "target.user", [RoleNames.StationManager]);
        await LoginAsync(seed, "target.user", "wrong.password.1");
        await using var provider = BuildProvider(Admin(Guid.NewGuid()));

        var result = await RunAsync<YCR.Application.Identity.UnlockUser.UnlockUserHandler, Result>(provider, handler =>
            handler.Handle(new YCR.Application.Identity.UnlockUser.UnlockUserCommand(target), CancellationToken));

        Assert.True(result.IsSuccess);
        Assert.Equal((1, (DateTimeOffset?)null), await LockoutStateAsync(target));
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.UserUnlocked));
    }

    [Fact]
    public async Task UnlockUser_UnknownUser_ReturnsNotFound()
    {
        await using var provider = BuildProvider(Admin(Guid.NewGuid()));

        var result = await RunAsync<YCR.Application.Identity.UnlockUser.UnlockUserHandler, Result>(provider, handler =>
            handler.Handle(new YCR.Application.Identity.UnlockUser.UnlockUserCommand(Guid.NewGuid()), CancellationToken));

        Assert.Equal(IdentityErrors.UserNotFound, result.Error);
    }

    // Sessions, roles, users

    [Fact]
    public async Task ListUserSessions_ReturnsPagedSessionsWithReasons()
    {
        await using var seed = BuildProvider();
        var target = await SeedUserAsync(seed, "target.user", [RoleNames.StationManager]);
        var first = FakeAccessTokenIssuer.Parse((await LoginAsync(seed, "target.user")).Value.AccessToken).SessionId;
        Clock.Advance(TimeSpan.FromMinutes(1));
        await LoginAsync(seed, "target.user");
        await ExecuteAsMigratorAsync($"UPDATE [identity].[AuthSessions] SET [RevokedAtUtc] = SYSUTCDATETIME() AT TIME ZONE 'UTC', [RevocationReason] = N'Logout' WHERE [Id] = '{first}';");
        await using var provider = BuildProvider(Admin(Guid.NewGuid(), "users.read"));

        var page = await RunAsync<ListUserSessionsHandler, Result<PagedResult<AuthSessionDto>>>(provider, handler =>
            handler.Handle(new ListUserSessionsQuery(target, 1, 1), CancellationToken));

        Assert.Equal(2, page.Value.TotalCount);
        Assert.Null(Assert.Single(page.Value.Items).RevocationReason);
        var second = await RunAsync<ListUserSessionsHandler, Result<PagedResult<AuthSessionDto>>>(provider, handler =>
            handler.Handle(new ListUserSessionsQuery(target, 2, 1), CancellationToken));
        Assert.Equal("Logout", Assert.Single(second.Value.Items).RevocationReason);

        Assert.Equal(IdentityErrors.UserNotFound, (await RunAsync<ListUserSessionsHandler, Result<PagedResult<AuthSessionDto>>>(provider, handler =>
            handler.Handle(new ListUserSessionsQuery(Guid.NewGuid()), CancellationToken))).Error);
    }

    /// <summary>S19 / G1: the named session only; the audit subject is the user, the session id and reason in AfterJson.</summary>
    [Fact]
    public async Task RevokeSession_RevokesNamedSessionOnlyAndAudits()
    {
        await using var seed = BuildProvider();
        var target = await SeedUserAsync(seed, "target.user", [RoleNames.StationManager]);
        var a = FakeAccessTokenIssuer.Parse((await LoginAsync(seed, "target.user")).Value.AccessToken).SessionId;
        var b = FakeAccessTokenIssuer.Parse((await LoginAsync(seed, "target.user")).Value.AccessToken).SessionId;
        var admin = Guid.NewGuid();
        await using var provider = BuildProvider(Admin(admin, "auth-sessions.revoke"));

        var result = await RunAsync<RevokeSessionHandler, Result>(provider, handler => handler.Handle(new RevokeSessionCommand(a), CancellationToken));
        var again = await RunAsync<RevokeSessionHandler, Result>(provider, handler => handler.Handle(new RevokeSessionCommand(a), CancellationToken));

        Assert.True(result.IsSuccess);
        Assert.True(again.IsSuccess);
        Assert.Equal("AdministratorRevoked", await ScalarAsync<string>($"SELECT [RevocationReason] FROM [identity].[AuthSessions] WHERE [Id] = '{a}';"));
        Assert.Equal(1, await ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].[AuthSessions] WHERE [Id] = '{b}' AND [RevokedAtUtc] IS NULL;"));
        var row = Assert.Single(await AuditRowsAsync(IdentityAuditActions.SessionRevoked));
        Assert.Equal(IdentityAuditSubjects.User, row.SubjectType);
        Assert.Equal(target, row.SubjectId);
        Assert.Equal(admin, row.ActorUserId);
        Assert.Equal("auth-sessions.revoke", row.AuthorizedByPermission);
        Assert.Contains($"\"sessionId\":\"{a}\"", row.AfterJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"revocationReason\":\"AdministratorRevoked\"", row.AfterJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RevokeSession_UnknownSession_ReturnsNotFound()
    {
        await using var provider = BuildProvider(Admin(Guid.NewGuid(), "auth-sessions.revoke"));

        Assert.Equal(IdentityErrors.SessionNotFound, (await RunAsync<RevokeSessionHandler, Result>(provider, handler =>
            handler.Handle(new RevokeSessionCommand(Guid.NewGuid()), CancellationToken))).Error);
    }

    [Fact]
    public async Task ListRoles_ReturnsEightRolesWithPermissions()
    {
        await using var provider = BuildProvider(Admin(Guid.NewGuid(), "users.read"));

        var roles = await RunAsync<ListRolesHandler, IReadOnlyList<RoleDto>>(provider, handler => handler.Handle(new ListRolesQuery(), CancellationToken));

        Assert.Equal(RoleNames.All, roles.Select(role => role.Name));
        // F-003 adds the route grants to these roles (docs/10 §Route permission grants, OQ40), and
        // F-004 the service grants (docs/10 §Service permission grants, OQ49).
        Assert.Equal(
            ["auth-sessions.revoke", "routes.manage", "routes.read", "services.manage", "services.read", "stations.manage", "stations.read", "users.manage", "users.read", "users.roles.manage"],
            roles[0].Permissions);
        Assert.Equal(["routes.read", "services.read", "stations.read"], roles.Single(role => role.Name == RoleNames.ReportingUser).Permissions);
    }

    [Fact]
    public async Task ListUsers_ReturnsPagedEnvelope()
    {
        await using var seed = BuildProvider();
        await SeedUserAsync(seed, "charlie.user", [RoleNames.Auditor]);
        await SeedUserAsync(seed, "alpha.user", [RoleNames.StationManager, RoleNames.SystemAdministrator]);
        await SeedUserAsync(seed, "bravo.user", []);
        await using var provider = BuildProvider(Admin(Guid.NewGuid(), "users.read"));

        var page = await RunAsync<ListUsersHandler, Result<PagedResult<UserDto>>>(provider, handler => handler.Handle(new ListUsersQuery(1, 2), CancellationToken));

        Assert.Equal(3, page.Value.TotalCount);
        Assert.Equal(["alpha.user", "bravo.user"], page.Value.Items.Select(user => user.UserName));
        Assert.Equal([RoleNames.SystemAdministrator, RoleNames.StationManager], page.Value.Items[0].Roles);
        Assert.Equal(IdentityErrors.InvalidPageRequest, (await RunAsync<ListUsersHandler, Result<PagedResult<UserDto>>>(provider, handler =>
            handler.Handle(new ListUsersQuery(0, 2), CancellationToken))).Error);
    }

    [Fact]
    public async Task GetUser_ShowsLockoutAndUnknownIdReturnsNotFound()
    {
        await using var seed = BuildProvider();
        var target = await SeedUserAsync(seed, "target.user", [RoleNames.StationManager]);
        for (var attempt = 0; attempt < AccountLockoutPolicy.MaxFailedAttempts; attempt++)
        {
            await LoginAsync(seed, "target.user", $"wrong.password.{attempt}");
        }

        await using var provider = BuildProvider(Admin(Guid.NewGuid(), "users.read"));

        var user = await RunAsync<GetUserHandler, Result<UserDto>>(provider, handler => handler.Handle(new GetUserQuery(target), CancellationToken));
        Assert.Equal(Clock.GetUtcNow().AddMinutes(15), user.Value.LockedUntilUtc);
        Clock.Advance(TimeSpan.FromMinutes(15));
        user = await RunAsync<GetUserHandler, Result<UserDto>>(provider, handler => handler.Handle(new GetUserQuery(target), CancellationToken));
        Assert.Null(user.Value.LockedUntilUtc);

        Assert.Equal(IdentityErrors.UserNotFound, (await RunAsync<GetUserHandler, Result<UserDto>>(provider, handler =>
            handler.Handle(new GetUserQuery(Guid.NewGuid()), CancellationToken))).Error);
    }

    // Bootstrap (D10, S32)

    [Fact]
    public async Task Bootstrap_WithNoAdministrator_CreatesMustChangeAdministratorAndAuditsNullActor()
    {
        var ambient = new TestCurrentUser { UserId = Guid.NewGuid(), Roles = [RoleNames.Auditor], CorrelationId = "corr-bootstrap" };
        await using var provider = BuildProvider(ambient);

        var result = await BootstrapAsync(provider, "hein");

        Assert.True(result.IsSuccess);
        Assert.True(await ScalarAsync<bool>($"SELECT [MustChangePassword] FROM [identity].[Users] WHERE [Id] = '{result.Value}';"));
        Assert.Equal(1, await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM [identity].[UserRoles] AS ur JOIN [identity].[Roles] AS r ON r.[Id] = ur.[RoleId] WHERE ur.[UserId] = '{result.Value}' AND r.[Name] = N'SystemAdministrator';"));
        var row = Assert.Single(await AuditRowsAsync(IdentityAuditActions.UserCreated));
        Assert.Null(row.ActorUserId);
        Assert.Null(row.ActorRole);
        Assert.Null(row.AuthorizedByPermission);
        Assert.Equal(result.Value, row.SubjectId);
    }

    [Fact]
    public async Task Bootstrap_WhenAnAdministratorExists_RefusesAndWritesNothing()
    {
        await using var seed = BuildProvider();
        var existing = await SeedUserAsync(seed, "admin.one", [RoleNames.SystemAdministrator]);
        await ExecuteAsMigratorAsync($"UPDATE [identity].[Users] SET [IsDisabled] = 1, [DisabledAtUtc] = SYSUTCDATETIME() AT TIME ZONE 'UTC' WHERE [Id] = '{existing}';");

        var result = await BootstrapAsync(seed, "hein");

        Assert.Equal(IdentityErrors.AdministratorAlreadyExists, result.Error);
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM [identity].[Users];"));
        Assert.Equal(0, await AuditCountAsync());
    }

    [Fact]
    public async Task Bootstrap_TwoConcurrentRuns_CreateOneAdministrator()
    {
        await using var provider = BuildProvider();

        var results = await Task.WhenAll(BootstrapAsync(provider, "hein"), BootstrapAsync(provider, "hein.min"));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Equal(IdentityErrors.AdministratorAlreadyExists, Assert.Single(results, result => result.IsFailure).Error);
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM [identity].[Users];"));
        Assert.Single(await AuditRowsAsync(IdentityAuditActions.UserCreated));
    }

    [Theory]
    [InlineData("Hein", Password)]
    [InlineData("hein", "short")]
    public async Task Bootstrap_InvalidUserNameOrPassword_CreatesNothing(string userName, string password)
    {
        await using var provider = BuildProvider();

        var result = await BootstrapAsync(provider, userName, password);

        Assert.True(result.IsFailure);
        Assert.Equal(0, await ScalarAsync<int>("SELECT COUNT(*) FROM [identity].[Users];"));
        Assert.Equal(0, await AuditCountAsync());
    }

    private static TestCurrentUser Admin(Guid id, string permission = "users.manage") => new()
    {
        UserId = id,
        Roles = [RoleNames.SystemAdministrator],
        CorrelationId = "corr-admin",
        AuthorizedByPermission = permission,
    };

    private async Task<TResult> RunAsync<THandler, TResult>(ServiceProvider provider, Func<THandler, Task<TResult>> call)
        where THandler : notnull
    {
        await using var scope = provider.CreateAsyncScope();
        return await call(scope.ServiceProvider.GetRequiredService<THandler>());
    }

    private Task<Result<Guid>> CreateAsync(ServiceProvider provider, string userName, string[] roles, string password = Password) =>
        RunAsync<CreateUserHandler, Result<Guid>>(provider, handler => handler.Handle(new CreateUserCommand(userName, password, roles), CancellationToken));

    private Task<Result> ReplaceAsync(ServiceProvider provider, Guid userId, string[] roles) =>
        RunAsync<ReplaceUserRolesHandler, Result>(provider, handler => handler.Handle(new ReplaceUserRolesCommand(userId, roles), CancellationToken));

    private Task<Result> ResetAsync(ServiceProvider provider, Guid userId, string password) =>
        RunAsync<ResetUserPasswordHandler, Result>(provider, handler => handler.Handle(new ResetUserPasswordCommand(userId, password), CancellationToken));

    private Task<Result<Guid>> BootstrapAsync(ServiceProvider provider, string userName, string password = Password) =>
        RunAsync<BootstrapAdministratorHandler, Result<Guid>>(provider, handler => handler.Handle(new BootstrapAdministratorCommand(userName, password), CancellationToken));
}

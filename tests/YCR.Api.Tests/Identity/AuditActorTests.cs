using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using YCR.Api.Tests.Authentication;
using YCR.Application.Identity;
using YCR.Domain.Identity;

namespace YCR.Api.Tests.Identity;

/// <summary>
/// Audit actors from the server context only (spec S27; R12, U4; plan P5). This file holds the
/// sign-in events and the administration events.
/// </summary>
public sealed class AuditActorTests(SqlServerFixture fixture) : RealAuthApiTestBase(fixture)
{
    protected override string DatabasePrefix => "api_audit_actor";

    /// <summary>
    /// U4: <c>LoginSucceeded</c> names the user just verified, with their roles; <c>LoginFailed</c>
    /// and <c>LockedOut</c> have no actor. A bearer token of another user, and body fields naming
    /// one, change none of it.
    /// </summary>
    [Fact]
    public async Task LoginEvents_ActorFieldsPerU4_BearerOfAnotherUserIgnored()
    {
        await using var api = RealApi();
        var administrator = await StaffUserSeeder.SeedAsync(api, "admin.user", [RoleNames.SystemAdministrator], cancellationToken: CancellationToken);
        var target = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager, RoleNames.TicketOperator], cancellationToken: CancellationToken);
        using var anonymous = Client(api);
        var (administratorToken, _) = await SignInAsync(anonymous, "admin.user");
        using var spoofing = Client(api, accessToken: administratorToken);
        spoofing.DefaultRequestHeaders.Add("X-User-Id", administrator.ToString());

        object Body(string password) => new
        {
            userName = "hein.min",
            password,
            actorUserId = administrator,
            actorRole = RoleNames.SystemAdministrator,
            authorizedByPermission = "users.manage",
        };

        Assert.Equal(HttpStatusCode.OK, (await spoofing.PostAsJsonAsync("/api/v1/auth/login", Body(StaffUserSeeder.Password), CancellationToken)).StatusCode);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await spoofing.PostAsJsonAsync("/api/v1/auth/login", Body("wrong.password.1"), CancellationToken)).StatusCode);
        }

        var succeeded = (await AuditRowsAsync(IdentityAuditActions.LoginSucceeded)).Last();
        Assert.Equal(target, succeeded.ActorUserId);
        Assert.Equal(target, succeeded.SubjectId);
        Assert.Equal([RoleNames.StationManager, RoleNames.TicketOperator], JsonSerializer.Deserialize<string[]>(succeeded.ActorRole!)!.Order(StringComparer.Ordinal));
        Assert.Null(succeeded.AuthorizedByPermission);

        var failures = await AuditRowsAsync(IdentityAuditActions.LoginFailed);
        var lockedOut = await AuditRowsAsync(IdentityAuditActions.LockedOut);
        Assert.Equal(10, failures.Count);
        Assert.Single(lockedOut);
        Assert.All(failures.Concat(lockedOut), row =>
        {
            Assert.Null(row.ActorUserId);
            Assert.Null(row.ActorRole);
            Assert.Null(row.AuthorizedByPermission);
            Assert.Equal(target, row.SubjectId);
        });
    }

    /// <summary>U4 / G1: family revocation is a system action — no actor, subject the session's user.</summary>
    [Fact]
    public async Task FamilyRevoked_ActorNull()
    {
        await using var api = RealApi();
        await StaffUserSeeder.SeedAsync(api, "admin.user", [RoleNames.SystemAdministrator], cancellationToken: CancellationToken);
        var target = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var anonymous = Client(api);
        var (administratorToken, _) = await SignInAsync(anonymous, "admin.user");
        var (_, first) = await SignInAsync(anonymous, "hein.min");
        var second = RefreshCookieOf(await RefreshAsync(anonymous, first));
        await RefreshAsync(anonymous, second);
        using var spoofing = Client(api, accessToken: administratorToken);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(spoofing, first)).StatusCode);

        var row = Assert.Single(await AuditRowsAsync(IdentityAuditActions.RefreshFamilyRevoked));
        Assert.Null(row.ActorUserId);
        Assert.Null(row.ActorRole);
        Assert.Null(row.AuthorizedByPermission);
        Assert.Equal("Identity.User", row.SubjectType);
        Assert.Equal(target, row.SubjectId);
    }

    /// <summary>
    /// S27: every administration event takes its actor, role and permission from the server
    /// context. Body fields and headers naming another user, role or permission change nothing.
    /// </summary>
    [Fact]
    public async Task AdministrationEvents_ActorAndPermissionFromServerContext_BodyAndHeadersIgnored()
    {
        await using var api = RealApi();
        var (adminId, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);
        var other = await StaffUserSeeder.SeedAsync(api, "other.admin", [RoleNames.SystemAdministrator], cancellationToken: CancellationToken);
        var target = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        admin.DefaultRequestHeaders.Add("X-User-Id", other.ToString());
        admin.DefaultRequestHeaders.Add("X-User-Roles", RoleNames.Auditor);
        admin.DefaultRequestHeaders.Add("X-User-Permissions", "stations.read");
        var spoof = new { actorUserId = other, actorRole = RoleNames.Auditor, authorizedByPermission = "stations.read" };
        using var anonymous = Client(api);
        var (targetToken, _) = await SignInAsync(anonymous, "hein.min");

        var calls = new (string Action, string Permission, Func<Task<HttpResponseMessage>> Call)[]
        {
            (IdentityAuditActions.UserCreated, "users.manage", () => admin.PostAsJsonAsync("/api/v1/users", new { userName = "new.clerk", password = "shwe.dagon.2026", roles = Array.Empty<string>(), spoof.actorUserId, spoof.actorRole, spoof.authorizedByPermission }, CancellationToken)),
            (IdentityAuditActions.RolesChanged, "users.roles.manage", () => admin.PutAsJsonAsync($"/api/v1/users/{target}/roles", new { roles = new[] { RoleNames.Auditor }, spoof.actorUserId, spoof.actorRole, spoof.authorizedByPermission }, CancellationToken)),
            (IdentityAuditActions.SessionRevoked, "auth-sessions.revoke", () => admin.PostAsJsonAsync($"/api/v1/auth-sessions/{SessionOf(targetToken)}/revoke", spoof, CancellationToken)),
            (IdentityAuditActions.PasswordReset, "users.manage", () => admin.PostAsJsonAsync($"/api/v1/users/{target}/password-reset", new { newPassword = "shwe.dagon.2026", spoof.actorUserId, spoof.actorRole, spoof.authorizedByPermission }, CancellationToken)),
            (IdentityAuditActions.UserDisabled, "users.manage", () => admin.PostAsJsonAsync($"/api/v1/users/{target}/disable", spoof, CancellationToken)),
            (IdentityAuditActions.UserEnabled, "users.manage", () => admin.PostAsJsonAsync($"/api/v1/users/{target}/enable", spoof, CancellationToken)),
        };

        foreach (var (action, permission, call) in calls)
        {
            Assert.True((await call()).IsSuccessStatusCode, action);
            var row = Assert.Single(await AuditRowsAsync(action));
            Assert.Equal(adminId, row.ActorUserId);
            Assert.Equal([RoleNames.SystemAdministrator], JsonSerializer.Deserialize<string[]>(row.ActorRole!)!);
            Assert.Equal(permission, row.AuthorizedByPermission);
        }
    }
}

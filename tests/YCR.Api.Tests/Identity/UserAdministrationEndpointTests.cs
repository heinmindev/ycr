using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using YCR.Api.Tests.Authentication;
using YCR.Application.Common.Authorization;
using YCR.Application.Identity;
using YCR.Domain.Identity;

namespace YCR.Api.Tests.Identity;

/// <summary>
/// The administration API over the real pipeline (spec §6.2; S17, S19, S19a, S19b, S19c, S32a;
/// ADR-0016 required tests "disabled user" and "session revocation"; <c>docs/21</c> §Tests).
/// </summary>
public sealed class UserAdministrationEndpointTests(SqlServerFixture fixture) : RealAuthApiTestBase(fixture)
{
    private const string NewPassword = "shwe.dagon.2026";

    protected override string DatabasePrefix => "api_user_admin";

    // ---- create -------------------------------------------------------------------------------

    [Fact]
    public async Task Create_WithValidBody_Returns201MustChangeUserAndAudits()
    {
        await using var api = RealApi();
        var (adminId, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);

        var response = await admin.PostAsJsonAsync("/api/v1/users", new { userName = "new.clerk", password = NewPassword, roles = new[] { RoleNames.TicketOperator } }, CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("id").GetGuid();
        Assert.Equal($"/api/v1/users/{id}", response.Headers.Location?.OriginalString);
        Assert.True(await ScalarAsync<bool>($"SELECT [MustChangePassword] FROM [identity].[Users] WHERE [Id] = '{id}';"));
        var row = Assert.Single(await AuditRowsAsync(IdentityAuditActions.UserCreated));
        Assert.Equal(adminId, row.ActorUserId);
        Assert.Equal(id, row.SubjectId);
        Assert.Equal(Permissions.UsersManage, row.AuthorizedByPermission);

        var get = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/users/{id}", CancellationToken);
        Assert.Equal("new.clerk", get.GetProperty("userName").GetString());
        Assert.Equal([RoleNames.TicketOperator], get.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
        Assert.False(get.GetProperty("isDisabled").GetBoolean());
        Assert.Equal(JsonValueKind.Null, get.GetProperty("lockedUntilUtc").ValueKind);
    }

    /// <summary>S32a: 2 and 51 characters, upper case, a space, a hyphen, a non-ASCII letter.</summary>
    [Theory]
    [InlineData("ab")]
    [InlineData("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijk")]
    [InlineData("Hein.Min")]
    [InlineData("hein min")]
    [InlineData("hein-min")]
    [InlineData("héin")]
    public async Task Create_WithInvalidUserName_Returns400AndCreatesNothing(string userName)
    {
        await using var api = RealApi();
        var (_, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);

        var response = await admin.PostAsJsonAsync("/api/v1/users", new { userName, password = NewPassword, roles = Array.Empty<string>() }, CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Identity.InvalidUserName", await ErrorCodeOf(response));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM [identity].[Users];"));
    }

    [Fact]
    public async Task Create_WithPolicyViolatingPassword_Returns400IdentityPasswordRejected()
    {
        await using var api = RealApi();
        var (_, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);

        var response = await admin.PostAsJsonAsync("/api/v1/users", new { userName = "new.clerk", password = "qwertyuiop12", roles = Array.Empty<string>() }, CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Identity.PasswordRejected", await ErrorCodeOf(response));
    }

    [Fact]
    public async Task Create_WithDuplicateUserName_Returns409()
    {
        await using var api = RealApi();
        var (_, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);
        await StaffUserSeeder.SeedAsync(api, "new.clerk", [], cancellationToken: CancellationToken);

        var response = await admin.PostAsJsonAsync("/api/v1/users", new { userName = "new.clerk", password = NewPassword, roles = Array.Empty<string>() }, CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Identity.UserNameAlreadyExists", await ErrorCodeOf(response));
    }

    [Theory]
    [InlineData("""{"userName":"new.clerk","password":"shwe.dagon.2026","roles":["Cashier"]}""")]
    [InlineData("""{"userName":"new.clerk","password":"shwe.dagon.2026","roles":["systemadministrator"]}""")]
    [InlineData("""{"userName":"new.clerk","password":"shwe.dagon.2026"}""")]
    [InlineData("""{"userName":"new.clerk","roles":[]}""")]
    [InlineData("""{"password":"shwe.dagon.2026","roles":[]}""")]
    public async Task Create_WithInvalidBodyOrUnknownRole_Returns400ValidationFailed(string json)
    {
        await using var api = RealApi();
        var (_, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);

        var response = await admin.PostAsync("/api/v1/users", new StringContent(json, System.Text.Encoding.UTF8, "application/json"), CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Common.ValidationFailed", await ErrorCodeOf(response));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM [identity].[Users];"));
    }

    // ---- disable / enable ---------------------------------------------------------------------

    /// <summary>ADR-0016 required test "disabled user" (S17).</summary>
    [Fact]
    public async Task Disable_RevokesBothSessionsThenEnableRestoresSignIn()
    {
        await using var api = RealApi();
        var (_, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);
        var userId = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api);
        var (accessA, refreshA) = await SignInAsync(client, "hein.min");
        var (accessB, _) = await SignInAsync(client, "hein.min");

        var disable = await admin.PostAsync($"/api/v1/users/{userId}/disable", content: null, CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, disable.StatusCode);
        Assert.Equal(2, await CountAsync($"SELECT COUNT(*) FROM [identity].[AuthSessions] WHERE [UserId] = '{userId}' AND [RevocationReason] = N'UserDisabled';"));
        Assert.Single(await AuditRowsAsync(IdentityAuditActions.UserDisabled));
        Clock.Advance(TimeSpan.FromSeconds(30));
        foreach (var token in new[] { accessA, accessB })
        {
            using var bearer = Client(api, accessToken: token);
            Assert.Equal(HttpStatusCode.Unauthorized, (await bearer.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);
        }

        Assert.Equal("Auth.RefreshInvalid", await ErrorCodeOf(await RefreshAsync(client, refreshA)));
        Assert.Equal("Auth.InvalidCredentials", await ErrorCodeOf(await LoginAsync(client, "hein.min")));

        // G2: a repeat disable changes nothing and writes nothing.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/v1/users/{userId}/disable", content: null, CancellationToken)).StatusCode);
        Assert.Single(await AuditRowsAsync(IdentityAuditActions.UserDisabled));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/v1/users/{userId}/enable", content: null, CancellationToken)).StatusCode);
        Assert.Single(await AuditRowsAsync(IdentityAuditActions.UserEnabled));
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, "hein.min")).StatusCode);

        // G2: a repeat enable likewise.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/v1/users/{userId}/enable", content: null, CancellationToken)).StatusCode);
        Assert.Single(await AuditRowsAsync(IdentityAuditActions.UserEnabled));
    }

    [Fact]
    public async Task Disable_Self_Returns422AndChangesNothing()
    {
        await using var api = RealApi();
        var (adminId, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);
        await StaffUserSeeder.SeedAsync(api, "other.admin", [RoleNames.SystemAdministrator], cancellationToken: CancellationToken);

        var response = await admin.PostAsync($"/api/v1/users/{adminId}/disable", content: null, CancellationToken);

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Equal("Identity.CannotDisableOwnAccount", await ErrorCodeOf(response));
        Assert.False(await ScalarAsync<bool>($"SELECT [IsDisabled] FROM [identity].[Users] WHERE [Id] = '{adminId}';"));
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.UserDisabled));
    }

    [Theory]
    [InlineData("POST", "disable")]
    [InlineData("POST", "enable")]
    [InlineData("POST", "unlock")]
    [InlineData("PUT", "roles")]
    [InlineData("POST", "password-reset")]
    [InlineData("GET", "auth-sessions")]
    [InlineData("GET", "")]
    public async Task UserEndpoint_UnknownId_Returns404UserNotFound(string method, string action)
    {
        await using var api = RealApi();
        var (_, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/v1/users/{Guid.CreateVersion7()}/{action}".TrimEnd('/'));
        if (action == "roles")
        {
            request.Content = JsonContent.Create(new { roles = Array.Empty<string>() });
        }
        else if (action == "password-reset")
        {
            request.Content = JsonContent.Create(new { newPassword = NewPassword });
        }

        var response = await admin.SendAsync(request, CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Identity.UserNotFound", await ErrorCodeOf(response));
    }

    // ---- roles --------------------------------------------------------------------------------

    [Fact]
    public async Task ReplaceRoles_Returns204AndAuditsBeforeAndAfter()
    {
        await using var api = RealApi();
        var (adminId, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);
        var userId = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);

        var response = await admin.PutAsJsonAsync($"/api/v1/users/{userId}/roles", new { roles = new[] { RoleNames.TicketOperator, RoleNames.Auditor } }, CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var row = Assert.Single(await AuditRowsAsync(IdentityAuditActions.RolesChanged));
        Assert.Equal(adminId, row.ActorUserId);
        Assert.Equal(Permissions.UsersRolesManage, row.AuthorizedByPermission);
        Assert.Equal([RoleNames.StationManager], RolesIn(row.BeforeJson));
        Assert.Equal([RoleNames.TicketOperator, RoleNames.Auditor], RolesIn(row.AfterJson));
    }

    public static TheoryData<string[]> OwnRoleValues() =>
        new([], [RoleNames.SystemAdministrator], [RoleNames.SystemAdministrator, RoleNames.Auditor]);

    [Theory]
    [MemberData(nameof(OwnRoleValues))]
    public async Task ReplaceRoles_Self_Returns422AndChangesNothing(string[] roles)
    {
        await using var api = RealApi();
        var (adminId, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);

        var response = await admin.PutAsJsonAsync($"/api/v1/users/{adminId}/roles", new { roles }, CancellationToken);

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Equal("Identity.CannotChangeOwnRoles", await ErrorCodeOf(response));
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.RolesChanged));
        Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM [identity].[UserRoles] WHERE [UserId] = '{adminId}';"));
    }

    [Theory]
    [InlineData("""{"roles":["Cashier"]}""")]
    [InlineData("""{"roles":null}""")]
    [InlineData("{}")]
    public async Task ReplaceRoles_WithInvalidBodyOrUnknownRole_Returns400(string json)
    {
        await using var api = RealApi();
        var (_, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);
        var userId = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);

        var response = await admin.PutAsync($"/api/v1/users/{userId}/roles", new StringContent(json, System.Text.Encoding.UTF8, "application/json"), CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Common.ValidationFailed", await ErrorCodeOf(response));
    }

    // ---- password reset -----------------------------------------------------------------------

    /// <summary>S19a.</summary>
    [Fact]
    public async Task PasswordReset_RevokesAllSessionsSetsMustChangeAndAudits()
    {
        await using var api = RealApi();
        var (_, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);
        var userId = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api);
        await SignInAsync(client, "hein.min");
        await SignInAsync(client, "hein.min");

        var response = await admin.PostAsJsonAsync($"/api/v1/users/{userId}/password-reset", new { newPassword = NewPassword }, CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(2, await CountAsync($"SELECT COUNT(*) FROM [identity].[AuthSessions] WHERE [UserId] = '{userId}' AND [RevocationReason] = N'AdministratorPasswordReset';"));
        Assert.Single(await AuditRowsAsync(IdentityAuditActions.PasswordReset));
        Assert.Equal("Auth.InvalidCredentials", await ErrorCodeOf(await LoginAsync(client, "hein.min")));
        var (accessToken, _) = await SignInAsync(client, "hein.min", NewPassword);
        using var bearer = Client(api, accessToken: accessToken);
        Assert.Equal("Auth.PasswordChangeRequired", await ErrorCodeOf(await bearer.GetAsync("/api/v1/stations", CancellationToken)));
    }

    [Fact]
    public async Task PasswordReset_Self_Returns422()
    {
        await using var api = RealApi();
        var (adminId, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);

        var response = await admin.PostAsJsonAsync($"/api/v1/users/{adminId}/password-reset", new { newPassword = NewPassword }, CancellationToken);

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Equal("Identity.CannotResetOwnPassword", await ErrorCodeOf(response));
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.PasswordReset));
    }

    [Fact]
    public async Task PasswordReset_PolicyViolation_Returns400IdentityPasswordRejected()
    {
        await using var api = RealApi();
        var (_, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);
        var userId = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);

        var response = await admin.PostAsJsonAsync($"/api/v1/users/{userId}/password-reset", new { newPassword = "short" }, CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Identity.PasswordRejected", await ErrorCodeOf(response));
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.PasswordReset));
    }

    // ---- reads, sessions, revocation ----------------------------------------------------------

    [Fact]
    public async Task ListUsers_ReturnsPagedEnvelope()
    {
        await using var api = RealApi();
        var (_, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        await StaffUserSeeder.SeedAsync(api, "zaw.win", [], cancellationToken: CancellationToken);

        var page = await admin.GetFromJsonAsync<JsonElement>("/api/v1/users?page=1&pageSize=2", CancellationToken);

        Assert.Equal(3, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, page.GetProperty("pageSize").GetInt32());
        Assert.Equal(["admin.user", "hein.min"], page.GetProperty("items").EnumerateArray().Select(user => user.GetProperty("userName").GetString()));
        Assert.DoesNotContain("password", page.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Identity.InvalidPageRequest", await ErrorCodeOf(await admin.GetAsync("/api/v1/users?pageSize=201", CancellationToken)));
    }

    /// <summary>ADR-0016 required test "session revocation" (S19).</summary>
    [Fact]
    public async Task RevokeSession_RevokesNamedSessionOnlyAndAudits()
    {
        await using var api = RealApi();
        var (adminId, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);
        var userId = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api);
        var (accessA, refreshA) = await SignInAsync(client, "hein.min");
        var (accessB, refreshB) = await SignInAsync(client, "hein.min");

        var sessions = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/users/{userId}/auth-sessions", CancellationToken);
        Assert.Equal(2, sessions.GetProperty("totalCount").GetInt32());
        Assert.Equal(
            ["createdAtUtc", "expiresAtUtc", "id", "revocationReason", "revokedAtUtc"],
            sessions.GetProperty("items")[0].EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));

        var response = await admin.PostAsync($"/api/v1/auth-sessions/{SessionOf(accessA)}/revoke", content: null, CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var row = Assert.Single(await AuditRowsAsync(IdentityAuditActions.SessionRevoked));
        Assert.Equal(adminId, row.ActorUserId);
        Assert.Equal(userId, row.SubjectId);
        Assert.Equal(Permissions.AuthSessionsRevoke, row.AuthorizedByPermission);
        Clock.Advance(TimeSpan.FromSeconds(30));
        using var bearerA = Client(api, accessToken: accessA);
        using var bearerB = Client(api, accessToken: accessB);
        Assert.Equal(HttpStatusCode.Unauthorized, (await bearerA.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);
        Assert.Equal("Auth.RefreshInvalid", await ErrorCodeOf(await RefreshAsync(client, refreshA)));
        Assert.Equal(HttpStatusCode.OK, (await bearerB.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(client, refreshB)).StatusCode);

        // Already revoked: 204, no second row.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/v1/auth-sessions/{SessionOf(accessA)}/revoke", content: null, CancellationToken)).StatusCode);
        Assert.Single(await AuditRowsAsync(IdentityAuditActions.SessionRevoked));
    }

    [Fact]
    public async Task RevokeSession_UnknownSession_Returns404()
    {
        await using var api = RealApi();
        var (_, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);

        var response = await admin.PostAsync($"/api/v1/auth-sessions/{Guid.CreateVersion7()}/revoke", content: null, CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Identity.SessionNotFound", await ErrorCodeOf(response));
    }

    [Fact]
    public async Task ListRoles_ReturnsEightRolesWithPermissions()
    {
        await using var api = RealApi();
        var (_, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);

        var roles = await admin.GetFromJsonAsync<JsonElement>("/api/v1/roles", CancellationToken);

        Assert.Equal(RoleNames.All, roles.EnumerateArray().Select(role => role.GetProperty("name").GetString()!));
        Assert.Equal(14, roles.EnumerateArray().Sum(role => role.GetProperty("permissions").GetArrayLength()));
    }

    // ---- authentication and authorization on every administration endpoint --------------------

    public static TheoryData<string, string> AdministrationEndpoints() => new()
    {
        { "GET", "/api/v1/users" },
        { "GET", "/api/v1/users/{id}" },
        { "POST", "/api/v1/users" },
        { "POST", "/api/v1/users/{id}/disable" },
        { "POST", "/api/v1/users/{id}/enable" },
        { "POST", "/api/v1/users/{id}/unlock" },
        { "PUT", "/api/v1/users/{id}/roles" },
        { "POST", "/api/v1/users/{id}/password-reset" },
        { "GET", "/api/v1/users/{id}/auth-sessions" },
        { "POST", "/api/v1/auth-sessions/{id}/revoke" },
        { "GET", "/api/v1/roles" },
    };

    [Theory]
    [MemberData(nameof(AdministrationEndpoints))]
    public async Task EveryAdministrationEndpoint_Anonymous_Returns401(string method, string path)
    {
        await using var api = RealApi();
        using var client = Client(api);

        var response = await client.SendAsync(Request(method, path, Guid.CreateVersion7()), CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Auth.Unauthenticated", await ErrorCodeOf(response));
    }

    /// <summary>S26 / <c>docs/21</c>: a real user whose roles grant every permission except the administration ones.</summary>
    [Theory]
    [MemberData(nameof(AdministrationEndpoints))]
    public async Task EveryAdministrationEndpoint_WithoutPermission_Returns403(string method, string path)
    {
        await using var api = RealApi();
        var (_, holder) = await SignedInAsync(api, "rail.admin", [.. RoleNames.All.Where(role => role != RoleNames.SystemAdministrator)]);
        var target = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        var auditBefore = await AuditCountAsync();

        var response = await holder.SendAsync(Request(method, path, target), CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(auditBefore, await AuditCountAsync());
    }

    public static TheoryData<string, string> AdministrationEndpointsWithBody() => new()
    {
        { "POST", "/api/v1/users" },
        { "PUT", "/api/v1/users/{id}/roles" },
        { "POST", "/api/v1/users/{id}/password-reset" },
    };

    [Theory]
    [MemberData(nameof(AdministrationEndpointsWithBody))]
    public async Task EveryAdministrationEndpoint_WithInvalidBody_Returns400(string method, string path)
    {
        await using var api = RealApi();
        var (_, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);
        var target = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var request = new HttpRequestMessage(new HttpMethod(method), path.Replace("{id}", target.ToString(), StringComparison.Ordinal))
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
        };

        var response = await admin.SendAsync(request, CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Common.ValidationFailed", await ErrorCodeOf(response));
    }

    private static HttpRequestMessage Request(string method, string path, Guid id)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path.Replace("{id}", id.ToString(), StringComparison.Ordinal));
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(new { userName = "x.y.z", password = NewPassword, newPassword = NewPassword, roles = Array.Empty<string>() });
        }

        return request;
    }

    private static string[] RolesIn(string? json) =>
        [.. JsonDocument.Parse(json!).RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetString()!)];
}

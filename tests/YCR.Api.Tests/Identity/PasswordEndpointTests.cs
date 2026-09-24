using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using YCR.Api.Tests.Authentication;
using YCR.Application.Common.Authorization;
using YCR.Application.Identity;
using YCR.Domain.Identity;

namespace YCR.Api.Tests.Identity;

/// <summary>
/// <c>POST /api/v1/auth/password</c> — ADR-0016 required test "password change" (spec S16; R4, R18,
/// U1) — and <c>GET /api/v1/auth/me</c> (spec §6.1).
/// </summary>
public sealed class PasswordEndpointTests(SqlServerFixture fixture) : RealAuthApiTestBase(fixture)
{
    private const string NewPassword = "shwe.dagon.2026";

    protected override string DatabasePrefix => "api_password";

    [Fact]
    public async Task ChangePassword_RevokesOtherSessionsWithin30sAndKeepsCurrent()
    {
        await using var api = RealApi();
        var userId = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api);
        var (accessA, refreshA) = await SignInAsync(client, "hein.min");
        var (accessB, refreshB) = await SignInAsync(client, "hein.min");
        using var bearerA = Client(api, accessToken: accessA);
        using var bearerB = Client(api, accessToken: accessB);
        Assert.Equal(HttpStatusCode.OK, (await bearerB.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);

        var response = await ChangeAsync(bearerA, StaffUserSeeder.Password, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("PasswordChanged", await ScalarAsync<string>($"SELECT [RevocationReason] FROM [identity].[AuthSessions] WHERE [Id] = '{SessionOf(accessB)}';"));
        var row = Assert.Single(await AuditRowsAsync(IdentityAuditActions.PasswordChanged));
        Assert.Equal(userId, row.ActorUserId);

        Clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(HttpStatusCode.Unauthorized, (await bearerB.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);
        Assert.Equal("Auth.RefreshInvalid", await ErrorCodeOf(await RefreshAsync(client, refreshB)));
        Assert.Equal(HttpStatusCode.OK, (await bearerA.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(client, refreshA)).StatusCode);

        Assert.Equal("Auth.InvalidCredentials", await ErrorCodeOf(await LoginAsync(client, "hein.min")));
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, "hein.min", NewPassword)).StatusCode);
    }

    /// <summary>11 and 129 characters, and a blocklisted password (R18, D2).</summary>
    public static TheoryData<string> RejectedPasswords() =>
        [new string('a', 11) , new string('b', 129), "qwertyuiop12"];

    [Theory]
    [MemberData(nameof(RejectedPasswords))]
    public async Task ChangePassword_PolicyViolation_Returns400AuthPasswordRejected(string rejected)
    {
        await using var api = RealApi();
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api);
        var (accessToken, _) = await SignInAsync(client, "hein.min");
        using var bearer = Client(api, accessToken: accessToken);

        var response = await ChangeAsync(bearer, StaffUserSeeder.Password, rejected);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Auth.PasswordRejected", await ErrorCodeOf(response));
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.PasswordChanged));
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, "hein.min")).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrent_Returns422()
    {
        await using var api = RealApi();
        var userId = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api);
        var (accessToken, _) = await SignInAsync(client, "hein.min");
        using var bearer = Client(api, accessToken: accessToken);

        var response = await ChangeAsync(bearer, "wrong.password.1", NewPassword);

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Equal("Auth.CurrentPasswordIncorrect", await ErrorCodeOf(response));
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.PasswordChanged));
        Assert.Equal((0, (DateTimeOffset?)null), await LockoutStateAsync(userId));
    }

    [Fact]
    public async Task ChangePassword_WithInvalidBody_Returns400()
    {
        await using var api = RealApi();
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api);
        var (accessToken, _) = await SignInAsync(client, "hein.min");
        using var bearer = Client(api, accessToken: accessToken);

        var response = await bearer.PostAsJsonAsync("/api/v1/auth/password", new { newPassword = NewPassword }, CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Common.ValidationFailed", await ErrorCodeOf(response));
    }

    [Fact]
    public async Task Me_ReturnsUserNameRolesAndPermissions()
    {
        await using var api = RealApi();
        var userId = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager, RoleNames.RailwayAdministrator], cancellationToken: CancellationToken);
        using var client = Client(api);
        var (accessToken, _) = await SignInAsync(client, "hein.min");
        using var bearer = Client(api, accessToken: accessToken);

        var response = await bearer.GetAsync("/api/v1/auth/me", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        var root = body.RootElement;
        Assert.Equal(["userId", "userName", "roles", "permissions"], root.EnumerateObject().Select(property => property.Name));
        Assert.Equal(userId, root.GetProperty("userId").GetGuid());
        Assert.Equal("hein.min", root.GetProperty("userName").GetString());
        Assert.Equal(
            [RoleNames.RailwayAdministrator, RoleNames.StationManager],
            root.GetProperty("roles").EnumerateArray().Select(role => role.GetString()!).Order(StringComparer.Ordinal));
        Assert.Equal(
            [Permissions.StationsManage, Permissions.StationsRead],
            root.GetProperty("permissions").EnumerateArray().Select(permission => permission.GetString()!).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Me_Anonymous_Returns401()
    {
        await using var api = RealApi();
        using var client = Client(api);

        var response = await client.GetAsync("/api/v1/auth/me", CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Auth.Unauthenticated", await ErrorCodeOf(response));
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
    }

    private static Task<HttpResponseMessage> ChangeAsync(HttpClient client, string currentPassword, string newPassword) =>
        client.PostAsJsonAsync("/api/v1/auth/password", new { currentPassword, newPassword }, CancellationToken);
}

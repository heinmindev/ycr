using System.Net;
using YCR.Api.Tests.Authentication;
using YCR.Application.Identity;
using YCR.Domain.Identity;

namespace YCR.Api.Tests.Identity;

/// <summary><c>POST /api/v1/auth/logout</c> — ADR-0016 required test "logout" (spec S15; R3, R4).</summary>
public sealed class LogoutEndpointTests(SqlServerFixture fixture) : RealAuthApiTestBase(fixture)
{
    protected override string DatabasePrefix => "api_logout";

    [Fact]
    public async Task Logout_RevokesSessionExpiresCookieAndAccessTokenFailsWithin30s()
    {
        await using var api = RealApi();
        var userId = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api);
        var (accessToken, refreshToken) = await SignInAsync(client, "hein.min");
        using var bearer = Client(api, accessToken: accessToken);
        Assert.Equal(HttpStatusCode.OK, (await bearer.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);

        var response = await bearer.PostAsync("/api/v1/auth/logout", content: null, CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var expiry = RefreshSetCookie(response);
        Assert.NotNull(expiry);
        Assert.StartsWith("ycr_refresh=;", expiry, StringComparison.Ordinal);
        Assert.Contains("expires=Thu, 01 Jan 1970", expiry, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth/refresh", expiry, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Logout", await ScalarAsync<string>($"SELECT [RevocationReason] FROM [identity].[AuthSessions] WHERE [Id] = '{SessionOf(accessToken)}';"));
        var row = Assert.Single(await AuditRowsAsync(IdentityAuditActions.LoggedOut));
        Assert.Equal(userId, row.ActorUserId);
        Assert.Equal(userId, row.SubjectId);

        Assert.Equal("Auth.RefreshInvalid", await ErrorCodeOf(await RefreshAsync(client, refreshToken)));

        // R3: within 30 seconds on any instance; on the instance that served the logout, at once.
        var immediately = await bearer.GetAsync("/api/v1/stations", CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, immediately.StatusCode);
        Clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(HttpStatusCode.Unauthorized, (await bearer.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);

        // Spec §6.1: a second logout on the revoked session gets 401; nothing more is audited.
        Assert.Equal(HttpStatusCode.Unauthorized, (await bearer.PostAsync("/api/v1/auth/logout", content: null, CancellationToken)).StatusCode);
        Assert.Single(await AuditRowsAsync(IdentityAuditActions.LoggedOut));
    }

    [Fact]
    public async Task Logout_LeavesOtherSessionsActive()
    {
        await using var api = RealApi();
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api);
        var (accessA, _) = await SignInAsync(client, "hein.min");
        var (accessB, refreshB) = await SignInAsync(client, "hein.min");
        using var bearerA = Client(api, accessToken: accessA);

        Assert.Equal(HttpStatusCode.NoContent, (await bearerA.PostAsync("/api/v1/auth/logout", content: null, CancellationToken)).StatusCode);
        Clock.Advance(TimeSpan.FromSeconds(30));

        using var bearerB = Client(api, accessToken: accessB);
        Assert.Equal(HttpStatusCode.OK, (await bearerB.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(client, refreshB)).StatusCode);
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM [identity].[AuthSessions] WHERE [RevokedAtUtc] IS NULL;"));
    }

    [Fact]
    public async Task Logout_WithoutBearer_Returns401()
    {
        await using var api = RealApi();
        using var client = Client(api);

        var response = await client.PostAsync("/api/v1/auth/logout", content: null, CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Auth.Unauthenticated", await ErrorCodeOf(response));
        Assert.Null(RefreshSetCookie(response));
    }
}

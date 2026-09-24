using System.Net;
using System.Net.Http.Json;
using YCR.Api.Tests.Authentication;
using YCR.Domain.Identity;

namespace YCR.Api.Tests.Common;

/// <summary>
/// R24 (D16; plan P10): no CORS, security headers on every response kind, and no caching of
/// <c>/api/v1/auth/*</c> responses (spec S26b, S26c).
/// </summary>
public sealed class BrowserControlsTests(SqlServerFixture fixture) : RealAuthApiTestBase(fixture)
{
    protected override string DatabasePrefix => "api_browser_controls";

    [Fact]
    public async Task Preflight_FromForeignOrigin_HasNoCorsHeaders()
    {
        await using var api = RealApi();
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var foreign = Client(api, "https://evil.test");
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type");

        var responses = new[]
        {
            await foreign.SendAsync(preflight, CancellationToken),
            await LoginAsync(foreign, "hein.min"),
            await LoginAsync(Client(api), "hein.min"),
        };

        foreach (var response in responses)
        {
            Assert.DoesNotContain(
                response.Headers.Concat(response.Content.Headers),
                header => header.Key.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task EveryResponseKind_CarriesSecurityHeaders()
    {
        await using var api = RealApi(new Dictionary<string, string?> { ["Auth:RateLimits:RefreshPerClientAddressPerMinute"] = "1" });
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api);
        var (accessToken, _) = await SignInAsync(client, "hein.min");
        using var bearer = Client(api, accessToken: accessToken);
        using var foreign = Client(api, "https://evil.test");
        await using var broken = new YcrApiFactory(
            Database.ApplicationConnectionString.Replace(Database.Name, $"{Database.Name}_missing", StringComparison.Ordinal),
            mode: AuthMode.RealTokens,
            settings: new Dictionary<string, string?> { ["Auth:PrincipalCacheSeconds"] = "30" });

        await RefreshAsync(client, "first");
        var responses = new (HttpStatusCode Expected, HttpResponseMessage Response)[]
        {
            (HttpStatusCode.OK, await bearer.GetAsync("/api/v1/auth/me", CancellationToken)),
            (HttpStatusCode.BadRequest, await LoginAsync(client, userName: null)),
            (HttpStatusCode.Unauthorized, await LoginAsync(client, "hein.min", "wrong.password.1")),
            (HttpStatusCode.Forbidden, await LoginAsync(foreign, "hein.min")),
            (HttpStatusCode.NotFound, await bearer.GetAsync($"/api/v1/stations/{Guid.CreateVersion7()}", CancellationToken)),
            (HttpStatusCode.TooManyRequests, await RefreshAsync(client, "second")),
            (HttpStatusCode.InternalServerError, await LoginAsync(Client(broken), "hein.min")),
            (HttpStatusCode.NoContent, await bearer.PostAsync("/api/v1/auth/logout", content: null, CancellationToken)),
        };

        foreach (var (expected, response) in responses)
        {
            Assert.Equal(expected, response.StatusCode);
            Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
            Assert.Equal(
                "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'",
                Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
            Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
            Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        }
    }

    [Fact]
    public async Task AuthResponses_AreNoStore()
    {
        await using var api = RealApi();
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api);
        var login = await LoginAsync(client, "hein.min");
        var accessToken = await AccessTokenOf(login);
        using var bearer = Client(api, accessToken: accessToken);

        foreach (var response in new[]
        {
            login,
            await RefreshAsync(client, RefreshCookieOf(login)),
            await RefreshAsync(client, "unknown"),
            await bearer.GetAsync("/api/v1/auth/me", CancellationToken),
            await bearer.PostAsJsonAsync("/api/v1/auth/password", new { currentPassword = "x", newPassword = "y" }, CancellationToken),
        })
        {
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        }

        Assert.Null((await bearer.GetAsync("/api/v1/stations", CancellationToken)).Headers.CacheControl);
    }
}

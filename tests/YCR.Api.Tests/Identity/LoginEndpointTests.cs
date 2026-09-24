using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using YCR.Api.Tests.Authentication;
using YCR.Application.Identity;
using YCR.Domain.Identity;

namespace YCR.Api.Tests.Identity;

/// <summary><c>POST /api/v1/auth/login</c> over the real pipeline (spec S1, S2, S4; R23; V8).</summary>
public sealed class LoginEndpointTests(SqlServerFixture fixture) : RealAuthApiTestBase(fixture)
{
    protected override string DatabasePrefix => "api_login";

    [Fact]
    public async Task Login_WithValidCredentials_Returns200TokenAndHardenedCookie()
    {
        await using var api = RealApi();
        var userId = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api);

        var response = await LoginAsync(client, "hein.min");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        Assert.Equal(["accessToken", "expiresAtUtc"], body.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(Clock.GetUtcNow().AddMinutes(15), body.RootElement.GetProperty("expiresAtUtc").GetDateTimeOffset());

        var cookie = RefreshSetCookie(response);
        Assert.NotNull(cookie);
        var attributes = cookie.Split("; ").Skip(1).Select(attribute => attribute.ToLowerInvariant()).ToList();
        Assert.Contains("httponly", attributes);
        Assert.Contains("secure", attributes);
        Assert.Contains("samesite=strict", attributes);
        Assert.Contains("path=/api/v1/auth/refresh", attributes);
        Assert.Contains($"expires={Clock.GetUtcNow().Add(SessionLifetime):R}".ToLowerInvariant(), attributes);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        // S1 / S31: one active session of 12 hours; the stored token is a hash, never the raw value.
        Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM [identity].[AuthSessions] WHERE [UserId] = '{userId}' AND [RevokedAtUtc] IS NULL;"));
        Assert.Equal(Clock.GetUtcNow().Add(SessionLifetime), await ScalarAsync<DateTimeOffset>($"SELECT [ExpiresAtUtc] FROM [identity].[AuthSessions] WHERE [UserId] = '{userId}';"));
        var raw = RefreshCookieOf(response)!;
        Assert.Equal(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)), await ScalarAsync<byte[]>($"SELECT [TokenHash] FROM [identity].[RefreshTokens] t JOIN [identity].[AuthSessions] s ON s.[Id] = t.[SessionId] WHERE s.[UserId] = '{userId}';"));
        Assert.Single(await AuditRowsAsync(IdentityAuditActions.LoginSucceeded));
        Assert.Equal(0, (await LockoutStateAsync(userId)).Count);
    }

    /// <summary>S2 / R23: four rejections, one response — identical apart from traceId, no cookie, no session.</summary>
    [Fact]
    public async Task Login_UniformRejection_FourCasesIdenticalApartFromTraceId()
    {
        await using var api = RealApi();
        await StaffUserSeeder.SeedAsync(api, "active.user", [RoleNames.StationManager], cancellationToken: CancellationToken);
        var disabled = await StaffUserSeeder.SeedAsync(api, "disabled.user", [RoleNames.StationManager], cancellationToken: CancellationToken);
        var locked = await StaffUserSeeder.SeedAsync(api, "locked.user", [RoleNames.StationManager], cancellationToken: CancellationToken);
        var now = Clock.GetUtcNow();
        await ExecuteAsMigratorAsync(
            $"""
            UPDATE [identity].[Users] SET [IsDisabled] = 1, [DisabledAtUtc] = '{now:O}' WHERE [Id] = '{disabled}';
            UPDATE [identity].[Users] SET [LockoutEndUtc] = '{now.AddMinutes(15):O}' WHERE [Id] = '{locked}';
            """);
        using var client = Client(api);

        var responses = new[]
        {
            await LoginAsync(client, "active.user", "wrong.password.1"),
            await LoginAsync(client, "no.such.user"),
            await LoginAsync(client, "disabled.user"),
            await LoginAsync(client, "locked.user"),
        };

        var bodies = new List<string>();
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Null(RefreshSetCookie(response));
            Assert.Empty(response.Headers.WwwAuthenticate);
            var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(CancellationToken))!.AsObject();
            Assert.Equal("Auth.InvalidCredentials", (string?)body["errorCode"]);
            Assert.True(body.Remove("traceId"));
            bodies.Add(body.ToJsonString());
        }

        Assert.Single(bodies.Distinct());
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM [identity].[AuthSessions];"));
    }

    public static TheoryData<string> InvalidBodies() =>
        ["""{"password":"kyauk.tan.12"}""", """{"userName":"hein.min"}""", """{"userName":"","password":""}""", "{}"];

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task Login_WithInvalidBody_Returns400(string json)
    {
        await using var api = RealApi();
        using var client = Client(api);

        var response = await client.PostAsync("/api/v1/auth/login", new StringContent(json, System.Text.Encoding.UTF8, "application/json"), CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Common.ValidationFailed", await ErrorCodeOf(response));
        Assert.Equal(0, await AuditCountAsync());
    }

    /// <summary>
    /// V8: a <c>WebApplicationFactory</c> client on <c>https://localhost</c> keeps the
    /// <c>Secure; SameSite=Strict; Path=/api/v1/auth/refresh</c> cookie and sends it back to the
    /// refresh endpoint — the cookie works as a browser would use it, not only as parsed text.
    /// </summary>
    [Fact]
    public async Task Login_CookieContainerClient_SendsRefreshCookieBackToRefresh()
    {
        await using var api = RealApi();
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", YcrApiFactory.AllowedOrigin);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = "hein.min", password = StaffUserSeeder.Password }, CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var refresh = await client.PostAsync("/api/v1/auth/refresh", content: null, CancellationToken);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.NotEqual(RefreshCookieOf(login), RefreshCookieOf(refresh));

        var again = await client.PostAsync("/api/v1/auth/refresh", content: null, CancellationToken);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }
}


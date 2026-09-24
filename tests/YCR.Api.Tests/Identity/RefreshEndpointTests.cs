using System.Net;
using YCR.Api.Tests.Authentication;
using YCR.Application.Identity;
using YCR.Domain.Identity;

namespace YCR.Api.Tests.Identity;

/// <summary>
/// <c>POST /api/v1/auth/refresh</c> over the real pipeline — ADR-0016 §Required tests for rotation,
/// the two-tab race, predecessor reuse inside and outside the grace window, and ancestor reuse
/// (spec S7–S11, S13, S14, S29).
/// </summary>
public sealed class RefreshEndpointTests(SqlServerFixture fixture) : RealAuthApiTestBase(fixture)
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(20);

    protected override string DatabasePrefix => "api_refresh";

    [Fact]
    public async Task Refresh_WithValidCookie_RotatesAndKeepsSessionExpiry()
    {
        await using var api = RealApi();
        var userId = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api);
        var (firstAccess, first) = await SignInAsync(client, "hein.min");
        var sessionExpiry = Clock.GetUtcNow().Add(SessionLifetime);
        Clock.Advance(TimeSpan.FromMinutes(14));

        var response = await RefreshAsync(client, first);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var accessToken = await AccessTokenOf(response);
        Assert.NotEqual(firstAccess, accessToken);
        Assert.Equal(SessionOf(firstAccess), SessionOf(accessToken));
        var successor = RefreshCookieOf(response);
        Assert.NotNull(successor);
        Assert.NotEqual(first, successor);
        Assert.Contains($"expires={sessionExpiry:R}", RefreshSetCookie(response)!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        // D12: the absolute lifetime never moves; the presented token is marked rotated.
        Assert.Equal(sessionExpiry, await ScalarAsync<DateTimeOffset>($"SELECT [ExpiresAtUtc] FROM [identity].[AuthSessions] WHERE [UserId] = '{userId}';"));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM [identity].[RefreshTokens] WHERE [RotatedAtUtc] IS NOT NULL AND [ReplacedByTokenId] IS NOT NULL;"));
        using var bearer = Client(api, accessToken: accessToken);
        Assert.Equal(HttpStatusCode.OK, (await bearer.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);

        // S7: rotation is not an audited event.
        Assert.Equal([IdentityAuditActions.LoginSucceeded], (await AuditRowsAsync()).Select(row => row.Action));
    }

    /// <summary>ADR-0016 required test: two tabs, one token (S8).</summary>
    [Fact]
    public async Task Refresh_TwoTabRace_OneRotatesOther409ThenSuccessorWorks()
    {
        await using var api = RealApi();
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api);
        var (_, token) = await SignInAsync(client, "hein.min");

        var responses = await Task.WhenAll(RefreshAsync(client, token), RefreshAsync(client, token));

        var winner = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        var loser = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("Auth.RefreshSuperseded", await ErrorCodeOf(loser));
        Assert.Null(RefreshSetCookie(loser));
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM [identity].[AuthSessions] WHERE [RevokedAtUtc] IS NOT NULL;"));
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(client, RefreshCookieOf(winner))).StatusCode);
    }

    /// <summary>ADR-0016 required test: predecessor reuse inside the grace window (S9).</summary>
    [Fact]
    public async Task Refresh_PredecessorInsideGrace_Returns409NoCookieNoAudit()
    {
        await using var api = RealApi();
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api);
        var (_, first) = await SignInAsync(client, "hein.min");
        var successor = RefreshCookieOf(await RefreshAsync(client, first));
        Clock.Advance(Grace);

        var response = await RefreshAsync(client, first);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Auth.RefreshSuperseded", await ErrorCodeOf(response));
        Assert.Null(RefreshSetCookie(response));
        Assert.DoesNotContain("accessToken", await response.Content.ReadAsStringAsync(CancellationToken), StringComparison.Ordinal);
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.RefreshFamilyRevoked));
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(client, successor)).StatusCode);
    }

    /// <summary>ADR-0016 required test: predecessor reuse outside the grace window (S10).</summary>
    [Fact]
    public async Task Refresh_PredecessorOutsideGrace_RevokesFamilyAndAccessTokenFailsWithin30s()
    {
        await using var api = RealApi();
        var userId = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api);
        var (_, first) = await SignInAsync(client, "hein.min");
        var rotated = await RefreshAsync(client, first);
        var successor = RefreshCookieOf(rotated);
        var accessToken = await AccessTokenOf(rotated);
        using var bearer = Client(api, accessToken: accessToken);
        Assert.Equal(HttpStatusCode.OK, (await bearer.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);
        Clock.Advance(Grace + TimeSpan.FromSeconds(1));

        var response = await RefreshAsync(client, first);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Auth.RefreshInvalid", await ErrorCodeOf(response));
        Assert.Equal("FamilyReuse", await ScalarAsync<string>($"SELECT [RevocationReason] FROM [identity].[AuthSessions] WHERE [UserId] = '{userId}';"));
        var row = Assert.Single(await AuditRowsAsync(IdentityAuditActions.RefreshFamilyRevoked));
        Assert.Equal(userId, row.SubjectId);
        Assert.Equal("Auth.RefreshInvalid", await ErrorCodeOf(await RefreshAsync(client, successor)));

        Clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(HttpStatusCode.Unauthorized, (await bearer.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);
    }

    /// <summary>ADR-0016 required test: ancestor reuse, even inside the grace window (S11).</summary>
    [Fact]
    public async Task Refresh_Ancestor_RevokesFamily()
    {
        await using var api = RealApi();
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api);
        var (_, first) = await SignInAsync(client, "hein.min");
        var second = RefreshCookieOf(await RefreshAsync(client, first));
        var third = RefreshCookieOf(await RefreshAsync(client, second));

        var response = await RefreshAsync(client, first);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Auth.RefreshInvalid", await ErrorCodeOf(response));
        Assert.Single(await AuditRowsAsync(IdentityAuditActions.RefreshFamilyRevoked));
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, third)).StatusCode);
    }

    [Fact]
    public async Task Refresh_AfterTwelveHours_Returns401()
    {
        await using var api = RealApi();
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api);
        var (_, token) = await SignInAsync(client, "hein.min");
        Clock.Advance(SessionLifetime);

        var response = await RefreshAsync(client, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Auth.RefreshInvalid", await ErrorCodeOf(response));
        Assert.Null(RefreshSetCookie(response));
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM [identity].[RefreshTokens] WHERE [RotatedAtUtc] IS NOT NULL;"));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("malformed")]
    [InlineData("revoked")]
    public async Task Refresh_MissingRevokedOrUnknownCookie_Returns401(string defect)
    {
        await using var api = RealApi();
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api);
        var (accessToken, token) = await SignInAsync(client, "hein.min");
        if (defect == "revoked")
        {
            using var bearer = Client(api, accessToken: accessToken);
            Assert.Equal(HttpStatusCode.NoContent, (await bearer.PostAsync("/api/v1/auth/logout", content: null, CancellationToken)).StatusCode);
        }

        var presented = defect switch
        {
            "missing" => null,
            "unknown" => System.Buffers.Text.Base64Url.EncodeToString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            "malformed" => "not a token; at all",
            _ => token,
        };

        var response = await RefreshAsync(client, presented);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Auth.RefreshInvalid", await ErrorCodeOf(response));
        Assert.Null(RefreshSetCookie(response));
    }

    /// <summary>S29: after S8–S11 the ledger holds exactly two family revocations (S10 and S11).</summary>
    [Fact]
    public async Task Ledger_AfterRefreshScenarios_HasExactlyTwoFamilyRevokedRows()
    {
        await using var api = RealApi();
        foreach (var name in new[] { "race.user", "grace.user", "late.user", "ancestor.user" })
        {
            await StaffUserSeeder.SeedAsync(api, name, [RoleNames.StationManager], cancellationToken: CancellationToken);
        }

        using var client = Client(api);

        // S8
        var (_, race) = await SignInAsync(client, "race.user");
        await Task.WhenAll(RefreshAsync(client, race), RefreshAsync(client, race));

        // S9
        var (_, grace) = await SignInAsync(client, "grace.user");
        await RefreshAsync(client, grace);
        Assert.Equal(HttpStatusCode.Conflict, (await RefreshAsync(client, grace)).StatusCode);

        // S10
        var (_, late) = await SignInAsync(client, "late.user");
        await RefreshAsync(client, late);
        Clock.Advance(Grace + TimeSpan.FromSeconds(1));
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, late)).StatusCode);

        // S11
        var (_, ancestor) = await SignInAsync(client, "ancestor.user");
        var next = RefreshCookieOf(await RefreshAsync(client, ancestor));
        await RefreshAsync(client, next);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, ancestor)).StatusCode);

        Assert.Equal(2, (await AuditRowsAsync(IdentityAuditActions.RefreshFamilyRevoked)).Count);
    }
}

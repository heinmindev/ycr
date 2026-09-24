using System.Net;
using System.Net.Http.Json;
using YCR.Api.Tests.Authentication;
using YCR.Domain.Identity;

namespace YCR.Api.Tests.Identity;

/// <summary>
/// R3 (ADR-0016): revocation and permission changes take effect within 30 seconds, without a new
/// sign-in (spec S17, S18, S19; ADR-0016 required test "removed permission effective within 30 s").
/// </summary>
/// <remarks>
/// The principal cache runs at its 30-second ceiling (plan P4). Each test first loads the
/// principal into the cache, makes the change, then advances the test clock by exactly 30 seconds:
/// the bound is tested, not asserted. The "cached until" half is asserted too where it is
/// deterministic, so the test would notice a cache that never served anything.
/// </remarks>
public sealed class RevocationLatencyTests(SqlServerFixture fixture) : RealAuthApiTestBase(fixture)
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    protected override string DatabasePrefix => "api_revocation_latency";

    /// <summary>S18: roles replaced with <c>[]</c> by an administrator.</summary>
    [Fact]
    public async Task RemovedRole_WithinThirtySeconds_Returns403WithoutReLogin()
    {
        await using var api = RealApi();
        var (_, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);
        var (userId, user) = await SignedInAsync(api, "hein.min", RoleNames.StationManager);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);
        Clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/v1/users/{userId}/roles", new { roles = Array.Empty<string>() }, CancellationToken)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);
        Clock.Advance(Bound - TimeSpan.FromSeconds(1));
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);
    }

    /// <summary>S18: a grant removed directly in the store (grants change by migration, R11).</summary>
    [Fact]
    public async Task RemovedRolePermissionRow_WithinThirtySeconds_Returns403()
    {
        await using var api = RealApi();
        var (_, user) = await SignedInAsync(api, "hein.min", RoleNames.StationManager);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);

        await ExecuteAsMigratorAsync(
            $"DELETE rp FROM [identity].[RolePermissions] rp JOIN [identity].[Roles] r ON r.[Id] = rp.[RoleId] WHERE r.[Name] = N'{RoleNames.StationManager}';");

        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);
        Clock.Advance(Bound);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);
    }

    /// <summary>ADR-0016 required test "disabled user" (S17).</summary>
    [Fact]
    public async Task DisabledUser_AccessTokenRejectedWithin30s()
    {
        await using var api = RealApi();
        var (_, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);
        var (userId, user) = await SignedInAsync(api, "hein.min", RoleNames.StationManager);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/v1/users/{userId}/disable", content: null, CancellationToken)).StatusCode);

        Clock.Advance(Bound);
        var response = await user.GetAsync("/api/v1/stations", CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Auth.Unauthenticated", await ErrorCodeOf(response));
    }

    /// <summary>ADR-0016 required test "session revocation" (S19): on another instance's cache, within the bound.</summary>
    [Fact]
    public async Task RevokedSession_AccessTokenRejectedWithin30s()
    {
        await using var api = RealApi();
        var (_, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var anonymous = Client(api);
        var (accessToken, _) = await SignInAsync(anonymous, "hein.min");

        // A second host over the same database and key plays another instance: its cache is not
        // evicted by the revocation served here, so only the TTL bounds it.
        await using var otherInstance = new YcrApiFactory(
            Database.ApplicationConnectionString,
            mode: AuthMode.RealTokens,
            clock: Clock,
            settings: new Dictionary<string, string?> { ["Auth:PrincipalCacheSeconds"] = "30" },
            signingKey: api.SigningKey);
        using var elsewhere = Client(otherInstance, accessToken: accessToken);
        Assert.Equal(HttpStatusCode.OK, (await elsewhere.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/v1/auth-sessions/{SessionOf(accessToken)}/revoke", content: null, CancellationToken)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await elsewhere.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);
        Clock.Advance(Bound);
        Assert.Equal(HttpStatusCode.Unauthorized, (await elsewhere.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);
    }
}

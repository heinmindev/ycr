using System.Net;
using YCR.Api.Tests.Authentication;
using YCR.Domain.Identity;

namespace YCR.Api.Tests.Identity;

/// <summary>
/// R16 (D5, U6; plan P6): in-process limits on sign-in and refresh (spec S5, S12). Bursts are fired
/// well inside one real-time window (R-3); the test clock is never moved here.
/// </summary>
public sealed class RateLimitTests(SqlServerFixture fixture) : RealAuthApiTestBase(fixture)
{
    protected override string DatabasePrefix => "api_rate_limit";

    [Fact]
    public async Task Login_OverPerUserNameLimitFromVariedAddresses_Returns429()
    {
        await using var api = RealApi(Limits(perUserName: 3));
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            using var client = Client(api, clientAddress: $"10.0.0.{attempt}");
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, "hein.min", "wrong.password.1")).StatusCode);
        }

        // The partition is the normalized username: another spelling is the same account.
        using var fourth = Client(api, clientAddress: "10.0.0.4");
        var response = await LoginAsync(fourth, "HEIN.MIN");

        await AssertTooManyRequestsAsync(response);
    }

    [Fact]
    public async Task Login_OverPerAddressLimitForVariedUserNames_Returns429()
    {
        await using var api = RealApi(Limits(perAddress: 3));
        using var client = Client(api, clientAddress: "10.0.0.1");

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, $"user.{attempt}")).StatusCode);
        }

        await AssertTooManyRequestsAsync(await LoginAsync(client, "user.4"));

        // Another address is a different partition.
        using var other = Client(api, clientAddress: "10.0.0.2");
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(other, "user.5")).StatusCode);
    }

    /// <summary>S5: a refused attempt evaluates no credential, writes no audit row and counts no failure.</summary>
    [Fact]
    public async Task Login_RejectedByLimit_EvaluatesNoCredentialAndWritesNoAudit()
    {
        await using var api = RealApi(Limits(perUserName: 2));
        var userId = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api, clientAddress: "10.0.0.1");
        await LoginAsync(client, "hein.min", "wrong.password.1");
        await LoginAsync(client, "hein.min", "wrong.password.1");
        var auditBefore = await AuditCountAsync();

        var refused = await LoginAsync(client, "hein.min");
        var refusedWrong = await LoginAsync(client, "hein.min", "wrong.password.1");

        await AssertTooManyRequestsAsync(refused);
        await AssertTooManyRequestsAsync(refusedWrong);
        Assert.Equal(auditBefore, await AuditCountAsync());
        Assert.Equal(2, (await LockoutStateAsync(userId)).Count);
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM [identity].[AuthSessions];"));
    }

    /// <summary>U6: with no override the 6th sign-in for one username and the 21st from one address are refused.</summary>
    [Fact]
    public async Task Login_DefaultLimits_AreFivePerUserNameAndTwentyPerAddress()
    {
        await using var api = RealApi(shippedRateLimits: true);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var client = Client(api, clientAddress: $"10.0.1.{attempt}");
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, "one.name")).StatusCode);
        }

        using (var sixth = Client(api, clientAddress: "10.0.1.6"))
        {
            await AssertTooManyRequestsAsync(await LoginAsync(sixth, "one.name"));
        }

        using var oneAddress = Client(api, clientAddress: "10.0.2.1");
        for (var attempt = 1; attempt <= 20; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(oneAddress, $"name.{attempt}")).StatusCode);
        }

        await AssertTooManyRequestsAsync(await LoginAsync(oneAddress, "name.21"));
    }

    [Fact]
    public async Task Refresh_OverPerAddressLimit_Returns429()
    {
        await using var api = RealApi(Limits(refreshPerAddress: 2));
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api, clientAddress: "10.0.0.1");
        var (_, token) = await SignInAsync(client, "hein.min");
        await RefreshAsync(client, "unknown");
        await RefreshAsync(client, "unknown");

        var response = await RefreshAsync(client, token);

        await AssertTooManyRequestsAsync(response);
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM [identity].[RefreshTokens] WHERE [RotatedAtUtc] IS NOT NULL;"));
    }

    /// <summary>U6: with no override the 31st refresh within a minute from one address is refused.</summary>
    [Fact]
    public async Task Refresh_DefaultLimit_IsThirtyPerAddress()
    {
        await using var api = RealApi(shippedRateLimits: true);
        using var client = Client(api, clientAddress: "10.0.3.1");

        for (var attempt = 1; attempt <= 30; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, refreshToken: null)).StatusCode);
        }

        await AssertTooManyRequestsAsync(await RefreshAsync(client, refreshToken: null));
    }

    private static Dictionary<string, string?> Limits(int perUserName = 10_000, int perAddress = 10_000, int refreshPerAddress = 10_000) => new()
    {
        ["Auth:RateLimits:LoginPerUserNamePerMinute"] = perUserName.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["Auth:RateLimits:LoginPerClientAddressPerMinute"] = perAddress.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["Auth:RateLimits:RefreshPerClientAddressPerMinute"] = refreshPerAddress.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    private static async Task AssertTooManyRequestsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("Auth.TooManyRequests", await ErrorCodeOf(response));
        Assert.Null(RefreshSetCookie(response));
    }
}

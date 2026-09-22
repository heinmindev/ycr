using System.Net;
using YCR.Api.Tests.Authentication;

namespace YCR.Api.Tests.Common;

/// <summary>
/// Spec S21b: the startup scheme allowlist, both halves.
/// </summary>
/// <remarks>
/// Defence in depth behind S21a, which asserts no <c>AuthenticationHandler&lt;&gt;</c> subtype
/// exists in <c>src/</c> at all. This proves that even if one reached a running application some
/// other way, a deployed environment would refuse to start rather than serve traffic with it.
/// </remarks>
public sealed class AuthenticationSchemeGuardTests(SqlServerFixture fixture) : ApiTestBase(fixture)
{
    protected override string DatabasePrefix => "api_scheme_guard";

    [Fact]
    public async Task Startup_UnderTesting_Succeeds()
    {
        using var client = Api.CreateAnonymousClient();

        var response = await client.GetAsync("/health/live", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Startup_WithTestHandlerOutsideTesting_Throws()
    {
        await using var production = new YcrApiFactory(
            Database.ApplicationConnectionString,
            environment: "Production");

        // The host is built lazily, so the guard runs on the first client creation.
        var failure = Assert.Throws<InvalidOperationException>(() => production.CreateAnonymousClient());

        Assert.Contains("Unexpected authentication scheme", failure.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(TestAuthHandler), failure.Message, StringComparison.Ordinal);
        Assert.Contains("ADR-0020", failure.Message, StringComparison.Ordinal);
    }
}

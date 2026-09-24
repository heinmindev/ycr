using System.Net;
using Microsoft.Extensions.DependencyInjection;
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

    /// <summary>S23: outside Testing, exactly the framework JwtBearerHandler starts (ADR-0020 item 5 as amended).</summary>
    [Fact]
    public async Task Startup_WithOnlyJwtBearerOutsideTesting_Succeeds()
    {
        await using var production = new YcrApiFactory(
            Database.ApplicationConnectionString,
            environment: "Production",
            mode: AuthMode.Unmodified,
            signingKey: new YCR.TestSupport.TestSigningKey($"ci-{Guid.NewGuid():N}", developmentOnly: false));
        using var client = production.CreateAnonymousClient();

        var response = await client.GetAsync("/health/live", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var schemes = production.Services.GetRequiredService<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>();
        var scheme = Assert.Single(await schemes.GetAllSchemesAsync());
        Assert.Equal(typeof(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerHandler), scheme.HandlerType);
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

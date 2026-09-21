using System.Net;

namespace YCR.Api.Tests.Common;

/// <summary>Spec S24: the probes are anonymous, and readiness reports the database.</summary>
public sealed class HealthEndpointsTests(SqlServerFixture fixture) : ApiTestBase(fixture)
{
    protected override string DatabasePrefix => "api_health";

    [Fact]
    public async Task Health_Live_Anonymous_Returns200()
    {
        using var client = Api.CreateAnonymousClient();

        var response = await client.GetAsync("/health/live", CancellationToken);

        // Anonymous on purpose: a liveness probe runs while authentication is broken, and
        // requiring a credential would make the platform look dead exactly then.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_Ready_WithReachableDatabase_Returns200()
    {
        using var client = Api.CreateAnonymousClient();

        var response = await client.GetAsync("/health/ready", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Readiness has to mean something. A probe that reports healthy while the database is
    /// unreachable would let an orchestrator route traffic to an instance that cannot serve it.
    /// </summary>
    [Fact]
    public async Task Health_Ready_WithUnreachableDatabase_Returns503()
    {
        await using var broken = new Authentication.YcrApiFactory(
            Database.ApplicationConnectionString.Replace(
                Database.Name, $"{Database.Name}_missing", StringComparison.Ordinal));

        using var client = broken.CreateAnonymousClient();
        var response = await client.GetAsync("/health/ready", CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}

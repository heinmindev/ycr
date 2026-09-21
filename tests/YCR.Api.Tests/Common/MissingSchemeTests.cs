using System.Net;
using System.Text.Json;
using YCR.Api.Tests.Authentication;

namespace YCR.Api.Tests.Common;

/// <summary>
/// The deployed shape: ADR-0020 ships F-001 with no authentication handler, so a protected
/// endpoint must answer <c>401</c> rather than failing.
/// </summary>
/// <remarks>
/// This is the gap every other API test has by construction. They all register the test handler
/// through <c>ConfigureTestServices</c>, so a challenge scheme always exists and the framework's
/// "no DefaultChallengeScheme found" throw never fires. Running the API against the local compose
/// database is what exposed it — an unauthenticated request returned <c>500</c>.
/// <para>
/// These tests host the application with authentication left exactly as <c>src/</c> configures
/// it, which is the only way to see what a deployment would actually do.
/// </para>
/// </remarks>
public sealed class MissingSchemeTests(SqlServerFixture fixture) : ApiTestBase(fixture)
{
    protected override string DatabasePrefix => "api_no_scheme";

    [Theory]
    [InlineData("GET", "/api/v1/stations")]
    [InlineData("GET", "/api/v1/stations/11111111-1111-1111-1111-111111111111")]
    [InlineData("POST", "/api/v1/stations")]
    [InlineData("POST", "/api/v1/stations/11111111-1111-1111-1111-111111111111/deactivate")]
    public async Task ProtectedEndpoint_WithNoAuthenticationScheme_Returns401NotServerError(
        string method,
        string path)
    {
        await using var deployed = NewDeployedFactory();
        using var client = deployed.CreateAnonymousClient();

        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        var response = await client.SendAsync(request, CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(CancellationToken);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("Common.Unauthenticated", document.RootElement.GetProperty("errorCode").GetString());
        Assert.True(document.RootElement.TryGetProperty("traceId", out _), body);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoint_WithNoAuthenticationScheme_StaysAnonymous(string path)
    {
        // The probes must not be dragged into the 401: an orchestrator has no credential, and
        // these are the only endpoints docs/20 §4 allows to be anonymous.
        await using var deployed = NewDeployedFactory();
        using var client = deployed.CreateAnonymousClient();

        var response = await client.GetAsync(path, CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private YcrApiFactory NewDeployedFactory() =>
        new(Database.ApplicationConnectionString, registerTestAuthentication: false);
}

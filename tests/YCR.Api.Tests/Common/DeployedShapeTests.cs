using System.Net;
using System.Text.Json;
using YCR.Api.Tests.Authentication;
using YCR.TestSupport;

namespace YCR.Api.Tests.Common;

/// <summary>
/// The deployed shape after F-002 (spec S22; D14; ADR-0020 as amended): the framework bearer
/// scheme is the only scheme, and its challenge writes ProblemDetails.
/// </summary>
/// <remarks>
/// Successor of F-001's <c>MissingSchemeTests</c>, whose premise — no scheme at all, a
/// <c>Common.Unauthenticated</c> body from <c>AuthorizationResultHandler</c> — F-002 retires (D14:
/// the premise is rewritten, the cases are kept). The host is <see cref="AuthMode.Unmodified"/>
/// in <c>Production</c>: no <c>ConfigureTestServices</c>, only configuration, and a signing key
/// that production accepts.
/// </remarks>
public sealed class DeployedShapeTests(SqlServerFixture fixture) : ApiTestBase(fixture)
{
    protected override string DatabasePrefix => "api_deployed_shape";

    [Theory]
    [InlineData("GET", "/api/v1/stations")]
    [InlineData("GET", "/api/v1/stations/11111111-1111-1111-1111-111111111111")]
    [InlineData("POST", "/api/v1/stations")]
    [InlineData("POST", "/api/v1/stations/11111111-1111-1111-1111-111111111111/deactivate")]
    [InlineData("GET", "/api/v1/routes")]
    [InlineData("GET", "/api/v1/routes/11111111-1111-1111-1111-111111111111")]
    [InlineData("POST", "/api/v1/routes")]
    [InlineData("POST", "/api/v1/routes/11111111-1111-1111-1111-111111111111/deactivate")]
    public async Task ProtectedEndpoint_Anonymous_Returns401BearerChallengeWithProblemDetails(string method, string path)
    {
        await using var deployed = NewDeployedFactory();
        using var client = deployed.CreateAnonymousClient();

        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        var response = await client.SendAsync(request, CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync(CancellationToken);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("Auth.Unauthenticated", document.RootElement.GetProperty("errorCode").GetString());
        Assert.True(document.RootElement.TryGetProperty("traceId", out _), body);

        // R24 / P10: the challenge carries the security headers too.
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Contains("frame-ancestors 'none'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithGarbageBearer_Returns401WithoutValidationDetail()
    {
        await using var deployed = NewDeployedFactory();
        using var client = deployed.CreateBearerClient("not.a.token");

        var response = await client.GetAsync("/api/v1/stations", CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        // P11: IncludeErrorDetails = false — no validation reason is echoed to the caller.
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).ToString());
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoint_Anonymous_StaysAnonymous(string path)
    {
        // The probes must not be dragged into the 401: an orchestrator has no credential, and
        // these are the only endpoints docs/20 §4 allows to be anonymous besides sign-in.
        await using var deployed = NewDeployedFactory();
        using var client = deployed.CreateAnonymousClient();

        var response = await client.GetAsync(path, CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void CommonUnauthenticated_AppearsNowhereInSrc()
    {
        var src = Path.Combine(RepositoryRoot(), "src");

        var offenders = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file).Contains("Common.Unauthenticated", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void AuthorizationResultHandler_TypeNoLongerExists()
    {
        var apiTypes = typeof(Program).Assembly.GetTypes();

        Assert.DoesNotContain(apiTypes, type => type.Name == "AuthorizationResultHandler");
        Assert.DoesNotContain(apiTypes, type => typeof(Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler).IsAssignableFrom(type));
    }

    private YcrApiFactory NewDeployedFactory() =>
        new(
            Database.ApplicationConnectionString,
            environment: "Production",
            mode: AuthMode.Unmodified,
            signingKey: new TestSigningKey($"ci-{Guid.NewGuid():N}", developmentOnly: false));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "YCR.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No YCR.sln above the test directory.");
    }
}

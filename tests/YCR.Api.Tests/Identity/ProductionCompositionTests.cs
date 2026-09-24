using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YCR.Api.Tests.Authentication;
using YCR.TestSupport;
using YCR.Worker;

namespace YCR.Api.Tests.Identity;

/// <summary>
/// S25 (<c>docs/21</c> §Tests): the unmodified production composition, end to end, with real
/// tokens — no <c>ConfigureTestServices</c>, no test clock, no test handler; configuration only.
/// The first administrator comes from the Worker's bootstrap command (D10) over the same database.
/// </summary>
public sealed class ProductionCompositionTests(SqlServerFixture fixture) : ApiTestBase(fixture)
{
    private const string InitialPassword = "kyauk.tan.12.first";
    private const string ChangedPassword = "shwe.dagon.2026.own";

    protected override string DatabasePrefix => "api_production_composition";

    [Fact]
    public async Task Bootstrap_ChangePassword_Login_ListStations_WithNoTestOverrides()
    {
        Assert.Equal(BootstrapAdministratorCli.Created, await BootstrapAsync("hein.admin", InitialPassword));
        await using var api = Deployed();
        using var client = OriginClient(api);

        // Must-change: the bootstrap password signs in but may only change itself (R26).
        var first = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = "hein.admin", password = InitialPassword }, CancellationToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", await AccessTokenOf(first));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);
        var change = await client.PostAsJsonAsync("/api/v1/auth/password", new { currentPassword = InitialPassword, newPassword = ChangedPassword }, CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = "hein.admin", password = ChangedPassword }, CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", await AccessTokenOf(login));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task ListStations_Anonymous_Returns401AuthUnauthenticated()
    {
        await using var api = Deployed();
        using var client = api.CreateClient();

        var response = await client.GetAsync("/api/v1/stations", CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        Assert.Equal("Auth.Unauthenticated", body.RootElement.GetProperty("errorCode").GetString());
    }

    private YcrApiFactory Deployed() =>
        new(
            Database.ApplicationConnectionString,
            environment: "Production",
            mode: AuthMode.Unmodified,
            signingKey: new TestSigningKey($"ci-{Guid.NewGuid():N}", developmentOnly: false));

    private static HttpClient OriginClient(YcrApiFactory api)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", YcrApiFactory.AllowedOrigin);
        return client;
    }

    private async Task<int> BootstrapAsync(string userName, string password)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Application"] = Database.ApplicationConnectionString })
            .Build();
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddBootstrapAdministrator(configuration)
            .BuildServiceProvider();

        return await BootstrapAdministratorCli.RunAsync(
            [BootstrapAdministratorCli.CommandName, "--username", userName],
            new StringReader(password),
            TextWriter.Null,
            TextWriter.Null,
            services,
            CancellationToken);
    }

    private static async Task<string> AccessTokenOf(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        return body.RootElement.GetProperty("accessToken").GetString()!;
    }
}

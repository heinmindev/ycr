using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using YCR.Api.Endpoints.Network;
using YCR.Api.Tests.Authentication;
using YCR.Application.Common.Authorization;

namespace YCR.Api.Tests.Network;

/// <summary>
/// F-003 R27 (spec Amendment 3; REQUIRED CONTROL, hein, 2026-09-25, review S-1): the 32 KB
/// request-body limit of <c>POST /api/v1/routes</c>, and the bounded answer to malformed JSON.
/// </summary>
/// <remarks>
/// These tests run the API on real Kestrel (<c>WebApplicationFactory.UseKestrel</c>), not on
/// <c>TestServer</c>. The limit is endpoint metadata that endpoint routing copies into the
/// server's <c>IHttpMaxRequestBodySizeFeature</c>; Kestrel provides that feature and
/// <c>TestServer</c> does not, so only a Kestrel-hosted test proves the limit the deployed API
/// enforces.
/// </remarks>
public sealed class RouteRequestLimitTests(SqlServerFixture fixture) : ApiTestBase(fixture)
{
    private const int LimitBytes = 32 * 1024;

    protected override string DatabasePrefix => "api_route_limits";

    [Fact]
    public void Limit_Is32KiB()
    {
        Assert.Equal(LimitBytes, RouteEndpoints.CreateRouteMaxRequestBodyBytes);
    }

    /// <summary>
    /// A body over 32 KB is refused before binding with <c>413</c>, whether the client declares
    /// its length or streams it chunked, and nothing is written. Without the limit the same body
    /// is parsed whole and then refused with <c>400</c> (checked when the limit was added).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Post_WithBodyOver32KiB_Returns413WithoutInternals(bool chunked)
    {
        await using var kestrel = StartKestrel();
        using var client = AdminClient(kestrel);

        // Well-formed JSON whose only fault is its size: one name of about 40 KB.
        var json = JsonSerializer.Serialize(new
        {
            code = "R1",
            nameEn = new string('A', 40 * 1024),
            nameMy = "မြို့ပတ်ရထားလမ်း",
            isClosed = false,
            stationIds = new[] { Guid.CreateVersion7(), Guid.CreateVersion7() },
        });
        Assert.True(Encoding.UTF8.GetByteCount(json) > LimitBytes);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/routes")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.TransferEncodingChunked = chunked;

        using var response = await client.SendAsync(request, CancellationToken);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        await AssertBoundedAndOpaqueAsync(response);
        await AssertNoRouteWrittenAsync();
    }

    /// <summary>Malformed JSON is a bounded <c>400</c> that reveals nothing of the parser or the server.</summary>
    [Theory]
    [InlineData("{\"code\": \"R1\", \"stationIds\": [")]
    [InlineData("{\"code\": \"R1\",, }")]
    [InlineData("not json")]
    public async Task Post_WithMalformedJson_Returns400WithoutInternals(string body)
    {
        await using var kestrel = StartKestrel();
        using var client = AdminClient(kestrel);

        using var response = await client.PostAsync(
            "/api/v1/routes",
            new StringContent(body, Encoding.UTF8, "application/json"),
            CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertBoundedAndOpaqueAsync(response);
        await AssertNoRouteWrittenAsync();
    }

    /// <summary>
    /// The largest valid request — 200 ids (R26), a 10-character code and two 100-character
    /// names, the Myanmar one at three UTF-8 bytes a character — is under the limit and reaches
    /// the handler, which answers for the first unknown station.
    /// </summary>
    [Fact]
    public async Task Post_WithLargestValidRequest_IsUnderTheLimitAndReachesTheHandler()
    {
        await using var kestrel = StartKestrel();
        using var client = AdminClient(kestrel);

        var json = JsonSerializer.Serialize(new
        {
            code = "ABCDE12345",
            nameEn = new string('A', 100),
            nameMy = new string('မ', 100),
            isClosed = false,
            stationIds = Enumerable.Range(0, 200).Select(_ => Guid.CreateVersion7()).ToArray(),
        });
        Assert.True(Encoding.UTF8.GetByteCount(json) < LimitBytes / 3);

        using var response = await client.PostAsync(
            "/api/v1/routes",
            new StringContent(json, Encoding.UTF8, "application/json"),
            CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        Assert.Equal("Network.RouteStationNotFound", document.RootElement.GetProperty("errorCode").GetString());
        await AssertNoRouteWrittenAsync();
    }

    private YcrApiFactory StartKestrel()
    {
        var kestrel = new YcrApiFactory(Database.ApplicationConnectionString);
        kestrel.UseKestrel(0);
        kestrel.StartServer();
        return kestrel;
    }

    private static HttpClient AdminClient(YcrApiFactory kestrel)
    {
        var address = kestrel.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.First();

        var client = new HttpClient { BaseAddress = new Uri(address) };
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.CreateVersion7().ToString());
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.PermissionsHeader,
            string.Join(',', Permissions.RoutesManage, Permissions.RoutesRead));
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "RailwayAdministrator");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return client;
    }

    /// <summary>
    /// REQUIRED CONTROL (docs/18, data disclosure): a short answer with no stack trace, exception
    /// type, framework or parser detail, or server path.
    /// </summary>
    private async Task AssertBoundedAndOpaqueAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(CancellationToken);

        Assert.True(body.Length < 1024, body);
        Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" at ", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("System.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("YCR.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Kestrel", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LineNumber", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BytePosition", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(":\\", body, StringComparison.Ordinal);
        Assert.DoesNotContain("/src/", body, StringComparison.Ordinal);
        Assert.DoesNotContain(Database.Name, body, StringComparison.OrdinalIgnoreCase);

        // The framework's ProblemDetails (status-code pages): the status and a trace id an
        // operator can follow into the logs, nothing more.
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(body);
        Assert.Equal((int)response.StatusCode, document.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
    }

    private async Task AssertNoRouteWrittenAsync()
    {
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(Database.MigratorConnectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = new Microsoft.Data.SqlClient.SqlCommand(
            "SELECT (SELECT COUNT(*) FROM [network].[Routes]) + (SELECT COUNT(*) FROM [network].[RouteStations]);",
            connection);

        Assert.Equal(0, (int)(await command.ExecuteScalarAsync(CancellationToken))!);
    }
}

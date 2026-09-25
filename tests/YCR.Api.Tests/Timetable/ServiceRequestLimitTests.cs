using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using YCR.Api.Endpoints.Timetable;
using YCR.Api.Tests.Authentication;
using YCR.Application.Common.Authorization;
using YCR.TestSupport;

namespace YCR.Api.Tests.Timetable;

/// <summary>
/// F-004 R31, S43 (REQUIRED CONTROL; plan P20): the 32 KiB limit of <c>POST /api/v1/services</c>,
/// the 1 KiB limit of <c>POST /api/v1/services/{id}/withdraw</c>, and the bounded answer to
/// malformed JSON.
/// </summary>
/// <remarks>
/// On real Kestrel (<c>WebApplicationFactory.UseKestrel</c>), not <c>TestServer</c>: the limit is
/// endpoint metadata that endpoint routing copies into the server's
/// <c>IHttpMaxRequestBodySizeFeature</c>, which only Kestrel provides (the F-003
/// <c>RouteRequestLimitTests</c> pattern).
/// </remarks>
public sealed class ServiceRequestLimitTests(SqlServerFixture fixture) : ApiTestBase(fixture)
{
    private const int CreateLimitBytes = 32 * 1024;
    private const int WithdrawLimitBytes = 1024;

    protected override string DatabasePrefix => "api_service_limits";

    [Fact]
    public void Limits_Are32KiBAnd1KiB()
    {
        Assert.Equal(CreateLimitBytes, ServiceEndpoints.CreateServiceMaxRequestBodyBytes);
        Assert.Equal(WithdrawLimitBytes, ServiceEndpoints.WithdrawServiceMaxRequestBodyBytes);
    }

    /// <summary>A body over the endpoint's limit is refused before binding, declared or chunked.</summary>
    [Theory]
    [InlineData("create", false)]
    [InlineData("create", true)]
    [InlineData("withdraw", false)]
    [InlineData("withdraw", true)]
    public async Task Post_WithBodyOverLimit_Returns413WithoutInternals(string endpoint, bool chunked)
    {
        await using var kestrel = StartKestrel();
        using var client = AdminClient(kestrel);

        // Well-formed JSON whose only fault is its size.
        var (path, json, limit) = endpoint == "create"
            ? ("/api/v1/services", JsonSerializer.Serialize(new
            {
                code = "S101",
                nameEn = new string('A', 40 * 1024),
                nameMy = "မြို့ပတ်ရထား",
                routeId = Guid.CreateVersion7(),
                direction = "Forward",
                stopStationIds = new[] { Guid.CreateVersion7(), Guid.CreateVersion7() },
                operatingDays = new[] { "Monday" },
                effectiveFrom = "2026-10-05",
            }), CreateLimitBytes)
            : ($"/api/v1/services/{Guid.CreateVersion7()}/withdraw", JsonSerializer.Serialize(new
            {
                withdrawFrom = "2026-11-01",
                padding = new string(' ', 2 * 1024),
            }), WithdrawLimitBytes);
        Assert.True(Encoding.UTF8.GetByteCount(json) > limit);

        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.TransferEncodingChunked = chunked;

        using var response = await client.SendAsync(request, CancellationToken);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        await AssertBoundedAndOpaqueAsync(response);
        await AssertNothingWrittenAsync();
    }

    /// <summary>Malformed JSON is a bounded <c>400</c> that reveals nothing of the parser or the server.</summary>
    [Theory]
    [InlineData("create", "{\"code\": \"S101\", \"stopStationIds\": [")]
    [InlineData("create", "{\"code\": \"S101\",, }")]
    [InlineData("create", "not json")]
    [InlineData("withdraw", "{\"withdrawFrom\": ")]
    [InlineData("withdraw", "not json")]
    public async Task Post_WithMalformedJson_Returns400WithoutInternals(string endpoint, string body)
    {
        await using var kestrel = StartKestrel();
        using var client = AdminClient(kestrel);
        var path = endpoint == "create" ? "/api/v1/services" : $"/api/v1/services/{Guid.CreateVersion7()}/withdraw";

        using var response = await client.PostAsync(path, new StringContent(body, Encoding.UTF8, "application/json"), CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertBoundedAndOpaqueAsync(response);
        await AssertNothingWrittenAsync();
    }

    /// <summary>
    /// The largest valid create — 200 stop ids, all seven days, a 10-character code and two
    /// 100-character names, the Myanmar one escaped as <c>\uXXXX</c> the way <c>System.Text.Json</c>
    /// writes it — is under a third of the limit and reaches the handler, which answers for the
    /// unknown route.
    /// </summary>
    [Fact]
    public async Task Post_WithLargestValidCreate_IsUnderTheLimitAndReachesTheHandler()
    {
        await using var kestrel = StartKestrel();
        using var client = AdminClient(kestrel);

        var json = JsonSerializer.Serialize(new
        {
            code = "ABCDE12345",
            nameEn = new string('A', 100),
            nameMy = new string('မ', 100),
            routeId = Guid.CreateVersion7(),
            direction = "Reverse",
            stopStationIds = Enumerable.Range(0, 200).Select(_ => Guid.CreateVersion7()).ToArray(),
            operatingDays = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" },
            effectiveFrom = "2026-10-05",
            effectiveTo = "2026-12-31",
        });
        Assert.Contains("\\u", json, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(json) < CreateLimitBytes / 3);

        using var response = await client.PostAsync(
            "/api/v1/services", new StringContent(json, Encoding.UTF8, "application/json"), CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        Assert.Equal("Timetable.ServiceRouteNotFound", document.RootElement.GetProperty("errorCode").GetString());
        await AssertNothingWrittenAsync();
    }

    /// <summary>The only valid withdrawal body reaches the handler, which answers for the unknown service.</summary>
    [Fact]
    public async Task Withdraw_WithLargestValidBody_ReachesTheHandler()
    {
        await using var kestrel = StartKestrel();
        using var client = AdminClient(kestrel);
        const string Json = "{\"withdrawFrom\":\"2026-11-01\"}";
        Assert.Equal(29, Encoding.UTF8.GetByteCount(Json));

        using var response = await client.PostAsync(
            $"/api/v1/services/{Guid.CreateVersion7()}/withdraw", new StringContent(Json, Encoding.UTF8, "application/json"), CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        Assert.Equal("Timetable.ServiceNotFound", document.RootElement.GetProperty("errorCode").GetString());
    }

    /// <summary>Kestrel with the spec §4 clock, so the dates in these bodies are never "in the past".</summary>
    private YcrApiFactory StartKestrel()
    {
        var kestrel = new YcrApiFactory(
            Database.ApplicationConnectionString, clock: new TestClock(new DateTimeOffset(2026, 10, 1, 3, 0, 0, TimeSpan.Zero)));
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
            string.Join(',', Permissions.ServicesManage, Permissions.ServicesRead));
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

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(body);
        Assert.Equal((int)response.StatusCode, document.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
    }

    private async Task AssertNothingWrittenAsync()
    {
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(Database.MigratorConnectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = new Microsoft.Data.SqlClient.SqlCommand(
            "SELECT (SELECT COUNT(*) FROM [timetable].[Services]) + (SELECT COUNT(*) FROM [timetable].[ServiceStops]);",
            connection);

        Assert.Equal(0, (int)(await command.ExecuteScalarAsync(CancellationToken))!);
    }
}

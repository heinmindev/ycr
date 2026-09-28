using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using YCR.Api.Contracts.Timetable;
using YCR.Api.Endpoints.Timetable;
using YCR.Api.Tests.Authentication;
using YCR.Application.Common.Authorization;
using YCR.TestSupport;

namespace YCR.Api.Tests.Timetable;

/// <summary>
/// F-005 R41, SV47 (REQUIRED CONTROL; plan P15, ruling Q1): the 2 MiB limit of
/// <c>POST /api/v1/schedules/versions</c>, the three caps (250 services, 200 stop times per service,
/// 10,000 per version), and the bounded answer to malformed JSON.
/// </summary>
/// <remarks>
/// On real Kestrel (<c>WebApplicationFactory.UseKestrel</c>), not <c>TestServer</c>: the limit is
/// endpoint metadata that endpoint routing copies into the server's
/// <c>IHttpMaxRequestBodySizeFeature</c>, which only Kestrel provides (the F-003/F-004 pattern).
/// </remarks>
public sealed class ScheduleVersionRequestLimitTests(SqlServerFixture fixture) : ApiTestBase(fixture)
{
    private const int LimitBytes = 2 * 1024 * 1024;

    protected override string DatabasePrefix => "api_schedule_limits";

    [Fact]
    public void Limits_Are2MiBAndTheThreeCaps()
    {
        Assert.Equal(LimitBytes, ScheduleVersionEndpoints.CreateScheduleVersionMaxRequestBodyBytes);
        Assert.Equal(250, CreateScheduleVersionRequestValidator.MaxServicesPerVersion);
        Assert.Equal(200, CreateScheduleVersionRequestValidator.MaxStopTimesPerService);
        Assert.Equal(10_000, CreateScheduleVersionRequestValidator.MaxStopTimesPerVersion);
    }

    /// <summary>SV47: a body over 2 MiB is refused with 413 before binding, declared or chunked.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Post_WithBodyOverTheLimit_Returns413WithoutInternals(bool chunked)
    {
        await using var kestrel = StartKestrel();
        using var client = AdminClient(kestrel);

        // Well-formed JSON whose only fault is its size.
        var json = JsonSerializer.Serialize(new
        {
            nameEn = new string('A', LimitBytes + 1024),
            nameMy = "မြို့ပတ်",
            effectiveFrom = "2026-10-05",
            services = Array.Empty<object>(),
        });
        Assert.True(Encoding.UTF8.GetByteCount(json) > LimitBytes);

        using var response = await SendAsync(client, json, chunked);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        await AssertBoundedAndOpaqueAsync(response);
        await AssertNothingWrittenAsync();
    }

    /// <summary>SV47: malformed JSON is a bounded 400 that reveals nothing of the parser or the server.</summary>
    [Theory]
    [InlineData("{\"nameEn\": \"N\", \"services\": [")]
    [InlineData("{\"nameEn\": \"N\",, }")]
    [InlineData("not json")]
    public async Task Post_WithMalformedJson_Returns400WithoutInternals(string body)
    {
        await using var kestrel = StartKestrel();
        using var client = AdminClient(kestrel);

        using var response = await SendAsync(client, body, chunked: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertBoundedAndOpaqueAsync(response);
        await AssertNothingWrittenAsync();
    }

    /// <summary>SV47, Q1: one over each cap is 400 Common.ValidationFailed, before the handler.</summary>
    [Theory]
    [InlineData(251, 2, false)] // 251 services
    [InlineData(1, 201, false)] // 201 stop times in one service
    [InlineData(0, 0, true)] // 10,001 stop times in total, every service within its own cap
    public async Task Post_OneOverACap_Returns400ValidationFailed(int services, int stopTimesEach, bool overTotal)
    {
        await using var kestrel = StartKestrel();
        using var client = AdminClient(kestrel);
        var json = overTotal ? Body([41, .. Enumerable.Repeat(40, 249)]) : Body([.. Enumerable.Repeat(stopTimesEach, services)]);
        if (overTotal)
        {
            // Only the total is over: 250 services (the cap), none over 200 stop times.
            using var shape = JsonDocument.Parse(json);
            var entries = shape.RootElement.GetProperty("services").EnumerateArray().ToList();
            Assert.Equal(250, entries.Count);
            Assert.Equal(10_001, entries.Sum(entry => entry.GetProperty("stopTimes").GetArrayLength()));
        }

        using var response = await SendAsync(client, json, chunked: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Common.ValidationFailed", await ErrorCodeAsync(response));
        await AssertNothingWrittenAsync();
    }

    /// <summary>
    /// SV47: exactly at the caps — 250 services, one with 200 stop times, 10,000 in total, both names
    /// 100 characters escaped as <c>\uXXXX</c> — is at most 568,300 bytes, under the limit, and reaches
    /// the handler, which answers for the unknown services (nothing is written).
    /// </summary>
    [Fact]
    public async Task Post_ExactlyAtTheCaps_IsUnderTheLimitAndReachesTheHandler()
    {
        await using var kestrel = StartKestrel();
        using var client = AdminClient(kestrel);
        // 1 × 200 + 245 × 40 = 10,000 stop times; 4 more services with none make 250 services.
        var json = Body([200, .. Enumerable.Repeat(40, 245), 0, 0, 0, 0], nameEn: new string('A', 100), nameMy: new string('မ', 100));
        using var document = JsonDocument.Parse(json);
        Assert.Equal(250, document.RootElement.GetProperty("services").GetArrayLength());
        Assert.Equal(
            10_000,
            document.RootElement.GetProperty("services").EnumerateArray().Sum(service => service.GetProperty("stopTimes").GetArrayLength()));
        Assert.Contains("\\u", json, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(json) <= 568_300, Encoding.UTF8.GetByteCount(json).ToString(System.Globalization.CultureInfo.InvariantCulture));

        using var response = await SendAsync(client, json, chunked: false);

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
        Assert.Equal("Timetable.ScheduleServiceNotFound", await ErrorCodeAsync(response));
        await AssertNothingWrittenAsync();
    }

    /// <summary>
    /// SV47, security (docs/18 API abuse): 3 MiB declared and chunked → 413; a sub-limit body with
    /// 10,001 stop times → 400; no version row and no event — refused before any database work.
    /// </summary>
    [Fact]
    public async Task Post_LargeBodyAbuse_IsRefusedBeforeAnyDatabaseWork()
    {
        await using var kestrel = StartKestrel();
        using var client = AdminClient(kestrel);
        var huge = JsonSerializer.Serialize(new
        {
            nameEn = "N",
            nameMy = "M",
            effectiveFrom = "2026-10-05",
            services = new[] { new { serviceId = Guid.CreateVersion7(), stopTimes = new[] { new { position = 1, departure = new string('0', 3 * 1024 * 1024) } } } },
        });

        // Declared 3 MiB with Expect: 100-continue: Kestrel refuses on the Content-Length alone, so
        // the client never writes the body into a connection the server closes. A chunked body has
        // no length to refuse up front: Kestrel reads it to the limit, answers 413 and resets the
        // connection, and a 3 MiB chunked write can lose that answer to the reset (observed once in
        // four runs). The chunked leg is therefore just over the limit, as in
        // Post_WithBodyOverTheLimit_Returns413WithoutInternals (progress.md V12).
        var overLimit = JsonSerializer.Serialize(new
        {
            nameEn = new string('A', LimitBytes + (16 * 1024)),
            nameMy = "M",
            effectiveFrom = "2026-10-05",
            services = Array.Empty<object>(),
        });
        using var declared = await SendAsync(client, huge, chunked: false, expectContinue: true);
        using var streamed = await SendAsync(client, overLimit, chunked: true);
        using var tooMany = await SendAsync(client, Body([41, .. Enumerable.Repeat(40, 249)]), chunked: false);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, declared.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, streamed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        await AssertBoundedAndOpaqueAsync(declared);
        await AssertBoundedAndOpaqueAsync(streamed);
        await AssertNothingWrittenAsync();
    }

    /// <summary>
    /// A create body with one service per entry of <paramref name="stopTimesPerService"/>, unknown
    /// service ids, and valid increasing times; positions up to 200 (3 digits).
    /// </summary>
    private static string Body(int[] stopTimesPerService, string nameEn = "N", string nameMy = "M") =>
        JsonSerializer.Serialize(new
        {
            nameEn,
            nameMy,
            effectiveFrom = "2026-10-05",
            services = stopTimesPerService.Select(count => new
            {
                serviceId = Guid.CreateVersion7(),
                stopTimes = Enumerable.Range(1, count).Select(position => new
                {
                    position,
                    arrival = position == 1 ? null : Clock(position * 5),
                    departure = position == count ? null : Clock(position * 5 + 1),
                }).ToArray(),
            }).ToArray(),
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static string Clock(int minutes) => $"{minutes / 60 % 24:00}:{minutes % 60:00}";

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string json, bool chunked, bool expectContinue = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/schedules/versions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.TransferEncodingChunked = chunked;
        request.Headers.ExpectContinue = expectContinue ? true : null;
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
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
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, string.Join(',', Permissions.SchedulesManage, Permissions.SchedulesRead));
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "RailwayAdministrator");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return client;
    }

    private async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        return document.RootElement.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
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
            """
            SELECT (SELECT COUNT(*) FROM [timetable].[ScheduleVersions])
                 + (SELECT COUNT(*) FROM [timetable].[ScheduleStopTimes])
                 + (SELECT COUNT(*) FROM [audit].[AuditEvents] WHERE [Action] LIKE N'Timetable.ScheduleVersion%');
            """,
            connection);

        Assert.Equal(0, (int)(await command.ExecuteScalarAsync(CancellationToken))!);
    }
}

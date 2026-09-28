using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using YCR.Api.Common;
using YCR.Api.Tests.Authentication;
using YCR.Application.Common.Authorization;
using YCR.TestSupport;

namespace YCR.Api.Tests.Common;

/// <summary>
/// T-042 (ENGINEERING DECISION, tech lead, hein, 2026-09-28; docs/20 §4): the request-size and
/// JSON policy of every endpoint that takes a body, and the stable codes of the framework's own
/// refusals — <c>413 Common.RequestTooLarge</c> and <c>400 Common.MalformedRequest</c>.
/// </summary>
/// <remarks>
/// On real Kestrel (<c>WebApplicationFactory.UseKestrel</c>), not <c>TestServer</c>: body limits are
/// enforced by Kestrel's <c>IHttpMaxRequestBodySizeFeature</c>, which <c>TestServer</c> does not
/// provide (the F-003 <c>RouteRequestLimitTests</c> approach). Every capped endpoint is exercised
/// with the same five bodies: over its limit, malformed, of the wrong JSON type, nested deeper than
/// 32, and just under its limit at depth 32.
/// </remarks>
public sealed class RequestLimitTests(SqlServerFixture fixture) : ApiTestBase(fixture)
{
    /// <summary>Every endpoint that binds a JSON body, by endpoint name.</summary>
    private static readonly Dictionary<string, CappedEndpoint> Capped = new CappedEndpoint[]
    {
        new("Login", "POST", "/api/v1/auth/login", 4 * 1024, "userName", """{"userName":"someone","password":"not-the-password"}"""),
        new("ChangeOwnPassword", "POST", "/api/v1/auth/password", 4 * 1024, "newPassword", """{"currentPassword":"a","newPassword":"b"}"""),
        new("CreateUser", "POST", "/api/v1/users", 4 * 1024, "userName", """{"userName":"new.user","password":"p","roles":["StationManager"]}"""),
        new("ReplaceUserRoles", "PUT", $"/api/v1/users/{Guid.CreateVersion7()}/roles", 4 * 1024, "roles", """{"roles":["StationManager"]}"""),
        new("ResetUserPassword", "POST", $"/api/v1/users/{Guid.CreateVersion7()}/password-reset", 4 * 1024, "newPassword", """{"newPassword":"p"}"""),
        new("CreateStation", "POST", "/api/v1/stations", 4 * 1024, "code", """{"code":"","nameEn":"","nameMy":""}"""),
        new("CreateRoute", "POST", "/api/v1/routes", 32 * 1024, "code", """{"code":"","nameEn":"","nameMy":"","isClosed":false,"stationIds":[]}"""),
        new("CreateService", "POST", "/api/v1/services", 32 * 1024, "code", """{"code":""}"""),
        new("WithdrawService", "POST", $"/api/v1/services/{Guid.CreateVersion7()}/withdraw", 1024, "withdrawFrom", """{"withdrawFrom":""}"""),
        new("CreateScheduleVersion", "POST", "/api/v1/schedules/versions", 2 * 1024 * 1024, "nameEn", """{"nameEn":""}"""),
    }.ToDictionary(endpoint => endpoint.Name);

    protected override string DatabasePrefix => "api_request_limits";

    public static TheoryData<string> CappedNames => [.. Capped.Keys];

    public static TheoryData<string, bool> CappedNamesChunked =>
        [.. Capped.Keys.SelectMany(name => new[] { (name, false), (name, true) })];

    public static TheoryData<string, string> CappedNamesMalformed =>
        [.. Capped.Keys.SelectMany(name => new[]
        {
            (name, "{\"a\": ["),
            (name, "{\"a\": 1,, }"),
            (name, "not json"),
        })];

    [Fact]
    public void Limits_AreTheRuledValues()
    {
        Assert.Equal(64 * 1024, RequestLimits.GlobalMaxRequestBodyBytes);
        Assert.Equal(4 * 1024, RequestLimits.SmallJsonBodyMaxBytes);
        Assert.Equal(32, RequestLimits.JsonMaxDepth);
    }

    /// <summary>Ruling 1: the server-wide backstop is set on the Kestrel the API runs on.</summary>
    [Fact]
    public async Task Kestrel_GlobalLimit_Is64KiB()
    {
        await using var kestrel = StartKestrel();

        var options = kestrel.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;

        Assert.Equal(RequestLimits.GlobalMaxRequestBodyBytes, options.Limits.MaxRequestBodySize);
    }

    /// <summary>
    /// Ruling 1, behaviour: a pipeline step that reads a body and declares no limit of its own (a
    /// test-only probe, first in the pipeline) is held to 64 KiB by Kestrel itself.
    /// </summary>
    [Fact]
    public async Task Kestrel_BodyOver64KiBWithNoEndpointLimit_IsRefusedWith413()
    {
        await using var kestrel = StartKestrel(services => services.AddSingleton<IStartupFilter, BodyProbeStartupFilter>());
        using var client = Client(kestrel);

        using var under = await client.PostAsync(
            BodyProbeStartupFilter.Path,
            new ByteArrayContent(new byte[RequestLimits.GlobalMaxRequestBodyBytes]),
            CancellationToken);
        using var over = await client.PostAsync(
            BodyProbeStartupFilter.Path,
            new ByteArrayContent(new byte[RequestLimits.GlobalMaxRequestBodyBytes + 1]),
            CancellationToken);

        Assert.Equal(HttpStatusCode.OK, under.StatusCode);
        Assert.Equal(
            RequestLimits.GlobalMaxRequestBodyBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            await under.Content.ReadAsStringAsync(CancellationToken));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, over.StatusCode);
    }

    /// <summary>
    /// Rulings 1 and 2, by metadata: every endpoint that accepts a body declares its own limit, with
    /// the ruled value, and no other endpoint declares one (body-less actions rely on the global
    /// limit). A new body endpoint without a limit fails here. <c>POST /auth/refresh</c> is capped by
    /// ruling 2 although it reads only its cookie.
    /// </summary>
    [Fact]
    public async Task Endpoints_DeclareExactlyTheRuledLimits()
    {
        await using var kestrel = StartKestrel();
        var endpoints = kestrel.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

        var expected = Capped.Values.ToDictionary(endpoint => endpoint.Name, endpoint => (long?)endpoint.LimitBytes);
        expected["RefreshSession"] = RequestLimits.SmallJsonBodyMaxBytes;

        var declared = endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<IRequestSizeLimitMetadata>() is not null)
            .ToDictionary(
                endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName ?? endpoint.DisplayName!,
                endpoint => endpoint.Metadata.GetMetadata<IRequestSizeLimitMetadata>()!.MaxRequestBodySize);

        Assert.Equal(expected.OrderBy(pair => pair.Key), declared.OrderBy(pair => pair.Key));

        var bodyEndpoints = endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAcceptsMetadata>() is not null)
            .Select(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName)
            .ToHashSet();
        Assert.Equal(Capped.Keys.Order(), bodyEndpoints.Order());
    }

    /// <summary>Rulings 2 and 3: a well-formed body over the endpoint's limit, declared or chunked.</summary>
    [Theory]
    [MemberData(nameof(CappedNamesChunked))]
    public async Task Post_WithBodyOverTheLimit_Returns413RequestTooLarge(string name, bool chunked)
    {
        var endpoint = Capped[name];
        await using var kestrel = StartKestrel();
        using var client = Client(kestrel);
        var json = Padded(endpoint.Sample, endpoint.LimitBytes + 1024);
        Assert.True(Encoding.UTF8.GetByteCount(json) > endpoint.LimitBytes);
        var auditBefore = await AuditCountAsync();

        using var response = await SendAsync(client, endpoint, json, chunked);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        await AssertCodedAndOpaqueAsync(response, RequestLimits.RequestTooLargeCode);
        Assert.Equal(auditBefore, await AuditCountAsync());
    }

    /// <summary>Ruling 3: JSON that cannot be parsed.</summary>
    [Theory]
    [MemberData(nameof(CappedNamesMalformed))]
    public async Task Post_WithMalformedJson_Returns400MalformedRequest(string name, string body)
    {
        await AssertMalformedAsync(Capped[name], body);
    }

    /// <summary>Ruling 3: well-formed JSON that cannot bind — a number where the contract has a string.</summary>
    [Theory]
    [MemberData(nameof(CappedNames))]
    public async Task Post_WithTheWrongJsonType_Returns400MalformedRequest(string name)
    {
        var endpoint = Capped[name];

        await AssertMalformedAsync(endpoint, $"{{\"{endpoint.StringField}\": 1}}");
    }

    /// <summary>Ruling 5: nesting of 33, even inside a property the contract ignores.</summary>
    [Theory]
    [MemberData(nameof(CappedNames))]
    public async Task Post_NestedDeeperThan32_Returns400MalformedRequest(string name)
    {
        var endpoint = Capped[name];

        await AssertMalformedAsync(endpoint, WithNesting(endpoint.Sample, depth: 33));
    }

    /// <summary>
    /// The limits are not tighter than ruled: a body a little under the limit, nested exactly 32
    /// deep in an unknown property (ruling 6: ignored), passes both and reaches the endpoint's
    /// filters or handler, which answer with their own status.
    /// </summary>
    [Theory]
    [MemberData(nameof(CappedNames))]
    public async Task Post_UnderTheLimitAtDepth32_IsNotRefusedByThePolicy(string name)
    {
        var endpoint = Capped[name];
        await using var kestrel = StartKestrel();
        using var client = Client(kestrel);
        var json = Padded(WithNesting(endpoint.Sample, depth: 32), endpoint.LimitBytes - 256);
        Assert.InRange(Encoding.UTF8.GetByteCount(json), endpoint.LimitBytes - 512, endpoint.LimitBytes);

        using var response = await SendAsync(client, endpoint, json, chunked: false);

        Assert.NotEqual(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.NotEqual(RequestLimits.MalformedRequestCode, await ErrorCodeAsync(response));
    }

    /// <summary>
    /// Ruling 4: a stop <c>position</c> sent as the string <c>"1"</c> is refused; the same body with
    /// the number 1 binds and reaches the handler, which answers for the unknown service.
    /// </summary>
    [Fact]
    public async Task Post_NumberAsString_Returns400MalformedRequest()
    {
        var endpoint = Capped["CreateScheduleVersion"];
        await using var kestrel = StartKestrel();
        using var client = Client(kestrel);

        using var asString = await SendAsync(client, endpoint, ScheduleVersionBody("\"1\"", 1), chunked: false);
        using var asNumber = await SendAsync(client, endpoint, ScheduleVersionBody("1", 1), chunked: false);

        Assert.Equal(HttpStatusCode.BadRequest, asString.StatusCode);
        await AssertCodedAndOpaqueAsync(asString, RequestLimits.MalformedRequestCode);
        Assert.Equal(HttpStatusCode.UnprocessableContent, asNumber.StatusCode);
        Assert.Equal("Timetable.ScheduleServiceNotFound", await ErrorCodeAsync(asNumber));
    }

    /// <summary>
    /// Ruling 1: the 2 MiB endpoint limit still raises the 64 KiB global one — a valid-shaped body
    /// over 64 KiB reaches the handler.
    /// </summary>
    [Fact]
    public async Task Post_ScheduleVersionOver64KiB_IsAcceptedByItsOwnLimit()
    {
        var endpoint = Capped["CreateScheduleVersion"];
        await using var kestrel = StartKestrel();
        using var client = Client(kestrel);
        var json = ScheduleVersionBody("1", services: 60);
        Assert.InRange(Encoding.UTF8.GetByteCount(json), RequestLimits.GlobalMaxRequestBodyBytes + 1, endpoint.LimitBytes);

        using var response = await SendAsync(client, endpoint, json, chunked: false);

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
        Assert.Equal("Timetable.ScheduleServiceNotFound", await ErrorCodeAsync(response));
    }

    /// <summary>
    /// Ruling 3 in Development, where the framework throws its bad-request exception instead of
    /// setting the status: the exception handler gives the same coded answer, never a 500.
    /// </summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("{\"userName\": 1}")]
    public async Task Development_MalformedJson_Returns400MalformedRequest(string body)
    {
        // The anonymous login endpoint, because the test authentication handler is refused outside
        // the Testing environment (ADR-0020 item 5).
        await using var development = new YcrApiFactory(
            Database.ApplicationConnectionString, environment: "Development", mode: AuthMode.RealTokens);
        using var client = development.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("Origin", YcrApiFactory.AllowedOrigin);

        using var response = await client.PostAsync(
            "/api/v1/auth/login", new StringContent(body, Encoding.UTF8, "application/json"), CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertCodedAndOpaqueAsync(response, RequestLimits.MalformedRequestCode);
    }

    /// <summary>
    /// Ruling 3 keeps the application's own 400s as they were: a blank field is still
    /// <c>Common.ValidationFailed</c>.
    /// </summary>
    [Fact]
    public async Task Post_BlankField_KeepsCommonValidationFailed()
    {
        await using var kestrel = StartKestrel();
        using var client = Client(kestrel);

        using var response = await SendAsync(client, Capped["CreateStation"], Capped["CreateStation"].Sample, chunked: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Common.ValidationFailed", await ErrorCodeAsync(response));
    }

    private async Task AssertMalformedAsync(CappedEndpoint endpoint, string body)
    {
        await using var kestrel = StartKestrel();
        using var client = Client(kestrel);
        var auditBefore = await AuditCountAsync();

        using var response = await SendAsync(client, endpoint, body, chunked: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertCodedAndOpaqueAsync(response, RequestLimits.MalformedRequestCode);
        Assert.Equal(auditBefore, await AuditCountAsync());
    }

    /// <summary><paramref name="json"/> with an unknown <c>pad</c> property, grown to about <paramref name="bytes"/>.</summary>
    private static string Padded(string json, long bytes)
    {
        var prefix = "{\"pad\":\"";
        var rest = "\"," + json[1..];
        var padding = (int)bytes - Encoding.UTF8.GetByteCount(prefix + rest);
        return prefix + new string('A', padding) + rest;
    }

    /// <summary><paramref name="json"/> with an unknown property nesting the document <paramref name="depth"/> deep.</summary>
    private static string WithNesting(string json, int depth) =>
        "{\"nest\":" + new string('[', depth - 1) + new string(']', depth - 1) + "," + json[1..];

    /// <summary>A create body of unknown services with two stop times each; the first stop's position is <paramref name="position"/>.</summary>
    private static string ScheduleVersionBody(string position, int services)
    {
        var builder = new StringBuilder("{\"nameEn\":\"N\",\"nameMy\":\"M\",\"effectiveFrom\":\"2026-10-05\",\"services\":[");
        for (var service = 0; service < services; service++)
        {
            builder.Append(service == 0 ? string.Empty : ",")
                .Append("{\"serviceId\":\"").Append(Guid.CreateVersion7().ToString("D"))
                .Append("\",\"stopTimes\":[{\"position\":").Append(position).Append(",\"arrival\":null,\"departure\":\"06:00\"}");
            for (var stop = 2; stop <= 40; stop++)
            {
                builder.Append(",{\"position\":").Append(stop)
                    .Append(",\"arrival\":\"").Append(Clock(360 + stop * 2)).Append('"')
                    .Append(",\"departure\":").Append(stop == 40 ? "null" : $"\"{Clock(360 + stop * 2 + 1)}\"").Append('}');
            }

            builder.Append("]}");
        }

        return builder.Append("]}").ToString();
    }

    private static string Clock(int minutes) => $"{minutes / 60 % 24:00}:{minutes % 60:00}";

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, CappedEndpoint endpoint, string json, bool chunked)
    {
        using var request = new HttpRequestMessage(new HttpMethod(endpoint.Method), endpoint.Path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.TransferEncodingChunked = chunked;
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Kestrel with the F-005 spec §4 clock, so the dates in these bodies are never "in the past".</summary>
    private YcrApiFactory StartKestrel(Action<IServiceCollection>? configureServices = null)
    {
        var kestrel = new YcrApiFactory(
            Database.ApplicationConnectionString,
            clock: new TestClock(new DateTimeOffset(2026, 10, 1, 3, 0, 0, TimeSpan.Zero)),
            configureServices: configureServices);
        kestrel.UseKestrel(0);
        kestrel.StartServer();
        return kestrel;
    }

    /// <summary>A caller holding every permission a capped endpoint needs; login also gets an allowed origin.</summary>
    private static HttpClient Client(YcrApiFactory kestrel)
    {
        var address = kestrel.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.First();

        var client = new HttpClient { BaseAddress = new Uri(address) };
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.CreateVersion7().ToString());
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.PermissionsHeader,
            string.Join(
                ',',
                Permissions.StationsManage,
                Permissions.RoutesManage,
                Permissions.ServicesManage,
                Permissions.SchedulesManage,
                Permissions.UsersManage,
                Permissions.UsersRolesManage));
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "RailwayAdministrator");
        client.DefaultRequestHeaders.Add("Origin", YcrApiFactory.AllowedOrigin);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return client;
    }

    private async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(CancellationToken);
        if (string.IsNullOrEmpty(body))
        {
            return null;
        }

        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
    }

    /// <summary>
    /// REQUIRED CONTROL (docs/18, data disclosure): ProblemDetails with the ruled code and a trace id,
    /// short, and with no parser, exception or server detail.
    /// </summary>
    private async Task AssertCodedAndOpaqueAsync(HttpResponseMessage response, string errorCode)
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
        Assert.DoesNotContain("depth", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"detail\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain(":\\", body, StringComparison.Ordinal);
        Assert.DoesNotContain("/src/", body, StringComparison.Ordinal);
        Assert.DoesNotContain(Database.Name, body, StringComparison.OrdinalIgnoreCase);

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(body);
        Assert.Equal((int)response.StatusCode, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(errorCode, document.RootElement.GetProperty("errorCode").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
    }

    private async Task<int> AuditCountAsync()
    {
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(Database.MigratorConnectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = new Microsoft.Data.SqlClient.SqlCommand("SELECT COUNT(*) FROM [audit].[AuditEvents];", connection);
        return (int)(await command.ExecuteScalarAsync(CancellationToken))!;
    }

    /// <param name="Name">The endpoint name (<c>WithName</c>).</param>
    /// <param name="StringField">A property the contract binds as a string.</param>
    /// <param name="Sample">A small well-formed body of the contract's shape.</param>
    private sealed record CappedEndpoint(string Name, string Method, string Path, long LimitBytes, string StringField, string Sample);

    /// <summary>
    /// Test-only: <c>POST /__probe/body</c>, first in the pipeline, reads the whole body with no
    /// endpoint limit and answers its length, so only Kestrel's global limit applies.
    /// </summary>
    private sealed class BodyProbeStartupFilter : IStartupFilter
    {
        public const string Path = "/__probe/body";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (!context.Request.Path.Equals(Path, StringComparison.Ordinal))
                {
                    await nextMiddleware(context);
                    return;
                }

                using var buffer = new MemoryStream();
                await context.Request.Body.CopyToAsync(buffer, context.RequestAborted);
                await context.Response.WriteAsync(
                    buffer.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), context.RequestAborted);
            });
            next(app);
        };
    }
}

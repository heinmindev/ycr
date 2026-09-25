using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using YCR.Api.Contracts.Network;
using YCR.Application.Common.Authorization;

namespace YCR.Api.Tests.Network;

/// <summary>
/// F-003 S1-S3, S5-S15, S17, S22, S23, S26 and S28-S30 end to end through the real API under the
/// <c>ycr_app</c> credential, with the test authentication handler.
/// </summary>
public sealed class RouteEndpointsTests(SqlServerFixture fixture) : ApiTestBase(fixture)
{
    protected override string DatabasePrefix => "api_routes";

    [Fact]
    public async Task Post_WithValidRequest_Returns201WithLocationAndId()
    {
        using var client = AdminClient();
        var stations = await CreateStationsAsync(client, "AAA", "BBB", "CCC");

        var response = await client.PostAsJsonAsync("/api/v1/routes", Body("R1", false, stations), CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreateRouteResponse>(CancellationToken);
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal($"/api/v1/routes/{created.Id}", response.Headers.Location?.ToString());
        Assert.Equal(1, await DatabaseScalarAsync<int>("SELECT COUNT(*) FROM [audit].[AuditEvents] WHERE [Action] = N'Network.RouteCreated';"));
    }

    /// <summary>S2 / AGENTS.md rule 4: the exact wire shape, nested stations included.</summary>
    [Fact]
    public async Task Get_WithKnownId_ReturnsRouteResponseRecordNotEntity()
    {
        using var client = AdminClient();
        var stations = await CreateStationsAsync(client, "CCC", "AAA", "BBB");
        var id = await CreateRouteAsync(client, "R1", false, stations);

        var response = await client.GetAsync($"/api/v1/routes/{id}", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var route = await response.Content.ReadFromJsonAsync<RouteResponse>(CancellationToken);
        Assert.NotNull(route);
        Assert.Equal("R1", route.Code);
        Assert.False(route.IsClosed);
        Assert.True(route.IsActive);
        Assert.Null(route.DeactivatedAtUtc);
        Assert.Equal(["CCC", "AAA", "BBB"], route.Stations.Select(station => station.Code));
        Assert.Equal([1, 2, 3], route.Stations.Select(station => station.Position));
        Assert.Equal(stations, route.Stations.Select(station => station.StationId));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        Assert.Equal(
            ["id", "code", "nameEn", "nameMy", "isClosed", "isActive", "createdAtUtc", "deactivatedAtUtc", "stations"],
            document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            ["position", "stationId", "code", "nameEn", "nameMy", "isActive"],
            document.RootElement.GetProperty("stations")[0].EnumerateObject().Select(property => property.Name));
    }

    [Fact]
    public async Task List_WithThreeRoutesOneInactive_ReturnsPagedEnvelopeOrderedByCode()
    {
        using var client = AdminClient();
        var stations = await CreateStationsAsync(client, "AAA", "BBB", "CCC");
        await CreateRouteAsync(client, "ZED", false, stations[..2]);
        var inactive = await CreateRouteAsync(client, "MID", true, stations);
        await CreateRouteAsync(client, "ABC", false, stations[1..]);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/v1/routes/{inactive}/deactivate", null, CancellationToken)).StatusCode);

        var response = await client.GetAsync("/api/v1/routes?page=1&pageSize=50", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<RouteSummaryResponse>>(CancellationToken);
        Assert.NotNull(page);
        Assert.Equal(1, page.Page);
        Assert.Equal(50, page.PageSize);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(["ABC", "MID", "ZED"], page.Items.Select(route => route.Code));
        Assert.Equal([true, false, true], page.Items.Select(route => route.IsActive));
        Assert.Equal([2, 3, 2], page.Items.Select(route => route.StationCount));
        Assert.NotNull(page.Items[1].DeactivatedAtUtc);
        Assert.Null(page.Items[0].DeactivatedAtUtc);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        Assert.Equal(
            ["id", "code", "nameEn", "nameMy", "isClosed", "isActive", "stationCount", "createdAtUtc", "deactivatedAtUtc"],
            document.RootElement.GetProperty("items")[0].EnumerateObject().Select(property => property.Name));
    }

    /// <summary>
    /// S5 (with R25 and R26), and S11 as amended (Amendment 3, V7): shape failures, including a
    /// whitespace-only code or name, are <c>400 Common.ValidationFailed</c>.
    /// </summary>
    [Theory]
    [InlineData("missing stationIds")]
    [InlineData("empty stationIds")]
    [InlineData("non-GUID id")]
    [InlineData("null element")]
    [InlineData("missing code")]
    [InlineData("missing nameEn")]
    [InlineData("missing nameMy")]
    [InlineData("missing isClosed")]
    [InlineData("201 ids")]
    [InlineData("whitespace nameEn")]
    [InlineData("whitespace nameMy")]
    [InlineData("whitespace code")]
    public async Task Post_WithInvalidBody_Returns400CommonValidationFailed(string invalidity)
    {
        using var client = AdminClient();
        var body = Body("R1", false, [Guid.CreateVersion7(), Guid.CreateVersion7()]);
        switch (invalidity)
        {
            case "missing stationIds": body.Remove("stationIds"); break;
            case "empty stationIds": body["stationIds"] = Array.Empty<string>(); break;
            case "non-GUID id": body["stationIds"] = new[] { Guid.CreateVersion7().ToString(), "not-a-guid" }; break;
            case "null element": body["stationIds"] = new[] { Guid.CreateVersion7().ToString(), null }; break;
            case "missing code": body.Remove("code"); break;
            case "missing nameEn": body.Remove("nameEn"); break;
            case "missing nameMy": body.Remove("nameMy"); break;
            case "missing isClosed": body.Remove("isClosed"); break;
            case "whitespace nameEn": body["nameEn"] = "   "; break;
            case "whitespace nameMy": body["nameMy"] = " \t "; break;
            case "whitespace code": body["code"] = "   "; break;
            case "201 ids": body["stationIds"] = Enumerable.Range(0, 201).Select(_ => Guid.CreateVersion7().ToString()).ToArray(); break;
            default: throw new ArgumentOutOfRangeException(nameof(invalidity));
        }

        var response = await client.PostAsJsonAsync("/api/v1/routes", body, CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Common.ValidationFailed", await ErrorCodeOf(response));
        await AssertNoRouteWrittenAsync();
    }

    /// <summary>
    /// S5 / R26 boundary: exactly 200 ids pass validation and reach the handler, which answers for
    /// the first unknown station rather than the validator refusing the list.
    /// </summary>
    [Fact]
    public async Task Post_WithExactly200StationIds_PassesValidationAndReachesTheHandler()
    {
        using var client = AdminClient();
        var ids = Enumerable.Range(0, 200).Select(_ => Guid.CreateVersion7()).ToArray();

        var response = await client.PostAsJsonAsync("/api/v1/routes", Body("R200", false, ids), CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
        Assert.Equal("Network.RouteStationNotFound", await ErrorCodeOf(response));
        await AssertNoRouteWrittenAsync();
    }

    [Fact]
    public async Task Post_WithUnknownStation_Returns422RouteStationNotFound()
    {
        using var client = AdminClient();
        var stations = await CreateStationsAsync(client, "AAA", "BBB");

        var response = await client.PostAsJsonAsync("/api/v1/routes", Body("R1", false, [stations[0], Guid.CreateVersion7(), stations[1]]), CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
        Assert.Equal("Network.RouteStationNotFound", await ErrorCodeOf(response));
        await AssertNoRouteWrittenAsync();
    }

    [Fact]
    public async Task Post_WithInactiveStation_Returns422RouteStationInactive()
    {
        using var client = AdminClient();
        var stations = await CreateStationsAsync(client, "AAA", "BBB", "CCC");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/v1/stations/{stations[1]}/deactivate", null, CancellationToken)).StatusCode);

        var response = await client.PostAsJsonAsync("/api/v1/routes", Body("R1", false, stations), CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
        Assert.Equal("Network.RouteStationInactive", await ErrorCodeOf(response));
        await AssertNoRouteWrittenAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Post_WithRepeatedStation_Returns422RouteStationRepeated(bool isClosed)
    {
        using var client = AdminClient();
        var stations = await CreateStationsAsync(client, "AAA", "BBB", "CCC");

        // [A, B, C, A]: on a closed route too, the first station is never repeated (R6).
        var response = await client.PostAsJsonAsync("/api/v1/routes", Body("R1", isClosed, [.. stations, stations[0]]), CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
        Assert.Equal("Network.RouteStationRepeated", await ErrorCodeOf(response));
        await AssertNoRouteWrittenAsync();
    }

    [Theory]
    [InlineData(false, 1, HttpStatusCode.UnprocessableContent)]
    [InlineData(false, 2, HttpStatusCode.Created)]
    [InlineData(true, 2, HttpStatusCode.UnprocessableContent)]
    [InlineData(true, 3, HttpStatusCode.Created)]
    public async Task Post_AtAndBelowMinimumLength_Returns201Or422(bool isClosed, int count, HttpStatusCode expected)
    {
        using var client = AdminClient();
        var stations = await CreateStationsAsync(client, "AAA", "BBB", "CCC");

        var response = await client.PostAsJsonAsync("/api/v1/routes", Body("R1", isClosed, stations[..count]), CancellationToken);

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.UnprocessableContent)
        {
            Assert.Equal("Network.RouteTooFewStations", await ErrorCodeOf(response));
            await AssertNoRouteWrittenAsync();
        }
    }

    [Fact]
    public async Task Post_WithDuplicateCode_Returns409WithErrorCode()
    {
        using var client = AdminClient();
        var stations = await CreateStationsAsync(client, "AAA", "BBB");
        await CreateRouteAsync(client, "R1", false, stations);

        var response = await client.PostAsJsonAsync("/api/v1/routes", Body("R1", false, stations), CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Network.RouteCodeAlreadyExists", await ErrorCodeOf(response));
    }

    [Theory]
    [InlineData("R")]
    [InlineData("ABCDEFGHIJK")]
    [InlineData("loop1")]
    [InlineData("R-1")]
    [InlineData("R 1")]
    public async Task Post_WithInvalidCode_Returns400InvalidRouteCode(string code)
    {
        using var client = AdminClient();
        var stations = await CreateStationsAsync(client, "AAA", "BBB");

        var response = await client.PostAsJsonAsync("/api/v1/routes", Body(code, false, stations), CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Network.InvalidRouteCode", await ErrorCodeOf(response));
        await AssertNoRouteWrittenAsync();
    }

    /// <summary>S11's name cases that reach the domain rule.</summary>
    /// <remarks>
    /// Overlong rather than whitespace, as in F-001's station endpoint tests: the validator's
    /// <c>NotEmpty()</c> (plan P6, "as in <c>CreateStationRequestValidator</c>") treats a
    /// whitespace-only string as empty, so <c>"   "</c> is <c>400 Common.ValidationFailed</c> at the
    /// endpoint. The domain's own whitespace rule is proven below the filter by
    /// <c>CreateRouteHandlerTests.CreateRoute_WithInvalidCodeOrName_ReturnsValidationErrorAndWritesNothing</c>.
    /// </remarks>
    [Theory]
    [InlineData("101", "လမ်းကြောင်း")]
    [InlineData("Route", "101")]
    public async Task Post_WithInvalidName_Returns400InvalidRouteName(string nameEn, string nameMy)
    {
        using var client = AdminClient();
        var stations = await CreateStationsAsync(client, "AAA", "BBB");
        var overlong = new string('A', 101);

        var response = await client.PostAsJsonAsync(
            "/api/v1/routes",
            Body("R1", false, stations, nameEn == "101" ? overlong : nameEn, nameMy == "101" ? overlong : nameMy),
            CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Network.InvalidRouteName", await ErrorCodeOf(response));
        await AssertNoRouteWrittenAsync();
    }

    [Fact]
    public async Task GetAndDeactivate_WithUnknownId_Return404RouteNotFound()
    {
        using var client = AdminClient();
        var unknown = Guid.CreateVersion7();

        var get = await client.GetAsync($"/api/v1/routes/{unknown}", CancellationToken);
        var deactivate = await client.PostAsync($"/api/v1/routes/{unknown}/deactivate", null, CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal("Network.RouteNotFound", await ErrorCodeOf(get));
        Assert.Equal(HttpStatusCode.NotFound, deactivate.StatusCode);
        Assert.Equal("Network.RouteNotFound", await ErrorCodeOf(deactivate));
    }

    [Fact]
    public async Task List_WithPageSizeAbove200_Returns400InvalidPageRequest()
    {
        using var client = AdminClient();

        var response = await client.GetAsync("/api/v1/routes?page=1&pageSize=201", CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Network.InvalidPageRequest", await ErrorCodeOf(response));
    }

    [Theory]
    [InlineData("GET", "/api/v1/routes")]
    [InlineData("GET", "/api/v1/routes/11111111-1111-1111-1111-111111111111")]
    [InlineData("POST", "/api/v1/routes")]
    [InlineData("POST", "/api/v1/routes/11111111-1111-1111-1111-111111111111/deactivate")]
    public async Task AnyRouteEndpoint_Anonymous_Returns401(string method, string path)
    {
        using var client = Api.CreateAnonymousClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
        {
            request.Content = JsonContent.Create(Body("R1", false, [Guid.CreateVersion7(), Guid.CreateVersion7()]));
        }

        var response = await client.SendAsync(request, CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNoRouteWrittenAsync();
    }

    [Fact]
    public async Task Post_WithOnlyRoutesRead_Returns403()
    {
        using var admin = AdminClient();
        var stations = await CreateStationsAsync(admin, "AAA", "BBB");
        using var reader = Api.CreateClientWith(Permissions.RoutesRead);

        var response = await reader.PostAsJsonAsync("/api/v1/routes", Body("R1", false, stations), CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/api/v1/routes", CancellationToken)).StatusCode);
        await AssertNoRouteWrittenAsync();
    }

    [Fact]
    public async Task Deactivate_WithOnlyRoutesRead_Returns403()
    {
        using var admin = AdminClient();
        var id = await CreateRouteAsync(admin, "R1", false, await CreateStationsAsync(admin, "AAA", "BBB"));
        using var reader = Api.CreateClientWith(Permissions.RoutesRead);

        var response = await reader.PostAsync($"/api/v1/routes/{id}/deactivate", null, CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, await DatabaseScalarAsync<int>("SELECT CAST([IsActive] AS int) FROM [network].[Routes];"));
    }

    /// <summary>S15 / R3: holding a station permission gives no route right.</summary>
    [Fact]
    public async Task Post_WithOnlyStationsManage_Returns403()
    {
        using var stationManager = Api.CreateClientWith(Permissions.StationsManage, Permissions.StationsRead);
        var stations = await CreateStationsAsync(stationManager, "AAA", "BBB");

        var response = await stationManager.PostAsJsonAsync("/api/v1/routes", Body("R1", false, stations), CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertNoRouteWrittenAsync();
    }

    [Fact]
    public async Task List_WithOnlyStationsRead_Returns403()
    {
        using var stationReader = Api.CreateClientWith(Permissions.StationsRead);

        var list = await stationReader.GetAsync("/api/v1/routes", CancellationToken);
        var get = await stationReader.GetAsync($"/api/v1/routes/{Guid.CreateVersion7()}", CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
    }

    /// <summary>S17 / R12: the station endpoint is unchanged, and the route shows the station inactive.</summary>
    [Fact]
    public async Task DeactivateStation_InActiveRoute_Returns204AndRouteShowsStationInactive()
    {
        using var client = AdminClient();
        var stations = await CreateStationsAsync(client, "AAA", "BBB", "CCC");
        var id = await CreateRouteAsync(client, "R1", false, stations);

        var response = await client.PostAsync($"/api/v1/stations/{stations[1]}/deactivate", null, CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var route = await client.GetFromJsonAsync<RouteResponse>($"/api/v1/routes/{id}", CancellationToken);
        Assert.NotNull(route);
        Assert.True(route.IsActive);
        Assert.Equal(stations, route.Stations.Select(station => station.StationId));
        Assert.Equal([true, false, true], route.Stations.Select(station => station.IsActive));
    }

    /// <summary>S22: the recorded actor is the authenticated caller, whatever the body says.</summary>
    [Fact]
    public async Task Post_WhenRequestTriesToSupplyActorFields_RecordsTheAuthenticatedActor()
    {
        var realActor = Guid.CreateVersion7();
        using var admin = AdminClient();
        var stations = await CreateStationsAsync(admin, "AAA", "BBB");
        using var client = Api.CreateClientAs(realActor, Permissions.RoutesManage);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.1");
        var body = Body("R1", false, stations);
        body["actorUserId"] = Guid.Empty.ToString();
        body["actorRole"] = "SystemAdministrator";
        body["authorizedByPermission"] = "users.manage";
        body["clientIp"] = "10.0.0.1";

        var response = await client.PostAsJsonAsync("/api/v1/routes", body, CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        const string Where = " FROM [audit].[AuditEvents] WHERE [Action] = N'Network.RouteCreated';";
        Assert.Equal(realActor, await DatabaseScalarAsync<Guid>("SELECT [ActorUserId]" + Where));
        Assert.Equal(Permissions.RoutesManage, await DatabaseScalarAsync<string>("SELECT [AuthorizedByPermission]" + Where));
        Assert.NotEqual("10.0.0.1", await DatabaseScalarAsync<string>("SELECT [ClientIp]" + Where));
        Assert.DoesNotContain("SystemAdministrator", await DatabaseScalarAsync<string>("SELECT [ActorRole]" + Where) ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deactivate_WhenActiveThenRepeated_Returns204Then422AndReadsBackInactive()
    {
        using var client = AdminClient();
        var stations = await CreateStationsAsync(client, "AAA", "BBB");
        var id = await CreateRouteAsync(client, "R1", false, stations);

        var first = await client.PostAsync($"/api/v1/routes/{id}/deactivate", null, CancellationToken);
        var second = await client.PostAsync($"/api/v1/routes/{id}/deactivate", null, CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableContent, second.StatusCode);
        Assert.Equal("Network.RouteAlreadyInactive", await ErrorCodeOf(second));
        Assert.Equal(1, await DatabaseScalarAsync<int>("SELECT COUNT(*) FROM [audit].[AuditEvents] WHERE [Action] = N'Network.RouteDeactivated';"));

        var route = await client.GetFromJsonAsync<RouteResponse>($"/api/v1/routes/{id}", CancellationToken);
        Assert.NotNull(route);
        Assert.False(route.IsActive);
        Assert.NotNull(route.DeactivatedAtUtc);
        Assert.Equal(TimeSpan.Zero, route.DeactivatedAtUtc.Value.Offset);
        Assert.Equal(stations, route.Stations.Select(station => station.StationId));
    }

    /// <summary>S26: real Myanmar Unicode through the whole API, as <c>\u</c> escapes (F-001 R-10).</summary>
    [Fact]
    public async Task Post_WithMyanmarName_RoundTripsThroughGet()
    {
        const string MyanmarName = "မြို့ပတ်ရထား";
        using var client = AdminClient();
        var stations = await CreateStationsAsync(client, "AAA", "BBB");

        var response = await client.PostAsJsonAsync("/api/v1/routes", Body("MYA1", false, stations, "Circular", MyanmarName), CancellationToken);
        var created = await response.Content.ReadFromJsonAsync<CreateRouteResponse>(CancellationToken);

        var route = await client.GetFromJsonAsync<RouteResponse>($"/api/v1/routes/{created!.Id}", CancellationToken);
        Assert.Equal(MyanmarName, route!.NameMy);
    }

    [Fact]
    public async Task Get_ClosedRoute_ReturnsIsClosedAndThreeStations()
    {
        using var client = AdminClient();
        var stations = await CreateStationsAsync(client, "AAA", "BBB", "CCC");
        var id = await CreateRouteAsync(client, "LOOP", true, stations);

        var route = await client.GetFromJsonAsync<RouteResponse>($"/api/v1/routes/{id}", CancellationToken);

        Assert.NotNull(route);
        Assert.True(route.IsClosed);
        Assert.Equal(3, route.Stations.Count);
        Assert.Equal(1, Assert.Single(route.Stations, station => station.StationId == stations[0]).Position);
    }

    [Fact]
    public async Task Post_SecondRouteSharingStations_Returns201()
    {
        using var client = AdminClient();
        var stations = await CreateStationsAsync(client, "AAA", "BBB", "CCC", "DDD");
        var first = await CreateRouteAsync(client, "R1", false, stations[..3]);

        var response = await client.PostAsJsonAsync("/api/v1/routes", Body("R2", false, stations[1..]), CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var second = (await response.Content.ReadFromJsonAsync<CreateRouteResponse>(CancellationToken))!.Id;
        var one = await client.GetFromJsonAsync<RouteResponse>($"/api/v1/routes/{first}", CancellationToken);
        var two = await client.GetFromJsonAsync<RouteResponse>($"/api/v1/routes/{second}", CancellationToken);
        Assert.Equal(2, one!.Stations.Single(station => station.StationId == stations[1]).Position);
        Assert.Equal(1, two!.Stations.Single(station => station.StationId == stations[1]).Position);
    }

    [Fact]
    public async Task Post_WithCodeOfDeactivatedRoute_Returns409()
    {
        using var client = AdminClient();
        var stations = await CreateStationsAsync(client, "AAA", "BBB");
        var id = await CreateRouteAsync(client, "R1", false, stations);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/v1/routes/{id}/deactivate", null, CancellationToken)).StatusCode);

        var response = await client.PostAsJsonAsync("/api/v1/routes", Body("R1", false, stations), CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Network.RouteCodeAlreadyExists", await ErrorCodeOf(response));
        Assert.Equal(1, await DatabaseScalarAsync<int>("SELECT COUNT(*) FROM [network].[Routes];"));
    }

    private HttpClient AdminClient() => Api.CreateClientWith(
        Permissions.RoutesManage,
        Permissions.RoutesRead,
        Permissions.StationsManage,
        Permissions.StationsRead);

    private static Dictionary<string, object?> Body(
        string code,
        bool isClosed,
        IEnumerable<Guid> stationIds,
        string nameEn = "Circular Route",
        string nameMy = "မြို့ပတ်ရထားလမ်း") => new()
        {
            ["code"] = code,
            ["nameEn"] = nameEn,
            ["nameMy"] = nameMy,
            ["isClosed"] = isClosed,
            ["stationIds"] = stationIds.Select(id => id.ToString()).ToArray(),
        };

    private async Task<Guid[]> CreateStationsAsync(HttpClient client, params string[] codes)
    {
        var ids = new List<Guid>();
        foreach (var code in codes)
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/stations",
                new CreateStationRequest(code, $"Station {code}", "ဘူတာ"),
                CancellationToken);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            ids.Add((await response.Content.ReadFromJsonAsync<CreateStationResponse>(CancellationToken))!.Id);
        }

        return [.. ids];
    }

    private async Task<Guid> CreateRouteAsync(HttpClient client, string code, bool isClosed, IEnumerable<Guid> stationIds)
    {
        var response = await client.PostAsJsonAsync("/api/v1/routes", Body(code, isClosed, stationIds), CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateRouteResponse>(CancellationToken))!.Id;
    }

    private async Task AssertNoRouteWrittenAsync()
    {
        Assert.Equal(0, await DatabaseScalarAsync<int>("SELECT COUNT(*) FROM [network].[Routes];"));
        Assert.Equal(0, await DatabaseScalarAsync<int>("SELECT COUNT(*) FROM [network].[RouteStations];"));
        Assert.Equal(0, await DatabaseScalarAsync<int>("SELECT COUNT(*) FROM [audit].[AuditEvents] WHERE [Action] LIKE N'Network.Route%';"));
    }

    private async Task<string?> ErrorCodeOf(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync(CancellationToken);
        using var document = JsonDocument.Parse(json);

        // docs/20 §4: every error carries errorCode and traceId.
        Assert.True(document.RootElement.TryGetProperty("traceId", out _), json);

        return document.RootElement.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
    }

    private async Task<T?> DatabaseScalarAsync<T>(string sql)
    {
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(Database.MigratorConnectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = new Microsoft.Data.SqlClient.SqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync(CancellationToken);

        return value is null or DBNull ? default : (T)value;
    }
}

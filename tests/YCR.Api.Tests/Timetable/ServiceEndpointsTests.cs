using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using YCR.Api.Contracts.Network;
using YCR.Api.Contracts.Timetable;
using YCR.Api.Tests.Authentication;
using YCR.Application.Common.Authorization;
using YCR.TestSupport;

namespace YCR.Api.Tests.Timetable;

/// <summary>
/// F-004 S1-S26, S30-S36, S38-S42, S44, S45, S49, S52 end to end through the real API under the
/// <c>ycr_app</c> credential, with the test authentication handler. The API's clock reads
/// 2026-10-01 03:00 UTC, so today in Asia/Yangon is 2026-10-01 (spec §4).
/// </summary>
public sealed class ServiceEndpointsTests(SqlServerFixture fixture) : ApiTestBase(fixture), IAsyncDisposable
{
    private static readonly DateTimeOffset NowUtc = new(2026, 10, 1, 3, 0, 0, TimeSpan.Zero);

    private YcrApiFactory? services;

    protected override string DatabasePrefix => "api_services";

    /// <summary>The API with the spec §4 clock.</summary>
    private YcrApiFactory Services => services ??= new YcrApiFactory(Database.ApplicationConnectionString, clock: new TestClock(NowUtc));

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (services is not null)
        {
            await services.DisposeAsync();
        }

        await DisposeAsync();
    }

    [Fact]
    public async Task Post_WithValidRequest_Returns201WithLocationAndId()
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/services", Body(network), CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreateServiceResponse>(CancellationToken);
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal($"/api/v1/services/{created.Id}", response.Headers.Location?.ToString());
        Assert.Equal(1, await DatabaseScalarAsync<int>("SELECT COUNT(*) FROM [timetable].[Services];"));
        Assert.Equal(3, await DatabaseScalarAsync<int>("SELECT COUNT(*) FROM [timetable].[ServiceStops];"));
        Assert.Equal(1, await DatabaseScalarAsync<int>("SELECT COUNT(*) FROM [audit].[AuditEvents] WHERE [Action] = N'Timetable.ServiceCreated';"));
    }

    /// <summary>S2, R22, AGENTS.md rule 4: the exact wire shape, nested route and stops, no time field.</summary>
    [Fact]
    public async Task Get_WithKnownId_ReturnsServiceResponseRecordNotEntity()
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);
        var id = await CreateAsync(client, Body(network, "DEAB"));

        var response = await client.GetAsync($"/api/v1/services/{id}", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var service = await response.Content.ReadFromJsonAsync<ServiceResponse>(CancellationToken);
        Assert.NotNull(service);
        Assert.Equal("S101", service.Code);
        Assert.Equal("Forward", service.Direction);
        Assert.Equal("RC", service.Route.Code);
        Assert.True(service.Route.IsClosed);
        Assert.Equal(["SD", "SE", "SA", "SB"], service.Stops.Select(stop => stop.Code));
        Assert.Equal([1, 2, 3, 4], service.Stops.Select(stop => stop.Position));
        Assert.Equal(["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"], service.OperatingDays);
        Assert.Equal(new DateOnly(2026, 10, 5), service.EffectiveFrom);
        Assert.Null(service.EffectiveTo);
        Assert.False(service.NeverRuns);
        Assert.Null(service.WithdrawnAtUtc);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        var root = document.RootElement;
        Assert.Equal(
            ["id", "code", "nameEn", "nameMy", "direction", "route", "stops", "operatingDays", "effectiveFrom", "effectiveTo", "neverRuns", "createdAtUtc", "withdrawnAtUtc"],
            root.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            ["id", "code", "nameEn", "nameMy", "isClosed", "isActive"],
            root.GetProperty("route").EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            ["position", "stationId", "code", "nameEn", "nameMy", "isActive"],
            root.GetProperty("stops")[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal("2026-10-05", root.GetProperty("effectiveFrom").GetString());
    }

    /// <summary>S3: the envelope, ordered by code then effective-from, withdrawn services included.</summary>
    [Fact]
    public async Task List_ReturnsPagedEnvelopeOrderedByCodeThenEffectiveFrom()
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);
        var old = await CreateAsync(client, Body(network, code: "S101"));
        Assert.Equal(HttpStatusCode.NoContent, (await WithdrawAsync(client, old, "2027-01-01")).StatusCode);
        var replacement = await CreateAsync(client, Body(network, code: "S101", effectiveFrom: "2027-01-01"));
        var first = await CreateAsync(client, Body(network, "QR", code: "A10", routeId: network.Ro));

        var response = await client.GetAsync("/api/v1/services?page=1&pageSize=50", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ServiceSummaryResponse>>(CancellationToken);
        Assert.NotNull(page);
        Assert.Equal(1, page.Page);
        Assert.Equal(50, page.PageSize);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal([first, old, replacement], page.Items.Select(item => item.Id));
        Assert.Equal(["RO", "RC", "RC"], page.Items.Select(item => item.RouteCode));
        Assert.Equal([2, 3, 3], page.Items.Select(item => item.StopCount));
        Assert.Equal(new DateOnly(2026, 12, 31), page.Items[1].EffectiveTo);
        Assert.NotNull(page.Items[1].WithdrawnAtUtc);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        Assert.Equal(
            ["id", "code", "nameEn", "nameMy", "routeId", "routeCode", "direction", "stopCount", "operatingDays", "effectiveFrom", "effectiveTo", "neverRuns", "createdAtUtc", "withdrawnAtUtc"],
            document.RootElement.GetProperty("items")[0].EnumerateObject().Select(property => property.Name));
    }

    [Fact]
    public async Task List_FilteredByRouteId_ReturnsOnlyThatRoute()
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);
        var onRo = await CreateAsync(client, Body(network, "QR", code: "S202", routeId: network.Ro));
        await CreateAsync(client, Body(network, code: "S101"));

        var page = await client.GetFromJsonAsync<PagedResponse<ServiceSummaryResponse>>(
            $"/api/v1/services?routeId={network.Ro}", CancellationToken);
        var none = await client.GetFromJsonAsync<PagedResponse<ServiceSummaryResponse>>(
            $"/api/v1/services?routeId={Guid.CreateVersion7()}", CancellationToken);

        Assert.Equal([onRo], page!.Items.Select(item => item.Id));
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(0, none!.TotalCount);
        Assert.Empty(none.Items);
    }

    [Theory]
    [InlineData("RC", "Forward", "DEAB")] // S4
    [InlineData("RC", "Reverse", "BAED")] // S5
    [InlineData("RC", "Forward", "DAC")] // S6
    [InlineData("RC", "Forward", "CB")] // S6
    [InlineData("RC", "Forward", "CDEABC")] // S7
    [InlineData("RC", "Forward", "CEBC")] // S7
    [InlineData("RC", "Reverse", "CADC")] // S7
    [InlineData("RO", "Forward", "QR")] // S10
    [InlineData("RO", "Reverse", "SRP")] // S10
    public async Task Post_WithValidPatterns_Returns201(string route, string direction, string stops)
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/services",
            Body(network, stops, routeId: route == "RO" ? network.Ro : network.Rc, direction: direction),
            CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<CreateServiceResponse>(CancellationToken))!.Id;
        var service = await client.GetFromJsonAsync<ServiceResponse>($"/api/v1/services/{id}", CancellationToken);
        Assert.Equal(stops.Select(letter => $"S{letter}"), service!.Stops.Select(stop => stop.Code));
    }

    [Theory]
    [InlineData("RC", "Forward", "CEC", "Timetable.ServiceTooFewStops")] // S8
    [InlineData("RC", "Forward", "CDEABCD", "Timetable.ServiceStopRepeated")] // S9
    [InlineData("RC", "Forward", "DACE", "Timetable.ServiceStopsOutOfOrder")] // S9
    [InlineData("RO", "Forward", "RSP", "Timetable.ServiceStopsOutOfOrder")] // S11
    [InlineData("RO", "Reverse", "QPS", "Timetable.ServiceStopsOutOfOrder")] // S11
    [InlineData("RO", "Forward", "PQRP", "Timetable.ServiceStopRepeated")] // S12
    [InlineData("RC", "Forward", "ABBC", "Timetable.ServiceStopRepeated")] // S13
    [InlineData("RC", "Forward", "ABAC", "Timetable.ServiceStopRepeated")] // S13
    [InlineData("RC", "Forward", "ACB", "Timetable.ServiceStopsOutOfOrder")] // S14
    [InlineData("RO", "Forward", "PRQ", "Timetable.ServiceStopsOutOfOrder")] // S14
    [InlineData("RC", "Forward", "A", "Timetable.ServiceTooFewStops")] // S15
    [InlineData("RC", "Forward", "AXC", "Timetable.ServiceStopNotOnRoute")] // S16: a station on no route
    [InlineData("RC", "Forward", "AZC", "Timetable.ServiceStopNotOnRoute")] // S16: an id that names no station
    public async Task Post_WithInvalidPatterns_Returns422WithErrorCode(string route, string direction, string stops, string errorCode)
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/services",
            Body(network, stops, routeId: route == "RO" ? network.Ro : network.Rc, direction: direction),
            CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
        Assert.Equal(errorCode, await ErrorCodeOf(response));
        await AssertNoServiceWrittenAsync();
    }

    [Fact]
    public async Task Post_OnInactiveRoute_Returns422RouteInactive()
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/v1/routes/{network.Rc}/deactivate", null, CancellationToken)).StatusCode);

        var response = await client.PostAsJsonAsync("/api/v1/services", Body(network), CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
        Assert.Equal("Timetable.ServiceRouteInactive", await ErrorCodeOf(response));
        await AssertNoServiceWrittenAsync();
    }

    /// <summary>S18: a stop at an inactive station is refused; passing it without stopping is not.</summary>
    [Fact]
    public async Task Post_WithInactiveStopStation_Returns422()
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/v1/stations/{network['C']}/deactivate", null, CancellationToken)).StatusCode);

        var refused = await client.PostAsJsonAsync("/api/v1/services", Body(network, "ACE"), CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableContent, refused.StatusCode);
        Assert.Equal("Timetable.ServiceStopStationInactive", await ErrorCodeOf(refused));
        await AssertNoServiceWrittenAsync();

        var passing = await client.PostAsJsonAsync("/api/v1/services", Body(network, "BD"), CancellationToken);
        Assert.Equal(HttpStatusCode.Created, passing.StatusCode);
    }

    [Fact]
    public async Task Post_WithUnknownRoute_Returns422RouteNotFound()
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/services", Body(network, routeId: Guid.CreateVersion7()), CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
        Assert.Equal("Timetable.ServiceRouteNotFound", await ErrorCodeOf(response));
        await AssertNoServiceWrittenAsync();
    }

    /// <summary>S20: deactivation answers exactly as before, changes no service row, and reads show it.</summary>
    [Theory]
    [InlineData("route")]
    [InlineData("station")]
    public async Task DeactivateRouteOrStation_UsedByAService_Returns204AndServiceShowsInactive(string deactivated)
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);
        var id = await CreateAsync(client, Body(network, "ACE"));
        var path = deactivated == "route" ? $"/api/v1/routes/{network.Rc}/deactivate" : $"/api/v1/stations/{network['C']}/deactivate";

        var response = await client.PostAsync(path, null, CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var service = await client.GetFromJsonAsync<ServiceResponse>($"/api/v1/services/{id}", CancellationToken);
        Assert.Equal(deactivated != "route", service!.Route.IsActive);
        Assert.Equal([true, deactivated != "station", true], service.Stops.Select(stop => stop.IsActive));
        Assert.Equal(3, await DatabaseScalarAsync<int>("SELECT COUNT(*) FROM [timetable].[ServiceStops];"));
        Assert.Equal(1, await DatabaseScalarAsync<int>("SELECT COUNT(*) FROM [timetable].[Services] WHERE [EffectiveTo] IS NULL AND [WithdrawnAtUtc] IS NULL;"));
    }

    /// <summary>S21, R31: every shape failure is <c>400 Common.ValidationFailed</c>, and nothing is read or written.</summary>
    [Theory]
    [InlineData("missing code")]
    [InlineData("blank code")]
    [InlineData("missing nameEn")]
    [InlineData("blank nameMy")]
    [InlineData("missing routeId")]
    [InlineData("non-GUID routeId")]
    [InlineData("missing direction")]
    [InlineData("direction Sideways")]
    [InlineData("direction forward")]
    [InlineData("missing stopStationIds")]
    [InlineData("empty stopStationIds")]
    [InlineData("null stop id")]
    [InlineData("non-GUID stop id")]
    [InlineData("201 stop ids")]
    [InlineData("missing operatingDays")]
    [InlineData("empty operatingDays")]
    [InlineData("repeated day")]
    [InlineData("day monday")]
    [InlineData("day 1")]
    [InlineData("day Mon")]
    [InlineData("missing effectiveFrom")]
    [InlineData("effectiveFrom 2026-13-01")]
    [InlineData("effectiveFrom 05/10/2026")]
    [InlineData("effectiveTo 2026-10-5")]
    public async Task Post_WithInvalidBody_Returns400CommonValidationFailed(string invalidity)
    {
        using var client = AdminClient();
        var body = Body(TimetableApiNetwork.Unconnected);
        switch (invalidity)
        {
            case "missing code": body.Remove("code"); break;
            case "blank code": body["code"] = "   "; break;
            case "missing nameEn": body.Remove("nameEn"); break;
            case "blank nameMy": body["nameMy"] = " \t "; break;
            case "missing routeId": body.Remove("routeId"); break;
            case "non-GUID routeId": body["routeId"] = "route-1"; break;
            case "missing direction": body.Remove("direction"); break;
            case "direction Sideways": body["direction"] = "Sideways"; break;
            case "direction forward": body["direction"] = "forward"; break;
            case "missing stopStationIds": body.Remove("stopStationIds"); break;
            case "empty stopStationIds": body["stopStationIds"] = Array.Empty<string>(); break;
            case "null stop id": body["stopStationIds"] = new[] { Guid.CreateVersion7().ToString(), null }; break;
            case "non-GUID stop id": body["stopStationIds"] = new[] { Guid.CreateVersion7().ToString(), "not-a-guid" }; break;
            case "201 stop ids": body["stopStationIds"] = Enumerable.Range(0, 201).Select(_ => Guid.CreateVersion7().ToString()).ToArray(); break;
            case "missing operatingDays": body.Remove("operatingDays"); break;
            case "empty operatingDays": body["operatingDays"] = Array.Empty<string>(); break;
            case "repeated day": body["operatingDays"] = new[] { "Monday", "Monday" }; break;
            case "day monday": body["operatingDays"] = new[] { "monday" }; break;
            case "day 1": body["operatingDays"] = new[] { "1" }; break;
            case "day Mon": body["operatingDays"] = new[] { "Mon" }; break;
            case "missing effectiveFrom": body.Remove("effectiveFrom"); break;
            case "effectiveFrom 2026-13-01": body["effectiveFrom"] = "2026-13-01"; break;
            case "effectiveFrom 05/10/2026": body["effectiveFrom"] = "05/10/2026"; break;
            case "effectiveTo 2026-10-5": body["effectiveTo"] = "2026-10-5"; break;
            default: throw new ArgumentOutOfRangeException(nameof(invalidity));
        }

        var response = await client.PostAsJsonAsync("/api/v1/services", body, CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Common.ValidationFailed", await ErrorCodeOf(response));
        await AssertNoServiceWrittenAsync();
    }

    /// <summary>S21 / R31 boundary: exactly 200 stop ids pass validation and reach the handler.</summary>
    [Fact]
    public async Task Post_WithExactly200StopIds_PassesValidationAndReachesTheHandler()
    {
        using var client = AdminClient();
        var body = Body(TimetableApiNetwork.Unconnected);
        body["stopStationIds"] = Enumerable.Range(0, 200).Select(_ => Guid.CreateVersion7().ToString()).ToArray();

        var response = await client.PostAsJsonAsync("/api/v1/services", body, CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
        Assert.Equal("Timetable.ServiceRouteNotFound", await ErrorCodeOf(response));
    }

    [Theory]
    [InlineData("s1")]
    [InlineData("A")]
    [InlineData("ABCDEFGHIJK")]
    [InlineData("S-1")]
    public async Task Post_WithInvalidCode_Returns400InvalidServiceCode(string code)
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/services", Body(network, code: code), CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Timetable.InvalidServiceCode", await ErrorCodeOf(response));
        await AssertNoServiceWrittenAsync();
    }

    [Fact]
    public async Task Post_WithInvalidName_Returns400InvalidServiceName()
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/services", Body(network, nameEn: new string('N', 101)), CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Timetable.InvalidServiceName", await ErrorCodeOf(response));
        await AssertNoServiceWrittenAsync();
    }

    [Fact]
    public async Task Post_WithEffectiveToBeforeFrom_Returns400()
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/services", Body(network, effectiveTo: "2026-10-04"), CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Timetable.InvalidEffectivePeriod", await ErrorCodeOf(response));
        await AssertNoServiceWrittenAsync();
    }

    [Fact]
    public async Task Post_WithOneDayPeriod_Returns201()
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/services", Body(network, effectiveTo: "2026-10-05"), CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>S24, S25, S40: an overlapping period for the same code, whatever the route, direction or days.</summary>
    [Theory]
    [InlineData(null, "RC", "2027-01-01", null)] // S24
    [InlineData("2026-12-31", "RC", "2026-12-31", null)] // S25 edge
    [InlineData("2026-12-31", "RO", "2026-11-01", null)] // S25: RO, Reverse, weekend
    [InlineData(null, "RC", "2026-09-01", null)] // S40
    public async Task Post_WithOverlappingPeriod_Returns409(string? existingTo, string route, string from, string? to)
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);
        await CreateAsync(client, Body(network, effectiveTo: existingTo));

        var body = route == "RO"
            ? Body(network, "SRP", routeId: network.Ro, direction: "Reverse", effectiveFrom: from, effectiveTo: to, days: ["Saturday", "Sunday"])
            : Body(network, effectiveFrom: from, effectiveTo: to);
        var response = await client.PostAsJsonAsync("/api/v1/services", body, CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Timetable.ServiceCodePeriodOverlap", await ErrorCodeOf(response));
        Assert.Equal(1, await DatabaseScalarAsync<int>("SELECT COUNT(*) FROM [timetable].[Services];"));
    }

    [Fact]
    public async Task Post_WithAdjacentPeriod_Returns201()
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);
        await CreateAsync(client, Body(network, effectiveTo: "2026-12-31"));

        var response = await client.PostAsJsonAsync("/api/v1/services", Body(network, effectiveFrom: "2027-01-01"), CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>S26: withdraw from D, create the same code from D, and both are listed in effective-from order.</summary>
    [Fact]
    public async Task TimetableChange_WithdrawThenCreateSameCode_Returns204Then201AndListsBoth()
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);
        var old = await CreateAsync(client, Body(network));

        var withdrawn = await WithdrawAsync(client, old, "2027-01-01");
        var created = await client.PostAsJsonAsync(
            "/api/v1/services", Body(network, "QR", routeId: network.Ro, effectiveFrom: "2027-01-01"), CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, withdrawn.StatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var replacement = (await created.Content.ReadFromJsonAsync<CreateServiceResponse>(CancellationToken))!.Id;
        var page = await client.GetFromJsonAsync<PagedResponse<ServiceSummaryResponse>>("/api/v1/services", CancellationToken);
        Assert.Equal([old, replacement], page!.Items.Select(item => item.Id));
        Assert.Equal([new DateOnly(2026, 12, 31), (DateOnly?)null], page.Items.Select(item => item.EffectiveTo));
    }

    /// <summary>S30-S34, S36: withdrawal outcomes, read back through GET.</summary>
    [Theory]
    [InlineData("2026-10-05", null, "2026-11-01", 204, null, "2026-10-31", false)] // S30
    [InlineData("2026-09-01", null, "2026-10-01", 204, null, "2026-09-30", false)] // S31
    [InlineData("2026-10-05", null, "2026-09-30", 422, "Timetable.WithdrawalDateInPast", null, false)] // S32
    [InlineData("2026-10-05", "2026-12-31", "2027-02-01", 422, "Timetable.WithdrawalDoesNotShorten", "2026-12-31", false)] // S33
    [InlineData("2026-10-05", "2026-12-31", "2027-01-01", 422, "Timetable.WithdrawalDoesNotShorten", "2026-12-31", false)] // S33
    [InlineData("2026-09-01", "2026-10-01", "2026-10-03", 422, "Timetable.WithdrawalDoesNotShorten", "2026-10-01", false)] // S34: already ends today
    [InlineData("2026-11-01", null, "2026-10-15", 204, null, "2026-10-14", true)] // S36
    public async Task Withdraw_ReturnsExpectedStatusAndReadsBack(
        string from, string? to, string withdrawFrom, int status, string? errorCode, string? effectiveTo, bool neverRuns)
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);
        var id = await CreateAsync(client, Body(network, effectiveFrom: from, effectiveTo: to));

        var response = await WithdrawAsync(client, id, withdrawFrom);

        Assert.Equal(status, (int)response.StatusCode);
        if (errorCode is not null)
        {
            Assert.Equal(errorCode, await ErrorCodeOf(response));
        }

        var service = await client.GetFromJsonAsync<ServiceResponse>($"/api/v1/services/{id}", CancellationToken);
        Assert.Equal(effectiveTo is null ? null : DateOnly.Parse(effectiveTo, System.Globalization.CultureInfo.InvariantCulture), service!.EffectiveTo);
        Assert.Equal(neverRuns, service.NeverRuns);
        Assert.Equal(status == 204, service.WithdrawnAtUtc is not null);
    }

    /// <summary>S35.</summary>
    [Fact]
    public async Task Withdraw_Twice_Returns204Then204Then422()
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);
        var id = await CreateAsync(client, Body(network));

        var first = await WithdrawAsync(client, id, "2026-11-01");
        var second = await WithdrawAsync(client, id, "2026-10-20");
        var third = await WithdrawAsync(client, id, "2026-11-15");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableContent, third.StatusCode);
        Assert.Equal("Timetable.WithdrawalDoesNotShorten", await ErrorCodeOf(third));
        var service = await client.GetFromJsonAsync<ServiceResponse>($"/api/v1/services/{id}", CancellationToken);
        Assert.Equal(new DateOnly(2026, 10, 19), service!.EffectiveTo);
        Assert.Equal(2, await DatabaseScalarAsync<int>("SELECT COUNT(*) FROM [audit].[AuditEvents] WHERE [Action] = N'Timetable.ServiceWithdrawn';"));
    }

    /// <summary>S38: an unknown id is <c>404</c>; a missing or malformed <c>withdrawFrom</c> is <c>400</c>.</summary>
    [Theory]
    [InlineData("GET unknown", 404, "Timetable.ServiceNotFound")]
    [InlineData("withdraw unknown", 404, "Timetable.ServiceNotFound")]
    [InlineData("withdraw missing date", 400, "Common.ValidationFailed")]
    [InlineData("withdraw malformed date", 400, "Common.ValidationFailed")]
    public async Task UnknownIdOrInvalidWithdrawBody_Returns404Or400(string request, int status, string errorCode)
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);
        var existing = await CreateAsync(client, Body(network));
        var unknown = Guid.CreateVersion7();

        var response = request switch
        {
            "GET unknown" => await client.GetAsync($"/api/v1/services/{unknown}", CancellationToken),
            "withdraw unknown" => await WithdrawAsync(client, unknown, "2026-11-01"),
            "withdraw missing date" => await client.PostAsJsonAsync($"/api/v1/services/{existing}/withdraw", new Dictionary<string, object?>(), CancellationToken),
            _ => await WithdrawAsync(client, existing, "01/11/2026"),
        };

        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(errorCode, await ErrorCodeOf(response));
        Assert.Equal(0, await DatabaseScalarAsync<int>("SELECT COUNT(*) FROM [timetable].[Services] WHERE [WithdrawnAtUtc] IS NOT NULL;"));
    }

    /// <summary>S39.</summary>
    [Fact]
    public async Task Withdraw_AfterRouteDeactivated_Returns204()
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);
        var id = await CreateAsync(client, Body(network));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/v1/routes/{network.Rc}/deactivate", null, CancellationToken)).StatusCode);

        var response = await WithdrawAsync(client, id, "2026-11-01");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>S40.</summary>
    [Fact]
    public async Task Post_WithPastEffectiveFrom_Returns201()
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/services", Body(network, effectiveFrom: "2026-09-01"), CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<CreateServiceResponse>(CancellationToken))!.Id;
        var service = await client.GetFromJsonAsync<ServiceResponse>($"/api/v1/services/{id}", CancellationToken);
        Assert.Equal(new DateOnly(2026, 9, 1), service!.EffectiveFrom);
        Assert.Equal(NowUtc, service.CreatedAtUtc);
    }

    /// <summary>S40a.</summary>
    [Fact]
    public async Task Post_WithEffectiveToInPast_Returns422()
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/services", Body(network, effectiveFrom: "2026-09-01", effectiveTo: "2026-09-30"), CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
        Assert.Equal("Timetable.ServiceEffectiveToInPast", await ErrorCodeOf(response));
        await AssertNoServiceWrittenAsync();
    }

    /// <summary>S40a.</summary>
    [Fact]
    public async Task Post_WithEffectiveToToday_Returns201()
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/services", Body(network, effectiveFrom: "2026-09-01", effectiveTo: "2026-10-01"), CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>
    /// S41, through the test handler. The deployed challenge body (<c>Auth.Unauthenticated</c>,
    /// <c>WWW-Authenticate: Bearer</c>) for the same four endpoints is asserted on the unmodified
    /// host by <c>DeployedShapeTests.ProtectedEndpoint_Anonymous_Returns401BearerChallengeWithProblemDetails</c>.
    /// </summary>
    [Theory]
    [InlineData("GET", "/api/v1/services")]
    [InlineData("GET", "/api/v1/services/11111111-1111-1111-1111-111111111111")]
    [InlineData("POST", "/api/v1/services")]
    [InlineData("POST", "/api/v1/services/11111111-1111-1111-1111-111111111111/withdraw")]
    public async Task AnyServiceEndpoint_Anonymous_Returns401(string method, string path)
    {
        using var client = Services.CreateAnonymousClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        var response = await client.SendAsync(request, CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>S42: <c>services.read</c> grants no write.</summary>
    [Theory]
    [InlineData("create")]
    [InlineData("withdraw")]
    public async Task WriteEndpoints_WithOnlyServicesRead_Return403(string endpoint)
    {
        using var admin = AdminClient();
        var network = await NetworkAsync(admin);
        var id = await CreateAsync(admin, Body(network));
        using var reader = Services.CreateClientWith(Permissions.ServicesRead);

        var response = endpoint == "create"
            ? await reader.PostAsJsonAsync("/api/v1/services", Body(network, code: "S202"), CancellationToken)
            : await WithdrawAsync(reader, id, "2026-11-01");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, await DatabaseScalarAsync<int>("SELECT COUNT(*) FROM [timetable].[Services] WHERE [WithdrawnAtUtc] IS NULL;"));
    }

    /// <summary>S42, R4: no station or route permission gives any service right.</summary>
    [Theory]
    [InlineData(Permissions.RoutesManage, "POST")]
    [InlineData(Permissions.RoutesManage, "GET")]
    [InlineData(Permissions.RoutesRead, "POST")]
    [InlineData(Permissions.RoutesRead, "GET")]
    [InlineData(Permissions.StationsManage, "POST")]
    [InlineData(Permissions.StationsManage, "GET")]
    [InlineData(Permissions.StationsRead, "POST")]
    [InlineData(Permissions.StationsRead, "GET")]
    public async Task ServiceEndpoints_WithOnlyStationOrRoutePermissions_Return403(string permission, string method)
    {
        using var client = Services.CreateClientWith(permission);

        var response = method == "GET"
            ? await client.GetAsync("/api/v1/services", CancellationToken)
            : await client.PostAsJsonAsync("/api/v1/services", Body(TimetableApiNetwork.Unconnected), CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertNoServiceWrittenAsync();
    }

    /// <summary>S44.</summary>
    [Fact]
    public async Task List_WithPageSizeAbove200_Returns400InvalidPageRequest()
    {
        using var client = AdminClient();

        var response = await client.GetAsync("/api/v1/services?pageSize=201", CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Timetable.InvalidPageRequest", await ErrorCodeOf(response));
    }

    /// <summary>S45: actor fields in the body are ignored; the audit row names the authenticated caller.</summary>
    [Fact]
    public async Task Post_WhenRequestTriesToSupplyActorFields_RecordsTheAuthenticatedActor()
    {
        var realActor = Guid.CreateVersion7();
        using var client = Services.CreateClientAs(
            realActor, Permissions.ServicesManage, Permissions.ServicesRead, Permissions.RoutesManage, Permissions.StationsManage);
        var network = await NetworkAsync(client);
        var body = Body(network);
        body["actorUserId"] = Guid.CreateVersion7().ToString();
        body["actorRole"] = "SystemAdministrator";
        body["authorizedByPermission"] = "users.manage";

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/services", body, CancellationToken)).StatusCode);

        const string Where = " FROM [audit].[AuditEvents] WHERE [Action] = N'Timetable.ServiceCreated';";
        Assert.Equal(realActor, await DatabaseScalarAsync<Guid>("SELECT [ActorUserId]" + Where));
        Assert.Equal(Permissions.ServicesManage, await DatabaseScalarAsync<string>("SELECT [AuthorizedByPermission]" + Where));
    }

    /// <summary>S49: real Myanmar Unicode, written as escapes so no editor can normalise it.</summary>
    [Fact]
    public async Task Post_WithMyanmarName_RoundTripsThroughGet()
    {
        const string MyanmarName = "မြို့ပတ်ရထား";
        using var client = AdminClient();
        var network = await NetworkAsync(client);

        var id = await CreateAsync(client, Body(network, nameMy: MyanmarName));

        var service = await client.GetFromJsonAsync<ServiceResponse>($"/api/v1/services/{id}", CancellationToken);
        Assert.Equal(MyanmarName, service!.NameMy);
    }

    /// <summary>S52, R40.</summary>
    [Fact]
    public async Task Post_WithSundayAndMonday_ReadsBackMondayFirst()
    {
        using var client = AdminClient();
        var network = await NetworkAsync(client);

        var id = await CreateAsync(client, Body(network, days: ["Sunday", "Monday"]));

        var service = await client.GetFromJsonAsync<ServiceResponse>($"/api/v1/services/{id}", CancellationToken);
        Assert.Equal(["Monday", "Sunday"], service!.OperatingDays);
        var summary = await client.GetFromJsonAsync<PagedResponse<ServiceSummaryResponse>>("/api/v1/services", CancellationToken);
        Assert.Equal(["Monday", "Sunday"], summary!.Items.Single().OperatingDays);
    }

    /// <summary>
    /// R5, R20, R33: no edit, no delete, no trains. (F-005 routes <c>POST /schedules/versions</c>;
    /// its row was removed by ruling Q4, hein, 2026-09-27, and its absent routes are
    /// <c>ScheduleVersionEndpointsTests.AbsentScheduleEndpoints_AreNotRouted</c>.)
    /// </summary>
    [Theory]
    [InlineData("PATCH", "/api/v1/services/11111111-1111-1111-1111-111111111111")]
    [InlineData("PUT", "/api/v1/services/11111111-1111-1111-1111-111111111111")]
    [InlineData("DELETE", "/api/v1/services/11111111-1111-1111-1111-111111111111")]
    [InlineData("GET", "/api/v1/trains")]
    public async Task AbsentEndpoints_AreNotRouted(string method, string path)
    {
        using var client = AdminClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(new { code = "S1" }),
        };

        var response = await client.SendAsync(request, CancellationToken);

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
    }

    private HttpClient AdminClient() => Services.CreateClientWith(
        Permissions.ServicesManage,
        Permissions.ServicesRead,
        Permissions.RoutesManage,
        Permissions.RoutesRead,
        Permissions.StationsManage,
        Permissions.StationsRead);

    /// <summary>Spec §4's network through the API: RC closed [A-E], RO open [P-S], X on no route; Z names nothing.</summary>
    private async Task<TimetableApiNetwork> NetworkAsync(HttpClient client)
    {
        var stations = new Dictionary<char, Guid>();
        foreach (var letter in "ABCDEPQRSX")
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/stations", new CreateStationRequest($"S{letter}", $"Station S{letter}", "ဘူတာ"), CancellationToken);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            stations[letter] = (await response.Content.ReadFromJsonAsync<CreateStationResponse>(CancellationToken))!.Id;
        }

        stations['Z'] = Guid.CreateVersion7();
        var rc = await CreateRouteAsync(client, "RC", isClosed: true, "ABCDE", stations);
        var ro = await CreateRouteAsync(client, "RO", isClosed: false, "PQRS", stations);
        return new TimetableApiNetwork(rc, ro, stations);
    }

    private async Task<Guid> CreateRouteAsync(HttpClient client, string code, bool isClosed, string letters, Dictionary<char, Guid> stations)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/routes",
            new Dictionary<string, object?>
            {
                ["code"] = code,
                ["nameEn"] = "Circular Route",
                ["nameMy"] = "မြို့ပတ်ရထားလမ်း",
                ["isClosed"] = isClosed,
                ["stationIds"] = letters.Select(letter => stations[letter].ToString()).ToArray(),
            },
            CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateRouteResponse>(CancellationToken))!.Id;
    }

    /// <summary>Spec §4's default create body, with overrides.</summary>
    private static Dictionary<string, object?> Body(
        TimetableApiNetwork network,
        string stops = "ACE",
        string code = "S101",
        Guid? routeId = null,
        string direction = "Forward",
        string effectiveFrom = "2026-10-05",
        string? effectiveTo = null,
        string[]? days = null,
        string nameEn = "Circular",
        string nameMy = "မြို့ပတ်ရထား") => new()
        {
            ["code"] = code,
            ["nameEn"] = nameEn,
            ["nameMy"] = nameMy,
            ["routeId"] = (routeId ?? network.Rc).ToString(),
            ["direction"] = direction,
            ["stopStationIds"] = stops.Select(letter => network.Stations[letter].ToString()).ToArray(),
            ["operatingDays"] = days ?? ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
            ["effectiveFrom"] = effectiveFrom,
            ["effectiveTo"] = effectiveTo,
        };

    private async Task<Guid> CreateAsync(HttpClient client, Dictionary<string, object?> body)
    {
        var response = await client.PostAsJsonAsync("/api/v1/services", body, CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(CancellationToken));
        return (await response.Content.ReadFromJsonAsync<CreateServiceResponse>(CancellationToken))!.Id;
    }

    private Task<HttpResponseMessage> WithdrawAsync(HttpClient client, Guid id, string withdrawFrom) =>
        client.PostAsJsonAsync($"/api/v1/services/{id}/withdraw", new { withdrawFrom }, CancellationToken);

    private async Task AssertNoServiceWrittenAsync()
    {
        Assert.Equal(0, await DatabaseScalarAsync<int>("SELECT COUNT(*) FROM [timetable].[Services];"));
        Assert.Equal(0, await DatabaseScalarAsync<int>("SELECT COUNT(*) FROM [timetable].[ServiceStops];"));
        Assert.Equal(0, await DatabaseScalarAsync<int>("SELECT COUNT(*) FROM [audit].[AuditEvents] WHERE [Action] LIKE N'Timetable.%';"));
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

/// <summary>The spec §4 network as the API created it.</summary>
internal sealed record TimetableApiNetwork(Guid Rc, Guid Ro, IReadOnlyDictionary<char, Guid> Stations)
{
    /// <summary>Ids that name nothing, for requests that must not reach the database.</summary>
    public static TimetableApiNetwork Unconnected { get; } = new(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        "ABCDEPQRSXZ".ToDictionary(letter => letter, _ => Guid.CreateVersion7()));

    public Guid this[char letter] => Stations[letter];
}

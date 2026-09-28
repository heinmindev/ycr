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
/// F-005 SV1-SV45, SV48, SV49, SV53-SV56 end to end through the real API under the <c>ycr_app</c>
/// credential, with the test authentication handler. The API's clock starts at 2026-10-01 03:00 UTC
/// (today in Asia/Yangon is 2026-10-01, a Thursday; spec §4) and some tests move it forward.
/// </summary>
public sealed class ScheduleVersionEndpointsTests(SqlServerFixture fixture) : ApiTestBase(fixture), IAsyncDisposable
{
    private static readonly DateTimeOffset NowUtc = new(2026, 10, 1, 3, 0, 0, TimeSpan.Zero);

    private readonly TestClock clock = new(NowUtc);
    private YcrApiFactory? api;

    protected override string DatabasePrefix => "api_schedule_versions";

    private YcrApiFactory Schedules => api ??= new YcrApiFactory(Database.ApplicationConnectionString, clock: clock);

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (api is not null)
        {
            await api.DisposeAsync();
        }

        await DisposeAsync();
    }

    // ----- Create and read (SV1-SV5) -----

    /// <summary>SV1: 201, Location, id and number; one version, one entry, three times, one event.</summary>
    [Fact]
    public async Task Post_WithValidRequest_Returns201WithLocationIdAndNumber()
    {
        using var client = AdminClient();
        var net = await NetworkAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/schedules/versions", Version("2026-10-05", ValidS1(net.S1)), CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<CreateScheduleVersionResponse>(CancellationToken))!;
        Assert.Equal(1, created.Number);
        Assert.Equal($"/api/v1/schedules/versions/{created.Id}", response.Headers.Location?.ToString());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        Assert.Equal(["id", "number"], document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM [timetable].[ScheduleVersions] WHERE [Status] = N'Draft';"));
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM [timetable].[ScheduleVersionServices];"));
        Assert.Equal(3, await ScalarAsync("SELECT COUNT(*) FROM [timetable].[ScheduleStopTimes];"));
        Assert.Equal(1, await EventsAsync("Timetable.ScheduleVersionCreated"));
    }

    /// <summary>SV2, AGENTS.md rule 4: a caller with only schedules.read sees the exact wire shape.</summary>
    [Fact]
    public async Task Get_ReturnsScheduleVersionResponseRecordNotEntity()
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        var (id, _) = await CreateVersionAsync(admin, Version("2026-10-05", ValidS1(net.S1), ValidThreeStop(net.S2)));
        using var reader = Schedules.CreateClientWith(Permissions.SchedulesRead);

        var response = await reader.GetAsync($"/api/v1/schedules/versions/{id}", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        var root = document.RootElement;
        Assert.Equal(
            ["id", "number", "nameEn", "nameMy", "effectiveFrom", "status", "createdAtUtc", "publishedAtUtc", "discardedAtUtc", "cancelledAtUtc", "services"],
            root.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            ["serviceId", "code", "nameEn", "nameMy", "direction", "stopCount", "effectiveFrom", "effectiveTo", "neverRuns"],
            root.GetProperty("services")[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal("Draft", root.GetProperty("status").GetString());
        Assert.Equal("2026-10-05", root.GetProperty("effectiveFrom").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("publishedAtUtc").ValueKind);
        Assert.Equal(["S101", "S202"], root.GetProperty("services").EnumerateArray().Select(service => service.GetProperty("code").GetString()));
        Assert.Equal(3, root.GetProperty("services")[0].GetProperty("stopCount").GetInt32());
    }

    /// <summary>SV2: one service's stops, HH:mm times and nulls, stations' current code and names.</summary>
    [Fact]
    public async Task GetServiceTimes_ReturnsStopsWithHHmmTimesAndNulls()
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        var (id, _) = await CreateVersionAsync(admin, Version("2026-10-05", ValidS1(net.S1)));
        using var reader = Schedules.CreateClientWith(Permissions.SchedulesRead);

        var response = await reader.GetAsync($"/api/v1/schedules/versions/{id}/services/{net.S1}", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        var root = document.RootElement;
        Assert.Equal(["versionId", "serviceId", "code", "stops"], root.EnumerateObject().Select(property => property.Name));
        var stops = root.GetProperty("stops").EnumerateArray().ToList();
        Assert.Equal(
            ["position", "stationId", "stationCode", "stationNameEn", "stationNameMy", "arrival", "departure"],
            stops[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal(["SA", "SC", "SE"], stops.Select(stop => stop.GetProperty("stationCode").GetString()));
        Assert.Equal(JsonValueKind.Null, stops[0].GetProperty("arrival").ValueKind);
        Assert.Equal("06:00", stops[0].GetProperty("departure").GetString());
        Assert.Equal(("06:20", "06:22"), (stops[1].GetProperty("arrival").GetString(), stops[1].GetProperty("departure").GetString()));
        Assert.Equal("06:40", stops[2].GetProperty("arrival").GetString());
        Assert.Equal(JsonValueKind.Null, stops[2].GetProperty("departure").ValueKind);
    }

    [Fact]
    public async Task GetServiceTimes_ForAServiceNotInTheVersion_Returns404NotInVersion()
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        var (id, _) = await CreateVersionAsync(admin, Version("2026-10-05", ValidS1(net.S1)));

        var notListed = await admin.GetAsync($"/api/v1/schedules/versions/{id}/services/{net.S2}", CancellationToken);
        var unknown = await admin.GetAsync($"/api/v1/schedules/versions/{Guid.CreateVersion7()}", CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, notListed.StatusCode);
        Assert.Equal("Timetable.ScheduleServiceNotInVersion", await ErrorCodeOf(notListed));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("Timetable.ScheduleVersionNotFound", await ErrorCodeOf(unknown));
    }

    /// <summary>SV3: the paged envelope, ordered by number; ?status filters.</summary>
    [Fact]
    public async Task List_ReturnsPagedEnvelopeOrderedByNumberAndFiltersByStatus()
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        var (first, _) = await CreateVersionAsync(admin, Version("2026-10-05", ValidS1(net.S1)));
        Assert.Equal(HttpStatusCode.NoContent, (await PostActionAsync(admin, first, "publish")).StatusCode);
        var (second, _) = await CreateVersionAsync(admin, Version("2026-10-06", ValidS1(net.S1)));

        var response = await admin.GetAsync("/api/v1/schedules/versions?page=1&pageSize=50", CancellationToken);
        var published = await admin.GetFromJsonAsync<PagedResponse<ScheduleVersionSummaryResponse>>(
            "/api/v1/schedules/versions?status=Published", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = (await response.Content.ReadFromJsonAsync<PagedResponse<ScheduleVersionSummaryResponse>>(CancellationToken))!;
        Assert.Equal((1, 50, 2), (page.Page, page.PageSize, page.TotalCount));
        Assert.Equal([first, second], page.Items.Select(item => item.Id));
        Assert.Equal(["Published", "Draft"], page.Items.Select(item => item.Status));
        Assert.Equal([1, 1], page.Items.Select(item => item.ServiceCount));
        Assert.Equal([first], published!.Items.Select(item => item.Id));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        Assert.Equal(
            ["id", "number", "nameEn", "nameMy", "effectiveFrom", "status", "serviceCount", "createdAtUtc", "publishedAtUtc", "discardedAtUtc", "cancelledAtUtc"],
            document.RootElement.GetProperty("items")[0].EnumerateObject().Select(property => property.Name));
    }

    /// <summary>Q2 (spec Amendment 3): an unknown status is 400 Common.ValidationFailed.</summary>
    [Theory]
    [InlineData("Live")]
    [InlineData("published")]
    [InlineData("DRAFT")]
    public async Task List_WithUnknownStatus_Returns400ValidationFailed(string status)
    {
        using var admin = AdminClient();

        var response = await admin.GetAsync($"/api/v1/schedules/versions?status={status}", CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Common.ValidationFailed", await ErrorCodeOf(response));
    }

    /// <summary>SV4: 1, 2; discard 2; 3; cancel 3; 4 — numbers never reused.</summary>
    [Fact]
    public async Task Numbers_AreContiguousAcrossDiscardAndCancel()
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);

        var (_, one) = await CreateVersionAsync(admin, Version("2026-10-05", ValidS1(net.S1)));
        var (two, twoNumber) = await CreateVersionAsync(admin, Version("2026-10-06", ValidS1(net.S1)));
        Assert.Equal(HttpStatusCode.NoContent, (await PostActionAsync(admin, two, "discard")).StatusCode);
        var (three, threeNumber) = await CreateVersionAsync(admin, Version("2026-10-07", ValidS1(net.S1)));
        Assert.Equal(HttpStatusCode.NoContent, (await PostActionAsync(admin, three, "publish")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PostActionAsync(admin, three, "cancel")).StatusCode);
        var (_, four) = await CreateVersionAsync(admin, Version("2026-10-08", ValidS1(net.S1)));

        Assert.Equal([1, 2, 3, 4], [one, twoNumber, threeNumber, four]);
        var cancelled = await admin.GetFromJsonAsync<ScheduleVersionResponse>($"/api/v1/schedules/versions/{three}", CancellationToken);
        Assert.Equal((3, "Cancelled"), (cancelled!.Number, cancelled.Status));
    }

    /// <summary>SV5: real Myanmar Unicode text round-trips unchanged.</summary>
    [Fact]
    public async Task Post_WithMyanmarName_RoundTripsThroughGet()
    {
        const string myanmar = "ရန်ကုန်မြို့ပတ်ရထား အချိန်ဇယား";
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);

        var (id, _) = await CreateVersionAsync(admin, Version("2026-10-05", [ValidS1(net.S1)], nameMy: myanmar));

        var version = await admin.GetFromJsonAsync<ScheduleVersionResponse>($"/api/v1/schedules/versions/{id}", CancellationToken);
        Assert.Equal(myanmar, version!.NameMy);
    }

    // ----- Times (SV6-SV13) -----

    public static TheoryData<string, string?[], string> InvalidTimes => new()
    {
        { "SV6 arrival at stop 1", ["05:59", "06:00", "06:20", "06:22", "06:40", null], "Timetable.ScheduleStopTimeUnexpected" },
        { "SV7 departure missing at C", [null, "06:00", "06:20", null, "06:40", null], "Timetable.ScheduleStopTimesIncomplete" },
        { "SV8 position 4 of three", [], "Timetable.ScheduleStopNotInService" },
        { "SV9 negative dwell", [null, "06:00", "06:22", "06:20", "06:40", null], "Timetable.ScheduleDwellNegative" },
        { "SV10 equal", [null, "06:00", "06:00", "06:22", "06:40", null], "Timetable.ScheduleTimesNotIncreasing" },
        { "SV12 crossing midnight", [null, "23:50", "00:10", "00:12", "00:30", null], "Timetable.ScheduleTimesNotIncreasing" },
    };

    /// <summary>SV6-SV10, SV12: 422 with the code; nothing written.</summary>
    [Theory]
    [MemberData(nameof(InvalidTimes))]
    public async Task Post_WithInvalidTimes_Returns422WithErrorCode(string description, string?[] times, string code)
    {
        _ = description;
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        var service = times.Length == 0
            ? new Dictionary<string, object?>
            {
                ["serviceId"] = net.S1.ToString(),
                ["stopTimes"] = new object[]
                {
                    Stop(1, null, "06:00"), Stop(2, "06:20", "06:22"), Stop(3, "06:40", null), Stop(4, "06:50", null)
                }
            }
            : ServiceTimes(net.S1, times);

        var response = await PostVersionAsync(admin, Version("2026-10-05", service));

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
        Assert.Equal(code, await ErrorCodeOf(response));
        await AssertNoVersionWrittenAsync();
    }

    /// <summary>SV11: the seven malformed strings are 400 Timetable.InvalidTimetableTime.</summary>
    [Theory]
    [InlineData("24:00")]
    [InlineData("24:15")]
    [InlineData("6:00")]
    [InlineData("06:60")]
    [InlineData("06:00:30")]
    [InlineData("0600")]
    [InlineData("")]
    public async Task Post_WithBadTimeFormat_Returns400InvalidTimetableTime(string text)
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);

        var response = await PostVersionAsync(admin, Version("2026-10-05", ServiceTimes(net.S1, [null, "06:00", text, "06:22", "06:40", null])));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Timetable.InvalidTimetableTime", await ErrorCodeOf(response));
        await AssertNoVersionWrittenAsync();
    }

    /// <summary>SV9 (dwell 0), SV11 (00:00 … 23:59), SV13 (full circuit, closing stop arrives only).</summary>
    [Fact]
    public async Task Post_WithEdgeTimesOrFullCircuit_Returns201()
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        var circuit = await CreateServiceAsync(admin, ServiceBody(net.Network, "CDEABC", code: "S303"));

        Assert.Equal(HttpStatusCode.Created, (await PostVersionAsync(admin, Version("2026-10-05", ServiceTimes(net.S1, [null, "06:00", "06:20", "06:20", "06:40", null])))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await PostVersionAsync(admin, Version("2026-10-06", ServiceTimes(net.S1, [null, "00:00", "12:00", "12:01", "23:59", null])))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await PostVersionAsync(admin, Version("2026-10-07", ServiceTimes(circuit,
            [null, "08:00", "08:10", "08:11", "08:20", "08:21", "08:30", "08:31", "08:40", "08:41", "08:50", null])))).StatusCode);
    }

    // ----- Services in a version (SV14-SV16) -----

    /// <summary>SV14, SV15: unknown, repeated, and four ways of not being effective on 2026-10-05.</summary>
    [Theory]
    [InlineData("unknown", "Timetable.ScheduleServiceNotFound")]
    [InlineData("repeated", "Timetable.ScheduleServiceRepeated")]
    [InlineData("starts later", "Timetable.ScheduleServiceNotEffective")]
    [InlineData("ended before", "Timetable.ScheduleServiceNotEffective")]
    [InlineData("withdrawn from the start date", "Timetable.ScheduleServiceNotEffective")]
    [InlineData("never runs", "Timetable.ScheduleServiceNotEffective")]
    public async Task Post_WithServiceErrors_Returns422(string kind, string code)
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        object[] services = kind switch
        {
            "unknown" => [ValidThreeStop(Guid.CreateVersion7())],
            "repeated" => [ValidS1(net.S1), ValidS1(net.S1)],
            "starts later" => [ValidThreeStop(await CreateServiceAsync(admin, ServiceBody(net.Network, code: "S401", effectiveFrom: "2026-10-06")))],
            "ended before" => [ValidThreeStop(await CreateServiceAsync(admin, ServiceBody(net.Network, code: "S401", effectiveFrom: "2026-09-01", effectiveTo: "2026-10-04")))],
            _ => [ValidThreeStop(await WithdrawnAsync(admin, net, kind == "never runs" ? "2026-10-10" : "2026-09-01"))],
        };

        var response = await PostVersionAsync(admin, Version("2026-10-05", services));

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
        Assert.Equal(code, await ErrorCodeOf(response));
        await AssertNoVersionWrittenAsync();
    }

    /// <summary>SV16: effective only on the start date, or from the past (OQ50).</summary>
    [Fact]
    public async Task Post_WithServiceEffectiveOnlyOnStartDate_Returns201()
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        var oneDay = await CreateServiceAsync(admin, ServiceBody(net.Network, code: "S401", effectiveFrom: "2026-10-05", effectiveTo: "2026-10-05"));
        var fromPast = await CreateServiceAsync(admin, ServiceBody(net.Network, code: "S402", effectiveFrom: "2026-09-01"));

        var response = await PostVersionAsync(admin, Version("2026-10-05", ValidThreeStop(oneDay), ValidThreeStop(fromPast)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ----- Version fields and start date (SV18, SV19, SV22, SV55) -----

    public static TheoryData<string, string> InvalidBodies => new()
    {
        { "effectiveFrom missing", """{"nameEn":"N","nameMy":"M","services":[]}""" },
        { "effectiveFrom not a date", """{"nameEn":"N","nameMy":"M","effectiveFrom":"2026-13-01","services":[]}""" },
        { "effectiveFrom another format", """{"nameEn":"N","nameMy":"M","effectiveFrom":"05/10/2026","services":[]}""" },
        { "services missing", """{"nameEn":"N","nameMy":"M","effectiveFrom":"2026-10-05"}""" },
        { "services null element", """{"nameEn":"N","nameMy":"M","effectiveFrom":"2026-10-05","services":[null]}""" },
        { "serviceId null", """{"nameEn":"N","nameMy":"M","effectiveFrom":"2026-10-05","services":[{"serviceId":null,"stopTimes":[]}]}""" },
        { "serviceId not a GUID", """{"nameEn":"N","nameMy":"M","effectiveFrom":"2026-10-05","services":[{"serviceId":"abc","stopTimes":[]}]}""" },
        { "stopTimes missing", """{"nameEn":"N","nameMy":"M","effectiveFrom":"2026-10-05","services":[{"serviceId":"11111111-1111-1111-1111-111111111111"}]}""" },
        { "stop time null", """{"nameEn":"N","nameMy":"M","effectiveFrom":"2026-10-05","services":[{"serviceId":"11111111-1111-1111-1111-111111111111","stopTimes":[null]}]}""" },
        { "position missing", """{"nameEn":"N","nameMy":"M","effectiveFrom":"2026-10-05","services":[{"serviceId":"11111111-1111-1111-1111-111111111111","stopTimes":[{"departure":"06:00"}]}]}""" },
        { "position 0", """{"nameEn":"N","nameMy":"M","effectiveFrom":"2026-10-05","services":[{"serviceId":"11111111-1111-1111-1111-111111111111","stopTimes":[{"position":0,"departure":"06:00"}]}]}""" },
        { "position -1", """{"nameEn":"N","nameMy":"M","effectiveFrom":"2026-10-05","services":[{"serviceId":"11111111-1111-1111-1111-111111111111","stopTimes":[{"position":-1,"departure":"06:00"}]}]}""" },
    };

    /// <summary>SV18, SV8: shape errors are 400 Common.ValidationFailed, before the handler.</summary>
    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task Post_WithInvalidBody_Returns400CommonValidationFailed(string description, string json)
    {
        _ = description;
        using var admin = AdminClient();

        var response = await admin.PostAsync("/api/v1/schedules/versions", new StringContent(json, System.Text.Encoding.UTF8, "application/json"), CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Common.ValidationFailed", await ErrorCodeOf(response));
        await AssertNoVersionWrittenAsync();
    }

    /// <summary>SV18: a blank, 101-character or missing name is 400 Timetable.InvalidScheduleVersionName.</summary>
    [Theory]
    [InlineData("blank")]
    [InlineData("101")]
    [InlineData("missing")]
    public async Task Post_WithInvalidName_Returns400InvalidScheduleVersionName(string kind)
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        var body = Version("2026-10-05", ValidS1(net.S1));
        switch (kind)
        {
            case "blank":
                body["nameEn"] = "   ";
                break;
            case "101":
                body["nameMy"] = new string('က', 101);
                break;
            default:
                body.Remove("nameEn");
                break;
        }

        var response = await PostVersionAsync(admin, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Timetable.InvalidScheduleVersionName", await ErrorCodeOf(response));
        await AssertNoVersionWrittenAsync();
    }

    /// <summary>SV19: a start date before today is 422.</summary>
    [Fact]
    public async Task Post_WithStartBeforeToday_Returns422()
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        var early = await CreateServiceAsync(admin, ServiceBody(net.Network, code: "S401", effectiveFrom: "2026-09-01"));

        var response = await PostVersionAsync(admin, Version("2026-09-30", ValidThreeStop(early)));

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
        Assert.Equal("Timetable.ScheduleVersionEffectiveFromInPast", await ErrorCodeOf(response));
    }

    /// <summary>SV19 (Amendment 3): today, listing a service effective on 2026-10-01.</summary>
    [Fact]
    public async Task Post_WithStartToday_Returns201()
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        var early = await CreateServiceAsync(admin, ServiceBody(net.Network, code: "S401", effectiveFrom: "2026-09-01"));

        var response = await PostVersionAsync(admin, Version("2026-10-01", ValidThreeStop(early)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>SV22: an empty services array (from tomorrow) is a header only.</summary>
    [Fact]
    public async Task Post_WithNoServices_Returns201()
    {
        using var admin = AdminClient();

        var response = await PostVersionAsync(admin, Version("2026-10-02"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM [timetable].[ScheduleVersions];"));
        Assert.Equal(0, await ScalarAsync("SELECT COUNT(*) FROM [timetable].[ScheduleVersionServices];"));
    }

    /// <summary>SV55 at creation (Amendment 2): an empty version from today is 422 EmptyScheduleVersionNotInFuture.</summary>
    [Fact]
    public async Task Post_WithNoServicesStartingToday_Returns422EmptyNotInFuture()
    {
        using var admin = AdminClient();

        var response = await PostVersionAsync(admin, Version("2026-10-01"));

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
        Assert.Equal("Timetable.EmptyScheduleVersionNotInFuture", await ErrorCodeOf(response));
        await AssertNoVersionWrittenAsync();
    }

    // ----- Publish (SV17, SV20, SV21, SV23-SV25, SV55) -----

    /// <summary>SV20, SV21, SV23, SV25 ×4, SV55 at publication.</summary>
    [Theory]
    [InlineData("SV23 draft", 204, null)]
    [InlineData("SV21 on its start date", 204, null)]
    [InlineData("SV20 after its start date", 422, "Timetable.ScheduleVersionEffectiveFromInPast")]
    [InlineData("SV25 published", 422, "Timetable.ScheduleVersionNotDraft")]
    [InlineData("SV25 cancelled", 422, "Timetable.ScheduleVersionNotDraft")]
    [InlineData("SV25 discarded", 422, "Timetable.ScheduleVersionNotDraft")]
    [InlineData("SV25 unknown", 404, "Timetable.ScheduleVersionNotFound")]
    [InlineData("SV55 empty, its start has become today", 422, "Timetable.EmptyScheduleVersionNotInFuture")]
    public async Task Publish_ReturnsExpectedStatuses(string state, int status, string? code)
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        var early = await CreateServiceAsync(admin, ServiceBody(net.Network, code: "S401", effectiveFrom: "2026-09-01"));
        Guid id;
        switch (state)
        {
            case "SV21 on its start date":
                (id, _) = await CreateVersionAsync(admin, Version("2026-10-01", ValidThreeStop(early)));
                break;
            case "SV20 after its start date":
                (id, _) = await CreateVersionAsync(admin, Version("2026-10-02", ValidThreeStop(early)));
                clock.Advance(TimeSpan.FromDays(2));
                break;
            case "SV25 unknown":
                id = Guid.CreateVersion7();
                break;
            case "SV55 empty, its start has become today":
                (id, _) = await CreateVersionAsync(admin, Version("2026-10-02"));
                clock.Advance(TimeSpan.FromDays(1));
                break;
            default:
                (id, _) = await CreateVersionAsync(admin, Version("2026-10-05", ValidS1(net.S1)));
                if (state is "SV25 published" or "SV25 cancelled")
                {
                    Assert.Equal(HttpStatusCode.NoContent, (await PostActionAsync(admin, id, "publish")).StatusCode);
                }

                if (state == "SV25 cancelled")
                {
                    Assert.Equal(HttpStatusCode.NoContent, (await PostActionAsync(admin, id, "cancel")).StatusCode);
                }

                if (state == "SV25 discarded")
                {
                    Assert.Equal(HttpStatusCode.NoContent, (await PostActionAsync(admin, id, "discard")).StatusCode);
                }

                break;
        }

        var events = await ScalarAsync("SELECT COUNT(*) FROM [audit].[AuditEvents];");

        var response = await PostActionAsync(admin, id, "publish");

        Assert.Equal(status, (int)response.StatusCode);
        if (code is null)
        {
            Assert.Equal(events + 1, await ScalarAsync("SELECT COUNT(*) FROM [audit].[AuditEvents];"));
        }
        else
        {
            Assert.Equal(code, await ErrorCodeOf(response));
            Assert.Equal(events, await ScalarAsync("SELECT COUNT(*) FROM [audit].[AuditEvents];"));
        }
    }

    /// <summary>SV24: a second published start date is 409 until the first is cancelled.</summary>
    [Fact]
    public async Task Publish_WithAPublishedStartDate_Returns409ThenAfterCancel204()
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        var (v1, _) = await CreateVersionAsync(admin, Version("2026-10-05", ValidS1(net.S1)));
        Assert.Equal(HttpStatusCode.NoContent, (await PostActionAsync(admin, v1, "publish")).StatusCode);
        var (draft, _) = await CreateVersionAsync(admin, Version("2026-10-05", ValidThreeStop(net.S2)));

        var taken = await PostActionAsync(admin, draft, "publish");

        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal("Timetable.ScheduleVersionEffectiveFromTaken", await ErrorCodeOf(taken));
        Assert.Equal(HttpStatusCode.NoContent, (await PostActionAsync(admin, v1, "cancel")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PostActionAsync(admin, draft, "publish")).StatusCode);
    }

    /// <summary>SV17: a listed service withdrawn from the start date since creation → 422 NotEffective.</summary>
    [Fact]
    public async Task Publish_AfterAListedServiceWasWithdrawn_Returns422NotEffective()
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        var (draft, _) = await CreateVersionAsync(admin, Version("2026-11-01", ValidThreeStop(net.S2)));
        Assert.Equal(HttpStatusCode.NoContent, (await WithdrawAsync(admin, net.S2, "2026-11-01")).StatusCode);

        var response = await PostActionAsync(admin, draft, "publish");

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
        Assert.Equal("Timetable.ScheduleServiceNotEffective", await ErrorCodeOf(response));
    }

    // ----- In force (SV27-SV30, SV53) -----

    /// <summary>SV27, SV28, SV29 through the API.</summary>
    [Theory]
    [InlineData("2026-10-04", null)]
    [InlineData("2026-10-05", "V1")]
    [InlineData("2026-10-31", "V1")]
    [InlineData("2026-11-15", "V3")]
    [InlineData("2026-12-31", "V3")]
    [InlineData("2027-01-01", "V2")]
    [InlineData("2030-01-01", "V2")]
    public async Task InForce_ReturnsTheVersionForEachDate(string date, string? expected)
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        var ids = new Dictionary<string, Guid>
        {
            ["V1"] = await PublishedAsync(admin, Version("2026-10-05", ValidS1(net.S1))),
            ["V2"] = await PublishedAsync(admin, Version("2027-01-01", ValidS1(net.S1))),
            ["V3"] = await PublishedAsync(admin, Version("2026-11-01", ValidS1(net.S1))),
        };

        var response = await admin.GetAsync($"/api/v1/schedules/versions/in-force?date={date}", CancellationToken);

        if (expected is null)
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("Timetable.ScheduleVersionNotInForce", await ErrorCodeOf(response));
            return;
        }

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var inForce = (await response.Content.ReadFromJsonAsync<ScheduleVersionInForceResponse>(CancellationToken))!;
        Assert.Equal(ids[expected], inForce.Id);
        Assert.Equal(DateOnly.ParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), inForce.Date);
    }

    /// <summary>SV30: runsOnDate per service, and the exact wire shape.</summary>
    [Fact]
    public async Task InForce_ReportsRunsOnDate()
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        await PublishedAsync(admin, Version("2026-10-05", ValidS1(net.S1), ValidThreeStop(net.S2)));

        var saturday = await admin.GetAsync("/api/v1/schedules/versions/in-force?date=2026-10-10", CancellationToken);
        var monday = await admin.GetFromJsonAsync<ScheduleVersionInForceResponse>("/api/v1/schedules/versions/in-force?date=2026-10-12", CancellationToken);

        using var document = JsonDocument.Parse(await saturday.Content.ReadAsStringAsync(CancellationToken));
        var root = document.RootElement;
        Assert.Equal(
            ["date", "id", "number", "nameEn", "nameMy", "effectiveFrom", "publishedAtUtc", "services"],
            root.EnumerateObject().Select(property => property.Name));
        Assert.Equal(["serviceId", "code", "runsOnDate"], root.GetProperty("services")[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            [("S101", false), ("S202", true)],
            root.GetProperty("services").EnumerateArray().Select(service => (service.GetProperty("code").GetString(), service.GetProperty("runsOnDate").GetBoolean())));
        Assert.Equal([true, true], monday!.Services.Select(service => service.RunsOnDate));
    }

    /// <summary>SV53: an empty version in force is 200 with services: [].</summary>
    [Fact]
    public async Task InForce_WithAnEmptyVersion_Returns200WithNoServices()
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        var v1 = await PublishedAsync(admin, Version("2026-10-05", ValidS1(net.S1)));
        var empty = await PublishedAsync(admin, Version("2026-11-02"));

        var before = await admin.GetFromJsonAsync<ScheduleVersionInForceResponse>("/api/v1/schedules/versions/in-force?date=2026-11-01", CancellationToken);
        var response = await admin.GetAsync("/api/v1/schedules/versions/in-force?date=2026-11-02", CancellationToken);

        Assert.Equal(v1, before!.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        Assert.Equal(empty, document.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(0, document.RootElement.GetProperty("services").GetArrayLength());
    }

    // ----- Cancel and discard (SV31-SV35, SV56) -----

    /// <summary>SV31, SV33 ×2, SV34 ×4.</summary>
    [Theory]
    [InlineData("SV31 before its start", 204, null)]
    [InlineData("SV33 on its start date", 422, "Timetable.ScheduleVersionAlreadyEffective")]
    [InlineData("SV33 after its start date", 422, "Timetable.ScheduleVersionAlreadyEffective")]
    [InlineData("SV34 draft", 422, "Timetable.ScheduleVersionNotPublished")]
    [InlineData("SV34 discarded", 422, "Timetable.ScheduleVersionNotPublished")]
    [InlineData("SV34 cancelled", 422, "Timetable.ScheduleVersionNotPublished")]
    [InlineData("SV34 unknown", 404, "Timetable.ScheduleVersionNotFound")]
    public async Task Cancel_ReturnsExpectedStatuses(string state, int status, string? code)
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        var early = await CreateServiceAsync(admin, ServiceBody(net.Network, code: "S401", effectiveFrom: "2026-09-01"));
        Guid id;
        switch (state)
        {
            case "SV31 before its start":
                id = await PublishedAsync(admin, Version("2026-11-01", ValidS1(net.S1)));
                break;
            case "SV33 on its start date":
                id = await PublishedAsync(admin, Version("2026-10-01", ValidThreeStop(early)));
                break;
            case "SV33 after its start date":
                id = await PublishedAsync(admin, Version("2026-10-01", ValidThreeStop(early)));
                clock.Advance(TimeSpan.FromDays(1));
                break;
            case "SV34 unknown":
                id = Guid.CreateVersion7();
                break;
            default:
                (id, _) = await CreateVersionAsync(admin, Version("2026-10-05", ValidS1(net.S1)));
                if (state == "SV34 discarded")
                {
                    Assert.Equal(HttpStatusCode.NoContent, (await PostActionAsync(admin, id, "discard")).StatusCode);
                }

                if (state == "SV34 cancelled")
                {
                    Assert.Equal(HttpStatusCode.NoContent, (await PostActionAsync(admin, id, "publish")).StatusCode);
                    Assert.Equal(HttpStatusCode.NoContent, (await PostActionAsync(admin, id, "cancel")).StatusCode);
                }

                break;
        }

        var response = await PostActionAsync(admin, id, "cancel");

        Assert.Equal(status, (int)response.StatusCode);
        if (code is not null)
        {
            Assert.Equal(code, await ErrorCodeOf(response));
        }
    }

    /// <summary>SV32: after cancelling V3, V1 is in force on V3's dates again.</summary>
    [Fact]
    public async Task Cancel_ThenInForceReturnsTheEarlierVersion()
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        var v1 = await PublishedAsync(admin, Version("2026-10-05", ValidS1(net.S1)));
        var v3 = await PublishedAsync(admin, Version("2026-11-01", ValidS1(net.S1)));

        Assert.Equal(HttpStatusCode.NoContent, (await PostActionAsync(admin, v3, "cancel")).StatusCode);

        var inForce = await admin.GetFromJsonAsync<ScheduleVersionInForceResponse>("/api/v1/schedules/versions/in-force?date=2026-11-15", CancellationToken);
        Assert.Equal(v1, inForce!.Id);
        var cancelled = await admin.GetFromJsonAsync<ScheduleVersionResponse>($"/api/v1/schedules/versions/{v3}", CancellationToken);
        Assert.Equal("Cancelled", cancelled!.Status);
    }

    /// <summary>SV56: an empty version starting tomorrow is published, then cancelled.</summary>
    [Fact]
    public async Task EmptyVersionStartingTomorrow_PublishesThenCancels()
    {
        using var admin = AdminClient();
        var (id, _) = await CreateVersionAsync(admin, Version("2026-10-02"));

        Assert.Equal(HttpStatusCode.NoContent, (await PostActionAsync(admin, id, "publish")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PostActionAsync(admin, id, "cancel")).StatusCode);

        Assert.Equal(1, await EventsAsync("Timetable.ScheduleVersionPublished"));
        Assert.Equal(1, await EventsAsync("Timetable.ScheduleVersionCancelled"));
    }

    /// <summary>SV35 ×4.</summary>
    [Theory]
    [InlineData("draft", 204, null)]
    [InlineData("published", 422, "Timetable.ScheduleVersionNotDraft")]
    [InlineData("discarded", 422, "Timetable.ScheduleVersionNotDraft")]
    [InlineData("unknown", 404, "Timetable.ScheduleVersionNotFound")]
    public async Task Discard_ReturnsExpectedStatuses(string state, int status, string? code)
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        var (id, _) = await CreateVersionAsync(admin, Version("2026-10-05", ValidS1(net.S1)));
        if (state == "published")
        {
            Assert.Equal(HttpStatusCode.NoContent, (await PostActionAsync(admin, id, "publish")).StatusCode);
        }

        if (state == "discarded")
        {
            Assert.Equal(HttpStatusCode.NoContent, (await PostActionAsync(admin, id, "discard")).StatusCode);
        }

        var response = await PostActionAsync(admin, state == "unknown" ? Guid.CreateVersion7() : id, "discard");

        Assert.Equal(status, (int)response.StatusCode);
        if (code is not null)
        {
            Assert.Equal(code, await ErrorCodeOf(response));
        }
    }

    // ----- F-004 withdrawal refused while a published version applies (SV36-SV39, SV54) -----

    /// <summary>SV36: 422 ServiceInPublishedScheduleVersion; the service unchanged; no event.</summary>
    [Fact]
    public async Task Withdraw_WhileAPublishedVersionListsTheService_Returns422InPublishedVersion()
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        await PublishedAsync(admin, Version("2026-10-05", ValidS1(net.S1)));

        var response = await WithdrawAsync(admin, net.S1, "2026-11-01");

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
        Assert.Equal("Timetable.ServiceInPublishedScheduleVersion", await ErrorCodeOf(response));
        Assert.Equal(0, await EventsAsync("Timetable.ServiceWithdrawn"));
    }

    /// <summary>SV36: F-004's checks come first.</summary>
    [Fact]
    public async Task Withdraw_WithAPastDate_Returns422DateInPastFirst()
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        await PublishedAsync(admin, Version("2026-10-05", ValidS1(net.S1)));

        var response = await WithdrawAsync(admin, net.S1, "2026-09-30");

        Assert.Equal("Timetable.WithdrawalDateInPast", await ErrorCodeOf(response));
    }

    /// <summary>SV37, R20: publish without S1, then withdraw S1 from that start → 204; the day before → 422.</summary>
    [Fact]
    public async Task DroppingAService_PublishWithoutItThenWithdraw_Returns204AndTheDayBefore422()
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        await PublishedAsync(admin, Version("2026-10-05", ValidS1(net.S1)));
        await PublishedAsync(admin, Version("2027-01-01", ValidThreeStop(net.S2)));

        var dayBefore = await WithdrawAsync(admin, net.S1, "2026-12-31");
        var fromTheDrop = await WithdrawAsync(admin, net.S1, "2027-01-01");

        Assert.Equal(HttpStatusCode.UnprocessableContent, dayBefore.StatusCode);
        Assert.Equal("Timetable.ServiceInPublishedScheduleVersion", await ErrorCodeOf(dayBefore));
        Assert.Equal(HttpStatusCode.NoContent, fromTheDrop.StatusCode);
    }

    /// <summary>SV38: only a draft, a discarded and a cancelled version list S2 → 204.</summary>
    [Fact]
    public async Task Withdraw_ListedOnlyByDraftDiscardedOrCancelled_Returns204()
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        await CreateVersionAsync(admin, Version("2026-10-05", ValidThreeStop(net.S2)));
        var (discarded, _) = await CreateVersionAsync(admin, Version("2026-10-06", ValidThreeStop(net.S2)));
        Assert.Equal(HttpStatusCode.NoContent, (await PostActionAsync(admin, discarded, "discard")).StatusCode);
        var cancelled = await PublishedAsync(admin, Version("2026-10-07", ValidThreeStop(net.S2)));
        Assert.Equal(HttpStatusCode.NoContent, (await PostActionAsync(admin, cancelled, "cancel")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await WithdrawAsync(admin, net.S2, "2026-11-01")).StatusCode);
    }

    /// <summary>SV39: V0 (published when the clock read 2026-09-01) lists S4; V1 does not → 204 from 2026-10-05.</summary>
    [Fact]
    public async Task Withdraw_ListedOnlyByASupersededPastVersion_Returns204()
    {
        var september = new TestClock(new DateTimeOffset(2026, 9, 1, 3, 0, 0, TimeSpan.Zero));
        await using var past = new YcrApiFactory(Database.ApplicationConnectionString, clock: september);
        using var admin = past.CreateClientWith(AdminPermissions);
        var net = await NetworkAsync(admin);
        var s4 = await CreateServiceAsync(admin, ServiceBody(net.Network, code: "S404", effectiveFrom: "2026-09-01"));
        await PublishedAsync(admin, Version("2026-09-01", ValidThreeStop(s4)));
        september.Advance(TimeSpan.FromDays(30));
        await PublishedAsync(admin, Version("2026-10-05", ValidS1(net.S1)));

        Assert.Equal(HttpStatusCode.NoContent, (await WithdrawAsync(admin, s4, "2026-10-05")).StatusCode);
    }

    /// <summary>SV54, R49: the withdrawal stays after the cancel that made it legal; in force says not running.</summary>
    [Fact]
    public async Task WithdrawThenCancel_ServiceStaysWithdrawnAndInForceSaysNotRunning()
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        var v1 = await PublishedAsync(admin, Version("2026-10-05", ValidS1(net.S1)));
        var v2 = await PublishedAsync(admin, Version("2027-01-01", ValidThreeStop(net.S2)));

        Assert.Equal(HttpStatusCode.NoContent, (await WithdrawAsync(admin, net.S1, "2027-01-01")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PostActionAsync(admin, v2, "cancel")).StatusCode);

        var december = await admin.GetFromJsonAsync<ScheduleVersionInForceResponse>("/api/v1/schedules/versions/in-force?date=2026-12-28", CancellationToken);
        var january = await admin.GetFromJsonAsync<ScheduleVersionInForceResponse>("/api/v1/schedules/versions/in-force?date=2027-01-04", CancellationToken);
        Assert.Equal((v1, true), (december!.Id, december.Services.Single().RunsOnDate));
        Assert.Equal((v1, false), (january!.Id, january.Services.Single().RunsOnDate));
        Assert.Equal("Timetable.WithdrawalDoesNotShorten", await ErrorCodeOf(await WithdrawAsync(admin, net.S1, "2027-01-04")));
    }

    // ----- Access, routes, limits (SV43-SV45, SV48, SV49) -----

    public static TheoryData<string, string> ScheduleEndpoints => new()
    {
        { "POST", "/api/v1/schedules/versions" },
        { "GET", "/api/v1/schedules/versions" },
        { "GET", "/api/v1/schedules/versions/in-force?date=2026-10-05" },
        { "GET", "/api/v1/schedules/versions/11111111-1111-1111-1111-111111111111" },
        { "GET", "/api/v1/schedules/versions/11111111-1111-1111-1111-111111111111/services/22222222-2222-2222-2222-222222222222" },
        { "POST", "/api/v1/schedules/versions/11111111-1111-1111-1111-111111111111/publish" },
        { "POST", "/api/v1/schedules/versions/11111111-1111-1111-1111-111111111111/cancel" },
        { "POST", "/api/v1/schedules/versions/11111111-1111-1111-1111-111111111111/discard" },
    };

    /// <summary>
    /// SV43: every schedule endpoint answers an anonymous caller 401. The <c>Auth.Unauthenticated</c>
    /// body is the deployed bearer challenge's, proved on the unmodified host by
    /// <c>DeployedShapeTests.ProtectedEndpoint_Anonymous_Returns401BearerChallengeWithProblemDetails</c>
    /// (the F-004 <c>AnyServiceEndpoint_Anonymous_Returns401</c> pattern).
    /// </summary>
    [Theory]
    [MemberData(nameof(ScheduleEndpoints))]
    public async Task AnyScheduleEndpoint_Anonymous_Returns401(string method, string path)
    {
        using var anonymous = Schedules.CreateAnonymousClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        var response = await anonymous.SendAsync(request, CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNoVersionWrittenAsync();
    }

    /// <summary>SV44: no PATCH, PUT or DELETE route exists under /schedules/versions.</summary>
    [Theory]
    [InlineData("PATCH", "/api/v1/schedules/versions/11111111-1111-1111-1111-111111111111")]
    [InlineData("PUT", "/api/v1/schedules/versions/11111111-1111-1111-1111-111111111111")]
    [InlineData("DELETE", "/api/v1/schedules/versions/11111111-1111-1111-1111-111111111111")]
    [InlineData("PUT", "/api/v1/schedules/versions")]
    [InlineData("DELETE", "/api/v1/schedules/versions")]
    public async Task AbsentScheduleEndpoints_AreNotRouted(string method, string path)
    {
        using var admin = AdminClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { nameEn = "X" }) };

        var response = await admin.SendAsync(request, CancellationToken);

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        await AssertNoVersionWrittenAsync();
    }

    /// <summary>SV45: a caller with only schedules.read is 403 on every write.</summary>
    [Theory]
    [InlineData("/api/v1/schedules/versions")]
    [InlineData("/api/v1/schedules/versions/11111111-1111-1111-1111-111111111111/publish")]
    [InlineData("/api/v1/schedules/versions/11111111-1111-1111-1111-111111111111/cancel")]
    [InlineData("/api/v1/schedules/versions/11111111-1111-1111-1111-111111111111/discard")]
    public async Task WriteEndpoints_WithOnlySchedulesRead_Return403(string path)
    {
        using var reader = Schedules.CreateClientWith(Permissions.SchedulesRead);

        var response = await reader.PostAsJsonAsync(path, Version("2026-10-05"), CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertNoVersionWrittenAsync();
    }

    /// <summary>SV45, R5: station, route and service permissions give no schedule right.</summary>
    [Theory]
    [InlineData(Permissions.ServicesManage)]
    [InlineData(Permissions.ServicesRead)]
    [InlineData(Permissions.RoutesManage)]
    [InlineData(Permissions.RoutesRead)]
    [InlineData(Permissions.StationsManage)]
    [InlineData(Permissions.StationsRead)]
    public async Task ScheduleEndpoints_WithOnlyServiceRouteOrStationPermissions_Return403(string permission)
    {
        using var other = Schedules.CreateClientWith(permission);

        var read = await other.GetAsync("/api/v1/schedules/versions", CancellationToken);
        var write = await other.PostAsJsonAsync("/api/v1/schedules/versions", Version("2026-10-05"), CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    /// <summary>SV45, R5: schedule permissions give no service right.</summary>
    [Fact]
    public async Task Withdraw_WithOnlySchedulePermissions_Returns403()
    {
        using var scheduler = Schedules.CreateClientWith(Permissions.SchedulesManage, Permissions.SchedulesRead);

        var response = await WithdrawAsync(scheduler, Guid.CreateVersion7(), "2026-11-01");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>SV48: pageSize 201 → 400 Timetable.InvalidPageRequest.</summary>
    [Fact]
    public async Task List_WithPageSizeAbove200_Returns400InvalidPageRequest()
    {
        using var admin = AdminClient();

        var response = await admin.GetAsync("/api/v1/schedules/versions?pageSize=201", CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Timetable.InvalidPageRequest", await ErrorCodeOf(response));
    }

    /// <summary>SV48, plan V4: in-force with no date or a malformed one → 400 Common.ValidationFailed.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("?date=")]
    [InlineData("?date=2026-13-01")]
    [InlineData("?date=05/10/2026")]
    [InlineData("?date=2026-10-5")]
    public async Task InForce_WithMissingOrMalformedDate_Returns400ValidationFailed(string query)
    {
        using var admin = AdminClient();

        var response = await admin.GetAsync($"/api/v1/schedules/versions/in-force{query}", CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Common.ValidationFailed", await ErrorCodeOf(response));
    }

    /// <summary>SV49: actor fields in the body are ignored; the event names the authenticated caller.</summary>
    [Fact]
    public async Task Post_WhenRequestTriesToSupplyActorFields_RecordsTheAuthenticatedActor()
    {
        var caller = Guid.CreateVersion7();
        using var admin = Schedules.CreateClientAs(caller, AdminPermissions);
        var body = Version("2026-10-02");
        body["actorUserId"] = Guid.CreateVersion7().ToString();
        body["authorizedByPermission"] = "users.manage";

        Assert.Equal(HttpStatusCode.Created, (await PostVersionAsync(admin, body)).StatusCode);

        Assert.Equal(caller, await DatabaseScalarAsync<Guid>("SELECT [ActorUserId] FROM [audit].[AuditEvents] WHERE [Action] = N'Timetable.ScheduleVersionCreated';"));
        Assert.Equal(Permissions.SchedulesManage, await DatabaseScalarAsync<string>("SELECT [AuthorizedByPermission] FROM [audit].[AuditEvents] WHERE [Action] = N'Timetable.ScheduleVersionCreated';"));
    }

    /// <summary>SV49, R39: a refused create, publish or cancel writes no event.</summary>
    [Theory]
    [InlineData("create")]
    [InlineData("publish")]
    [InlineData("cancel")]
    public async Task RefusedRequests_WriteNoEvent(string action)
    {
        using var admin = AdminClient();
        var net = await NetworkAsync(admin);
        var (draft, _) = await CreateVersionAsync(admin, Version("2026-10-05", ValidS1(net.S1)));
        var events = await ScalarAsync("SELECT COUNT(*) FROM [audit].[AuditEvents];");

        var response = action switch
        {
            "create" => await PostVersionAsync(admin, Version("2026-09-30", ValidS1(net.S1))),
            "publish" => await PostActionAsync(admin, Guid.CreateVersion7(), "publish"),
            _ => await PostActionAsync(admin, draft, "cancel"),
        };

        Assert.True((int)response.StatusCode >= 400);
        Assert.Equal(events, await ScalarAsync("SELECT COUNT(*) FROM [audit].[AuditEvents];"));
    }

    /// <summary>Spec §6, plan V5: /in-force is not taken for an {id}.</summary>
    [Fact]
    public async Task InForceRoute_IsNotShadowedByTheIdRoute()
    {
        using var admin = AdminClient();

        var response = await admin.GetAsync("/api/v1/schedules/versions/in-force?date=2026-10-05", CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Timetable.ScheduleVersionNotInForce", await ErrorCodeOf(response));
    }

    // ----- Helpers -----

    private static readonly string[] AdminPermissions =
    [
        Permissions.SchedulesManage,
        Permissions.SchedulesRead,
        Permissions.ServicesManage,
        Permissions.ServicesRead,
        Permissions.RoutesManage,
        Permissions.RoutesRead,
        Permissions.StationsManage,
        Permissions.StationsRead,
    ];

    private HttpClient AdminClient() => Schedules.CreateClientWith(AdminPermissions);

    /// <summary>Spec §4 through the API: RC closed [A-E], RO open [P-S]; S1 [A, C, E] Mon–Fri, S2 [E, C, A] Reverse every day.</summary>
    private async Task<ScheduleApiNetwork> NetworkAsync(HttpClient client)
    {
        var stations = new Dictionary<char, Guid>();
        foreach (var letter in "ABCDEPQRS")
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/stations", new CreateStationRequest($"S{letter}", $"Station S{letter}", "ဘူတာ"), CancellationToken);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            stations[letter] = (await response.Content.ReadFromJsonAsync<CreateStationResponse>(CancellationToken))!.Id;
        }

        var rc = await CreateRouteAsync(client, "RC", isClosed: true, "ABCDE", stations);
        var ro = await CreateRouteAsync(client, "RO", isClosed: false, "PQRS", stations);
        var network = new TimetableApiNetwork(rc, ro, stations);
        var s1 = await CreateServiceAsync(client, ServiceBody(network));
        var s2 = await CreateServiceAsync(client, ServiceBody(network, "ECA", code: "S202", direction: "Reverse",
            days: ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"]));
        return new ScheduleApiNetwork(network, s1, s2);
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

    private static Dictionary<string, object?> ServiceBody(
        TimetableApiNetwork network,
        string stops = "ACE",
        string code = "S101",
        string direction = "Forward",
        string effectiveFrom = "2026-10-05",
        string? effectiveTo = null,
        string[]? days = null) => new()
        {
            ["code"] = code,
            ["nameEn"] = "Circular",
            ["nameMy"] = "မြို့ပတ်ရထား",
            ["routeId"] = network.Rc.ToString(),
            ["direction"] = direction,
            ["stopStationIds"] = stops.Select(letter => network.Stations[letter].ToString()).ToArray(),
            ["operatingDays"] = days ?? ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
            ["effectiveFrom"] = effectiveFrom,
            ["effectiveTo"] = effectiveTo,
        };

    private async Task<Guid> CreateServiceAsync(HttpClient client, Dictionary<string, object?> body)
    {
        var response = await client.PostAsJsonAsync("/api/v1/services", body, CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(CancellationToken));
        return (await response.Content.ReadFromJsonAsync<CreateServiceResponse>(CancellationToken))!.Id;
    }

    /// <summary>A service from <paramref name="from"/> withdrawn from 2026-10-05 (not effective on it).</summary>
    private async Task<Guid> WithdrawnAsync(HttpClient client, ScheduleApiNetwork net, string from)
    {
        var id = await CreateServiceAsync(client, ServiceBody(net.Network, code: "S401", effectiveFrom: from));
        Assert.Equal(HttpStatusCode.NoContent, (await WithdrawAsync(client, id, "2026-10-05")).StatusCode);
        return id;
    }

    private static Dictionary<string, object?> Stop(int position, string? arrival, string? departure) => new()
    {
        ["position"] = position,
        ["arrival"] = arrival,
        ["departure"] = departure,
    };

    /// <summary>Times as (arrival, departure) pairs for stops 1..k, flattened.</summary>
    private static Dictionary<string, object?> ServiceTimes(Guid serviceId, string?[] pairs) => new()
    {
        ["serviceId"] = serviceId.ToString(),
        ["stopTimes"] = Enumerable.Range(0, pairs.Length / 2).Select(index => Stop(index + 1, pairs[2 * index], pairs[2 * index + 1])).ToArray(),
    };

    private static Dictionary<string, object?> ValidS1(Guid s1) => ServiceTimes(s1, [null, "06:00", "06:20", "06:22", "06:40", null]);

    private static Dictionary<string, object?> ValidThreeStop(Guid serviceId) => ServiceTimes(serviceId, [null, "07:00", "07:20", "07:21", "07:40", null]);

    private static Dictionary<string, object?> Version(string effectiveFrom, params object[] services) =>
        Version(effectiveFrom, services, "October 2026", "အောက်တိုဘာ");

    private static Dictionary<string, object?> Version(string effectiveFrom, object[] services, string nameEn = "October 2026", string nameMy = "အောက်တိုဘာ") => new()
    {
        ["nameEn"] = nameEn,
        ["nameMy"] = nameMy,
        ["effectiveFrom"] = effectiveFrom,
        ["services"] = services,
    };

    private Task<HttpResponseMessage> PostVersionAsync(HttpClient client, Dictionary<string, object?> body) =>
        client.PostAsJsonAsync("/api/v1/schedules/versions", body, CancellationToken);

    private async Task<(Guid Id, int Number)> CreateVersionAsync(HttpClient client, Dictionary<string, object?> body)
    {
        var response = await PostVersionAsync(client, body);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(CancellationToken));
        var created = (await response.Content.ReadFromJsonAsync<CreateScheduleVersionResponse>(CancellationToken))!;
        return (created.Id, created.Number);
    }

    private async Task<Guid> PublishedAsync(HttpClient client, Dictionary<string, object?> body)
    {
        var (id, _) = await CreateVersionAsync(client, body);
        var published = await PostActionAsync(client, id, "publish");
        Assert.True(published.StatusCode == HttpStatusCode.NoContent, await published.Content.ReadAsStringAsync(CancellationToken));
        return id;
    }

    private Task<HttpResponseMessage> PostActionAsync(HttpClient client, Guid id, string action) =>
        client.PostAsync($"/api/v1/schedules/versions/{id}/{action}", content: null, CancellationToken);

    private Task<HttpResponseMessage> WithdrawAsync(HttpClient client, Guid id, string withdrawFrom) =>
        client.PostAsJsonAsync($"/api/v1/services/{id}/withdraw", new { withdrawFrom }, CancellationToken);

    private async Task AssertNoVersionWrittenAsync()
    {
        Assert.Equal(0, await ScalarAsync("SELECT COUNT(*) FROM [timetable].[ScheduleVersions];"));
        Assert.Equal(0, await ScalarAsync("SELECT COUNT(*) FROM [timetable].[ScheduleStopTimes];"));
        Assert.Equal(0, await EventsAsync("Timetable.ScheduleVersionCreated"));
    }

    private Task<int> EventsAsync(string action) =>
        ScalarAsync($"SELECT COUNT(*) FROM [audit].[AuditEvents] WHERE [Action] = N'{action}';");

    private async Task<int> ScalarAsync(string sql) => await DatabaseScalarAsync<int>(sql);

    private async Task<string?> ErrorCodeOf(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync(CancellationToken);
        using var document = JsonDocument.Parse(json);
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

/// <summary>Spec §4's network and services S1, S2 as the API created them.</summary>
internal sealed record ScheduleApiNetwork(TimetableApiNetwork Network, Guid S1, Guid S2);

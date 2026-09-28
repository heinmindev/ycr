using YCR.Application.Timetable.ListScheduleVersions;
using YCR.Domain.Timetable;
using YCR.TestSupport;
using static YCR.Application.Tests.Timetable.ScheduleTestData;
using static YCR.Application.Tests.Timetable.TimetableTestData;

namespace YCR.Application.Tests.Timetable;

/// <summary>
/// F-005 reads against real SQL Server under <c>ycr_app</c>, today 2026-10-01 (Asia/Yangon): SV2,
/// SV3, SV27-SV32, SV48, SV53; R18, R21, R24, R25, R48.
/// </summary>
public sealed class ScheduleVersionQueryHandlerTests(SqlServerFixture fixture) : ScheduleHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "schedule_queries";

    /// <summary>SV2: the header and each listed service's current values, ordered by code.</summary>
    [Fact]
    public async Task GetScheduleVersion_ReturnsHeaderAndListedServicesWithCurrentValues()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var created = await CreateVersionOrFailAsync(provider, Version("2026-10-05", [ValidThreeStop(net.S2), ValidS1(net.S1)]));

        var version = (await GetVersionAsync(provider, created.Id)).Value;

        Assert.Equal(created.Id, version.Id);
        Assert.Equal(1, version.Number);
        Assert.Equal("October 2026", version.NameEn);
        Assert.Equal("အောက်တိုဘာ", version.NameMy);
        Assert.Equal(Date("2026-10-05"), version.EffectiveFrom);
        Assert.Equal("Draft", version.Status);
        Assert.Equal(DefaultNowUtc, version.CreatedAtUtc);
        Assert.Null(version.PublishedAtUtc);
        Assert.Null(version.DiscardedAtUtc);
        Assert.Null(version.CancelledAtUtc);
        Assert.Equal([("S101", net.S1), ("S202", net.S2)], version.Services.Select(service => (service.Code, service.ServiceId)));
        var s1 = version.Services[0];
        Assert.Equal(("Circular", "မြို့ပတ်ရထား", "Forward", 3), (s1.NameEn, s1.NameMy, s1.Direction, s1.StopCount));
        Assert.Equal((Date("2026-10-05"), (DateOnly?)null, false), (s1.EffectiveFrom, s1.EffectiveTo, s1.NeverRuns));
        Assert.Equal("Reverse", version.Services[1].Direction);

        // Current values: a later withdrawal shows in the read; the version's rows do not change.
        Assert.True((await WithdrawServiceAsync(provider, net.S1, Date("2026-11-01"))).IsSuccess);
        var after = (await GetVersionAsync(provider, created.Id)).Value;
        Assert.Equal(Date("2026-10-31"), after.Services[0].EffectiveTo);
    }

    /// <summary>SV2: one service's stops in position order, stations through the Network contract, HH:mm or null.</summary>
    [Fact]
    public async Task GetScheduleServiceTimes_ReturnsStopsInOrderWithStationsAndTimes()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var created = await CreateVersionOrFailAsync(provider, Version("2026-10-05", [ValidS1(net.S1)]));

        var times = (await GetTimesAsync(provider, created.Id, net.S1)).Value;

        Assert.Equal((created.Id, net.S1, "S101"), (times.VersionId, times.ServiceId, times.Code));
        Assert.Equal(
            [
                (1, net.Network['A'], "SA", "Station SA", "ဘူတာ", (string?)null, (string?)"06:00"),
                (2, net.Network['C'], "SC", "Station SC", "ဘူတာ", "06:20", "06:22"),
                (3, net.Network['E'], "SE", "Station SE", "ဘူတာ", "06:40", null),
            ],
            times.Stops.Select(stop => (stop.Position, stop.StationId, stop.StationCode, stop.StationNameEn, stop.StationNameMy, stop.Arrival, stop.Departure)));
    }

    /// <summary>SV2: a service the version does not list → 404 ScheduleServiceNotInVersion.</summary>
    [Fact]
    public async Task GetScheduleServiceTimes_ForAServiceNotInTheVersion_ReturnsNotInVersion()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var created = await CreateVersionOrFailAsync(provider, Version("2026-10-05", [ValidS1(net.S1)]));

        AssertFailure(await GetTimesAsync(provider, created.Id, net.S2), TimetableErrors.ScheduleServiceNotInVersion);
        AssertFailure(await GetTimesAsync(provider, Guid.NewGuid(), net.S1), TimetableErrors.ScheduleVersionNotFound);
    }

    [Fact]
    public async Task GetScheduleVersion_WithUnknownId_ReturnsNotFound()
    {
        await using var provider = BuildScheduleProvider();

        AssertFailure(await GetVersionAsync(provider, Guid.NewGuid()), TimetableErrors.ScheduleVersionNotFound);
    }

    /// <summary>SV3: a paged envelope ordered by number, with the service count; ?status filters exactly.</summary>
    [Fact]
    public async Task ListScheduleVersions_OrderedByNumberAndFilteredByStatus()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var first = await PublishedVersionAsync(provider, "2026-10-05", [ValidS1(net.S1), ValidThreeStop(net.S2)]);
        var second = await CreateVersionOrFailAsync(provider, Version("2026-10-06", []));
        var third = await CreateVersionOrFailAsync(provider, Version("2026-10-07", [ValidS1(net.S1)]));
        Assert.True((await DiscardAsync(provider, third.Id)).IsSuccess);

        var all = (await ListVersionsAsync(provider, new ListScheduleVersionsQuery(1, 2))).Value;
        var rest = (await ListVersionsAsync(provider, new ListScheduleVersionsQuery(2, 2))).Value;
        var published = (await ListVersionsAsync(provider, new ListScheduleVersionsQuery(Status: "Published"))).Value;
        var drafts = (await ListVersionsAsync(provider, new ListScheduleVersionsQuery(Status: "Draft"))).Value;

        Assert.Equal((1, 2, 3), (all.Page, all.PageSize, all.TotalCount));
        Assert.Equal([(1, "Published", 2), (2, "Draft", 0)], all.Items.Select(item => (item.Number, item.Status, item.ServiceCount)));
        Assert.Equal([(3, "Discarded", third.Id)], rest.Items.Select(item => (item.Number, item.Status, item.Id)));
        Assert.Equal([first], published.Items.Select(item => item.Id));
        Assert.Equal(1, published.TotalCount);
        Assert.Equal([second.Id], drafts.Items.Select(item => item.Id));
        Assert.NotNull(all.Items[0].PublishedAtUtc);
    }

    /// <summary>SV48: pageSize 201 → 400 Timetable.InvalidPageRequest.</summary>
    [Theory]
    [InlineData(1, 201)]
    [InlineData(0, 50)]
    [InlineData(1, 0)]
    public async Task ListScheduleVersions_WithPageSizeAbove200_ReturnsInvalidPageRequest(int page, int pageSize)
    {
        await using var provider = BuildScheduleProvider();

        AssertFailure(await ListVersionsAsync(provider, new ListScheduleVersionsQuery(page, pageSize)), TimetableErrors.InvalidPageRequest);
    }

    /// <summary>R25, SV31, SV35: discarded and cancelled versions stay readable and unchanged.</summary>
    [Fact]
    public async Task ScheduleVersions_AfterDiscardOrCancel_StayReadableAndUnchanged()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var discarded = await CreateVersionOrFailAsync(provider, Version("2026-10-05", [ValidS1(net.S1)]));
        Assert.True((await DiscardAsync(provider, discarded.Id)).IsSuccess);
        var cancelled = await PublishedVersionAsync(provider, "2026-11-02", [ValidS1(net.S1)]);
        Assert.True((await CancelAsync(provider, cancelled)).IsSuccess);

        foreach (var (id, status) in new[] { (discarded.Id, "Discarded"), (cancelled, "Cancelled") })
        {
            var version = (await GetVersionAsync(provider, id)).Value;
            Assert.Equal(status, version.Status);
            Assert.Equal([net.S1], version.Services.Select(service => service.ServiceId));
            var times = (await GetTimesAsync(provider, id, net.S1)).Value;
            Assert.Equal(["06:00", "06:22", null], times.Stops.Select(stop => stop.Departure));
        }
    }

    /// <summary>SV27: before any published version, and before the first one's start, nothing is in force.</summary>
    [Fact]
    public async Task GetInForce_BeforeTheFirstVersion_ReturnsNotInForce()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        await CreateVersionOrFailAsync(provider, Version("2026-10-05", [ValidS1(net.S1)]));

        AssertFailure(await InForceAsync(provider, "2026-10-05"), TimetableErrors.ScheduleVersionNotInForce);

        var v1 = await PublishedVersionAsync(provider, "2026-10-05", [ValidS1(net.S1)]);
        AssertFailure(await InForceAsync(provider, "2026-10-04"), TimetableErrors.ScheduleVersionNotInForce);
        Assert.Equal(v1, (await InForceOrFailAsync(provider, "2026-10-05")).Id);
    }

    /// <summary>SV28: V1 from 2026-10-05, V2 from 2027-01-01 (open-ended).</summary>
    [Theory]
    [InlineData("2026-12-31", 1)]
    [InlineData("2027-01-01", 2)]
    [InlineData("2030-01-01", 2)]
    public async Task GetInForce_AcrossASupersession_ReturnsTheLatestStartOnOrBeforeTheDate(string date, int expected)
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var v1 = await PublishedVersionAsync(provider, "2026-10-05", [ValidS1(net.S1)]);
        var v2 = await PublishedVersionAsync(provider, "2027-01-01", [ValidThreeStop(net.S2)]);

        var inForce = await InForceOrFailAsync(provider, date);

        Assert.Equal(expected == 1 ? v1 : v2, inForce.Id);
        Assert.Equal(expected, inForce.Number);
        Assert.Equal(Date(date), inForce.Date);
        Assert.Equal(DefaultNowUtc, inForce.PublishedAtUtc);
    }

    /// <summary>SV29, R24: V3 from 2026-11-01 inserted between V1 and V2 applies to its range only; V1 unchanged.</summary>
    [Fact]
    public async Task GetInForce_WithAnInsertedVersion_ReturnsItForItsRangeOnly()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var v1 = await PublishedVersionAsync(provider, "2026-10-05", [ValidS1(net.S1)]);
        var v2 = await PublishedVersionAsync(provider, "2027-01-01", [ValidS1(net.S1)]);
        var v3 = await PublishedVersionAsync(provider, "2026-11-01", [ValidS1(net.S1)]);

        Assert.Equal(v1, (await InForceOrFailAsync(provider, "2026-10-31")).Id);
        Assert.Equal(v3, (await InForceOrFailAsync(provider, "2026-11-15")).Id);
        Assert.Equal(v2, (await InForceOrFailAsync(provider, "2027-01-01")).Id);
        var unchanged = (await GetVersionAsync(provider, v1)).Value;
        Assert.Equal(("Published", Date("2026-10-05")), (unchanged.Status, unchanged.EffectiveFrom));
    }

    /// <summary>SV30, R18, R48: runsOnDate is the service's own period and weekday.</summary>
    [Theory]
    [InlineData("2026-10-10", "S1", false)] // a Saturday; S1 runs Monday–Friday
    [InlineData("2026-10-12", "S1", true)] // a Monday
    [InlineData("2026-10-10", "S2", true)] // S2 runs every day
    [InlineData("2026-11-02", "S5", false)] // a Monday after S5's effectiveTo 2026-10-31
    [InlineData("2026-10-26", "S5", true)] // a Monday before it
    public async Task GetInForce_ReportsRunsOnDatePerService(string date, string service, bool runs)
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var s5 = await CreateServiceOrFailAsync(provider, Command(net.Network, code: "S505", effectiveTo: "2026-10-31"));
        await PublishedVersionAsync(provider, "2026-10-05", [ValidS1(net.S1), ValidThreeStop(net.S2), ValidThreeStop(s5)]);
        var id = service switch { "S1" => net.S1, "S2" => net.S2, _ => s5 };

        var inForce = await InForceOrFailAsync(provider, date);

        Assert.Equal(3, inForce.Services.Count);
        Assert.Equal(["S101", "S202", "S505"], inForce.Services.Select(listed => listed.Code));
        Assert.Equal(runs, inForce.Services.Single(listed => listed.ServiceId == id).RunsOnDate);
    }

    /// <summary>SV32: after cancelling V3, V1 is in force again on V3's dates; V3 reads back as Cancelled.</summary>
    [Fact]
    public async Task GetInForce_AfterACancellation_ReturnsTheEarlierVersion()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var v1 = await PublishedVersionAsync(provider, "2026-10-05", [ValidS1(net.S1)]);
        var v3 = await PublishedVersionAsync(provider, "2026-11-01", [ValidS1(net.S1)]);
        Assert.Equal(v3, (await InForceOrFailAsync(provider, "2026-11-15")).Id);

        Assert.True((await CancelAsync(provider, v3)).IsSuccess);

        Assert.Equal(v1, (await InForceOrFailAsync(provider, "2026-11-15")).Id);
        Assert.Equal("Cancelled", (await GetVersionAsync(provider, v3)).Value.Status);
    }

    /// <summary>SV53: an empty version in force is 200 with no services, open-ended; cancelled, V1 is back.</summary>
    [Fact]
    public async Task GetInForce_WithAnEmptyVersion_ReturnsItWithNoServices()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var v1 = await PublishedVersionAsync(provider, "2026-10-05", [ValidS1(net.S1)]);
        var empty = await PublishedVersionAsync(provider, "2026-11-02", []);

        var before = await InForceOrFailAsync(provider, "2026-11-01");
        Assert.Equal(v1, before.Id);
        Assert.Equal([net.S1], before.Services.Select(listed => listed.ServiceId));
        var during = await InForceOrFailAsync(provider, "2026-11-02");
        Assert.Equal(empty, during.Id);
        Assert.Empty(during.Services);
        Assert.Equal(empty, (await InForceOrFailAsync(provider, "2027-06-01")).Id);

        Assert.True((await CancelAsync(provider, empty)).IsSuccess);
        var back = await InForceOrFailAsync(provider, "2026-11-02");
        Assert.Equal(v1, back.Id);
        Assert.True(back.Services.Single().RunsOnDate);
    }
}

using YCR.Application.Timetable.GetService;
using YCR.Application.Timetable.ListServices;
using YCR.Domain.Common;
using YCR.Domain.Timetable;
using static YCR.Application.Tests.Timetable.TimetableTestData;

namespace YCR.Application.Tests.Timetable;

/// <summary>F-004 reads (S2, S3, S7, S20, S26, S36, S38, S44; R30, R41) under <c>ycr_app</c>.</summary>
public sealed class ServiceQueryHandlerTests(SqlServerFixture fixture) : TimetableHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "service_query";

    /// <summary>S2, R30: stops in position order, with the route's and stations' current values.</summary>
    [Fact]
    public async Task GetService_WithKnownId_ReturnsStopsInOrderWithCurrentRouteAndStationValues()
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);
        var id = await CreateServiceOrFailAsync(provider, Command(network, "DEAB"));

        var result = await GetServiceAsync(provider, id);

        Assert.True(result.IsSuccess);
        var service = result.Value;
        Assert.Equal(id, service.Id);
        Assert.Equal("S101", service.Code);
        Assert.Equal("Circular", service.NameEn);
        Assert.Equal("မြို့ပတ်ရထား", service.NameMy);
        Assert.Equal("Forward", service.Direction);
        Assert.Equal(
            new ServiceRouteDto(network.Rc, "RC", "Circular Route", "မြို့ပတ်ရထားလမ်း", IsClosed: true, IsActive: true),
            service.Route);
        Assert.Equal(
            [.. "DEAB".Select((letter, index) =>
                new ServiceStopDto(index + 1, network[letter], $"S{letter}", $"Station S{letter}", "ဘူတာ", IsActive: true))],
            service.Stops);
        Assert.Equal(["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"], service.OperatingDays);
        Assert.Equal(new DateOnly(2026, 10, 5), service.EffectiveFrom);
        Assert.Null(service.EffectiveTo);
        Assert.False(service.NeverRuns);
        Assert.Equal(DefaultNowUtc, service.CreatedAtUtc);
        Assert.Null(service.WithdrawnAtUtc);
    }

    /// <summary>S7: the closing stop stays at the last position.</summary>
    [Fact]
    public async Task GetService_FullCircuit_KeepsTheClosingStopAtTheLastPosition()
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);
        var id = await CreateServiceOrFailAsync(provider, Command(network, "CEBC"));

        var stops = (await GetServiceAsync(provider, id)).Value.Stops;

        Assert.Equal(
            [(1, network['C']), (2, network['E']), (3, network['B']), (4, network['C'])],
            stops.Select(stop => (stop.Position, stop.StationId)));
    }

    /// <summary>S20, R16: deactivation changes no service row, and reads show the inactive flag.</summary>
    [Theory]
    [InlineData("route")]
    [InlineData("station")]
    public async Task GetService_AfterRouteOrStationDeactivated_ShowsInactiveFlagsAndRowsUnchanged(string deactivated)
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);
        var id = await CreateServiceOrFailAsync(provider, Command(network, "ACE"));
        var rowsBefore = await ServiceRowsAsync();

        if (deactivated == "route")
        {
            Assert.True((await DeactivateRouteAsync(provider, network.Rc)).IsSuccess);
        }
        else
        {
            await DeactivateStationAsync(provider, network['C']);
        }

        var service = (await GetServiceAsync(provider, id)).Value;

        Assert.Equal(deactivated != "route", service.Route.IsActive);
        Assert.Equal([true, deactivated != "station", true], service.Stops.Select(stop => stop.IsActive));
        Assert.Equal(rowsBefore, await ServiceRowsAsync());
    }

    /// <summary>S36, R41: stored dates as they are, withdrawnAtUtc set, neverRuns true.</summary>
    [Fact]
    public async Task GetService_ThatNeverRuns_ReturnsStoredDatesAndNeverRunsTrue()
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);
        var id = await CreateServiceOrFailAsync(provider, Command(network, effectiveFrom: "2026-11-01"));
        Assert.True((await WithdrawServiceAsync(provider, id, Date("2026-10-15"))).IsSuccess);

        var service = (await GetServiceAsync(provider, id)).Value;

        Assert.Equal(new DateOnly(2026, 11, 1), service.EffectiveFrom);
        Assert.Equal(new DateOnly(2026, 10, 14), service.EffectiveTo);
        Assert.Equal(DefaultNowUtc, service.WithdrawnAtUtc);
        Assert.True(service.NeverRuns);
    }

    [Fact]
    public async Task GetService_WithUnknownId_ReturnsNotFound()
    {
        await using var provider = BuildServiceProvider();

        var result = await GetServiceAsync(provider, Guid.Parse("0199b3a0-0000-7000-8000-0000000000eb"));

        AssertFailure(result, TimetableErrors.ServiceNotFound);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    /// <summary>
    /// S3, S26: the envelope, ordered by code then effective-from, withdrawn services included; two
    /// services share a code with adjacent periods.
    /// </summary>
    [Fact]
    public async Task ListServices_ReturnsPagedEnvelopeOrderedByCodeThenEffectiveFromWithWithdrawn()
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);
        var old = await CreateServiceOrFailAsync(provider, Command(network, code: "S101"));
        Assert.True((await WithdrawServiceAsync(provider, old, Date("2027-01-01"))).IsSuccess);
        var replacement = await CreateServiceOrFailAsync(
            provider, Command(network, "SRP", code: "S101", routeId: network.Ro, direction: "Reverse", effectiveFrom: "2027-01-01"));
        var other = await CreateServiceOrFailAsync(provider, Command(network, "QR", code: "A10", routeId: network.Ro));
        var last = await CreateServiceOrFailAsync(provider, Command(network, "CB", code: "Z9"));

        var page = (await ListServicesAsync(provider, new ListServicesQuery(Page: 1, PageSize: 50))).Value;

        Assert.Equal(1, page.Page);
        Assert.Equal(50, page.PageSize);
        Assert.Equal(4, page.TotalCount);
        Assert.Equal([other, old, replacement, last], page.Items.Select(item => item.Id));

        var withdrawn = page.Items[1];
        Assert.Equal(new DateOnly(2026, 12, 31), withdrawn.EffectiveTo);
        Assert.Equal(DefaultNowUtc, withdrawn.WithdrawnAtUtc);
        Assert.False(withdrawn.NeverRuns);
        Assert.Equal("RC", withdrawn.RouteCode);
        Assert.Equal(network.Rc, withdrawn.RouteId);
        Assert.Equal(3, withdrawn.StopCount);
        Assert.Equal("Forward", withdrawn.Direction);
        Assert.Equal(["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"], withdrawn.OperatingDays);

        var reversed = page.Items[2];
        Assert.Equal("RO", reversed.RouteCode);
        Assert.Equal("Reverse", reversed.Direction);
        Assert.Equal(new DateOnly(2027, 1, 1), reversed.EffectiveFrom);
        Assert.Null(reversed.EffectiveTo);

        var second = (await ListServicesAsync(provider, new ListServicesQuery(Page: 2, PageSize: 3))).Value;
        Assert.Equal(4, second.TotalCount);
        Assert.Equal([last], second.Items.Select(item => item.Id));
    }

    /// <summary>S3: <c>?routeId=</c> returns only that route's services; an unknown id an empty page.</summary>
    [Fact]
    public async Task ListServices_FilteredByRoute_ReturnsOnlyThatRoutesServices()
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);
        var onRc = await CreateServiceOrFailAsync(provider, Command(network, code: "S101"));
        await CreateServiceOrFailAsync(provider, Command(network, "QR", code: "S202", routeId: network.Ro));

        var filtered = (await ListServicesAsync(provider, new ListServicesQuery(RouteId: network.Rc))).Value;
        var unknown = (await ListServicesAsync(
            provider, new ListServicesQuery(RouteId: Guid.Parse("0199b3a0-0000-7000-8000-0000000000ea")))).Value;

        Assert.Equal(1, filtered.TotalCount);
        Assert.Equal([onRc], filtered.Items.Select(item => item.Id));
        Assert.Equal(0, unknown.TotalCount);
        Assert.Empty(unknown.Items);
    }

    /// <summary>S44.</summary>
    [Fact]
    public async Task ListServices_WithPageSizeAbove200_ReturnsInvalidPageRequest()
    {
        await using var provider = BuildServiceProvider();

        var result = await ListServicesAsync(provider, new ListServicesQuery(PageSize: 201));

        AssertFailure(result, TimetableErrors.InvalidPageRequest);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    private Task<string> ServiceRowsAsync() =>
        ScalarAsync<string>(
            """
            SELECT STRING_AGG(CONCAT(s.[Code], ':', LOWER(CONVERT(nvarchar(36), s.[RouteId])), ':', s.[Direction], ':',
                ISNULL(CONVERT(nvarchar(10), s.[EffectiveTo], 23), 'null'), ':', stops.[Count]), '|')
            FROM [timetable].[Services] AS s
            CROSS APPLY (SELECT COUNT(*) AS [Count] FROM [timetable].[ServiceStops] AS t WHERE t.[ServiceId] = s.[Id]) AS stops;
            """)!;
}

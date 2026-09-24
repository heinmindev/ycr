using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Common.Pagination;
using YCR.Application.Network.GetRoute;
using YCR.Application.Network.ListRoutes;
using YCR.Domain.Common;

namespace YCR.Application.Tests.Network;

/// <summary>F-003 S2, S3, S12, S13, S17, S23 and S28 against real SQL Server under <c>ycr_app</c>.</summary>
public sealed class RouteQueryHandlerTests(SqlServerFixture fixture) : RouteHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "route_queries";

    /// <summary>S2 / R24: stations in position order, each with its current code, names and flag.</summary>
    [Fact]
    public async Task GetRoute_WithKnownId_ReturnsStationsInPositionOrderWithCurrentStationData()
    {
        await using var provider = BuildRouteProvider();
        var ids = await CreateStationsAsync(provider, "CCC", "AAA", "BBB");
        var routeId = await CreateRouteOrFailAsync(provider, "R1", isClosed: false, ids);

        var route = (await GetAsync(provider, routeId)).Value;

        Assert.Equal(routeId, route.Id);
        Assert.Equal("R1", route.Code);
        Assert.Equal("Circular Route", route.NameEn);
        Assert.Equal("မြို့ပတ်ရထားလမ်း", route.NameMy);
        Assert.False(route.IsClosed);
        Assert.True(route.IsActive);
        Assert.Equal(Clock.GetUtcNow(), route.CreatedAtUtc);

        // Position order, not code order and not insertion order of the stations themselves.
        Assert.Equal([1, 2, 3], route.Stations.Select(station => station.Position));
        Assert.Equal(ids, route.Stations.Select(station => station.StationId));
        Assert.Equal(["CCC", "AAA", "BBB"], route.Stations.Select(station => station.Code));
        Assert.All(route.Stations, station => Assert.Equal($"Station {station.Code}", station.NameEn));
        Assert.All(route.Stations, station => Assert.Equal("ဘူတာ", station.NameMy));
        Assert.All(route.Stations, station => Assert.True(station.IsActive));
    }

    [Fact]
    public async Task GetRoute_ActiveRoute_HasNullDeactivatedAt()
    {
        await using var provider = BuildRouteProvider();
        var routeId = await CreateRouteOrFailAsync(provider, "R1", isClosed: false, await CreateStationsAsync(provider, "AAA", "BBB"));

        var active = (await GetAsync(provider, routeId)).Value;
        Assert.True(active.IsActive);
        Assert.Null(active.DeactivatedAtUtc);

        Clock.Advance(TimeSpan.FromMinutes(30));
        Assert.True((await DeactivateRouteAsync(provider, routeId)).IsSuccess);

        var inactive = (await GetAsync(provider, routeId)).Value;
        Assert.False(inactive.IsActive);
        Assert.Equal(Clock.GetUtcNow(), inactive.DeactivatedAtUtc);
        Assert.Equal(2, inactive.Stations.Count);
    }

    /// <summary>S17 / R12: a deactivated station stays at its position and reads back inactive.</summary>
    [Fact]
    public async Task GetRoute_AfterStationDeactivated_ShowsStationInactiveAtSamePosition()
    {
        await using var provider = BuildRouteProvider();
        var ids = await CreateStationsAsync(provider, "AAA", "BBB", "CCC");
        var routeId = await CreateRouteOrFailAsync(provider, "R1", isClosed: false, ids);

        await DeactivateStationAsync(provider, ids[1]);

        var route = (await GetAsync(provider, routeId)).Value;
        Assert.True(route.IsActive);
        Assert.Equal(ids, route.Stations.Select(station => station.StationId));
        Assert.Equal([true, false, true], route.Stations.Select(station => station.IsActive));
        Assert.Equal(2, route.Stations.Single(station => station.StationId == ids[1]).Position);
    }

    /// <summary>S28 / R6: a closed route reads back with its flag and its first station only once.</summary>
    [Fact]
    public async Task GetRoute_ClosedRoute_ReturnsIsClosedAndThreeStationsFirstOnlyAtPositionOne()
    {
        await using var provider = BuildRouteProvider();
        var ids = await CreateStationsAsync(provider, "AAA", "BBB", "CCC");
        var routeId = await CreateRouteOrFailAsync(provider, "LOOP", isClosed: true, ids);

        var route = (await GetAsync(provider, routeId)).Value;

        Assert.True(route.IsClosed);
        Assert.Equal(3, route.Stations.Count);
        Assert.Equal(1, Assert.Single(route.Stations, station => station.StationId == ids[0]).Position);
    }

    [Fact]
    public async Task GetRoute_WithUnknownId_ReturnsNotFound()
    {
        await using var provider = BuildRouteProvider();

        var result = await GetAsync(provider, Guid.CreateVersion7());

        Assert.True(result.IsFailure);
        Assert.Equal("Network.RouteNotFound", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    /// <summary>S3: ordered by code, inactive routes listed, with a station count per route.</summary>
    [Fact]
    public async Task ListRoutes_WithThreeRoutesOneInactive_ReturnsPagedEnvelopeOrderedByCode()
    {
        await using var provider = BuildRouteProvider();
        var ids = await CreateStationsAsync(provider, "AAA", "BBB", "CCC", "DDD");
        await CreateRouteOrFailAsync(provider, "ZED", isClosed: false, [ids[0], ids[1]]);
        var inactive = await CreateRouteOrFailAsync(provider, "MID", isClosed: true, [ids[0], ids[1], ids[2]]);
        await CreateRouteOrFailAsync(provider, "ABC", isClosed: false, ids);
        Assert.True((await DeactivateRouteAsync(provider, inactive)).IsSuccess);

        var page = (await ListAsync(provider, new ListRoutesQuery(Page: 1, PageSize: 50))).Value;

        Assert.Equal(1, page.Page);
        Assert.Equal(50, page.PageSize);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(["ABC", "MID", "ZED"], page.Items.Select(route => route.Code));
        Assert.Equal([4, 3, 2], page.Items.Select(route => route.StationCount));
        Assert.Equal([true, false, true], page.Items.Select(route => route.IsActive));
        Assert.Equal([false, true, false], page.Items.Select(route => route.IsClosed));
        Assert.Null(page.Items[0].DeactivatedAtUtc);
        Assert.Equal(Clock.GetUtcNow(), page.Items[1].DeactivatedAtUtc);

        var second = (await ListAsync(provider, new ListRoutesQuery(Page: 2, PageSize: 2))).Value;
        Assert.Equal(3, second.TotalCount);
        Assert.Equal(["ZED"], second.Items.Select(route => route.Code));
    }

    [Fact]
    public async Task ListRoutes_WithPageSizeAbove200_ReturnsInvalidPageRequest()
    {
        await using var provider = BuildRouteProvider();

        var result = await ListAsync(provider, new ListRoutesQuery(Page: 1, PageSize: Paging.MaxPageSize + 1));

        Assert.True(result.IsFailure);
        Assert.Equal("Network.InvalidPageRequest", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    private static async Task<Result<RouteDto>> GetAsync(IServiceProvider provider, Guid routeId)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<GetRouteHandler>()
            .Handle(new GetRouteQuery(routeId), CancellationToken);
    }

    private static async Task<Result<PagedResult<RouteSummaryDto>>> ListAsync(IServiceProvider provider, ListRoutesQuery query)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ListRoutesHandler>().Handle(query, CancellationToken);
    }
}

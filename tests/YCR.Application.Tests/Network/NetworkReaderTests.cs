using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Network.Contracts;
using YCR.Domain.Common;
using YCR.Domain.Network;
using YCR.Infrastructure.Persistence;

namespace YCR.Application.Tests.Network;

/// <summary>
/// The Network contract's implementation (ADR-0025 items 2-3; F-004 plan P14, R28, R30), resolved
/// from the real composition and run as <c>ycr_app</c>.
/// </summary>
public sealed class NetworkReaderTests(SqlServerFixture fixture) : RouteHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "network_reader";

    [Fact]
    public async Task GetRoute_ReturnsStationsInPositionOrderWithCurrentValues()
    {
        await using var provider = BuildRouteProvider();
        var ids = await CreateStationsAsync(provider, "AAA", "BBB", "CCC");
        var routeId = await CreateRouteOrFailAsync(provider, "LOOP1", isClosed: true, [ids[2], ids[0], ids[1]]);
        await DeactivateStationAsync(provider, ids[1]);

        var route = await ReadRouteAsync(provider, routeId);

        Assert.NotNull(route);
        Assert.Equal(routeId, route.Id);
        Assert.Equal("LOOP1", route.Code);
        Assert.Equal("Circular Route", route.NameEn);
        Assert.Equal("မြို့ပတ်ရထားလမ်း", route.NameMy);
        Assert.True(route.IsClosed);
        Assert.True(route.IsActive);
        Assert.Equal(
            [
                new RouteStationReference(1, ids[2], "CCC", "Station CCC", "ဘူတာ", true),
                new RouteStationReference(2, ids[0], "AAA", "Station AAA", "ဘူတာ", true),
                new RouteStationReference(3, ids[1], "BBB", "Station BBB", "ဘူတာ", false)
            ],
            route.Stations);

        // Current values: a deactivation after the first read shows on the next one.
        Assert.True((await DeactivateRouteAsync(provider, routeId)).IsSuccess);
        Assert.False((await ReadRouteAsync(provider, routeId))!.IsActive);
    }

    [Fact]
    public async Task GetRoute_WithUnknownId_ReturnsNull()
    {
        await using var provider = BuildRouteProvider();

        Assert.Null(await ReadRouteAsync(provider, Guid.Parse("0199b3a0-0000-7000-8000-0000000000ff")));
    }

    [Fact]
    public async Task GetRouteSummariesAndStations_OmitUnknownIds()
    {
        await using var provider = BuildRouteProvider();
        var ids = await CreateStationsAsync(provider, "AAA", "BBB");
        var routeId = await CreateRouteOrFailAsync(provider, "R1", isClosed: false, ids);
        var unknown = Guid.Parse("0199b3a0-0000-7000-8000-0000000000fe");

        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<INetworkReader>();

        var routes = await reader.GetRouteSummariesAsync([routeId, unknown, routeId], CancellationToken);
        var stations = await reader.GetStationsAsync([ids[1], unknown], CancellationToken);

        Assert.Equal(
            new RouteSummaryReference(routeId, "R1", "Circular Route", "မြို့ပတ်ရထားလမ်း", false, true),
            Assert.Single(routes).Value);
        Assert.Equal(new StationReference(ids[1], "BBB", "Station BBB", "ဘူတာ", true), Assert.Single(stations).Value);
        Assert.Empty(await reader.GetRouteSummariesAsync([], CancellationToken));
        Assert.Empty(await reader.GetStationsAsync([], CancellationToken));
    }

    /// <summary>
    /// ADR-0025 item 2: the reader runs on the caller's scoped context, so inside the caller's
    /// transaction it sees that transaction's uncommitted rows, and it tracks nothing.
    /// </summary>
    [Fact]
    public async Task Reads_TrackNothingAndSeeTheCallersOpenTransaction()
    {
        await using var provider = BuildRouteProvider();
        var ids = await CreateStationsAsync(provider, "AAA", "BBB");
        var routeId = await CreateRouteOrFailAsync(provider, "R1", isClosed: false, ids);
        var uncommittedId = Guid.Parse("0199b3a0-0000-7000-8000-0000000000fd");

        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<YcrDbContext>();
            var reader = scope.ServiceProvider.GetRequiredService<INetworkReader>();

            await using var transaction = await context.Database.BeginTransactionAsync(CancellationToken);
            context.Stations.Add(Station.Create(
                uncommittedId,
                StationCode.Create("NEW").Value,
                BilingualName.Create("New", "နယူး", NetworkErrors.InvalidStationName).Value,
                Clock.GetUtcNow()));
            await context.SaveChangesAsync(CancellationToken);
            var trackedBefore = context.ChangeTracker.Entries().Count();

            var stations = await reader.GetStationsAsync([uncommittedId, ids[0]], CancellationToken);
            var route = await reader.GetRouteAsync(routeId, CancellationToken);
            var routes = await reader.GetRouteSummariesAsync([routeId], CancellationToken);

            Assert.Equal("NEW", stations[uncommittedId].Code);
            Assert.Equal(2, stations.Count);
            Assert.NotNull(route);
            Assert.Single(routes);
            Assert.Equal(trackedBefore, context.ChangeTracker.Entries().Count());

            await transaction.RollbackAsync(CancellationToken);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<INetworkReader>();
            Assert.Empty(await reader.GetStationsAsync([uncommittedId], CancellationToken));
        }
    }

    private static async Task<RouteReference?> ReadRouteAsync(IServiceProvider provider, Guid routeId)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<INetworkReader>().GetRouteAsync(routeId, CancellationToken);
    }
}

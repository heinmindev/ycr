using Microsoft.EntityFrameworkCore;
using YCR.Application.Network.Contracts;

namespace YCR.Application.Network;

/// <summary>
/// The Network module's implementation of <see cref="INetworkReader"/> (ADR-0025 item 2; F-004 plan
/// P14).
/// </summary>
/// <remarks>
/// <see langword="internal"/> and outside <c>Contracts</c>, so no other module can name it; the
/// architecture rules would reject a Timetable type that did. Every query is <c>AsNoTracking</c>
/// and projects scalars, taking whole value objects (the <c>StationProjection</c> pattern) and
/// unwrapping them after materialisation. The id-list reads use <c>Contains</c>, one JSON parameter
/// (F-003 plan O5).
/// </remarks>
internal sealed class NetworkReader(INetworkDbContext db) : INetworkReader
{
    public async Task<RouteReference?> GetRouteAsync(Guid routeId, CancellationToken cancellationToken)
    {
        var route = await db.Routes
            .AsNoTracking()
            .Where(candidate => candidate.Id == routeId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Code,
                candidate.Name.En,
                candidate.Name.My,
                candidate.IsClosed,
                candidate.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (route is null)
        {
            return null;
        }

        // The same statement as GetRouteHandler's (F-003 plan V2): the sequence joined to the
        // stations for their current values, in position order.
        var stations = await db.Routes
            .AsNoTracking()
            .Where(candidate => candidate.Id == routeId)
            .SelectMany(candidate => candidate.Stations)
            .Join(
                db.Stations,
                routeStation => routeStation.StationId,
                station => station.Id,
                (routeStation, station) => new
                {
                    routeStation.Position,
                    station.Id,
                    station.Code,
                    station.Name.En,
                    station.Name.My,
                    station.IsActive
                })
            .OrderBy(row => row.Position)
            .ToListAsync(cancellationToken);

        return new RouteReference(
            route.Id,
            route.Code.Value,
            route.En,
            route.My,
            route.IsClosed,
            route.IsActive,
            [.. stations.Select(row => new RouteStationReference(
                row.Position,
                row.Id,
                row.Code.Value,
                row.En,
                row.My,
                row.IsActive))]);
    }

    public async Task<IReadOnlyDictionary<Guid, RouteSummaryReference>> GetRouteSummariesAsync(
        IReadOnlyCollection<Guid> routeIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(routeIds);
        if (routeIds.Count == 0)
        {
            return new Dictionary<Guid, RouteSummaryReference>();
        }

        var ids = routeIds.Distinct().ToList();
        var rows = await db.Routes
            .AsNoTracking()
            .Where(route => ids.Contains(route.Id))
            .Select(route => new
            {
                route.Id,
                route.Code,
                route.Name.En,
                route.Name.My,
                route.IsClosed,
                route.IsActive
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => row.Id,
            row => new RouteSummaryReference(row.Id, row.Code.Value, row.En, row.My, row.IsClosed, row.IsActive));
    }

    public async Task<IReadOnlyDictionary<Guid, StationReference>> GetStationsAsync(
        IReadOnlyCollection<Guid> stationIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stationIds);
        if (stationIds.Count == 0)
        {
            return new Dictionary<Guid, StationReference>();
        }

        var ids = stationIds.Distinct().ToList();
        var rows = await db.Stations
            .AsNoTracking()
            .Where(station => ids.Contains(station.Id))
            .Select(station => new
            {
                station.Id,
                station.Code,
                station.Name.En,
                station.Name.My,
                station.IsActive
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => row.Id,
            row => new StationReference(row.Id, row.Code.Value, row.En, row.My, row.IsActive));
    }
}

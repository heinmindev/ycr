using Microsoft.EntityFrameworkCore;
using YCR.Domain.Common;
using YCR.Domain.Network;

namespace YCR.Application.Network.GetRoute;

/// <summary>Reads one route with its stations (F-003 S2, S12, S17, S28).</summary>
/// <remarks>
/// Two <c>AsNoTracking</c> projections (plan P11): the route row, then its sequence joined to
/// <c>network.Stations</c> for each station's current code, names and active flag (R24), ordered by
/// position. The second is one SQL statement (plan-V2, asserted by <c>ListRoutesSqlTests</c>).
/// </remarks>
public sealed class GetRouteHandler(INetworkDbContext db)
{
    public async Task<Result<RouteDto>> Handle(GetRouteQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var route = await db.Routes
            .AsNoTracking()
            .Where(candidate => candidate.Id == query.RouteId)
            .Select(candidate => new RouteProjection(
                candidate.Id,
                candidate.Code,
                candidate.Name.En,
                candidate.Name.My,
                candidate.IsClosed,
                candidate.IsActive,
                candidate.CreatedAtUtc,
                candidate.DeactivatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (route is null)
        {
            return NetworkErrors.RouteNotFound;
        }

        var stations = await db.Routes
            .AsNoTracking()
            .Where(candidate => candidate.Id == query.RouteId)
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

        return route.ToDto(
            [.. stations.Select(row => new RouteStationDto(
                row.Position,
                row.Id,
                row.Code.Value,
                row.En,
                row.My,
                row.IsActive))]);
    }
}

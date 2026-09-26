using Microsoft.EntityFrameworkCore;
using YCR.Application.Network.Contracts;
using YCR.Domain.Common;
using YCR.Domain.Timetable;

namespace YCR.Application.Timetable.GetService;

/// <summary>Reads one service with its stops (F-004 S2, S7, S20, S36, S38; R30).</summary>
/// <remarks>
/// Two <c>AsNoTracking</c> projections (plan P17): the service row, then its stops in position
/// order. The route's and each stop station's <em>current</em> code, names and flags come from the
/// Network contract (R30, ADR-0025), since a service row stores only ids.
/// </remarks>
public sealed class GetServiceHandler(ITimetableDbContext db, INetworkReader network)
{
    public async Task<Result<ServiceDto>> Handle(GetServiceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var service = await db.Services
            .AsNoTracking()
            .Where(candidate => candidate.Id == query.ServiceId)
            .Select(candidate => new ServiceProjection(
                candidate.Id,
                candidate.Code,
                candidate.Name.En,
                candidate.Name.My,
                candidate.RouteId,
                candidate.Direction,
                candidate.OperatingDays.RunsOnMonday,
                candidate.OperatingDays.RunsOnTuesday,
                candidate.OperatingDays.RunsOnWednesday,
                candidate.OperatingDays.RunsOnThursday,
                candidate.OperatingDays.RunsOnFriday,
                candidate.OperatingDays.RunsOnSaturday,
                candidate.OperatingDays.RunsOnSunday,
                candidate.EffectiveFrom,
                candidate.EffectiveTo,
                candidate.CreatedAtUtc,
                candidate.WithdrawnAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (service is null)
        {
            return TimetableErrors.ServiceNotFound;
        }

        var stops = await db.Services
            .AsNoTracking()
            .Where(candidate => candidate.Id == query.ServiceId)
            .SelectMany(candidate => candidate.Stops)
            .OrderBy(stop => stop.Position)
            .Select(stop => new { stop.Position, stop.StationId })
            .ToListAsync(cancellationToken);

        var routes = await network.GetRouteSummariesAsync([service.RouteId], cancellationToken);
        var stations = await network.GetStationsAsync(
            [.. stops.Select(stop => stop.StationId).Distinct()], cancellationToken);

        // The foreign keys guarantee both exist (R29); a miss is a broken database, not a 404.
        var route = routes.TryGetValue(service.RouteId, out var found)
            ? found
            : throw new InvalidOperationException($"Service '{service.Id}' names route '{service.RouteId}', which does not exist.");

        return service.ToDto(
            new ServiceRouteDto(route.Id, route.Code, route.NameEn, route.NameMy, route.IsClosed, route.IsActive),
            [.. stops.Select(stop =>
            {
                var station = stations[stop.StationId];
                return new ServiceStopDto(stop.Position, station.Id, station.Code, station.NameEn, station.NameMy, station.IsActive);
            })]);
    }
}

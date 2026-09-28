using Microsoft.EntityFrameworkCore;
using YCR.Application.Network.Contracts;
using YCR.Domain.Common;
using YCR.Domain.Timetable;

namespace YCR.Application.Timetable.GetScheduleServiceTimes;

/// <summary>
/// Reads one listed service's stop times in a version (F-005 SV2; plan P13).
/// </summary>
/// <remarks>
/// Order: the version (<c>404 ScheduleVersionNotFound</c>); its stop times for the service, a
/// <c>PK_ScheduleStopTimes</c> range (none → <c>404 ScheduleServiceNotInVersion</c>: every listed
/// service has at least two times); the service's code and its stops' station ids; then station
/// codes and names through the Network contract only (R43).
/// </remarks>
public sealed class GetScheduleServiceTimesHandler(ITimetableDbContext db, INetworkReader network)
{
    public async Task<Result<ScheduleServiceTimesDto>> Handle(GetScheduleServiceTimesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var exists = await db.ScheduleVersions
            .AsNoTracking()
            .AnyAsync(version => version.Id == query.ScheduleVersionId, cancellationToken);
        if (!exists)
        {
            return TimetableErrors.ScheduleVersionNotFound;
        }

        var times = await db.ScheduleVersions
            .AsNoTracking()
            .Where(version => version.Id == query.ScheduleVersionId)
            .SelectMany(version => version.Services)
            .Where(entry => entry.ServiceId == query.ServiceId)
            .SelectMany(entry => entry.StopTimes)
            .Select(stop => new { stop.Position, stop.Arrival, stop.Departure })
            .ToListAsync(cancellationToken);
        if (times.Count == 0)
        {
            return TimetableErrors.ScheduleServiceNotInVersion;
        }

        var service = await db.Services
            .AsNoTracking()
            .Where(candidate => candidate.Id == query.ServiceId)
            .Select(candidate => new
            {
                candidate.Code,
                Stops = candidate.Stops.Select(stop => new { stop.Position, stop.StationId }).ToList()
            })
            .SingleAsync(cancellationToken);
        var stationIdByPosition = service.Stops.ToDictionary(stop => stop.Position, stop => stop.StationId);
        var stations = await network.GetStationsAsync([.. stationIdByPosition.Values.Distinct()], cancellationToken);

        return new ScheduleServiceTimesDto(
            query.ScheduleVersionId,
            query.ServiceId,
            service.Code.Value,
            [.. times
                .OrderBy(stop => stop.Position)
                .Select(stop =>
                {
                    var station = stations[stationIdByPosition[stop.Position]];
                    return new ScheduleStopTimeDto(
                        stop.Position,
                        station.Id,
                        station.Code,
                        station.NameEn,
                        station.NameMy,
                        ScheduleReadMapping.TimeText(stop.Arrival),
                        ScheduleReadMapping.TimeText(stop.Departure));
                })]);
    }
}

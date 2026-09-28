using Microsoft.EntityFrameworkCore;
using YCR.Domain.Common;
using YCR.Domain.Timetable;

namespace YCR.Application.Timetable.GetScheduleVersion;

/// <summary>
/// Reads one timetable version with its listed services (F-005 SV2; plan P13).
/// </summary>
/// <remarks>
/// Projects rows and never materialises the aggregate (its stop times can be 10,000 rows): the
/// header by <c>PK_ScheduleVersions</c>, then its entries joined to <c>Services</c> for current
/// code, names, direction, stop count and period. Reads take no lock (spec §5).
/// </remarks>
public sealed class GetScheduleVersionHandler(ITimetableDbContext db)
{
    public async Task<Result<ScheduleVersionDto>> Handle(GetScheduleVersionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var header = await db.ScheduleVersions
            .AsNoTracking()
            .Where(version => version.Id == query.ScheduleVersionId)
            .Select(version => new
            {
                version.Id,
                version.Number,
                version.Name.En,
                version.Name.My,
                version.EffectiveFrom,
                version.Status,
                version.CreatedAtUtc,
                version.PublishedAtUtc,
                version.DiscardedAtUtc,
                version.CancelledAtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (header is null)
        {
            return TimetableErrors.ScheduleVersionNotFound;
        }

        var services = await db.ScheduleVersions
            .AsNoTracking()
            .Where(version => version.Id == query.ScheduleVersionId)
            .SelectMany(version => version.Services)
            .Join(db.Services, entry => entry.ServiceId, service => service.Id, (entry, service) => new
            {
                service.Id,
                service.Code,
                service.Name.En,
                service.Name.My,
                service.Direction,
                StopCount = service.Stops.Count(),
                service.EffectiveFrom,
                service.EffectiveTo
            })
            .OrderBy(service => service.Code)
            .ThenBy(service => service.Id)
            .ToListAsync(cancellationToken);

        return new ScheduleVersionDto(
            header.Id,
            header.Number,
            header.En,
            header.My,
            header.EffectiveFrom,
            ScheduleReadMapping.StatusName(header.Status),
            header.CreatedAtUtc,
            header.PublishedAtUtc,
            header.DiscardedAtUtc,
            header.CancelledAtUtc,
            [.. services.Select(service => new ScheduleVersionServiceDto(
                service.Id,
                service.Code.Value,
                service.En,
                service.My,
                ServiceReadMapping.DirectionName(service.Direction),
                service.StopCount,
                service.EffectiveFrom,
                service.EffectiveTo,
                ServiceReadMapping.NeverRuns(service.EffectiveFrom, service.EffectiveTo)))]);
    }
}

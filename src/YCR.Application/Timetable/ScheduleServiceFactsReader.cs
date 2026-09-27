using Microsoft.EntityFrameworkCore;
using YCR.Domain.Timetable;

namespace YCR.Application.Timetable;

/// <summary>
/// The one read of <see cref="ScheduleServiceFacts"/> for create and publish, under the
/// Timetable-wide lock (F-005 plan P3, P6, P7): <c>PK_Services</c> seeks and the stop count.
/// </summary>
internal static class ScheduleServiceFactsReader
{
    public static async Task<List<ScheduleServiceFacts>> LoadAsync(
        ITimetableDbContext db,
        IReadOnlyCollection<Guid> serviceIds,
        CancellationToken cancellationToken)
    {
        if (serviceIds.Count == 0)
        {
            return [];
        }

        var rows = await db.Services
            .AsNoTracking()
            .Where(service => serviceIds.Contains(service.Id))
            .Select(service => new
            {
                service.Id,
                service.Code,
                service.EffectiveFrom,
                service.EffectiveTo,
                StopCount = service.Stops.Count
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => new ScheduleServiceFacts(row.Id, row.Code.Value, row.EffectiveFrom, row.EffectiveTo, row.StopCount))];
    }
}

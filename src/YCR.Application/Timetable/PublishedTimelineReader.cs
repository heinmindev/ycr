using Microsoft.EntityFrameworkCore;
using YCR.Domain.Timetable;

namespace YCR.Application.Timetable;

/// <summary>
/// Reads the published versions into the domain's <see cref="PublishedTimeline"/> — the one home
/// of "in force" and "applies" (F-005 plan P11). A covered scan of the filtered index
/// <c>UX_ScheduleVersions_EffectiveFrom_Published</c> (plan O3, O4); drafts, discarded and cancelled
/// versions are never read, so they never apply and never refuse a withdrawal (R19, R34).
/// </summary>
internal static class PublishedTimelineReader
{
    /// <summary>
    /// The coverage of one service for the withdrawal guard (R19): the published timeline and the
    /// published versions that list the service. Called under the Timetable-wide lock.
    /// </summary>
    public static async Task<ServiceScheduleCoverage> LoadCoverageAsync(
        ITimetableDbContext db,
        Guid serviceId,
        CancellationToken cancellationToken)
    {
        var published = await db.ScheduleVersions
            .AsNoTracking()
            .Where(version => version.Status == ScheduleVersionStatus.Published)
            .Select(version => new
            {
                version.Id,
                version.EffectiveFrom,
                Lists = version.Services.Any(entry => entry.ServiceId == serviceId)
            })
            .ToListAsync(cancellationToken);

        return new ServiceScheduleCoverage(
            PublishedTimeline.From(published.Select(version => (version.Id, version.EffectiveFrom))),
            published.Where(version => version.Lists).Select(version => version.Id).ToHashSet());
    }
}

using Microsoft.EntityFrameworkCore;
using YCR.Domain.Common;
using YCR.Domain.Timetable;

namespace YCR.Application.Timetable.GetScheduleVersionInForce;

/// <summary>
/// Reads the version in force on a date, with whether each listed service runs on it (F-005 SV27-SV30,
/// SV32, SV53, SV54; R18, R21, R48; plan §The in-force read).
/// </summary>
/// <remarks>
/// <para>
/// 1. The published timeline (<see cref="PublishedTimelineReader"/>, the filtered-index scan) and
/// <see cref="PublishedTimeline.InForceOn"/> — none before the first published start date →
/// <c>404 Timetable.ScheduleVersionNotInForce</c> (R21). An empty version in force is <c>200</c>
/// with no services (SV53). 2. The header by primary key. 3. The listed services with their period
/// and weekdays; <c>runsOnDate</c> is <see cref="ServiceRunningDay.RunsOn"/> (R18: the version in
/// force lists it, the date is in its period and on one of its weekdays; holidays are not
/// considered, OQ47).
/// </para>
/// <para>
/// Reads take no lock (spec §5): a publish or cancel in flight is seen whole or not at all (one save,
/// one commit).
/// </para>
/// </remarks>
public sealed class GetScheduleVersionInForceHandler(ITimetableDbContext db)
{
    public async Task<Result<ScheduleVersionInForceDto>> Handle(GetScheduleVersionInForceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var timeline = await PublishedTimelineReader.LoadTimelineAsync(db, cancellationToken);
        if (timeline.InForceOn(query.Date) is not { } versionId)
        {
            return TimetableErrors.ScheduleVersionNotInForce;
        }

        var header = await db.ScheduleVersions
            .AsNoTracking()
            .Where(version => version.Id == versionId)
            .Select(version => new
            {
                version.Number,
                version.Name.En,
                version.Name.My,
                version.EffectiveFrom,
                version.PublishedAtUtc
            })
            .SingleAsync(cancellationToken);

        var services = await db.ScheduleVersions
            .AsNoTracking()
            .Where(version => version.Id == versionId)
            .SelectMany(version => version.Services)
            .Join(db.Services, entry => entry.ServiceId, service => service.Id, (entry, service) => new
            {
                service.Id,
                service.Code,
                service.EffectiveFrom,
                service.EffectiveTo,
                service.OperatingDays.RunsOnMonday,
                service.OperatingDays.RunsOnTuesday,
                service.OperatingDays.RunsOnWednesday,
                service.OperatingDays.RunsOnThursday,
                service.OperatingDays.RunsOnFriday,
                service.OperatingDays.RunsOnSaturday,
                service.OperatingDays.RunsOnSunday
            })
            .OrderBy(service => service.Code)
            .ThenBy(service => service.Id)
            .ToListAsync(cancellationToken);

        return new ScheduleVersionInForceDto(
            query.Date,
            versionId,
            header.Number,
            header.En,
            header.My,
            header.EffectiveFrom,
            header.PublishedAtUtc ?? throw new InvalidOperationException("A published version has a publication instant (CK_ScheduleVersions_StatusInstants)."),
            [.. services.Select(service => new ScheduleServiceRunningDto(
                service.Id,
                service.Code.Value,
                ServiceRunningDay.RunsOn(
                    query.Date,
                    service.EffectiveFrom,
                    service.EffectiveTo,
                    Days(
                        service.RunsOnMonday, service.RunsOnTuesday, service.RunsOnWednesday, service.RunsOnThursday,
                        service.RunsOnFriday, service.RunsOnSaturday, service.RunsOnSunday))))]);
    }

    /// <summary>The stored weekday bits as <see cref="OperatingDays"/>; <c>CK_Services_OperatingDays</c> guarantees one.</summary>
    private static OperatingDays Days(bool monday, bool tuesday, bool wednesday, bool thursday, bool friday, bool saturday, bool sunday)
    {
        var days = new List<DayOfWeek>(7);
        if (monday) days.Add(DayOfWeek.Monday);
        if (tuesday) days.Add(DayOfWeek.Tuesday);
        if (wednesday) days.Add(DayOfWeek.Wednesday);
        if (thursday) days.Add(DayOfWeek.Thursday);
        if (friday) days.Add(DayOfWeek.Friday);
        if (saturday) days.Add(DayOfWeek.Saturday);
        if (sunday) days.Add(DayOfWeek.Sunday);
        return OperatingDays.Create(days);
    }
}

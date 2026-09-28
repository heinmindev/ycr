using YCR.Domain.Timetable;

namespace YCR.Application.Timetable;

/// <summary>
/// How timetable-version values are written in DTOs and audit snapshots, in one place (F-005 plan
/// P13): status names and <c>HH:mm</c> times.
/// </summary>
public static class ScheduleReadMapping
{
    /// <summary>The four status names, exactly as stored and as the API writes them (spec §6).</summary>
    public static IReadOnlyList<string> StatusNames { get; } =
        [nameof(ScheduleVersionStatus.Draft), nameof(ScheduleVersionStatus.Published), nameof(ScheduleVersionStatus.Discarded), nameof(ScheduleVersionStatus.Cancelled)];

    public static string StatusName(ScheduleVersionStatus status) => status switch
    {
        ScheduleVersionStatus.Draft => nameof(ScheduleVersionStatus.Draft),
        ScheduleVersionStatus.Published => nameof(ScheduleVersionStatus.Published),
        ScheduleVersionStatus.Discarded => nameof(ScheduleVersionStatus.Discarded),
        ScheduleVersionStatus.Cancelled => nameof(ScheduleVersionStatus.Cancelled),
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    /// <summary>Exactly one of the four names (case-sensitive), or null (plan P17).</summary>
    public static ScheduleVersionStatus? ParseStatus(string? name) => name switch
    {
        nameof(ScheduleVersionStatus.Draft) => ScheduleVersionStatus.Draft,
        nameof(ScheduleVersionStatus.Published) => ScheduleVersionStatus.Published,
        nameof(ScheduleVersionStatus.Discarded) => ScheduleVersionStatus.Discarded,
        nameof(ScheduleVersionStatus.Cancelled) => ScheduleVersionStatus.Cancelled,
        _ => null
    };

    /// <summary><c>HH:mm</c>, or null for a missing time (ADR-0027 item 3).</summary>
    public static string? TimeText(TimetableTime? time) => time?.ToString();
}

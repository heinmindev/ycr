using YCR.Domain.Timetable;

namespace YCR.Application.Timetable;

/// <summary>
/// How a service's direction, operating days and <c>neverRuns</c> are written in every read DTO and
/// audit snapshot, in one place (F-004 plan P17, R40, R41).
/// </summary>
public static class ServiceReadMapping
{
    /// <summary><c>Forward</c> or <c>Reverse</c>, the stored text (R9).</summary>
    public static string DirectionName(Direction direction) => direction switch
    {
        Direction.Forward => nameof(Direction.Forward),
        Direction.Reverse => nameof(Direction.Reverse),
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };

    /// <summary>English day names, Monday first (R40).</summary>
    public static IReadOnlyList<string> DayNames(IEnumerable<DayOfWeek> days) =>
        [.. days.OrderBy(day => ((int)day + 6) % 7).Select(day => day.ToString())];

    /// <summary>The same, from the seven stored flags.</summary>
    public static IReadOnlyList<string> DayNames(
        bool monday, bool tuesday, bool wednesday, bool thursday, bool friday, bool saturday, bool sunday)
    {
        var days = new List<DayOfWeek>(7);
        if (monday) days.Add(DayOfWeek.Monday);
        if (tuesday) days.Add(DayOfWeek.Tuesday);
        if (wednesday) days.Add(DayOfWeek.Wednesday);
        if (thursday) days.Add(DayOfWeek.Thursday);
        if (friday) days.Add(DayOfWeek.Friday);
        if (saturday) days.Add(DayOfWeek.Saturday);
        if (sunday) days.Add(DayOfWeek.Sunday);
        return DayNames(days);
    }

    /// <summary>A withdrawn service whose period ended before it began never runs (R41).</summary>
    public static bool NeverRuns(DateOnly effectiveFrom, DateOnly? effectiveTo) => effectiveTo < effectiveFrom;
}

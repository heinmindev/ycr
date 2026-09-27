namespace YCR.Domain.Timetable;

/// <summary>
/// Whether a service listed by the version in force on a date runs on that date (R18, R48; F-005
/// plan P12).
/// </summary>
/// <remarks>
/// BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-26; T-053, OQ54, OQ55) — not a
/// Myanma Railways answer: a service runs on date <c>d</c> only if the version in force on
/// <c>d</c> lists it (the caller's condition), <c>d</c> is within the service's own period, and
/// <c>d</c>'s weekday is one of its operating days. Holidays and per-date exceptions are not
/// considered: OQ47 is still OPEN with Myanma Railways, with no placeholder (R35).
/// </remarks>
public static class ServiceRunningDay
{
    /// <param name="effectiveTo">Inclusive; null when open-ended. Earlier than
    /// <paramref name="effectiveFrom"/> for a service that never runs.</param>
    public static bool RunsOn(DateOnly date, DateOnly effectiveFrom, DateOnly? effectiveTo, OperatingDays days)
    {
        ArgumentNullException.ThrowIfNull(days);
        return effectiveFrom <= date
            && date <= (effectiveTo ?? DateOnly.MaxValue)
            && days.Includes(date.DayOfWeek);
    }
}

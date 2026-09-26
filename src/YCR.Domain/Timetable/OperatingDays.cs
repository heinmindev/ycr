namespace YCR.Domain.Timetable;

/// <summary>
/// The days of the week a service runs on (R17, R40; plan P7).
/// </summary>
/// <remarks>
/// BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-25; T-044, OQ47) — not a Myanma
/// Railways answer: days of the week only. A public-holiday calendar and per-date exceptions are
/// still an OPEN QUESTION with Myanma Railways (OQ47) and are deliberately absent, with no
/// placeholder. Stored as seven <c>bit</c> columns; <c>CK_Services_OperatingDays</c> requires one.
/// </remarks>
public sealed record OperatingDays
{
    private OperatingDays(
        bool runsOnMonday,
        bool runsOnTuesday,
        bool runsOnWednesday,
        bool runsOnThursday,
        bool runsOnFriday,
        bool runsOnSaturday,
        bool runsOnSunday)
    {
        RunsOnMonday = runsOnMonday;
        RunsOnTuesday = runsOnTuesday;
        RunsOnWednesday = runsOnWednesday;
        RunsOnThursday = runsOnThursday;
        RunsOnFriday = runsOnFriday;
        RunsOnSaturday = runsOnSaturday;
        RunsOnSunday = runsOnSunday;
    }

    public bool RunsOnMonday { get; }

    public bool RunsOnTuesday { get; }

    public bool RunsOnWednesday { get; }

    public bool RunsOnThursday { get; }

    public bool RunsOnFriday { get; }

    public bool RunsOnSaturday { get; }

    public bool RunsOnSunday { get; }

    /// <summary>The days, Monday first (R40).</summary>
    public IReadOnlyList<DayOfWeek> Days
    {
        get
        {
            var days = new List<DayOfWeek>(7);
            if (RunsOnMonday) days.Add(DayOfWeek.Monday);
            if (RunsOnTuesday) days.Add(DayOfWeek.Tuesday);
            if (RunsOnWednesday) days.Add(DayOfWeek.Wednesday);
            if (RunsOnThursday) days.Add(DayOfWeek.Thursday);
            if (RunsOnFriday) days.Add(DayOfWeek.Friday);
            if (RunsOnSaturday) days.Add(DayOfWeek.Saturday);
            if (RunsOnSunday) days.Add(DayOfWeek.Sunday);
            return days.AsReadOnly();
        }
    }

    /// <summary>Operating days from a non-empty set of distinct days (R17).</summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="days"/> is empty or repeats a day. The request validator refuses both first
    /// with <c>400 Common.ValidationFailed</c> (R17), so this is an invariant, not a business
    /// outcome, and the spec gives it no <c>Timetable.*</c> code (plan P7).
    /// </exception>
    public static OperatingDays Create(IReadOnlyCollection<DayOfWeek> days)
    {
        ArgumentNullException.ThrowIfNull(days);
        if (days.Count == 0)
        {
            throw new ArgumentException("A service runs on at least one day (R17).", nameof(days));
        }

        if (days.Distinct().Count() != days.Count)
        {
            throw new ArgumentException("Operating days may not repeat a day (R17).", nameof(days));
        }

        if (days.Any(day => !Enum.IsDefined(day)))
        {
            throw new ArgumentOutOfRangeException(nameof(days), "Every operating day must be a day of the week.");
        }

        return new OperatingDays(
            days.Contains(DayOfWeek.Monday),
            days.Contains(DayOfWeek.Tuesday),
            days.Contains(DayOfWeek.Wednesday),
            days.Contains(DayOfWeek.Thursday),
            days.Contains(DayOfWeek.Friday),
            days.Contains(DayOfWeek.Saturday),
            days.Contains(DayOfWeek.Sunday));
    }
}

namespace YCR.Domain.Timetable;

/// <summary>
/// A new version's number (R7; F-005 plan P5).
/// </summary>
/// <remarks>
/// BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-26; T-053, OQ51) — not a Myanma
/// Railways answer: a system-assigned sequential number, never reused. ENGINEERING DECISION (tech
/// lead, hein, 2026-09-26): <c>1</c>, then the highest number ever assigned + 1, read under the
/// Timetable-wide lock, so numbers are contiguous; <c>UX_ScheduleVersions_Number</c> is the backstop.
/// </remarks>
public static class ScheduleVersionNumbering
{
    /// <param name="highestAssigned">The highest number of any version, whatever its status; null
    /// when none exists.</param>
    public static int Next(int? highestAssigned) => (highestAssigned ?? 0) + 1;
}

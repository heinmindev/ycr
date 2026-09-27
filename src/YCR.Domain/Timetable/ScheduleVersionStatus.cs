namespace YCR.Domain.Timetable;

/// <summary>
/// The lifecycle of a <see cref="ScheduleVersion"/> (F-005 spec §5): Draft → Published →
/// (Cancelled), or Draft → Discarded.
/// </summary>
/// <remarks>
/// BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-26; T-053, OQ56, OQ58) — not a
/// Myanma Railways answer. Stored as text and checked by <c>CK_ScheduleVersions_Status</c>, so a
/// member is never renamed.
/// </remarks>
public enum ScheduleVersionStatus
{
    Draft = 1,
    Published = 2,
    Discarded = 3,
    Cancelled = 4
}

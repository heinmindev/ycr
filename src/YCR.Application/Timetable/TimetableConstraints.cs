namespace YCR.Application.Timetable;

/// <summary>
/// Database constraint names the Timetable module's handlers recognise (F-005 plan P19).
/// </summary>
/// <remarks>
/// A handler catches <see cref="Common.UniqueConstraintViolationException"/> and must match on the
/// constraint name, so the name has to be stated somewhere Application can see. The index itself
/// is declared by <c>ScheduleVersionConfiguration</c> in Infrastructure, which Application may not
/// reference. <c>ScheduleModelTests.ScheduleModel_ConstraintNames_MatchTheModel</c> keeps the two
/// equal: without it a rename would turn a <c>409</c> into an unhandled <c>500</c>.
/// </remarks>
public static class TimetableConstraints
{
    /// <summary>
    /// R22: start dates are unique among published versions; a filtered unique index
    /// (<c>WHERE [Status] = N'Published'</c>) is the authority (ADR-0026 item 1).
    /// </summary>
    public const string ScheduleVersionEffectiveFromPublishedUniqueIndex = "UX_ScheduleVersions_EffectiveFrom_Published";
}

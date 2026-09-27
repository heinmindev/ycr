namespace YCR.Domain.Timetable;

/// <summary>
/// What a <see cref="ScheduleVersion"/> needs to know about a service it lists, given by the
/// handler under the Timetable-wide lock (F-005 plan P3).
/// </summary>
/// <param name="EffectiveTo">Inclusive; null when open-ended.</param>
/// <param name="StopCount">The service's stop count <c>k</c>; positions are <c>1..k</c>.</param>
public sealed record ScheduleServiceFacts(
    Guid ServiceId,
    string Code,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    int StopCount)
{
    /// <summary>
    /// R17: the service is effective, and not withdrawn, on <paramref name="start"/>. A service that
    /// never runs (its end before its start) is effective on no date; a withdrawn service is not
    /// effective from its withdrawal date on (its end is the day before).
    /// </summary>
    /// <remarks>
    /// BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-26; T-053, OQ54) — not a
    /// Myanma Railways answer. The one home of this predicate (plan §Domain changes).
    /// </remarks>
    public bool IsEffectiveOn(DateOnly start) =>
        EffectiveFrom <= start && (EffectiveTo is null || start <= EffectiveTo);
}

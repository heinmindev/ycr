using YCR.Domain.Common;

namespace YCR.Domain.Timetable;

/// <summary>
/// A new service's effective period: an inclusive <see cref="From"/> and an inclusive, optional
/// <see cref="To"/> (null = open-ended) (R19; plan P13).
/// </summary>
/// <remarks>
/// BUSINESS DECISIONS — provisional tech-lead rulings (hein, 2026-09-25; T-044, OQ48, OQ50) — not
/// Myanma Railways answers: <c>EffectiveFrom</c> is required and may be earlier than today;
/// <c>EffectiveTo</c> is inclusive, nullable, and if given not earlier than today. The overlap
/// rule (R35) and the empty-period rule (R42) are decided here, from dates the handler loads, so
/// they are tested without a database.
/// </remarks>
public sealed record EffectivePeriod
{
    private EffectivePeriod(DateOnly from, DateOnly? to)
    {
        From = from;
        To = to;
    }

    public DateOnly From { get; }

    /// <summary>Inclusive; null when the period is open-ended.</summary>
    public DateOnly? To { get; }

    /// <summary>
    /// The period of a service being created. <paramref name="to"/> earlier than
    /// <paramref name="from"/> is <see cref="TimetableErrors.InvalidEffectivePeriod"/> (R19); then
    /// <paramref name="to"/> earlier than <paramref name="today"/> is
    /// <see cref="TimetableErrors.ServiceEffectiveToInPast"/> (R39). <paramref name="from"/> may be
    /// in the past (OQ50).
    /// </summary>
    /// <param name="today">Today's date in the configured local zone (R38).</param>
    public static Result<EffectivePeriod> ForNewService(DateOnly from, DateOnly? to, DateOnly today)
    {
        if (to < from)
        {
            return TimetableErrors.InvalidEffectivePeriod;
        }

        if (to < today)
        {
            return TimetableErrors.ServiceEffectiveToInPast;
        }

        return new EffectivePeriod(from, to);
    }

    /// <summary>
    /// Whether this period shares at least one date with another service's period (R35). Both are
    /// inclusive and a null end is unbounded. A period that never runs (its end before its start,
    /// R41) is empty and overlaps nothing (R42).
    /// </summary>
    public bool Overlaps(DateOnly otherFrom, DateOnly? otherTo)
    {
        if (otherTo < otherFrom || To < From)
        {
            return false;
        }

        return From <= (otherTo ?? DateOnly.MaxValue) && otherFrom <= (To ?? DateOnly.MaxValue);
    }
}

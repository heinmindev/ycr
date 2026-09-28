using YCR.Domain.Common;

namespace YCR.Domain.Timetable;

/// <summary>
/// A timetable version: a network-wide list of services with their stop times, in force from its
/// start date until the next published version's start date (F-005).
/// </summary>
/// <remarks>
/// BUSINESS DECISIONS — provisional tech-lead rulings (hein, 2026-09-26 and 2026-09-27; T-053,
/// T-054; OQ51-OQ58, OQ60, Q2, Amendments 1-2) — not Myanma Railways answers. If Myanma Railways
/// later answers differently, that supersedes these rulings and needs its own follow-up task:
/// <list type="bullet">
/// <item>OQ51: the whole network; a sequential number and a bilingual name; a start date only.</item>
/// <item>OQ52, OQ53: stop 1 departs, the last stop arrives, others both; whole minutes; arrival ≤
/// departure; each arrival later than the previous departure; no running past midnight.</item>
/// <item>OQ54: every listed service is effective, and not withdrawn, on the start date.</item>
/// <item>OQ56: created whole and never edited; a wrong draft is discarded, never deleted.</item>
/// <item>OQ57: published by any holder of <c>schedules.manage</c> while its start date is not
/// before today. OQ58: cancelled only while its start date is later than today.</item>
/// <item>OQ60, Amendments 1-2: it may list no services, but then it starts later than today.</item>
/// </list>
/// The facts about listed services are <em>given</em> by the handler (<see cref="ScheduleServiceFacts"/>);
/// what they mean is decided here (plan P3, P7). A published version never changes except its
/// status moving to <see cref="ScheduleVersionStatus.Cancelled"/> (R32): there is no other public
/// method.
/// </remarks>
public sealed class ScheduleVersion : AggregateRoot
{
    private readonly List<ScheduleVersionService> _services = [];

    private ScheduleVersion()
    {
    }

    /// <summary>R7: sequential, never reused.</summary>
    public int Number { get; private set; }

    public BilingualName Name { get; private set; } = null!;

    /// <summary>R8: the start date, set at creation and never changed.</summary>
    public DateOnly EffectiveFrom { get; private set; }

    public ScheduleVersionStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public DateTimeOffset? DiscardedAtUtc { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    /// <summary>The listed services, in request order; each with its stop times in position order.</summary>
    public IReadOnlyList<ScheduleVersionService> Services => _services.AsReadOnly();

    /// <summary>
    /// Creates a draft from parsed input and the listed services' facts (plan P3).
    /// </summary>
    /// <param name="facts">Facts for the requested services that exist; an id with no facts does
    /// not exist.</param>
    /// <remarks>
    /// For each service in request order, one service completed before the next (R45; plan P4):
    /// (6) exists, (7) not listed earlier, (8) effective on the start date, (9) every position ≤
    /// the stop count, (10) positions <c>1..k</c> each given once, (11) per stop in order an
    /// unexpected time then a missing required time, (12) per stop in order arrival ≤ departure,
    /// (13) for stops <c>2..k</c> arrival later than the previous departure.
    /// </remarks>
    public static Result<ScheduleVersion> CreateDraft(
        Guid id,
        int number,
        ScheduleVersionInput input,
        IReadOnlyCollection<ScheduleServiceFacts> facts,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        if (nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("CreatedAtUtc must use the UTC offset.", nameof(nowUtc));
        }

        var factsById = facts.ToDictionary(fact => fact.ServiceId);
        var listed = new HashSet<Guid>();
        var ordered = new List<(Guid ServiceId, IReadOnlyList<ScheduleStopTimeInput> StopTimes)>(input.Services.Count);
        foreach (var service in input.Services)
        {
            // 6. R17: the service exists.
            if (!factsById.TryGetValue(service.ServiceId, out var fact))
            {
                return TimetableErrors.ScheduleServiceNotFound(service.ServiceId);
            }

            // 7. R17: listed once; reported on the second occurrence.
            if (!listed.Add(service.ServiceId))
            {
                return TimetableErrors.ScheduleServiceRepeated(service.ServiceId);
            }

            // 8. R17: effective, and not withdrawn, on the start date.
            if (!fact.IsEffectiveOn(input.EffectiveFrom))
            {
                return TimetableErrors.ScheduleServiceNotEffective(service.ServiceId);
            }

            var stopTimes = CheckStopTimes(service, fact.StopCount);
            if (stopTimes.IsFailure)
            {
                return stopTimes.Error;
            }

            ordered.Add((service.ServiceId, stopTimes.Value));
        }

        var version = new ScheduleVersion
        {
            Id = id,
            Number = number,
            Name = input.Name,
            EffectiveFrom = input.EffectiveFrom,
            Status = ScheduleVersionStatus.Draft,
            CreatedAtUtc = nowUtc,
            PublishedAtUtc = null,
            DiscardedAtUtc = null,
            CancelledAtUtc = null
        };

        foreach (var (serviceId, stopTimes) in ordered)
        {
            version._services.Add(ScheduleVersionService.Create(id, serviceId, stopTimes));
        }

        return version;
    }

    /// <summary>
    /// Publishes a draft (Draft → Published). A refused call changes nothing.
    /// </summary>
    /// <param name="today">Today's date in the configured local zone (R33).</param>
    /// <param name="listedServices">Facts for every service this version lists, read under the
    /// lock; empty when it lists none.</param>
    /// <remarks>
    /// Order (plan P7): not a draft (R30); start before today (R31); an empty version whose start is
    /// not later than today (R50, Amendment 1); a listed service no longer effective on the start
    /// date, the lowest service id first (R17, re-checked at publication). The unique published
    /// start date (R22) is the database index's, not this method's.
    /// </remarks>
    public Result Publish(DateOnly today, IReadOnlyCollection<ScheduleServiceFacts> listedServices, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(listedServices);
        RequireUtc(nowUtc, nameof(nowUtc));

        if (Status != ScheduleVersionStatus.Draft)
        {
            return TimetableErrors.ScheduleVersionNotDraft;
        }

        if (EffectiveFrom < today)
        {
            return TimetableErrors.ScheduleVersionEffectiveFromInPast;
        }

        if (listedServices.Count == 0 && EffectiveFrom <= today)
        {
            return TimetableErrors.EmptyScheduleVersionNotInFuture;
        }

        var notEffective = listedServices
            .Where(service => !service.IsEffectiveOn(EffectiveFrom))
            .Select(service => service.ServiceId)
            .OrderBy(serviceId => serviceId.ToString("D"), StringComparer.Ordinal)
            .ToList();
        if (notEffective.Count > 0)
        {
            return TimetableErrors.ScheduleServiceNotEffective(notEffective[0]);
        }

        Status = ScheduleVersionStatus.Published;
        PublishedAtUtc = nowUtc;
        return Result.Success();
    }

    /// <summary>Discards a draft (Draft → Discarded; R28). A refused call changes nothing.</summary>
    public Result Discard(DateTimeOffset nowUtc)
    {
        RequireUtc(nowUtc, nameof(nowUtc));

        if (Status != ScheduleVersionStatus.Draft)
        {
            return TimetableErrors.ScheduleVersionNotDraft;
        }

        Status = ScheduleVersionStatus.Discarded;
        DiscardedAtUtc = nowUtc;
        return Result.Success();
    }

    /// <summary>
    /// Cancels a published version while its start date is later than today (Published →
    /// Cancelled; R34). <see cref="PublishedAtUtc"/> is kept. A refused call changes nothing.
    /// </summary>
    /// <param name="today">Today's date in the configured local zone (R33).</param>
    public Result Cancel(DateOnly today, DateTimeOffset nowUtc)
    {
        RequireUtc(nowUtc, nameof(nowUtc));

        if (Status != ScheduleVersionStatus.Published)
        {
            return TimetableErrors.ScheduleVersionNotPublished;
        }

        if (EffectiveFrom <= today)
        {
            return TimetableErrors.ScheduleVersionAlreadyEffective;
        }

        Status = ScheduleVersionStatus.Cancelled;
        CancelledAtUtc = nowUtc;
        return Result.Success();
    }

    /// <summary>
    /// Plan P4 checks 9-13 for one service with <paramref name="k"/> stops; on success, its stop
    /// times in position order.
    /// </summary>
    private static Result<IReadOnlyList<ScheduleStopTimeInput>> CheckStopTimes(ScheduleServiceInput service, int k)
    {
        var serviceId = service.ServiceId;

        // 9. R10: every position names a stop of the service (the first offending entry).
        foreach (var stopTime in service.StopTimes)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(stopTime.Position, 1);
            if (stopTime.Position > k)
            {
                return TimetableErrors.ScheduleStopNotInService(serviceId, stopTime.Position);
            }
        }

        // 10. R10: positions 1..k, each once: a repeated position first, then the lowest missing.
        var byPosition = new ScheduleStopTimeInput?[k + 1];
        foreach (var stopTime in service.StopTimes)
        {
            if (byPosition[stopTime.Position] is not null)
            {
                return TimetableErrors.ScheduleStopTimesIncomplete(serviceId, stopTime.Position);
            }

            byPosition[stopTime.Position] = stopTime;
        }

        for (var position = 1; position <= k; position++)
        {
            if (byPosition[position] is null)
            {
                return TimetableErrors.ScheduleStopTimesIncomplete(serviceId, position);
            }
        }

        var stops = byPosition.Skip(1).Select(stopTime => stopTime!).ToList();

        // 11. R10, per stop in order: stop 1 departs only, stop k arrives only, others both.
        for (var position = 1; position <= k; position++)
        {
            var stop = stops[position - 1];
            var arrivalAllowed = position > 1;
            var departureAllowed = position < k;
            if ((!arrivalAllowed && stop.Arrival is not null) || (!departureAllowed && stop.Departure is not null))
            {
                return TimetableErrors.ScheduleStopTimeUnexpected(serviceId, position);
            }

            if ((arrivalAllowed && stop.Arrival is null) || (departureAllowed && stop.Departure is null))
            {
                return TimetableErrors.ScheduleStopTimesIncomplete(serviceId, position);
            }
        }

        // 12. R12: at each stop, arrival ≤ departure (a dwell of 0 is allowed).
        for (var position = 1; position <= k; position++)
        {
            var stop = stops[position - 1];
            if (stop.Arrival is { } arrival && stop.Departure is { } departure && departure.Minutes < arrival.Minutes)
            {
                return TimetableErrors.ScheduleDwellNegative(serviceId, position);
            }
        }

        // 13. R12, R15: each arrival strictly later than the previous stop's departure. Every time
        // is on the operating date, so a journey that would cross midnight fails here.
        for (var position = 2; position <= k; position++)
        {
            if (stops[position - 1].Arrival!.Minutes <= stops[position - 2].Departure!.Minutes)
            {
                return TimetableErrors.ScheduleTimesNotIncreasing(serviceId, position);
            }
        }

        return Result<IReadOnlyList<ScheduleStopTimeInput>>.Success(stops.AsReadOnly());
    }

    private static void RequireUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Transition instants must use the UTC offset.", parameterName);
        }
    }
}

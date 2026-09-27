using YCR.Domain.Common;

namespace YCR.Domain.Timetable;

/// <summary>
/// A service: a code, a bilingual name, one route, a direction, an ordered list of stops (no
/// times), the days of the week it runs on and an effective period (F-004).
/// </summary>
/// <remarks>
/// BUSINESS DECISIONS — provisional tech-lead rulings (hein, 2026-09-25; T-044, OQ42-OQ50) — not
/// Myanma Railways answers. If Myanma Railways later answers differently, that supersedes these
/// rulings and needs its own follow-up task:
/// <list type="bullet">
/// <item>OQ44/OQ45: stops are stations of the route in the service's direction; a closed route
/// may wrap; a full circuit repeats the first stop as the last, once (R10-R14).</item>
/// <item>OQ46: an inactive route, or an inactive <em>stop</em> station, refuses creation (R15).</item>
/// <item>OQ48: immutable except <see cref="Withdraw"/>, which only shortens the period (R20, R21).</item>
/// </list>
/// Route and station facts are <em>given</em> by the handler (<see cref="ServiceRouteFacts"/>);
/// what they mean is decided here (plan P4), so every rule is testable without a database.
/// </remarks>
public sealed class Service : AggregateRoot
{
    private readonly List<ServiceStop> _stops = [];

    private Service()
    {
    }

    public ServiceCode Code { get; private set; } = null!;

    public BilingualName Name { get; private set; } = null!;

    /// <summary>The route, by id only (R8, R29).</summary>
    public Guid RouteId { get; private set; }

    public Direction Direction { get; private set; }

    public OperatingDays OperatingDays { get; private set; } = null!;

    public DateOnly EffectiveFrom { get; private set; }

    /// <summary>Inclusive; null when open-ended (R19). Changed only by <see cref="Withdraw"/>.</summary>
    public DateOnly? EffectiveTo { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Null until the first withdrawal; then the latest withdrawal's instant (R36).</summary>
    public DateTimeOffset? WithdrawnAtUtc { get; private set; }

    /// <summary>The stops, in position order <c>1..k</c> (S46).</summary>
    public IReadOnlyList<ServiceStop> Stops => _stops.AsReadOnly();

    /// <summary>A withdrawn service whose period ended before it began never runs (R41).</summary>
    public bool NeverRuns => EffectiveTo < EffectiveFrom;

    /// <summary>
    /// Creates a service with its whole stop list.
    /// </summary>
    /// <param name="route">The route's facts, stations in route position order.</param>
    /// <param name="stopStationIds">The stops, in order.</param>
    /// <remarks>
    /// Check order (R37; the earlier checks — request shape, code, names, period, route exists —
    /// run before this is called): route active (R15), stops on the route (R11), repeated stop
    /// (R14), too few stops (R13, R14), order (R12), inactive stop station (R15). Within one check
    /// the first offending stop in sequence order is reported. The overlap rule (R35) runs last,
    /// under the code lock, in the handler.
    /// </remarks>
    public static Result<Service> Create(
        Guid id,
        ServiceCode code,
        BilingualName name,
        ServiceRouteFacts route,
        Direction direction,
        IReadOnlyList<Guid> stopStationIds,
        OperatingDays operatingDays,
        EffectivePeriod period,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(route.Stations);
        ArgumentNullException.ThrowIfNull(stopStationIds);
        ArgumentNullException.ThrowIfNull(operatingDays);
        ArgumentNullException.ThrowIfNull(period);
        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        if (nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("CreatedAtUtc must use the UTC offset.", nameof(nowUtc));
        }

        var pattern = CheckStops(route, direction, stopStationIds);
        if (pattern.IsFailure)
        {
            return pattern.Error;
        }

        var service = new Service
        {
            Id = id,
            Code = code,
            Name = name,
            RouteId = route.RouteId,
            Direction = direction,
            OperatingDays = operatingDays,
            EffectiveFrom = period.From,
            EffectiveTo = period.To,
            CreatedAtUtc = nowUtc,
            WithdrawnAtUtc = null
        };

        // S46: positions 1..k in request order; a full circuit's closing stop stays last.
        for (var index = 0; index < stopStationIds.Count; index++)
        {
            service._stops.Add(ServiceStop.Create(id, index + 1, stopStationIds[index]));
        }

        return service;
    }

    /// <summary>
    /// Withdraws the service from <paramref name="withdrawFrom"/>, the first date on which it no
    /// longer runs: <see cref="EffectiveTo"/> becomes the day before (R21). A refused call changes
    /// nothing.
    /// </summary>
    /// <param name="today">Today's date in the configured local zone (R38).</param>
    /// <param name="coverage">The published timetable versions that list this service, read under
    /// the Timetable-wide lock (F-005 plan P10).</param>
    /// <remarks>
    /// F-004's checks run first and unchanged (R21: date in the past, does not shorten). Then the
    /// <strong>F-005 withdrawal guard (F-005 spec R19, §0.12)</strong> — BUSINESS DECISION,
    /// provisional tech-lead ruling (hein, 2026-09-26; T-053, OQ54), not a Myanma Railways answer:
    /// the withdrawal is refused while a published version that lists the service applies on some
    /// date ≥ <paramref name="withdrawFrom"/>. There is no overload without the coverage, so no code
    /// path can withdraw without it.
    /// </remarks>
    public Result Withdraw(DateOnly withdrawFrom, DateOnly today, DateTimeOffset nowUtc, ServiceScheduleCoverage coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        if (nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("WithdrawnAtUtc must use the UTC offset.", nameof(nowUtc));
        }

        if (withdrawFrom < today)
        {
            return TimetableErrors.WithdrawalDateInPast;
        }

        var newEnd = withdrawFrom.AddDays(-1);

        // The new end must be earlier than the current one; an open-ended period is unbounded.
        // Anything else would extend the period or leave it as it is (R21, S33, S34).
        if (EffectiveTo is { } currentEnd && newEnd >= currentEnd)
        {
            return TimetableErrors.WithdrawalDoesNotShorten;
        }

        // F-005 R19: checked last, after F-004's own checks (spec §0.12).
        if (coverage.AppliesOnOrAfter(withdrawFrom))
        {
            return TimetableErrors.ServiceInPublishedScheduleVersion(Id);
        }

        EffectiveTo = newEnd;
        WithdrawnAtUtc = nowUtc;
        return Result.Success();
    }

    /// <summary>The stop algorithm of plan §Domain changes (R15, R11, R14, R13, R12, R15).</summary>
    private static Result CheckStops(
        ServiceRouteFacts route,
        Direction direction,
        IReadOnlyList<Guid> stops)
    {
        // 1. R15: the route is active.
        if (!route.IsActive)
        {
            return TimetableErrors.ServiceRouteInactive(route.RouteId);
        }

        // 2. R11: every stop is a station of the route. A station's position is its index + 1.
        var position = new Dictionary<Guid, int>();
        for (var index = 0; index < route.Stations.Count; index++)
        {
            position.TryAdd(route.Stations[index].StationId, index + 1);
        }

        foreach (var stop in stops)
        {
            if (!position.ContainsKey(stop))
            {
                return TimetableErrors.ServiceStopNotOnRoute(stop);
            }
        }

        // 3. R14: no repeats, except the full-circuit closure (closed route, last stop = first).
        var k = stops.Count;
        var closure = route.IsClosed && k >= 2 && stops[k - 1] == stops[0];
        var seen = new HashSet<Guid>();
        for (var i = 0; i < k; i++)
        {
            if (!seen.Add(stops[i]) && !(closure && i == k - 1))
            {
                return TimetableErrors.ServiceStopRepeated(stops[i]);
            }
        }

        // 4. R13, R14: at least 2 stops, or 3 distinct stations for a full circuit.
        var distinct = closure ? k - 1 : k;
        if (distinct < (closure ? 3 : 2))
        {
            return TimetableErrors.ServiceTooFewStops;
        }

        // 5. R12: order in the direction. Open route: strictly monotonic, no wrap. Closed route:
        // each step is measured cyclically, and the steps add up to at most n - 1, or exactly n
        // for the closure: exactly one circuit.
        var n = route.Stations.Count;
        if (!route.IsClosed)
        {
            for (var i = 1; i < k; i++)
            {
                var inOrder = direction == Direction.Forward
                    ? position[stops[i]] > position[stops[i - 1]]
                    : position[stops[i]] < position[stops[i - 1]];
                if (!inOrder)
                {
                    return TimetableErrors.ServiceStopsOutOfOrder(stops[i]);
                }
            }
        }
        else
        {
            var travelled = 0;
            for (var i = 1; i < k; i++)
            {
                var step = direction == Direction.Forward
                    ? Mod(position[stops[i]] - position[stops[i - 1]], n)
                    : Mod(position[stops[i - 1]] - position[stops[i]], n);
                travelled += step;
                var limit = closure && i == k - 1 ? n : n - 1;
                if (travelled > limit)
                {
                    return TimetableErrors.ServiceStopsOutOfOrder(stops[i]);
                }
            }
        }

        // 6. R15: only the stations the service stops at must be active (spec §0.10 reading 2).
        foreach (var stop in stops)
        {
            if (!route.Stations[position[stop] - 1].IsActive)
            {
                return TimetableErrors.ServiceStopStationInactive(stop);
            }
        }

        return Result.Success();
    }

    /// <summary>The non-negative remainder, in <c>0..n-1</c>.</summary>
    private static int Mod(int value, int n) => ((value % n) + n) % n;
}

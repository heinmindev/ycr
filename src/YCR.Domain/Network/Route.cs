using YCR.Domain.Common;

namespace YCR.Domain.Network;

/// <summary>
/// A route: an ordered, immutable sequence of stations with a code, a bilingual name and an
/// open/closed setting (F-003).
/// </summary>
/// <remarks>
/// BUSINESS DECISION — provisional tech-lead rulings (hein, 2026-09-24; T-032, OQ36-OQ41) — not
/// a Myanma Railways answer. If Myanma Railways later answers differently, that supersedes these
/// rulings and needs its own follow-up task:
/// <list type="bullet">
/// <item>OQ37: a route is open or closed (<see cref="IsClosed"/>); a closed route runs from its
/// last station back to its first because of that flag, and the first station is never repeated
/// at the end (R6). A station appears at most once (R7). Minimum length: 2 open, 3 closed (R8).</item>
/// <item>OQ38 option (c): the sequence, <see cref="IsClosed"/>, code and names never change after
/// creation (R9). <see cref="Deactivate"/> is the only state change. The database grants state the
/// same rule (R10).</item>
/// <item>OQ39: an inactive station cannot be placed in a route (R11).</item>
/// <item>OQ41: deactivate only; no reactivation and no delete (R15).</item>
/// </list>
/// Station existence and activity are facts the aggregate is <em>given</em> by the handler; what
/// they mean is decided here (plan P4), so every rule is testable without a database.
/// </remarks>
public sealed class Route : AggregateRoot
{
    private readonly List<RouteStation> _stations = [];

    private Route()
    {
    }

    public RouteCode Code { get; private set; } = null!;

    public BilingualName Name { get; private set; } = null!;

    public bool IsClosed { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Null while active; set once, by <see cref="Deactivate"/> (A1).</summary>
    public DateTimeOffset? DeactivatedAtUtc { get; private set; }

    /// <summary>The sequence, in position order <c>1..n</c> (R18).</summary>
    public IReadOnlyList<RouteStation> Stations => _stations.AsReadOnly();

    /// <summary>
    /// Creates a route with its whole sequence (E4).
    /// </summary>
    /// <param name="stationIds">The sequence, in order.</param>
    /// <param name="knownStationIsActive">
    /// For each requested id that exists in <c>network.Stations</c>, whether it is active. An id
    /// missing from the map does not exist.
    /// </param>
    /// <remarks>
    /// Fixed precedence when several rules fail (plan P4): R7 repeated, then R8 too few, then R16
    /// not found, then R11 inactive. Within one rule the first offending id in sequence order is
    /// reported. Checking R7 first means R8 counts distinct stations.
    /// </remarks>
    public static Result<Route> Create(
        Guid id,
        RouteCode code,
        BilingualName name,
        bool isClosed,
        IReadOnlyList<Guid> stationIds,
        IReadOnlyDictionary<Guid, bool> knownStationIsActive,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(stationIds);
        ArgumentNullException.ThrowIfNull(knownStationIsActive);
        if (nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("CreatedAtUtc must use the UTC offset.", nameof(nowUtc));
        }

        var seen = new HashSet<Guid>();
        foreach (var stationId in stationIds)
        {
            if (!seen.Add(stationId))
            {
                return NetworkErrors.RouteStationRepeated(stationId);
            }
        }

        if (stationIds.Count < (isClosed ? 3 : 2))
        {
            return NetworkErrors.RouteTooFewStations;
        }

        foreach (var stationId in stationIds)
        {
            if (!knownStationIsActive.ContainsKey(stationId))
            {
                return NetworkErrors.RouteStationNotFound(stationId);
            }
        }

        foreach (var stationId in stationIds)
        {
            if (!knownStationIsActive[stationId])
            {
                return NetworkErrors.RouteStationInactive(stationId);
            }
        }

        var route = new Route
        {
            Id = id,
            Code = code,
            Name = name,
            IsClosed = isClosed,
            IsActive = true,
            CreatedAtUtc = nowUtc,
            DeactivatedAtUtc = null
        };

        // R6: the sequence is exactly what was given; a closed route is the flag, never a
        // repeated first station. R18: positions are 1..n by construction.
        for (var index = 0; index < stationIds.Count; index++)
        {
            route._stations.Add(RouteStation.Create(id, index + 1, stationIds[index]));
        }

        return route;
    }

    /// <summary>
    /// Deactivates the route, keeping the row and its sequence (R15). A second call is refused
    /// and changes nothing, so the first timestamp stands (R17).
    /// </summary>
    public Result Deactivate(DateTimeOffset nowUtc)
    {
        if (nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("DeactivatedAtUtc must use the UTC offset.", nameof(nowUtc));
        }

        if (!IsActive)
        {
            return NetworkErrors.RouteAlreadyInactive;
        }

        IsActive = false;
        DeactivatedAtUtc = nowUtc;
        Raise(new RouteDeactivated(Id));
        return Result.Success();
    }
}

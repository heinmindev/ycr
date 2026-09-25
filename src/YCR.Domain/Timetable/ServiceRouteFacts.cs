namespace YCR.Domain.Timetable;

/// <summary>
/// What a service needs to know about its route, given by the handler (plan P4).
/// </summary>
/// <remarks>
/// A Timetable domain record on purpose: the handler fills it from the Network contract
/// (ADR-0025), so the domain never sees a Network domain type or a Contracts type (ADR-0012 item 6).
/// </remarks>
/// <param name="Stations">The route's stations in route position order: a station's position is
/// its index + 1.</param>
public sealed record ServiceRouteFacts(
    Guid RouteId,
    bool IsActive,
    bool IsClosed,
    IReadOnlyList<ServiceRouteStationFacts> Stations);

/// <summary>One station of a <see cref="ServiceRouteFacts"/>, with its current active flag.</summary>
public sealed record ServiceRouteStationFacts(Guid StationId, bool IsActive);

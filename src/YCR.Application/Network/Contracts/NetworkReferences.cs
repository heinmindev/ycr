namespace YCR.Application.Network.Contracts;

/// <summary>A route and its stations, as <see cref="INetworkReader.GetRouteAsync"/> returns it.</summary>
/// <param name="Stations">In route position order <c>1..n</c>.</param>
public sealed record RouteReference(
    Guid Id,
    string Code,
    string NameEn,
    string NameMy,
    bool IsClosed,
    bool IsActive,
    IReadOnlyList<RouteStationReference> Stations);

/// <summary>One position of a <see cref="RouteReference"/>, with the station's current values.</summary>
public sealed record RouteStationReference(
    int Position,
    Guid StationId,
    string StationCode,
    string StationNameEn,
    string StationNameMy,
    bool StationIsActive);

/// <summary>A route's header, without its stations.</summary>
public sealed record RouteSummaryReference(
    Guid Id,
    string Code,
    string NameEn,
    string NameMy,
    bool IsClosed,
    bool IsActive);

/// <summary>A station's current code, names and active flag.</summary>
public sealed record StationReference(
    Guid Id,
    string Code,
    string NameEn,
    string NameMy,
    bool IsActive);

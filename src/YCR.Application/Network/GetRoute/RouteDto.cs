namespace YCR.Application.Network.GetRoute;

/// <summary>
/// A route as the Application layer hands it out (F-003 S2).
/// </summary>
/// <remarks>
/// Separate from the API's <c>RouteResponse</c> (F-001 P5): this one is internal, that one is the
/// public contract. No EF entity leaves through the API (AGENTS.md rule 4).
/// </remarks>
/// <param name="DeactivatedAtUtc">Null while the route is active (A1).</param>
/// <param name="Stations">In position order (R24).</param>
public sealed record RouteDto(
    Guid Id,
    string Code,
    string NameEn,
    string NameMy,
    bool IsClosed,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DeactivatedAtUtc,
    IReadOnlyList<RouteStationDto> Stations);

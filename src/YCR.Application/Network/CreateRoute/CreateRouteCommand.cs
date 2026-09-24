namespace YCR.Application.Network.CreateRoute;

/// <summary>Creates a route with its whole station sequence (F-003 S1; E4).</summary>
/// <remarks>
/// Carries no actor, address or correlation field (ADR-0017 item 2; S22).
/// </remarks>
/// <param name="StationIds">The complete sequence, in order.</param>
public sealed record CreateRouteCommand(
    string Code,
    string NameEn,
    string NameMy,
    bool IsClosed,
    IReadOnlyList<Guid> StationIds);

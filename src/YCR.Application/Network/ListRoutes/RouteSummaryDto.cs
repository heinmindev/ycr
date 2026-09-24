namespace YCR.Application.Network.ListRoutes;

/// <summary>One route in a list page (F-003 S3), with the length of its sequence.</summary>
/// <param name="DeactivatedAtUtc">Null while the route is active (A1).</param>
public sealed record RouteSummaryDto(
    Guid Id,
    string Code,
    string NameEn,
    string NameMy,
    bool IsClosed,
    bool IsActive,
    int StationCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DeactivatedAtUtc);

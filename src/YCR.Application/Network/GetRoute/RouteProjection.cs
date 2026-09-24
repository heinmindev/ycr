using YCR.Domain.Network;

namespace YCR.Application.Network.GetRoute;

/// <summary>
/// The shape a route query pulls back from the database, before it becomes a
/// <see cref="RouteDto"/>.
/// </summary>
/// <remarks>
/// The <c>StationProjection</c> pattern (ENGINEERING DECISION, hein, 2026-09-21): project the whole
/// <see cref="RouteCode"/>, which EF can translate, never <c>Code.Value</c>, which it cannot; and
/// the owned name as its two scalars. The aggregate is never materialised for a read (plan P11).
/// </remarks>
public sealed record RouteProjection(
    Guid Id,
    RouteCode Code,
    string NameEn,
    string NameMy,
    bool IsClosed,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DeactivatedAtUtc)
{
    public RouteDto ToDto(IReadOnlyList<RouteStationDto> stations) =>
        new(Id, Code.Value, NameEn, NameMy, IsClosed, IsActive, CreatedAtUtc, DeactivatedAtUtc, stations);
}

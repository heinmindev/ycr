using YCR.Domain.Network;

namespace YCR.Application.Network.GetStation;

/// <summary>
/// The shape a station query pulls back from the database, before it becomes a
/// <see cref="StationDto"/>.
/// </summary>
/// <remarks>
/// ENGINEERING DECISION (hein, 2026-09-21): queries <em>project</em>, as `docs/20` §4 requires —
/// but they project whole value-object properties rather than reaching inside them. EF Core can
/// translate <c>station.Code</c>, which is one column behind a value converter, and it cannot
/// translate <c>station.Code.Value</c>, because a converter is opaque to the query translator.
/// Projecting the property itself keeps the whole page-shaping — <c>ORDER BY</c>, <c>OFFSET</c>,
/// <c>FETCH NEXT</c> and <c>COUNT</c> — on the server, and leaves only the trivial unwrapping to
/// memory.
/// <para>
/// <c>NameEn</c> and <c>NameMy</c> are projected as the scalars they are, rather than as the
/// owned <c>BilingualName</c>, because EF refuses to project an owned entity type into a
/// non-entity result.
/// </para>
/// <para>
/// This is the pattern every module's queries should follow; the stage-8 `docs/20` §4 update
/// records it.
/// </para>
/// </remarks>
public sealed record StationProjection(
    Guid Id,
    StationCode Code,
    string NameEn,
    string NameMy,
    bool IsActive,
    DateTimeOffset CreatedAtUtc)
{
    public StationDto ToDto() => new(Id, Code.Value, NameEn, NameMy, IsActive, CreatedAtUtc);
}

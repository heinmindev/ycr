using YCR.Application.Common.Abstractions;
using YCR.Domain.Network;

namespace YCR.Application.Network;

/// <summary>
/// The route state written into an audit event's <c>BeforeJson</c> and <c>AfterJson</c>
/// (F-003 spec §8; plan §Audit).
/// </summary>
/// <remarks>
/// BUSINESS DECISION (hein, 2026-09-20): audit payloads are explicit snapshot records, never
/// entities. Serialising <see cref="Route"/> directly would put whatever the aggregate carries into
/// a row that can never be edited or redacted.
/// <para>
/// <strong>Changing this record's shape, or <see cref="RouteStationAuditSnapshot"/>'s, requires
/// bumping <c>PayloadVersion</c></strong> (ADR-0021 rule 3). Old rows are never rewritten.
/// </para>
/// <para>
/// Each station carries its code so a ledger row is readable without a join; the code is stable
/// because station codes are never reused (F-001 R3).
/// </para>
/// <para>
/// REQUIRED CONTROL (ADR-0021 rule 4): nothing added here may be a password, token, refresh
/// cookie, full QR payload, private key or personal data. A route has none today.
/// </para>
/// </remarks>
public sealed record RouteAuditSnapshot(
    string Code,
    string NameEn,
    string NameMy,
    bool IsClosed,
    bool IsActive,
    DateTimeOffset? DeactivatedAtUtc,
    IReadOnlyList<RouteStationAuditSnapshot> Stations) : IAuditSnapshot
{
    /// <summary>Takes a snapshot of <paramref name="route"/> as it stands now.</summary>
    /// <param name="stationCodes">The code of every station in the route, by id.</param>
    public static RouteAuditSnapshot From(Route route, IReadOnlyDictionary<Guid, string> stationCodes)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(stationCodes);

        return new RouteAuditSnapshot(
            route.Code.Value,
            route.Name.En,
            route.Name.My,
            route.IsClosed,
            route.IsActive,
            route.DeactivatedAtUtc,
            [.. route.Stations
                .OrderBy(station => station.Position)
                .Select(station => new RouteStationAuditSnapshot(
                    station.Position,
                    station.StationId,
                    stationCodes[station.StationId]))]);
    }
}

/// <summary>One position of a <see cref="RouteAuditSnapshot"/>'s sequence.</summary>
public sealed record RouteStationAuditSnapshot(int Position, Guid StationId, string StationCode);

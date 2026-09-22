using YCR.Application.Common.Abstractions;
using YCR.Domain.Network;

namespace YCR.Application.Network;

/// <summary>
/// The station state written into an audit event's <c>BeforeJson</c> and <c>AfterJson</c>.
/// </summary>
/// <remarks>
/// BUSINESS DECISION (hein, 2026-09-20): audit payloads are explicit snapshot records, never
/// entities. Serialising <see cref="Station"/> directly would put whatever the aggregate happens
/// to carry into a row that can never be edited or redacted, and would silently change the
/// payload shape every time the aggregate gained a field.
/// <para>
/// <strong>Changing this record's shape requires bumping <c>PayloadVersion</c></strong>
/// (ADR-0021 rule 3). Old rows are never rewritten; readers switch on the version instead.
/// </para>
/// <para>
/// REQUIRED CONTROL (ADR-0021 rule 4): nothing added here may be a password, token, refresh
/// cookie, full QR payload, private key or personal data. A station has none today, and that is
/// a property to re-check whenever this record grows.
/// </para>
/// </remarks>
/// <param name="Code">The station code, e.g. <c>INS</c>.</param>
/// <param name="NameEn">English name.</param>
/// <param name="NameMy">Myanmar-script name.</param>
/// <param name="IsActive">Whether the station was active in this state.</param>
public sealed record StationAuditSnapshot(string Code, string NameEn, string NameMy, bool IsActive)
    : IAuditSnapshot
{
    /// <summary>Takes a snapshot of <paramref name="station"/> as it stands now.</summary>
    public static StationAuditSnapshot From(Station station)
    {
        ArgumentNullException.ThrowIfNull(station);

        return new StationAuditSnapshot(
            station.Code.Value,
            station.Name.En,
            station.Name.My,
            station.IsActive);
    }
}

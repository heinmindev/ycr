using YCR.Application.Common.Abstractions;
using YCR.Domain.Timetable;

namespace YCR.Application.Timetable;

/// <summary>
/// The service state written into an audit event's <c>BeforeJson</c> and <c>AfterJson</c> (F-004
/// spec §8; plan §Audit).
/// </summary>
/// <remarks>
/// BUSINESS DECISION (hein, 2026-09-20): audit payloads are explicit snapshot records, never
/// entities. <strong>Changing this record's shape, or <see cref="ServiceStopAuditSnapshot"/>'s,
/// requires bumping <c>PayloadVersion</c></strong> (ADR-0021 rule 3). Old rows are never rewritten.
/// <para>
/// The route and each stop carry their code so a ledger row reads without a join; the codes are
/// stable because station and route codes are never reused (F-001 R3, F-003 R14).
/// </para>
/// <para>
/// REQUIRED CONTROL (ADR-0021 rule 4): nothing added here may be a password, token, refresh
/// cookie, full QR payload, private key or personal data. A service has none.
/// </para>
/// </remarks>
/// <param name="OperatingDays">Day names, Monday first (R40).</param>
public sealed record ServiceAuditSnapshot(
    string Code,
    string NameEn,
    string NameMy,
    Guid RouteId,
    string RouteCode,
    string Direction,
    IReadOnlyList<ServiceStopAuditSnapshot> Stops,
    IReadOnlyList<string> OperatingDays,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    DateTimeOffset? WithdrawnAtUtc) : IAuditSnapshot
{
    /// <summary>Takes a snapshot of <paramref name="service"/> as it stands now.</summary>
    /// <param name="routeCode">The code of the service's route, from the Network contract.</param>
    /// <param name="stationCodes">The code of every stop station, by id, from the Network contract.</param>
    public static ServiceAuditSnapshot From(
        Service service,
        string routeCode,
        IReadOnlyDictionary<Guid, string> stationCodes)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(routeCode);
        ArgumentNullException.ThrowIfNull(stationCodes);

        return new ServiceAuditSnapshot(
            service.Code.Value,
            service.Name.En,
            service.Name.My,
            service.RouteId,
            routeCode,
            ServiceReadMapping.DirectionName(service.Direction),
            [.. service.Stops
                .OrderBy(stop => stop.Position)
                .Select(stop => new ServiceStopAuditSnapshot(stop.Position, stop.StationId, stationCodes[stop.StationId]))],
            ServiceReadMapping.DayNames(service.OperatingDays.Days),
            service.EffectiveFrom,
            service.EffectiveTo,
            service.WithdrawnAtUtc);
    }
}

/// <summary>One stop of a <see cref="ServiceAuditSnapshot"/>.</summary>
public sealed record ServiceStopAuditSnapshot(int Position, Guid StationId, string StationCode);

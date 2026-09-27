using YCR.Application.Common.Abstractions;
using YCR.Domain.Timetable;

namespace YCR.Application.Timetable;

/// <summary>
/// A version's header, written into the <c>BeforeJson</c> and <c>AfterJson</c> of its publish,
/// discard and cancel events (F-005 spec §8; plan §Audit, P18).
/// </summary>
/// <remarks>
/// BUSINESS DECISION (hein, 2026-09-20): audit payloads are explicit snapshot records, never
/// entities. <strong>Changing this record's shape requires bumping <c>PayloadVersion</c></strong>
/// (ADR-0021 rule 3). Old rows are never rewritten.
/// <para>
/// REQUIRED CONTROL (ADR-0021 rule 4): nothing added here may be a password, token, refresh
/// cookie, full QR payload, private key or personal data. A timetable has none.
/// </para>
/// </remarks>
public sealed record ScheduleVersionAuditSnapshot(
    int Number,
    string NameEn,
    string NameMy,
    DateOnly EffectiveFrom,
    string Status,
    DateTimeOffset? PublishedAtUtc,
    DateTimeOffset? DiscardedAtUtc,
    DateTimeOffset? CancelledAtUtc) : IAuditSnapshot
{
    /// <summary>Takes a snapshot of <paramref name="version"/>'s header as it stands now.</summary>
    public static ScheduleVersionAuditSnapshot From(ScheduleVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return new ScheduleVersionAuditSnapshot(
            version.Number,
            version.Name.En,
            version.Name.My,
            version.EffectiveFrom,
            ScheduleReadMapping.StatusName(version.Status),
            version.PublishedAtUtc,
            version.DiscardedAtUtc,
            version.CancelledAtUtc);
    }
}

/// <summary>
/// A new draft, written into the <c>AfterJson</c> of <c>Timetable.ScheduleVersionCreated</c>: the
/// header, the listed services and a digest of every stop time (F-005 spec §8, E13; plan P18).
/// </summary>
/// <remarks>
/// <strong>Changing this record's shape, or <see cref="ScheduleVersionServiceAuditSnapshot"/>'s,
/// requires bumping <c>PayloadVersion</c></strong> (ADR-0021 rule 3). <see cref="Services"/> are
/// ordered by their lower-case <c>D</c> id, ordinal (the digest's order);
/// <see cref="StopTimesSha256"/> is <see cref="ScheduleStopTimesDigest"/>'s canonical form. No
/// personal data (ADR-0021 rule 4).
/// </remarks>
public sealed record ScheduleVersionCreatedAuditSnapshot(
    int Number,
    string NameEn,
    string NameMy,
    DateOnly EffectiveFrom,
    string Status,
    DateTimeOffset? PublishedAtUtc,
    DateTimeOffset? DiscardedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    IReadOnlyList<ScheduleVersionServiceAuditSnapshot> Services,
    string StopTimesSha256) : IAuditSnapshot
{
    /// <param name="serviceCodes">The code of every listed service, by id, from the facts read under the lock.</param>
    public static ScheduleVersionCreatedAuditSnapshot From(ScheduleVersion version, IReadOnlyDictionary<Guid, string> serviceCodes)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(serviceCodes);
        return new ScheduleVersionCreatedAuditSnapshot(
            version.Number,
            version.Name.En,
            version.Name.My,
            version.EffectiveFrom,
            ScheduleReadMapping.StatusName(version.Status),
            version.PublishedAtUtc,
            version.DiscardedAtUtc,
            version.CancelledAtUtc,
            [.. version.Services
                .OrderBy(entry => entry.ServiceId.ToString("D"), StringComparer.Ordinal)
                .Select(entry => new ScheduleVersionServiceAuditSnapshot(entry.ServiceId, serviceCodes[entry.ServiceId], entry.StopTimes.Count))],
            ScheduleStopTimesDigest.Compute(version.Services.SelectMany(entry => entry.StopTimes.Select(stop =>
                new ScheduleStopTimeDigestRow(stop.ServiceId, stop.Position, stop.Arrival?.Minutes, stop.Departure?.Minutes)))));
    }
}

/// <summary>One listed service of a <see cref="ScheduleVersionCreatedAuditSnapshot"/>.</summary>
public sealed record ScheduleVersionServiceAuditSnapshot(Guid ServiceId, string ServiceCode, int StopCount);

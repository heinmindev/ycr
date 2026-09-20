using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using YCR.Application.Common.Abstractions;
using YCR.Domain.Common;
using YCR.Infrastructure.Persistence;

namespace YCR.Infrastructure.Audit;

/// <summary>
/// Writes audit rows into the caller's unit of work, so the audit trail commits in the same
/// transaction as the change it describes (ADR-0017).
/// </summary>
/// <remarks>
/// <see cref="Record"/> only tracks the row; the caller's <c>SaveChangesAsync</c> is what
/// inserts it. That is the point: an audit row cannot commit for a change that rolled back, and
/// a change cannot commit without its audit row.
/// <para>
/// <strong>REQUIRED CONTROL (ADR-0017 item 2):</strong> <c>ActorUserId</c>, <c>ActorRole</c>,
/// <c>ClientIp</c>, <c>CorrelationId</c> and <c>AuthorizedByPermission</c> are read from
/// <see cref="ICurrentUser"/> and never from a caller-supplied argument. There is deliberately
/// no parameter through which a request could influence them.
/// </para>
/// <para>
/// <strong>REQUIRED CONTROL (ADR-0021 rule 4):</strong> <c>before</c> and <c>after</c> must never
/// carry passwords, tokens, refresh cookies, full QR payloads or private keys. This class cannot
/// enforce that; each module owns what its snapshot record contains.
/// </para>
/// </remarks>
internal sealed class AuditWriter(
    YcrDbContext context,
    ICurrentUser currentUser,
    IIdGenerator idGenerator,
    TimeProvider timeProvider) : IAuditWriter
{
    /// <summary>
    /// ADR-0021 rule 3: the schema version of the JSON payloads. Bump this rather than
    /// rewriting old rows when a snapshot record's shape changes.
    /// </summary>
    private const int CurrentPayloadVersion = 1;

    /// <remarks>
    /// The encoder is widened to all Unicode ranges deliberately. The default escapes every
    /// non-ASCII character, which would store every Myanmar-script station name in the ledger as
    /// a run of six-character Unicode escape sequences — unreadable to an investigator and
    /// several times larger, in a table that can never be rewritten. Myanmar names are the norm
    /// on this network, not an edge case.
    /// <para>
    /// This widens the character range only; the characters that matter for HTML and script
    /// injection are still escaped, and a ledger payload is never rendered as markup anyway.
    /// It changes the encoding of a payload, not its shape, so no <c>PayloadVersion</c> bump is
    /// required (ADR-0021 rule 3) — the JSON value is identical either way.
    /// </para>
    /// </remarks>
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public void Record(
        string action,
        string subjectType,
        Guid? subjectId,
        object? before,
        object? after,
        string? reasonCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectType);

        context.AuditEvents.Add(new AuditEvent
        {
            Id = idGenerator.New(),
            OccurredAtUtc = timeProvider.GetUtcNow(),
            Action = action,
            ActorUserId = currentUser.UserId,
            ActorRole = SerializeRoles(currentUser.Roles),
            SubjectType = subjectType,
            SubjectId = subjectId,
            BeforeJson = SerializeSnapshot(before, nameof(before)),
            AfterJson = SerializeSnapshot(after, nameof(after)),
            CorrelationId = currentUser.CorrelationId,
            ClientIp = currentUser.ClientIp,
            ReasonCode = reasonCode,
            AuthorizedByPermission = currentUser.AuthorizedByPermission,
            PayloadVersion = CurrentPayloadVersion
        });
    }

    /// <summary>
    /// ADR-0021's <c>CK_AuditEvents_ActorRole</c> only proves the text is JSON, not that it is
    /// an array. The ADR puts that obligation on the writer, so the array shape is produced
    /// here and asserted by a test.
    /// </summary>
    private static string? SerializeRoles(IReadOnlyCollection<string> roles) =>
        roles.Count == 0 ? null : JsonSerializer.Serialize(roles, PayloadOptions);

    /// <summary>
    /// Serialises a snapshot record, refusing a domain entity.
    /// </summary>
    /// <remarks>
    /// BUSINESS DECISION (hein, 2026-09-20): audit payloads are snapshot records, never entities.
    /// The parameter type is <c>object?</c>, so this is a runtime guard rather than a compile-time
    /// one — it fails loudly on the first call instead of writing an un-redactable row. Typing the
    /// parameters as a snapshot marker interface would make the mistake impossible to compile at
    /// all; that is a cheap change if it is wanted.
    /// </remarks>
    private static string? SerializeSnapshot(object? snapshot, string parameterName)
    {
        if (snapshot is null)
        {
            return null;
        }

        if (snapshot is Entity)
        {
            throw new ArgumentException(
                $"'{parameterName}' must be an audit snapshot record, not the '{snapshot.GetType().Name}' entity. "
                + "An entity serialises its whole surface into an append-only row that can never be redacted.",
                parameterName);
        }

        return JsonSerializer.Serialize(snapshot, PayloadOptions);
    }
}

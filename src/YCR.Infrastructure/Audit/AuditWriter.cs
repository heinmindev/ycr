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
/// F-002 plan P5 adds the two exceptions ADR-0023 item 6 (U4) requires, neither of which reads a
/// request: <see cref="RecordWithoutActor"/> writes no actor at all, and
/// <see cref="RecordSignIn"/> names the user whose password <c>LoginHandler</c> has just verified
/// (architecture-tested to that one caller). Both write <c>ActorRole</c> null when there is no actor.
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
        IAuditSnapshot? before,
        IAuditSnapshot? after,
        string? reasonCode = null)
    {
        Add(
            action,
            actorUserId: currentUser.UserId,
            actorRoles: currentUser.Roles,
            authorizedByPermission: currentUser.AuthorizedByPermission,
            subjectType,
            subjectId,
            before,
            after,
            reasonCode);
    }

    /// <inheritdoc />
    /// <remarks>
    /// F-002 plan P5: the actor fields are forced null here, not read from
    /// <see cref="ICurrentUser"/>, so a bearer token attached to a sign-in or refresh request
    /// cannot put its user on a failed-login, lockout or family-revocation row (S27).
    /// </remarks>
    public void RecordWithoutActor(
        string action,
        string subjectType,
        Guid? subjectId,
        IAuditSnapshot? before,
        IAuditSnapshot? after) =>
        Add(
            action,
            actorUserId: null,
            actorRoles: [],
            authorizedByPermission: null,
            subjectType,
            subjectId,
            before,
            after,
            reasonCode: null);

    /// <inheritdoc />
    public void RecordSignIn(
        Guid verifiedUserId,
        IReadOnlyCollection<string> roles,
        string action,
        string subjectType,
        IAuditSnapshot? after)
    {
        if (verifiedUserId == Guid.Empty)
        {
            throw new ArgumentException("The signed-in user must be identified.", nameof(verifiedUserId));
        }

        ArgumentNullException.ThrowIfNull(roles);

        Add(
            action,
            actorUserId: verifiedUserId,
            actorRoles: roles,
            authorizedByPermission: null,
            subjectType,
            subjectId: verifiedUserId,
            before: null,
            after,
            reasonCode: null);
    }

    private void Add(
        string action,
        Guid? actorUserId,
        IReadOnlyCollection<string> actorRoles,
        string? authorizedByPermission,
        string subjectType,
        Guid? subjectId,
        IAuditSnapshot? before,
        IAuditSnapshot? after,
        string? reasonCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectType);

        context.AuditEvents.Add(new AuditEvent
        {
            Id = idGenerator.New(),
            OccurredAtUtc = timeProvider.GetUtcNow(),
            Action = action,
            ActorUserId = actorUserId,
            ActorRole = SerializeRoles(actorRoles),
            SubjectType = subjectType,
            SubjectId = subjectId,
            BeforeJson = SerializeSnapshot(before),
            AfterJson = SerializeSnapshot(after),
            CorrelationId = currentUser.CorrelationId,
            ClientIp = currentUser.ClientIp,
            ReasonCode = reasonCode,
            AuthorizedByPermission = authorizedByPermission,
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

    /// <summary>Serialises a snapshot record by its runtime type.</summary>
    /// <remarks>
    /// The explicit <c>GetType()</c> is load-bearing, not defensive. The parameter's static type
    /// is <see cref="IAuditSnapshot"/>, which declares no members, so the generic overload would
    /// serialise against the interface and write <c>{}</c> into every payload — a silent, total
    /// loss of audit state in a table that can never be corrected. Passing the runtime type is
    /// what makes the marker interface safe to use here.
    /// <para>
    /// There is no entity check any more: <see cref="IAuditSnapshot"/> makes an entity impossible
    /// to pass (hein's ruling, 2026-09-20), and an architecture test enforces what may implement it.
    /// </para>
    /// </remarks>
    private static string? SerializeSnapshot(IAuditSnapshot? snapshot) =>
        snapshot is null ? null : JsonSerializer.Serialize(snapshot, snapshot.GetType(), PayloadOptions);
}

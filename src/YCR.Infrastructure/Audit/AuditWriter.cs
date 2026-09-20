using System.Text.Json;
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
/// <c>ClientIp</c> and <c>CorrelationId</c> are read from <see cref="ICurrentUser"/> and never
/// from a caller-supplied argument. There is deliberately no parameter through which a request
/// could influence them.
/// </para>
/// <para>
/// <strong>REQUIRED CONTROL (ADR-0021 rule 4):</strong> <paramref name="before"/> and
/// <paramref name="after"/> must never carry passwords, tokens, refresh cookies, full QR
/// payloads or private keys. This class cannot enforce that; each module owns what it passes.
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
    /// rewriting old rows when a payload shape changes.
    /// </summary>
    private const int CurrentPayloadVersion = 1;

    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    public void Record(
        string action,
        object subject,
        object? before,
        object? after,
        string? authorizedByPermission = null,
        string? reasonCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentNullException.ThrowIfNull(subject);

        context.AuditEvents.Add(new AuditEvent
        {
            Id = idGenerator.New(),
            OccurredAtUtc = timeProvider.GetUtcNow(),
            Action = action,
            ActorUserId = currentUser.UserId,
            ActorRole = SerializeRoles(currentUser.Roles),
            SubjectType = SubjectTypeOf(subject),
            SubjectId = (subject as Entity)?.Id,
            BeforeJson = SerializePayload(before),
            AfterJson = SerializePayload(after),
            CorrelationId = currentUser.CorrelationId,
            ClientIp = currentUser.ClientIp,
            ReasonCode = reasonCode,
            AuthorizedByPermission = authorizedByPermission,
            PayloadVersion = CurrentPayloadVersion
        });
    }

    /// <summary>
    /// The subject may be the aggregate itself or, for an event with no entity to hand, a
    /// <see cref="Type"/> naming what the event was about. An <see cref="Entity"/> also yields
    /// <c>SubjectId</c>; anything else leaves it null, which ADR-0021 allows.
    /// </summary>
    private static string SubjectTypeOf(object subject) =>
        subject is Type type ? type.Name : subject.GetType().Name;

    /// <summary>
    /// ADR-0021's <c>CK_AuditEvents_ActorRole</c> only proves the text is JSON, not that it is
    /// an array. The ADR puts that obligation on the writer, so the array shape is produced
    /// here and asserted by a test.
    /// </summary>
    private static string? SerializeRoles(IReadOnlyCollection<string> roles) =>
        roles.Count == 0 ? null : JsonSerializer.Serialize(roles, PayloadOptions);

    private static string? SerializePayload(object? payload) =>
        payload is null ? null : JsonSerializer.Serialize(payload, PayloadOptions);
}

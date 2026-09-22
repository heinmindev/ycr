namespace YCR.Infrastructure.Audit;

/// <summary>
/// One row of <c>audit.AuditEvents</c>, shaped exactly as ADR-0021 §Columns defines it.
/// </summary>
/// <remarks>
/// This is a persistence record, not a domain type, which is why it lives in Infrastructure:
/// the Application layer's contract with the audit trail is <see cref="Application.Common.Abstractions.IAuditWriter"/>,
/// and nothing above Infrastructure should know the ledger's column shape.
/// <para>
/// The table's generated ledger columns — <c>ledger_start_transaction_id</c> and
/// <c>ledger_start_sequence_number</c> — are deliberately absent. SQL Server marks them hidden
/// and populates them itself; mapping them would make EF try to write values the server owns.
/// </para>
/// </remarks>
internal sealed class AuditEvent
{
    public required Guid Id { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary><c>&lt;Module&gt;.&lt;Event&gt;</c>, e.g. <c>Network.StationCreated</c> (docs/20 §2).</summary>
    public required string Action { get; init; }

    /// <summary>Server-side authenticated context only (ADR-0017 item 2). Null for system actions.</summary>
    public Guid? ActorUserId { get; init; }

    /// <summary>JSON array of the roles held at event time, e.g. <c>["Admin"]</c> (ADR-0021).</summary>
    public string? ActorRole { get; init; }

    public required string SubjectType { get; init; }

    /// <summary>Null where the event has no GUID subject, such as a failed login.</summary>
    public Guid? SubjectId { get; init; }

    public string? BeforeJson { get; init; }

    public string? AfterJson { get; init; }

    public required string CorrelationId { get; init; }

    /// <summary>The caller address as the server observed it, never a client-supplied header.</summary>
    public string? ClientIp { get; init; }

    public string? ReasonCode { get; init; }

    public string? AuthorizedByPermission { get; init; }

    /// <summary>Schema version of the JSON payloads, starting at 1 (ADR-0021 rule 3).</summary>
    public required int PayloadVersion { get; init; }
}

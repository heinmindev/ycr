namespace YCR.Application.Common.Abstractions;

/// <summary>
/// Records a business-significant action in the audit ledger, within the caller's unit of work.
/// </summary>
/// <remarks>
/// BUSINESS DECISION (hein, 2026-09-20) — this shape replaces the step-4 signature, which took
/// the aggregate itself as the subject and as the state payload. Three things follow from it:
/// <list type="number">
/// <item><paramref name="subjectType"/> is a stable module-prefixed constant such as
/// <c>Network.Station</c>, declared per module (see <c>NetworkAuditSubjects</c>). It is never
/// derived from a CLR type name: a rename or a namespace move would silently change the meaning
/// of rows already written to an append-only table.</item>
/// <item><paramref name="before"/> and <paramref name="after"/> are explicit audit snapshot
/// records, never entities. An entity would drag its whole surface — navigations, lazily added
/// fields, eventually something secret — into a row that can never be redacted. Changing a
/// snapshot's shape requires bumping <c>PayloadVersion</c> (ADR-0021 rule 3).</item>
/// <item>There is no <c>authorizedByPermission</c> parameter. It is derived server-side from the
/// endpoint's required permission through <see cref="ICurrentUser"/>, so a handler cannot state
/// an authority it did not actually have.</item>
/// </list>
/// The actor, address and correlation fields are likewise absent by design: ADR-0017 item 2
/// requires them to come from the authenticated server-side context, and the only way to
/// guarantee that is to give a caller no way to supply them.
/// </remarks>
public interface IAuditWriter
{
    /// <param name="action"><c>&lt;Module&gt;.&lt;Event&gt;</c>, e.g. <c>Network.StationCreated</c> (docs/20 §2).</param>
    /// <param name="subjectType">A module-declared constant, e.g. <c>Network.Station</c>.</param>
    /// <param name="subjectId">The subject's identifier, or null where the event has no GUID subject.</param>
    /// <param name="before">Prior-state snapshot; null on creation events.</param>
    /// <param name="after">Resulting-state snapshot; null on deletion-style events.</param>
    /// <param name="reasonCode">Operator-supplied reason, for actions that require one.</param>
    void Record(
        string action,
        string subjectType,
        Guid? subjectId,
        object? before,
        object? after,
        string? reasonCode = null);
}

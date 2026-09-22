namespace YCR.Application.Common.Abstractions;

/// <summary>
/// Marks a record as a state payload that may be written into an audit event's
/// <c>BeforeJson</c> or <c>AfterJson</c>.
/// </summary>
/// <remarks>
/// BUSINESS DECISION (hein, 2026-09-20): audit payloads are explicit snapshot records, never
/// entities — and the rule is enforced by the type system rather than by a runtime check.
/// <see cref="IAuditWriter.Record"/> accepts only <see cref="IAuditSnapshot"/>, so passing an
/// aggregate is a compile error rather than an exception discovered on the first call.
/// <para>
/// The marker carries no members on purpose. Its whole job is to say "this shape was designed to
/// be written into a row that can never be edited or redacted". An entity would drag its full
/// surface — navigations, later-added fields, eventually something secret — into that row.
/// </para>
/// <para>
/// An architecture test enforces two things about every implementation: it must be a
/// <see langword="record"/>, so value semantics and a readable <c>ToString</c> come for free and
/// nobody attaches behaviour to a payload; and it must live in <c>YCR.Application.&lt;Module&gt;</c>,
/// never in <c>YCR.Domain</c>, so a snapshot cannot quietly become a domain concept.
/// </para>
/// <para>
/// REQUIRED CONTROL (ADR-0021 rule 4): no implementation may carry a password, token, refresh
/// cookie, full QR payload, private key or personal data. Changing an implementation's shape
/// requires bumping <c>PayloadVersion</c> (ADR-0021 rule 3).
/// </para>
/// </remarks>
public interface IAuditSnapshot;

namespace YCR.Application.Network;

/// <summary>
/// The audit subject types the Network module writes (ADR-0021 <c>SubjectType</c>).
/// </summary>
/// <remarks>
/// BUSINESS DECISION (hein, 2026-09-20): subject types are stable module-prefixed constants,
/// declared per module and never derived from a CLR type name. Rows in an append-only ledger
/// outlive the code that wrote them, so a class rename must not be able to change what a
/// historical row appears to be about — and a reader querying <c>SubjectType</c> must be able to
/// rely on one spelling forever.
/// <para>
/// Changing a value here is a breaking change to recorded history and needs the same scrutiny as
/// a schema change. Adding one is ordinary.
/// </para>
/// </remarks>
public static class NetworkAuditSubjects
{
    public const string Station = "Network.Station";
}

namespace YCR.Application.Timetable;

/// <summary>
/// The audit subject types the Timetable module writes (ADR-0021 <c>SubjectType</c>; F-004 plan P18).
/// </summary>
/// <remarks>
/// BUSINESS DECISION (hein, 2026-09-20): subject types are stable module-prefixed constants, never
/// derived from a CLR type name, because ledger rows outlive the code that wrote them. Changing a
/// value here is a breaking change to recorded history; adding one is ordinary.
/// </remarks>
public static class TimetableAuditSubjects
{
    public const string Service = "Timetable.Service";
}

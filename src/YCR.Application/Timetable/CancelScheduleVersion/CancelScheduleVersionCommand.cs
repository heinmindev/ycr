namespace YCR.Application.Timetable.CancelScheduleVersion;

/// <summary>Cancels a published version before it takes effect (F-005 SV31-SV34, SV41, SV54; R34, R49).</summary>
/// <remarks>Carries no actor field (ADR-0017 item 2; SV49).</remarks>
public sealed record CancelScheduleVersionCommand(Guid ScheduleVersionId);

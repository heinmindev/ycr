namespace YCR.Application.Timetable.DiscardScheduleVersion;

/// <summary>Discards a draft (F-005 SV35; R28).</summary>
/// <remarks>Carries no actor field (ADR-0017 item 2; SV49).</remarks>
public sealed record DiscardScheduleVersionCommand(Guid ScheduleVersionId);

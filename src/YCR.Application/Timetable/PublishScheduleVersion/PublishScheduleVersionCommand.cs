namespace YCR.Application.Timetable.PublishScheduleVersion;

/// <summary>Publishes a draft (F-005 SV17, SV20-SV26, SV53, SV55, SV56; R4, R17, R22, R30, R31, R50).</summary>
/// <remarks>Carries no actor field (ADR-0017 item 2; SV49).</remarks>
public sealed record PublishScheduleVersionCommand(Guid ScheduleVersionId);

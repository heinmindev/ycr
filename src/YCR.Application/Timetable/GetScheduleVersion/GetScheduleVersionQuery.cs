namespace YCR.Application.Timetable.GetScheduleVersion;

/// <summary>Reads one timetable version with its listed services (F-005 SV2).</summary>
public sealed record GetScheduleVersionQuery(Guid ScheduleVersionId);

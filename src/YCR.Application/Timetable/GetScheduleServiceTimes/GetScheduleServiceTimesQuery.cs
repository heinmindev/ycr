namespace YCR.Application.Timetable.GetScheduleServiceTimes;

/// <summary>Reads one listed service's stop times in a timetable version (F-005 SV2).</summary>
public sealed record GetScheduleServiceTimesQuery(Guid ScheduleVersionId, Guid ServiceId);

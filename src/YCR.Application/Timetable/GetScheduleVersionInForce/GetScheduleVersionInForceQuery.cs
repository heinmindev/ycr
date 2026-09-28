namespace YCR.Application.Timetable.GetScheduleVersionInForce;

/// <summary>Reads the timetable version in force on a date (F-005 SV27-SV30, SV53; R18, R21, R48).</summary>
public sealed record GetScheduleVersionInForceQuery(DateOnly Date);

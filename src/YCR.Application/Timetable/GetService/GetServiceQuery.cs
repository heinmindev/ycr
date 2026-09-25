namespace YCR.Application.Timetable.GetService;

/// <summary>Reads one service with its stops (F-004 S2).</summary>
public sealed record GetServiceQuery(Guid ServiceId);

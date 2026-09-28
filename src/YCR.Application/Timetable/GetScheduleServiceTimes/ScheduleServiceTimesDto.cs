namespace YCR.Application.Timetable.GetScheduleServiceTimes;

/// <summary>One listed service's stop times in a version (F-005 spec §6 <c>ScheduleServiceTimesResponse</c>).</summary>
/// <param name="Stops">In position order.</param>
public sealed record ScheduleServiceTimesDto(
    Guid VersionId,
    Guid ServiceId,
    string Code,
    IReadOnlyList<ScheduleStopTimeDto> Stops);

/// <summary>One stop, with the station's current code and names (R43) and its times.</summary>
/// <param name="Arrival"><c>HH:mm</c>, or null at stop 1 (R10).</param>
/// <param name="Departure"><c>HH:mm</c>, or null at the last stop (R10).</param>
public sealed record ScheduleStopTimeDto(
    int Position,
    Guid StationId,
    string StationCode,
    string StationNameEn,
    string StationNameMy,
    string? Arrival,
    string? Departure);

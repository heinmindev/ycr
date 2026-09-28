namespace YCR.Application.Timetable.GetScheduleVersionInForce;

/// <summary>The version in force on a date (F-005 spec §6 <c>ScheduleVersionInForceResponse</c>).</summary>
/// <param name="Services">Every listed service, ordered by code, then id; empty for an empty version (SV53).</param>
public sealed record ScheduleVersionInForceDto(
    DateOnly Date,
    Guid Id,
    int Number,
    string NameEn,
    string NameMy,
    DateOnly EffectiveFrom,
    DateTimeOffset PublishedAtUtc,
    IReadOnlyList<ScheduleServiceRunningDto> Services);

/// <summary>A listed service and whether it runs on the date (R18, R48).</summary>
public sealed record ScheduleServiceRunningDto(Guid ServiceId, string Code, bool RunsOnDate);

namespace YCR.Application.Timetable.ListScheduleVersions;

/// <summary>One row of the version list (F-005 spec §6 <c>ScheduleVersionSummaryResponse</c>).</summary>
public sealed record ScheduleVersionSummaryDto(
    Guid Id,
    int Number,
    string NameEn,
    string NameMy,
    DateOnly EffectiveFrom,
    string Status,
    int ServiceCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? PublishedAtUtc,
    DateTimeOffset? DiscardedAtUtc,
    DateTimeOffset? CancelledAtUtc);

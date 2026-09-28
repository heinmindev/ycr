namespace YCR.Application.Timetable.GetScheduleVersion;

/// <summary>
/// One timetable version (F-005 spec §6 <c>ScheduleVersionResponse</c>). A DTO, not the aggregate:
/// no EF entity leaves the Application layer (AGENTS.md rule 4).
/// </summary>
/// <param name="Status"><c>Draft</c>, <c>Published</c>, <c>Discarded</c> or <c>Cancelled</c>.</param>
/// <param name="Services">The listed services with their current values, ordered by code, then id.</param>
public sealed record ScheduleVersionDto(
    Guid Id,
    int Number,
    string NameEn,
    string NameMy,
    DateOnly EffectiveFrom,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? PublishedAtUtc,
    DateTimeOffset? DiscardedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    IReadOnlyList<ScheduleVersionServiceDto> Services);

/// <summary>A listed service's current values (spec §6).</summary>
/// <param name="Direction"><c>Forward</c> or <c>Reverse</c>.</param>
public sealed record ScheduleVersionServiceDto(
    Guid ServiceId,
    string Code,
    string NameEn,
    string NameMy,
    string Direction,
    int StopCount,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool NeverRuns);

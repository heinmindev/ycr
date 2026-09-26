namespace YCR.Application.Timetable.ListServices;

/// <summary>One service in a list page (F-004 S3), with its route's current code and its stop count.</summary>
/// <param name="OperatingDays">Day names, Monday first (R40).</param>
/// <param name="NeverRuns">Derived, not stored (R41).</param>
public sealed record ServiceSummaryDto(
    Guid Id,
    string Code,
    string NameEn,
    string NameMy,
    Guid RouteId,
    string RouteCode,
    string Direction,
    int StopCount,
    IReadOnlyList<string> OperatingDays,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool NeverRuns,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? WithdrawnAtUtc);

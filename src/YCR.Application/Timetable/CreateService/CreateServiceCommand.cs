namespace YCR.Application.Timetable.CreateService;

/// <summary>Creates a service with its whole stop list (F-004 S1).</summary>
/// <remarks>
/// Carries no actor, address or correlation field (ADR-0017 item 2; S45). The API validates the
/// shape first (plan P6), so <paramref name="Direction"/> is exactly <c>Forward</c> or
/// <c>Reverse</c> and <paramref name="OperatingDays"/> is non-empty with no repeats; it is a
/// <see cref="string"/> because the Api may not reference <c>YCR.Domain.Timetable</c>.
/// </remarks>
/// <param name="StopStationIds">The stops, as station ids, in stop order.</param>
/// <param name="EffectiveTo">Inclusive; null when open-ended.</param>
public sealed record CreateServiceCommand(
    string Code,
    string NameEn,
    string NameMy,
    Guid RouteId,
    string Direction,
    IReadOnlyList<Guid> StopStationIds,
    IReadOnlyList<DayOfWeek> OperatingDays,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);

using YCR.Domain.Timetable;

namespace YCR.Application.Timetable.GetService;

/// <summary>
/// The shape a service query pulls back from the database, before it becomes a DTO.
/// </summary>
/// <remarks>
/// The <c>StationProjection</c> pattern (ENGINEERING DECISION, hein, 2026-09-21): the whole
/// <see cref="ServiceCode"/>, which EF can translate, never <c>Code.Value</c>; the owned name and
/// the owned operating days as their scalars. The aggregate is never materialised for a read
/// (plan P17).
/// </remarks>
public sealed record ServiceProjection(
    Guid Id,
    ServiceCode Code,
    string NameEn,
    string NameMy,
    Guid RouteId,
    Direction Direction,
    bool RunsOnMonday,
    bool RunsOnTuesday,
    bool RunsOnWednesday,
    bool RunsOnThursday,
    bool RunsOnFriday,
    bool RunsOnSaturday,
    bool RunsOnSunday,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? WithdrawnAtUtc)
{
    public IReadOnlyList<string> OperatingDayNames() => ServiceReadMapping.DayNames(
        RunsOnMonday, RunsOnTuesday, RunsOnWednesday, RunsOnThursday, RunsOnFriday, RunsOnSaturday, RunsOnSunday);

    public ServiceDto ToDto(ServiceRouteDto route, IReadOnlyList<ServiceStopDto> stops) =>
        new(
            Id,
            Code.Value,
            NameEn,
            NameMy,
            ServiceReadMapping.DirectionName(Direction),
            route,
            stops,
            OperatingDayNames(),
            EffectiveFrom,
            EffectiveTo,
            ServiceReadMapping.NeverRuns(EffectiveFrom, EffectiveTo),
            CreatedAtUtc,
            WithdrawnAtUtc);
}

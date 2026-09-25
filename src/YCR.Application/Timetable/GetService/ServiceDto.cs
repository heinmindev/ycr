namespace YCR.Application.Timetable.GetService;

/// <summary>
/// A service as the Application layer hands it out (F-004 S2).
/// </summary>
/// <remarks>
/// Separate from the API's <c>ServiceResponse</c> (F-001 P5): this one is internal, that one is the
/// public contract. No EF entity leaves through the API (AGENTS.md rule 4). Direction and days are
/// strings (plan P17), so the Api needs no Timetable domain type.
/// </remarks>
/// <param name="Stops">In position order.</param>
/// <param name="OperatingDays">Day names, Monday first (R40).</param>
/// <param name="NeverRuns">Derived, not stored (R41).</param>
public sealed record ServiceDto(
    Guid Id,
    string Code,
    string NameEn,
    string NameMy,
    string Direction,
    ServiceRouteDto Route,
    IReadOnlyList<ServiceStopDto> Stops,
    IReadOnlyList<string> OperatingDays,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool NeverRuns,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? WithdrawnAtUtc);

/// <summary>The service's route with its <em>current</em> code, names and flags (R30).</summary>
public sealed record ServiceRouteDto(
    Guid Id,
    string Code,
    string NameEn,
    string NameMy,
    bool IsClosed,
    bool IsActive);

/// <summary>One stop, with the station's <em>current</em> code, names and active flag (R30).</summary>
public sealed record ServiceStopDto(
    int Position,
    Guid StationId,
    string Code,
    string NameEn,
    string NameMy,
    bool IsActive);

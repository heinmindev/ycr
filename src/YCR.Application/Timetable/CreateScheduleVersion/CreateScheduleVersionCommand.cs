namespace YCR.Application.Timetable.CreateScheduleVersion;

/// <summary>Creates a whole draft timetable version (F-005 SV1; R27).</summary>
/// <remarks>
/// Carries no actor, address or correlation field (ADR-0017 item 2; SV49). The API validates the
/// shape first (plan P14, the caps of R41); the names and the <c>HH:mm</c> times travel as text and
/// are decided by the domain, because the spec gives them their own codes (R7, R16).
/// </remarks>
/// <param name="Services">The listed services, in request order; may be empty (R9).</param>
public sealed record CreateScheduleVersionCommand(
    string? NameEn,
    string? NameMy,
    DateOnly EffectiveFrom,
    IReadOnlyList<CreateScheduleServiceItem> Services);

/// <summary>One listed service and its stop times, in request order.</summary>
public sealed record CreateScheduleServiceItem(Guid ServiceId, IReadOnlyList<CreateScheduleStopTimeItem> StopTimes);

/// <summary>One stop time: a position of the service and its times as <c>HH:mm</c> or null.</summary>
public sealed record CreateScheduleStopTimeItem(int Position, string? Arrival, string? Departure);

/// <summary>The new version's id and number (spec §6 <c>CreateScheduleVersionResponse</c>).</summary>
public sealed record CreatedScheduleVersion(Guid Id, int Number);

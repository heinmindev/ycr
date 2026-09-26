using System.Globalization;
using FluentValidation;
using YCR.Application.Timetable.CreateService;
using YCR.Application.Timetable.GetService;
using YCR.Application.Timetable.ListServices;
using YCR.Application.Timetable.WithdrawService;

namespace YCR.Api.Contracts.Timetable;

/// <summary>The body of <c>POST /api/v1/services</c> (F-004 spec §6).</summary>
/// <remarks>
/// Every field that is not a plain string travels as a string (plan P6): a malformed GUID, date,
/// direction or day name must answer <c>400 Common.ValidationFailed</c> (S21), and a typed property
/// would fail JSON binding before the validation filter runs, without that <c>errorCode</c>.
/// <para>
/// Carries no actor, address or correlation field, and must not gain one (ADR-0017 item 2; S45).
/// </para>
/// </remarks>
/// <param name="StopStationIds">The stops in order: station ids in GUID <c>D</c> format, 1-200.</param>
/// <param name="OperatingDays">Exact English day names, <c>Monday</c>…<c>Sunday</c>, 1-7, no repeats.</param>
/// <param name="EffectiveFrom">A date, <c>yyyy-MM-dd</c>.</param>
/// <param name="EffectiveTo">Absent, null or a date, <c>yyyy-MM-dd</c>; inclusive.</param>
public sealed record CreateServiceRequest(
    string? Code,
    string? NameEn,
    string? NameMy,
    string? RouteId,
    string? Direction,
    IReadOnlyList<string?>? StopStationIds,
    IReadOnlyList<string?>? OperatingDays,
    string? EffectiveFrom,
    string? EffectiveTo)
{
    /// <summary>Only after <see cref="CreateServiceRequestValidator"/> has passed.</summary>
    public CreateServiceCommand ToCommand() =>
        new(
            Code ?? string.Empty,
            NameEn ?? string.Empty,
            NameMy ?? string.Empty,
            Guid.ParseExact(RouteId!, "D"),
            Direction!,
            [.. (StopStationIds ?? []).Select(id => Guid.ParseExact(id!, "D"))],
            [.. (OperatingDays ?? []).Select(day => ServiceRequestFormats.Days[day!])],
            ServiceRequestFormats.ParseDate(EffectiveFrom!),
            EffectiveTo is null ? null : ServiceRequestFormats.ParseDate(EffectiveTo));
}

/// <summary>
/// Shape-level validation only (docs/20 §3; F-004 plan P6).
/// </summary>
/// <remarks>
/// Presence and form. Whether a code, a name or a period is <em>valid</em>, and every stop rule,
/// stays in <c>ServiceCode</c>, <c>BilingualName</c>, <c>EffectivePeriod</c> and
/// <c>Service.Create</c>, the sole homes of the provisional OQ43-OQ50 rulings.
/// </remarks>
public sealed class CreateServiceRequestValidator : AbstractValidator<CreateServiceRequest>
{
    /// <summary>
    /// REQUIRED CONTROL (R31; OQ45 ruling; spec §0.10 Q2 ruling, hein, 2026-09-25): at most 200 stop
    /// ids per request, refused before the handler runs, so nothing is read or written. A full
    /// circuit of a 200-station route (201 stops) is therefore not supported: a known limitation.
    /// </summary>
    public const int MaxStopStationIds = 200;

    public CreateServiceRequestValidator()
    {
        RuleFor(request => request.Code).NotEmpty();
        RuleFor(request => request.NameEn).NotEmpty();
        RuleFor(request => request.NameMy).NotEmpty();

        RuleFor(request => request.RouteId)
            .NotEmpty()
            .Must(ServiceRequestFormats.IsGuid)
            .WithMessage("routeId must be a GUID in the form 00000000-0000-0000-0000-000000000000.");

        // R9: required, no default, exactly one of the two stored names.
        RuleFor(request => request.Direction)
            .NotEmpty()
            .Must(direction => direction is "Forward" or "Reverse")
            .WithMessage("direction must be exactly Forward or Reverse.");

        RuleFor(request => request.StopStationIds)
            .NotEmpty()
            .Must(ids => ids is null || ids.Count <= MaxStopStationIds)
            .WithMessage($"At most {MaxStopStationIds} stop station ids are allowed.");
        RuleForEach(request => request.StopStationIds)
            .Must(ServiceRequestFormats.IsGuid)
            .WithMessage("Each stop station id must be a GUID in the form 00000000-0000-0000-0000-000000000000.");

        // R17: at least one day, no repeats, exact names.
        RuleFor(request => request.OperatingDays)
            .NotEmpty()
            .Must(days => days is null || (days.Count <= 7 && days.Distinct(StringComparer.Ordinal).Count() == days.Count))
            .WithMessage("operatingDays must not repeat a day.");
        RuleForEach(request => request.OperatingDays)
            .Must(day => day is not null && ServiceRequestFormats.Days.ContainsKey(day))
            .WithMessage("Each operating day must be exactly one of Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday.");

        RuleFor(request => request.EffectiveFrom)
            .NotEmpty()
            .Must(ServiceRequestFormats.IsDate)
            .WithMessage("effectiveFrom must be a date in the form YYYY-MM-DD.");
        RuleFor(request => request.EffectiveTo)
            .Must(to => to is null || ServiceRequestFormats.IsDate(to))
            .WithMessage("effectiveTo must be absent, null or a date in the form YYYY-MM-DD.");
    }
}

/// <summary>The body of <c>POST /api/v1/services/{id}/withdraw</c> (F-004 spec §6).</summary>
/// <param name="WithdrawFrom">The first date on which the service no longer runs, <c>yyyy-MM-dd</c>.</param>
public sealed record WithdrawServiceRequest(string? WithdrawFrom)
{
    /// <summary>Only after <see cref="WithdrawServiceRequestValidator"/> has passed.</summary>
    public WithdrawServiceCommand ToCommand(Guid serviceId) =>
        new(serviceId, ServiceRequestFormats.ParseDate(WithdrawFrom!));
}

/// <summary>Shape-level validation only (S38).</summary>
public sealed class WithdrawServiceRequestValidator : AbstractValidator<WithdrawServiceRequest>
{
    public WithdrawServiceRequestValidator()
    {
        RuleFor(request => request.WithdrawFrom)
            .NotEmpty()
            .Must(ServiceRequestFormats.IsDate)
            .WithMessage("withdrawFrom must be a date in the form YYYY-MM-DD.");
    }
}

/// <summary>The exact formats the service requests accept (plan P6).</summary>
internal static class ServiceRequestFormats
{
    /// <summary>
    /// Exact, case-sensitive English names. <c>Enum.TryParse&lt;DayOfWeek&gt;</c> is not used: it
    /// would accept <c>"1"</c> and ignore case (plan P6).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, DayOfWeek> Days = new Dictionary<string, DayOfWeek>(StringComparer.Ordinal)
    {
        ["Monday"] = DayOfWeek.Monday,
        ["Tuesday"] = DayOfWeek.Tuesday,
        ["Wednesday"] = DayOfWeek.Wednesday,
        ["Thursday"] = DayOfWeek.Thursday,
        ["Friday"] = DayOfWeek.Friday,
        ["Saturday"] = DayOfWeek.Saturday,
        ["Sunday"] = DayOfWeek.Sunday,
    };

    public static bool IsGuid(string? value) => Guid.TryParseExact(value, "D", out _);

    public static bool IsDate(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    public static DateOnly ParseDate(string value) =>
        DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>The body of a successful <c>POST /api/v1/services</c>.</summary>
public sealed record CreateServiceResponse(Guid Id);

/// <summary>A service as the API returns it (F-004 S2).</summary>
/// <remarks>
/// Mapped explicitly from <see cref="ServiceDto"/>; no EF entity leaves through the API (AGENTS.md
/// rule 4). No time field (R22) and no version token (E10).
/// </remarks>
public sealed record ServiceResponse(
    Guid Id,
    string Code,
    string NameEn,
    string NameMy,
    string Direction,
    ServiceRouteResponse Route,
    IReadOnlyList<ServiceStopResponse> Stops,
    IReadOnlyList<string> OperatingDays,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool NeverRuns,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? WithdrawnAtUtc)
{
    public static ServiceResponse From(ServiceDto service)
    {
        ArgumentNullException.ThrowIfNull(service);

        return new ServiceResponse(
            service.Id,
            service.Code,
            service.NameEn,
            service.NameMy,
            service.Direction,
            ServiceRouteResponse.From(service.Route),
            [.. service.Stops.Select(ServiceStopResponse.From)],
            service.OperatingDays,
            service.EffectiveFrom,
            service.EffectiveTo,
            service.NeverRuns,
            service.CreatedAtUtc,
            service.WithdrawnAtUtc);
    }
}

/// <summary>The service's route with its current values (R30).</summary>
public sealed record ServiceRouteResponse(
    Guid Id,
    string Code,
    string NameEn,
    string NameMy,
    bool IsClosed,
    bool IsActive)
{
    public static ServiceRouteResponse From(ServiceRouteDto route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return new ServiceRouteResponse(route.Id, route.Code, route.NameEn, route.NameMy, route.IsClosed, route.IsActive);
    }
}

/// <summary>One stop, with the station's current values (R30).</summary>
public sealed record ServiceStopResponse(
    int Position,
    Guid StationId,
    string Code,
    string NameEn,
    string NameMy,
    bool IsActive)
{
    public static ServiceStopResponse From(ServiceStopDto stop)
    {
        ArgumentNullException.ThrowIfNull(stop);

        return new ServiceStopResponse(stop.Position, stop.StationId, stop.Code, stop.NameEn, stop.NameMy, stop.IsActive);
    }
}

/// <summary>One service in <c>GET /api/v1/services</c> (F-004 S3).</summary>
public sealed record ServiceSummaryResponse(
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
    DateTimeOffset? WithdrawnAtUtc)
{
    public static ServiceSummaryResponse From(ServiceSummaryDto service)
    {
        ArgumentNullException.ThrowIfNull(service);

        return new ServiceSummaryResponse(
            service.Id,
            service.Code,
            service.NameEn,
            service.NameMy,
            service.RouteId,
            service.RouteCode,
            service.Direction,
            service.StopCount,
            service.OperatingDays,
            service.EffectiveFrom,
            service.EffectiveTo,
            service.NeverRuns,
            service.CreatedAtUtc,
            service.WithdrawnAtUtc);
    }
}

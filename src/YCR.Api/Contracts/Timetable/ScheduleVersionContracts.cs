using FluentValidation;
using YCR.Application.Timetable.CreateScheduleVersion;
using YCR.Application.Timetable.GetScheduleServiceTimes;
using YCR.Application.Timetable.GetScheduleVersion;
using YCR.Application.Timetable.GetScheduleVersionInForce;
using YCR.Application.Timetable.ListScheduleVersions;

namespace YCR.Api.Contracts.Timetable;

/// <summary>The body of <c>POST /api/v1/schedules/versions</c> (F-005 spec §6; plan P14).</summary>
/// <remarks>
/// A draft is created whole, with every listed service and all its times (OQ56). Fields that are not
/// plain strings travel as strings (plan P14), so a malformed date or GUID is
/// <c>400 Common.ValidationFailed</c> rather than a framework binding error. Names and <c>HH:mm</c>
/// times are not checked here: the domain decides them with their own codes (R7, R16).
/// <para>Carries no actor, address or correlation field (ADR-0017 item 2; SV49).</para>
/// </remarks>
/// <param name="EffectiveFrom">The start date, <c>yyyy-MM-dd</c>.</param>
/// <param name="Services">Required; may be empty (R9, R50).</param>
public sealed record CreateScheduleVersionRequest(
    string? NameEn,
    string? NameMy,
    string? EffectiveFrom,
    IReadOnlyList<ScheduleServiceRequest?>? Services)
{
    /// <summary>Only after <see cref="CreateScheduleVersionRequestValidator"/> has passed.</summary>
    public CreateScheduleVersionCommand ToCommand() =>
        new(
            NameEn,
            NameMy,
            ServiceRequestFormats.ParseDate(EffectiveFrom!),
            [.. Services!.Select(service => new CreateScheduleServiceItem(
                Guid.ParseExact(service!.ServiceId!, "D"),
                [.. service.StopTimes!.Select(stop => new CreateScheduleStopTimeItem(stop!.Position!.Value, stop.Arrival, stop.Departure))]))]);
}

/// <summary>One listed service and its stop times.</summary>
/// <param name="ServiceId">A GUID in <c>D</c> format.</param>
public sealed record ScheduleServiceRequest(string? ServiceId, IReadOnlyList<ScheduleStopTimeRequest?>? StopTimes);

/// <summary>One stop time: a position of the service (1..k) and <c>HH:mm</c> times or null.</summary>
public sealed record ScheduleStopTimeRequest(int? Position, string? Arrival, string? Departure);

/// <summary>
/// Shape-level validation only (docs/20 §3; F-005 plan P14, P15, §Contracts).
/// </summary>
public sealed class CreateScheduleVersionRequestValidator : AbstractValidator<CreateScheduleVersionRequest>
{
    /// <summary>
    /// REQUIRED CONTROL (R41; plan P15, ruling Q1, hein, 2026-09-27): at most 250 services per
    /// version, refused before the handler runs, so nothing is read or written.
    /// </summary>
    public const int MaxServicesPerVersion = 250;

    /// <summary>
    /// REQUIRED CONTROL (R41; plan P15): at most 200 stop times per service — F-004's stop cap, one
    /// time row per stop.
    /// </summary>
    public const int MaxStopTimesPerService = 200;

    /// <summary>
    /// REQUIRED CONTROL (R41; plan P15, ruling Q1, hein, 2026-09-27; spec Amendment 3): at most
    /// 10,000 stop times in one version (250 × 40), which keeps the largest valid body under the
    /// 2 MiB limit.
    /// </summary>
    public const int MaxStopTimesPerVersion = 10_000;

    public CreateScheduleVersionRequestValidator()
    {
        RuleFor(request => request.EffectiveFrom)
            .NotEmpty()
            .Must(ServiceRequestFormats.IsDate)
            .WithMessage("effectiveFrom must be a date in the form YYYY-MM-DD.");

        // R9: the array is required, and may be empty.
        RuleFor(request => request.Services)
            .NotNull()
            .Must(services => services is null || services.Count <= MaxServicesPerVersion)
            .WithMessage($"At most {MaxServicesPerVersion} services are allowed in one version.")
            .Must(services => services is null
                || services.Sum(service => service?.StopTimes?.Count ?? 0) <= MaxStopTimesPerVersion)
            .WithMessage($"At most {MaxStopTimesPerVersion} stop times are allowed in one version.");

        RuleForEach(request => request.Services)
            .NotNull()
            .WithMessage("A service entry must not be null.")
            .ChildRules(service =>
            {
                service.RuleFor(entry => entry!.ServiceId)
                    .NotEmpty()
                    .Must(ServiceRequestFormats.IsGuid)
                    .WithMessage("serviceId must be a GUID in the form 00000000-0000-0000-0000-000000000000.");
                service.RuleFor(entry => entry!.StopTimes)
                    .NotNull()
                    .Must(stopTimes => stopTimes is null || stopTimes.Count <= MaxStopTimesPerService)
                    .WithMessage($"At most {MaxStopTimesPerService} stop times are allowed for one service.");
                service.RuleForEach(entry => entry!.StopTimes)
                    .NotNull()
                    .WithMessage("A stop time must not be null.")
                    .ChildRules(stopTime => stopTime.RuleFor(time => time!.Position)
                        .NotNull()
                        .GreaterThanOrEqualTo(1)
                        .WithMessage("position must be 1 or greater."))
                    .When(entry => entry is not null);
            })
            .When(request => request.Services is not null);
    }
}

/// <summary>The query of <c>GET /api/v1/schedules/versions</c> (spec §6; plan P17).</summary>
/// <param name="Status">Absent, or exactly <c>Draft</c>, <c>Published</c>, <c>Discarded</c> or <c>Cancelled</c>.</param>
public sealed record ListScheduleVersionsRequest(int Page = 1, int PageSize = 50, string? Status = null)
{
    public ListScheduleVersionsQuery ToQuery() => new(Page, PageSize, Status);
}

/// <summary>An unknown status is <c>400 Common.ValidationFailed</c> (ruling Q2; spec Amendment 3).</summary>
public sealed class ListScheduleVersionsRequestValidator : AbstractValidator<ListScheduleVersionsRequest>
{
    public ListScheduleVersionsRequestValidator()
    {
        RuleFor(request => request.Status)
            .Must(status => status is null or "Draft" or "Published" or "Discarded" or "Cancelled")
            .WithMessage("status must be exactly one of Draft, Published, Discarded, Cancelled.");
    }
}

/// <summary>The query of <c>GET /api/v1/schedules/versions/in-force</c> (spec §6; plan P16).</summary>
/// <param name="Date">Required, <c>yyyy-MM-dd</c>; bound as text so a malformed value is <c>400 Common.ValidationFailed</c>.</param>
public sealed record ScheduleVersionInForceRequest(string? Date)
{
    public GetScheduleVersionInForceQuery ToQuery() => new(ServiceRequestFormats.ParseDate(Date!));
}

/// <summary>SV48: <c>date</c> present and exactly <c>yyyy-MM-dd</c>.</summary>
public sealed class ScheduleVersionInForceRequestValidator : AbstractValidator<ScheduleVersionInForceRequest>
{
    public ScheduleVersionInForceRequestValidator()
    {
        RuleFor(request => request.Date)
            .NotEmpty()
            .Must(ServiceRequestFormats.IsDate)
            .WithMessage("date must be a date in the form YYYY-MM-DD.");
    }
}

/// <summary>The body of a successful <c>POST /api/v1/schedules/versions</c>.</summary>
public sealed record CreateScheduleVersionResponse(Guid Id, int Number);

/// <summary>A timetable version as the API returns it (spec §6), mapped from <see cref="ScheduleVersionDto"/>.</summary>
public sealed record ScheduleVersionResponse(
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
    IReadOnlyList<ScheduleVersionServiceResponse> Services)
{
    public static ScheduleVersionResponse From(ScheduleVersionDto version)
    {
        ArgumentNullException.ThrowIfNull(version);

        return new ScheduleVersionResponse(
            version.Id,
            version.Number,
            version.NameEn,
            version.NameMy,
            version.EffectiveFrom,
            version.Status,
            version.CreatedAtUtc,
            version.PublishedAtUtc,
            version.DiscardedAtUtc,
            version.CancelledAtUtc,
            [.. version.Services.Select(service => new ScheduleVersionServiceResponse(
                service.ServiceId,
                service.Code,
                service.NameEn,
                service.NameMy,
                service.Direction,
                service.StopCount,
                service.EffectiveFrom,
                service.EffectiveTo,
                service.NeverRuns))]);
    }
}

/// <summary>A listed service with its current values.</summary>
public sealed record ScheduleVersionServiceResponse(
    Guid ServiceId,
    string Code,
    string NameEn,
    string NameMy,
    string Direction,
    int StopCount,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool NeverRuns);

/// <summary>One listed service's stop times (spec §6).</summary>
public sealed record ScheduleServiceTimesResponse(
    Guid VersionId,
    Guid ServiceId,
    string Code,
    IReadOnlyList<ScheduleStopTimeResponse> Stops)
{
    public static ScheduleServiceTimesResponse From(ScheduleServiceTimesDto times)
    {
        ArgumentNullException.ThrowIfNull(times);

        return new ScheduleServiceTimesResponse(
            times.VersionId,
            times.ServiceId,
            times.Code,
            [.. times.Stops.Select(stop => new ScheduleStopTimeResponse(
                stop.Position,
                stop.StationId,
                stop.StationCode,
                stop.StationNameEn,
                stop.StationNameMy,
                stop.Arrival,
                stop.Departure))]);
    }
}

/// <summary>One stop, with the station's current code and names, and <c>HH:mm</c> times or null.</summary>
public sealed record ScheduleStopTimeResponse(
    int Position,
    Guid StationId,
    string StationCode,
    string StationNameEn,
    string StationNameMy,
    string? Arrival,
    string? Departure);

/// <summary>One version in <c>GET /api/v1/schedules/versions</c> (spec §6).</summary>
public sealed record ScheduleVersionSummaryResponse(
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
    DateTimeOffset? CancelledAtUtc)
{
    public static ScheduleVersionSummaryResponse From(ScheduleVersionSummaryDto version)
    {
        ArgumentNullException.ThrowIfNull(version);

        return new ScheduleVersionSummaryResponse(
            version.Id,
            version.Number,
            version.NameEn,
            version.NameMy,
            version.EffectiveFrom,
            version.Status,
            version.ServiceCount,
            version.CreatedAtUtc,
            version.PublishedAtUtc,
            version.DiscardedAtUtc,
            version.CancelledAtUtc);
    }
}

/// <summary>The version in force on a date (spec §6).</summary>
public sealed record ScheduleVersionInForceResponse(
    DateOnly Date,
    Guid Id,
    int Number,
    string NameEn,
    string NameMy,
    DateOnly EffectiveFrom,
    DateTimeOffset PublishedAtUtc,
    IReadOnlyList<ScheduleServiceRunningResponse> Services)
{
    public static ScheduleVersionInForceResponse From(ScheduleVersionInForceDto inForce)
    {
        ArgumentNullException.ThrowIfNull(inForce);

        return new ScheduleVersionInForceResponse(
            inForce.Date,
            inForce.Id,
            inForce.Number,
            inForce.NameEn,
            inForce.NameMy,
            inForce.EffectiveFrom,
            inForce.PublishedAtUtc,
            [.. inForce.Services.Select(service => new ScheduleServiceRunningResponse(service.ServiceId, service.Code, service.RunsOnDate))]);
    }
}

/// <summary>A listed service and whether it runs on the date (R48).</summary>
public sealed record ScheduleServiceRunningResponse(Guid ServiceId, string Code, bool RunsOnDate);

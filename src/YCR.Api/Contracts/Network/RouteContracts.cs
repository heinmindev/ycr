using FluentValidation;
using YCR.Application.Network.CreateRoute;
using YCR.Application.Network.GetRoute;
using YCR.Application.Network.ListRoutes;

namespace YCR.Api.Contracts.Network;

/// <summary>The body of <c>POST /api/v1/routes</c> (F-003 spec §6).</summary>
/// <remarks>
/// <c>StationIds</c> travels as strings, not <c>Guid</c>s (plan P6): a malformed id must answer
/// <c>400 Common.ValidationFailed</c> (S5), and a <c>Guid[]</c> would fail JSON binding before the
/// validation filter runs, without that <c>errorCode</c>. <c>IsClosed</c> is nullable so that a
/// missing value is refused rather than defaulted (R25).
/// <para>
/// Carries no actor, address or correlation field, and must not gain one (ADR-0017 item 2; S22).
/// </para>
/// </remarks>
/// <param name="StationIds">The complete sequence, in order: station ids in GUID <c>D</c> format.</param>
public sealed record CreateRouteRequest(
    string? Code,
    string? NameEn,
    string? NameMy,
    bool? IsClosed,
    IReadOnlyList<string?>? StationIds)
{
    /// <summary>Only after <see cref="CreateRouteRequestValidator"/> has passed.</summary>
    public CreateRouteCommand ToCommand() =>
        new(
            Code ?? string.Empty,
            NameEn ?? string.Empty,
            NameMy ?? string.Empty,
            IsClosed ?? throw new InvalidOperationException("IsClosed is validated as present."),
            [.. (StationIds ?? []).Select(id => Guid.ParseExact(id!, "D"))]);
}

/// <summary>
/// Shape-level validation only (docs/20 §3; F-003 plan P6).
/// </summary>
/// <remarks>
/// Presence and form. Whether a code or a name is <em>valid</em>, and every sequence rule, stays
/// in <c>RouteCode</c>, <c>BilingualName</c> and <c>Route.Create</c>, the sole homes of the
/// provisional OQ37/OQ41 rulings.
/// </remarks>
public sealed class CreateRouteRequestValidator : AbstractValidator<CreateRouteRequest>
{
    /// <summary>
    /// REQUIRED CONTROL (R26; spec Amendment 2, hein, 2026-09-24, T-034 Q2): at most 200 station
    /// ids per request, refused before the handler runs, so nothing is read or written. An
    /// input-size limit against API abuse (<c>docs/18</c>), not a statement about how long a real
    /// route can be.
    /// </summary>
    public const int MaxStationIds = 200;

    public CreateRouteRequestValidator()
    {
        RuleFor(request => request.Code).NotEmpty();
        RuleFor(request => request.NameEn).NotEmpty();
        RuleFor(request => request.NameMy).NotEmpty();
        RuleFor(request => request.IsClosed).NotNull();

        RuleFor(request => request.StationIds)
            .NotEmpty()
            .Must(ids => ids is null || ids.Count <= MaxStationIds)
            .WithMessage($"At most {MaxStationIds} station ids are allowed.");

        RuleForEach(request => request.StationIds)
            .Must(id => Guid.TryParseExact(id, "D", out _))
            .WithMessage("Each station id must be a GUID in the form 00000000-0000-0000-0000-000000000000.");
    }
}

/// <summary>The body of a successful <c>POST /api/v1/routes</c>.</summary>
public sealed record CreateRouteResponse(Guid Id);

/// <summary>A route as the API returns it (F-003 S2).</summary>
/// <remarks>
/// Mapped explicitly from <see cref="RouteDto"/>; no EF entity leaves through the API
/// (AGENTS.md rule 4). <c>DeactivatedAtUtc</c> is null while the route is active (A1). No version
/// token (R17).
/// </remarks>
public sealed record RouteResponse(
    Guid Id,
    string Code,
    string NameEn,
    string NameMy,
    bool IsClosed,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DeactivatedAtUtc,
    IReadOnlyList<RouteStationResponse> Stations)
{
    public static RouteResponse From(RouteDto route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return new RouteResponse(
            route.Id,
            route.Code,
            route.NameEn,
            route.NameMy,
            route.IsClosed,
            route.IsActive,
            route.CreatedAtUtc,
            route.DeactivatedAtUtc,
            [.. route.Stations.Select(RouteStationResponse.From)]);
    }
}

/// <summary>One position of a route, with the station's current values (R24).</summary>
public sealed record RouteStationResponse(
    int Position,
    Guid StationId,
    string Code,
    string NameEn,
    string NameMy,
    bool IsActive)
{
    public static RouteStationResponse From(RouteStationDto station)
    {
        ArgumentNullException.ThrowIfNull(station);

        return new RouteStationResponse(
            station.Position,
            station.StationId,
            station.Code,
            station.NameEn,
            station.NameMy,
            station.IsActive);
    }
}

/// <summary>One route in <c>GET /api/v1/routes</c> (F-003 S3).</summary>
public sealed record RouteSummaryResponse(
    Guid Id,
    string Code,
    string NameEn,
    string NameMy,
    bool IsClosed,
    bool IsActive,
    int StationCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DeactivatedAtUtc)
{
    public static RouteSummaryResponse From(RouteSummaryDto route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return new RouteSummaryResponse(
            route.Id,
            route.Code,
            route.NameEn,
            route.NameMy,
            route.IsClosed,
            route.IsActive,
            route.StationCount,
            route.CreatedAtUtc,
            route.DeactivatedAtUtc);
    }
}

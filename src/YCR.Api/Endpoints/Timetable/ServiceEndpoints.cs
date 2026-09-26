using Microsoft.AspNetCore.Mvc;
using YCR.Api.Common;
using YCR.Api.Contracts.Network;
using YCR.Api.Contracts.Timetable;
using YCR.Application.Common.Authorization;
using YCR.Application.Timetable.GetService;
using YCR.Application.Timetable.ListServices;
using YCR.Application.Timetable.CreateService;
using YCR.Application.Timetable.WithdrawService;

namespace YCR.Api.Endpoints.Timetable;

/// <summary>
/// The service endpoints (F-004 spec §6; plan §API changes).
/// </summary>
/// <remarks>
/// The <c>RouteEndpoints</c> shape: request to command, handler, <c>Result</c> to <c>IResult</c>.
/// No status code is chosen here (<see cref="ResultExtensions"/> owns the ADR-0004 mapping) and no
/// business rule is evaluated here (AGENTS.md rule 3). Every route carries
/// <c>.RequireAuthorization(&lt;permission&gt;)</c>; there is no anonymous service endpoint.
/// <para>
/// Deliberately absent (spec §6): <c>PATCH</c>/<c>PUT /services/{id}</c> and any other edit
/// (R20), reactivation, <c>DELETE</c> (R33), <c>/trains</c> (R5), <c>/schedules/versions*</c> and
/// any time field (R22). No endpoint takes an <c>Idempotency-Key</c> (R25).
/// </para>
/// </remarks>
public static class ServiceEndpoints
{
    /// <summary>
    /// The request-body limit of <c>POST /services</c>, in bytes: 32 KiB (R31; plan P20; REQUIRED
    /// CONTROL).
    /// </summary>
    /// <remarks>
    /// The largest valid body — 200 quoted GUIDs, all seven days, two 100-character names escaped
    /// as <c>\uXXXX</c> — is 9,280 bytes (plan §API changes), so 32 KiB leaves 3.5 times that. It
    /// is endpoint metadata (<see cref="RequestSizeLimitAttribute"/>): endpoint routing copies it
    /// into Kestrel's <c>IHttpMaxRequestBodySizeFeature</c> before the body is read, so a larger
    /// body is refused with <c>413</c> before JSON binding allocates it. Every other endpoint keeps
    /// the server default (T-042).
    /// </remarks>
    public const long CreateServiceMaxRequestBodyBytes = 32 * 1024;

    /// <summary>
    /// The request-body limit of <c>POST /services/{id}/withdraw</c>, in bytes: 1 KiB (R31; plan
    /// P20; REQUIRED CONTROL). The only valid body, <c>{"withdrawFrom":"2026-11-01"}</c>, is 29 bytes.
    /// </summary>
    public const long WithdrawServiceMaxRequestBodyBytes = 1024;

    public static RouteGroupBuilder MapServiceEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        var services = api.MapGroup("/services").WithTags("Services");

        services.MapPost("/", async (
                CreateServiceRequest request,
                CreateServiceHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.Handle(request.ToCommand(), cancellationToken))
                .ToHttpResult(id => Results.Created(
                    $"/api/v1/services/{id}",
                    new CreateServiceResponse(id))))
            .RequireAuthorization(Permissions.ServicesManage)
            .WithMetadata(new RequestSizeLimitAttribute(CreateServiceMaxRequestBodyBytes))
            .AddEndpointFilter<ValidationFilter<CreateServiceRequest>>()
            .WithName("CreateService")
            .WithSummary(
                "Creates a service with its whole stop list. stopStationIds are station ids in GUID D format, "
                + "in stop order, at most 200; operatingDays are Monday..Sunday; dates are YYYY-MM-DD. "
                + "A service can afterwards only be withdrawn.")
            .Produces<CreateServiceResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        services.MapGet("/{id:guid}", async (
                Guid id,
                GetServiceHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.Handle(new GetServiceQuery(id), cancellationToken))
                .ToHttpResult(service => Results.Ok(ServiceResponse.From(service))))
            .RequireAuthorization(Permissions.ServicesRead)
            .WithName("GetService")
            .WithSummary("Reads one service with its stops in position order, showing the route's and each station's current values.")
            .Produces<ServiceResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        services.MapGet("/", async (
                ListServicesHandler handler,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 50,
                Guid? routeId = null) =>
                (await handler.Handle(new ListServicesQuery(page, pageSize, routeId), cancellationToken))
                .ToHttpResult(result => Results.Ok(new PagedResponse<ServiceSummaryResponse>(
                    [.. result.Items.Select(ServiceSummaryResponse.From)],
                    result.Page,
                    result.PageSize,
                    result.TotalCount))))
            .RequireAuthorization(Permissions.ServicesRead)
            .WithName("ListServices")
            .WithSummary(
                "Lists services, withdrawn ones included, ordered by code, then effectiveFrom, then id. "
                + "routeId filters to one route; pageSize is capped at 200.")
            .Produces<PagedResponse<ServiceSummaryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        services.MapPost("/{id:guid}/withdraw", async (
                Guid id,
                WithdrawServiceRequest request,
                WithdrawServiceHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.Handle(request.ToCommand(id), cancellationToken))
                .ToHttpResult(Results.NoContent))
            .RequireAuthorization(Permissions.ServicesManage)
            .WithMetadata(new RequestSizeLimitAttribute(WithdrawServiceMaxRequestBodyBytes))
            .AddEndpointFilter<ValidationFilter<WithdrawServiceRequest>>()
            .WithName("WithdrawService")
            .WithSummary(
                "Withdraws a service from withdrawFrom (YYYY-MM-DD), the first date it no longer runs: "
                + "effectiveTo becomes the day before. It may only shorten the period.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return services;
    }
}

using Microsoft.AspNetCore.Mvc;
using YCR.Api.Common;
using YCR.Api.Contracts.Network;
using YCR.Application.Common.Authorization;
using YCR.Application.Network.CreateStation;
using YCR.Application.Network.DeactivateStation;
using YCR.Application.Network.GetStation;
using YCR.Application.Network.ListStations;

namespace YCR.Api.Endpoints.Network;

/// <summary>
/// The station endpoints (docs/20 §1 fixes this path).
/// </summary>
/// <remarks>
/// Every endpoint is a thin translation: request to command, handler, <c>Result</c> to
/// <c>IResult</c>. No status code is chosen here — <see cref="ResultExtensions"/> owns the
/// ADR-0004 mapping — and no business rule is evaluated here, which is AGENTS.md rule 3.
/// <para>
/// Each route carries <c>.RequireAuthorization(&lt;permission&gt;)</c> (docs/20 §4). There is no
/// anonymous station endpoint.
/// </para>
/// </remarks>
public static class StationEndpoints
{
    public static RouteGroupBuilder MapStationEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        var stations = api.MapGroup("/stations").WithTags("Stations");

        stations.MapPost("/", async (
                CreateStationRequest request,
                CreateStationHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.Handle(request.ToCommand(), cancellationToken))
                .ToHttpResult(id => Results.Created(
                    $"/api/v1/stations/{id}",
                    new CreateStationResponse(id))))
            .RequireAuthorization(Permissions.StationsManage)
            .WithMetadata(new RequestSizeLimitAttribute(RequestLimits.SmallJsonBodyMaxBytes))
            .AddEndpointFilter<ValidationFilter<CreateStationRequest>>()
            .WithName("CreateStation")
            .WithSummary("Creates a station.")
            .Produces<CreateStationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        stations.MapPost("/{id:guid}/deactivate", async (
                Guid id,
                DeactivateStationHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.Handle(new DeactivateStationCommand(id), cancellationToken))
                .ToHttpResult(Results.NoContent))
            .RequireAuthorization(Permissions.StationsManage)
            .WithName("DeactivateStation")
            .WithSummary("Deactivates a station. Retains the row, so its code is never reused.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        stations.MapGet("/{id:guid}", async (
                Guid id,
                GetStationHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.Handle(new GetStationQuery(id), cancellationToken))
                .ToHttpResult(station => Results.Ok(StationResponse.From(station))))
            .RequireAuthorization(Permissions.StationsRead)
            .WithName("GetStation")
            .WithSummary("Reads one station.")
            .Produces<StationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        stations.MapGet("/", async (
                ListStationsHandler handler,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 50) =>
                (await handler.Handle(new ListStationsQuery(page, pageSize), cancellationToken))
                .ToHttpResult(result => Results.Ok(new PagedResponse<StationResponse>(
                    [.. result.Items.Select(StationResponse.From)],
                    result.Page,
                    result.PageSize,
                    result.TotalCount))))
            .RequireAuthorization(Permissions.StationsRead)
            .WithName("ListStations")
            .WithSummary("Lists stations, ordered by code. pageSize is capped at 200.")
            .Produces<PagedResponse<StationResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return stations;
    }
}

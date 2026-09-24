using YCR.Api.Common;
using YCR.Api.Contracts.Network;
using YCR.Application.Common.Authorization;
using YCR.Application.Network.CreateRoute;
using YCR.Application.Network.DeactivateRoute;
using YCR.Application.Network.GetRoute;
using YCR.Application.Network.ListRoutes;

namespace YCR.Api.Endpoints.Network;

/// <summary>
/// The route endpoints (F-003 spec §6; plan §API changes).
/// </summary>
/// <remarks>
/// The <c>StationEndpoints</c> shape: request to command, handler, <c>Result</c> to
/// <c>IResult</c>. No status code is chosen here (<see cref="ResultExtensions"/> owns the ADR-0004
/// mapping) and no business rule is evaluated here (AGENTS.md rule 3). Every route carries
/// <c>.RequireAuthorization(&lt;permission&gt;)</c>; there is no anonymous route endpoint.
/// <para>
/// Deliberately absent (spec §6): <c>PUT /routes/{id}/stations</c> and any other sequence
/// replacement (OQ38), <c>PATCH /routes/{id}</c> (OQ41), reactivation and <c>DELETE</c> (R15).
/// No endpoint takes an <c>Idempotency-Key</c> (R21).
/// </para>
/// </remarks>
public static class RouteEndpoints
{
    public static RouteGroupBuilder MapRouteEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        var routes = api.MapGroup("/routes").WithTags("Routes");

        routes.MapPost("/", async (
                CreateRouteRequest request,
                CreateRouteHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.Handle(request.ToCommand(), cancellationToken))
                .ToHttpResult(id => Results.Created(
                    $"/api/v1/routes/{id}",
                    new CreateRouteResponse(id))))
            .RequireAuthorization(Permissions.RoutesManage)
            .AddEndpointFilter<ValidationFilter<CreateRouteRequest>>()
            .WithName("CreateRoute")
            .WithSummary(
                "Creates a route with its whole station sequence. stationIds are station ids in GUID D format, "
                + "in order, at most 200. The sequence can never be changed afterwards.")
            .Produces<CreateRouteResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        routes.MapPost("/{id:guid}/deactivate", async (
                Guid id,
                DeactivateRouteHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.Handle(new DeactivateRouteCommand(id), cancellationToken))
                .ToHttpResult(Results.NoContent))
            .RequireAuthorization(Permissions.RoutesManage)
            .WithName("DeactivateRoute")
            .WithSummary("Deactivates a route. Retains the row and its sequence, so its code is never reused.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        routes.MapGet("/{id:guid}", async (
                Guid id,
                GetRouteHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.Handle(new GetRouteQuery(id), cancellationToken))
                .ToHttpResult(route => Results.Ok(RouteResponse.From(route))))
            .RequireAuthorization(Permissions.RoutesRead)
            .WithName("GetRoute")
            .WithSummary("Reads one route with its stations in position order, showing each station's current values.")
            .Produces<RouteResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        routes.MapGet("/", async (
                ListRoutesHandler handler,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 50) =>
                (await handler.Handle(new ListRoutesQuery(page, pageSize), cancellationToken))
                .ToHttpResult(result => Results.Ok(new PagedResponse<RouteSummaryResponse>(
                    [.. result.Items.Select(RouteSummaryResponse.From)],
                    result.Page,
                    result.PageSize,
                    result.TotalCount))))
            .RequireAuthorization(Permissions.RoutesRead)
            .WithName("ListRoutes")
            .WithSummary("Lists routes, inactive ones included, ordered by code. pageSize is capped at 200.")
            .Produces<PagedResponse<RouteSummaryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return routes;
    }
}

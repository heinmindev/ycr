using Microsoft.AspNetCore.Mvc;
using YCR.Api.Common;
using YCR.Api.Contracts.Network;
using YCR.Api.Contracts.Timetable;
using YCR.Application.Common.Authorization;
using YCR.Application.Timetable.CancelScheduleVersion;
using YCR.Application.Timetable.CreateScheduleVersion;
using YCR.Application.Timetable.DiscardScheduleVersion;
using YCR.Application.Timetable.GetScheduleServiceTimes;
using YCR.Application.Timetable.GetScheduleVersion;
using YCR.Application.Timetable.GetScheduleVersionInForce;
using YCR.Application.Timetable.ListScheduleVersions;
using YCR.Application.Timetable.PublishScheduleVersion;

namespace YCR.Api.Endpoints.Timetable;

/// <summary>
/// The timetable-version endpoints (F-005 spec §6; plan §API changes).
/// </summary>
/// <remarks>
/// The <c>ServiceEndpoints</c> shape: request to command, handler, <c>Result</c> to <c>IResult</c>.
/// No status code is chosen here (<see cref="ResultExtensions"/> owns the ADR-0004 mapping) and no
/// business rule is evaluated here (AGENTS.md rule 3). Every route carries
/// <c>.RequireAuthorization(&lt;permission&gt;)</c>; there is no anonymous schedule endpoint (R44).
/// <para>
/// <c>/in-force</c> is mapped before <c>/{id:guid}</c>, and cannot match the GUID constraint
/// anyway (plan P16, V5). Deliberately absent (spec §6): <c>PATCH</c>/<c>PUT</c>/<c>DELETE</c> on
/// versions (R25-R27, R32), adding or removing a draft's services (R27), withdrawing a version that
/// has taken effect (R34), <c>schedules.publish</c> (R4). No endpoint takes an
/// <c>Idempotency-Key</c> (R40) or a version token (R47).
/// </para>
/// </remarks>
public static class ScheduleVersionEndpoints
{
    /// <summary>
    /// The request-body limit of <c>POST /schedules/versions</c>, in bytes: 2 MiB (R41; plan P15,
    /// ruling Q1; REQUIRED CONTROL).
    /// </summary>
    /// <remarks>
    /// The largest valid body at the caps (250 services, 10,000 stop times, two 100-character names
    /// escaped as <c>\uXXXX</c>) is at most 568,300 bytes compact (plan §Body limits and caps, O6), so
    /// 2 MiB leaves 3.8 times that. Endpoint metadata (<see cref="RequestSizeLimitAttribute"/>):
    /// endpoint routing copies it into Kestrel's <c>IHttpMaxRequestBodySizeFeature</c> before the body
    /// is read, so a larger body is refused with <c>413</c> before JSON binding. Publish, cancel and
    /// discard take no body and keep the server default (T-042).
    /// </remarks>
    public const long CreateScheduleVersionMaxRequestBodyBytes = 2 * 1024 * 1024;

    public static RouteGroupBuilder MapScheduleVersionEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        var versions = api.MapGroup("/schedules/versions").WithTags("ScheduleVersions");

        versions.MapPost("/", async (
                CreateScheduleVersionRequest request,
                CreateScheduleVersionHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.Handle(request.ToCommand(), cancellationToken))
                .ToHttpResult(created => Results.Created(
                    $"/api/v1/schedules/versions/{created.Id}",
                    new CreateScheduleVersionResponse(created.Id, created.Number))))
            .RequireAuthorization(Permissions.SchedulesManage)
            .WithMetadata(new RequestSizeLimitAttribute(CreateScheduleVersionMaxRequestBodyBytes))
            .AddEndpointFilter<ValidationFilter<CreateScheduleVersionRequest>>()
            .WithName("CreateScheduleVersion")
            .WithSummary(
                "Creates a whole draft timetable version: every listed service with all its stop times. "
                + "effectiveFrom is YYYY-MM-DD; times are HH:mm (00:00-23:59); stop 1 has only a departure and the "
                + "last stop only an arrival. At most 250 services, 200 stop times per service and 10,000 in total; "
                + "an empty services array is allowed only for a start date later than today.")
            .Produces<CreateScheduleVersionResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        versions.MapGet("/in-force", async (
                [AsParameters] ScheduleVersionInForceRequest request,
                GetScheduleVersionInForceHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.Handle(request.ToQuery(), cancellationToken))
                .ToHttpResult(inForce => Results.Ok(ScheduleVersionInForceResponse.From(inForce))))
            .RequireAuthorization(Permissions.SchedulesRead)
            .AddEndpointFilter<ValidationFilter<ScheduleVersionInForceRequest>>()
            .WithName("GetScheduleVersionInForce")
            .WithSummary(
                "Reads the version in force on date (YYYY-MM-DD): the published version with the latest start date "
                + "on or before it, with runsOnDate for each listed service. 404 before the first published version.")
            .Produces<ScheduleVersionInForceResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        versions.MapGet("/{id:guid}", async (
                Guid id,
                GetScheduleVersionHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.Handle(new GetScheduleVersionQuery(id), cancellationToken))
                .ToHttpResult(version => Results.Ok(ScheduleVersionResponse.From(version))))
            .RequireAuthorization(Permissions.SchedulesRead)
            .WithName("GetScheduleVersion")
            .WithSummary("Reads one timetable version with its listed services' current values.")
            .Produces<ScheduleVersionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        versions.MapGet("/{id:guid}/services/{serviceId:guid}", async (
                Guid id,
                Guid serviceId,
                GetScheduleServiceTimesHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.Handle(new GetScheduleServiceTimesQuery(id, serviceId), cancellationToken))
                .ToHttpResult(times => Results.Ok(ScheduleServiceTimesResponse.From(times))))
            .RequireAuthorization(Permissions.SchedulesRead)
            .WithName("GetScheduleServiceTimes")
            .WithSummary("Reads one listed service's stop times in a version, in position order, with HH:mm times or null.")
            .Produces<ScheduleServiceTimesResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        versions.MapGet("/", async (
                [AsParameters] ListScheduleVersionsRequest request,
                ListScheduleVersionsHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.Handle(request.ToQuery(), cancellationToken))
                .ToHttpResult(result => Results.Ok(new PagedResponse<ScheduleVersionSummaryResponse>(
                    [.. result.Items.Select(ScheduleVersionSummaryResponse.From)],
                    result.Page,
                    result.PageSize,
                    result.TotalCount))))
            .RequireAuthorization(Permissions.SchedulesRead)
            .AddEndpointFilter<ValidationFilter<ListScheduleVersionsRequest>>()
            .WithName("ListScheduleVersions")
            .WithSummary(
                "Lists timetable versions, whatever their status, ordered by number. status filters to exactly Draft, "
                + "Published, Discarded or Cancelled; pageSize is capped at 200.")
            .Produces<PagedResponse<ScheduleVersionSummaryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        versions.MapPost("/{id:guid}/publish", async (
                Guid id,
                PublishScheduleVersionHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.Handle(new PublishScheduleVersionCommand(id), cancellationToken))
                .ToHttpResult(Results.NoContent))
            .RequireAuthorization(Permissions.SchedulesManage)
            .WithName("PublishScheduleVersion")
            .WithSummary(
                "Publishes a draft whose start date is not before today (an empty one only if it starts later than "
                + "today). Every listed service is checked again; start dates are unique among published versions.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        versions.MapPost("/{id:guid}/cancel", async (
                Guid id,
                CancelScheduleVersionHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.Handle(new CancelScheduleVersionCommand(id), cancellationToken))
                .ToHttpResult(Results.NoContent))
            .RequireAuthorization(Permissions.SchedulesManage)
            .WithName("CancelScheduleVersion")
            .WithSummary("Cancels a published version while its start date is later than today. It is kept and never applies.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        versions.MapPost("/{id:guid}/discard", async (
                Guid id,
                DiscardScheduleVersionHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.Handle(new DiscardScheduleVersionCommand(id), cancellationToken))
                .ToHttpResult(Results.NoContent))
            .RequireAuthorization(Permissions.SchedulesManage)
            .WithName("DiscardScheduleVersion")
            .WithSummary("Discards a draft. It is kept, stays readable and never applies.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return versions;
    }
}

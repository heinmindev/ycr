using Microsoft.AspNetCore.Mvc;
using YCR.Api.Common;
using YCR.Api.Common.Authentication;
using YCR.Api.Contracts.Identity;
using YCR.Api.Contracts.Network;
using YCR.Application.Common.Authorization;
using YCR.Application.Identity.CreateUser;
using YCR.Application.Identity.DisableUser;
using YCR.Application.Identity.EnableUser;
using YCR.Application.Identity.GetUser;
using YCR.Application.Identity.ListRoles;
using YCR.Application.Identity.ListUserSessions;
using YCR.Application.Identity.ListUsers;
using YCR.Application.Identity.ReplaceUserRoles;
using YCR.Application.Identity.ResetUserPassword;
using YCR.Application.Identity.RevokeSession;
using YCR.Application.Identity.UnlockUser;

namespace YCR.Api.Endpoints.Identity;

/// <summary>
/// Staff-account administration (spec §6.2; D8, D9): <c>/users</c>, <c>/auth-sessions</c>,
/// <c>/roles</c>.
/// </summary>
/// <remarks>
/// Thin translations like every endpoint; every rule — self-target (U5), last administrator (R27),
/// username (R20), password policy (R18) — is in the handlers and the domain. Each route carries
/// <c>.RequireAuthorization(&lt;permission&gt;)</c>, and the permission becomes the audit row's
/// <c>AuthorizedByPermission</c> (ADR-0017). Every error is <c>Identity.*</c> (U1, D14). No
/// endpoint edits role→permission grants (D8).
/// </remarks>
public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        var users = api.MapGroup("/users").WithTags("Users");

        users.MapGet("/", async (
                ListUsersHandler handler,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 50) =>
                (await handler.Handle(new ListUsersQuery(page, pageSize), cancellationToken))
                .ToHttpResult(result => Results.Ok(new PagedResponse<UserResponse>(
                    [.. result.Items.Select(UserResponse.From)],
                    result.Page,
                    result.PageSize,
                    result.TotalCount))))
            .RequireAuthorization(Permissions.UsersRead)
            .WithName("ListUsers")
            .WithSummary("Lists staff accounts, ordered by username. pageSize is capped at 200.")
            .Produces<PagedResponse<UserResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        users.MapGet("/{id:guid}", async (Guid id, GetUserHandler handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new GetUserQuery(id), cancellationToken))
                .ToHttpResult(user => Results.Ok(UserResponse.From(user))))
            .RequireAuthorization(Permissions.UsersRead)
            .WithName("GetUser")
            .WithSummary("Reads one staff account.")
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        users.MapPost("/", async (CreateUserRequest request, CreateUserHandler handler, CancellationToken cancellationToken) =>
                (await handler.Handle(request.ToCommand(), cancellationToken))
                .ToHttpResult(id => Results.Created($"/api/v1/users/{id}", new CreateUserResponse(id))))
            .RequireAuthorization(Permissions.UsersManage)
            .WithMetadata(new RequestSizeLimitAttribute(RequestLimits.SmallJsonBodyMaxBytes))
            .AddEndpointFilter<ValidationFilter<CreateUserRequest>>()
            .WithName("CreateUser")
            .WithSummary("Creates a staff account with a must-change password.")
            .Produces<CreateUserResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        users.MapPost("/{id:guid}/disable", async (Guid id, DisableUserHandler handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new DisableUserCommand(id), cancellationToken)).ToHttpResult(Results.NoContent))
            .RequireAuthorization(Permissions.UsersManage)
            .WithName("DisableUser")
            .WithSummary("Disables an account and revokes all its sessions.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        users.MapPost("/{id:guid}/enable", async (Guid id, EnableUserHandler handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new EnableUserCommand(id), cancellationToken)).ToHttpResult(Results.NoContent))
            .RequireAuthorization(Permissions.UsersManage)
            .WithName("EnableUser")
            .WithSummary("Enables a disabled account.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        users.MapPost("/{id:guid}/unlock", async (Guid id, UnlockUserHandler handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new UnlockUserCommand(id), cancellationToken)).ToHttpResult(Results.NoContent))
            .RequireAuthorization(Permissions.UsersManage)
            .WithName("UnlockUser")
            .WithSummary("Clears a lockout and the failed-attempt count.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        users.MapPut("/{id:guid}/roles", async (
                Guid id,
                ReplaceUserRolesRequest request,
                ReplaceUserRolesHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.Handle(request.ToCommand(id), cancellationToken)).ToHttpResult(Results.NoContent))
            .RequireAuthorization(Permissions.UsersRolesManage)
            .WithMetadata(new RequestSizeLimitAttribute(RequestLimits.SmallJsonBodyMaxBytes))
            .AddEndpointFilter<ValidationFilter<ReplaceUserRolesRequest>>()
            .WithName("ReplaceUserRoles")
            .WithSummary("Replaces an account's roles with the given set.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        users.MapPost("/{id:guid}/password-reset", async (
                Guid id,
                ResetUserPasswordRequest request,
                ResetUserPasswordHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.Handle(request.ToCommand(id), cancellationToken)).ToHttpResult(Results.NoContent))
            .RequireAuthorization(Permissions.UsersManage)
            .WithMetadata(new RequestSizeLimitAttribute(RequestLimits.SmallJsonBodyMaxBytes))
            .AddEndpointFilter<ValidationFilter<ResetUserPasswordRequest>>()
            .WithName("ResetUserPassword")
            .WithSummary("Sets a must-change password and revokes all the account's sessions.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        users.MapGet("/{id:guid}/auth-sessions", async (
                Guid id,
                ListUserSessionsHandler handler,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 50) =>
                (await handler.Handle(new ListUserSessionsQuery(id, page, pageSize), cancellationToken))
                .ToHttpResult(result => Results.Ok(new PagedResponse<AuthSessionResponse>(
                    [.. result.Items.Select(AuthSessionResponse.From)],
                    result.Page,
                    result.PageSize,
                    result.TotalCount))))
            .RequireAuthorization(Permissions.UsersRead)
            .WithName("ListUserSessions")
            .WithSummary("Lists an account's sessions, newest first.")
            .Produces<PagedResponse<AuthSessionResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var sessions = api.MapGroup("/auth-sessions").WithTags("Users");

        sessions.MapPost("/{id:guid}/revoke", async (
                Guid id,
                RevokeSessionHandler handler,
                SessionPrincipalCache principals,
                CancellationToken cancellationToken) =>
                (await handler.Handle(new RevokeSessionCommand(id), cancellationToken)).ToHttpResult(() =>
                {
                    // At once on this instance; every other instance within the TTL (R3).
                    principals.Evict(id);
                    return Results.NoContent();
                }))
            .RequireAuthorization(Permissions.AuthSessionsRevoke)
            .WithName("RevokeSession")
            .WithSummary("Revokes one session.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/roles", async (ListRolesHandler handler, CancellationToken cancellationToken) =>
                Results.Ok((await handler.Handle(new ListRolesQuery(), cancellationToken)).Select(RoleResponse.From).ToList()))
            .RequireAuthorization(Permissions.UsersRead)
            .WithTags("Users")
            .WithName("ListRoles")
            .WithSummary("Reads the role catalogue and each role's permissions (read-only; D8).")
            .Produces<List<RoleResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return users;
    }
}

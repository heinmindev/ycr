using System.Security.Claims;
using YCR.Api.Common;
using YCR.Api.Common.Authentication;
using YCR.Api.Contracts.Identity;
using YCR.Application.Identity.ChangeOwnPassword;
using YCR.Application.Identity.GetCurrentUser;
using YCR.Application.Identity.Login;
using YCR.Application.Identity.Logout;
using YCR.Application.Identity.RefreshSession;

namespace YCR.Api.Endpoints.Identity;

/// <summary>
/// The sign-in endpoints (spec §6.1; ADR-0016, ADR-0023).
/// </summary>
/// <remarks>
/// Thin translations, as every endpoint is: request to command, handler, <c>Result</c> to
/// <c>IResult</c> (<see cref="ResultExtensions"/>). The only HTTP-specific work here is the
/// refresh cookie, which is a transport concern. Every error these endpoints return is
/// <c>Auth.*</c> (U1). The three cookie endpoints run the filters in plan P6's order:
/// <see cref="OriginCheckFilter"/> → rate limit → validation → handler.
/// </remarks>
public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        var auth = api.MapGroup("/auth").WithTags("Authentication");

        auth.MapPost("/login", async (
                LoginRequest request,
                LoginHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
                (await handler.Handle(request.ToCommand(), cancellationToken))
                .ToHttpResult(signIn =>
                {
                    RefreshCookie.Append(http.Response, signIn.RefreshToken, signIn.SessionExpiresAtUtc);
                    return Results.Ok(AccessTokenResponse.From(signIn));
                }))
            // Anonymous: the caller has no token yet (spec §6.1).
            .AllowAnonymous()
            .AddEndpointFilter<OriginCheckFilter>()
            .AddEndpointFilter<LoginRateLimitFilter>()
            .AddEndpointFilter<ValidationFilter<LoginRequest>>()
            .WithName("Login")
            .WithSummary("Signs in with a username and password; sets the refresh cookie.")
            .Produces<AccessTokenResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/refresh", async (
                RefreshSessionHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
                (await handler.Handle(new RefreshSessionCommand(RefreshCookie.Read(http.Request)), cancellationToken))
                .ToHttpResult(signIn =>
                {
                    RefreshCookie.Append(http.Response, signIn.RefreshToken, signIn.SessionExpiresAtUtc);
                    return Results.Ok(AccessTokenResponse.From(signIn));
                }))
            // Anonymous: authenticated by the refresh cookie, read inside this endpoint and never by
            // an authentication scheme (R5, D15). Never gated by R26 for that reason.
            .AllowAnonymous()
            .AddEndpointFilter<OriginCheckFilter>()
            .AddEndpointFilter<RefreshRateLimitFilter>()
            .WithName("RefreshSession")
            .WithSummary("Rotates the refresh cookie and issues a new access token.")
            .Produces<AccessTokenResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/logout", async (
                LogoutHandler handler,
                SessionPrincipalCache principals,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetSessionId(http.User, out var sessionId))
                {
                    return AuthProblem.Unauthenticated(http);
                }

                return (await handler.Handle(new LogoutCommand(sessionId), cancellationToken))
                    .ToHttpResult(() =>
                    {
                        principals.Evict(sessionId);
                        RefreshCookie.Expire(http.Response);
                        return Results.NoContent();
                    });
            })
            // Self-service, no permission (D17; docs/20 §4): any signed-in user may end their own
            // session, named by the token's sid (D12, C6).
            .RequireAuthorization()
            .WithMetadata(new AllowedWhilePasswordChangeRequiredAttribute())
            .AddEndpointFilter<OriginCheckFilter>()
            .WithName("Logout")
            .WithSummary("Revokes the caller's session and expires the refresh cookie.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        auth.MapGet("/me", async (
                GetCurrentUserHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
                await handler.Handle(new GetCurrentUserQuery(), cancellationToken) is { } user
                    ? Results.Ok(CurrentUserResponse.From(user))
                    : AuthProblem.Unauthenticated(http))
            // Self-service, no permission (D17): every signed-in user may read who they are.
            .RequireAuthorization()
            .WithMetadata(new AllowedWhilePasswordChangeRequiredAttribute())
            .WithName("GetCurrentUser")
            .WithSummary("Reads the signed-in user's username, roles and permissions.")
            .Produces<CurrentUserResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapPost("/password", async (
                ChangePasswordRequest request,
                ChangeOwnPasswordHandler handler,
                SessionPrincipalCache principals,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetSessionId(http.User, out var sessionId))
                {
                    return AuthProblem.Unauthenticated(http);
                }

                return (await handler.Handle(request.ToCommand(sessionId), cancellationToken))
                    .ToHttpResult(() =>
                    {
                        // The cached principal still carries the must-change marker; drop it so the
                        // same session is released at once on this instance (S19d).
                        principals.Evict(sessionId);
                        return Results.NoContent();
                    });
            })
            // Self-service, no permission (D17): every signed-in user changes only their own
            // password; allowed while a password change is required, which is its purpose (R26).
            .RequireAuthorization()
            .WithMetadata(new AllowedWhilePasswordChangeRequiredAttribute())
            .AddEndpointFilter<ValidationFilter<ChangePasswordRequest>>()
            .WithName("ChangeOwnPassword")
            .WithSummary("Changes the caller's password; revokes their other sessions.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return auth;
    }

    private static bool TryGetSessionId(ClaimsPrincipal user, out Guid sessionId) =>
        Guid.TryParse(user.FindFirstValue(AuthClaims.SessionId), out sessionId);
}

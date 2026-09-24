using Microsoft.AspNetCore.Authorization;

namespace YCR.Api.Common.Authentication;

/// <summary>
/// Marks the endpoints a must-change session may still call (spec R26, U2): <c>GET /auth/me</c>,
/// <c>POST /auth/password</c>, <c>POST /auth/logout</c>. <c>POST /auth/refresh</c> is anonymous and
/// therefore never gated.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowedWhilePasswordChangeRequiredAttribute : Attribute;

/// <summary>
/// R26 (U2; ADR-0023 item 9): while the user must change their password, every request to an
/// endpoint that requires authorization gets <c>403 Auth.PasswordChangeRequired</c>, whatever
/// permissions the user holds — except the endpoints marked
/// <see cref="AllowedWhilePasswordChangeRequiredAttribute"/>.
/// </summary>
/// <remarks>
/// N1 (hein, 2026-09-23; T-024): only endpoints that <em>require authorization</em> are gated.
/// Anonymous endpoints — <c>/auth/login</c>, <c>/auth/refresh</c>, the health probes — are
/// unaffected even when a must-change bearer token is attached. Runs after authentication and
/// before authorization, so the principal is the server-built one (plan P3) and the answer is the
/// same whether or not the user holds the endpoint's permission.
/// </remarks>
public sealed class PasswordChangeRequiredMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var endpoint = context.GetEndpoint();
        var gated = endpoint is not null
            && context.User.HasClaim(AuthClaims.MustChangePassword, "true")
            && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null
            && endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0
            && endpoint.Metadata.GetMetadata<AllowedWhilePasswordChangeRequiredAttribute>() is null;

        if (gated)
        {
            await AuthProblem.WriteAsync(
                context,
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "The password must be changed before this endpoint can be used.",
                AuthErrorCodes.PasswordChangeRequired);
            return;
        }

        await next(context);
    }
}

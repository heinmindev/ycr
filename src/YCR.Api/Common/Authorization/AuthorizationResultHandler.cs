using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace YCR.Api.Common.Authorization;

/// <summary>
/// Answers <c>401</c> when a request needs a caller and the deployment has no authentication
/// scheme to challenge with.
/// </summary>
/// <remarks>
/// <strong>Why this exists.</strong> ADR-0020 ships F-001 with the authorization pipeline but
/// deliberately <em>no</em> authentication handler — item 4 forbids an
/// <c>AuthenticationHandler&lt;&gt;</c> subtype anywhere in <c>src/</c>. ASP.NET Core's default
/// behaviour in that situation is to throw: "No authenticationScheme was specified, and there was
/// no DefaultChallengeScheme found." That surfaces as a <c>500</c>, so an ordinary unauthenticated
/// request to a deployed instance would look like a server fault instead of a missing credential.
/// <para>
/// This was found by running the API against the local compose database, not by a test: every
/// API test registers the test handler through <c>ConfigureTestServices</c>, so in tests a
/// challenge scheme always exists and the throw never happens. <c>MissingSchemeTests</c> now
/// covers the deployed shape explicitly.
/// </para>
/// <para>
/// The interception is narrow on purpose: it applies only when there is genuinely no default
/// challenge scheme. Once the ADR-0016 token feature registers one, every challenge goes back
/// through the framework's own handler, headers and all.
/// </para>
/// </remarks>
public sealed class AuthorizationResultHandler(IAuthenticationSchemeProvider schemes)
    : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        if (authorizeResult.Challenged && await schemes.GetDefaultChallengeSchemeAsync() is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;

            await Results.Problem(
                    title: "Unauthorized",
                    detail: "This endpoint requires an authenticated caller.",
                    statusCode: StatusCodes.Status401Unauthorized,
                    extensions: new Dictionary<string, object?>
                    {
                        ["errorCode"] = "Common.Unauthenticated"
                    })
                .ExecuteAsync(context);

            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}

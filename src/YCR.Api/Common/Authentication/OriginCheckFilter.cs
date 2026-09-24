using Microsoft.Extensions.Options;

namespace YCR.Api.Common.Authentication;

/// <summary>
/// R9 (ADR-0016 §Browser security; D16; <c>docs/18</c> required control): every cookie-setting or
/// cookie-bearing endpoint — <c>/auth/login</c>, <c>/auth/refresh</c>, <c>/auth/logout</c> —
/// refuses a request whose <c>Origin</c> is missing or is not one of <c>Auth:AllowedOrigins</c>,
/// with <c>403 Auth.OriginRejected</c>.
/// </summary>
/// <remarks>
/// The first filter on those endpoints (plan P6 order: Origin → rate limit → validation →
/// handler), so a rejected request evaluates no credential, touches no token, writes no audit row
/// and counts no failure. Matching is exact on scheme, host and port (host case-insensitive, as
/// DNS is); the literal <c>null</c> origin a sandboxed page sends matches nothing.
/// </remarks>
public sealed class OriginCheckFilter(IOptions<AuthOptions> options) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var origins = context.HttpContext.Request.Headers.Origin;
        if (origins.Count != 1 || !IsAllowed(origins[0]))
        {
            return AuthProblem.Result(
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "The request origin is not allowed.",
                AuthErrorCodes.OriginRejected);
        }

        return await next(context);
    }

    private bool IsAllowed(string? origin)
    {
        if (!TryParseOrigin(origin, out var presented))
        {
            return false;
        }

        foreach (var allowed in options.Value.AllowedOrigins)
        {
            if (TryParseOrigin(allowed, out var candidate)
                && string.Equals(presented.Scheme, candidate.Scheme, StringComparison.OrdinalIgnoreCase)
                && string.Equals(presented.IdnHost, candidate.IdnHost, StringComparison.OrdinalIgnoreCase)
                && presented.Port == candidate.Port)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseOrigin(string? value, out Uri origin)
    {
        origin = null!;
        if (string.IsNullOrEmpty(value)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || uri.PathAndQuery != "/"
            || value.EndsWith('/')
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        origin = uri;
        return true;
    }
}

namespace YCR.Api.Common;

/// <summary>
/// Security headers on every API response (spec R24, S26c; plan P10; <c>docs/09</c> secure
/// headers). No CORS is configured anywhere: the SPA is same-origin (D16, C16).
/// </summary>
/// <remarks>
/// Added through <c>OnStarting</c>, so they are on every response kind — including the exception
/// handler's <c>500</c>, which clears headers set earlier, and the challenge's <c>401</c>. The CSP
/// is stricter than the SPA's will be, which is right for JSON. <c>/api/v1/auth/*</c> responses
/// also get <c>Cache-Control: no-store</c>: they carry tokens. In Development only, the Scalar UI
/// is exempt from the CSP (it needs scripts). No HSTS: TLS termination is a hosting decision
/// (<c>docs/15</c>).
/// </remarks>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    public const string ContentSecurityPolicy =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            var path = context.Request.Path;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers.XFrameOptions = "DENY";

            var scalarInDevelopment = environment.IsDevelopment()
                && (path.StartsWithSegments("/scalar") || path.StartsWithSegments("/openapi"));
            if (!scalarInDevelopment)
            {
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
            }

            if (path.StartsWithSegments("/api/v1/auth"))
            {
                headers.CacheControl = "no-store";
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}

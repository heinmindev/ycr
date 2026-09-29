using System.Text.Json.Serialization;

namespace YCR.Api.Common;

/// <summary>
/// The request-size and JSON policy for every endpoint (docs/20 §4; T-042; ENGINEERING DECISION,
/// tech lead, hein, 2026-09-28).
/// </summary>
/// <remarks>
/// Three layers, each refusing a request before it reaches a handler:
/// <list type="number">
/// <item>Kestrel's <see cref="GlobalMaxRequestBodyBytes"/> is the backstop for any endpoint that
/// reads a body and declares no limit of its own.</item>
/// <item>Each endpoint that binds a JSON body declares its own limit as endpoint metadata
/// (<see cref="RequestSizeLimitAttribute"/>): <see cref="SmallJsonBodyMaxBytes"/> for the F-001/F-002
/// bodies, larger or smaller where the endpoint's own spec says so. Endpoint routing copies it into
/// Kestrel's <c>IHttpMaxRequestBodySizeFeature</c> before the body is read, so it may raise the
/// global limit as well as lower it (<c>POST /schedules/versions</c>, 2 MiB).</item>
/// <item>The JSON reader is strict about numbers and depth (<see cref="ConfigureYcrJson"/>).</item>
/// </list>
/// A body the framework refuses — too large, malformed, or unbindable — is answered with the
/// ProblemDetails of <see cref="UseYcrStatusCodePages"/>, carrying a stable <c>errorCode</c> and the
/// <c>traceId</c>, and never the parser's or server's own message.
/// </remarks>
public static class RequestLimits
{
    /// <summary>Kestrel's server-wide request-body limit: 64 KiB.</summary>
    public const long GlobalMaxRequestBodyBytes = 64 * 1024;

    /// <summary>
    /// The body limit of every F-001/F-002 endpoint that takes JSON: 4 KiB. The largest of those
    /// bodies — a username, a password and the role names — is a few hundred bytes.
    /// </summary>
    public const long SmallJsonBodyMaxBytes = 4 * 1024;

    /// <summary>The deepest JSON nesting the reader accepts. The deepest valid body today is 5.</summary>
    public const int JsonMaxDepth = 32;

    /// <summary>A body over the endpoint's limit (<c>413</c>).</summary>
    public const string RequestTooLargeCode = "Common.RequestTooLarge";

    /// <summary>A body or value the framework could not read or bind (<c>400</c>).</summary>
    public const string MalformedRequestCode = "Common.MalformedRequest";

    /// <summary>Sets <see cref="GlobalMaxRequestBodyBytes"/> on Kestrel.</summary>
    public static IWebHostBuilder ConfigureYcrRequestLimits(this IWebHostBuilder webHost) =>
        webHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = GlobalMaxRequestBodyBytes);

    /// <summary>
    /// Strict JSON for request binding: a number must be a JSON number (<c>"1"</c> is refused), and
    /// nesting deeper than <see cref="JsonMaxDepth"/> is refused. Unknown properties stay ignored, so
    /// a body naming an actor or an audit field is bound without it (S22).
    /// </summary>
    public static IServiceCollection ConfigureYcrJson(this IServiceCollection services) =>
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
            options.SerializerOptions.MaxDepth = JsonMaxDepth;
        });
}

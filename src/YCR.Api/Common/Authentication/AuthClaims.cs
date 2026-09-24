namespace YCR.Api.Common.Authentication;

/// <summary>Claim names used by the token and by the server-built principal (plan P3).</summary>
public static class AuthClaims
{
    /// <summary>The session id in the access token and on the rebuilt principal (ADR-0016).</summary>
    public const string SessionId = "sid";

    /// <summary>
    /// Present, with value <c>true</c>, on a principal whose user must change their password
    /// (R26). Server-built only: no token carries it.
    /// </summary>
    public const string MustChangePassword = "ycr:must_change_password";
}

/// <summary>
/// The <c>Auth.*</c> error codes raised by the API pipeline before any endpoint runs, and by the
/// cookie-endpoint filters (spec R21; U1). They live here because <c>YCR.Api</c> may not reference
/// <c>YCR.Domain.Identity</c> (hein, checkpoint 1).
/// </summary>
public static class AuthErrorCodes
{
    public const string Unauthenticated = "Auth.Unauthenticated";
    public const string PasswordChangeRequired = "Auth.PasswordChangeRequired";
    public const string OriginRejected = "Auth.OriginRejected";
    public const string TooManyRequests = "Auth.TooManyRequests";
}

/// <summary>Writes a pipeline ProblemDetails with an <c>errorCode</c> (and, via ProblemDetailsSetup, a <c>traceId</c>).</summary>
public static class AuthProblem
{
    /// <summary>The same ProblemDetails as an <see cref="IResult"/>, for endpoint filters and endpoints.</summary>
    public static IResult Result(int statusCode, string title, string detail, string errorCode) =>
        Results.Problem(
            title: title,
            detail: detail,
            statusCode: statusCode,
            extensions: new Dictionary<string, object?> { ["errorCode"] = errorCode });

    /// <summary>
    /// The <c>401 Auth.Unauthenticated</c> an endpoint returns when the authenticated principal no
    /// longer resolves (same body and <c>WWW-Authenticate</c> as the bearer challenge, D14).
    /// </summary>
    public static IResult Unauthenticated(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return Result(
            StatusCodes.Status401Unauthorized,
            "Unauthorized",
            "This endpoint requires a valid access token.",
            AuthErrorCodes.Unauthenticated);
    }

    public static Task WriteAsync(HttpContext context, int statusCode, string title, string detail, string errorCode)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.StatusCode = statusCode;
        return Results.Problem(
                title: title,
                detail: detail,
                statusCode: statusCode,
                extensions: new Dictionary<string, object?> { ["errorCode"] = errorCode })
            .ExecuteAsync(context);
    }
}

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

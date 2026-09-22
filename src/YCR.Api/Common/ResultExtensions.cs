using YCR.Domain.Common;

namespace YCR.Api.Common;

/// <summary>
/// Turns a <see cref="Result"/> into an HTTP response, in one place.
/// </summary>
/// <remarks>
/// ADR-0004 §Errors fixes the mapping: <c>Validation</c> 400, <c>Unauthorized</c> 401,
/// <c>Forbidden</c> 403, <c>NotFound</c> 404, <c>Conflict</c> 409, <c>BusinessRule</c> 422.
/// Keeping it here means no endpoint hand-rolls a status code, so the mapping cannot drift
/// between endpoints and a new error type is a compile-time decision rather than a guess.
/// <para>
/// <strong>This is the only file in <c>YCR.Api</c> permitted to depend on <c>YCR.Domain</c></strong>,
/// and only on the four types plan P11 allowlists: <see cref="Result"/>, <c>Result&lt;T&gt;</c>,
/// <see cref="Error"/> and <see cref="ErrorType"/>. An architecture test enforces the allowlist
/// by type, so adding a fifth fails the build.
/// </para>
/// </remarks>
public static class ResultExtensions
{
    /// <summary>Maps a valueless result: <paramref name="onSuccess"/>, or the error's status.</summary>
    public static IResult ToHttpResult(this Result result, Func<IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.IsSuccess ? onSuccess() : Problem(result.Error);
    }

    /// <summary>Maps a result carrying a value.</summary>
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.IsSuccess ? onSuccess(result.Value) : Problem(result.Error);
    }

    /// <summary>
    /// RFC 9457 ProblemDetails carrying the stable <c>errorCode</c> (docs/20 §2).
    /// </summary>
    /// <remarks>
    /// The <c>traceId</c> is added by <c>ProblemDetailsSetup</c> for every problem response,
    /// including ones ASP.NET Core produces itself, so it is not repeated here.
    /// </remarks>
    private static IResult Problem(Error error) => Results.Problem(
        detail: error.Message,
        statusCode: StatusCodeFor(error.Type),
        title: TitleFor(error.Type),
        extensions: new Dictionary<string, object?> { ["errorCode"] = error.Code });

    private static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,

        // Not a default that guesses: a new ErrorType must be mapped deliberately, and until it
        // is, saying so loudly beats silently returning 500 or, worse, 200.
        _ => throw new ArgumentOutOfRangeException(
            nameof(type), type, "No HTTP status code is mapped for this error type (ADR-0004 §Errors).")
    };

    private static string TitleFor(ErrorType type) => type switch
    {
        ErrorType.Validation => "Validation failed",
        ErrorType.Unauthorized => "Unauthorized",
        ErrorType.Forbidden => "Forbidden",
        ErrorType.NotFound => "Not found",
        ErrorType.Conflict => "Conflict",
        ErrorType.BusinessRule => "Business rule violated",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No title is mapped for this error type.")
    };
}

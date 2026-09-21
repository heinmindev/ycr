using FluentValidation;

namespace YCR.Api.Common;

/// <summary>
/// Runs the FluentValidation validator for <typeparamref name="T"/> before the endpoint
/// (docs/20 §3), returning a 400 ProblemDetails when it fails.
/// </summary>
/// <remarks>
/// A filter rather than a call at the top of each endpoint: a forgotten call is invisible, while
/// a missing filter is visible in the endpoint's own registration.
/// <para>
/// The response uses the same <c>errorCode</c>/<c>traceId</c> shape as every other error
/// (docs/20 §4), so a caller parses one error format rather than two.
/// </para>
/// </remarks>
public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
    where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
        {
            // The filter is registered for a body type the endpoint does not take. That is a
            // wiring mistake, and silently skipping validation would hide it.
            throw new InvalidOperationException(
                $"No argument of type '{typeof(T).Name}' was found for this endpoint, so "
                + $"{nameof(ValidationFilter<T>)} cannot validate it.");
        }

        var result = await validator.ValidateAsync(argument, context.HttpContext.RequestAborted);
        if (result.IsValid)
        {
            return await next(context);
        }

        return Results.ValidationProblem(
            result.ToDictionary(),
            title: "Validation failed",
            extensions: new Dictionary<string, object?> { ["errorCode"] = "Common.ValidationFailed" });
    }
}

using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace YCR.Api.Common;

/// <summary>
/// RFC 9457 ProblemDetails for every error response, and a global handler that never leaks
/// internals (spec S25, docs/20 §4).
/// </summary>
public static class ProblemDetailsSetup
{
    public static IServiceCollection AddYcrProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            // Every problem response carries a traceId, including the ones ASP.NET Core produces
            // itself (401, 403, 404 on an unmatched route), so a caller can always quote one
            // identifier back to an operator.
            Stamp(context.ProblemDetails, context.HttpContext);
        });

        return services;
    }

    /// <summary>The <c>traceId</c> and <c>instance</c> every problem response carries.</summary>
    private static void Stamp(ProblemDetails problem, HttpContext context)
    {
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        problem.Instance ??= $"{context.Request.Method} {context.Request.Path}";
    }

    /// <summary>
    /// Turns an unhandled exception into a 500 that says nothing about the inside of the system.
    /// </summary>
    /// <remarks>
    /// REQUIRED CONTROL (spec S25, docs/18 "data disclosure"): the response carries a title, a
    /// status and the trace id, and never a message, stack trace or type name. The detail stays
    /// in the logs, where the operator can reach it and the caller cannot.
    /// </remarks>
    public static IApplicationBuilder UseYcrExceptionHandler(this WebApplication app)
    {
        app.UseExceptionHandler(builder => builder.Run(async context =>
        {
            var feature = context.Features.Get<IExceptionHandlerFeature>();
            var logger = context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("YCR.Api.UnhandledException");

            // In Development the framework throws its bad-request exception (RouteHandlerOptions.
            // ThrowOnBadRequest) where elsewhere it only sets the status. The caller gets the same
            // coded answer either way (T-042 ruling 3), never a 500.
            if (feature?.Error is BadHttpRequestException badRequest
                && FrameworkErrorCode(badRequest.StatusCode) is { } errorCode)
            {
                logger.LogInformation(
                    badRequest,
                    "Refused request for {Method} {Path} with {StatusCode}",
                    context.Request.Method,
                    context.Request.Path,
                    badRequest.StatusCode);

                context.Response.StatusCode = badRequest.StatusCode;
                await WriteFrameworkProblemAsync(context, errorCode);
                return;
            }

            // Message template, no interpolation (docs/20 §7).
            logger.LogError(
                feature?.Error,
                "Unhandled exception for {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            await Results.Problem(
                    title: "An unexpected error occurred.",
                    statusCode: StatusCodes.Status500InternalServerError,
                    extensions: new Dictionary<string, object?>
                    {
                        ["errorCode"] = "Common.UnexpectedError"
                    })
                .ExecuteAsync(context);
        }));

        return app;
    }

    /// <summary>
    /// Status-code pages for the framework's own refusals (T-042 ruling 3; docs/20 §4).
    /// </summary>
    /// <remarks>
    /// A response the pipeline ended with a status and no body is one no application code wrote:
    /// the application's own errors always carry a ProblemDetails body and never reach this handler.
    /// <c>413</c> (Kestrel refused a body over the endpoint's limit) becomes
    /// <c>Common.RequestTooLarge</c>; <c>400</c> (the framework could not read or bind the request:
    /// malformed JSON, the wrong JSON type, nesting over 32, an unparsable route or query value)
    /// becomes <c>Common.MalformedRequest</c>. REQUIRED CONTROL (docs/18, data disclosure): the answer
    /// carries the status, the code and the trace id, and never the parser's or server's message.
    /// Every other status keeps the framework's default ProblemDetails.
    /// </remarks>
    public static IApplicationBuilder UseYcrStatusCodePages(this WebApplication app)
    {
        var defaults = new StatusCodePagesOptions();

        app.UseStatusCodePages(new StatusCodePagesOptions
        {
            HandleAsync = context =>
                FrameworkErrorCode(context.HttpContext.Response.StatusCode) is { } errorCode
                    ? WriteFrameworkProblemAsync(context.HttpContext, errorCode)
                    : defaults.HandleAsync(context),
        });

        return app;
    }

    private static string? FrameworkErrorCode(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => RequestLimits.MalformedRequestCode,
        StatusCodes.Status413PayloadTooLarge => RequestLimits.RequestTooLargeCode,
        _ => null,
    };

    /// <summary>
    /// The framework's own title and type for the response's status, plus <paramref name="errorCode"/>,
    /// <c>traceId</c> and <c>instance</c>. No detail.
    /// </summary>
    /// <remarks>
    /// Written as <c>application/problem+json</c> whatever the request's <c>Accept</c> says.
    /// <c>IProblemDetailsService.WriteAsync</c> throws when no writer accepts the request (an
    /// <c>Accept: text/html</c> caller), which turned this 400 into a 500. <c>ProblemHttpResult</c>
    /// falls back to plain JSON instead, and that fallback skips <see cref="AddYcrProblemDetails"/>'s
    /// customization, so the trace id and instance are stamped here.
    /// </remarks>
    private static Task WriteFrameworkProblemAsync(HttpContext context, string errorCode)
    {
        var problem = new ProblemDetails
        {
            Status = context.Response.StatusCode,
            Extensions = { ["errorCode"] = errorCode },
        };
        Stamp(problem, context);

        return TypedResults.Problem(problem).ExecuteAsync(context);
    }
}

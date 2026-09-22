using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;

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
            context.ProblemDetails.Extensions["traceId"] =
                Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

            context.ProblemDetails.Instance ??=
                $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
        });

        return services;
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
}

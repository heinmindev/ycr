namespace YCR.Api.Endpoints.Health;

/// <summary>
/// Liveness and readiness probes (docs/02 §Reliability, spec S24).
/// </summary>
/// <remarks>
/// These sit outside <c>/api/v1</c> because they are infrastructure probes, not versioned API
/// surface: an orchestrator's probe URL should not change when the API version does.
/// </remarks>
public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // .AllowAnonymous() with a reason, as docs/20 §4 requires: a liveness probe runs before
        // and during any credential problem, so requiring a credential would make the platform
        // look dead exactly when authentication is what is broken. It reveals nothing but
        // "the process is running".
        app.MapHealthChecks("/health/live", new()
        {
            Predicate = _ => false
        }).AllowAnonymous().WithName("HealthLive");

        // Readiness reports the database, so an orchestrator does not route traffic to an
        // instance that cannot serve it. Anonymous for the same reason as above; it reveals only
        // healthy/unhealthy, never why.
        app.MapHealthChecks("/health/ready", new()
        {
            Predicate = check => check.Tags.Contains("ready")
        }).AllowAnonymous().WithName("HealthReady");

        return app;
    }
}

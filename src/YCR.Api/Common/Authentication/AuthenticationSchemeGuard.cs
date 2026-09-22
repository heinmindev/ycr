using Microsoft.AspNetCore.Authentication;

namespace YCR.Api.Common.Authentication;

/// <summary>
/// Refuses to start if an unexpected authentication handler is registered outside the
/// <c>Testing</c> environment (ADR-0020 item 5, spec S21b).
/// </summary>
/// <remarks>
/// Defence in depth, not the primary control. The primary control is S21a: an architecture test
/// asserts that <strong>no</strong> <c>AuthenticationHandler&lt;&gt;</c> subtype exists anywhere
/// in <c>src/</c>, so the test handler cannot ship because it is not there to ship. This guard
/// catches the case that test cannot see — a handler reaching the running application some other
/// way, for example a test host misconfigured to run as Production, or a future package that
/// registers a scheme of its own.
/// <para>
/// It fails at <em>startup</em> rather than per request. A deployment that would have accepted
/// the wrong credentials never becomes reachable, instead of serving traffic until someone
/// notices.
/// </para>
/// </remarks>
public static class AuthenticationSchemeGuard
{
    /// <summary>
    /// The handler types a deployed environment may register. Empty in F-001: ADR-0020 defers
    /// the real ADR-0016 token implementation to a later feature, so outside Testing there is
    /// legitimately no scheme at all, and every endpoint that needs a caller answers 401.
    /// </summary>
    private static readonly Type[] AllowedHandlerTypes = [];

    public static async Task GuardAuthenticationSchemesAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (app.Environment.IsEnvironment("Testing"))
        {
            return;
        }

        var provider = app.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        var unexpected = (await provider.GetAllSchemesAsync())
            .Where(scheme => !AllowedHandlerTypes.Contains(scheme.HandlerType))
            .ToArray();

        if (unexpected.Length > 0)
        {
            throw new InvalidOperationException(
                "Unexpected authentication scheme(s) registered outside the Testing environment: "
                + string.Join(", ", unexpected.Select(scheme => $"{scheme.Name} ({scheme.HandlerType.FullName})"))
                + ". ADR-0020 item 4 forbids an authentication handler in src/, and item 5 requires "
                + "startup to refuse an unexpected one.");
        }
    }
}

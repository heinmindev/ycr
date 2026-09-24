using Microsoft.Extensions.Options;
using YCR.Application.Identity;

namespace YCR.Api.Common.Authentication;

/// <summary>
/// Refuses to start with <c>Auth</c> settings that would weaken a control (plan P4, P11; R3, R9;
/// ADR-0023 item 8).
/// </summary>
/// <remarks>
/// Registered with <c>ValidateOnStart</c>, so a bad value stops the host before it serves a
/// request rather than surfacing on the first sign-in. The signing keys are validated separately,
/// by <see cref="ConfigurationSigningKeyProvider"/>.
/// </remarks>
public sealed class AuthOptionsValidator(IHostEnvironment environment) : IValidateOptions<AuthOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();

        if (options.PrincipalCacheSeconds is <= 0 or > AuthOptions.MaximumPrincipalCacheSeconds)
        {
            failures.Add(
                $"Auth:PrincipalCacheSeconds must be between 1 and {AuthOptions.MaximumPrincipalCacheSeconds} "
                + "(R3: revocation and permission changes take effect within 30 seconds; plan P4).");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Auth:Issuer and Auth:Audience must be set.");
        }

        RefuseChangedLifetime(failures, nameof(AuthOptions.AccessTokenLifetime), options.AccessTokenLifetime, AuthLifetimes.AccessToken);
        RefuseChangedLifetime(failures, nameof(AuthOptions.SessionLifetime), options.SessionLifetime, AuthLifetimes.Session);
        RefuseChangedLifetime(failures, nameof(AuthOptions.RefreshGraceWindow), options.RefreshGraceWindow, AuthLifetimes.RefreshGraceWindow);

        var limits = options.RateLimits;
        if (limits is null
            || limits.LoginPerUserNamePerMinute <= 0
            || limits.LoginPerClientAddressPerMinute <= 0
            || limits.RefreshPerClientAddressPerMinute <= 0)
        {
            failures.Add("Auth:RateLimits values must be positive (R16).");
        }

        var relaxed = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        if (!relaxed && options.AllowedOrigins.Length == 0)
        {
            failures.Add("Auth:AllowedOrigins must name at least one origin outside Development and Testing (R9).");
        }

        foreach (var origin in options.AllowedOrigins)
        {
            if (!IsOrigin(origin, requireHttps: !relaxed))
            {
                failures.Add($"Auth:AllowedOrigins entry '{origin}' is not an absolute origin{(relaxed ? string.Empty : " using https")} (scheme, host and optional port only).");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>
    /// ADR-0023 item 8 fixes the lifetimes in code (review C-1): a configured value is tolerated
    /// only when it equals the constant, so no deployment can silently change the approved policy.
    /// </summary>
    private static void RefuseChangedLifetime(List<string> failures, string setting, TimeSpan? configured, TimeSpan required)
    {
        if (configured is { } value && value != required)
        {
            failures.Add(
                $"Auth:{setting} is fixed at {required:c} by ADR-0023 item 8 and cannot be configured; "
                + $"remove the setting (configured: {value:c}).");
        }
    }

    private static bool IsOrigin(string? value, bool requireHttps) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || (!requireHttps && uri.Scheme == Uri.UriSchemeHttp))
        && uri.PathAndQuery == "/"
        && string.IsNullOrEmpty(uri.Fragment)
        && !value!.EndsWith('/');
}

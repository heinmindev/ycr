using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace YCR.Api.Common.Authorization;

/// <summary>
/// Builds an authorization policy on demand for any permission name, so a permission does not
/// need a registration line before an endpoint can require it.
/// </summary>
/// <remarks>
/// Without this, every `.RequireAuthorization("stations.manage")` would need a matching
/// <c>AddPolicy</c> call, and a typo or a forgotten line would surface as a runtime
/// <c>InvalidOperationException</c> on the first request to that endpoint rather than at startup.
/// <para>
/// A policy name is only ever treated as a permission when it looks like one — <c>docs/20</c> §2
/// fixes the shape as <c>&lt;resource&gt;.&lt;action&gt;</c>, lower case. Anything else falls
/// through to the default provider, so a genuinely unknown policy name still fails loudly
/// instead of being silently granted a policy that no claim can ever satisfy.
/// </para>
/// </remarks>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        ArgumentNullException.ThrowIfNull(policyName);

        return LooksLikeAPermission(policyName)
            ? Task.FromResult<AuthorizationPolicy?>(
                new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement(policyName))
                    .Build())
            : _fallback.GetPolicyAsync(policyName);
    }

    /// <summary>
    /// <c>&lt;resource&gt;.&lt;action&gt;</c> (docs/20 §2): two or more <c>.</c>-separated
    /// segments, each lower-case ASCII words joined by single hyphens.
    /// </summary>
    /// <remarks>
    /// F-002 (plan step 1): <c>users.roles.manage</c> and <c>auth-sessions.revoke</c>, approved in
    /// <c>docs/10</c>, read <c>users.roles</c> and <c>auth-sessions</c> as the resource. The
    /// earlier one-dot, letters-only check sent both to the default provider, which throws on the
    /// first request to an endpoint that requires them.
    /// </remarks>
    private static bool LooksLikeAPermission(string policyName)
    {
        var segments = policyName.Split('.');

        return segments.Length >= 2 && segments.All(IsSegment);
    }

    /// <summary><c>[a-z]+(-[a-z]+)*</c>.</summary>
    private static bool IsSegment(string segment) =>
        segment.Length > 0
        && char.IsAsciiLetterLower(segment[0])
        && char.IsAsciiLetterLower(segment[^1])
        && !segment.Contains("--", StringComparison.Ordinal)
        && segment.All(character => char.IsAsciiLetterLower(character) || character == '-');
}

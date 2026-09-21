using Microsoft.AspNetCore.Authorization;

namespace YCR.Api.Common.Authorization;

/// <summary>
/// Succeeds when the authenticated principal carries the required permission claim.
/// </summary>
/// <remarks>
/// Permissions are read from claims, not from roles. `docs/10` models authority as permissions,
/// and OQ28 — which roles hold <c>stations.manage</c> — is still open with Myanma Railways, so
/// <strong>no role-to-permission mapping is seeded anywhere</strong>. Tests mint the permission
/// claim directly on the test principal. When OQ28 is answered, the mapping arrives in one place
/// and nothing here changes.
/// </remarks>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    /// <summary>The claim type carrying a granted permission.</summary>
    public const string PermissionClaimType = "permission";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        var granted = context.User.Claims.Any(claim =>
            claim.Type == PermissionClaimType
            && string.Equals(claim.Value, requirement.Permission, StringComparison.Ordinal));

        if (granted)
        {
            context.Succeed(requirement);
        }

        // Not failing explicitly: another handler may satisfy the same requirement later, and
        // an unsatisfied requirement already denies the request.
        return Task.CompletedTask;
    }
}

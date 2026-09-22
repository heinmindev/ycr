using Microsoft.AspNetCore.Authorization;

namespace YCR.Api.Common.Authorization;

/// <summary>
/// Succeeds when the authenticated principal carries the required permission claim.
/// </summary>
/// <remarks>
/// Permissions are read from claims, not from roles. `docs/10` models authority as permissions.
/// OQ28 — which roles hold <c>stations.manage</c>/<c>stations.read</c> — is resolved by a final
/// tech-lead ruling (hein, 2026-09-22; T-014, not a Myanma Railways answer), recorded in
/// `docs/10-authorization-matrix.md`, but <strong>no role-to-permission mapping is seeded
/// anywhere</strong>: no identity/role provisioning system exists until the ADR-0016 follow-up
/// feature ships (ADR-0020). Tests mint the permission claim directly on the test principal.
/// When that follow-up feature lands, the mapping arrives in one place and nothing here changes.
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

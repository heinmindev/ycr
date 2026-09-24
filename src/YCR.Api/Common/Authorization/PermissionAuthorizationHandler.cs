using Microsoft.AspNetCore.Authorization;

namespace YCR.Api.Common.Authorization;

/// <summary>
/// Succeeds when the authenticated principal carries the required permission claim.
/// </summary>
/// <remarks>
/// Permissions are read from claims, not from roles. `docs/10` models authority as permissions.
/// Since F-002 the claims are server-built: <c>JwtBearerSetup</c> replaces the token's principal
/// with one carrying the union of the permissions granted, as data in
/// <c>identity.RolePermissions</c>, to the user's roles (plan P3; D8). No access token carries a
/// permission, so nothing a caller sends can add one. The F-001 test handler still mints the
/// claim directly for non-authentication API tests (D13).
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

using YCR.Application.Common.Abstractions;
using YCR.Application.Identity.Abstractions;
using YCR.Domain.Common;
using YCR.Domain.Identity;

namespace YCR.Application.Identity.ReplaceUserRoles;

/// <summary><c>PUT /users/{id}/roles</c> (spec §6.2; <c>users.roles.manage</c>).</summary>
public sealed record ReplaceUserRolesCommand(Guid UserId, IReadOnlyList<string> Roles);

/// <summary>
/// Replaces a user's role set with one <c>Identity.RolesChanged</c> row carrying before and after
/// (spec R20, R22, R27; S18, S19b, S19e). The change reaches the user's live sessions within the
/// principal-cache bound (R3) without a new sign-in; no session is revoked.
/// </summary>
/// <remarks>
/// Checks in order: unknown user → <c>404</c>; own account → <c>422 Identity.CannotChangeOwnRoles</c>
/// (with any value, S19b); unknown role → <c>400</c>; in Production, granting a privileged role
/// the user does not hold → <c>422 Identity.PrivilegedRoleRequiresMfa</c> (S-1); removing
/// <c>SystemAdministrator</c> from the last active one → <c>422 Identity.LastAdministrator</c>, under
/// the R27 lock (plan P14). Every accepted request is audited, including one that sets the same
/// roles (R22: one row per operation).
/// </remarks>
public sealed class ReplaceUserRolesHandler(
    IIdentityDbContext db,
    ICurrentUser currentUser,
    IIdentityAdministratorLock administratorLock,
    IAuditWriter audit,
    PrivilegedRoleGate privilegedRoles)
{
    public async Task<Result> Handle(ReplaceUserRolesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var callerId = UserReads.RequireCaller(currentUser);

        return await IdentityRetry.RunAsync(
            db,
            async _ =>
            {
                var administratorRoleId = await AdministratorGuard.AdministratorRoleIdAsync(db, cancellationToken);
                var guarded = await AdministratorGuard.IsActiveAdministratorAsync(db, command.UserId, administratorRoleId, cancellationToken);

                return guarded
                    ? await AdministratorGuard.UnderLockAsync(
                        db, administratorLock, () => ReplaceAsync(command, callerId, administratorRoleId, cancellationToken), cancellationToken)
                    : await ReplaceAsync(command, callerId, administratorRoleId, cancellationToken);
            },
            cancellationToken);
    }

    private async Task<Result> ReplaceAsync(
        ReplaceUserRolesCommand command, Guid callerId, Guid administratorRoleId, CancellationToken cancellationToken)
    {
        var user = await IdentityQueries.FindUserAsync(db, command.UserId, cancellationToken);
        if (user is null)
        {
            return IdentityErrors.UserNotFound;
        }

        if (user.Id == callerId)
        {
            return IdentityErrors.CannotChangeOwnRoles;
        }

        var roleIds = await AdministratorGuard.RoleIdsAsync(db, command.Roles ?? [], cancellationToken);
        if (roleIds.IsFailure)
        {
            return roleIds.Error;
        }

        var before = (await IdentityQueries.RoleGrantsAsync(db, user.Roles.Select(role => role.RoleId), cancellationToken)).Roles;
        var gated = privilegedRoles.Check((command.Roles ?? []).Except(before, StringComparer.Ordinal));
        if (gated.IsFailure)
        {
            return gated.Error;
        }

        var losesAdministrator = !user.IsDisabled
            && user.Roles.Any(role => role.RoleId == administratorRoleId)
            && !roleIds.Value.Contains(administratorRoleId);
        if (losesAdministrator)
        {
            var remaining = await AdministratorGuard.EnsureAnotherActiveAdministratorAsync(db, user.Id, administratorRoleId, cancellationToken);
            if (remaining.IsFailure)
            {
                return remaining.Error;
            }
        }

        var replaced = user.ReplaceRoles(roleIds.Value, callerId);
        if (replaced.IsFailure)
        {
            return replaced.Error;
        }

        var after = (await IdentityQueries.RoleGrantsAsync(db, roleIds.Value, cancellationToken)).Roles;
        audit.Record(
            IdentityAuditActions.RolesChanged,
            IdentityAuditSubjects.User,
            user.Id,
            new UserRolesAuditSnapshot(before),
            new UserRolesAuditSnapshot(after));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

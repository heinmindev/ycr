using Microsoft.EntityFrameworkCore;
using YCR.Application.Common.Abstractions;
using YCR.Application.Identity.Abstractions;
using YCR.Domain.Common;
using YCR.Domain.Identity;

namespace YCR.Application.Identity.DisableUser;

/// <summary><c>POST /users/{id}/disable</c> (spec §6.2; <c>users.manage</c>).</summary>
public sealed record DisableUserCommand(Guid UserId);

/// <summary>
/// Disables an account and revokes all its sessions (reason <c>UserDisabled</c>) with one
/// <c>Identity.UserDisabled</c> row (spec R4, R20, R27; S17, S19e; U5).
/// </summary>
/// <remarks>
/// Checks in order: unknown → <c>404</c>; own account → <c>422 Identity.CannotDisableOwnAccount</c>;
/// already disabled → <c>204</c>, nothing changes, no audit (hein, G2); the last active
/// <c>SystemAdministrator</c> → <c>422 Identity.LastAdministrator</c>. When the target is an active
/// administrator, the whole decision runs under the R27 lock (plan P14).
/// </remarks>
public sealed class DisableUserHandler(
    IIdentityDbContext db,
    ICurrentUser currentUser,
    IIdentityAdministratorLock administratorLock,
    IAuditWriter audit,
    TimeProvider clock)
{
    public async Task<Result> Handle(DisableUserCommand command, CancellationToken cancellationToken)
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
                        db, administratorLock, () => DisableAsync(command.UserId, callerId, administratorRoleId, cancellationToken), cancellationToken)
                    : await DisableAsync(command.UserId, callerId, administratorRoleId, cancellationToken);
            },
            cancellationToken);
    }

    private async Task<Result> DisableAsync(Guid userId, Guid callerId, Guid administratorRoleId, CancellationToken cancellationToken)
    {
        var user = await IdentityQueries.FindUserAsync(db, userId, cancellationToken);
        if (user is null)
        {
            return IdentityErrors.UserNotFound;
        }

        var roles = (await IdentityQueries.RoleGrantsAsync(db, user.Roles.Select(role => role.RoleId), cancellationToken)).Roles;
        var before = UserAuditSnapshot.From(user, roles);
        var now = clock.GetUtcNow();

        var disabled = user.Disable(callerId, now);
        if (disabled.IsFailure)
        {
            return disabled.Error;
        }

        if (!disabled.Value)
        {
            return Result.Success();
        }

        if (user.Roles.Any(role => role.RoleId == administratorRoleId))
        {
            var remaining = await AdministratorGuard.EnsureAnotherActiveAdministratorAsync(db, userId, administratorRoleId, cancellationToken);
            if (remaining.IsFailure)
            {
                return remaining.Error;
            }
        }

        await RevokeAllAsync(db, userId, RevocationReason.UserDisabled, now, cancellationToken);
        audit.Record(IdentityAuditActions.UserDisabled, IdentityAuditSubjects.User, userId, before, UserAuditSnapshot.From(user, roles));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>Revokes every active session of the user (R4). Shared with the password reset.</summary>
    internal static async Task RevokeAllAsync(
        IIdentityDbContext db, Guid userId, RevocationReason reason, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        var sessions = await db.AuthSessions
            .Where(session => session.UserId == userId && session.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.Revoke(reason, nowUtc);
        }
    }
}

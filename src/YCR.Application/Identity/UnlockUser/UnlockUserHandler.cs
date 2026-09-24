using YCR.Application.Common.Abstractions;
using YCR.Domain.Common;
using YCR.Domain.Identity;

namespace YCR.Application.Identity.UnlockUser;

/// <summary><c>POST /users/{id}/unlock</c> (spec §6.2; <c>users.manage</c>; U3).</summary>
public sealed record UnlockUserCommand(Guid UserId);

/// <summary>
/// Administrator unlock: clears the lockout and the failed-attempt count with one
/// <c>Identity.UserUnlocked</c> row (S4, S19c). Unlocking an account that is not locked is a
/// <c>204</c> that changes nothing and writes no audit row (hein's accepted gap-fill).
/// </summary>
public sealed class UnlockUserHandler(IIdentityDbContext db, IAuditWriter audit, TimeProvider clock)
{
    public async Task<Result> Handle(UnlockUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return await IdentityRetry.RunAsync(
            db,
            async _ =>
            {
                var user = await IdentityQueries.FindUserAsync(db, command.UserId, cancellationToken);
                if (user is null)
                {
                    return IdentityErrors.UserNotFound;
                }

                var roles = (await IdentityQueries.RoleGrantsAsync(db, user.Roles.Select(role => role.RoleId), cancellationToken)).Roles;
                var before = UserAuditSnapshot.From(user, roles);
                if (!user.Unlock(clock.GetUtcNow()))
                {
                    return Result.Success();
                }

                audit.Record(IdentityAuditActions.UserUnlocked, IdentityAuditSubjects.User, user.Id, before, UserAuditSnapshot.From(user, roles));
                await db.SaveChangesAsync(cancellationToken);
                return Result.Success();
            },
            cancellationToken);
    }
}

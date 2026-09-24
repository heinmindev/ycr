using YCR.Application.Common.Abstractions;
using YCR.Domain.Common;
using YCR.Domain.Identity;

namespace YCR.Application.Identity.EnableUser;

/// <summary><c>POST /users/{id}/enable</c> (spec §6.2; <c>users.manage</c>).</summary>
public sealed record EnableUserCommand(Guid UserId);

/// <summary>
/// Re-enables a disabled account with one <c>Identity.UserEnabled</c> row (S17). Enabling an active
/// account is a <c>204</c> that changes nothing and writes no audit row (hein, G2). Enabling can
/// only grow the set of active administrators, so R27's lock is not needed.
/// </summary>
public sealed class EnableUserHandler(IIdentityDbContext db, IAuditWriter audit)
{
    public async Task<Result> Handle(EnableUserCommand command, CancellationToken cancellationToken)
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
                if (!user.Enable())
                {
                    return Result.Success();
                }

                audit.Record(IdentityAuditActions.UserEnabled, IdentityAuditSubjects.User, user.Id, before, UserAuditSnapshot.From(user, roles));
                await db.SaveChangesAsync(cancellationToken);
                return Result.Success();
            },
            cancellationToken);
    }
}

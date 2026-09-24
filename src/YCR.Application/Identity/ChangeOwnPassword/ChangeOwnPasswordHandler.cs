using Microsoft.EntityFrameworkCore;
using YCR.Application.Common.Abstractions;
using YCR.Application.Identity.Abstractions;
using YCR.Domain.Common;
using YCR.Domain.Identity;

namespace YCR.Application.Identity.ChangeOwnPassword;

/// <summary>
/// The signed-in user changes their own password (spec R4, R18, R26, U1; S16, S19d).
/// </summary>
/// <remarks>
/// Order: the current password is verified first (<c>422 Auth.CurrentPasswordIncorrect</c>), then
/// the new one is checked against the policy (<c>400 Auth.PasswordRejected</c>); either failure
/// changes nothing — no lockout count, no audit. On success the must-change flag is cleared, every
/// <em>other</em> active session of the user is revoked (reason <c>PasswordChanged</c>), the calling
/// session survives, and one <c>Identity.PasswordChanged</c> row is written with the user as actor.
/// </remarks>
public sealed class ChangeOwnPasswordHandler(
    IIdentityDbContext db,
    ICurrentUser currentUser,
    IPasswordService passwords,
    IAuditWriter audit,
    TimeProvider clock)
{
    public async Task<Result> Handle(ChangeOwnPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Changing a password requires an authenticated user.");

        return await IdentityRetry.RunAsync(
            db,
            async _ =>
            {
                var user = await IdentityQueries.FindUserAsync(db, userId, cancellationToken)
                    ?? throw new InvalidOperationException("The authenticated user no longer exists.");

                if (!await passwords.VerifyAsync(user, command.CurrentPassword ?? string.Empty, cancellationToken))
                {
                    return IdentityErrors.CurrentPasswordIncorrect;
                }

                var roles = (await IdentityQueries.RoleGrantsAsync(db, user.Roles.Select(role => role.RoleId), cancellationToken)).Roles;
                var before = UserAuditSnapshot.From(user, roles);

                if (!await passwords.TrySetPasswordAsync(user, command.NewPassword ?? string.Empty, cancellationToken))
                {
                    return IdentityErrors.AuthPasswordRejected;
                }

                var now = clock.GetUtcNow();
                user.ChangeOwnPassword(now);

                var otherSessions = await db.AuthSessions
                    .Where(session => session.UserId == userId && session.Id != command.SessionId && session.RevokedAtUtc == null)
                    .ToListAsync(cancellationToken);
                foreach (var session in otherSessions)
                {
                    session.Revoke(RevocationReason.PasswordChanged, now);
                }

                audit.Record(
                    IdentityAuditActions.PasswordChanged,
                    IdentityAuditSubjects.User,
                    userId,
                    before,
                    UserAuditSnapshot.From(user, roles));
                await db.SaveChangesAsync(cancellationToken);
                return Result.Success();
            },
            cancellationToken);
    }
}

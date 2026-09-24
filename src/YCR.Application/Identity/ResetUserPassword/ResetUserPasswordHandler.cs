using YCR.Application.Common.Abstractions;
using YCR.Application.Identity.Abstractions;
using YCR.Application.Identity.DisableUser;
using YCR.Domain.Common;
using YCR.Domain.Identity;

namespace YCR.Application.Identity.ResetUserPassword;

/// <summary><c>POST /users/{id}/password-reset</c> (spec §6.2; <c>users.manage</c>).</summary>
public sealed record ResetUserPasswordCommand(Guid UserId, string NewPassword)
{
    public override string ToString() => $"ResetUserPasswordCommand {{ UserId = {UserId}, NewPassword = *** }}";
}

/// <summary>
/// Administrator password reset (spec R4, R20, R26, U1, U2, U5; S19a): the new password is
/// must-change, every session of the user is revoked (reason <c>AdministratorPasswordReset</c>),
/// and one <c>Identity.PasswordReset</c> row is written.
/// </summary>
/// <remarks>
/// Checks in order: unknown → <c>404</c>; own account → <c>422 Identity.CannotResetOwnPassword</c>
/// (they use <c>POST /auth/password</c>); policy → <c>400 Identity.PasswordRejected</c>. Any failure
/// changes nothing. The lockout state is left as it is.
/// </remarks>
public sealed class ResetUserPasswordHandler(
    IIdentityDbContext db,
    ICurrentUser currentUser,
    IPasswordService passwords,
    IAuditWriter audit,
    TimeProvider clock)
{
    public async Task<Result> Handle(ResetUserPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var callerId = UserReads.RequireCaller(currentUser);

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
                var now = clock.GetUtcNow();

                var reset = user.SetPasswordByAdministrator(callerId, now);
                if (reset.IsFailure)
                {
                    return reset.Error;
                }

                if (!await passwords.TrySetPasswordAsync(user, command.NewPassword ?? string.Empty, cancellationToken))
                {
                    return IdentityErrors.IdentityPasswordRejected;
                }

                await DisableUserHandler.RevokeAllAsync(db, user.Id, RevocationReason.AdministratorPasswordReset, now, cancellationToken);
                audit.Record(IdentityAuditActions.PasswordReset, IdentityAuditSubjects.User, user.Id, before, UserAuditSnapshot.From(user, roles));
                await db.SaveChangesAsync(cancellationToken);
                return Result.Success();
            },
            cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using YCR.Application.Common;
using YCR.Application.Common.Abstractions;
using YCR.Application.Identity.Abstractions;
using YCR.Domain.Common;
using YCR.Domain.Identity;

namespace YCR.Application.Identity.BootstrapAdministrator;

/// <summary>The one-time first-administrator command (D10; spec S32, S32a).</summary>
public sealed record BootstrapAdministratorCommand(string UserName, string Password)
{
    public override string ToString() => $"BootstrapAdministratorCommand {{ UserName = {UserName}, Password = *** }}";
}

/// <summary>
/// Creates the first <c>SystemAdministrator</c> with a must-change password, once (D10, U2).
/// </summary>
/// <remarks>
/// <para>
/// ENGINEERING DECISION (hein, 2026-09-23; D10): refuses — creating nothing and writing nothing —
/// when any user already holds <c>SystemAdministrator</c>, active or not. Two runs at once are
/// serialised by the R27 lock (plan P14), so exactly one creates the account.
/// </para>
/// <para>
/// The <c>UserCreated</c> row has no actor: no one is signed in (D10; R12). Holder of the account:
/// hein, until Myanma Railways names one — a provisional tech-lead ruling, not a Myanma Railways
/// answer (OQ34). The password is used here and never logged or audited (R13).
/// </para>
/// </remarks>
public sealed class BootstrapAdministratorHandler(
    IIdentityDbContext db,
    IIdentityAdministratorLock administratorLock,
    IPasswordService passwords,
    IIdGenerator ids,
    IAuditWriter audit,
    TimeProvider clock)
{
    public async Task<Result<Guid>> Handle(BootstrapAdministratorCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var userName = UserName.Create(command.UserName);
        if (userName.IsFailure)
        {
            return userName.Error;
        }

        return await AdministratorGuard.UnderLockAsync(
            db,
            administratorLock,
            async () =>
            {
                var administratorRoleId = await AdministratorGuard.AdministratorRoleIdAsync(db, cancellationToken);
                if (await db.Users.AnyAsync(user => user.Roles.Any(role => role.RoleId == administratorRoleId), cancellationToken))
                {
                    return IdentityErrors.AdministratorAlreadyExists;
                }

                var user = StaffUser.Create(ids.New(), userName.Value, clock.GetUtcNow());
                user.ReplaceRoles([administratorRoleId], callerId: null);
                if (!await passwords.TrySetPasswordAsync(user, command.Password ?? string.Empty, cancellationToken))
                {
                    return IdentityErrors.IdentityPasswordRejected;
                }

                if (await db.Users.AnyAsync(candidate => candidate.NormalizedUserName == user.NormalizedUserName, cancellationToken))
                {
                    return IdentityErrors.UserNameAlreadyExists;
                }

                db.Users.Add(user);
                audit.RecordWithoutActor(
                    IdentityAuditActions.UserCreated,
                    IdentityAuditSubjects.User,
                    user.Id,
                    before: null,
                    after: UserAuditSnapshot.From(user, [RoleNames.SystemAdministrator]));

                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (UniqueConstraintViolationException violation)
                    when (violation.ConstraintName == IdentityConstraints.UserNameUniqueIndex)
                {
                    return Result<Guid>.Failure(IdentityErrors.UserNameAlreadyExists);
                }

                return Result<Guid>.Success(user.Id);
            },
            cancellationToken);
    }
}

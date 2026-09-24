using Microsoft.EntityFrameworkCore;
using YCR.Application.Common;
using YCR.Application.Common.Abstractions;
using YCR.Application.Identity.Abstractions;
using YCR.Domain.Common;
using YCR.Domain.Identity;

namespace YCR.Application.Identity.CreateUser;

/// <summary><c>POST /users</c> (spec §6.2; <c>users.manage</c>).</summary>
public sealed record CreateUserCommand(string UserName, string Password, IReadOnlyList<string> Roles)
{
    public override string ToString() => $"CreateUserCommand {{ UserName = {UserName}, Password = ***, Roles = [{string.Join(", ", Roles)}] }}";
}

/// <summary>
/// Creates a staff account (spec R20, R26, U1, U2; S19d, S32a). The password an administrator sets
/// is must-change. Errors carry <c>Identity.*</c> codes (U1: the path is <c>/users</c>).
/// </summary>
/// <remarks>
/// The username is unique on its normalized form. A pre-check gives the ordinary <c>409</c>; the
/// unique index settles a concurrent duplicate (the <c>CreateStationHandler</c> pattern).
/// </remarks>
public sealed class CreateUserHandler(
    IIdentityDbContext db,
    ICurrentUser currentUser,
    IPasswordService passwords,
    IIdGenerator ids,
    IAuditWriter audit,
    TimeProvider clock)
{
    public async Task<Result<Guid>> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var callerId = UserReads.RequireCaller(currentUser);

        var userName = UserName.Create(command.UserName);
        if (userName.IsFailure)
        {
            return userName.Error;
        }

        var roleIds = await AdministratorGuard.RoleIdsAsync(db, command.Roles ?? [], cancellationToken);
        if (roleIds.IsFailure)
        {
            return roleIds.Error;
        }

        var user = StaffUser.Create(ids.New(), userName.Value, clock.GetUtcNow());
        var roles = user.ReplaceRoles(roleIds.Value, callerId);
        if (roles.IsFailure)
        {
            return roles.Error;
        }

        if (!await passwords.TrySetPasswordAsync(user, command.Password ?? string.Empty, cancellationToken))
        {
            return IdentityErrors.IdentityPasswordRejected;
        }

        if (await db.Users.AnyAsync(candidate => candidate.NormalizedUserName == user.NormalizedUserName, cancellationToken))
        {
            return IdentityErrors.UserNameAlreadyExists;
        }

        db.Users.Add(user);
        var grants = await IdentityQueries.RoleGrantsAsync(db, roleIds.Value, cancellationToken);
        audit.Record(
            IdentityAuditActions.UserCreated,
            IdentityAuditSubjects.User,
            user.Id,
            before: null,
            after: UserAuditSnapshot.From(user, grants.Roles));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException violation)
            when (violation.ConstraintName == IdentityConstraints.UserNameUniqueIndex)
        {
            return IdentityErrors.UserNameAlreadyExists;
        }

        return user.Id;
    }
}

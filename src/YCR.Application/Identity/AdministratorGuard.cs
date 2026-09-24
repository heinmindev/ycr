using Microsoft.EntityFrameworkCore;
using YCR.Application.Identity.Abstractions;
using YCR.Domain.Common;
using YCR.Domain.Identity;

namespace YCR.Application.Identity;

/// <summary>
/// R27 (U5; provisional tech-lead ruling, OQ34 family — not a Myanma Railways answer): at least
/// one active <c>SystemAdministrator</c> always remains, and the check holds under concurrent
/// requests (S19e) because it runs under plan P14's exclusive lock.
/// </summary>
/// <remarks>
/// The lock is taken only when an operation could shrink the set of active administrators — the
/// target is one now — or, for the bootstrap, to settle "does one exist yet". It is transaction-
/// owned: taken, then the count, then the change, the audit row and the save, then the commit.
/// This is the one documented exception to ADR-0004's single-save default. A caller runs its whole
/// decision inside <see cref="UnderLockAsync{T}"/> and loads the rows it changes <em>after</em> the
/// lock is held, so it decides on committed state.
/// </remarks>
internal static class AdministratorGuard
{
    public static Task<Guid> AdministratorRoleIdAsync(IIdentityDbContext db, CancellationToken cancellationToken) =>
        db.Roles.AsNoTracking()
            .Where(role => role.Name == RoleNames.SystemAdministrator)
            .Select(role => role.Id)
            .SingleAsync(cancellationToken);

    public static Task<bool> IsActiveAdministratorAsync(
        IIdentityDbContext db, Guid userId, Guid administratorRoleId, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().AnyAsync(
            user => user.Id == userId && !user.IsDisabled && user.Roles.Any(role => role.RoleId == administratorRoleId),
            cancellationToken);

    /// <summary>
    /// Refuses with <see cref="IdentityErrors.LastAdministrator"/> when no active administrator
    /// other than <paramref name="userId"/> exists. Call it under the lock, before the change.
    /// </summary>
    public static async Task<Result> EnsureAnotherActiveAdministratorAsync(
        IIdentityDbContext db, Guid userId, Guid administratorRoleId, CancellationToken cancellationToken)
    {
        var others = await db.Users.AsNoTracking().CountAsync(
            user => user.Id != userId && !user.IsDisabled && user.Roles.Any(role => role.RoleId == administratorRoleId),
            cancellationToken);

        return others > 0 ? Result.Success() : IdentityErrors.LastAdministrator;
    }

    public static async Task<T> UnderLockAsync<T>(
        IIdentityDbContext db,
        IIdentityAdministratorLock administratorLock,
        Func<Task<T>> work,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await administratorLock.AcquireAsync(cancellationToken);
        var result = await work();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// Maps role names to catalogue ids. Unknown names are refused (R19); the API's validator
    /// normally answers <c>400 Common.ValidationFailed</c> before this is reached (spec §6.2).
    /// </summary>
    public static async Task<Result<IReadOnlyList<Guid>>> RoleIdsAsync(
        IIdentityDbContext db, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken)
    {
        var wanted = roleNames.Distinct(StringComparer.Ordinal).ToList();
        var found = await db.Roles.AsNoTracking()
            .Where(role => wanted.Contains(role.Name))
            .Select(role => role.Id)
            .ToListAsync(cancellationToken);

        return found.Count == wanted.Count
            ? Result<IReadOnlyList<Guid>>.Success(found)
            : IdentityErrors.UnknownRole;
    }
}

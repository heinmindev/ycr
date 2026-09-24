using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using YCR.Domain.Identity;
using YCR.Infrastructure.Persistence;

namespace YCR.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity's view of the <see cref="StaffUser"/> aggregate (ADR-0023 item 1; plan P1).
/// </summary>
/// <remarks>
/// Hand-written, so Identity owns no table and no second user model: the <c>identity</c> tables
/// are exactly spec §7's. It implements only what <c>UserManager</c> needs for hashing,
/// rehash-on-verify and the password-validator pipeline. It deliberately does <b>not</b> implement
/// <see cref="IUserLockoutStore{TUser}"/>: lockout is the aggregate's, on the caller's clock
/// (plan P2), so <c>UserManager</c> never touches it. Every capability not implemented here is
/// reported unsupported by <c>UserManager</c> and throws if used (spec O6-d).
/// <para>
/// The store never saves. ADR-0004: the handler's single <c>SaveChangesAsync</c> commits the
/// user together with its audit row, so <see cref="UpdateAsync"/> only confirms that the tracked
/// entity will be saved by that unit of work.
/// </para>
/// </remarks>
internal sealed class StaffUserStore(YcrDbContext context)
    : IUserPasswordStore<StaffUser>, IUserSecurityStampStore<StaffUser>
{
    public Task<string> GetUserIdAsync(StaffUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Id.ToString());

    public Task<string?> GetUserNameAsync(StaffUser user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.UserName.Value);

    /// <summary>Usernames never change: no endpoint renames a user, and no grant allows it (plan P7).</summary>
    public Task SetUserNameAsync(StaffUser user, string? userName, CancellationToken cancellationToken) =>
        string.Equals(userName, user.UserName.Value, StringComparison.Ordinal)
            ? Task.CompletedTask
            : throw new NotSupportedException("A staff username cannot be changed.");

    public Task<string?> GetNormalizedUserNameAsync(StaffUser user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.NormalizedUserName);

    /// <summary>
    /// The aggregate computes its normalized name; this only confirms Identity's normalizer agrees,
    /// so a lookup by <c>UserManager</c> and by a handler can never disagree.
    /// </summary>
    public Task SetNormalizedUserNameAsync(StaffUser user, string? normalizedName, CancellationToken cancellationToken) =>
        string.Equals(normalizedName, user.NormalizedUserName, StringComparison.Ordinal)
            ? Task.CompletedTask
            : throw new InvalidOperationException("Identity's user-name normalizer disagrees with StaffUser.NormalizedUserName.");

    /// <summary>Users are added by the Identity handlers through their own unit of work.</summary>
    public Task<IdentityResult> CreateAsync(StaffUser user, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Staff users are created by the Identity handlers, not by UserManager.");

    /// <summary>The user is tracked; the handler's <c>SaveChangesAsync</c> persists it (ADR-0004).</summary>
    public Task<IdentityResult> UpdateAsync(StaffUser user, CancellationToken cancellationToken) =>
        Task.FromResult(IdentityResult.Success);

    /// <summary>Staff accounts are disabled, never deleted (no grant allows it, plan P7).</summary>
    public Task<IdentityResult> DeleteAsync(StaffUser user, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Staff users are never deleted.");

    public async Task<StaffUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        Guid.TryParse(userId, out var id)
            ? await context.Users.Include(user => user.Roles).SingleOrDefaultAsync(user => user.Id == id, cancellationToken)
            : null;

    public Task<StaffUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        context.Users.Include(user => user.Roles)
            .SingleOrDefaultAsync(user => user.NormalizedUserName == normalizedUserName, cancellationToken);

    public Task SetPasswordHashAsync(StaffUser user, string? passwordHash, CancellationToken cancellationToken)
    {
        user.ApplyPasswordHash(passwordHash);
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(StaffUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.PasswordHash);

    public Task<bool> HasPasswordAsync(StaffUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.PasswordHash is not null);

    public Task SetSecurityStampAsync(StaffUser user, string stamp, CancellationToken cancellationToken)
    {
        user.ApplySecurityStamp(stamp);
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(StaffUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.SecurityStamp);

    public void Dispose()
    {
        // The context belongs to the request scope, not to the store.
    }
}

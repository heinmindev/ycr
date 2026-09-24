using Microsoft.EntityFrameworkCore;
using YCR.Application.Identity.GetUser;

namespace YCR.Application.Identity;

/// <summary>Projects users to <see cref="UserDto"/> for the administration reads.</summary>
internal static class UserReads
{
    public static async Task<List<UserDto>> ProjectAsync(
        IIdentityDbContext db,
        IQueryable<Domain.Identity.StaffUser> users,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var rows = await users
            .Select(user => new
            {
                user.Id,
                user.UserName,
                user.IsDisabled,
                user.LockoutEndUtc,
                user.CreatedAtUtc,
                RoleIds = user.Roles.Select(role => role.RoleId).ToList(),
            })
            .ToListAsync(cancellationToken);

        var roleNames = await db.Roles.AsNoTracking().ToDictionaryAsync(role => role.Id, role => role.Name, cancellationToken);

        return [.. rows.Select(row => new UserDto(
            row.Id,
            row.UserName.Value,
            [.. row.RoleIds.Select(id => roleNames[id]).OrderBy(IdentityQueries.CanonicalOrder)],
            row.IsDisabled,
            row.LockoutEndUtc > nowUtc ? row.LockoutEndUtc : null,
            row.CreatedAtUtc))];
    }

    public static Guid RequireCaller(Common.Abstractions.ICurrentUser currentUser) =>
        currentUser.UserId ?? throw new InvalidOperationException("Administration requires an authenticated user.");
}

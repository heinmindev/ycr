using Microsoft.EntityFrameworkCore;
using YCR.Application.Common.Abstractions;

namespace YCR.Application.Identity.GetCurrentUser;

/// <summary><c>GET /auth/me</c> (spec §6.1).</summary>
public sealed record GetCurrentUserQuery;

/// <summary>
/// The signed-in user's id, username, roles and permissions. No display name exists (OQ34 ruling),
/// and no must-change flag is returned (U2: the client learns it from
/// <c>403 Auth.PasswordChangeRequired</c>).
/// </summary>
public sealed record CurrentUserDto(Guid UserId, string UserName, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

/// <summary>Reads the authenticated user (<see cref="ICurrentUser"/>) and their grants from the database.</summary>
public sealed class GetCurrentUserHandler(IIdentityDbContext db, ICurrentUser currentUser)
{
    /// <returns>Null when there is no authenticated user or it no longer exists; the endpoint answers 401.</returns>
    public async Task<CurrentUserDto?> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (currentUser.UserId is not { } userId)
        {
            return null;
        }

        var user = await db.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new { candidate.UserName, RoleIds = candidate.Roles.Select(role => role.RoleId).ToList() })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return null;
        }

        var grants = await IdentityQueries.RoleGrantsAsync(db, user.RoleIds, cancellationToken);
        return new CurrentUserDto(userId, user.UserName.Value, grants.Roles, grants.Permissions);
    }
}

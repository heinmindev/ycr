using Microsoft.EntityFrameworkCore;
using YCR.Domain.Common;
using YCR.Domain.Identity;

namespace YCR.Application.Identity.GetUser;

/// <summary><c>GET /users/{id}</c> (spec §6.2; <c>users.read</c>).</summary>
public sealed record GetUserQuery(Guid UserId);

/// <summary>Reads one staff account; <c>404 Identity.UserNotFound</c> when absent.</summary>
public sealed class GetUserHandler(IIdentityDbContext db, TimeProvider clock)
{
    public async Task<Result<UserDto>> Handle(GetUserQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var users = await UserReads.ProjectAsync(
            db,
            db.Users.AsNoTracking().Where(user => user.Id == query.UserId),
            clock.GetUtcNow(),
            cancellationToken);

        return users.Count == 1 ? users[0] : IdentityErrors.UserNotFound;
    }
}

using Microsoft.EntityFrameworkCore;
using YCR.Application.Common.Pagination;
using YCR.Application.Identity.GetUser;
using YCR.Domain.Common;
using YCR.Domain.Identity;

namespace YCR.Application.Identity.ListUsers;

/// <summary><c>GET /users</c> (spec §6.2; <c>users.read</c>; <c>docs/20</c> §4 paging).</summary>
public sealed record ListUsersQuery(int Page = 1, int PageSize = Paging.DefaultPageSize);

/// <summary>Staff accounts, ordered by username, one page at a time.</summary>
public sealed class ListUsersHandler(IIdentityDbContext db, TimeProvider clock)
{
    public async Task<Result<PagedResult<UserDto>>> Handle(ListUsersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!Paging.IsWithinLimits(query.Page, query.PageSize))
        {
            return IdentityErrors.InvalidPageRequest;
        }

        var users = db.Users.AsNoTracking().OrderBy(user => user.NormalizedUserName);
        var totalCount = await users.CountAsync(cancellationToken);
        var page = await UserReads.ProjectAsync(
            db,
            users.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize),
            clock.GetUtcNow(),
            cancellationToken);

        return new PagedResult<UserDto>(page, query.Page, query.PageSize, totalCount);
    }
}

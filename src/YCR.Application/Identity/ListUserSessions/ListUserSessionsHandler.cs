using Microsoft.EntityFrameworkCore;
using YCR.Application.Common.Pagination;
using YCR.Domain.Common;
using YCR.Domain.Identity;

namespace YCR.Application.Identity.ListUserSessions;

/// <summary><c>GET /users/{id}/auth-sessions</c> (spec §6.2; <c>users.read</c>).</summary>
public sealed record ListUserSessionsQuery(Guid UserId, int Page = 1, int PageSize = Paging.DefaultPageSize);

/// <summary>A session as the administration API shows it: never a token or hash.</summary>
public sealed record AuthSessionDto(
    Guid Id,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc,
    string? RevocationReason);

/// <summary>A user's sessions, newest first; <c>404 Identity.UserNotFound</c> for an unknown user.</summary>
public sealed class ListUserSessionsHandler(IIdentityDbContext db)
{
    public async Task<Result<PagedResult<AuthSessionDto>>> Handle(ListUserSessionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!Paging.IsWithinLimits(query.Page, query.PageSize))
        {
            return IdentityErrors.InvalidPageRequest;
        }

        if (!await db.Users.AsNoTracking().AnyAsync(user => user.Id == query.UserId, cancellationToken))
        {
            return IdentityErrors.UserNotFound;
        }

        var sessions = db.AuthSessions.AsNoTracking()
            .Where(session => session.UserId == query.UserId)
            .OrderByDescending(session => session.CreatedAtUtc)
            .ThenBy(session => session.Id);
        var totalCount = await sessions.CountAsync(cancellationToken);
        var page = await sessions
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(session => new { session.Id, session.CreatedAtUtc, session.ExpiresAtUtc, session.RevokedAtUtc, session.RevocationReason })
            .ToListAsync(cancellationToken);

        return new PagedResult<AuthSessionDto>(
            [.. page.Select(row => new AuthSessionDto(row.Id, row.CreatedAtUtc, row.ExpiresAtUtc, row.RevokedAtUtc, row.RevocationReason?.ToString()))],
            query.Page,
            query.PageSize,
            totalCount);
    }
}

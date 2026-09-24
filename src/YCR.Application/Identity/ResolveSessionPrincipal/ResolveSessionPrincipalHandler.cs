using Microsoft.EntityFrameworkCore;

namespace YCR.Application.Identity.ResolveSessionPrincipal;

/// <summary>The <c>sub</c> and <c>sid</c> of a validated access token (ADR-0016; spec R1).</summary>
public sealed record ResolveSessionPrincipalQuery(Guid UserId, Guid SessionId);

/// <summary>
/// Everything the request principal is rebuilt from (plan P3; spec O3/O4): the user, their roles
/// <em>and</em> the union of their permissions, and the session's absolute expiry, so a cached copy
/// can be re-checked against the clock on every use (P4).
/// </summary>
public sealed record SessionPrincipal(
    Guid UserId,
    Guid SessionId,
    DateTimeOffset SessionExpiresAtUtc,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    bool MustChangePassword);

/// <summary>
/// Resolves a token's session and user server-side on every (uncached) request — ADR-0016's
/// "every request resolves the session and the caller's roles and permissions" (R2, R3).
/// </summary>
/// <remarks>
/// Returns null — and the request is <c>401 Auth.Unauthenticated</c> — when the session is
/// unknown, revoked or past its absolute expiry, belongs to a different user than <c>sub</c>, or
/// the user is disabled (S13, S17, S21). A locked account keeps its existing sessions: lockout
/// guards password guessing, not signed-in sessions. Read-only, no tracking.
/// </remarks>
public sealed class ResolveSessionPrincipalHandler(IIdentityDbContext db, TimeProvider clock)
{
    public async Task<SessionPrincipal?> Handle(ResolveSessionPrincipalQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var row = await db.AuthSessions
            .AsNoTracking()
            .Where(session => session.Id == query.SessionId && session.UserId == query.UserId && session.RevokedAtUtc == null)
            .Join(
                db.Users.AsNoTracking().Where(user => !user.IsDisabled),
                session => session.UserId,
                user => user.Id,
                (session, user) => new
                {
                    session.ExpiresAtUtc,
                    user.MustChangePassword,
                    RoleIds = user.Roles.Select(role => role.RoleId).ToList(),
                })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null || clock.GetUtcNow() >= row.ExpiresAtUtc)
        {
            return null;
        }

        var grants = await IdentityQueries.RoleGrantsAsync(db, row.RoleIds, cancellationToken);
        return new SessionPrincipal(
            query.UserId,
            query.SessionId,
            row.ExpiresAtUtc,
            grants.Roles,
            grants.Permissions,
            row.MustChangePassword);
    }
}

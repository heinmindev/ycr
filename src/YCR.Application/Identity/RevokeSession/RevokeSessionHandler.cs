using Microsoft.EntityFrameworkCore;
using YCR.Application.Common.Abstractions;
using YCR.Domain.Common;
using YCR.Domain.Identity;

namespace YCR.Application.Identity.RevokeSession;

/// <summary><c>POST /auth-sessions/{id}/revoke</c> (spec §6.2; <c>auth-sessions.revoke</c>).</summary>
public sealed record RevokeSessionCommand(Guid SessionId);

/// <summary>
/// Revokes one named session (reason <c>AdministratorRevoked</c>) with one
/// <c>Identity.SessionRevoked</c> row; the user's other sessions are untouched (spec R4; S19).
/// </summary>
/// <remarks>
/// G1 (hein, 2026-09-23): the audit subject is the session's <b>user</b>; the session id and the
/// reason are in <c>AfterJson</c>. An already-revoked session is a <c>204</c> with no second audit
/// row (plan §API inventory). Unknown → <c>404 Identity.SessionNotFound</c>.
/// </remarks>
public sealed class RevokeSessionHandler(IIdentityDbContext db, IAuditWriter audit, TimeProvider clock)
{
    public async Task<Result> Handle(RevokeSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return await IdentityRetry.RunAsync(
            db,
            async _ =>
            {
                var session = await db.AuthSessions.SingleOrDefaultAsync(candidate => candidate.Id == command.SessionId, cancellationToken);
                if (session is null)
                {
                    return IdentityErrors.SessionNotFound;
                }

                if (!session.Revoke(RevocationReason.AdministratorRevoked, clock.GetUtcNow()))
                {
                    return Result.Success();
                }

                audit.Record(
                    IdentityAuditActions.SessionRevoked,
                    IdentityAuditSubjects.User,
                    session.UserId,
                    before: null,
                    after: AuthSessionAuditSnapshot.From(session));
                await db.SaveChangesAsync(cancellationToken);
                return Result.Success();
            },
            cancellationToken);
    }
}

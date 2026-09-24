using Microsoft.EntityFrameworkCore;
using YCR.Application.Common.Abstractions;
using YCR.Domain.Common;
using YCR.Domain.Identity;

namespace YCR.Application.Identity.Logout;

/// <summary>
/// Revokes the caller's own session (reason <c>Logout</c>) and writes one
/// <c>Identity.LoggedOut</c> row; the user's other sessions are untouched (spec R4, S15).
/// </summary>
/// <remarks>
/// The session must belong to the authenticated user (<see cref="ICurrentUser"/>). The pipeline
/// only lets an active session of that user reach here, so "not found" and "already revoked" are
/// races with another revocation: nothing changes and nothing more is audited — the revocation
/// that won wrote its own row (<c>RevokedAtUtc</c> is a concurrency token, plan P15).
/// </remarks>
public sealed class LogoutHandler(IIdentityDbContext db, ICurrentUser currentUser, IAuditWriter audit, TimeProvider clock)
{
    public async Task<Result> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Logout requires an authenticated user.");

        return await IdentityRetry.RunAsync(
            db,
            async _ =>
            {
                var session = await db.AuthSessions.SingleOrDefaultAsync(
                    candidate => candidate.Id == command.SessionId && candidate.UserId == userId,
                    cancellationToken);

                if (session is null || !session.Revoke(RevocationReason.Logout, clock.GetUtcNow()))
                {
                    return Result.Success();
                }

                audit.Record(
                    IdentityAuditActions.LoggedOut,
                    IdentityAuditSubjects.User,
                    userId,
                    before: null,
                    after: AuthSessionAuditSnapshot.From(session));
                await db.SaveChangesAsync(cancellationToken);
                return Result.Success();
            },
            cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YCR.Application.Common;
using YCR.Application.Common.Abstractions;
using YCR.Application.Identity.Abstractions;
using YCR.Application.Identity.Login;
using YCR.Domain.Common;
using YCR.Domain.Identity;

namespace YCR.Application.Identity.RefreshSession;

/// <summary>
/// Rotates a refresh token (ADR-0016 §Refresh; spec R5–R8; S7–S11, S13, S14, S29, S33).
/// </summary>
/// <remarks>
/// <para>
/// The session decides (<see cref="AuthSession.Rotate"/>); this handler persists. Outcomes:
/// the current token rotates and a new access token is issued; the immediate predecessor within
/// the grace window gets <c>409 Auth.RefreshSuperseded</c>, is logged and counted, and is
/// <b>not</b> audited (R7, D6); any older token, or the predecessor after the window, revokes the
/// family with one <c>Identity.RefreshFamilyRevoked</c> row and gets <c>401</c> (R8); anything
/// else gets <c>401 Auth.RefreshInvalid</c>. The session's absolute expiry never moves (D12).
/// </para>
/// <para>
/// <b>Two tabs, one token (S8, S33; plan P15).</b> Both requests may load the same current
/// token. The rotation's <c>UPDATE … WHERE ReplacedByTokenId IS NULL</c> lets exactly one win; the
/// loser's successor insert rolls back with it, and the loser re-reads the family — where its
/// token is now the immediate predecessor within the grace window, so it answers <c>409</c>.
/// </para>
/// <para>
/// <b>Audit actor (R12, U4; G1).</b> Family revocation is a system action: no actor, even if the
/// request carried a bearer token; the subject is the session's user, and the session id and
/// reason are in <c>AfterJson</c>.
/// </para>
/// </remarks>
public sealed class RefreshSessionHandler(
    IIdentityDbContext db,
    IRefreshTokenGenerator refreshTokens,
    IAccessTokenIssuer accessTokens,
    IIdGenerator ids,
    IAuditWriter audit,
    TimeProvider clock,
    ILogger<RefreshSessionHandler> logger)
{
    public async Task<Result<SignInResult>> Handle(RefreshSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrEmpty(command.RefreshToken))
        {
            return IdentityErrors.RefreshInvalid;
        }

        var presentedHash = refreshTokens.Hash(command.RefreshToken);

        return await IdentityRetry.RunAsync(
            db,
            _ => RotateAsync(presentedHash, cancellationToken),
            cancellationToken);
    }

    private async Task<Result<SignInResult>> RotateAsync(byte[] presentedHash, CancellationToken cancellationToken)
    {
        var session = await db.AuthSessions
            .Include(candidate => candidate.Tokens)
            .SingleOrDefaultAsync(candidate => candidate.Tokens.Any(token => token.TokenHash == presentedHash), cancellationToken);

        if (session is null)
        {
            return IdentityErrors.RefreshInvalid;
        }

        var now = clock.GetUtcNow();
        var successor = refreshTokens.Generate();
        var outcome = session.Rotate(presentedHash, ids.New(), successor.TokenHash, now, AuthLifetimes.RefreshGraceWindow);

        switch (outcome)
        {
            case RefreshOutcome.Rotated:
                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (UniqueConstraintViolationException violation)
                    when (violation.ConstraintName == IdentityConstraints.RefreshTokenSuccessorUniqueIndex)
                {
                    // The database's at-most-one-successor rule, reached by a path that skipped the
                    // concurrency token: the same lost race, so re-read like one.
                    throw new DbUpdateConcurrencyException(violation.Message, violation);
                }

                var accessToken = accessTokens.Issue(session.UserId, session.Id);
                return new SignInResult(accessToken.Value, accessToken.ExpiresAtUtc, successor.RawToken, session.ExpiresAtUtc);

            case RefreshOutcome.Superseded:
                IdentityTelemetry.RefreshSuperseded.Add(1);
                IdentityTelemetry.LogRefreshSuperseded(logger, session.Id, null);
                return IdentityErrors.RefreshSuperseded;

            case RefreshOutcome.FamilyReused:
                audit.RecordWithoutActor(
                    IdentityAuditActions.RefreshFamilyRevoked,
                    IdentityAuditSubjects.User,
                    session.UserId,
                    before: null,
                    after: AuthSessionAuditSnapshot.From(session));
                await db.SaveChangesAsync(cancellationToken);
                IdentityTelemetry.RefreshFamilyRevoked.Add(1);
                IdentityTelemetry.LogRefreshFamilyRevoked(logger, session.Id, null);
                return IdentityErrors.RefreshInvalid;

            default:
                return IdentityErrors.RefreshInvalid;
        }
    }
}

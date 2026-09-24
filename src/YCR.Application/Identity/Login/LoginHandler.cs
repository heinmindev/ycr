using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YCR.Application.Common.Abstractions;
using YCR.Application.Identity.Abstractions;
using YCR.Domain.Common;
using YCR.Domain.Identity;

namespace YCR.Application.Identity.Login;

/// <summary>
/// Signs a staff member in: verifies the password, starts an <see cref="AuthSession"/> (one
/// refresh family, D12) and issues the access and refresh tokens (spec R1, R2, R5, R12, R18, R22,
/// R23; S1–S4, S27, S31).
/// </summary>
/// <remarks>
/// <para>
/// <b>Uniform rejection with equalised timing (R23; ADR-0023 item 3; hein, 2026-09-23).</b> The
/// password is verified in full <em>before</em> the account's state is looked at, on every path:
/// against a dummy hash when the username is unknown, and against the real hash when the account
/// is disabled or locked. There is no early return. Wrong password, unknown user, disabled and
/// locked accounts then all get the same <see cref="IdentityErrors.InvalidCredentials"/>.
/// </para>
/// <para>
/// <b>Failure counting (D3).</b> Only a wrong password for an active, unlocked account counts
/// towards lockout. A disabled or locked account's attempt — right or wrong password — neither
/// counts, nor unlocks, nor resets the count; it writes one <c>Identity.LoginFailed</c>.
/// </para>
/// <para>
/// <b>Audit actor (R12, U4; plan P5).</b> Failures and lockouts are recorded without an actor;
/// success names the user just verified, through <see cref="IAuditWriter.RecordSignIn"/> — the one
/// caller that method allows. A bearer token sent with this request reaches no actor field.
/// </para>
/// <para>
/// <b>Concurrency.</b> Parallel attempts on one account race on its <c>rowversion</c>; the loser
/// reloads and applies its already-verified outcome again (<see cref="IdentityRetry"/>), so every
/// failure is counted. The username is matched on its normalized form, as ASP.NET Core Identity
/// does.
/// </para>
/// </remarks>
public sealed class LoginHandler(
    IIdentityDbContext db,
    IPasswordService passwords,
    IRefreshTokenGenerator refreshTokens,
    IAccessTokenIssuer accessTokens,
    IIdGenerator ids,
    IAuditWriter audit,
    TimeProvider clock,
    ILogger<LoginHandler> logger)
{
    /// <summary>The longest username R20 allows; anything longer cannot match and is not looked up.</summary>
    private const int MaximumUserNameLength = 50;

    public async Task<Result<SignInResult>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await FindAsync(command.UserName, cancellationToken);

        // Always one full hash verification, whatever the account's state (R23).
        var passwordVerified = await passwords.VerifyAsync(user, command.Password ?? string.Empty, cancellationToken);

        if (user is null)
        {
            audit.RecordWithoutActor(IdentityAuditActions.LoginFailed, IdentityAuditSubjects.User, subjectId: null, before: null, after: null);
            await db.SaveChangesAsync(cancellationToken);
            return Rejected();
        }

        var userId = user.Id;
        return await IdentityRetry.RunAsync(
            db,
            async attempt =>
            {
                var current = attempt == 1 ? user : await IdentityQueries.FindUserAsync(db, userId, cancellationToken);
                if (current is null)
                {
                    return Rejected();
                }

                return await DecideAsync(current, passwordVerified, cancellationToken);
            },
            cancellationToken);
    }

    private async Task<Result<SignInResult>> DecideAsync(StaffUser user, bool passwordVerified, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        if (user.IsDisabled || user.IsLockedOut(now))
        {
            audit.RecordWithoutActor(IdentityAuditActions.LoginFailed, IdentityAuditSubjects.User, user.Id, before: null, after: null);
            await db.SaveChangesAsync(cancellationToken);
            return Rejected();
        }

        if (!passwordVerified)
        {
            var lockedOut = user.RecordFailedSignIn(now);
            audit.RecordWithoutActor(IdentityAuditActions.LoginFailed, IdentityAuditSubjects.User, user.Id, before: null, after: null);
            if (lockedOut)
            {
                var grants = await IdentityQueries.RoleGrantsAsync(db, user.Roles.Select(role => role.RoleId), cancellationToken);
                audit.RecordWithoutActor(
                    IdentityAuditActions.LockedOut,
                    IdentityAuditSubjects.User,
                    user.Id,
                    before: null,
                    after: UserAuditSnapshot.From(user, grants.Roles));
            }

            await db.SaveChangesAsync(cancellationToken);
            return Rejected();
        }

        user.RecordSuccessfulSignIn();
        var refreshToken = refreshTokens.Generate();
        var session = AuthSession.Start(ids.New(), user.Id, ids.New(), refreshToken.TokenHash, now, AuthLifetimes.Session);
        db.AuthSessions.Add(session);

        var roles = await IdentityQueries.RoleGrantsAsync(db, user.Roles.Select(role => role.RoleId), cancellationToken);
        audit.RecordSignIn(
            user.Id,
            roles.Roles,
            IdentityAuditActions.LoginSucceeded,
            IdentityAuditSubjects.User,
            AuthSessionAuditSnapshot.From(session));

        await db.SaveChangesAsync(cancellationToken);

        IdentityTelemetry.LoginSucceeded.Add(1);
        IdentityTelemetry.LogLoginSucceeded(logger, null);

        var accessToken = accessTokens.Issue(user.Id, session.Id);
        return new SignInResult(accessToken.Value, accessToken.ExpiresAtUtc, refreshToken.RawToken, session.ExpiresAtUtc);
    }

    private async Task<StaffUser?> FindAsync(string? userName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(userName) || userName.Length > MaximumUserNameLength)
        {
            return null;
        }

        var normalized = userName.ToUpperInvariant();
        return await db.Users
            .Include(user => user.Roles)
            .SingleOrDefaultAsync(user => user.NormalizedUserName == normalized, cancellationToken);
    }

    private Result<SignInResult> Rejected()
    {
        IdentityTelemetry.LoginFailed.Add(1);
        IdentityTelemetry.LogLoginFailed(logger, null);
        return IdentityErrors.InvalidCredentials;
    }
}

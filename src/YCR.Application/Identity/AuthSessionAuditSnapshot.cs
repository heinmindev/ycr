using YCR.Application.Common.Abstractions;
using YCR.Domain.Identity;

namespace YCR.Application.Identity;

/// <summary>
/// A session's audit state (ADR-0021; spec §8). <c>PayloadVersion</c> 1.
/// </summary>
/// <remarks>
/// G1 (hein, 2026-09-23): session events have the user as subject, and this snapshot carries the
/// session id and — for a revocation — the reason. REQUIRED CONTROL (R13): never a token, token
/// hash or cookie value.
/// </remarks>
/// <param name="SessionId">The <c>identity.AuthSessions</c> id (the refresh family, D12).</param>
/// <param name="UserId">The session's user.</param>
/// <param name="RevocationReason">A <see cref="Domain.Identity.RevocationReason"/> name, or null while active.</param>
public sealed record AuthSessionAuditSnapshot(Guid SessionId, Guid UserId, string? RevocationReason) : IAuditSnapshot
{
    public static AuthSessionAuditSnapshot From(AuthSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new AuthSessionAuditSnapshot(session.Id, session.UserId, session.RevocationReason?.ToString());
    }
}

using System.Security.Cryptography;
using YCR.Domain.Common;

namespace YCR.Domain.Identity;

/// <summary>
/// A signed-in session, and also its refresh-token family: one AuthSession is one family
/// (ENGINEERING DECISION, hein, 2026-09-23; D12; ADR-0023 records the C5 clarification of ADR-0016).
/// </summary>
/// <remarks>
/// The absolute lifetime is fixed at start (12 h, D12) and never extended: there is no idle
/// timeout. The domain decides what a presented refresh token means (<see cref="Rotate"/>); the
/// handler persists it, and the database makes the rotation race-safe (plan P15).
/// </remarks>
public sealed class AuthSession : AggregateRoot
{
    private readonly List<RefreshToken> _tokens = [];

    private AuthSession()
    {
    }

    public Guid UserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public RevocationReason? RevocationReason { get; private set; }

    public IReadOnlyCollection<RefreshToken> Tokens => _tokens.AsReadOnly();

    public static AuthSession Start(
        Guid id,
        Guid userId,
        Guid firstTokenId,
        byte[] tokenHash,
        DateTimeOffset nowUtc,
        TimeSpan lifetime)
    {
        EnsureUtc(nowUtc);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);

        var session = new AuthSession
        {
            Id = id,
            UserId = userId,
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc + lifetime,
        };
        session._tokens.Add(RefreshToken.Issue(firstTokenId, id, tokenHash, nowUtc));
        return session;
    }

    /// <summary>Not revoked and before its absolute expiry.</summary>
    public bool IsActive(DateTimeOffset nowUtc) => RevokedAtUtc is null && nowUtc < ExpiresAtUtc;

    /// <summary>
    /// Presents a refresh token to this family (ADR-0016 §Refresh; spec R5–R8):
    /// <list type="number">
    /// <item>Session revoked or expired, or the hash is not in this family → <see cref="RefreshOutcome.Invalid"/>.</item>
    /// <item>The current token → rotated, successor added → <see cref="RefreshOutcome.Rotated"/>.</item>
    /// <item>The immediate predecessor of the current token, within <paramref name="grace"/> of its
    /// rotation → <see cref="RefreshOutcome.Superseded"/>, nothing changes.</item>
    /// <item>Anything older, or the predecessor after the grace window → the family is revoked
    /// (<see cref="Identity.RevocationReason.FamilyReuse"/>) → <see cref="RefreshOutcome.FamilyReused"/>.</item>
    /// </list>
    /// </summary>
    public RefreshOutcome Rotate(
        byte[] presentedHash,
        Guid newTokenId,
        byte[] newHash,
        DateTimeOffset nowUtc,
        TimeSpan grace)
    {
        ArgumentNullException.ThrowIfNull(presentedHash);
        EnsureUtc(nowUtc);
        ArgumentOutOfRangeException.ThrowIfLessThan(grace, TimeSpan.Zero);

        if (!IsActive(nowUtc))
        {
            return RefreshOutcome.Invalid;
        }

        var presented = _tokens.Find(token => CryptographicOperations.FixedTimeEquals(token.TokenHash, presentedHash));
        if (presented is null)
        {
            return RefreshOutcome.Invalid;
        }

        if (!presented.IsRotated)
        {
            var successor = RefreshToken.Issue(newTokenId, Id, newHash, nowUtc);
            presented.MarkRotated(successor.Id, nowUtc);
            _tokens.Add(successor);
            return RefreshOutcome.Rotated;
        }

        var successorOfPresented = _tokens.Find(token => token.Id == presented.ReplacedByTokenId);
        if (successorOfPresented is { IsRotated: false } && nowUtc - presented.RotatedAtUtc <= grace)
        {
            return RefreshOutcome.Superseded;
        }

        Revoke(Identity.RevocationReason.FamilyReuse, nowUtc);
        return RefreshOutcome.FamilyReused;
    }

    /// <summary>Revocation is terminal; revoking again changes nothing and is not audited again.</summary>
    /// <returns><see langword="true"/> when this call revoked the session.</returns>
    public bool Revoke(RevocationReason reason, DateTimeOffset nowUtc)
    {
        EnsureUtc(nowUtc);
        if (RevokedAtUtc is not null)
        {
            return false;
        }

        RevokedAtUtc = nowUtc;
        RevocationReason = reason;
        return true;
    }

    private static void EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Identity timestamps must use the UTC offset (ADR-0018).", nameof(value));
        }
    }
}

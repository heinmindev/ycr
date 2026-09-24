using YCR.Domain.Common;

namespace YCR.Domain.Identity;

/// <summary>
/// One refresh token of an <see cref="AuthSession"/>'s family (spec §7 <c>identity.RefreshTokens</c>).
/// Only its SHA-256 hash is stored (R5, S31). Its usable lifetime is the session's (D12), so it
/// has no expiry of its own.
/// </summary>
public sealed class RefreshToken : Entity
{
    public const int HashLength = 32;

    private RefreshToken()
    {
    }

    public Guid SessionId { get; private set; }

    public byte[] TokenHash { get; private set; } = null!;

    public DateTimeOffset IssuedAtUtc { get; private set; }

    public DateTimeOffset? RotatedAtUtc { get; private set; }

    /// <summary>At most one successor, enforced by the database (R6, S33; plan P15).</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    public bool IsRotated => ReplacedByTokenId is not null;

    internal static RefreshToken Issue(Guid id, Guid sessionId, byte[] tokenHash, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        if (tokenHash.Length != HashLength)
        {
            throw new ArgumentException($"A refresh-token hash is {HashLength} bytes (SHA-256).", nameof(tokenHash));
        }

        return new RefreshToken
        {
            Id = id,
            SessionId = sessionId,
            TokenHash = (byte[])tokenHash.Clone(),
            IssuedAtUtc = nowUtc,
        };
    }

    internal void MarkRotated(Guid successorId, DateTimeOffset nowUtc)
    {
        RotatedAtUtc = nowUtc;
        ReplacedByTokenId = successorId;
    }
}

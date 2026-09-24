namespace YCR.Application.Identity.Abstractions;

/// <summary>
/// Refresh tokens: random, and stored only as a hash (ADR-0016; spec R5, S31).
/// </summary>
public interface IRefreshTokenGenerator
{
    /// <summary>A new token: the raw value for the cookie and its hash for storage.</summary>
    RefreshTokenMaterial Generate();

    /// <summary>The storage hash of a presented raw token (any string, including a malformed one).</summary>
    byte[] Hash(string rawToken);
}

/// <param name="RawToken">Goes into the cookie only; never logged, audited or stored.</param>
/// <param name="TokenHash">SHA-256, 32 bytes; the only form that reaches the database.</param>
public sealed record RefreshTokenMaterial(string RawToken, byte[] TokenHash)
{
    /// <summary>Never print the raw token, even by accident (R13).</summary>
    public override string ToString() => "RefreshTokenMaterial { RawToken = ***, TokenHash = *** }";
}

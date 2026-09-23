using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using YCR.Application.Identity.Abstractions;

namespace YCR.Infrastructure.Identity;

/// <summary>
/// 256-bit random refresh tokens, stored as SHA-256 (ADR-0016; spec R5, S31).
/// </summary>
/// <remarks>
/// The raw token is the base64url text of 32 random bytes; the stored hash is SHA-256 over that
/// text's UTF-8 bytes. Hashing the text rather than decoding it first means any presented cookie
/// value — however malformed — has a hash, which simply matches no row.
/// </remarks>
internal sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    public const int TokenBytes = 32;

    public RefreshTokenMaterial Generate()
    {
        var raw = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
        return new RefreshTokenMaterial(raw, Hash(raw));
    }

    public byte[] Hash(string rawToken)
    {
        ArgumentNullException.ThrowIfNull(rawToken);
        return SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
    }
}

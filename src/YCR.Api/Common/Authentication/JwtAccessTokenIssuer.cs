using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using YCR.Application.Identity;
using YCR.Application.Identity.Abstractions;

namespace YCR.Api.Common.Authentication;

/// <summary>
/// Issues the ES256 access JWT (ADR-0016; ADR-0023 items 7–8; spec R1, S20).
/// </summary>
/// <remarks>
/// Payload: exactly <c>sub</c>, <c>sid</c>, <c>jti</c>, <c>iss</c>, <c>aud</c>, <c>iat</c>,
/// <c>nbf</c>, <c>exp</c> — no role, permission or username (C7). Times come from
/// <see cref="TimeProvider"/> (plan P11), so a test clock controls <c>exp</c>. Lives in the API,
/// next to <c>JwtBearerHandler</c>, to reuse the IdentityModel it already brings (plan
/// §New packages).
/// </remarks>
public sealed class JwtAccessTokenIssuer(
    ISigningKeyProvider keys,
    IOptions<AuthOptions> options,
    TimeProvider clock) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler handler = new() { SetDefaultTimesOnTokenCreation = false };

    public AccessToken Issue(Guid userId, Guid sessionId)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow();
        var expires = now + AuthLifetimes.AccessToken;

        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = keys.SigningCredentials,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = userId.ToString(),
                [AuthClaims.SessionId] = sessionId.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
            },
        });

        return new AccessToken(token, expires);
    }
}

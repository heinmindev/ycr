using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using YCR.Api.Common.Authorization;
using YCR.Application.Identity.ResolveSessionPrincipal;

namespace YCR.Api.Common.Authentication;

/// <summary>
/// Configures the framework <see cref="JwtBearerHandler"/> — the only scheme a deployment may
/// register (ADR-0020 items 4–5 as amended; D13, D15). No YCR handler type exists.
/// </summary>
/// <remarks>
/// <para>
/// <b>Validation (plan P11).</b> ES256 only, the configured keys, issuer and audience; <c>exp</c>
/// required; lifetime judged against <see cref="TimeProvider"/> with a 30-second skew, so a test
/// clock controls expiry (V3); no validation reason in <c>WWW-Authenticate</c>.
/// </para>
/// <para>
/// <b>Principal rebuilt server-side (plan P3; spec O3/O4).</b> After the signature checks out, only
/// <c>sub</c> and <c>sid</c> are read. The session and user are resolved (through
/// <see cref="SessionPrincipalCache"/>) and the token's principal is <em>replaced</em> by a new one
/// carrying the user id, one role claim per role, one permission claim per permission, the
/// <c>sid</c>, and the must-change marker when set. Nothing else a token carries survives, so no
/// claim smuggled into a token can grant anything. An unknown, revoked or expired session, a
/// <c>sub</c> that is not the session's user, or a disabled user fails authentication.
/// </para>
/// <para>
/// <b>Challenge (D14).</b> Every <c>401</c> from this scheme is ProblemDetails with
/// <c>errorCode</c> <c>Auth.Unauthenticated</c> and <c>WWW-Authenticate: Bearer</c>.
/// </para>
/// </remarks>
public sealed class JwtBearerSetup(
    ISigningKeyProvider keys,
    IOptions<AuthOptions> authOptions,
    TimeProvider clock) : IConfigureNamedOptions<JwtBearerOptions>
{
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    public void Configure(JwtBearerOptions options) => Configure(JwtBearerDefaults.AuthenticationScheme, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        var settings = authOptions.Value;
        options.MapInboundClaims = false;
        options.IncludeErrorDetails = false;
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = settings.Issuer,
            ValidAudience = settings.Audience,
            IssuerSigningKeys = keys.ValidationKeys,
            ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = ClockSkew,
            LifetimeValidator = ValidateLifetime,
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = RebuildPrincipalAsync,
            OnChallenge = WriteChallengeAsync,
        };
    }

    private bool ValidateLifetime(DateTime? notBefore, DateTime? expires, SecurityToken token, TokenValidationParameters parameters)
    {
        if (expires is null)
        {
            return false;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        return (notBefore is null || notBefore.Value <= now + ClockSkew) && now < expires.Value + ClockSkew;
    }

    private static async Task RebuildPrincipalAsync(TokenValidatedContext context)
    {
        var token = context.Principal;
        if (!Guid.TryParse(token?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)
            || !Guid.TryParse(token?.FindFirstValue(AuthClaims.SessionId), out var sessionId))
        {
            context.Fail("The access token does not name a user and a session.");
            return;
        }

        var services = context.HttpContext.RequestServices;
        var cache = services.GetRequiredService<SessionPrincipalCache>();
        var principal = await cache.GetAsync(
            userId,
            sessionId,
            () => services.GetRequiredService<ResolveSessionPrincipalHandler>()
                .Handle(new ResolveSessionPrincipalQuery(userId, sessionId), context.HttpContext.RequestAborted));

        if (principal is null)
        {
            context.Fail("The session is not active.");
            return;
        }

        var identity = new ClaimsIdentity(JwtBearerDefaults.AuthenticationScheme, ClaimTypes.NameIdentifier, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, principal.UserId.ToString()));
        identity.AddClaim(new Claim(AuthClaims.SessionId, principal.SessionId.ToString()));
        identity.AddClaims(principal.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        identity.AddClaims(principal.Permissions.Select(permission => new Claim(PermissionAuthorizationHandler.PermissionClaimType, permission)));
        if (principal.MustChangePassword)
        {
            identity.AddClaim(new Claim(AuthClaims.MustChangePassword, "true"));
        }

        context.Principal = new ClaimsPrincipal(identity);
    }

    private static async Task WriteChallengeAsync(JwtBearerChallengeContext context)
    {
        context.HandleResponse();
        context.Response.Headers.WWWAuthenticate = JwtBearerDefaults.AuthenticationScheme;
        await AuthProblem.WriteAsync(
            context.HttpContext,
            StatusCodes.Status401Unauthorized,
            "Unauthorized",
            "This endpoint requires a valid access token.",
            AuthErrorCodes.Unauthenticated);
    }
}

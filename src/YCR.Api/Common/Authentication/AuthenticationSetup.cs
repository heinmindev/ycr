using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using YCR.Application.Identity.Abstractions;

namespace YCR.Api.Common.Authentication;

/// <summary>The F-002 authentication composition, kept out of <c>Program</c> so it reads in one place.</summary>
public static class AuthenticationSetup
{
    /// <summary>
    /// Registers the <c>Auth</c> options (validated at start), the signing keys, the access-token
    /// issuer, the principal cache, and the framework <see cref="JwtBearerHandler"/> as the
    /// default scheme.
    /// </summary>
    public static IServiceCollection AddYcrAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AuthOptions>, AuthOptionsValidator>();

        services.AddSingleton<ISigningKeyProvider, ConfigurationSigningKeyProvider>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddMemoryCache();
        services.AddSingleton<SessionPrincipalCache>();
        services.AddSingleton<AuthRateLimiters>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, JwtBearerSetup>();

        return services;
    }

    /// <summary>
    /// Fails startup on a signing key that is missing, malformed or not allowed in this
    /// environment (S26a) — before the first request, not on the first sign-in.
    /// </summary>
    public static void ValidateSigningKeys(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        _ = app.Services.GetRequiredService<ISigningKeyProvider>();
    }
}

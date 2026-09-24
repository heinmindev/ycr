namespace YCR.Api.Common.Authentication;

/// <summary>
/// The <c>Auth</c> configuration section (ADR-0023; plan F-002 P4, P6, P11, D11, D12).
/// Non-secret defaults live here and in <c>appsettings.json</c>; the signing key never does.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>The upper bound R3 puts on revocation latency.</summary>
    public const int MaximumPrincipalCacheSeconds = 30;

    /// <summary>The access token's <c>iss</c>, and the only issuer accepted.</summary>
    public string Issuer { get; set; } = "YCR.Api";

    /// <summary>The access token's <c>aud</c>, and the only audience accepted.</summary>
    public string Audience { get; set; } = "YCR.Api";

    /// <summary>D12: 15 minutes.</summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>D12: 12 hours, absolute (see <c>AuthSettings</c>).</summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromHours(12);

    /// <summary>D12 / ADR-0016: about 20 seconds.</summary>
    public TimeSpan RefreshGraceWindow { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Plan P4: how long a resolved principal is reused. Must be above 0 and at most
    /// <see cref="MaximumPrincipalCacheSeconds"/>; startup fails otherwise.
    /// </summary>
    public int PrincipalCacheSeconds { get; set; } = 15;

    /// <summary>
    /// ADR-0016 / R9: the exact origins (scheme, host, port) allowed on the cookie endpoints.
    /// Required outside Development and Testing, and each must be an absolute <c>https</c> origin.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];

    public SigningOptions Signing { get; set; } = new();

    /// <summary>R16 / U6: the in-process limits on the anonymous cookie endpoints.</summary>
    public AuthRateLimitOptions RateLimits { get; set; } = new();
}

/// <summary>
/// R16 (D5, U6; ADR-0023 item 5): per-minute limits, in process and per instance. The defaults
/// are the shipped values; a test asserts them (S5, S12).
/// </summary>
public sealed class AuthRateLimitOptions
{
    /// <summary>Sign-in attempts per minute for one normalized username.</summary>
    public int LoginPerUserNamePerMinute { get; set; } = 5;

    /// <summary>Sign-in attempts per minute from one client address.</summary>
    public int LoginPerClientAddressPerMinute { get; set; } = 20;

    /// <summary>Refreshes per minute from one client address (a refresh carries no username).</summary>
    public int RefreshPerClientAddressPerMinute { get; set; } = 30;
}

/// <summary>
/// D11 / ADR-0023 item 7: ES256 signing keys, injected through configuration or a secret store and
/// never committed. The key list with an active <c>kid</c> makes rotation a configuration change.
/// </summary>
public sealed class SigningOptions
{
    public string? ActiveKeyId { get; set; }

    public List<SigningKeyEntry> Keys { get; set; } = [];
}

/// <param name="KeyId">The JWT <c>kid</c>. A <c>dev-</c> or <c>test-</c> prefix marks a key production refuses.</param>
public sealed class SigningKeyEntry
{
    public string? KeyId { get; set; }

    /// <summary>A PKCS#8 PEM P-256 private key. Secret: configuration or user-secrets only.</summary>
    public string? PrivateKeyPkcs8Pem { get; set; }

    /// <summary>A developer's own key; production refuses to start with one (S26a).</summary>
    public bool DevelopmentOnly { get; set; }
}

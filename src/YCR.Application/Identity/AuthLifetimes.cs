namespace YCR.Application.Identity;

/// <summary>
/// The fixed token and session lifetimes (ADR-0023 item 8; D12). They are constants, not
/// configuration: <c>YCR.Api</c> refuses to start when <c>Auth:AccessTokenLifetime</c>,
/// <c>Auth:SessionLifetime</c> or <c>Auth:RefreshGraceWindow</c> is set to anything else.
/// </summary>
/// <remarks>
/// ENGINEERING DECISION (hein, 2026-09-23; D12): a session lives 12 hours from sign-in with no
/// idle extension; a rotated refresh token's immediate predecessor is tolerated for 20 seconds
/// (ADR-0016's "approximately 20 seconds"). Fixed in code by hein's ruling on review finding C-1
/// (2026-09-24, T-030), so an operator cannot change the approved policy by configuration.
/// </remarks>
public static class AuthLifetimes
{
    /// <summary>The access token's lifetime (R1).</summary>
    public static readonly TimeSpan AccessToken = TimeSpan.FromMinutes(15);

    /// <summary>Absolute session lifetime (R2, S13).</summary>
    public static readonly TimeSpan Session = TimeSpan.FromHours(12);

    /// <summary>The predecessor grace window (R7, S9).</summary>
    public static readonly TimeSpan RefreshGraceWindow = TimeSpan.FromSeconds(20);
}

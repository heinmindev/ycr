namespace YCR.Application.Identity;

/// <summary>
/// The session settings the Identity handlers need (ADR-0023 item 8; D12). Registered as a
/// singleton; <c>YCR.Api</c> binds it from the <c>Auth</c> configuration section.
/// </summary>
/// <remarks>
/// ENGINEERING DECISION (hein, 2026-09-23; D12): a session lives 12 hours from sign-in with no
/// idle extension; a rotated refresh token's immediate predecessor is tolerated for a configured
/// grace window, default 20 seconds (ADR-0016's "approximately 20 seconds").
/// </remarks>
public sealed class AuthSettings
{
    public static readonly TimeSpan DefaultSessionLifetime = TimeSpan.FromHours(12);

    public static readonly TimeSpan DefaultRefreshGraceWindow = TimeSpan.FromSeconds(20);

    /// <summary>Absolute session lifetime (R2, S13).</summary>
    public TimeSpan SessionLifetime { get; init; } = DefaultSessionLifetime;

    /// <summary>The predecessor grace window (R7, S9).</summary>
    public TimeSpan RefreshGraceWindow { get; init; } = DefaultRefreshGraceWindow;
}

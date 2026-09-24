namespace YCR.Domain.Identity;

/// <summary>
/// ENGINEERING DECISION (hein, 2026-09-23; D3; ADR-0023 item 3): ten consecutive failed sign-ins
/// lock the account for fifteen minutes, then it unlocks automatically (or earlier by
/// administrator unlock, U3). OQ35: no mandated policy is known.
/// </summary>
public static class AccountLockoutPolicy
{
    public const int MaxFailedAttempts = 10;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
}

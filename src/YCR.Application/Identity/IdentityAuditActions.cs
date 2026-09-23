namespace YCR.Application.Identity;

/// <summary>
/// The thirteen Identity audit actions (spec R22; ADR-0023 item 6; D6 and U3). Exactly one row per
/// operation. Permanent once written: the ledger is append-only.
/// </summary>
/// <remarks>
/// <c>docs/17</c>'s <c>AuthLoginSucceeded</c>-style names are log and metric names
/// (<see cref="IdentityTelemetry"/>), not audit actions (C11). A within-grace refresh predecessor
/// is logged and counted, never audited (R7).
/// </remarks>
public static class IdentityAuditActions
{
    public const string LoginSucceeded = "Identity.LoginSucceeded";
    public const string LoginFailed = "Identity.LoginFailed";
    public const string LockedOut = "Identity.LockedOut";
    public const string LoggedOut = "Identity.LoggedOut";
    public const string PasswordChanged = "Identity.PasswordChanged";
    public const string PasswordReset = "Identity.PasswordReset";
    public const string UserCreated = "Identity.UserCreated";
    public const string UserDisabled = "Identity.UserDisabled";
    public const string UserEnabled = "Identity.UserEnabled";
    public const string UserUnlocked = "Identity.UserUnlocked";
    public const string RolesChanged = "Identity.RolesChanged";
    public const string SessionRevoked = "Identity.SessionRevoked";
    public const string RefreshFamilyRevoked = "Identity.RefreshFamilyRevoked";
}

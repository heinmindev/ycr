using YCR.Domain.Common;

namespace YCR.Domain.Identity;

/// <summary>
/// Every error the Identity use cases return (spec §6, R21; plan F-002 §API changes).
/// </summary>
/// <remarks>
/// ENGINEERING DECISION (hein, 2026-09-23; D14, U1; ADR-0023 item 9; <c>docs/20</c> §2): the prefix
/// follows the path. Endpoints under <c>/auth/*</c> return <c>Auth.*</c>; endpoints under
/// <c>/users/*</c>, <c>/auth-sessions/*</c> and <c>/roles</c> return <c>Identity.*</c>. A password
/// policy rejection therefore has two codes, one per path, and the handler that owns the path
/// picks it.
/// <para>
/// The pipeline codes raised before any endpoint runs (<c>Auth.Unauthenticated</c>,
/// <c>Auth.PasswordChangeRequired</c>, <c>Auth.OriginRejected</c>, <c>Auth.TooManyRequests</c>)
/// are written by <c>YCR.Api</c>, which may not reference this namespace; they live there.
/// </para>
/// </remarks>
public static class IdentityErrors
{
    // /auth/* — sign-in, refresh, own password.

    /// <summary>Uniform for wrong password, unknown user, disabled and locked accounts (R23).</summary>
    public static readonly Error InvalidCredentials = Error.Unauthorized(
        "Auth.InvalidCredentials",
        "The username or password is incorrect.");

    public static readonly Error RefreshInvalid = Error.Unauthorized(
        "Auth.RefreshInvalid",
        "The refresh token is not valid.");

    /// <summary>ADR-0016: the immediate predecessor presented within the grace window.</summary>
    public static readonly Error RefreshSuperseded = Error.Conflict(
        "Auth.RefreshSuperseded",
        "The refresh token has already been rotated.");

    public static readonly Error AuthPasswordRejected = Error.Validation(
        "Auth.PasswordRejected",
        "The new password does not meet the password policy.");

    public static readonly Error CurrentPasswordIncorrect = Error.BusinessRule(
        "Auth.CurrentPasswordIncorrect",
        "The current password is incorrect.");

    // /users/*, /auth-sessions/*, /roles — user administration.

    public static readonly Error InvalidUserName = Error.Validation(
        "Identity.InvalidUserName",
        "A username is 3 to 50 characters of lower-case a-z, 0-9 and '.'.");

    public static readonly Error UserNotFound = Error.NotFound(
        "Identity.UserNotFound",
        "User was not found.");

    public static readonly Error SessionNotFound = Error.NotFound(
        "Identity.SessionNotFound",
        "Session was not found.");

    public static readonly Error UserNameAlreadyExists = Error.Conflict(
        "Identity.UserNameAlreadyExists",
        "The username is already in use.");

    public static readonly Error IdentityPasswordRejected = Error.Validation(
        "Identity.PasswordRejected",
        "The password does not meet the password policy.");

    public static readonly Error CannotChangeOwnRoles = Error.BusinessRule(
        "Identity.CannotChangeOwnRoles",
        "A user may not change their own roles.");

    public static readonly Error CannotDisableOwnAccount = Error.BusinessRule(
        "Identity.CannotDisableOwnAccount",
        "An administrator may not disable their own account.");

    public static readonly Error CannotResetOwnPassword = Error.BusinessRule(
        "Identity.CannotResetOwnPassword",
        "An administrator may not reset their own password; use the own password change.");

    public static readonly Error LastAdministrator = Error.BusinessRule(
        "Identity.LastAdministrator",
        "At least one active SystemAdministrator must remain.");

    /// <summary>A role name outside the catalogue (R19). The API's validator normally answers first.</summary>
    public static readonly Error UnknownRole = Error.Validation(
        "Identity.UnknownRole",
        "One or more role names are not in the role catalogue.");

    /// <summary>The <c>docs/20</c> §4 page limits, per module (the <c>Network.InvalidPageRequest</c> pattern).</summary>
    public static readonly Error InvalidPageRequest = Error.Validation(
        "Identity.InvalidPageRequest",
        "Page must be 1 or greater and pageSize must be between 1 and 200.");

    /// <summary>D10: the one-time bootstrap refuses once any <c>SystemAdministrator</c> exists (S32).</summary>
    public static readonly Error AdministratorAlreadyExists = Error.BusinessRule(
        "Identity.AdministratorAlreadyExists",
        "A SystemAdministrator already exists; the bootstrap runs only once.");
}

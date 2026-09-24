namespace YCR.Domain.Identity;

/// <summary>
/// Why an <see cref="AuthSession"/> was revoked (spec §7, R4). Stored as a check-constrained
/// string, so the names are permanent once written.
/// </summary>
public enum RevocationReason
{
    /// <summary>The session's own user signed out (the token's <c>sid</c>).</summary>
    Logout,

    /// <summary>The user changed their own password; every other session of theirs is revoked.</summary>
    PasswordChanged,

    /// <summary>An administrator reset the user's password; every session is revoked.</summary>
    AdministratorPasswordReset,

    /// <summary>The user was disabled; every session is revoked.</summary>
    UserDisabled,

    /// <summary>An administrator revoked this session.</summary>
    AdministratorRevoked,

    /// <summary>A rotated refresh token was presented outside the grace window (R8).</summary>
    FamilyReuse,
}

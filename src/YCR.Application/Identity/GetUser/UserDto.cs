namespace YCR.Application.Identity.GetUser;

/// <summary>
/// A staff account as the administration API shows it (spec §6.2: username, roles, disabled,
/// locked-until, created). Never a hash, stamp or token.
/// </summary>
/// <param name="LockedUntilUtc">When a lockout in force ends; null when not locked now.</param>
public sealed record UserDto(
    Guid Id,
    string UserName,
    IReadOnlyList<string> Roles,
    bool IsDisabled,
    DateTimeOffset? LockedUntilUtc,
    DateTimeOffset CreatedAtUtc);

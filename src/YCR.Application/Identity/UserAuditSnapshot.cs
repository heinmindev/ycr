using YCR.Application.Common.Abstractions;
using YCR.Domain.Identity;

namespace YCR.Application.Identity;

/// <summary>
/// A staff account's audit state (ADR-0021; spec §8). <c>PayloadVersion</c> 1.
/// </summary>
/// <remarks>
/// D6 (hein, 2026-09-23): a resolved user's username may appear in an audit snapshot. REQUIRED
/// CONTROL (R13; ADR-0021 rule 4): never the password hash, the security stamp, a token or a
/// cookie. Changing this shape requires a <c>PayloadVersion</c> bump (ADR-0021 rule 3).
/// </remarks>
/// <param name="UserName">The username (the staff identifier, R20).</param>
/// <param name="Roles">Canonical role identifiers (R19), in <see cref="RoleNames.All"/> order.</param>
/// <param name="IsDisabled">Whether the account is disabled.</param>
/// <param name="LockoutEndUtc">When a lockout ends, if one was set.</param>
/// <param name="MustChangePassword">Whether the password must be changed (R26).</param>
public sealed record UserAuditSnapshot(
    string UserName,
    IReadOnlyList<string> Roles,
    bool IsDisabled,
    DateTimeOffset? LockoutEndUtc,
    bool MustChangePassword) : IAuditSnapshot
{
    public static UserAuditSnapshot From(StaffUser user, IReadOnlyList<string> roles)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roles);

        return new UserAuditSnapshot(
            user.UserName.Value,
            roles,
            user.IsDisabled,
            user.LockoutEndUtc,
            user.MustChangePassword);
    }
}

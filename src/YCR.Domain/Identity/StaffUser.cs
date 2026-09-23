using YCR.Domain.Common;

namespace YCR.Domain.Identity;

/// <summary>
/// A staff account (spec §7 <c>identity.Users</c>; plan F-002 §Domain changes).
/// </summary>
/// <remarks>
/// Lockout lives here, on the caller's clock, not in ASP.NET Core Identity (plan P2). Password
/// hashing and the password policy are Identity's, applied through the Infrastructure store,
/// which is the only caller of <see cref="ApplyPasswordHash"/> and <see cref="ApplySecurityStamp"/>.
/// The last-active-<c>SystemAdministrator</c> rule (R27) is a set invariant across users, so it is
/// enforced by the handlers under a lock (P14), not here.
/// </remarks>
public sealed class StaffUser : AggregateRoot
{
    private readonly List<UserRole> _roles = [];

    private StaffUser()
    {
    }

    public UserName UserName { get; private set; } = null!;

    /// <summary>
    /// Upper-invariant form, as ASP.NET Core Identity's default lookup normalizer produces it.
    /// </summary>
    public string NormalizedUserName { get; private set; } = null!;

    /// <summary>Null only until the first password is applied; the column is not null.</summary>
    public string? PasswordHash { get; private set; }

    public string? SecurityStamp { get; private set; }

    public bool IsDisabled { get; private set; }

    public DateTimeOffset? DisabledAtUtc { get; private set; }

    public DateTimeOffset? LockoutEndUtc { get; private set; }

    public int AccessFailedCount { get; private set; }

    public DateTimeOffset PasswordChangedAtUtc { get; private set; }

    /// <summary>R26 / U2: set for the bootstrap account and every administrator-set password.</summary>
    public bool MustChangePassword { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();

    /// <summary>
    /// A new account whose first password is set by an administrator or by the bootstrap, so it
    /// starts must-change (U2). The caller applies the password hash before saving.
    /// </summary>
    public static StaffUser Create(Guid id, UserName userName, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(userName);
        EnsureUtc(nowUtc);

        return new StaffUser
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = userName.Value.ToUpperInvariant(),
            IsDisabled = false,
            AccessFailedCount = 0,
            MustChangePassword = true,
            PasswordChangedAtUtc = nowUtc,
            CreatedAtUtc = nowUtc,
        };
    }

    public bool IsLockedOut(DateTimeOffset nowUtc) => LockoutEndUtc > nowUtc;

    /// <summary>
    /// Counts one failed sign-in. The tenth consecutive failure locks the account for
    /// <see cref="AccountLockoutPolicy.LockoutDuration"/> and resets the count. A failure while
    /// locked changes nothing: the lockout is not extended.
    /// </summary>
    /// <returns><see langword="true"/> when this failure locked the account.</returns>
    public bool RecordFailedSignIn(DateTimeOffset nowUtc)
    {
        EnsureUtc(nowUtc);
        if (IsLockedOut(nowUtc))
        {
            return false;
        }

        AccessFailedCount++;
        if (AccessFailedCount < AccountLockoutPolicy.MaxFailedAttempts)
        {
            return false;
        }

        LockoutEndUtc = nowUtc + AccountLockoutPolicy.LockoutDuration;
        AccessFailedCount = 0;
        return true;
    }

    public void RecordSuccessfulSignIn() => AccessFailedCount = 0;

    /// <summary>
    /// Administrator unlock (U3). Unlocking an account that is not locked changes nothing
    /// (hein's accepted gap-fill: <c>204</c>, no audit row).
    /// </summary>
    /// <returns><see langword="true"/> when the account was locked and is now unlocked.</returns>
    public bool Unlock(DateTimeOffset nowUtc)
    {
        EnsureUtc(nowUtc);
        if (!IsLockedOut(nowUtc))
        {
            return false;
        }

        LockoutEndUtc = null;
        AccessFailedCount = 0;
        return true;
    }

    /// <summary>
    /// Disables the account. Refused for the caller's own account (U5). Disabling an account that
    /// is already disabled changes nothing (hein, G2).
    /// </summary>
    /// <returns><see langword="true"/> when the account changed.</returns>
    public Result<bool> Disable(Guid callerId, DateTimeOffset nowUtc)
    {
        EnsureUtc(nowUtc);
        if (callerId == Id)
        {
            return IdentityErrors.CannotDisableOwnAccount;
        }

        if (IsDisabled)
        {
            return false;
        }

        IsDisabled = true;
        DisabledAtUtc = nowUtc;
        return true;
    }

    /// <summary>Enabling an account that is already active changes nothing (hein, G2).</summary>
    /// <returns><see langword="true"/> when the account changed.</returns>
    public bool Enable()
    {
        if (!IsDisabled)
        {
            return false;
        }

        IsDisabled = false;
        DisabledAtUtc = null;
        return true;
    }

    /// <summary>
    /// Replaces the role set. No user may change their own roles (OQ34 ruling, R20).
    /// <paramref name="callerId"/> is null only for the bootstrap, which has no actor (D10).
    /// Assignments present in both sets are kept, not re-created.
    /// </summary>
    public Result ReplaceRoles(IEnumerable<Guid> roleIds, Guid? callerId)
    {
        ArgumentNullException.ThrowIfNull(roleIds);
        if (callerId == Id)
        {
            return IdentityErrors.CannotChangeOwnRoles;
        }

        var wanted = roleIds.ToHashSet();
        _roles.RemoveAll(role => !wanted.Contains(role.RoleId));
        foreach (var roleId in wanted.Where(roleId => _roles.TrueForAll(role => role.RoleId != roleId)))
        {
            _roles.Add(UserRole.Create(Id, roleId));
        }

        return Result.Success();
    }

    /// <summary>
    /// Administrator password reset (U2, U5): refused for the caller's own account; the new
    /// password is must-change. The caller applies the new hash.
    /// </summary>
    public Result SetPasswordByAdministrator(Guid callerId, DateTimeOffset nowUtc)
    {
        EnsureUtc(nowUtc);
        if (callerId == Id)
        {
            return IdentityErrors.CannotResetOwnPassword;
        }

        MustChangePassword = true;
        PasswordChangedAtUtc = nowUtc;
        return Result.Success();
    }

    /// <summary>Own password change clears must-change (R26). The caller applies the new hash.</summary>
    public void ChangeOwnPassword(DateTimeOffset nowUtc)
    {
        EnsureUtc(nowUtc);
        MustChangePassword = false;
        PasswordChangedAtUtc = nowUtc;
    }

    /// <summary>ASP.NET Core Identity's hash, applied by the Infrastructure user store only.</summary>
    internal void ApplyPasswordHash(string? passwordHash) => PasswordHash = passwordHash;

    /// <summary>ASP.NET Core Identity's security stamp, applied by the Infrastructure user store only.</summary>
    internal void ApplySecurityStamp(string stamp) => SecurityStamp = stamp;

    private static void EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Identity timestamps must use the UTC offset (ADR-0018).", nameof(value));
        }
    }
}

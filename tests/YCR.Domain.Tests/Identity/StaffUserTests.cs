using YCR.Domain.Identity;

namespace YCR.Domain.Tests.Identity;

/// <summary>
/// The staff account aggregate: lockout (S4, R18), self-target rules (R20, U5), disable/enable
/// including hein's G2 ruling (a repeat is a no-op), roles (S18, S19b) and passwords (U2, R26).
/// </summary>
public sealed class StaffUserTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 3, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AdministratorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RoleA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid RoleB = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    [Fact]
    public void Create_WithUtcTime_ReturnsActiveUserWithNoRoles()
    {
        var user = StaffUser.Create(UserId, UserName.Create("hein.min").Value, Now);

        Assert.Equal(UserId, user.Id);
        Assert.Equal("hein.min", user.UserName.Value);
        Assert.Equal("HEIN.MIN", user.NormalizedUserName);
        Assert.False(user.IsDisabled);
        Assert.Null(user.DisabledAtUtc);
        Assert.Null(user.LockoutEndUtc);
        Assert.Equal(0, user.AccessFailedCount);
        Assert.True(user.MustChangePassword);
        Assert.Equal(Now, user.CreatedAtUtc);
        Assert.Equal(Now, user.PasswordChangedAtUtc);
        Assert.Empty(user.Roles);
        Assert.Null(user.PasswordHash);
    }

    [Fact]
    public void Create_WithNonUtcTime_Throws()
    {
        var nonUtc = new DateTimeOffset(2026, 9, 23, 9, 30, 0, TimeSpan.FromHours(6.5));

        Assert.Throws<ArgumentException>(() => StaffUser.Create(UserId, UserName.Create("hein.min").Value, nonUtc));
    }

    [Fact]
    public void RecordFailedSignIn_NineTimes_DoesNotLock()
    {
        var user = CreateUser();

        for (var attempt = 1; attempt <= 9; attempt++)
        {
            Assert.False(user.RecordFailedSignIn(Now));
        }

        Assert.Equal(9, user.AccessFailedCount);
        Assert.False(user.IsLockedOut(Now));
    }

    [Fact]
    public void RecordFailedSignIn_TenthTime_LocksForFifteenMinutesAndResetsCount()
    {
        var user = CreateUser();
        FailNineTimes(user);

        var lockedOut = user.RecordFailedSignIn(Now);

        Assert.True(lockedOut);
        Assert.Equal(Now.AddMinutes(15), user.LockoutEndUtc);
        Assert.Equal(0, user.AccessFailedCount);
        Assert.True(user.IsLockedOut(Now));
        Assert.True(user.IsLockedOut(Now.AddMinutes(15).AddTicks(-1)));
    }

    [Fact]
    public void IsLockedOut_AfterFifteenMinutes_ReturnsFalse()
    {
        var user = CreateLockedUser();

        Assert.False(user.IsLockedOut(Now.AddMinutes(15)));
    }

    [Fact]
    public void RecordFailedSignIn_WhileLocked_DoesNotExtendLockout()
    {
        var user = CreateLockedUser();

        var lockedAgain = user.RecordFailedSignIn(Now.AddMinutes(5));

        Assert.False(lockedAgain);
        Assert.Equal(Now.AddMinutes(15), user.LockoutEndUtc);
        Assert.Equal(0, user.AccessFailedCount);
    }

    [Fact]
    public void RecordFailedSignIn_AfterLockoutExpires_CountsFromZero()
    {
        var user = CreateLockedUser();

        Assert.False(user.RecordFailedSignIn(Now.AddMinutes(15)));

        Assert.Equal(1, user.AccessFailedCount);
        Assert.False(user.IsLockedOut(Now.AddMinutes(15)));
    }

    [Fact]
    public void RecordSuccessfulSignIn_ResetsFailedCount()
    {
        var user = CreateUser();
        FailNineTimes(user);

        user.RecordSuccessfulSignIn();

        Assert.Equal(0, user.AccessFailedCount);
    }

    [Fact]
    public void Unlock_WhenLocked_ClearsLockoutAndCount()
    {
        var user = CreateLockedUser();

        var changed = user.Unlock(Now.AddMinutes(1));

        Assert.True(changed);
        Assert.Null(user.LockoutEndUtc);
        Assert.Equal(0, user.AccessFailedCount);
        Assert.False(user.IsLockedOut(Now.AddMinutes(1)));
    }

    [Fact]
    public void Unlock_WhenNotLocked_ReportsNoChange()
    {
        var user = CreateUser();
        user.RecordFailedSignIn(Now);

        var changed = user.Unlock(Now);

        Assert.False(changed);
        Assert.Equal(1, user.AccessFailedCount);
    }

    [Fact]
    public void Unlock_AfterLockoutExpired_ReportsNoChange()
    {
        var user = CreateLockedUser();

        Assert.False(user.Unlock(Now.AddMinutes(15)));
    }

    [Fact]
    public void Disable_ByAnotherUser_Disables()
    {
        var user = CreateUser();

        var result = user.Disable(AdministratorId, Now);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.True(user.IsDisabled);
        Assert.Equal(Now, user.DisabledAtUtc);
    }

    [Fact]
    public void Disable_BySelf_ReturnsCannotDisableOwnAccount()
    {
        var user = CreateUser();

        var result = user.Disable(UserId, Now);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.CannotDisableOwnAccount, result.Error);
        Assert.False(user.IsDisabled);
    }

    [Fact]
    public void Disable_WhenAlreadyDisabled_ReportsNoChange()
    {
        // G2 (hein, 2026-09-23): a repeat disable is a 204 that changes nothing.
        var user = CreateUser();
        user.Disable(AdministratorId, Now);

        var result = user.Disable(AdministratorId, Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
        Assert.Equal(Now, user.DisabledAtUtc);
    }

    [Fact]
    public void Enable_WhenDisabled_Enables()
    {
        var user = CreateUser();
        user.Disable(AdministratorId, Now);

        var changed = user.Enable();

        Assert.True(changed);
        Assert.False(user.IsDisabled);
        Assert.Null(user.DisabledAtUtc);
    }

    [Fact]
    public void Enable_WhenActive_ReportsNoChange()
    {
        // G2 (hein, 2026-09-23): enabling an active account is a 204 that changes nothing.
        var user = CreateUser();

        Assert.False(user.Enable());
        Assert.False(user.IsDisabled);
    }

    [Fact]
    public void ReplaceRoles_ByAnotherUser_ReplacesSet()
    {
        var user = CreateUser();
        user.ReplaceRoles([RoleA], AdministratorId);

        var result = user.ReplaceRoles([RoleB, RoleB], AdministratorId);

        Assert.True(result.IsSuccess);
        var role = Assert.Single(user.Roles);
        Assert.Equal(RoleB, role.RoleId);
        Assert.Equal(UserId, role.UserId);
    }

    [Fact]
    public void ReplaceRoles_WithSameSet_KeepsTheExistingAssignments()
    {
        var user = CreateUser();
        user.ReplaceRoles([RoleA, RoleB], AdministratorId);
        var before = user.Roles.ToList();

        user.ReplaceRoles([RoleB, RoleA], AdministratorId);

        Assert.Equal(before, user.Roles);
    }

    [Fact]
    public void ReplaceRoles_WithNoActor_IsAllowed()
    {
        // The bootstrap (D10) assigns roles with no actor at all.
        var user = CreateUser();

        Assert.True(user.ReplaceRoles([RoleA], callerId: null).IsSuccess);
        Assert.Single(user.Roles);
    }

    [Fact]
    public void ReplaceRoles_BySelf_ReturnsCannotChangeOwnRoles()
    {
        var user = CreateUser();
        user.ReplaceRoles([RoleA], AdministratorId);

        var result = user.ReplaceRoles([RoleA, RoleB], UserId);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.CannotChangeOwnRoles, result.Error);
        Assert.Equal(RoleA, Assert.Single(user.Roles).RoleId);
    }

    [Fact]
    public void SetPasswordByAdministrator_ForAnotherUser_SetsMustChange()
    {
        var user = CreateUser();
        user.ChangeOwnPassword(Now);

        var result = user.SetPasswordByAdministrator(AdministratorId, Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.True(user.MustChangePassword);
        Assert.Equal(Now.AddHours(1), user.PasswordChangedAtUtc);
    }

    [Fact]
    public void SetPasswordByAdministrator_ForSelf_ReturnsCannotResetOwnPassword()
    {
        var user = CreateUser();
        user.ChangeOwnPassword(Now);

        var result = user.SetPasswordByAdministrator(UserId, Now.AddHours(1));

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.CannotResetOwnPassword, result.Error);
        Assert.False(user.MustChangePassword);
        Assert.Equal(Now, user.PasswordChangedAtUtc);
    }

    [Fact]
    public void ChangeOwnPassword_ClearsMustChange()
    {
        var user = CreateUser();

        user.ChangeOwnPassword(Now.AddMinutes(3));

        Assert.False(user.MustChangePassword);
        Assert.Equal(Now.AddMinutes(3), user.PasswordChangedAtUtc);
    }

    private static StaffUser CreateUser() =>
        StaffUser.Create(UserId, UserName.Create("hein.min").Value, Now);

    private static StaffUser CreateLockedUser()
    {
        var user = CreateUser();
        FailNineTimes(user);
        Assert.True(user.RecordFailedSignIn(Now));
        return user;
    }

    private static void FailNineTimes(StaffUser user)
    {
        for (var attempt = 1; attempt <= 9; attempt++)
        {
            user.RecordFailedSignIn(Now);
        }
    }
}

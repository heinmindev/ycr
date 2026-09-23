namespace YCR.Domain.Identity;

/// <summary>
/// One role held by a <see cref="StaffUser"/> (spec §7 <c>identity.UserRoles</c>). A user may hold
/// several (D7). Replacing roles deletes and inserts these rows; role history is in the
/// <c>Identity.RolesChanged</c> audit rows (plan P7).
/// </summary>
public sealed class UserRole
{
    private UserRole()
    {
    }

    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }

    internal static UserRole Create(Guid userId, Guid roleId) =>
        new() { UserId = userId, RoleId = roleId };
}

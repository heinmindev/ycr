namespace YCR.Domain.Identity;

/// <summary>
/// One role→permission grant (spec §7 <c>identity.RolePermissions</c>; R11). Grants are data,
/// changed only by reviewed migration (D8) — never by the application.
/// </summary>
public sealed class RolePermission
{
    private RolePermission()
    {
    }

    public Guid RoleId { get; private set; }

    /// <summary>A <c>docs/20</c> §2 permission name, e.g. <c>stations.read</c>.</summary>
    public string Permission { get; private set; } = null!;
}

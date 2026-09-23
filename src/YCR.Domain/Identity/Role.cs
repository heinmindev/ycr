using YCR.Domain.Common;

namespace YCR.Domain.Identity;

/// <summary>
/// A role in the catalogue (spec §7 <c>identity.Roles</c>). Seeded by migration and never
/// written by the application (D8): there is no factory and no mutator.
/// </summary>
public sealed class Role : Entity
{
    private readonly List<RolePermission> _permissions = [];

    private Role()
    {
    }

    /// <summary>One of <see cref="RoleNames.All"/>.</summary>
    public string Name { get; private set; } = null!;

    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();
}

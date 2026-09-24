using Microsoft.EntityFrameworkCore;

namespace YCR.Application.Identity.ListRoles;

/// <summary><c>GET /roles</c> (spec §6.2; <c>users.read</c>).</summary>
public sealed record ListRolesQuery;

/// <summary>A role and its grants, read from the seeded data (D8).</summary>
public sealed record RoleDto(string Name, IReadOnlyList<string> Permissions);

/// <summary>The role catalogue in canonical order. Read-only: no endpoint edits grants (D8).</summary>
public sealed class ListRolesHandler(IIdentityDbContext db)
{
    public async Task<IReadOnlyList<RoleDto>> Handle(ListRolesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var roles = await db.Roles.AsNoTracking()
            .Select(role => new { role.Name, Permissions = role.Permissions.Select(grant => grant.Permission).ToList() })
            .ToListAsync(cancellationToken);

        return [.. roles
            .OrderBy(role => IdentityQueries.CanonicalOrder(role.Name))
            .Select(role => new RoleDto(role.Name, [.. role.Permissions.Order(StringComparer.Ordinal)]))];
    }
}

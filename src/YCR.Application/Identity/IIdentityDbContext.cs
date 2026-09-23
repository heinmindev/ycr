using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using YCR.Domain.Identity;

namespace YCR.Application.Identity;

/// <summary>
/// The Identity module's view of the shared context (ADR-0012 item 2; <c>docs/07</c> schema
/// <c>identity</c>).
/// </summary>
/// <remarks>
/// <see cref="Roles"/> is read-only by grant, not by type: <c>ycr_app</c> has only
/// <c>SELECT</c> on <c>identity.Roles</c> and <c>identity.RolePermissions</c> (D8, plan P7), so a
/// write through it fails in the database. <see cref="Database"/> is exposed for the one explicit
/// transaction ADR-0004 allows here: the last-administrator lock (plan P14).
/// </remarks>
public interface IIdentityDbContext
{
    DbSet<StaffUser> Users { get; }

    DbSet<Role> Roles { get; }

    DbSet<AuthSession> AuthSessions { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

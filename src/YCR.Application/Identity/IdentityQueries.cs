using Microsoft.EntityFrameworkCore;
using YCR.Domain.Identity;

namespace YCR.Application.Identity;

/// <summary>A set of roles and the union of their permissions (R10, R11).</summary>
/// <param name="Roles">Canonical identifiers in <see cref="RoleNames.All"/> order.</param>
/// <param name="Permissions">Distinct permission names, ordinal order.</param>
public sealed record RoleGrants(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)
{
    public static readonly RoleGrants None = new([], []);
}

/// <summary>Read helpers shared by the Identity handlers.</summary>
internal static class IdentityQueries
{
    /// <summary>
    /// The names of <paramref name="roleIds"/> and the union of their grants, read from
    /// <c>identity.RolePermissions</c> — the data, never code (D8).
    /// </summary>
    public static async Task<RoleGrants> RoleGrantsAsync(
        IIdentityDbContext db,
        IEnumerable<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        var ids = roleIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return RoleGrants.None;
        }

        var roles = await db.Roles
            .AsNoTracking()
            .Where(role => ids.Contains(role.Id))
            .Select(role => new { role.Name, Permissions = role.Permissions.Select(grant => grant.Permission).ToList() })
            .ToListAsync(cancellationToken);

        return new RoleGrants(
            [.. roles.Select(role => role.Name).OrderBy(CanonicalOrder)],
            [.. roles.SelectMany(role => role.Permissions).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]);
    }

    /// <summary>Role names sort by the catalogue's order, so <c>ActorRole</c> is stable.</summary>
    public static int CanonicalOrder(string roleName)
    {
        var index = RoleNames.All.ToList().IndexOf(roleName);
        return index < 0 ? int.MaxValue : index;
    }

    public static Task<StaffUser?> FindUserAsync(IIdentityDbContext db, Guid userId, CancellationToken cancellationToken) =>
        db.Users.Include(user => user.Roles).SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
}

/// <summary>
/// Optimistic-concurrency retry for the Identity handlers (plan P15): a handler that loses a
/// <c>rowversion</c> or concurrency-token race discards everything it tracked — including its
/// audit rows — and runs its decision again against fresh rows.
/// </summary>
internal static class IdentityRetry
{
    /// <summary>
    /// Each round of a race has one winner, so N simultaneous requests on one row need up to N
    /// attempts. Ten covers a burst larger than the per-username sign-in limit (U6: 5 a minute).
    /// </summary>
    public const int MaxAttempts = 10;

    public static async Task<T> RunAsync<T>(
        IIdentityDbContext db,
        Func<int, Task<T>> attempt,
        CancellationToken cancellationToken)
    {
        for (var number = 1; ; number++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await attempt(number);
            }
            catch (DbUpdateConcurrencyException) when (number < MaxAttempts)
            {
                db.ChangeTracker.Clear();

                // Jitter, so the losers of one round do not collide again in lock-step.
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(1, 10 * number)), cancellationToken);
            }
        }
    }
}

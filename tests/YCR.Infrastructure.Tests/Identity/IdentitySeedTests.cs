using System.Reflection;
using System.Text.RegularExpressions;
using YCR.Application.Common.Authorization;
using YCR.Domain.Identity;
using YCR.TestSupport;

namespace YCR.Infrastructure.Tests.Identity;

/// <summary>
/// S30: exactly eight roles and thirty-four grants (fourteen from F-002, ten route grants from
/// F-003, OQ40, ten service grants from F-004, OQ49), identical to <c>docs/10</c>, every permission a
/// <see cref="Permissions"/> constant, and no user.
/// </summary>
/// <remarks>
/// The grants are a provisional tech-lead ruling — not a Myanma Railways answer (R11). The
/// docs/10 comparison exists so the migration and the document cannot drift apart: a change to
/// either fails here until the other matches (plan R-6).
/// </remarks>
[Collection(SqlServerCollection.Name)]
public sealed class IdentitySeedTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private TestDatabase database = null!;

    public async ValueTask InitializeAsync() =>
        database = await fixture.Container.ProvisionDatabaseAsync("identity_seed", TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Seed_ProducesExactlyEightRolesAndThirtyFourGrants()
    {
        var roles = await IdentitySql.StringsAsync(database.MigratorConnectionString, "SELECT [Name] FROM [identity].[Roles]");
        Assert.Equal(RoleNames.All.Order(StringComparer.Ordinal), roles.Order(StringComparer.Ordinal));

        var grants = await SeededGrantsAsync();
        Assert.Equal(34, grants.Count);
        string[] expected =
        [
            "RailwayAdministrator:stations.manage",
            "SystemAdministrator:stations.manage",
            .. RoleNames.All.Select(role => $"{role}:stations.read"),
            "SystemAdministrator:users.read",
            "SystemAdministrator:users.manage",
            "SystemAdministrator:users.roles.manage",
            "SystemAdministrator:auth-sessions.revoke",
            "RailwayAdministrator:routes.manage",
            "SystemAdministrator:routes.manage",
            .. RoleNames.All.Select(role => $"{role}:routes.read"),
            "RailwayAdministrator:services.manage",
            "SystemAdministrator:services.manage",
            .. RoleNames.All.Select(role => $"{role}:services.read"),
        ];
        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            grants.Select(grant => $"{grant.Role}:{grant.Permission}").Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Seed_MatchesDocs10GrantTables()
    {
        var documented = ReadDocs10Grants();

        var seeded = await SeededGrantsAsync();

        Assert.Equal(
            documented.Order().ToArray(),
            seeded.Select(grant => $"{grant.Permission} -> {grant.Role}").Order().ToArray());
    }

    [Fact]
    public async Task Seed_EveryPermissionIsAPermissionsConstant()
    {
        var constants = typeof(Permissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

        var seeded = await SeededGrantsAsync();

        Assert.All(seeded, grant => Assert.Contains(grant.Permission, constants));
    }

    [Fact]
    public async Task Seed_CreatesNoUser()
    {
        Assert.Equal(0, await IdentitySql.ScalarAsync<int>(database.MigratorConnectionString, "SELECT COUNT(*) FROM [identity].[Users];"));
        Assert.Equal(0, await IdentitySql.ScalarAsync<int>(database.MigratorConnectionString, "SELECT COUNT(*) FROM [identity].[UserRoles];"));
    }

    private async Task<List<(string Role, string Permission)>> SeededGrantsAsync() =>
        await IdentitySql.PairsAsync(
            database.MigratorConnectionString,
            """
            SELECT r.[Name], p.[Permission]
            FROM [identity].[RolePermissions] AS p
            JOIN [identity].[Roles] AS r ON r.[Id] = p.[RoleId];
            """);

    /// <summary>
    /// Reads the four grant sections of <c>docs/10</c>: the station, route and service bullets
    /// (<c>- `perm` → … roles …</c>, up to the first parenthesis, which lists who does
    /// <em>not</em> hold it) and the identity table's "Held by" column.
    /// </summary>
    private static List<string> ReadDocs10Grants()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "10-authorization-matrix.md"));
        var roles = RoleNames.All.ToHashSet(StringComparer.Ordinal);
        var grants = new List<string>();

        // The station, route and service sections share one bullet format (F-003 plan R-4), so one parser
        // reads all three. Each section must yield grants, so a reformatted section fails by name.
        foreach (var heading in new[] { "## Station permission grants", "## Route permission grants", "## Service permission grants" })
        {
            var section = Section(text, heading);
            var before = grants.Count;
            foreach (Match bullet in Regex.Matches(section, @"^- `(?<permission>[a-z.-]+)` → (?<rest>.*)$", RegexOptions.Multiline))
            {
                var rest = bullet.Groups["rest"].Value;
                var holders = rest.Split('(')[0];
                grants.AddRange(RolesIn(holders, roles).Select(role => $"{bullet.Groups["permission"].Value} -> {role}"));
            }

            Assert.True(grants.Count > before, $"docs/10 '{heading}' yielded no grants.");
        }

        var identity = Section(text, "## Identity permission grants");
        foreach (Match row in Regex.Matches(identity, @"^\| `(?<permission>[a-z.-]+)` \|.*\| (?<held>[^|]*) \|\s*$", RegexOptions.Multiline))
        {
            grants.AddRange(RolesIn(row.Groups["held"].Value, roles).Select(role => $"{row.Groups["permission"].Value} -> {role}"));
        }

        Assert.NotEmpty(grants);
        return grants;
    }

    private static IEnumerable<string> RolesIn(string text, HashSet<string> roles) =>
        Regex.Matches(text, "`([A-Za-z]+)`").Select(match => match.Groups[1].Value).Where(roles.Contains).Distinct();

    private static string Section(string text, string heading)
    {
        var start = text.IndexOf(heading, StringComparison.Ordinal);
        Assert.True(start >= 0, $"docs/10 has no '{heading}' section.");
        var end = text.IndexOf("\n## ", start + heading.Length, StringComparison.Ordinal);
        return end < 0 ? text[start..] : text[start..end];
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "YCR.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No YCR.sln above the test directory.");
    }
}

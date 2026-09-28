using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using YCR.Infrastructure.Tests.Identity;
using YCR.TestSupport;

namespace YCR.Infrastructure.Tests.Persistence;

/// <summary>
/// F-003 spec §7 and plan §DB changes: the route tables in a real database, built by the
/// migration bundle.
/// </summary>
/// <remarks>
/// Credential: <c>ycr_migrator</c>. These tests are about what the <em>schema</em> refuses from
/// any writer, so they write directly with the credential that owns it; what the application
/// credential may do is <c>DatabasePrivilegeTests</c>' concern.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public sealed class RouteMigrationTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private TestDatabase database = null!;

    public async ValueTask InitializeAsync() =>
        database = await fixture.Container.ProvisionDatabaseAsync("route_schema", TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>ADR-0018 and spec §7: UTC checks on both instants, and <c>Position &gt;= 1</c>.</summary>
    [Fact]
    public async Task RouteConstraints_RejectNonUtcOffsetsAndNonPositivePositions()
    {
        var stationId = await InsertStationAsync("CKA");

        var nonUtcCreated = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(
            InsertRouteSql(Guid.NewGuid(), "CK1", "'2026-09-24T06:30:00+06:30'", "NULL")));
        Assert.Contains("CK_Routes_CreatedAtUtc_Utc", nonUtcCreated.Message, StringComparison.Ordinal);

        var nonUtcDeactivated = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(
            InsertRouteSql(Guid.NewGuid(), "CK2", "'2026-09-24T00:00:00+00:00'", "'2026-09-25T06:30:00+06:30'")));
        Assert.Contains("CK_Routes_DeactivatedAtUtc_Utc", nonUtcDeactivated.Message, StringComparison.Ordinal);

        // Both instants in UTC, and a null DeactivatedAtUtc, are accepted.
        var routeId = Guid.NewGuid();
        await ExecuteAsync(InsertRouteSql(routeId, "CK3", "'2026-09-24T00:00:00+00:00'", "NULL"));
        await ExecuteAsync(InsertRouteSql(Guid.NewGuid(), "CK4", "'2026-09-24T00:00:00+00:00'", "'2026-09-25T00:00:00+00:00'"));

        foreach (var position in new[] { 0, -1 })
        {
            var failure = await Assert.ThrowsAsync<SqlException>(() =>
                ExecuteAsync(InsertRouteStationSql(routeId, position, stationId)));
            Assert.Contains("CK_RouteStations_Position", failure.Message, StringComparison.Ordinal);
        }

        await ExecuteAsync(InsertRouteStationSql(routeId, 1, stationId));

        Assert.Equal(2, await ScalarAsync("SELECT COUNT(*) FROM [network].[Routes];"));
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM [network].[RouteStations];"));
    }

    /// <summary>R7: the database refuses a station twice in one route, whatever the positions.</summary>
    [Fact]
    public async Task RouteStations_SameStationTwiceInOneRoute_RejectedByUniqueIndex()
    {
        var stationId = await InsertStationAsync("UQA");
        var otherRouteId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        await ExecuteAsync(InsertRouteSql(routeId, "UQ1", "'2026-09-24T00:00:00+00:00'", "NULL"));
        await ExecuteAsync(InsertRouteSql(otherRouteId, "UQ2", "'2026-09-24T00:00:00+00:00'", "NULL"));
        await ExecuteAsync(InsertRouteStationSql(routeId, 1, stationId));

        var failure = await Assert.ThrowsAsync<SqlException>(() =>
            ExecuteAsync(InsertRouteStationSql(routeId, 2, stationId)));

        Assert.Equal(2601, failure.Number);
        Assert.Contains("UX_RouteStations_StationId_RouteId", failure.Message, StringComparison.Ordinal);

        // The same station in a different route is fine (R5, S29).
        await ExecuteAsync(InsertRouteStationSql(otherRouteId, 1, stationId));
        Assert.Equal(2, await ScalarAsync("SELECT COUNT(*) FROM [network].[RouteStations];"));
    }

    /// <summary>R16: the foreign key is the database authority that a station exists.</summary>
    [Fact]
    public async Task RouteStations_UnknownStation_RejectedByForeignKey()
    {
        var routeId = Guid.NewGuid();
        await ExecuteAsync(InsertRouteSql(routeId, "FK1", "'2026-09-24T00:00:00+00:00'", "NULL"));

        var unknownStation = await Assert.ThrowsAsync<SqlException>(() =>
            ExecuteAsync(InsertRouteStationSql(routeId, 1, Guid.NewGuid())));
        Assert.Equal(547, unknownStation.Number);
        Assert.Contains("FK_RouteStations_Stations_StationId", unknownStation.Message, StringComparison.Ordinal);

        var stationId = await InsertStationAsync("FKA");
        var unknownRoute = await Assert.ThrowsAsync<SqlException>(() =>
            ExecuteAsync(InsertRouteStationSql(Guid.NewGuid(), 1, stationId)));
        Assert.Equal(547, unknownRoute.Number);
        Assert.Contains("FK_RouteStations_Routes_RouteId", unknownRoute.Message, StringComparison.Ordinal);

        Assert.Equal(0, await ScalarAsync("SELECT COUNT(*) FROM [network].[RouteStations];"));
    }

    /// <summary>
    /// <c>docs/21</c> §Data: the three F-003 migrations applied on top of a database exactly as
    /// F-002 left it, with station rows in it.
    /// </summary>
    [Fact]
    public async Task Migrate_FromF002Schema_CreatesRouteTablesConstraintsIndexesAndGrants()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var upgraded = await fixture.Container.CreateEmptyDatabaseAsync("route_upgrade", cancellationToken);

        await using (var context = IdentitySql.Context(upgraded.MigratorConnectionString))
        {
            await context.Database.MigrateAsync(LastF002Migration, cancellationToken);
        }

        await IdentitySql.ExecuteAsync(
            upgraded.MigratorConnectionString,
            """
            INSERT INTO [network].[Stations] ([Id], [Code], [NameEn], [NameMy], [IsActive], [CreatedAtUtc])
            VALUES (NEWID(), N'YGN', N'Yangon', N'ရန်ကုန်', 1, SYSUTCDATETIME() AT TIME ZONE 'UTC');
            """);

        await using (var context = IdentitySql.Context(upgraded.MigratorConnectionString))
        {
            await context.Database.MigrateAsync(cancellationToken);
            Assert.Empty(await context.Database.GetPendingMigrationsAsync(cancellationToken));
        }

        // F-001's data survives the upgrade untouched, and no route data ships (R23).
        Assert.Equal(1, await ScalarOnAsync(upgraded, "SELECT COUNT(*) FROM [network].[Stations] WHERE [Code] = N'YGN';"));
        Assert.Equal(0, await ScalarOnAsync(upgraded, "SELECT COUNT(*) FROM [network].[Routes];"));

        Assert.Equal(
            ["RouteStations", "Routes", "Stations"],
            (await StringsOnAsync(upgraded, "SELECT name FROM sys.tables WHERE schema_id = SCHEMA_ID(N'network')"))
                .Order(StringComparer.Ordinal));

        Assert.Equal(
            ["CK_RouteStations_Position", "CK_Routes_CreatedAtUtc_Utc", "CK_Routes_DeactivatedAtUtc_Utc"],
            (await StringsOnAsync(
                upgraded,
                "SELECT name FROM sys.check_constraints WHERE parent_object_id IN (OBJECT_ID(N'network.Routes'), OBJECT_ID(N'network.RouteStations'))"))
                .Order(StringComparer.Ordinal));

        // Exactly these indexes: in particular no IX_RouteStations_StationId (spec Amendment 1).
        Assert.Equal(
            ["PK_RouteStations", "PK_Routes", "UX_RouteStations_StationId_RouteId", "UX_Routes_Code"],
            (await StringsOnAsync(
                upgraded,
                "SELECT name FROM sys.indexes WHERE name IS NOT NULL AND object_id IN (OBJECT_ID(N'network.Routes'), OBJECT_ID(N'network.RouteStations'))"))
                .Order(StringComparer.Ordinal));
        Assert.Equal(
            ["1:StationId", "2:RouteId"],
            (await StringsOnAsync(
                upgraded,
                """
                SELECT CONCAT(ic.key_ordinal, N':', c.name COLLATE DATABASE_DEFAULT) FROM sys.index_columns AS ic
                JOIN sys.indexes AS i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                WHERE i.name = N'UX_RouteStations_StationId_RouteId'
                """)).Order(StringComparer.Ordinal));

        // Both foreign keys NO ACTION on delete and update.
        Assert.Equal(
            ["FK_RouteStations_Routes_RouteId:NO_ACTION:NO_ACTION", "FK_RouteStations_Stations_StationId:NO_ACTION:NO_ACTION"],
            (await StringsOnAsync(
                upgraded,
                "SELECT CONCAT(name COLLATE DATABASE_DEFAULT, N':', delete_referential_action_desc COLLATE DATABASE_DEFAULT, N':', update_referential_action_desc COLLATE DATABASE_DEFAULT) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'network.RouteStations')"))
                .Order(StringComparer.Ordinal));

        // R10: the role's grants on the two tables, exactly.
        Assert.Equal(
            [
                "INSERT:RouteStations:",
                "INSERT:Routes:",
                "SELECT:RouteStations:",
                "SELECT:Routes:",
                "UPDATE:Routes:DeactivatedAtUtc",
                "UPDATE:Routes:IsActive",
            ],
            (await RouteGrantsAsync(upgraded)).Order(StringComparer.Ordinal));

        // OQ40: the ten route grants, on top of F-002's fourteen; the total includes F-004's ten
        // service grants (OQ49), because the database is migrated to the latest migration.
        Assert.Equal(10, await ScalarOnAsync(upgraded, "SELECT COUNT(*) FROM [identity].[RolePermissions] WHERE [Permission] LIKE N'routes.%';"));
        Assert.Equal(44, await ScalarOnAsync(upgraded, "SELECT COUNT(*) FROM [identity].[RolePermissions];"));
    }

    /// <summary>
    /// Rollback (plan §DB changes): down to F-002's last migration removes the route tables, their
    /// grants and the ten seeded grants, and keeps the <c>network</c> schema and its stations.
    /// </summary>
    [Fact]
    public async Task Migrate_DownToF002_RemovesRouteObjectsGrantsAndSeedRowsAndKeepsStations()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var stationId = await InsertStationAsync("DNA");
        var routeId = Guid.NewGuid();
        await ExecuteAsync(InsertRouteSql(routeId, "DN1", "'2026-09-24T00:00:00+00:00'", "NULL"));
        await ExecuteAsync(InsertRouteStationSql(routeId, 1, stationId));

        await using (var context = IdentitySql.Context(database.MigratorConnectionString))
        {
            await context.Database.MigrateAsync(LastF002Migration, cancellationToken);
            Assert.Equal(LastF002Migration, (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).Last());
        }

        Assert.Equal(0, await ScalarAsync(
            "SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID(N'network') AND name IN (N'Routes', N'RouteStations');"));
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM sys.schemas WHERE name = N'network';"));
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) FROM [network].[Stations] WHERE [Id] = '{stationId}';"));

        Assert.Empty(await RouteGrantsAsync(database));
        Assert.Equal(0, await ScalarAsync("SELECT COUNT(*) FROM [identity].[RolePermissions] WHERE [Permission] LIKE N'routes.%';"));
        Assert.Equal(14, await ScalarAsync("SELECT COUNT(*) FROM [identity].[RolePermissions];"));

        // F-001's station grants are not touched by the route grant revocation.
        Assert.Equal(1, await ScalarAsync(
            """
            SELECT COUNT(*) FROM sys.database_permissions
            WHERE grantee_principal_id = DATABASE_PRINCIPAL_ID(N'ycr_app') AND state = 'G'
              AND major_id = OBJECT_ID(N'network.Stations') AND permission_name = N'UPDATE'
              AND minor_id = COLUMNPROPERTY(OBJECT_ID(N'network.Stations'), N'IsActive', 'ColumnId');
            """));

        // And forward again, so the rollback is repeatable.
        await using (var context = IdentitySql.Context(database.MigratorConnectionString))
        {
            await context.Database.MigrateAsync(cancellationToken);
        }

        Assert.Equal(6, (await RouteGrantsAsync(database)).Count);
        // Forward to the latest migration, so F-004's ten service grants (OQ49) are back too.
        Assert.Equal(44, await ScalarAsync("SELECT COUNT(*) FROM [identity].[RolePermissions];"));
    }

    private const string LastF002Migration = "20260923133743_Security_IdentityGrants";

    /// <summary>The <c>ycr_app</c> role's grants on the two route tables, as <c>PERMISSION:Table:Column</c>.</summary>
    private static Task<List<string>> RouteGrantsAsync(TestDatabase target) =>
        IdentitySql.StringsAsync(
            target.MigratorConnectionString,
            """
            SELECT CONCAT(p.permission_name COLLATE DATABASE_DEFAULT, N':', OBJECT_NAME(p.major_id) COLLATE DATABASE_DEFAULT, N':', c.name COLLATE DATABASE_DEFAULT)
            FROM sys.database_permissions AS p
            LEFT JOIN sys.columns AS c ON c.object_id = p.major_id AND c.column_id = p.minor_id
            WHERE p.grantee_principal_id = DATABASE_PRINCIPAL_ID(N'ycr_app')
              AND p.class = 1 AND p.state = 'G'
              AND OBJECT_SCHEMA_NAME(p.major_id) = N'network'
              AND OBJECT_NAME(p.major_id) IN (N'Routes', N'RouteStations')
            """);

    private static Task<int> ScalarOnAsync(TestDatabase target, string sql) =>
        IdentitySql.ScalarAsync<int>(target.MigratorConnectionString, sql);

    private static Task<List<string>> StringsOnAsync(TestDatabase target, string sql) =>
        IdentitySql.StringsAsync(target.MigratorConnectionString, sql);

    private async Task<Guid> InsertStationAsync(string code)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            $"""
            INSERT INTO [network].[Stations] ([Id], [Code], [NameEn], [NameMy], [IsActive], [CreatedAtUtc])
            VALUES ('{id}', N'{code}', N'Station {code}', N'ဘူတာ', 1, SYSUTCDATETIME() AT TIME ZONE 'UTC');
            """);
        return id;
    }

    private static string InsertRouteSql(Guid id, string code, string createdAtUtc, string deactivatedAtUtc) =>
        $"""
        INSERT INTO [network].[Routes] ([Id], [Code], [NameEn], [NameMy], [IsClosed], [IsActive], [CreatedAtUtc], [DeactivatedAtUtc])
        VALUES ('{id}', N'{code}', N'Route {code}', N'လမ်းကြောင်း', 0, 1, {createdAtUtc}, {deactivatedAtUtc});
        """;

    private static string InsertRouteStationSql(Guid routeId, int position, Guid stationId) =>
        $"INSERT INTO [network].[RouteStations] ([RouteId], [Position], [StationId]) VALUES ('{routeId}', {position}, '{stationId}');";

    private Task ExecuteAsync(string sql) => IdentitySql.ExecuteAsync(database.MigratorConnectionString, sql);

    private Task<int> ScalarAsync(string sql) => IdentitySql.ScalarAsync<int>(database.MigratorConnectionString, sql);
}

using Microsoft.EntityFrameworkCore;
using YCR.Infrastructure.Tests.Identity;
using YCR.TestSupport;

namespace YCR.Infrastructure.Tests.Persistence;

/// <summary>
/// F-005 spec §7 and plan §DB changes: the three F-005 migrations applied on top of a database
/// exactly as F-004 left it, and rolled back.
/// </summary>
/// <remarks>
/// Credential: <c>ycr_migrator</c>. What the application credential may do is
/// <c>DatabasePrivilegeTests</c>' concern. The SQL helpers here are shared with it.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public sealed class ScheduleMigrationTests(SqlServerFixture fixture)
{
    internal const string UtcNow = "'2026-10-01T03:00:00+00:00'";

    private const string LastF004Migration = "20260925072603_Security_TimetableGrants";

    private static readonly Guid UpgradeStationId = Guid.Parse("0199b3a0-0000-7000-8000-00000000f501");
    private static readonly Guid UpgradeRouteId = Guid.Parse("0199b3a0-0000-7000-8000-00000000f502");
    private static readonly Guid UpgradeServiceId = Guid.Parse("0199b3a0-0000-7000-8000-00000000f503");

    /// <summary>
    /// <c>docs/21</c> §Data: the upgrade from F-004's last migration, with a station, a route, a
    /// service and its stops in it. Service rows are intact; no version ships (R37); the objects,
    /// grants and seed are exactly the plan's.
    /// </summary>
    [Fact]
    public async Task Migrate_FromF004Schema_CreatesScheduleObjectsGrantsAndSeed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var upgraded = await fixture.Container.CreateEmptyDatabaseAsync("schedule_upgrade", cancellationToken);

        await using (var context = IdentitySql.Context(upgraded.MigratorConnectionString))
        {
            await context.Database.MigrateAsync(LastF004Migration, cancellationToken);
        }

        await IdentitySql.ExecuteAsync(
            upgraded.MigratorConnectionString,
            $"""
            INSERT INTO [network].[Stations] ([Id], [Code], [NameEn], [NameMy], [IsActive], [CreatedAtUtc])
            VALUES ('{UpgradeStationId}', N'YGN', N'Yangon', N'ရန်ကုန်', 1, {UtcNow});
            INSERT INTO [network].[Routes] ([Id], [Code], [NameEn], [NameMy], [IsClosed], [IsActive], [CreatedAtUtc], [DeactivatedAtUtc])
            VALUES ('{UpgradeRouteId}', N'LOOP1', N'Circular', N'မြို့ပတ်', 1, 1, {UtcNow}, NULL);
            INSERT INTO [network].[RouteStations] ([RouteId], [Position], [StationId]) VALUES ('{UpgradeRouteId}', 1, '{UpgradeStationId}');
            {TimetableMigrationTests.InsertServiceSql(UpgradeServiceId, UpgradeRouteId)}
            {TimetableMigrationTests.InsertStopSql(UpgradeServiceId, 1, UpgradeStationId)}
            {TimetableMigrationTests.InsertStopSql(UpgradeServiceId, 2, UpgradeStationId)}
            """);

        await using (var context = IdentitySql.Context(upgraded.MigratorConnectionString))
        {
            await context.Database.MigrateAsync(cancellationToken);
            Assert.Empty(await context.Database.GetPendingMigrationsAsync(cancellationToken));
        }

        // F-004's rows survive untouched, and no timetable version ships (R37).
        Assert.Equal(1, await ScalarOnAsync(upgraded,
            $"SELECT COUNT(*) FROM [timetable].[Services] WHERE [Id] = '{UpgradeServiceId}' AND [Code] = N'S101' AND [EffectiveFrom] = '2026-10-05' AND [EffectiveTo] IS NULL;"));
        Assert.Equal(2, await ScalarOnAsync(upgraded, $"SELECT COUNT(*) FROM [timetable].[ServiceStops] WHERE [ServiceId] = '{UpgradeServiceId}';"));
        Assert.Equal(0, await ScalarOnAsync(upgraded, "SELECT COUNT(*) FROM [timetable].[ScheduleVersions];"));
        Assert.Equal(0, await ScalarOnAsync(upgraded, "SELECT COUNT(*) FROM [timetable].[ScheduleVersionServices];"));
        Assert.Equal(0, await ScalarOnAsync(upgraded, "SELECT COUNT(*) FROM [timetable].[ScheduleStopTimes];"));

        Assert.Equal(
            ["ScheduleStopTimes", "ScheduleVersionServices", "ScheduleVersions", "ServiceStops", "Services"],
            (await StringsOnAsync(upgraded, "SELECT name FROM sys.tables WHERE schema_id = SCHEMA_ID(N'timetable')"))
                .Order(StringComparer.Ordinal));

        Assert.Equal(
            [
                "CK_ScheduleStopTimes_AnyTime",
                "CK_ScheduleStopTimes_Dwell",
                "CK_ScheduleStopTimes_Minutes",
                "CK_ScheduleVersions_CancelledAtUtc_Utc",
                "CK_ScheduleVersions_CreatedAtUtc_Utc",
                "CK_ScheduleVersions_DiscardedAtUtc_Utc",
                "CK_ScheduleVersions_Number",
                "CK_ScheduleVersions_PublishedAtUtc_Utc",
                "CK_ScheduleVersions_Status",
                "CK_ScheduleVersions_StatusInstants",
            ],
            (await StringsOnAsync(upgraded, $"SELECT name FROM sys.check_constraints WHERE parent_object_id IN ({ScheduleTables})"))
                .Order(StringComparer.Ordinal));

        // Exactly these indexes, with the filter on R22's authority (plan O1, P21).
        Assert.Equal(
            [
                "IX_ScheduleStopTimes_ServiceId_Position:0:ServiceId,Position:",
                "IX_ScheduleVersionServices_ServiceId:0:ServiceId:",
                "PK_ScheduleStopTimes:1:ScheduleVersionId,ServiceId,Position:",
                "PK_ScheduleVersionServices:1:ScheduleVersionId,ServiceId:",
                "PK_ScheduleVersions:1:Id:",
                "UX_ScheduleVersions_EffectiveFrom_Published:1:EffectiveFrom:([Status]=N'Published')",
                "UX_ScheduleVersions_Number:1:Number:",
            ],
            (await StringsOnAsync(
                upgraded,
                $"""
                SELECT CONCAT(i.name COLLATE DATABASE_DEFAULT, N':', CAST(i.is_unique AS int), N':',
                       STRING_AGG(c.name COLLATE DATABASE_DEFAULT, N',') WITHIN GROUP (ORDER BY ic.key_ordinal), N':',
                       MAX(i.filter_definition) COLLATE DATABASE_DEFAULT)
                FROM sys.indexes AS i
                JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal > 0
                JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                WHERE i.object_id IN ({ScheduleTables})
                GROUP BY i.name, i.is_unique
                """)).Order(StringComparer.Ordinal));

        // Four foreign keys, NO ACTION on delete and update, all inside timetable.
        Assert.Equal(
            [
                "FK_ScheduleStopTimes_ScheduleVersionServices_ScheduleVersionId_ServiceId:timetable.ScheduleVersionServices:NO_ACTION:NO_ACTION",
                "FK_ScheduleStopTimes_ServiceStops_ServiceId_Position:timetable.ServiceStops:NO_ACTION:NO_ACTION",
                "FK_ScheduleVersionServices_ScheduleVersions_ScheduleVersionId:timetable.ScheduleVersions:NO_ACTION:NO_ACTION",
                "FK_ScheduleVersionServices_Services_ServiceId:timetable.Services:NO_ACTION:NO_ACTION",
            ],
            (await StringsOnAsync(
                upgraded,
                $"""
                SELECT CONCAT(name COLLATE DATABASE_DEFAULT, N':',
                       OBJECT_SCHEMA_NAME(referenced_object_id) COLLATE DATABASE_DEFAULT, N'.', OBJECT_NAME(referenced_object_id) COLLATE DATABASE_DEFAULT, N':',
                       delete_referential_action_desc COLLATE DATABASE_DEFAULT, N':', update_referential_action_desc COLLATE DATABASE_DEFAULT)
                FROM sys.foreign_keys
                WHERE parent_object_id IN ({ScheduleTables})
                """)).Order(StringComparer.Ordinal));

        // SV51: the role's grants on the three tables, exactly; F-004's are unchanged.
        Assert.Equal(ExpectedScheduleGrants, (await ScheduleGrantsAsync(upgraded)).Order(StringComparer.Ordinal));

        // OQ59: the ten schedule grants, on top of F-002's fourteen and F-003's and F-004's ten each.
        Assert.Equal(
            [
                "Auditor:schedules.read",
                "FinanceOfficer:schedules.read",
                "RailwayAdministrator:schedules.manage",
                "RailwayAdministrator:schedules.read",
                "ReportingUser:schedules.read",
                "StationManager:schedules.read",
                "SystemAdministrator:schedules.manage",
                "SystemAdministrator:schedules.read",
                "TicketInspector:schedules.read",
                "TicketOperator:schedules.read",
            ],
            (await StringsOnAsync(
                upgraded,
                """
                SELECT CONCAT(r.[Name], N':', p.[Permission]) FROM [identity].[RolePermissions] AS p
                JOIN [identity].[Roles] AS r ON r.[Id] = p.[RoleId] WHERE p.[Permission] LIKE N'schedules.%'
                """)).Order(StringComparer.Ordinal));
        Assert.Equal(44, await ScalarOnAsync(upgraded, "SELECT COUNT(*) FROM [identity].[RolePermissions];"));
    }

    /// <summary>
    /// Rollback (plan §DB changes): down to F-004's last migration removes the three tables, their
    /// grants and the ten seeded grants, and keeps the service rows; then forward again.
    /// </summary>
    [Fact]
    public async Task Migrate_DownToF004_RemovesScheduleObjectsGrantsAndSeedRowsAndKeepsServices()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var database = await fixture.Container.ProvisionDatabaseAsync("schedule_down", cancellationToken);
        var (routeId, stationId) = await InsertRouteAsync(database, "SD");
        var serviceId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        await IdentitySql.ExecuteAsync(
            database.MigratorConnectionString,
            TimetableMigrationTests.InsertServiceSql(serviceId, routeId) +
            TimetableMigrationTests.InsertStopSql(serviceId, 1, stationId) +
            TimetableMigrationTests.InsertStopSql(serviceId, 2, stationId));

        // A rollback destroys timetable history (plan §Rollback): only an empty table set goes down
        // cleanly in production; here a draft is present to show the tables really are dropped.
        await IdentitySql.ExecuteAsync(
            database.MigratorConnectionString,
            InsertVersionSql(versionId) + InsertEntrySql(versionId, serviceId) +
            InsertStopTimeSql(versionId, serviceId, 1, "NULL", "360") + InsertStopTimeSql(versionId, serviceId, 2, "400", "NULL"));

        await using (var context = IdentitySql.Context(database.MigratorConnectionString))
        {
            await context.Database.MigrateAsync(LastF004Migration, cancellationToken);
            Assert.Equal(LastF004Migration, (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).Last());
        }

        Assert.Equal(1, await ScalarOnAsync(database, "SELECT COUNT(*) FROM sys.schemas WHERE name = N'timetable';"));
        Assert.Equal(
            ["ServiceStops", "Services"],
            (await StringsOnAsync(database, "SELECT name FROM sys.tables WHERE schema_id = SCHEMA_ID(N'timetable')"))
                .Order(StringComparer.Ordinal));
        Assert.Equal(1, await ScalarOnAsync(database, $"SELECT COUNT(*) FROM [timetable].[Services] WHERE [Id] = '{serviceId}';"));
        Assert.Equal(2, await ScalarOnAsync(database, $"SELECT COUNT(*) FROM [timetable].[ServiceStops] WHERE [ServiceId] = '{serviceId}';"));

        Assert.Empty(await ScheduleGrantsAsync(database));
        Assert.Equal(0, await ScalarOnAsync(database, "SELECT COUNT(*) FROM [identity].[RolePermissions] WHERE [Permission] LIKE N'schedules.%';"));
        Assert.Equal(34, await ScalarOnAsync(database, "SELECT COUNT(*) FROM [identity].[RolePermissions];"));

        // F-004's grants are not touched by the schedule grant revocation.
        Assert.Equal(1, await ScalarOnAsync(database,
            """
            SELECT COUNT(*) FROM sys.database_permissions
            WHERE grantee_principal_id = DATABASE_PRINCIPAL_ID(N'ycr_app') AND state = 'G'
              AND major_id = OBJECT_ID(N'timetable.Services') AND permission_name = N'UPDATE'
              AND minor_id = COLUMNPROPERTY(OBJECT_ID(N'timetable.Services'), N'EffectiveTo', 'ColumnId');
            """));

        // And forward again, so the rollback is repeatable.
        await using (var context = IdentitySql.Context(database.MigratorConnectionString))
        {
            await context.Database.MigrateAsync(cancellationToken);
        }

        Assert.Equal(ExpectedScheduleGrants, (await ScheduleGrantsAsync(database)).Order(StringComparer.Ordinal));
        Assert.Equal(44, await ScalarOnAsync(database, "SELECT COUNT(*) FROM [identity].[RolePermissions];"));
        Assert.Equal(0, await ScalarOnAsync(database, "SELECT COUNT(*) FROM [timetable].[ScheduleVersions];"));
        Assert.Equal(1, await ScalarOnAsync(database, $"SELECT COUNT(*) FROM [timetable].[Services] WHERE [Id] = '{serviceId}';"));
    }

    // ----- Shared SQL (also used by DatabasePrivilegeTests) -----

    internal static string InsertVersionSql(
        Guid id,
        int number = 1,
        string effectiveFrom = "'2026-10-05'",
        string status = "N'Draft'",
        string createdAtUtc = UtcNow,
        string publishedAtUtc = "NULL",
        string discardedAtUtc = "NULL",
        string cancelledAtUtc = "NULL") =>
        $"""
        INSERT INTO [timetable].[ScheduleVersions]
            ([Id], [Number], [NameEn], [NameMy], [EffectiveFrom], [Status], [CreatedAtUtc], [PublishedAtUtc], [DiscardedAtUtc], [CancelledAtUtc])
        VALUES ('{id}', {number}, N'October', N'အောက်တိုဘာ', {effectiveFrom}, {status}, {createdAtUtc}, {publishedAtUtc}, {discardedAtUtc}, {cancelledAtUtc});
        """;

    internal static string InsertEntrySql(Guid versionId, Guid serviceId) =>
        $"INSERT INTO [timetable].[ScheduleVersionServices] ([ScheduleVersionId], [ServiceId]) VALUES ('{versionId}', '{serviceId}');";

    internal static string InsertStopTimeSql(Guid versionId, Guid serviceId, int position, string arrival, string departure) =>
        $"""
        INSERT INTO [timetable].[ScheduleStopTimes] ([ScheduleVersionId], [ServiceId], [Position], [ArrivalMinute], [DepartureMinute])
        VALUES ('{versionId}', '{serviceId}', {position}, {arrival}, {departure});
        """;

    internal static readonly string[] ExpectedScheduleGrants =
    [
        "INSERT:ScheduleStopTimes:",
        "INSERT:ScheduleVersionServices:",
        "INSERT:ScheduleVersions:",
        "SELECT:ScheduleStopTimes:",
        "SELECT:ScheduleVersionServices:",
        "SELECT:ScheduleVersions:",
        "UPDATE:ScheduleVersions:CancelledAtUtc",
        "UPDATE:ScheduleVersions:DiscardedAtUtc",
        "UPDATE:ScheduleVersions:PublishedAtUtc",
        "UPDATE:ScheduleVersions:Status",
    ];

    private const string ScheduleTables =
        "OBJECT_ID(N'timetable.ScheduleVersions'), OBJECT_ID(N'timetable.ScheduleVersionServices'), OBJECT_ID(N'timetable.ScheduleStopTimes')";

    /// <summary>The <c>ycr_app</c> role's grants on the three schedule tables, as <c>PERMISSION:Object:Column</c>.</summary>
    private static Task<List<string>> ScheduleGrantsAsync(TestDatabase target) =>
        IdentitySql.StringsAsync(
            target.MigratorConnectionString,
            """
            SELECT CONCAT(p.permission_name COLLATE DATABASE_DEFAULT, N':', OBJECT_NAME(p.major_id) COLLATE DATABASE_DEFAULT, N':', c.name COLLATE DATABASE_DEFAULT)
            FROM sys.database_permissions AS p
            LEFT JOIN sys.columns AS c ON c.object_id = p.major_id AND c.column_id = p.minor_id
            WHERE p.grantee_principal_id = DATABASE_PRINCIPAL_ID(N'ycr_app')
              AND p.class = 1 AND p.state IN ('G', 'W')
              AND OBJECT_SCHEMA_NAME(p.major_id) = N'timetable'
              AND OBJECT_NAME(p.major_id) LIKE N'Schedule%'
            """);

    private static async Task<(Guid RouteId, Guid StationId)> InsertRouteAsync(TestDatabase target, string prefix)
    {
        var stationId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        await IdentitySql.ExecuteAsync(
            target.MigratorConnectionString,
            $"""
            INSERT INTO [network].[Stations] ([Id], [Code], [NameEn], [NameMy], [IsActive], [CreatedAtUtc])
            VALUES ('{stationId}', N'{prefix}S', N'Station {prefix}', N'ဘူတာ', 1, {UtcNow});
            INSERT INTO [network].[Routes] ([Id], [Code], [NameEn], [NameMy], [IsClosed], [IsActive], [CreatedAtUtc], [DeactivatedAtUtc])
            VALUES ('{routeId}', N'{prefix}R', N'Route {prefix}', N'လမ်းကြောင်း', 0, 1, {UtcNow}, NULL);
            INSERT INTO [network].[RouteStations] ([RouteId], [Position], [StationId]) VALUES ('{routeId}', 1, '{stationId}');
            """);
        return (routeId, stationId);
    }

    private static Task<int> ScalarOnAsync(TestDatabase target, string sql) =>
        IdentitySql.ScalarAsync<int>(target.MigratorConnectionString, sql);

    private static Task<List<string>> StringsOnAsync(TestDatabase target, string sql) =>
        IdentitySql.StringsAsync(target.MigratorConnectionString, sql);
}

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using YCR.Infrastructure.Tests.Identity;
using YCR.TestSupport;

namespace YCR.Infrastructure.Tests.Persistence;

/// <summary>
/// F-004 spec §7 and plan §DB changes: the timetable tables in a real database, built by the
/// migration bundle.
/// </summary>
/// <remarks>
/// Credential: <c>ycr_migrator</c>. These tests are about what the <em>schema</em> refuses from
/// any writer, so they write directly with the credential that owns it; what the application
/// credential may do is <c>DatabasePrivilegeTests</c>' concern.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public sealed class TimetableMigrationTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string UtcNow = "'2026-10-01T03:00:00+00:00'";

    private TestDatabase database = null!;

    public async ValueTask InitializeAsync() =>
        database = await fixture.Container.ProvisionDatabaseAsync("timetable_schema", TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>§7, R19, R26, R41: every check constraint refuses its invalid row from any writer.</summary>
    [Theory]
    [InlineData("CK_Services_CreatedAtUtc_Utc", "CreatedAtUtc", "'2026-10-01T09:30:00+06:30'")]
    [InlineData("CK_Services_WithdrawnAtUtc_Utc", "WithdrawnAtUtc", "'2026-10-01T09:30:00+06:30'")]
    [InlineData("CK_Services_Direction", "Direction", "N'Sideways'")]
    [InlineData("CK_Services_OperatingDays", "Days", "0")]
    [InlineData("CK_Services_EffectivePeriod", "EffectiveTo", "'2026-10-04'")]
    [InlineData("CK_ServiceStops_Position", "Position", "0")]
    public async Task TimetableConstraints_RejectInvalidRows(string constraint, string column, string value)
    {
        var (routeId, stationId) = await InsertRouteAsync("CK");
        var serviceId = Guid.NewGuid();

        if (column == "Position")
        {
            await ExecuteAsync(InsertServiceSql(serviceId, routeId));
            var stop = await Assert.ThrowsAsync<SqlException>(() =>
                ExecuteAsync(InsertStopSql(serviceId, int.Parse(value, System.Globalization.CultureInfo.InvariantCulture), stationId)));
            Assert.Contains(constraint, stop.Message, StringComparison.Ordinal);
            Assert.Equal(0, await ScalarAsync("SELECT COUNT(*) FROM [timetable].[ServiceStops];"));
            return;
        }

        var failure = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(column switch
        {
            "CreatedAtUtc" => InsertServiceSql(serviceId, routeId, createdAtUtc: value),
            "WithdrawnAtUtc" => InsertServiceSql(serviceId, routeId, withdrawnAtUtc: value),
            "Direction" => InsertServiceSql(serviceId, routeId, direction: value),
            "Days" => InsertServiceSql(serviceId, routeId, days: value),
            "EffectiveTo" => InsertServiceSql(serviceId, routeId, effectiveTo: value),
            _ => throw new ArgumentOutOfRangeException(nameof(column))
        }));

        Assert.Equal(547, failure.Number);
        Assert.Contains(constraint, failure.Message, StringComparison.Ordinal);
        Assert.Equal(0, await ScalarAsync("SELECT COUNT(*) FROM [timetable].[Services];"));
    }

    /// <summary>
    /// R19, R41: a period can be empty only after a withdrawal. A service that never runs
    /// (<c>EffectiveTo</c> before <c>EffectiveFrom</c>) is stored when <c>WithdrawnAtUtc</c> is set.
    /// </summary>
    [Fact]
    public async Task TimetableConstraints_AllowAnEmptyPeriodOnlyAfterWithdrawal()
    {
        var (routeId, stationId) = await InsertRouteAsync("EP");
        var neverRuns = Guid.NewGuid();
        var oneDay = Guid.NewGuid();

        await ExecuteAsync(InsertServiceSql(neverRuns, routeId, effectiveTo: "'2026-10-04'", withdrawnAtUtc: UtcNow));
        await ExecuteAsync(InsertServiceSql(oneDay, routeId, effectiveTo: "'2026-10-05'"));
        await ExecuteAsync(InsertStopSql(neverRuns, 1, stationId));

        Assert.Equal(2, await ScalarAsync("SELECT COUNT(*) FROM [timetable].[Services];"));
        Assert.Equal(1, await ScalarAsync(
            $"SELECT COUNT(*) FROM [timetable].[Services] WHERE [Id] = '{neverRuns}' AND [EffectiveTo] < [EffectiveFrom];"));
    }

    /// <summary>R29, S48: the cross-schema keys are the database authority that the route and station exist.</summary>
    [Fact]
    public async Task ForeignKeys_RejectAServiceOrStopNamingNoNetworkRow()
    {
        var (routeId, stationId) = await InsertRouteAsync("FK");
        var serviceId = Guid.NewGuid();

        var unknownRoute = await Assert.ThrowsAsync<SqlException>(() =>
            ExecuteAsync(InsertServiceSql(serviceId, Guid.NewGuid())));
        Assert.Equal(547, unknownRoute.Number);
        Assert.Contains("FK_Services_Routes_RouteId", unknownRoute.Message, StringComparison.Ordinal);

        await ExecuteAsync(InsertServiceSql(serviceId, routeId));

        var unknownStation = await Assert.ThrowsAsync<SqlException>(() =>
            ExecuteAsync(InsertStopSql(serviceId, 1, Guid.NewGuid())));
        Assert.Equal(547, unknownStation.Number);
        Assert.Contains("FK_ServiceStops_Stations_StationId", unknownStation.Message, StringComparison.Ordinal);

        var unknownService = await Assert.ThrowsAsync<SqlException>(() =>
            ExecuteAsync(InsertStopSql(Guid.NewGuid(), 1, stationId)));
        Assert.Equal(547, unknownService.Number);
        Assert.Contains("FK_ServiceStops_Services_ServiceId", unknownService.Message, StringComparison.Ordinal);

        // NO ACTION: a network row that a service names cannot be deleted from under it.
        await ExecuteAsync(InsertStopSql(serviceId, 1, stationId));
        var deleteStation = await Assert.ThrowsAsync<SqlException>(() =>
            ExecuteAsync($"DELETE FROM [network].[RouteStations] WHERE [StationId] = '{stationId}'; DELETE FROM [network].[Stations] WHERE [Id] = '{stationId}';"));
        Assert.Equal(547, deleteStation.Number);
        Assert.Contains("FK_ServiceStops_Stations_StationId", deleteStation.Message, StringComparison.Ordinal);

        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM [timetable].[Services];"));
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM [timetable].[ServiceStops];"));
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) FROM [network].[Stations] WHERE [Id] = '{stationId}';"));
    }

    /// <summary>
    /// <c>docs/21</c> §Data: the three F-004 migrations applied on top of a database exactly as F-003
    /// left it, with station and route rows in it.
    /// </summary>
    [Fact]
    public async Task Migrate_FromF003Schema_CreatesTimetableObjectsGrantsAndSeed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var upgraded = await fixture.Container.CreateEmptyDatabaseAsync("timetable_upgrade", cancellationToken);

        await using (var context = IdentitySql.Context(upgraded.MigratorConnectionString))
        {
            await context.Database.MigrateAsync(LastF003Migration, cancellationToken);
        }

        await IdentitySql.ExecuteAsync(
            upgraded.MigratorConnectionString,
            $"""
            INSERT INTO [network].[Stations] ([Id], [Code], [NameEn], [NameMy], [IsActive], [CreatedAtUtc])
            VALUES ('{UpgradeStationId}', N'YGN', N'Yangon', N'ရန်ကုန်', 1, SYSUTCDATETIME() AT TIME ZONE 'UTC');
            INSERT INTO [network].[Routes] ([Id], [Code], [NameEn], [NameMy], [IsClosed], [IsActive], [CreatedAtUtc], [DeactivatedAtUtc])
            VALUES ('{UpgradeRouteId}', N'LOOP1', N'Circular', N'မြို့ပတ်', 1, 1, SYSUTCDATETIME() AT TIME ZONE 'UTC', NULL);
            INSERT INTO [network].[RouteStations] ([RouteId], [Position], [StationId]) VALUES ('{UpgradeRouteId}', 1, '{UpgradeStationId}');
            """);

        await using (var context = IdentitySql.Context(upgraded.MigratorConnectionString))
        {
            await context.Database.MigrateAsync(cancellationToken);
            Assert.Empty(await context.Database.GetPendingMigrationsAsync(cancellationToken));
        }

        // F-001's and F-003's data survive the upgrade untouched, and no service data ships (R27).
        Assert.Equal(1, await ScalarOnAsync(upgraded, $"SELECT COUNT(*) FROM [network].[Stations] WHERE [Id] = '{UpgradeStationId}' AND [Code] = N'YGN';"));
        Assert.Equal(1, await ScalarOnAsync(upgraded, $"SELECT COUNT(*) FROM [network].[Routes] WHERE [Id] = '{UpgradeRouteId}' AND [Code] = N'LOOP1';"));
        Assert.Equal(1, await ScalarOnAsync(upgraded, "SELECT COUNT(*) FROM [network].[RouteStations];"));
        Assert.Equal(0, await ScalarOnAsync(upgraded, "SELECT COUNT(*) FROM [timetable].[Services];"));
        Assert.Equal(0, await ScalarOnAsync(upgraded, "SELECT COUNT(*) FROM [timetable].[ServiceStops];"));

        // F-005 adds its three tables to the schema (this test migrates to the latest migration).
        Assert.Equal(
            ["ScheduleStopTimes", "ScheduleVersionServices", "ScheduleVersions", "ServiceStops", "Services"],
            (await StringsOnAsync(upgraded, "SELECT name FROM sys.tables WHERE schema_id = SCHEMA_ID(N'timetable')"))
                .Order(StringComparer.Ordinal));

        Assert.Equal(
            [
                "CK_ServiceStops_Position",
                "CK_Services_CreatedAtUtc_Utc",
                "CK_Services_Direction",
                "CK_Services_EffectivePeriod",
                "CK_Services_OperatingDays",
                "CK_Services_WithdrawnAtUtc_Utc",
            ],
            (await StringsOnAsync(
                upgraded,
                "SELECT name FROM sys.check_constraints WHERE parent_object_id IN (OBJECT_ID(N'timetable.Services'), OBJECT_ID(N'timetable.ServiceStops'))"))
                .Order(StringComparer.Ordinal));

        // Exactly these indexes, none unique except the primary keys (R35; the closure repeats a station).
        Assert.Equal(
            [
                "IX_ServiceStops_StationId:0:StationId",
                "IX_Services_Code_EffectiveFrom:0:Code,EffectiveFrom",
                "IX_Services_RouteId:0:RouteId",
                "PK_ServiceStops:1:ServiceId,Position",
                "PK_Services:1:Id",
            ],
            (await StringsOnAsync(
                upgraded,
                """
                SELECT CONCAT(i.name COLLATE DATABASE_DEFAULT, N':', CAST(i.is_unique AS int), N':',
                       STRING_AGG(c.name COLLATE DATABASE_DEFAULT, N',') WITHIN GROUP (ORDER BY ic.key_ordinal))
                FROM sys.indexes AS i
                JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal > 0
                JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                WHERE i.object_id IN (OBJECT_ID(N'timetable.Services'), OBJECT_ID(N'timetable.ServiceStops'))
                GROUP BY i.name, i.is_unique
                """)).Order(StringComparer.Ordinal));

        // Three foreign keys, NO ACTION on delete and update; two reference the network schema.
        Assert.Equal(
            [
                "FK_ServiceStops_Services_ServiceId:timetable.Services:NO_ACTION:NO_ACTION",
                "FK_ServiceStops_Stations_StationId:network.Stations:NO_ACTION:NO_ACTION",
                "FK_Services_Routes_RouteId:network.Routes:NO_ACTION:NO_ACTION",
            ],
            (await StringsOnAsync(
                upgraded,
                """
                SELECT CONCAT(name COLLATE DATABASE_DEFAULT, N':',
                       OBJECT_SCHEMA_NAME(referenced_object_id) COLLATE DATABASE_DEFAULT, N'.', OBJECT_NAME(referenced_object_id) COLLATE DATABASE_DEFAULT, N':',
                       delete_referential_action_desc COLLATE DATABASE_DEFAULT, N':', update_referential_action_desc COLLATE DATABASE_DEFAULT)
                FROM sys.foreign_keys
                WHERE parent_object_id IN (OBJECT_ID(N'timetable.Services'), OBJECT_ID(N'timetable.ServiceStops'))
                """)).Order(StringComparer.Ordinal));

        // S47: the role's grants on the two tables, exactly.
        Assert.Equal(ExpectedTimetableGrants, (await TimetableGrantsAsync(upgraded)).Order(StringComparer.Ordinal));

        // OQ49: the ten service grants, on top of F-002's fourteen and F-003's ten.
        Assert.Equal(
            [
                "Auditor:services.read",
                "FinanceOfficer:services.read",
                "RailwayAdministrator:services.manage",
                "RailwayAdministrator:services.read",
                "ReportingUser:services.read",
                "StationManager:services.read",
                "SystemAdministrator:services.manage",
                "SystemAdministrator:services.read",
                "TicketInspector:services.read",
                "TicketOperator:services.read",
            ],
            (await StringsOnAsync(
                upgraded,
                """
                SELECT CONCAT(r.[Name], N':', p.[Permission]) FROM [identity].[RolePermissions] AS p
                JOIN [identity].[Roles] AS r ON r.[Id] = p.[RoleId] WHERE p.[Permission] LIKE N'services.%'
                """)).Order(StringComparer.Ordinal));
        Assert.Equal(34, await ScalarOnAsync(upgraded, "SELECT COUNT(*) FROM [identity].[RolePermissions];"));
    }

    /// <summary>
    /// Rollback (plan §DB changes): down to F-003's last migration removes the timetable schema, its
    /// grants and the ten seeded grants, and keeps <c>network</c> and its rows; then forward again.
    /// </summary>
    [Fact]
    public async Task Migrate_DownToF003_RemovesTimetableObjectsGrantsAndSeedRowsAndKeepsNetwork()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (routeId, stationId) = await InsertRouteAsync("DN");
        var serviceId = Guid.NewGuid();
        await ExecuteAsync(InsertServiceSql(serviceId, routeId) + InsertStopSql(serviceId, 1, stationId));

        await using (var context = IdentitySql.Context(database.MigratorConnectionString))
        {
            await context.Database.MigrateAsync(LastF003Migration, cancellationToken);
            Assert.Equal(LastF003Migration, (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).Last());
        }

        Assert.Equal(0, await ScalarAsync("SELECT COUNT(*) FROM sys.schemas WHERE name = N'timetable';"));
        Assert.Equal(0, await ScalarAsync("SELECT COUNT(*) FROM sys.tables WHERE name IN (N'Services', N'ServiceStops');"));
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) FROM [network].[Stations] WHERE [Id] = '{stationId}';"));
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) FROM [network].[Routes] WHERE [Id] = '{routeId}';"));
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) FROM [network].[RouteStations] WHERE [RouteId] = '{routeId}';"));

        Assert.Empty(await TimetableGrantsAsync(database));
        Assert.Equal(0, await ScalarAsync("SELECT COUNT(*) FROM [identity].[RolePermissions] WHERE [Permission] LIKE N'services.%';"));
        Assert.Equal(24, await ScalarAsync("SELECT COUNT(*) FROM [identity].[RolePermissions];"));

        // F-003's route grants are not touched by the timetable grant revocation.
        Assert.Equal(1, await ScalarAsync(
            """
            SELECT COUNT(*) FROM sys.database_permissions
            WHERE grantee_principal_id = DATABASE_PRINCIPAL_ID(N'ycr_app') AND state = 'G'
              AND major_id = OBJECT_ID(N'network.Routes') AND permission_name = N'UPDATE'
              AND minor_id = COLUMNPROPERTY(OBJECT_ID(N'network.Routes'), N'IsActive', 'ColumnId');
            """));

        // And forward again, so the rollback is repeatable.
        await using (var context = IdentitySql.Context(database.MigratorConnectionString))
        {
            await context.Database.MigrateAsync(cancellationToken);
        }

        Assert.Equal(ExpectedTimetableGrants, (await TimetableGrantsAsync(database)).Order(StringComparer.Ordinal));
        Assert.Equal(34, await ScalarAsync("SELECT COUNT(*) FROM [identity].[RolePermissions];"));
        Assert.Equal(0, await ScalarAsync("SELECT COUNT(*) FROM [timetable].[Services];"));
    }

    private const string LastF003Migration = "20260924152836_Security_NetworkRouteGrants";

    private static readonly Guid UpgradeStationId = Guid.Parse("0199b3a0-0000-7000-8000-00000000f401");
    private static readonly Guid UpgradeRouteId = Guid.Parse("0199b3a0-0000-7000-8000-00000000f402");

    private static readonly string[] ExpectedTimetableGrants =
    [
        "INSERT:ServiceStops:",
        "INSERT:Services:",
        "SELECT:ServiceStops:",
        "SELECT:Services:",
        "UPDATE:Services:EffectiveTo",
        "UPDATE:Services:WithdrawnAtUtc",
    ];

    /// <summary>The <c>ycr_app</c> role's grants on the timetable schema and its objects, as <c>PERMISSION:Object:Column</c>.</summary>
    private static Task<List<string>> TimetableGrantsAsync(TestDatabase target) =>
        IdentitySql.StringsAsync(
            target.MigratorConnectionString,
            """
            SELECT CONCAT(p.permission_name COLLATE DATABASE_DEFAULT, N':', OBJECT_NAME(p.major_id) COLLATE DATABASE_DEFAULT, N':', c.name COLLATE DATABASE_DEFAULT)
            FROM sys.database_permissions AS p
            LEFT JOIN sys.columns AS c ON c.object_id = p.major_id AND c.column_id = p.minor_id
            WHERE p.grantee_principal_id = DATABASE_PRINCIPAL_ID(N'ycr_app')
              AND p.class = 1 AND p.state IN ('G', 'W')
              AND OBJECT_SCHEMA_NAME(p.major_id) = N'timetable'
            """);

    private static Task<int> ScalarOnAsync(TestDatabase target, string sql) =>
        IdentitySql.ScalarAsync<int>(target.MigratorConnectionString, sql);

    private static Task<List<string>> StringsOnAsync(TestDatabase target, string sql) =>
        IdentitySql.StringsAsync(target.MigratorConnectionString, sql);
    /// <summary>A route with one station, written directly under the migrator credential.</summary>
    private async Task<(Guid RouteId, Guid StationId)> InsertRouteAsync(string prefix)
    {
        var stationId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        await ExecuteAsync(
            $"""
            INSERT INTO [network].[Stations] ([Id], [Code], [NameEn], [NameMy], [IsActive], [CreatedAtUtc])
            VALUES ('{stationId}', N'{prefix}S', N'Station {prefix}', N'ဘူတာ', 1, {UtcNow});
            INSERT INTO [network].[Routes] ([Id], [Code], [NameEn], [NameMy], [IsClosed], [IsActive], [CreatedAtUtc], [DeactivatedAtUtc])
            VALUES ('{routeId}', N'{prefix}R', N'Route {prefix}', N'လမ်းကြောင်း', 0, 1, {UtcNow}, NULL);
            INSERT INTO [network].[RouteStations] ([RouteId], [Position], [StationId]) VALUES ('{routeId}', 1, '{stationId}');
            """);
        return (routeId, stationId);
    }

    internal static string InsertServiceSql(
        Guid id,
        Guid routeId,
        string code = "N'S101'",
        string direction = "N'Forward'",
        string days = "1",
        string effectiveFrom = "'2026-10-05'",
        string effectiveTo = "NULL",
        string createdAtUtc = UtcNow,
        string withdrawnAtUtc = "NULL") =>
        $"""
        INSERT INTO [timetable].[Services]
            ([Id], [Code], [NameEn], [NameMy], [RouteId], [Direction],
             [RunsOnMonday], [RunsOnTuesday], [RunsOnWednesday], [RunsOnThursday], [RunsOnFriday], [RunsOnSaturday], [RunsOnSunday],
             [EffectiveFrom], [EffectiveTo], [CreatedAtUtc], [WithdrawnAtUtc])
        VALUES ('{id}', {code}, N'Service', N'ရထား', '{routeId}', {direction},
             {days}, 0, 0, 0, 0, 0, 0,
             {effectiveFrom}, {effectiveTo}, {createdAtUtc}, {withdrawnAtUtc});
        """;

    internal static string InsertStopSql(Guid serviceId, int position, Guid stationId) =>
        $"INSERT INTO [timetable].[ServiceStops] ([ServiceId], [Position], [StationId]) VALUES ('{serviceId}', {position}, '{stationId}');";

    private Task ExecuteAsync(string sql) => IdentitySql.ExecuteAsync(database.MigratorConnectionString, sql);

    private Task<int> ScalarAsync(string sql) => IdentitySql.ScalarAsync<int>(database.MigratorConnectionString, sql);
}

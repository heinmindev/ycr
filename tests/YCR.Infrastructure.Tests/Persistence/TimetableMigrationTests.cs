using Microsoft.Data.SqlClient;
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

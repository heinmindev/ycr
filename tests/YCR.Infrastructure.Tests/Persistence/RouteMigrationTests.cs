using Microsoft.Data.SqlClient;
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

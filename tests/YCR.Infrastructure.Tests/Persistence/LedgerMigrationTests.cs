using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Common.Abstractions;
using YCR.Application.Common.Authorization;
using YCR.Application.Network;
using YCR.Domain.Common;
using YCR.Domain.Network;
using YCR.Infrastructure;
using YCR.Infrastructure.Persistence;
using YCR.TestSupport;

namespace YCR.Infrastructure.Tests.Persistence;

/// <summary>
/// S18 and verifications V1, V2 and V4: the audit ledger as ADR-0017 and ADR-0021 define it,
/// created by the migration bundle against the pinned SQL Server 2022 image.
/// </summary>
/// <remarks>
/// Credential: the migrator, because these assert on migration output. The least-privilege
/// checks on the same table (S22) arrive with the <c>ycr_app</c> role at plan step 9.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public sealed class LedgerMigrationTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private TestDatabase database = null!;

    public async ValueTask InitializeAsync() =>
        database = await fixture.Container.ProvisionDatabaseAsync("ledger", TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>V1: the pinned image's edition really does create an append-only ledger table.</summary>
    [Fact]
    public async Task LedgerTable_AfterMigration_IsAppendOnlyLedgerTable()
    {
        var ledgerType = await ScalarAsync<string>(
            """
            SELECT t.ledger_type_desc
            FROM sys.tables AS t
            JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            WHERE s.name = 'audit' AND t.name = 'AuditEvents';
            """);

        Assert.Equal("APPEND_ONLY_LEDGER_TABLE", ledgerType);
    }

    /// <summary>V2: check constraints and nonclustered indexes survive on a ledger table.</summary>
    [Fact]
    public async Task LedgerTable_AfterMigration_HasEveryConstraintAndIndexAdr0021Requires()
    {
        var constraints = await QueryStringsAsync(
            """
            SELECT name FROM sys.check_constraints
            WHERE parent_object_id = OBJECT_ID('audit.AuditEvents')
            ORDER BY name;
            """);

        Assert.Equal(
            ["CK_AuditEvents_ActorRole", "CK_AuditEvents_AfterJson", "CK_AuditEvents_BeforeJson", "CK_AuditEvents_OccurredAtUtc_Utc"],
            constraints);

        var indexes = await QueryStringsAsync(
            """
            SELECT name FROM sys.indexes
            WHERE object_id = OBJECT_ID('audit.AuditEvents') AND type_desc = 'NONCLUSTERED'
            ORDER BY name;
            """);

        Assert.Equal(["IX_AuditEvents_OccurredAtUtc", "IX_AuditEvents_Subject"], indexes);
    }

    [Fact]
    public async Task LedgerTable_AfterMigration_HasTheFourteenAdr0021Columns()
    {
        // The generated ledger columns are excluded: SQL Server marks them hidden and owns them.
        var columns = await QueryStringsAsync(
            """
            SELECT c.name
            FROM sys.columns AS c
            WHERE c.object_id = OBJECT_ID('audit.AuditEvents') AND c.is_hidden = 0
            ORDER BY c.name;
            """);

        Assert.Equal(
            [
                "Action", "ActorRole", "ActorUserId", "AfterJson", "AuthorizedByPermission",
                "BeforeJson", "ClientIp", "CorrelationId", "Id", "OccurredAtUtc",
                "PayloadVersion", "ReasonCode", "SubjectId", "SubjectType"
            ],
            columns);
    }

    /// <summary>
    /// V4: EF Core can INSERT into a ledger table whose generated columns it does not map, and
    /// the audit row commits in the caller's own <c>SaveChangesAsync</c> (ADR-0017).
    /// </summary>
    [Fact]
    public async Task AuditWriter_WithEfCore_InsertsIntoTheLedgerInTheCallersUnitOfWork()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var actorId = Guid.CreateVersion7();
        var currentUser = new StubCurrentUser
        {
            UserId = actorId,
            Roles = ["Admin", "StationManager"],
            ClientIp = "203.0.113.7",
            CorrelationId = "corr-ledger-1",
            // Derived server-side from the endpoint's required permission (hein's ruling,
            // 2026-09-20). The handler never names its own authority.
            AuthorizedByPermission = Permissions.StationsManage
        };

        var stationId = Guid.CreateVersion7();
        var station = Station.Create(
            stationId,
            StationCode.Create("YGN").Value,
            BilingualName.Create("Yangon Central", "ရန်ကုန်ဘူတာကြီး", NetworkErrors.InvalidStationName).Value,
            DateTimeOffset.UtcNow);

        await using var provider = BuildProvider(currentUser);
        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<YcrDbContext>();
            var writer = scope.ServiceProvider.GetRequiredService<IAuditWriter>();

            context.Stations.Add(station);
            writer.Record(
                "Network.StationCreated",
                NetworkAuditSubjects.Station,
                station.Id,
                before: null,
                after: StationAuditSnapshot.From(station));

            // One SaveChangesAsync commits the station and its audit row together.
            await context.SaveChangesAsync(cancellationToken);
        }

        await using var connection = new SqlConnection(database.MigratorConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            """
            SELECT [Action], [ActorUserId], [ActorRole], [SubjectType], [SubjectId],
                   [BeforeJson], [AfterJson], [CorrelationId], [ClientIp],
                   [AuthorizedByPermission], [PayloadVersion]
            FROM [audit].[AuditEvents];
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        Assert.True(await reader.ReadAsync(cancellationToken));
        Assert.Equal("Network.StationCreated", reader.GetString(0));
        Assert.Equal(actorId, reader.GetGuid(1));
        // ADR-0021: a JSON array, which the ISJSON constraint alone would not guarantee.
        Assert.Equal("""["Admin","StationManager"]""", reader.GetString(2));
        // The module-declared constant, not a CLR type name (hein's ruling, 2026-09-20).
        Assert.Equal("Network.Station", reader.GetString(3));
        Assert.Equal(NetworkAuditSubjects.Station, reader.GetString(3));
        Assert.Equal(stationId, reader.GetGuid(4));
        Assert.True(reader.IsDBNull(5));
        Assert.Equal(
            """{"code":"YGN","nameEn":"Yangon Central","nameMy":"ရန်ကုန်ဘူတာကြီး","isActive":true}""",
            reader.GetString(6));
        Assert.Equal("corr-ledger-1", reader.GetString(7));
        Assert.Equal("203.0.113.7", reader.GetString(8));
        Assert.Equal(Permissions.StationsManage, reader.GetString(9));
        Assert.Equal(1, reader.GetInt32(10));
        Assert.False(await reader.ReadAsync(cancellationToken));
    }

    [Fact]
    public async Task AuditWriter_WhenThereIsNoAuthenticatedActor_LeavesActorFieldsNull()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var currentUser = new StubCurrentUser { CorrelationId = "corr-system" };

        await using var provider = BuildProvider(currentUser);
        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<YcrDbContext>();
            scope.ServiceProvider.GetRequiredService<IAuditWriter>()
                .Record("Network.SystemSweep", NetworkAuditSubjects.Station, subjectId: null, before: null, after: null);
            await context.SaveChangesAsync(cancellationToken);
        }

        // ADR-0021 allows a null actor and a null subject id; the subject type never is.
        Assert.Null(await ScalarAsync<object>("SELECT [ActorUserId] FROM [audit].[AuditEvents];"));
        Assert.Null(await ScalarAsync<object>("SELECT [ActorRole] FROM [audit].[AuditEvents];"));
        Assert.Null(await ScalarAsync<object>("SELECT [SubjectId] FROM [audit].[AuditEvents];"));
        Assert.Null(await ScalarAsync<object>("SELECT [AuthorizedByPermission] FROM [audit].[AuditEvents];"));
        Assert.Equal(
            NetworkAuditSubjects.Station,
            await ScalarAsync<string>("SELECT [SubjectType] FROM [audit].[AuditEvents];"));
    }

    /// <summary>
    /// Hein's ruling, 2026-09-20: an entity must never become an audit payload, and this is now
    /// enforced by the type system — <c>Record</c> takes <c>IAuditSnapshot?</c>, so passing an
    /// aggregate does not compile. There is no runtime rejection left to test.
    /// </summary>
    /// <remarks>
    /// What is worth asserting is that the compile barrier is real and stays real: if an entity
    /// ever implemented <c>IAuditSnapshot</c>, the guarantee would quietly disappear and every
    /// call site would keep compiling. The architecture test covers placement and record-ness;
    /// this covers the aggregate specifically.
    /// </remarks>
    [Fact]
    public void Station_IsNotAnAuditSnapshot_SoPassingItCannotCompile()
    {
        Assert.False(typeof(IAuditSnapshot).IsAssignableFrom(typeof(Station)));
        Assert.True(typeof(IAuditSnapshot).IsAssignableFrom(typeof(StationAuditSnapshot)));
    }

    [Fact]
    public async Task AuditWriter_RecordsTheSnapshotShapeNotTheEntityShape()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var station = Station.Create(
            Guid.CreateVersion7(),
            StationCode.Create("DNN").Value,
            BilingualName.Create("Danyingon", "ဒညင်းကုန်း", NetworkErrors.InvalidStationName).Value,
            DateTimeOffset.UtcNow);

        await using var provider = BuildProvider(new StubCurrentUser { CorrelationId = "corr-snapshot" });
        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<YcrDbContext>();
            scope.ServiceProvider.GetRequiredService<IAuditWriter>().Record(
                "Network.StationDeactivated",
                NetworkAuditSubjects.Station,
                station.Id,
                before: StationAuditSnapshot.From(station),
                after: StationAuditSnapshot.From(station) with { IsActive = false });
            await context.SaveChangesAsync(cancellationToken);
        }

        // Exactly the four snapshot fields: no Id, no DomainEvents, nothing the aggregate might
        // grow later. PayloadVersion is what changes when this shape does (ADR-0021 rule 3).
        Assert.Equal(
            """{"code":"DNN","nameEn":"Danyingon","nameMy":"ဒညင်းကုန်း","isActive":true}""",
            await ScalarAsync<string>("SELECT [BeforeJson] FROM [audit].[AuditEvents];"));
        Assert.Equal(
            """{"code":"DNN","nameEn":"Danyingon","nameMy":"ဒညင်းကုန်း","isActive":false}""",
            await ScalarAsync<string>("SELECT [AfterJson] FROM [audit].[AuditEvents];"));
        Assert.Equal(1, await ScalarAsync<int>("SELECT [PayloadVersion] FROM [audit].[AuditEvents];"));
    }

    [Fact]
    public async Task LedgerTable_Update_IsRejectedByTheEngine()
    {
        await InsertRawAsync("corr-update");

        var failure = await Assert.ThrowsAsync<SqlException>(() =>
            ExecuteAsync("UPDATE [audit].[AuditEvents] SET [ReasonCode] = N'tampered';"));

        Assert.Contains("append only", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LedgerTable_Delete_IsRejectedByTheEngine()
    {
        await InsertRawAsync("corr-delete");

        await Assert.ThrowsAsync<SqlException>(() =>
            ExecuteAsync("DELETE FROM [audit].[AuditEvents];"));

        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM [audit].[AuditEvents];"));
    }

    [Fact]
    public async Task LedgerTable_WithMalformedJsonPayload_IsRejectedByTheCheckConstraint()
    {
        var failure = await Assert.ThrowsAsync<SqlException>(() =>
            ExecuteAsync(
                """
                INSERT INTO [audit].[AuditEvents]
                    ([Id], [OccurredAtUtc], [Action], [SubjectType], [CorrelationId], [PayloadVersion], [AfterJson])
                VALUES (NEWID(), SYSUTCDATETIME() AT TIME ZONE 'UTC', N'Network.Bad', N'Station', N'corr-bad', 1, N'not json');
                """));

        // The constraint is the only chance to reject a malformed payload: the row can never be
        // repaired once it is in an append-only table.
        Assert.Contains("CK_AuditEvents_AfterJson", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// ADR-0018: <c>*Utc</c> columns hold UTC values, and on an append-only table the database is
    /// the only place that rule can be enforced in time.
    /// </summary>
    /// <remarks>
    /// The counterpart of <c>CK_Stations_CreatedAtUtc_Utc</c>. It matters more here than on
    /// <c>Stations</c>: a station row can be corrected, an audit row cannot, and
    /// <c>OccurredAtUtc</c> is what orders tamper-evident history. The insert is raw SQL so the
    /// constraint, not an EF mapping, is what rejects it.
    /// </remarks>
    [Fact]
    public async Task LedgerTable_WithNonUtcOccurredAtUtc_IsRejectedByTheCheckConstraint()
    {
        var failure = await Assert.ThrowsAsync<SqlException>(() =>
            ExecuteAsync(
                """
                INSERT INTO [audit].[AuditEvents]
                    ([Id], [OccurredAtUtc], [Action], [SubjectType], [CorrelationId], [PayloadVersion])
                VALUES (NEWID(), TODATETIMEOFFSET(SYSUTCDATETIME(), '+06:30'), N'Network.Bad', N'Station', N'corr-offset', 1);
                """));

        Assert.Equal(547, failure.Number);
        Assert.Contains("CK_AuditEvents_OccurredAtUtc_Utc", failure.Message, StringComparison.Ordinal);

        // Nothing landed: the append-only table is unchanged, which is the property that cannot be
        // restored afterwards if the constraint were missing.
        Assert.Equal(0, await ScalarAsync<int>(
            "SELECT COUNT(*) FROM [audit].[AuditEvents] WHERE [CorrelationId] = N'corr-offset';"));
    }

    /// <summary>Plan P7: reverting this migration must refuse, not drop the ledger.</summary>
    [Fact]
    public void DownMigration_ThrowsInsteadOfDroppingTheLedgerTable()
    {
        using var context = NewContext();
        var migrations = context.Database.GetMigrations().Order(StringComparer.Ordinal).ToArray();
        var ledgerMigration = Assert.Single(migrations, name => name.EndsWith("Audit_CreateAuditEventsLedger", StringComparison.Ordinal));
        var previous = migrations[Array.IndexOf(migrations, ledgerMigration) - 1];

        var script = context.GetService<IMigrator>().GenerateScript(ledgerMigration, previous);

        Assert.Contains("THROW 50017", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE [audit].[AuditEvents]", script, StringComparison.Ordinal);
    }

    private ServiceProvider BuildProvider(ICurrentUser currentUser) =>
        new ServiceCollection()
            .AddInfrastructure(database.MigratorConnectionString)
            .AddScoped(_ => currentUser)
            .BuildServiceProvider();

    private YcrDbContext NewContext() =>
        new(new DbContextOptionsBuilder<YcrDbContext>()
            .UseSqlServer(database.MigratorConnectionString)
            .Options);

    private Task InsertRawAsync(string correlationId) =>
        ExecuteAsync(
            $"""
             INSERT INTO [audit].[AuditEvents]
                 ([Id], [OccurredAtUtc], [Action], [SubjectType], [CorrelationId], [PayloadVersion])
             VALUES (NEWID(), SYSUTCDATETIME() AT TIME ZONE 'UTC', N'Network.StationCreated', N'Station', N'{correlationId}', 1);
             """);

    private async Task ExecuteAsync(string sql)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(database.MigratorConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<T?> ScalarAsync<T>(string sql)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(database.MigratorConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync(cancellationToken);

        return value is null or DBNull ? default : (T)value;
    }

    private async Task<string[]> QueryStringsAsync(string sql)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(database.MigratorConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var values = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(reader.GetString(0));
        }

        return [.. values];
    }

    private sealed class StubCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; init; }

        public IReadOnlyCollection<string> Roles { get; init; } = [];

        public string? ClientIp { get; init; }

        public required string CorrelationId { get; init; }

        public string? AuthorizedByPermission { get; init; }
    }
}

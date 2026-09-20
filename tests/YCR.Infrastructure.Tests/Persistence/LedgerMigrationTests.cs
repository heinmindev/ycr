using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Common.Abstractions;
using YCR.Application.Common.Authorization;
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
            ["CK_AuditEvents_ActorRole", "CK_AuditEvents_AfterJson", "CK_AuditEvents_BeforeJson"],
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
            CorrelationId = "corr-ledger-1"
        };

        var stationId = Guid.CreateVersion7();
        var station = Station.Create(
            stationId,
            StationCode.Create("YGN").Value,
            BilingualName.Create("Yangon Central", "ရန်ကုန်ဘူတာကြီး").Value,
            DateTimeOffset.UtcNow);

        await using var provider = BuildProvider(currentUser);
        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<YcrDbContext>();
            var writer = scope.ServiceProvider.GetRequiredService<IAuditWriter>();

            context.Stations.Add(station);
            writer.Record(
                "Network.StationCreated",
                station,
                before: null,
                after: new { code = "YGN" },
                authorizedByPermission: Permissions.StationsManage);

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
        Assert.Equal(nameof(Station), reader.GetString(3));
        Assert.Equal(stationId, reader.GetGuid(4));
        Assert.True(reader.IsDBNull(5));
        Assert.Equal("""{"code":"YGN"}""", reader.GetString(6));
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
                .Record("Network.SystemSweep", typeof(Station), before: null, after: null);
            await context.SaveChangesAsync(cancellationToken);
        }

        // ADR-0021 allows a null actor and a null subject id; the subject type never is.
        Assert.Null(await ScalarAsync<object>("SELECT [ActorUserId] FROM [audit].[AuditEvents];"));
        Assert.Null(await ScalarAsync<object>("SELECT [ActorRole] FROM [audit].[AuditEvents];"));
        Assert.Null(await ScalarAsync<object>("SELECT [SubjectId] FROM [audit].[AuditEvents];"));
        Assert.Equal(nameof(Station), await ScalarAsync<string>("SELECT [SubjectType] FROM [audit].[AuditEvents];"));
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
                VALUES (NEWID(), SYSDATETIMEOFFSET(), N'Network.Bad', N'Station', N'corr-bad', 1, N'not json');
                """));

        // The constraint is the only chance to reject a malformed payload: the row can never be
        // repaired once it is in an append-only table.
        Assert.Contains("CK_AuditEvents_AfterJson", failure.Message, StringComparison.Ordinal);
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
             VALUES (NEWID(), SYSDATETIMEOFFSET(), N'Network.StationCreated', N'Station', N'{correlationId}', 1);
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
    }
}

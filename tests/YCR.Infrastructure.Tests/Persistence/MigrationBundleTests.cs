using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using YCR.Domain.Network;
using YCR.Infrastructure.Persistence;
using YCR.TestSupport;

namespace YCR.Infrastructure.Tests.Persistence;

/// <summary>
/// Verification V6 and the step-7 smoke test: the EF migration bundle — the artifact plan
/// decision P1 chose instead of a <c>YCR.DbMigrator</c> project — builds on EF 10 and applies
/// cleanly to the pinned SQL Server 2022 image, and the schema it produces is usable.
/// </summary>
/// <remarks>
/// Applying migrations in-process with <c>Migrate()</c> would pass while leaving the shipped
/// artifact untested, which is precisely the risk V6 exists to retire.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public sealed class MigrationBundleTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private TestDatabase database = null!;

    public async ValueTask InitializeAsync() =>
        database = await fixture.Container.ProvisionDatabaseAsync("migration_bundle", TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Migrate_AgainstPinnedImage_CreatesStationsAndLedgerTable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // The bundle already ran in InitializeAsync; V6 is the fact that it built and applied.
        var columns = await QueryStringsAsync(
            """
            SELECT c.name
            FROM sys.columns AS c
            JOIN sys.tables AS t ON t.object_id = c.object_id
            JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            WHERE s.name = 'network' AND t.name = 'Stations'
            ORDER BY c.name;
            """,
            cancellationToken);

        Assert.Equal(
            ["Code", "CreatedAtUtc", "Id", "IsActive", "NameEn", "NameMy"],
            columns);

        var indexes = await QueryStringsAsync(
            """
            SELECT i.name
            FROM sys.indexes AS i
            JOIN sys.tables AS t ON t.object_id = i.object_id
            JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            WHERE s.name = 'network' AND t.name = 'Stations' AND i.is_unique = 1 AND i.is_primary_key = 0;
            """,
            cancellationToken);

        Assert.Equal(["UX_Stations_Code"], indexes);

        // Both raw-SQL and EF-generated migrations ship in the same bundle. LedgerMigrationTests
        // owns the ledger's shape; what matters here is that the bundle created it at all.
        var ledgerTables = await QueryStringsAsync(
            """
            SELECT t.name
            FROM sys.tables AS t
            JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            WHERE s.name = 'audit' AND t.ledger_type_desc = 'APPEND_ONLY_LEDGER_TABLE';
            """,
            cancellationToken);

        Assert.Equal(["AuditEvents"], ledgerTables);
    }

    [Fact]
    public async Task Migrate_AgainstPinnedImage_RecordsEveryMigrationAsApplied()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var applied = await QueryStringsAsync(
            "SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;",
            cancellationToken);

        // Every migration the assembly carries must be in the bundle. A migration that EF can
        // scaffold but the bundle silently omits is exactly the V6 failure mode.
        await using var context = NewContext();
        var expected = context.Database.GetMigrations().Order(StringComparer.Ordinal).ToArray();

        Assert.NotEmpty(expected);
        Assert.Equal(expected, applied);
    }

    [Fact]
    public async Task Stations_AfterBundleMigration_RoundTripThroughEfCore()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var code = StationCode.Create("INS").Value;
        var name = BilingualName.Create("Insein", "အင်းစိန်").Value;
        var id = Guid.CreateVersion7();

        await using (var writer = NewContext())
        {
            writer.Stations.Add(Station.Create(id, code, name, DateTimeOffset.UtcNow));
            await writer.SaveChangesAsync(cancellationToken);
        }

        await using var reader = NewContext();
        var persisted = await reader.Stations.SingleAsync(station => station.Id == id, cancellationToken);

        Assert.Equal("INS", persisted.Code.Value);
        Assert.Equal("Insein", persisted.Name.En);
        // S14's concern, checked here as soon as there is a real database: Myanmar text must
        // survive the round trip byte for byte, which is what the nvarchar mapping is for.
        Assert.Equal("အင်းစိန်", persisted.Name.My);
        Assert.True(persisted.IsActive);
    }

    private YcrDbContext NewContext() =>
        new(new DbContextOptionsBuilder<YcrDbContext>()
            .UseSqlServer(database.MigratorConnectionString)
            .Options);

    private async Task<string[]> QueryStringsAsync(string sql, CancellationToken cancellationToken)
    {
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
}

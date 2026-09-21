using Microsoft.Data.SqlClient;
using YCR.TestSupport;

namespace YCR.Infrastructure.Tests.Persistence;

/// <summary>
/// S19: the audit ledger migration fails loudly on an unsupported SQL Server version rather than
/// silently degrading to a normal table (ADR-0017 item 5, AGENTS.md rule 8).
/// </summary>
/// <remarks>
/// Run against a real, digest-pinned SQL Server <strong>2019</strong> server. Testing the guard
/// against a genuinely unsupported server is the only way to know it fires <em>before</em> the
/// DDL rather than after: a unit test over the script text would prove the <c>THROW</c> exists,
/// not that it runs first, and not that the ledger syntax does not fail on its own beforehand
/// with some incidental error.
/// <para>
/// <strong>Trunk-only</strong> (spec E5, plan review item 7). It pulls a second multi-gigabyte
/// image, which the plan judged too expensive for every branch build. It is excluded by trait,
/// not skipped, so the branch run still reports zero skipped tests (S15).
/// </para>
/// <para>
/// <strong>What this does not cover:</strong> the guard's <em>edition</em> branch. No readily
/// available container runs a 2022 edition without ledger support, so that branch is covered by
/// V1 in step 8 — which showed the pinned image's Developer edition does create a ledger table —
/// and by code review. Stated here so nobody reads this test as proving more than it does.
/// </para>
/// </remarks>
[Trait("Category", "TrunkOnly")]
public sealed class LedgerGuardTests : IAsyncLifetime
{
    private SqlServerTestContainer container = null!;

    public async ValueTask InitializeAsync()
    {
        container = new SqlServerTestContainer(SqlServerImage.Unsupported.Reference);
        await container.StartAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await container.DisposeAsync();

    [Fact]
    public async Task Migrate_OnSqlServer2019_ThrowsWithVersionInMessage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var database = await container.CreateEmptyDatabaseAsync("guard_2019", cancellationToken);

        // Confirm the server really is the unsupported version, so a pass cannot come from the
        // guard firing for some unrelated reason.
        var detectedMajorVersion = await ScalarAsync(
            database.MigratorConnectionString,
            "SELECT CAST(SERVERPROPERTY('ProductMajorVersion') AS int);",
            cancellationToken);
        Assert.Equal(SqlServerImage.Unsupported.ProductMajorVersion, Convert.ToInt32(detectedMajorVersion));

        var failure = await MigrationBundle.TryApplyAsync(database.MigratorConnectionString, cancellationToken);

        Assert.NotNull(failure);

        // It is our guard, not an incidental syntax error from the ledger DDL. The message names
        // the requirement, the detected version and the edition, which is what makes an operator
        // able to act on it (ADR-0017 item 5).
        Assert.Contains("YCR requires SQL Server 2022", failure, StringComparison.Ordinal);
        Assert.Contains("ADR-0017", failure, StringComparison.Ordinal);
        Assert.Contains("Detected version 15.", failure, StringComparison.Ordinal);
        Assert.Contains("edition", failure, StringComparison.OrdinalIgnoreCase);

        // Not a SQL Server parse error about LEDGER, which would mean the guard ran too late.
        Assert.DoesNotContain("Incorrect syntax near", failure, StringComparison.Ordinal);
    }

    /// <summary>
    /// The guard runs before any DDL, so a rejected server is left with nothing half-created
    /// (AGENTS.md rule 8: no silent degradation, and no partial schema either).
    /// </summary>
    [Fact]
    public async Task Migrate_OnSqlServer2019_LeavesNoAuditSchemaBehind()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var database = await container.CreateEmptyDatabaseAsync("guard_2019_partial", cancellationToken);

        Assert.NotNull(await MigrationBundle.TryApplyAsync(database.MigratorConnectionString, cancellationToken));

        Assert.Equal(0, Convert.ToInt32(await ScalarAsync(
            database.MigratorConnectionString,
            "SELECT COUNT(*) FROM sys.schemas WHERE name = 'audit';",
            cancellationToken)));

        Assert.Equal(0, Convert.ToInt32(await ScalarAsync(
            database.MigratorConnectionString,
            "SELECT COUNT(*) FROM sys.tables WHERE name = 'AuditEvents';",
            cancellationToken)));
    }

    private static async Task<object?> ScalarAsync(
        string connectionString,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);

        return await command.ExecuteScalarAsync(cancellationToken);
    }
}

using Microsoft.Data.SqlClient;
using YCR.Infrastructure.Identifiers;
using YCR.TestSupport;

namespace YCR.Infrastructure.Tests.Identifiers;

/// <summary>
/// S23 and ADR-0006's REQUIRED CONTROL: inserting 10,000 rows through
/// <see cref="SqlServerSequentialGuidIdGenerator"/> into a clustered GUID key must not fragment
/// the index materially worse than SQL Server's own <c>NEWSEQUENTIALID()</c>.
/// </summary>
/// <remarks>
/// ADR-0006 chose application-assigned sequential GUIDs so an aggregate has its identity before
/// it is saved. The risk that choice takes on is page-split fragmentation on a clustered key —
/// the exact problem <c>NEWSEQUENTIALID()</c> exists to avoid. This measures it rather than
/// assuming the generator behaves; a random GUID would fail here by roughly 90 percentage points.
///
/// <para>
/// <strong>Credential exception — the one trade-off in the test suite.</strong> This runs under
/// the <strong>migrator</strong> credential, not <c>ycr_app</c>, unlike every other Application
/// and API test. It has to: it creates its own measurement tables and reads
/// <c>sys.dm_db_index_physical_stats</c>, which requires <c>VIEW DATABASE STATE</c>. Granting
/// that to <c>ycr_app</c> to satisfy a test would widen the very role that spec E7 and ADR-0017
/// item 3 exist to keep narrow, and <c>DatabasePrivilegeTests</c> asserts that role holds nothing
/// beyond its four grants. Running as migrator is correct here: this is an infrastructure
/// characterisation test about index behaviour, not an application-path test. Recorded in the
/// plan's §Test fixture table as the single documented exception.
/// </para>
///
/// <para>
/// <strong>Trunk-only</strong> (spec E5): 20,000 inserts and two index rebuilds are too slow for
/// every branch build. Excluded by trait rather than skipped, so the branch run still reports
/// zero skipped tests (S15).
/// </para>
/// </remarks>
[Trait("Category", "TrunkOnly")]
[Collection(SqlServerCollection.Name)]
public sealed class SequentialGuidFragmentationTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const int RowCount = 10_000;

    /// <summary>ADR-0006: no more than ten percentage points worse than the baseline.</summary>
    private const double MaximumPointsWorseThanBaseline = 10.0;

    private TestDatabase database = null!;

    public async ValueTask InitializeAsync() =>
        database = await fixture.Container.ProvisionDatabaseAsync("fragmentation", TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Insert10000Rows_FragmentationWithinTenPointsOfBaseline_Passes()
    {
        var generator = new SqlServerSequentialGuidIdGenerator();

        var ours = await MeasureAsync(
            "GeneratedIds",
            Enumerable.Range(0, RowCount).Select(_ => generator.New()).ToArray());

        var baseline = await MeasureAsync("BaselineIds", ids: null);

        var compatibilityLevel = await ScalarAsync<byte>(
            "SELECT compatibility_level FROM sys.databases WHERE database_id = DB_ID();");

        // The ADR requires these four to be recorded, so they are printed rather than only
        // asserted: the number is the evidence, and a later regression is only interpretable
        // against what this run actually measured.
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"rows={RowCount}; compatibility_level={compatibilityLevel}; "
            + $"index=PK_GeneratedIds fragmentation={ours:F2}%; "
            + $"index=PK_BaselineIds (NEWSEQUENTIALID baseline) fragmentation={baseline:F2}%; "
            + $"delta={ours - baseline:F2} points; budget={MaximumPointsWorseThanBaseline:F2} points");

        Assert.InRange(compatibilityLevel, (byte)160, (byte)255);
        Assert.True(
            ours - baseline <= MaximumPointsWorseThanBaseline,
            $"Sequential GUID fragmentation was {ours:F2}% against a {baseline:F2}% "
            + $"NEWSEQUENTIALID baseline — {ours - baseline:F2} points worse, over the "
            + $"{MaximumPointsWorseThanBaseline:F2}-point budget in ADR-0006.");
    }

    /// <summary>
    /// Proves the measurement has teeth: random GUIDs must blow the same budget the sequential
    /// generator passes.
    /// </summary>
    /// <remarks>
    /// Without this the control would be untrustworthy. The sequential generator and the
    /// <c>NEWSEQUENTIALID()</c> baseline measure within a rounding error of each other, which is
    /// the right answer but is also exactly what a measurement that always returned the same
    /// number would look like. Feeding the same instrument a known-bad input is the only way to
    /// tell "the generator is good" from "the test cannot detect anything".
    /// <para>
    /// This is the negative case `docs/21` requires for architecture rules, applied to a
    /// performance control for the same reason.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Insert10000RandomGuids_FragmentsFarWorseThanBaseline_ProvingTheMeasurementWorks()
    {
        var random = await MeasureAsync(
            "RandomIds",
            Enumerable.Range(0, RowCount).Select(_ => Guid.NewGuid()).ToArray());

        var baseline = await MeasureAsync("RandomBaselineIds", ids: null);

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"rows={RowCount}; index=PK_RandomIds (Guid.NewGuid, known bad) fragmentation={random:F2}%; "
            + $"index=PK_RandomBaselineIds fragmentation={baseline:F2}%; "
            + $"delta={random - baseline:F2} points");

        Assert.True(
            random - baseline > MaximumPointsWorseThanBaseline,
            $"Random GUIDs fragmented {random - baseline:F2} points worse than the baseline, "
            + $"which is inside the {MaximumPointsWorseThanBaseline:F2}-point budget. The "
            + "measurement cannot distinguish a good generator from a bad one, so the S23 "
            + "control above proves nothing.");
    }

    /// <summary>
    /// Builds a table with a clustered GUID primary key, fills it, and returns the index's
    /// average fragmentation.
    /// </summary>
    /// <param name="ids">
    /// Ids to insert, or <see langword="null"/> to let SQL Server supply
    /// <c>NEWSEQUENTIALID()</c> — the baseline the ADR compares against.
    /// </param>
    private async Task<double> MeasureAsync(string table, Guid[]? ids)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(database.MigratorConnectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(
            connection,
            $"""
             CREATE TABLE [dbo].[{table}]
             (
                 [Id] uniqueidentifier NOT NULL CONSTRAINT [DF_{table}] DEFAULT NEWSEQUENTIALID(),
                 [Payload] nvarchar(100) NOT NULL,
                 CONSTRAINT [PK_{table}] PRIMARY KEY CLUSTERED ([Id])
             );
             """);

        if (ids is null)
        {
            // Let the default fire: this is SQL Server's own sequential generator, measured
            // under identical conditions so the comparison is like for like.
            for (var row = 0; row < RowCount; row++)
            {
                await ExecuteAsync(connection, $"INSERT INTO [dbo].[{table}] ([Payload]) VALUES (N'row {row}');");
            }
        }
        else
        {
            for (var row = 0; row < ids.Length; row++)
            {
                await using var command = new SqlCommand(
                    $"INSERT INTO [dbo].[{table}] ([Id], [Payload]) VALUES (@id, @payload);", connection);
                command.Parameters.AddWithValue("@id", ids[row]);
                command.Parameters.AddWithValue("@payload", $"row {row}");
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await using var measure = new SqlCommand(
            """
            SELECT AVG(stats.avg_fragmentation_in_percent)
            FROM sys.dm_db_index_physical_stats(DB_ID(), OBJECT_ID(@table), NULL, NULL, 'DETAILED') AS stats
            WHERE stats.index_level = 0;
            """,
            connection);
        measure.Parameters.AddWithValue("@table", $"dbo.{table}");

        return Convert.ToDouble(await measure.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(database.MigratorConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);

        return (T)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}

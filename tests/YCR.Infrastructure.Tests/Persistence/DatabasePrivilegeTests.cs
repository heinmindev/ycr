using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YCR.Infrastructure.Persistence;
using YCR.TestSupport;

namespace YCR.Infrastructure.Tests.Persistence;

/// <summary>
/// S22 and spec E7: the application credential holds the <c>ycr_app</c> role and nothing more.
/// No DDL anywhere, no <c>UPDATE</c> or <c>DELETE</c> on the audit ledger, and no migration at
/// startup (ADR-0017 item 3).
/// </summary>
/// <remarks>
/// These assert <strong>absences</strong>, which is the point. A grant that is too wide is the
/// failure this role exists to prevent, and it is invisible unless a test looks for it.
/// <para>
/// Credential: <c>ycr_app</c> throughout, except where creating the situation needs the migrator.
/// </para>
/// </remarks>
[Collection(SqlServerCollection.Name)]
public sealed class DatabasePrivilegeTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private TestDatabase database = null!;

    public async ValueTask InitializeAsync() =>
        database = await fixture.Container.ProvisionDatabaseAsync("privileges", TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Theory]
    [InlineData("CREATE TABLE [network].[Smuggled] ([Id] int NOT NULL);")]
    [InlineData("ALTER TABLE [network].[Stations] ADD [Smuggled] int NULL;")]
    [InlineData("DROP TABLE [network].[Stations];")]
    [InlineData("CREATE INDEX [IX_Smuggled] ON [network].[Stations] ([NameEn]);")]
    [InlineData("CREATE SCHEMA [smuggled];")]
    public async Task ApplicationCredential_AttemptingDdl_IsDenied(string ddl)
    {
        var failure = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsApplicationAsync(ddl));

        // SQL Server words this differently per statement — "CREATE TABLE permission denied",
        // "User does not have permission to perform this action", and, where the credential
        // cannot even see the object, "Cannot find the object ... or you do not have permission".
        // The common, stable element is the word itself; asserting one exact sentence made this
        // test fail against four of its own five cases.
        Assert.Contains("permission", failure.Message, StringComparison.OrdinalIgnoreCase);

        // And the schema is genuinely untouched, which is the claim that actually matters.
        Assert.Equal(0, await ScalarAsMigratorAsync(
            """
            SELECT COUNT(*) FROM sys.objects AS o
            JOIN sys.schemas AS s ON s.schema_id = o.schema_id
            WHERE o.name IN ('Smuggled', 'IX_Smuggled') OR s.name = 'smuggled';
            """));
        Assert.Equal(6, await ScalarAsMigratorAsync(
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('network.Stations');"));
    }

    [Fact]
    public async Task ApplicationCredential_CanUpdateOnlyTheIsActiveColumnOfAStation()
    {
        await ExecuteAsApplicationAsync(
            """
            INSERT INTO [network].[Stations] ([Id], [Code], [NameEn], [NameMy], [IsActive], [CreatedAtUtc])
            VALUES (NEWID(), N'PZD', N'Pazundaung', N'ပုဇွန်တောင်', 1, SYSDATETIMEOFFSET());
            """);

        // DeactivateStation, which is the only update F-001 performs.
        await ExecuteAsApplicationAsync("UPDATE [network].[Stations] SET [IsActive] = 0;");

        // Anything else on the same table is denied, because the grant is column-scoped
        // (hein's ruling, 2026-09-20). A table-wide UPDATE would have let this through.
        var failure = await Assert.ThrowsAsync<SqlException>(() =>
            ExecuteAsApplicationAsync("UPDATE [network].[Stations] SET [NameEn] = N'Renamed';"));
        Assert.Contains("permission", failure.Message, StringComparison.OrdinalIgnoreCase);

        await Assert.ThrowsAsync<SqlException>(() =>
            ExecuteAsApplicationAsync("UPDATE [network].[Stations] SET [Code] = N'XXX';"));

        Assert.Equal(1, await ScalarAsMigratorAsync(
            "SELECT COUNT(*) FROM [network].[Stations] WHERE [NameEn] = N'Pazundaung' AND [IsActive] = 0;"));
    }

    [Fact]
    public async Task ApplicationCredential_HasNoUpdateOrDeleteOnAuditEvents()
    {
        // HAS_PERMS_BY_NAME, not an attempted UPDATE: the ledger engine also refuses updates, so
        // a failed statement would pass this test even if the grant were wrong. The permission
        // itself is what S22 is about, and it is checked directly.
        Assert.Equal(0, await PermissionAsApplicationAsync("audit.AuditEvents", "UPDATE"));
        Assert.Equal(0, await PermissionAsApplicationAsync("audit.AuditEvents", "DELETE"));
        Assert.Equal(0, await PermissionAsApplicationAsync("audit.AuditEvents", "ALTER"));
    }

    [Fact]
    public async Task ApplicationCredential_HasExactlyTheGrantsTheApplicationNeeds()
    {
        // The grants that must be present. An over-tight role would fail here rather than in
        // production, which is why every test above step 9 runs as ycr_app.
        Assert.Equal(1, await PermissionAsApplicationAsync("audit.AuditEvents", "INSERT"));
        Assert.Equal(1, await PermissionAsApplicationAsync("audit.AuditEvents", "SELECT"));
        Assert.Equal(1, await PermissionAsApplicationAsync("network.Stations", "SELECT"));
        Assert.Equal(1, await PermissionAsApplicationAsync("network.Stations", "INSERT"));
        Assert.Equal(0, await PermissionAsApplicationAsync("network.Stations", "DELETE"));

        // UPDATE is column-scoped (hein's ruling, 2026-09-20): IsActive yes, everything else no.
        Assert.Equal(1, await ColumnPermissionAsApplicationAsync("network.Stations", "UPDATE", "IsActive"));
        Assert.Equal(0, await ColumnPermissionAsApplicationAsync("network.Stations", "UPDATE", "Code"));
        Assert.Equal(0, await ColumnPermissionAsApplicationAsync("network.Stations", "UPDATE", "NameEn"));
        Assert.Equal(0, await ColumnPermissionAsApplicationAsync("network.Stations", "UPDATE", "NameMy"));
        Assert.Equal(0, await ColumnPermissionAsApplicationAsync("network.Stations", "UPDATE", "CreatedAtUtc"));
    }

    [Fact]
    public async Task ApplicationCredential_IsAMemberOfOnlyTheYcrAppRole()
    {
        var roles = await QueryStringsAsApplicationAsync(
            """
            SELECT USER_NAME(m.role_principal_id)
            FROM sys.database_role_members AS m
            WHERE m.member_principal_id = DATABASE_PRINCIPAL_ID(CURRENT_USER)
            ORDER BY 1;
            """);

        // Nothing like db_owner or db_datawriter: the role is the whole of its privilege.
        Assert.Equal([SqlServerTestContainer.ApplicationRole], roles);
    }

    [Fact]
    public async Task ApplicationCredential_CanInsertAuditRowsButNotAmendThem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await ExecuteAsApplicationAsync(
            """
            INSERT INTO [audit].[AuditEvents]
                ([Id], [OccurredAtUtc], [Action], [SubjectType], [CorrelationId], [PayloadVersion])
            VALUES (NEWID(), SYSDATETIMEOFFSET(), N'Network.StationCreated', N'Station', N'corr-priv', 1);
            """);

        var failure = await Assert.ThrowsAsync<SqlException>(() =>
            ExecuteAsApplicationAsync("UPDATE [audit].[AuditEvents] SET [ReasonCode] = N'tampered';"));

        // Two independent controls stand between an operator and a rewritten audit row: the
        // missing grant and the ledger engine. This asserts the write fails; the test above
        // proves the grant is the first of the two.
        Assert.True(failure.Number is 229 or 37359, $"Unexpected SQL error {failure.Number}: {failure.Message}");

        await using var connection = new SqlConnection(database.MigratorConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM [audit].[AuditEvents] WHERE [ReasonCode] IS NOT NULL;", connection);

        Assert.Equal(0, (int)(await command.ExecuteScalarAsync(cancellationToken))!);
    }

    /// <summary>
    /// Spec E7: the application does not migrate at startup. Resolving the whole infrastructure
    /// graph against an unmigrated database must leave it unmigrated.
    /// </summary>
    [Fact]
    public async Task ResolvingInfrastructure_AgainstAnUnmigratedDatabase_DoesNotMigrateIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var empty = await fixture.Container.CreateEmptyDatabaseAsync("no_startup_migration", cancellationToken);

        await using var provider = new ServiceCollection()
            .AddInfrastructure(empty.ApplicationConnectionString)
            .BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            // Resolving and opening a connection is everything a request would do before a
            // handler runs. None of it may create schema.
            var context = scope.ServiceProvider.GetRequiredService<YcrDbContext>();
            await context.Database.OpenConnectionAsync(cancellationToken);
            await context.Database.CloseConnectionAsync();
        }

        await using var connection = new SqlConnection(empty.MigratorConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM sys.tables WHERE name IN ('Stations', 'AuditEvents', '__EFMigrationsHistory');",
            connection);

        Assert.Equal(0, (int)(await command.ExecuteScalarAsync(cancellationToken))!);
    }

    private async Task ExecuteAsApplicationAsync(string sql)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(database.ApplicationConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// F-006A-1 / S-007A-1: the migrator connection must be the deployment's <c>ycr_migrator</c>
    /// login, not the container's <c>sa</c>.
    /// </summary>
    /// <remarks>
    /// Every other test in this class compares the application credential against the migrator
    /// one. While the migrator was really <c>sa</c>, those comparisons proved only that
    /// <c>ycr_app</c> is not a sysadmin — which was never in doubt — and the boundary E7 and
    /// ADR-0017 item 3 actually require went unverified. This test is what makes the rest of the
    /// file mean what it claims: it pins the identity on both sides of the comparison.
    /// <para>
    /// <c>db_owner</c> is asserted too, because the boundary is "owns this database, owns nothing
    /// else". A migrator that had lost <c>db_owner</c> would fail the migration tests loudly; one
    /// that had quietly gained <c>sysadmin</c> would pass everything and prove nothing.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task MigratorCredential_ConnectedToATestDatabase_IsYcrMigratorAndNotSysadmin()
    {
        Assert.Equal(
            SqlServerTestContainer.MigratorLogin,
            await ScalarAsMigratorAsync<string>("SELECT SUSER_NAME();"));

        Assert.Equal(0, await ScalarAsMigratorAsync("SELECT IS_SRVROLEMEMBER('sysadmin');"));

        // Positive half: it is genuinely the schema owner, so the migration tests around it are
        // exercising a real privilege set rather than a coincidence.
        Assert.Equal(1, await ScalarAsMigratorAsync("SELECT IS_ROLEMEMBER('db_owner');"));

        // And the application credential is a different principal on the same database, which is
        // the comparison every other test in this class depends on.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(database.ApplicationConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("SELECT SUSER_NAME();", connection);

        Assert.Equal(
            SqlServerTestContainer.ApplicationLogin,
            (string)(await command.ExecuteScalarAsync(cancellationToken))!);
    }

    private async Task<T> ScalarAsMigratorAsync<T>(string sql)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(database.MigratorConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);

        return (T)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private async Task<int> ScalarAsMigratorAsync(string sql)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(database.MigratorConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);

        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private async Task<int> ColumnPermissionAsApplicationAsync(string securable, string permission, string column)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(database.ApplicationConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "SELECT HAS_PERMS_BY_NAME(@securable, 'OBJECT', @permission, @column, 'COLUMN');", connection);
        command.Parameters.AddWithValue("@securable", securable);
        command.Parameters.AddWithValue("@permission", permission);
        command.Parameters.AddWithValue("@column", column);

        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private async Task<int> PermissionAsApplicationAsync(string securable, string permission)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(database.ApplicationConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "SELECT HAS_PERMS_BY_NAME(@securable, 'OBJECT', @permission);", connection);
        command.Parameters.AddWithValue("@securable", securable);
        command.Parameters.AddWithValue("@permission", permission);

        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private async Task<string[]> QueryStringsAsApplicationAsync(string sql)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(database.ApplicationConnectionString);
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

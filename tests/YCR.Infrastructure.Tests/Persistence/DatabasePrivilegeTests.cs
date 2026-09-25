using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Identity.Abstractions;
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
    // F-003 S25: no DDL on the route tables either.
    [InlineData("ALTER TABLE [network].[Routes] ADD [Smuggled] int NULL;")]
    [InlineData("DROP TABLE [network].[RouteStations];")]
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
            VALUES (NEWID(), N'PZD', N'Pazundaung', N'ပုဇွန်တောင်', 1, SYSUTCDATETIME() AT TIME ZONE 'UTC');
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

    /// <summary>F-002 plan P7 / <c>Security_IdentityGrants</c>: the grants the Identity module needs.</summary>
    [Fact]
    public async Task ApplicationCredential_HasExactlyTheIdentityGrants()
    {
        string[] tables = ["Users", "Roles", "RolePermissions", "UserRoles", "AuthSessions", "RefreshTokens"];
        foreach (var table in tables)
        {
            Assert.Equal(1, await PermissionAsApplicationAsync($"identity.{table}", "SELECT"));
            Assert.Equal(0, await PermissionAsApplicationAsync($"identity.{table}", "ALTER"));
            Assert.Equal(0, await PermissionAsApplicationAsync($"identity.{table}", "CONTROL"));
        }

        Assert.Equal(1, await PermissionAsApplicationAsync("identity.Users", "INSERT"));
        Assert.Equal(1, await PermissionAsApplicationAsync("identity.UserRoles", "INSERT"));
        Assert.Equal(1, await PermissionAsApplicationAsync("identity.UserRoles", "DELETE"));
        Assert.Equal(0, await PermissionAsApplicationAsync("identity.UserRoles", "UPDATE"));
        Assert.Equal(1, await PermissionAsApplicationAsync("identity.AuthSessions", "INSERT"));
        Assert.Equal(1, await PermissionAsApplicationAsync("identity.RefreshTokens", "INSERT"));
        Assert.Equal(0, await PermissionAsApplicationAsync("identity.Users", "DELETE"));

        Assert.Equal(1, await ColumnPermissionAsApplicationAsync("identity.AuthSessions", "UPDATE", "RevokedAtUtc"));
        Assert.Equal(1, await ColumnPermissionAsApplicationAsync("identity.AuthSessions", "UPDATE", "RevocationReason"));
        Assert.Equal(0, await ColumnPermissionAsApplicationAsync("identity.AuthSessions", "UPDATE", "ExpiresAtUtc"));
        Assert.Equal(0, await ColumnPermissionAsApplicationAsync("identity.AuthSessions", "UPDATE", "UserId"));
        Assert.Equal(1, await ColumnPermissionAsApplicationAsync("identity.RefreshTokens", "UPDATE", "RotatedAtUtc"));
        Assert.Equal(1, await ColumnPermissionAsApplicationAsync("identity.RefreshTokens", "UPDATE", "ReplacedByTokenId"));
        Assert.Equal(0, await ColumnPermissionAsApplicationAsync("identity.RefreshTokens", "UPDATE", "TokenHash"));
        Assert.Equal(0, await ColumnPermissionAsApplicationAsync("identity.RefreshTokens", "UPDATE", "SessionId"));
    }

    /// <summary>
    /// F-003 S25 / R10 (<c>Security_NetworkRouteGrants</c>): the presences and, which is the point,
    /// the absences. The grant set is the database-level statement that a stored sequence never
    /// changes (R9).
    /// </summary>
    [Fact]
    public async Task ApplicationCredential_HasExactlyTheRouteGrants()
    {
        Assert.Equal(1, await PermissionAsApplicationAsync("network.Routes", "SELECT"));
        Assert.Equal(1, await PermissionAsApplicationAsync("network.Routes", "INSERT"));
        Assert.Equal(1, await ColumnPermissionAsApplicationAsync("network.Routes", "UPDATE", "IsActive"));
        Assert.Equal(1, await ColumnPermissionAsApplicationAsync("network.Routes", "UPDATE", "DeactivatedAtUtc"));
        Assert.Equal(1, await PermissionAsApplicationAsync("network.RouteStations", "SELECT"));
        Assert.Equal(1, await PermissionAsApplicationAsync("network.RouteStations", "INSERT"));

        foreach (var table in new[] { "network.Routes", "network.RouteStations" })
        {
            Assert.Equal(0, await PermissionAsApplicationAsync(table, "DELETE"));
            Assert.Equal(0, await PermissionAsApplicationAsync(table, "ALTER"));
            Assert.Equal(0, await PermissionAsApplicationAsync(table, "CONTROL"));
        }

        foreach (var column in new[] { "Id", "Code", "NameEn", "NameMy", "IsClosed", "CreatedAtUtc" })
        {
            Assert.Equal(0, await ColumnPermissionAsApplicationAsync("network.Routes", "UPDATE", column));
        }

        // Table-level UPDATE on Routes would mean every column; it is column-scoped instead.
        Assert.Equal(0, await PermissionAsApplicationAsync("network.Routes", "UPDATE"));

        Assert.Equal(0, await PermissionAsApplicationAsync("network.RouteStations", "UPDATE"));
        foreach (var column in new[] { "RouteId", "Position", "StationId" })
        {
            Assert.Equal(0, await ColumnPermissionAsApplicationAsync("network.RouteStations", "UPDATE", column));
        }
    }

    /// <summary>
    /// F-003 S25 / R9, executed rather than inferred: the application can deactivate a route and
    /// can do nothing else to it or its sequence, and the rows are unchanged afterwards.
    /// </summary>
    [Fact]
    public async Task ApplicationCredential_CanDeactivateARouteButNotRewriteIt()
    {
        var stationId = Guid.NewGuid();
        var otherStationId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        await ExecuteAsApplicationAsync(
            $"""
            INSERT INTO [network].[Stations] ([Id], [Code], [NameEn], [NameMy], [IsActive], [CreatedAtUtc])
            VALUES ('{stationId}', N'RGA', N'Route Grant A', N'ဘူတာ', 1, SYSUTCDATETIME() AT TIME ZONE 'UTC'),
                   ('{otherStationId}', N'RGB', N'Route Grant B', N'ဘူတာ', 1, SYSUTCDATETIME() AT TIME ZONE 'UTC');
            INSERT INTO [network].[Routes] ([Id], [Code], [NameEn], [NameMy], [IsClosed], [IsActive], [CreatedAtUtc], [DeactivatedAtUtc])
            VALUES ('{routeId}', N'RG1', N'Route Grant', N'လမ်းကြောင်း', 0, 1, SYSUTCDATETIME() AT TIME ZONE 'UTC', NULL);
            INSERT INTO [network].[RouteStations] ([RouteId], [Position], [StationId])
            VALUES ('{routeId}', 1, '{stationId}'), ('{routeId}', 2, '{otherStationId}');
            """);

        // DeactivateRoute, which is the only update F-003 performs: both columns in one UPDATE.
        await ExecuteAsApplicationAsync(
            "UPDATE [network].[Routes] SET [IsActive] = 0, [DeactivatedAtUtc] = SYSUTCDATETIME() AT TIME ZONE 'UTC';");

        string[] denied =
        [
            "UPDATE [network].[Routes] SET [Code] = N'XX1';",
            "UPDATE [network].[Routes] SET [NameEn] = N'Renamed';",
            "UPDATE [network].[Routes] SET [IsClosed] = 1;",
            "UPDATE [network].[RouteStations] SET [Position] = [Position] + 10;",
            $"UPDATE [network].[RouteStations] SET [StationId] = '{stationId}' WHERE [Position] = 2;",
            "DELETE FROM [network].[RouteStations];",
            "DELETE FROM [network].[Routes];",
        ];
        foreach (var sql in denied)
        {
            var failure = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsApplicationAsync(sql));
            Assert.Contains("permission", failure.Message, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(1, await ScalarAsMigratorAsync(
            $"""
            SELECT COUNT(*) FROM [network].[Routes]
            WHERE [Id] = '{routeId}' AND [Code] = N'RG1' AND [NameEn] = N'Route Grant' AND [IsClosed] = 0
              AND [IsActive] = 0 AND [DeactivatedAtUtc] IS NOT NULL;
            """));
        Assert.Equal(2, await ScalarAsMigratorAsync(
            $"""
            SELECT COUNT(*) FROM [network].[RouteStations]
            WHERE ([Position] = 1 AND [StationId] = '{stationId}') OR ([Position] = 2 AND [StationId] = '{otherStationId}');
            """));
    }

    /// <summary>D18 / plan P7: F-002 deletes no session or token row.</summary>
    [Fact]
    public async Task ApplicationCredential_HasNoDeleteOnSessionsOrTokens()
    {
        Assert.Equal(0, await PermissionAsApplicationAsync("identity.AuthSessions", "DELETE"));
        Assert.Equal(0, await PermissionAsApplicationAsync("identity.RefreshTokens", "DELETE"));
    }

    /// <summary>D8: grants change by reviewed migration only.</summary>
    [Fact]
    public async Task ApplicationCredential_CannotWriteRolesOrGrants()
    {
        foreach (var table in new[] { "identity.Roles", "identity.RolePermissions" })
        {
            Assert.Equal(0, await PermissionAsApplicationAsync(table, "INSERT"));
            Assert.Equal(0, await PermissionAsApplicationAsync(table, "UPDATE"));
            Assert.Equal(0, await PermissionAsApplicationAsync(table, "DELETE"));
        }

        var failure = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsApplicationAsync(
            """
            INSERT INTO [identity].[RolePermissions] ([RoleId], [Permission])
            SELECT [Id], N'users.manage' FROM [identity].[Roles] WHERE [Name] = N'TicketOperator';
            """));
        Assert.Contains("permission", failure.Message, StringComparison.OrdinalIgnoreCase);
        // 14 F-002 grants plus the ten F-003 route grants (OQ40); unchanged by the denied INSERT.
        Assert.Equal(24, await ScalarAsMigratorAsync("SELECT COUNT(*) FROM [identity].[RolePermissions];"));
    }

    /// <summary>
    /// Plan P7: the columns that legitimately change are updatable; a user's identity columns are
    /// not (no endpoint renames a user).
    /// </summary>
    [Fact]
    public async Task ApplicationCredential_CanUpdateOnlyListedUserColumns()
    {
        string[] updatable =
        [
            "PasswordHash", "SecurityStamp", "PasswordChangedAtUtc", "MustChangePassword",
            "IsDisabled", "DisabledAtUtc", "LockoutEndUtc", "AccessFailedCount",
        ];
        foreach (var column in updatable)
        {
            Assert.Equal(1, await ColumnPermissionAsApplicationAsync("identity.Users", "UPDATE", column));
        }

        foreach (var column in new[] { "Id", "UserName", "NormalizedUserName", "CreatedAtUtc" })
        {
            Assert.Equal(0, await ColumnPermissionAsApplicationAsync("identity.Users", "UPDATE", column));
        }

        await ExecuteAsApplicationAsync(
            """
            INSERT INTO [identity].[Users]
                ([Id], [UserName], [NormalizedUserName], [PasswordHash], [SecurityStamp], [IsDisabled],
                 [AccessFailedCount], [PasswordChangedAtUtc], [MustChangePassword], [CreatedAtUtc])
            VALUES (NEWID(), N'grant.check', N'GRANT.CHECK', N'placeholder-hash', N'stamp', 0,
                    0, SYSUTCDATETIME() AT TIME ZONE 'UTC', 1, SYSUTCDATETIME() AT TIME ZONE 'UTC');
            """);
        await ExecuteAsApplicationAsync("UPDATE [identity].[Users] SET [AccessFailedCount] = 1;");

        var failure = await Assert.ThrowsAsync<SqlException>(() =>
            ExecuteAsApplicationAsync("UPDATE [identity].[Users] SET [UserName] = N'renamed';"));
        Assert.Contains("permission", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// V5 / plan P14: <c>ycr_app</c> can take the R27 administrator lock with no extra grant, and
    /// the lock is exclusive — a second transaction waits until the first ends.
    /// </summary>
    [Fact]
    public async Task ApplicationCredential_CanTakeTheAdministratorApplock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(database.ApplicationConnectionString)
            .BuildServiceProvider();
        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<YcrDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<YcrDbContext>();
        var firstLock = firstScope.ServiceProvider.GetRequiredService<IIdentityAdministratorLock>();
        var secondLock = secondScope.ServiceProvider.GetRequiredService<IIdentityAdministratorLock>();

        // Without a transaction the lock refuses to run unowned.
        await Assert.ThrowsAsync<InvalidOperationException>(() => firstLock.AcquireAsync(cancellationToken));

        await using var firstTransaction = await firstContext.Database.BeginTransactionAsync(cancellationToken);
        await firstLock.AcquireAsync(cancellationToken);

        await using var secondTransaction = await secondContext.Database.BeginTransactionAsync(cancellationToken);
        var waiting = secondLock.AcquireAsync(cancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken);
        Assert.False(waiting.IsCompleted, "The second transaction acquired the lock while the first still held it.");

        await firstTransaction.CommitAsync(cancellationToken);
        await waiting.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        await secondTransaction.CommitAsync(cancellationToken);
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
            VALUES (NEWID(), SYSUTCDATETIME() AT TIME ZONE 'UTC', N'Network.StationCreated', N'Station', N'corr-priv', 1);
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

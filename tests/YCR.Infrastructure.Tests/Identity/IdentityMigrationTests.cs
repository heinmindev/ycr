using Microsoft.EntityFrameworkCore;
using YCR.Application.Identity;
using YCR.Domain.Identity;
using YCR.Infrastructure.Persistence.Configurations.Identity;
using YCR.TestSupport;

namespace YCR.Infrastructure.Tests.Identity;

/// <summary>
/// F-002 spec §7 and plan §DB changes: the three Identity migrations, applied on top of a database
/// already at F-001's last migration (<c>docs/21</c> §Data: the upgrade path is tested).
/// </summary>
/// <remarks>Credential: <c>ycr_migrator</c>, which is what applies migrations in every environment.</remarks>
[Collection(SqlServerCollection.Name)]
public sealed class IdentityMigrationTests(SqlServerFixture fixture)
{
    private const string LastF001Migration = "20260920135245_Security_AppDatabaseRole";

    [Fact]
    public async Task Migrate_FromF001Schema_CreatesIdentityTablesConstraintsAndIndexes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var database = await fixture.Container.CreateEmptyDatabaseAsync("identity_upgrade", cancellationToken);

        // A database exactly as F-001 left it, with data in it.
        await using (var context = IdentitySql.Context(database.MigratorConnectionString))
        {
            await context.Database.MigrateAsync(LastF001Migration, cancellationToken);
        }

        await IdentitySql.ExecuteAsync(
            database.MigratorConnectionString,
            """
            INSERT INTO [network].[Stations] ([Id], [Code], [NameEn], [NameMy], [IsActive], [CreatedAtUtc])
            VALUES (NEWID(), N'YGN', N'Yangon', N'ရန်ကုန်', 1, SYSUTCDATETIME() AT TIME ZONE 'UTC');
            """);

        await using (var context = IdentitySql.Context(database.MigratorConnectionString))
        {
            await context.Database.MigrateAsync(cancellationToken);
            Assert.Empty(await context.Database.GetPendingMigrationsAsync(cancellationToken));
        }

        // F-001's data survives the upgrade untouched.
        Assert.Equal(1, await IdentitySql.ScalarAsync<int>(
            database.MigratorConnectionString,
            "SELECT COUNT(*) FROM [network].[Stations] WHERE [Code] = N'YGN';"));

        var tables = await IdentitySql.StringsAsync(
            database.MigratorConnectionString,
            "SELECT t.name FROM sys.tables AS t WHERE t.schema_id = SCHEMA_ID(N'identity')");
        Assert.Equal(
            ["AuthSessions", "RefreshTokens", "RolePermissions", "Roles", "UserRoles", "Users"],
            tables.Order(StringComparer.Ordinal));

        var checks = await IdentitySql.StringsAsync(
            database.MigratorConnectionString,
            "SELECT c.name FROM sys.check_constraints AS c WHERE c.schema_id = SCHEMA_ID(N'identity')");
        Assert.Equal(
            [
                "CK_AuthSessions_CreatedAtUtc_Utc",
                "CK_AuthSessions_ExpiresAtUtc_Utc",
                "CK_AuthSessions_Expiry",
                "CK_AuthSessions_RevocationReason",
                "CK_AuthSessions_Revocation_Consistent",
                "CK_AuthSessions_RevokedAtUtc_Utc",
                "CK_RefreshTokens_IssuedAtUtc_Utc",
                "CK_RefreshTokens_NotSelf",
                "CK_RefreshTokens_RotatedAtUtc_Utc",
                "CK_RefreshTokens_Rotation_Consistent",
                "CK_RolePermissions_Permission_Format",
                "CK_Users_AccessFailedCount",
                "CK_Users_CreatedAtUtc_Utc",
                "CK_Users_DisabledAtUtc_Utc",
                "CK_Users_Disabled_Consistent",
                "CK_Users_LockoutEndUtc_Utc",
                "CK_Users_PasswordChangedAtUtc_Utc",
                "CK_Users_UserName_Format",
            ],
            checks.Order(StringComparer.Ordinal));

        var indexes = await IdentitySql.PairsAsync(
            database.MigratorConnectionString,
            """
            SELECT i.name, CONCAT(i.is_unique, ':', ISNULL(i.filter_definition, N''))
            FROM sys.indexes AS i
            JOIN sys.tables AS t ON t.object_id = i.object_id
            WHERE t.schema_id = SCHEMA_ID(N'identity') AND i.is_primary_key = 0 AND i.name IS NOT NULL
            """);
        Assert.Equal(
            [
                ("IX_AuthSessions_UserId", "0:"),
                ("IX_RefreshTokens_SessionId", "0:"),
                ("IX_UserRoles_RoleId", "0:"),
                ("UX_RefreshTokens_ReplacedByTokenId", "1:([ReplacedByTokenId] IS NOT NULL)"),
                ("UX_RefreshTokens_TokenHash", "1:"),
                ("UX_Roles_Name", "1:"),
                ("UX_Users_NormalizedUserName", "1:"),
            ],
            indexes.OrderBy(index => index.First, StringComparer.Ordinal));

        // Every foreign key is NO ACTION: nothing in identity cascades (plan §DB changes).
        Assert.Equal(0, await IdentitySql.ScalarAsync<int>(
            database.MigratorConnectionString,
            """
            SELECT COUNT(*) FROM sys.foreign_keys
            WHERE schema_id = SCHEMA_ID(N'identity')
              AND (delete_referential_action <> 0 OR update_referential_action <> 0);
            """));
        Assert.Equal(6, await IdentitySql.ScalarAsync<int>(
            database.MigratorConnectionString,
            "SELECT COUNT(*) FROM sys.foreign_keys WHERE schema_id = SCHEMA_ID(N'identity');"));

        // Every *Utc column is datetimeoffset(3) (docs/20 §2).
        Assert.Equal(0, await IdentitySql.ScalarAsync<int>(
            database.MigratorConnectionString,
            """
            SELECT COUNT(*) FROM sys.columns AS c
            JOIN sys.tables AS t ON t.object_id = c.object_id
            WHERE t.schema_id = SCHEMA_ID(N'identity') AND c.name LIKE N'%Utc'
              AND (TYPE_NAME(c.user_type_id) <> N'datetimeoffset' OR c.scale <> 3);
            """));
    }

    [Fact]
    public void Model_IdentityConstraintNames_MatchTheApplicationConstants()
    {
        using var context = IdentitySql.Context("Server=unused.invalid;Database=unused");

        var users = context.Model.FindEntityType(typeof(StaffUser))!;
        var userName = Assert.Single(users.GetIndexes(), index => index.IsUnique);
        Assert.Equal(IdentityConstraints.UserNameUniqueIndex, userName.GetDatabaseName());
        Assert.True(users.FindProperty(StaffUserConfiguration.RowVersion)!.IsConcurrencyToken);

        var tokens = context.Model.FindEntityType(typeof(RefreshToken))!;
        Assert.True(tokens.FindProperty(nameof(RefreshToken.ReplacedByTokenId))!.IsConcurrencyToken);
        Assert.Contains(tokens.GetIndexes(), index => index.GetDatabaseName() == IdentityConstraints.RefreshTokenSuccessorUniqueIndex);
        Assert.Contains(tokens.GetIndexes(), index => index.GetDatabaseName() == IdentityConstraints.RefreshTokenHashUniqueIndex);

        var sessions = context.Model.FindEntityType(typeof(AuthSession))!;
        Assert.True(sessions.FindProperty(nameof(AuthSession.RevokedAtUtc))!.IsConcurrencyToken);
    }
}

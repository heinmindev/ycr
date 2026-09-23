using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using YCR.Domain.Identity;
using YCR.TestSupport;

namespace YCR.Infrastructure.Tests.Identity;

/// <summary>
/// The database half of the Identity rules: the username format (S32a), UTC offsets (ADR-0018),
/// half-set revocation and rotation rows, at most one successor per refresh token (S33, plan P15,
/// V4), and EF updates staying inside <c>ycr_app</c>'s column grants (V7).
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class IdentityConstraintTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 3, 0, 0, TimeSpan.Zero);

    private TestDatabase database = null!;

    public async ValueTask InitializeAsync() =>
        database = await fixture.Container.ProvisionDatabaseAsync("identity_constraints", TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Theory]
    [InlineData("ab")]
    [InlineData("Hein")]
    [InlineData("hein min")]
    [InlineData("hein-min")]
    [InlineData("hein_min")]
    [InlineData("heiné")]
    [InlineData("hein၁")]
    [InlineData("123456789012345678901234567890123456789012345678901")]
    public async Task UserNameConstraint_RejectsInvalidNames(string userName)
    {
        var failure = await Assert.ThrowsAsync<SqlException>(() =>
            IdentitySql.ExecuteAsync(database.MigratorConnectionString, IdentitySql.InsertUserSql(Guid.NewGuid(), userName)));

        // 547 = constraint violation; 8152/2628 = truncation for the 51-character case.
        Assert.True(failure.Number is 547 or 8152 or 2628, $"Unexpected SQL error {failure.Number}: {failure.Message}");
        Assert.Equal(0, await IdentitySql.ScalarAsync<int>(database.MigratorConnectionString, "SELECT COUNT(*) FROM [identity].[Users];"));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("hein.min")]
    [InlineData("12345678901234567890123456789012345678901234567890")]
    public async Task UserNameConstraint_AcceptsValidNames(string userName)
    {
        await IdentitySql.ExecuteAsync(database.MigratorConnectionString, IdentitySql.InsertUserSql(Guid.NewGuid(), userName));
    }

    [Theory]
    [InlineData("Stations.read", false)]
    [InlineData("stations.read1", false)]
    [InlineData("stations_read", false)]
    [InlineData("stations read", false)]
    [InlineData("auth-sessions.revoke", true)]
    [InlineData("users.roles.manage", true)]
    public async Task PermissionConstraint_AcceptsOnlyLowerCaseLettersDotsAndHyphens(string permission, bool accepted)
    {
        var insert = $"INSERT INTO [identity].[RolePermissions] ([RoleId], [Permission]) VALUES ('{IdentitySql.SystemAdministratorRoleId}', N'{permission}x');";

        if (accepted)
        {
            await IdentitySql.ExecuteAsync(database.MigratorConnectionString, insert);
            return;
        }

        var failure = await Assert.ThrowsAsync<SqlException>(() => IdentitySql.ExecuteAsync(database.MigratorConnectionString, insert));
        Assert.Equal(547, failure.Number);
    }

    [Theory]
    [InlineData("[identity].[Users]", "[CreatedAtUtc]")]
    [InlineData("[identity].[Users]", "[PasswordChangedAtUtc]")]
    [InlineData("[identity].[Users]", "[LockoutEndUtc]")]
    [InlineData("[identity].[AuthSessions]", "[CreatedAtUtc]")]
    [InlineData("[identity].[RefreshTokens]", "[IssuedAtUtc]")]
    public async Task UtcConstraints_RejectNonUtcOffsets(string table, string column)
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        await IdentitySql.ExecuteAsync(database.MigratorConnectionString, IdentitySql.InsertUserSql(userId, $"u{userId:N}"[..20]));
        await IdentitySql.ExecuteAsync(database.MigratorConnectionString, IdentitySql.InsertSessionSql(sessionId, userId));
        await IdentitySql.ExecuteAsync(database.MigratorConnectionString, IdentitySql.InsertTokenSql(tokenId, sessionId, (byte)Random.Shared.Next(256)));

        var id = table.Contains("Users", StringComparison.Ordinal) ? userId
            : table.Contains("AuthSessions", StringComparison.Ordinal) ? sessionId
            : tokenId;

        var failure = await Assert.ThrowsAsync<SqlException>(() => IdentitySql.ExecuteAsync(
            database.MigratorConnectionString,
            $"UPDATE {table} SET {column} = TODATETIMEOFFSET(SYSUTCDATETIME(), '+06:30') WHERE [Id] = '{id}';"));

        Assert.Equal(547, failure.Number);
    }

    [Theory]
    [InlineData("UPDATE [identity].[AuthSessions] SET [RevokedAtUtc] = SYSUTCDATETIME() AT TIME ZONE 'UTC'")]
    [InlineData("UPDATE [identity].[AuthSessions] SET [RevocationReason] = N'Logout'")]
    [InlineData("UPDATE [identity].[AuthSessions] SET [RevokedAtUtc] = SYSUTCDATETIME() AT TIME ZONE 'UTC', [RevocationReason] = N'Stolen'")]
    [InlineData("UPDATE [identity].[AuthSessions] SET [ExpiresAtUtc] = [CreatedAtUtc]")]
    [InlineData("UPDATE [identity].[RefreshTokens] SET [RotatedAtUtc] = SYSUTCDATETIME() AT TIME ZONE 'UTC'")]
    [InlineData("UPDATE [identity].[RefreshTokens] SET [ReplacedByTokenId] = [Id], [RotatedAtUtc] = SYSUTCDATETIME() AT TIME ZONE 'UTC'")]
    [InlineData("UPDATE [identity].[Users] SET [IsDisabled] = 1")]
    [InlineData("UPDATE [identity].[Users] SET [AccessFailedCount] = 11")]
    public async Task RevocationConstraints_RejectHalfSetRows(string update)
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        await IdentitySql.ExecuteAsync(database.MigratorConnectionString, IdentitySql.InsertUserSql(userId, $"u{userId:N}"[..20]));
        await IdentitySql.ExecuteAsync(database.MigratorConnectionString, IdentitySql.InsertSessionSql(sessionId, userId));
        await IdentitySql.ExecuteAsync(database.MigratorConnectionString, IdentitySql.InsertTokenSql(Guid.NewGuid(), sessionId, (byte)Random.Shared.Next(256)));

        var failure = await Assert.ThrowsAsync<SqlException>(() => IdentitySql.ExecuteAsync(
            database.MigratorConnectionString,
            $"{update} WHERE {(update.Contains("[Users]", StringComparison.Ordinal) ? $"[Id] = '{userId}'" : update.Contains("[AuthSessions]", StringComparison.Ordinal) ? $"[Id] = '{sessionId}'" : $"[SessionId] = '{sessionId}'")};"));

        Assert.Equal(547, failure.Number);
    }

    /// <summary>S33 in SQL: the rotation UPDATE can succeed once only.</summary>
    [Fact]
    public async Task RefreshTokens_SecondRotationOfSameToken_AffectsNoRow()
    {
        var (sessionId, first) = await SeedSessionAsync();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        await IdentitySql.ExecuteAsync(database.ApplicationConnectionString, IdentitySql.InsertTokenSql(second, sessionId, 0xB2));
        await IdentitySql.ExecuteAsync(database.ApplicationConnectionString, IdentitySql.InsertTokenSql(third, sessionId, 0xB3));

        var rotate = $"UPDATE [identity].[RefreshTokens] SET [RotatedAtUtc] = SYSUTCDATETIME() AT TIME ZONE 'UTC', [ReplacedByTokenId] = '{{0}}' WHERE [Id] = '{first}' AND [ReplacedByTokenId] IS NULL;";

        Assert.Equal(1, await IdentitySql.ExecuteCountAsync(database.ApplicationConnectionString, string.Format(System.Globalization.CultureInfo.InvariantCulture, rotate, second)));
        Assert.Equal(0, await IdentitySql.ExecuteCountAsync(database.ApplicationConnectionString, string.Format(System.Globalization.CultureInfo.InvariantCulture, rotate, third)));
        Assert.Equal(second, await IdentitySql.ScalarAsync<Guid>(
            database.ApplicationConnectionString, $"SELECT [ReplacedByTokenId] FROM [identity].[RefreshTokens] WHERE [Id] = '{first}';"));
    }

    /// <summary>S33: even a statement that skips the IS NULL guard cannot give one successor to two tokens.</summary>
    [Fact]
    public async Task RefreshTokens_SameSuccessorTwice_IsRejectedByUniqueIndex()
    {
        var (sessionId, first) = await SeedSessionAsync();
        var other = Guid.NewGuid();
        var successor = Guid.NewGuid();
        await IdentitySql.ExecuteAsync(database.ApplicationConnectionString, IdentitySql.InsertTokenSql(other, sessionId, 0xC2));
        await IdentitySql.ExecuteAsync(database.ApplicationConnectionString, IdentitySql.InsertTokenSql(successor, sessionId, 0xC3));
        await IdentitySql.ExecuteAsync(
            database.ApplicationConnectionString,
            $"UPDATE [identity].[RefreshTokens] SET [RotatedAtUtc] = SYSUTCDATETIME() AT TIME ZONE 'UTC', [ReplacedByTokenId] = '{successor}' WHERE [Id] = '{first}';");

        var failure = await Assert.ThrowsAsync<SqlException>(() => IdentitySql.ExecuteAsync(
            database.ApplicationConnectionString,
            $"UPDATE [identity].[RefreshTokens] SET [RotatedAtUtc] = SYSUTCDATETIME() AT TIME ZONE 'UTC', [ReplacedByTokenId] = '{successor}' WHERE [Id] = '{other}';"));

        Assert.Equal(2601, failure.Number);
        Assert.Contains("UX_RefreshTokens_ReplacedByTokenId", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// V4 / plan P15: two contexts rotate the same current token; EF's concurrency token makes the
    /// loser's UPDATE affect no row, so it throws and its successor INSERT rolls back with it.
    /// </summary>
    [Fact]
    public async Task RefreshTokens_ParallelRotationThroughEfCore_LoserThrowsAndLeavesNoSecondSuccessor()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (sessionId, _) = await SeedSessionAsync(hashSeed: 0xD1);
        var presented = Enumerable.Repeat((byte)0xD1, 32).ToArray();

        await using var winnerContext = IdentitySql.Context(database.ApplicationConnectionString);
        await using var loserContext = IdentitySql.Context(database.ApplicationConnectionString);
        var winner = await winnerContext.AuthSessions.Include(session => session.Tokens).SingleAsync(session => session.Id == sessionId, cancellationToken);
        var loser = await loserContext.AuthSessions.Include(session => session.Tokens).SingleAsync(session => session.Id == sessionId, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        Assert.Equal(RefreshOutcome.Rotated, winner.Rotate(presented, Guid.NewGuid(), Hash(0xD2), now, TimeSpan.FromSeconds(20)));
        Assert.Equal(RefreshOutcome.Rotated, loser.Rotate(presented, Guid.NewGuid(), Hash(0xD3), now, TimeSpan.FromSeconds(20)));

        await winnerContext.SaveChangesAsync(cancellationToken);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => loserContext.SaveChangesAsync(cancellationToken));

        Assert.Equal(2, await IdentitySql.ScalarAsync<int>(
            database.MigratorConnectionString, $"SELECT COUNT(*) FROM [identity].[RefreshTokens] WHERE [SessionId] = '{sessionId}';"));
        Assert.Equal(1, await IdentitySql.ScalarAsync<int>(
            database.MigratorConnectionString, $"SELECT COUNT(*) FROM [identity].[RefreshTokens] WHERE [SessionId] = '{sessionId}' AND [ReplacedByTokenId] IS NOT NULL;"));
    }

    /// <summary>Plan P15: two revocations racing produce one revocation.</summary>
    [Fact]
    public async Task AuthSessions_ParallelRevocationThroughEfCore_LoserThrows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (sessionId, _) = await SeedSessionAsync(hashSeed: 0xE1);

        await using var first = IdentitySql.Context(database.ApplicationConnectionString);
        await using var second = IdentitySql.Context(database.ApplicationConnectionString);
        var a = await first.AuthSessions.SingleAsync(session => session.Id == sessionId, cancellationToken);
        var b = await second.AuthSessions.SingleAsync(session => session.Id == sessionId, cancellationToken);
        Assert.True(a.Revoke(RevocationReason.Logout, DateTimeOffset.UtcNow));
        Assert.True(b.Revoke(RevocationReason.FamilyReuse, DateTimeOffset.UtcNow));

        await first.SaveChangesAsync(cancellationToken);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(cancellationToken));

        Assert.Equal("Logout", await IdentitySql.ScalarAsync<string>(
            database.MigratorConnectionString, $"SELECT [RevocationReason] FROM [identity].[AuthSessions] WHERE [Id] = '{sessionId}';"));
    }

    /// <summary>
    /// V7: every update the Identity aggregates make through EF stays inside <c>ycr_app</c>'s
    /// column grants — EF sets only the modified columns, and the <c>rowversion</c> is the
    /// server's. Role replacement deletes and inserts <c>UserRoles</c> rows.
    /// </summary>
    [Fact]
    public async Task Users_UpdatedThroughEfCore_StayWithinTheColumnGrants()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        await IdentitySql.ExecuteAsync(database.ApplicationConnectionString, IdentitySql.InsertUserSql(userId, "grant.probe"));
        var administratorRole = Guid.Parse(IdentitySql.SystemAdministratorRoleId);
        var otherRole = await IdentitySql.ScalarAsync<Guid>(
            database.ApplicationConnectionString, "SELECT [Id] FROM [identity].[Roles] WHERE [Name] = N'Auditor';");

        await using (var context = IdentitySql.Context(database.ApplicationConnectionString))
        {
            var user = await context.Users.Include(u => u.Roles).SingleAsync(u => u.Id == userId, cancellationToken);
            for (var attempt = 0; attempt < AccountLockoutPolicy.MaxFailedAttempts; attempt++)
            {
                user.RecordFailedSignIn(Now);
            }

            Assert.True(user.Disable(Guid.NewGuid(), Now).Value);
            user.ChangeOwnPassword(Now);
            Assert.True(user.ReplaceRoles([administratorRole, otherRole], Guid.NewGuid()).IsSuccess);
            await context.SaveChangesAsync(cancellationToken);
        }

        await using (var context = IdentitySql.Context(database.ApplicationConnectionString))
        {
            var user = await context.Users.Include(u => u.Roles).SingleAsync(u => u.Id == userId, cancellationToken);
            Assert.True(user.Unlock(Now));
            Assert.True(user.Enable());
            Assert.True(user.ReplaceRoles([otherRole], Guid.NewGuid()).IsSuccess);
            await context.SaveChangesAsync(cancellationToken);
        }

        await using (var context = IdentitySql.Context(database.ApplicationConnectionString))
        {
            var user = await context.Users.Include(u => u.Roles).AsNoTracking().SingleAsync(u => u.Id == userId, cancellationToken);
            Assert.Equal(otherRole, Assert.Single(user.Roles).RoleId);
            Assert.False(user.IsDisabled);
            Assert.Null(user.LockoutEndUtc);
            Assert.False(user.MustChangePassword);
        }
    }

    private async Task<(Guid SessionId, Guid FirstTokenId)> SeedSessionAsync(byte hashSeed = 0xB1)
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        await IdentitySql.ExecuteAsync(database.ApplicationConnectionString, IdentitySql.InsertUserSql(userId, $"u{userId:N}"[..20]));
        await IdentitySql.ExecuteAsync(database.ApplicationConnectionString, IdentitySql.InsertSessionSql(sessionId, userId));
        await IdentitySql.ExecuteAsync(database.ApplicationConnectionString, IdentitySql.InsertTokenSql(tokenId, sessionId, hashSeed));
        return (sessionId, tokenId);
    }

    private static byte[] Hash(byte seed) => Enumerable.Repeat(seed, 32).ToArray();
}

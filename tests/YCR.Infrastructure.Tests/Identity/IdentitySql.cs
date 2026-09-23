using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using YCR.Infrastructure.Persistence;

namespace YCR.Infrastructure.Tests.Identity;

/// <summary>Raw-SQL helpers for the <c>identity</c> schema tests, under whichever credential a test names.</summary>
internal static class IdentitySql
{
    /// <summary>The seeded <c>SystemAdministrator</c> role id (Identity_SeedRolesAndPermissionGrants).</summary>
    public const string SystemAdministratorRoleId = "0199b3a0-0000-7000-8000-000000000001";

    public static async Task ExecuteAsync(string connectionString, string sql)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<int> ExecuteCountAsync(string connectionString, string sql)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public static async Task<List<(string First, string Second)>> PairsAsync(string connectionString, string sql)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<(string, string)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add((reader.GetString(0), reader.GetString(1)));
        }

        return rows;
    }

    public static async Task<List<string>> StringsAsync(string connectionString, string sql) =>
        (await PairsAsync(connectionString, $"SELECT x, N'' FROM ({sql}) AS q(x);")).Select(row => row.First).ToList();

    /// <summary>A valid user row; the password hash is an opaque placeholder, never a real hash.</summary>
    public static string InsertUserSql(Guid id, string userName) =>
        $"""
         INSERT INTO [identity].[Users]
             ([Id], [UserName], [NormalizedUserName], [PasswordHash], [SecurityStamp], [IsDisabled],
              [AccessFailedCount], [PasswordChangedAtUtc], [MustChangePassword], [CreatedAtUtc])
         VALUES ('{id}', N'{userName}', UPPER(N'{userName}'), N'placeholder-hash', N'stamp', 0,
                 0, SYSUTCDATETIME() AT TIME ZONE 'UTC', 1, SYSUTCDATETIME() AT TIME ZONE 'UTC');
         """;

    public static string InsertSessionSql(Guid id, Guid userId) =>
        $"""
         INSERT INTO [identity].[AuthSessions] ([Id], [UserId], [CreatedAtUtc], [ExpiresAtUtc])
         VALUES ('{id}', '{userId}', SYSUTCDATETIME() AT TIME ZONE 'UTC', DATEADD(HOUR, 12, SYSUTCDATETIME() AT TIME ZONE 'UTC'));
         """;

    public static string InsertTokenSql(Guid id, Guid sessionId, byte seed) =>
        $"""
         INSERT INTO [identity].[RefreshTokens] ([Id], [SessionId], [TokenHash], [IssuedAtUtc])
         VALUES ('{id}', '{sessionId}', 0x{Convert.ToHexString(Enumerable.Repeat(seed, 32).ToArray())}, SYSUTCDATETIME() AT TIME ZONE 'UTC');
         """;

    public static YcrDbContext Context(string connectionString) =>
        new(new DbContextOptionsBuilder<YcrDbContext>().UseSqlServer(connectionString).Options);
}

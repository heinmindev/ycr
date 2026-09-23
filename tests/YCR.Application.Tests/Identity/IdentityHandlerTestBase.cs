using Microsoft.AspNetCore.Identity;
using SignInResult = YCR.Application.Identity.Login.SignInResult;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using YCR.Application.Common.Abstractions;
using YCR.Application.Identity;
using YCR.Application.Identity.Abstractions;
using YCR.Application.Identity.Login;
using YCR.Application.Tests.Network;
using YCR.Domain.Common;
using YCR.Domain.Identity;
using YCR.Infrastructure;
using YCR.TestSupport;

namespace YCR.Application.Tests.Identity;

/// <summary>
/// Shared setup for the Identity handler suites: a migrated database per test, the real
/// composition (<c>AddInfrastructure</c> + <c>AddApplication</c>) under <c>ycr_app</c>, a
/// <see cref="TestClock"/>, and a fake access-token issuer (the real one is the API's, step 7).
/// </summary>
[Collection(SqlServerCollection.Name)]
public abstract class IdentityHandlerTestBase(SqlServerFixture fixture) : IAsyncLifetime
{
    public const string Password = "kyauk.tan.12";

    protected TestDatabase Database { get; private set; } = null!;

    protected TestClock Clock { get; } = new();

    protected CountingPasswordHasher Hasher { get; } = new();

    protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    protected abstract string DatabasePrefix { get; }

    public async ValueTask InitializeAsync() =>
        Database = await fixture.Container.ProvisionDatabaseAsync(DatabasePrefix, CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected ServiceProvider BuildProvider(ICurrentUser? currentUser = null) =>
        new ServiceCollection()
            .AddLogging()
            .AddSingleton<TimeProvider>(Clock)
            .AddInfrastructure(Database.ApplicationConnectionString)
            .AddApplication()
            .Replace(ServiceDescriptor.Scoped<IPasswordHasher<StaffUser>>(_ => Hasher))
            .AddSingleton<IAccessTokenIssuer>(new FakeAccessTokenIssuer(Clock))
            .AddScoped(_ => currentUser ?? TestCurrentUser.Anonymous)
            .BuildServiceProvider();

    /// <summary>
    /// Creates a user directly through the domain and the password service (the administration
    /// handlers arrive in step 6). By default the must-change flag is cleared, as spec §4 assumes.
    /// </summary>
    protected async Task<Guid> SeedUserAsync(
        ServiceProvider provider,
        string userName,
        string[] roles,
        bool mustChangePassword = false,
        string password = Password)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IIdentityDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        var user = StaffUser.Create(Guid.CreateVersion7(), UserName.Create(userName).Value, Clock.GetUtcNow());
        var roleIds = await db.Roles.Where(role => roles.Contains(role.Name)).Select(role => role.Id).ToListAsync(CancellationToken);
        Assert.Equal(roles.Length, roleIds.Count);
        Assert.True(user.ReplaceRoles(roleIds, callerId: null).IsSuccess);
        Assert.True(await passwords.TrySetPasswordAsync(user, password, CancellationToken));
        if (!mustChangePassword)
        {
            user.ChangeOwnPassword(Clock.GetUtcNow());
        }

        db.Users.Add(user);
        await db.SaveChangesAsync(CancellationToken);
        Hasher.Reset();
        return user.Id;
    }

    protected async Task<Result<SignInResult>> LoginAsync(ServiceProvider provider, string userName, string password = Password)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<LoginHandler>()
            .Handle(new LoginCommand(userName, password), CancellationToken);
    }

    protected async Task<T> WithScopeAsync<T>(ServiceProvider provider, Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = provider.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    protected async Task ExecuteAsMigratorAsync(string sql)
    {
        await using var connection = new SqlConnection(Database.MigratorConnectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(CancellationToken);
    }

    protected async Task<T?> ScalarAsync<T>(string sql)
    {
        await using var connection = new SqlConnection(Database.MigratorConnectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = new SqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync(CancellationToken);
        return value is null or DBNull ? default : (T)value;
    }

    /// <summary>Every ledger row for <paramref name="action"/>, oldest first.</summary>
    protected async Task<List<AuditRow>> AuditRowsAsync(string action)
    {
        await using var connection = new SqlConnection(Database.MigratorConnectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = new SqlCommand(
            """
            SELECT [ActorUserId], [ActorRole], [SubjectType], [SubjectId], [BeforeJson], [AfterJson], [AuthorizedByPermission]
            FROM [audit].[AuditEvents] WHERE [Action] = @action ORDER BY [OccurredAtUtc], [Id];
            """,
            connection);
        command.Parameters.AddWithValue("@action", action);
        await using var reader = await command.ExecuteReaderAsync(CancellationToken);
        var rows = new List<AuditRow>();
        while (await reader.ReadAsync(CancellationToken))
        {
            rows.Add(new AuditRow(
                reader.IsDBNull(0) ? null : reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetGuid(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return rows;
    }

    protected Task<int> AuditCountAsync() =>
        ScalarAsync<int>("SELECT COUNT(*) FROM [audit].[AuditEvents];")!;

    protected async Task<(int Count, DateTimeOffset? LockoutEnd)> LockoutStateAsync(Guid userId)
    {
        await using var connection = new SqlConnection(Database.MigratorConnectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = new SqlCommand(
            $"SELECT [AccessFailedCount], [LockoutEndUtc] FROM [identity].[Users] WHERE [Id] = '{userId}';", connection);
        await using var reader = await command.ExecuteReaderAsync(CancellationToken);
        Assert.True(await reader.ReadAsync(CancellationToken));
        return (reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetDateTimeOffset(1));
    }

    public sealed record AuditRow(
        Guid? ActorUserId,
        string? ActorRole,
        string SubjectType,
        Guid? SubjectId,
        string? BeforeJson,
        string? AfterJson,
        string? AuthorizedByPermission);
}

/// <summary>Issues a recognisable fake token; the real ES256 issuer is the API's (plan step 7).</summary>
public sealed class FakeAccessTokenIssuer(TimeProvider clock) : IAccessTokenIssuer
{
    public AccessToken Issue(Guid userId, Guid sessionId) =>
        new($"fake-access.{userId:N}.{sessionId:N}", clock.GetUtcNow().AddMinutes(15));

    public static (Guid UserId, Guid SessionId) Parse(string token)
    {
        var parts = token.Split('.');
        return (Guid.ParseExact(parts[1], "N"), Guid.ParseExact(parts[2], "N"));
    }
}

/// <summary>The real hasher, counting verifications (R23: asserted by counting, not by timing).</summary>
public sealed class CountingPasswordHasher : IPasswordHasher<StaffUser>
{
    private readonly PasswordHasher<StaffUser> inner = new();
    private int verifications;

    public int Verifications => Volatile.Read(ref verifications);

    public string HashPassword(StaffUser user, string password) => inner.HashPassword(user, password);

    public PasswordVerificationResult VerifyHashedPassword(StaffUser user, string hashedPassword, string providedPassword)
    {
        Interlocked.Increment(ref verifications);
        return inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
    }

    public void Reset() => Interlocked.Exchange(ref verifications, 0);
}

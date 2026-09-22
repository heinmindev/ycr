using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Common.Abstractions;
using YCR.Infrastructure;
using YCR.TestSupport;

namespace YCR.Application.Tests.Network;

/// <summary>
/// Shared setup for the Network handler suites: a migrated database per test class, and a service
/// provider wired exactly as the API will wire it.
/// </summary>
/// <remarks>
/// <strong>Credential: <c>ycr_app</c>.</strong> Handler tests run as the identity the platform
/// actually runs under (plan §Test fixture), so a missing grant fails here rather than in
/// production. The migrator credential is used only to read the audit ledger back in assertions
/// where a test needs to see rows the application itself is not meant to query.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public abstract class NetworkHandlerTestBase(SqlServerFixture fixture) : IAsyncLifetime
{
    protected TestDatabase Database { get; private set; } = null!;

    protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() =>
        Database = await fixture.Container.ProvisionDatabaseAsync(DatabasePrefix, CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>Each suite gets its own database, so suites cannot see each other's rows.</summary>
    protected abstract string DatabasePrefix { get; }

    /// <summary>
    /// The real composition: <c>AddInfrastructure</c> plus <c>AddApplication</c>, with only the
    /// authenticated context stubbed. Handlers are resolved from it rather than constructed by
    /// hand, so a missing DI registration fails a test instead of only failing in the API.
    /// </summary>
    protected ServiceProvider BuildProvider(ICurrentUser? currentUser = null) =>
        new ServiceCollection()
            .AddInfrastructure(Database.ApplicationConnectionString)
            .AddApplication()
            .AddScoped(_ => currentUser ?? TestCurrentUser.Anonymous)
            .BuildServiceProvider();

    protected async Task<T?> ScalarAsync<T>(string sql)
    {
        await using var connection = new SqlConnection(Database.MigratorConnectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = new SqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync(CancellationToken);

        return value is null or DBNull ? default : (T)value;
    }
}

/// <summary>A stand-in for the authenticated request context (ADR-0017 item 2).</summary>
public sealed class TestCurrentUser : ICurrentUser
{
    public static readonly TestCurrentUser Anonymous = new() { CorrelationId = "test-correlation" };

    public Guid? UserId { get; init; }

    public IReadOnlyCollection<string> Roles { get; init; } = [];

    public string? ClientIp { get; init; }

    public required string CorrelationId { get; init; }

    public string? AuthorizedByPermission { get; init; }
}

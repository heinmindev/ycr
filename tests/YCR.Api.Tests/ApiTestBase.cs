using YCR.Api.Tests.Authentication;
using YCR.TestSupport;

namespace YCR.Api.Tests;

/// <summary>
/// xUnit's adapter onto <see cref="SqlServerTestContainer"/>, plus a hosted API per test class.
/// </summary>
/// <remarks>
/// Duplicated from the other suites because <c>YCR.TestSupport</c> deliberately references no
/// test framework (<c>tests/Directory.Build.props</c> excludes it from the runner).
/// </remarks>
public sealed class SqlServerFixture : IAsyncLifetime
{
    public SqlServerTestContainer Container { get; } = new();

    public ValueTask InitializeAsync() => Container.StartAsync();

    public ValueTask DisposeAsync() => Container.DisposeAsync();
}

[CollectionDefinition(SqlServerCollection.Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SQL Server (pinned container)";
}

/// <summary>
/// A migrated database and a running API per test class, both under the <c>ycr_app</c>
/// credential (plan §Test fixture).
/// </summary>
[Collection(SqlServerCollection.Name)]
public abstract class ApiTestBase(SqlServerFixture fixture) : IAsyncLifetime
{
    protected TestDatabase Database { get; private set; } = null!;

    protected YcrApiFactory Api { get; private set; } = null!;

    protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    protected abstract string DatabasePrefix { get; }

    public async ValueTask InitializeAsync()
    {
        Database = await fixture.Container.ProvisionDatabaseAsync(DatabasePrefix, CancellationToken);
        Api = new YcrApiFactory(Database.ApplicationConnectionString);
    }

    public async ValueTask DisposeAsync() => await Api.DisposeAsync();
}

using YCR.TestSupport;

namespace YCR.Infrastructure.Tests;

/// <summary>
/// xUnit's adapter onto <see cref="SqlServerTestContainer"/>: one pinned SQL Server per
/// collection, started once and shared by every class in it.
/// </summary>
/// <remarks>
/// The adapter lives here rather than in <c>YCR.TestSupport</c> because that project is a
/// fixture library with no test-framework reference (<c>tests/Directory.Build.props</c>).
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

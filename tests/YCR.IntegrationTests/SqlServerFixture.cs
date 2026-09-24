using YCR.TestSupport;

namespace YCR.IntegrationTests;

/// <summary>
/// xUnit's adapter onto <see cref="SqlServerTestContainer"/>, one pinned SQL Server per collection.
/// </summary>
/// <remarks>
/// Duplicated from the other suites on purpose: <c>YCR.TestSupport</c> deliberately
/// references no test framework (<c>tests/Directory.Build.props</c> excludes it from the runner),
/// so the xUnit-shaped wrapper has to live in each consuming suite. Twenty lines is the price of
/// keeping the fixture library framework-agnostic.
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

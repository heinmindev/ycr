using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace YCR.TestSupport;

/// <summary>
/// One digest-pinned SQL Server 2022 container, shared by every test class in a collection,
/// handing each class its own freshly migrated database (plan §Test fixture).
/// </summary>
/// <remarks>
/// Deliberately framework-agnostic: <c>YCR.TestSupport</c> is a fixture library, not a test
/// project (<c>tests/Directory.Build.props</c> excludes it from the runner), so it does not
/// reference xUnit. Each consuming test project wraps this in its own collection fixture.
/// <para>
/// The <c>sa</c> password is generated per container and never written to source or to a
/// log. <see cref="Redact"/> strips it from anything this class reports.
/// </para>
/// </remarks>
public sealed class SqlServerTestContainer : IAsyncDisposable
{
    private readonly MsSqlContainer _container;
    private readonly string _password;
    private int _databaseCount;

    public SqlServerTestContainer()
    {
        _password = GeneratePassword();
        // The image is a constructor argument in Testcontainers 4.15: there is no default to
        // fall back to, which is exactly the property E4 wants.
        _container = new MsSqlBuilder(SqlServerImage.Reference)
            .WithPassword(_password)
            // Developer edition carries the full feature set, which is what the audit ledger
            // (ADR-0017 item 1) is verified against. Stated explicitly rather than inherited,
            // so an image default can never quietly downgrade the edition under V1.
            .WithEnvironment("MSSQL_PID", "Developer")
            .Build();
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        await _container.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a database, applies the migration bundle to it under the migrator credential,
    /// and returns its connection strings. The name is made unique per call so parallel test
    /// classes in one collection cannot collide.
    /// </summary>
    public async Task<TestDatabase> ProvisionDatabaseAsync(
        string namePrefix,
        CancellationToken cancellationToken = default)
    {
        // SQL Server names are limited to 128 characters and the suffix takes 40 of them.
        ArgumentOutOfRangeException.ThrowIfGreaterThan(namePrefix.Length, 80, nameof(namePrefix));
        var ordinal = Interlocked.Increment(ref _databaseCount);
        var database = $"{namePrefix}_{ordinal}_{Guid.NewGuid():N}";

        await ExecuteOnMasterAsync(
            $"CREATE DATABASE [{database.Replace("]", "]]", StringComparison.Ordinal)}];",
            cancellationToken).ConfigureAwait(false);

        var migratorConnectionString = ConnectionStringFor(database);
        await MigrationBundle.ApplyAsync(migratorConnectionString, cancellationToken).ConfigureAwait(false);

        return new TestDatabase(database, migratorConnectionString);
    }

    /// <summary>Connection string for <paramref name="database"/> under the migrator credential.</summary>
    public string ConnectionStringFor(string database) =>
        new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = database,
            TrustServerCertificate = true
        }.ConnectionString;

    /// <summary>Removes the generated password from text before it reaches a message or a log.</summary>
    public string Redact(string text) =>
        text.Replace(_password, "***", StringComparison.Ordinal);

    public async ValueTask DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(false);

    private async Task ExecuteOnMasterAsync(string sql, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_container.GetConnectionString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string GeneratePassword() =>
        // SQL Server rejects weak passwords, so the generated value carries each character
        // class explicitly rather than hoping a random slice happens to contain them.
        "Yc1!" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
            .Replace('+', 'a')
            .Replace('/', 'b')
            .Replace("=", string.Empty, StringComparison.Ordinal);
}

/// <summary>A migrated database on the shared container, with the credentials to reach it.</summary>
/// <param name="Name">Database name.</param>
/// <param name="MigratorConnectionString">
/// The credential that owns schema. Plan step 9 adds the separate least-privilege
/// <c>ycr_app</c> connection string alongside it.
/// </param>
public sealed record TestDatabase(string Name, string MigratorConnectionString);

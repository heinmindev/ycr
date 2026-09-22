using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace YCR.TestSupport;

/// <summary>
/// One digest-pinned SQL Server 2022 container, shared by every test class in a collection,
/// handing each class its own freshly migrated database with both credentials (plan §Test fixture).
/// </summary>
/// <remarks>
/// Deliberately framework-agnostic: <c>YCR.TestSupport</c> is a fixture library, not a test
/// project (<c>tests/Directory.Build.props</c> excludes it from the runner), so it does not
/// reference xUnit. Each consuming test project wraps this in its own collection fixture.
/// <para>
/// Both passwords are generated per container and never written to source or to a log.
/// </para>
/// </remarks>
public sealed class SqlServerTestContainer : IAsyncDisposable
{
    /// <summary>The server login the application runs as. The database user is <see cref="ApplicationUser"/>.</summary>
    public const string ApplicationLogin = "ycr_app";

    /// <summary>
    /// The database user for <see cref="ApplicationLogin"/>. It cannot also be called
    /// <c>ycr_app</c>: the role created by <c>Security_AppDatabaseRole</c> already holds that
    /// name, and a role and a user are both database principals.
    /// </summary>
    public const string ApplicationUser = "ycr_app_user";

    /// <summary>The role the migration creates and grants on (spec E7, ADR-0017 item 3).</summary>
    public const string ApplicationRole = "ycr_app";

    /// <summary>
    /// The login that owns schema and runs the migration bundle. Its database user carries the
    /// same name, exactly as <c>docker/sqlserver/init-principals.sql</c> creates it.
    /// </summary>
    /// <remarks>
    /// Before F-006A-1 / S-007A-1 this fixture handed out the Testcontainers <c>sa</c> connection
    /// as its "migrator", so every S22 privilege test compared the application credential against
    /// a sysadmin rather than against the credential the deployment actually uses. SA is now
    /// reserved for what only SA can do — <c>CREATE DATABASE</c> and login provisioning — and the
    /// migrator boundary is asserted by
    /// <c>MigratorCredential_ConnectedToATestDatabase_IsYcrMigratorAndNotSysadmin</c>.
    /// </remarks>
    public const string MigratorLogin = "ycr_migrator";

    private readonly MsSqlContainer _container;
    private readonly string _saPassword;
    private readonly string _applicationPassword;
    private readonly string _migratorPassword;
    private int _databaseCount;

    public SqlServerTestContainer()
        : this(SqlServerImage.Reference)
    {
    }

    /// <param name="image">
    /// The pinned image to run. The S19 guard test passes
    /// <see cref="SqlServerImage.Unsupported"/> to prove the audit ledger migration refuses an
    /// unsupported server version; everything else uses the default.
    /// </param>
    public SqlServerTestContainer(string image)
    {
        _saPassword = GeneratePassword();
        _applicationPassword = GeneratePassword();
        _migratorPassword = GeneratePassword();

        // The image is a constructor argument in Testcontainers 4.15: there is no default to
        // fall back to, which is exactly the property E4 wants.
        _container = new MsSqlBuilder(image)
            .WithPassword(_saPassword)
            // Developer edition carries the full feature set, which is what the audit ledger
            // (ADR-0017 item 1) is verified against. Stated explicitly rather than inherited,
            // so an image default can never quietly downgrade the edition under V1.
            .WithEnvironment("MSSQL_PID", "Developer")
            .Build();
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        await _container.StartAsync(cancellationToken).ConfigureAwait(false);

        // The login is server-scoped, so one per container serves every database on it. This is
        // the fixture standing in for environment provisioning (plan P9) — the same job
        // docker/sqlserver/init-principals.sql does locally and a CI step does in CI.
        //
        // CHECK_POLICY = OFF only here: the password is already 32 random characters, and the
        // container's policy is a property of a throwaway image rather than of our deployment.
        // The local and CI scripts leave the policy ON, where it means something.
        await ExecuteOnMasterAsync(
            $"""
             IF SUSER_ID(N'{ApplicationLogin}') IS NULL
                 CREATE LOGIN [{ApplicationLogin}] WITH PASSWORD = '{_applicationPassword}', CHECK_POLICY = OFF;
             IF SUSER_ID(N'{MigratorLogin}') IS NULL
                 CREATE LOGIN [{MigratorLogin}] WITH PASSWORD = '{_migratorPassword}', CHECK_POLICY = OFF;
             """,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a database, applies the migration bundle to it under the migrator credential, adds
    /// the application user to the role the migration created, and returns both connection
    /// strings. The name is made unique per call so parallel test classes cannot collide.
    /// </summary>
    public async Task<TestDatabase> ProvisionDatabaseAsync(
        string namePrefix,
        CancellationToken cancellationToken = default)
    {
        var database = await CreateEmptyDatabaseAsync(namePrefix, cancellationToken).ConfigureAwait(false);

        // Order matters. The bundle runs under the migrator credential, and only then is the
        // application user added to the role — because Security_AppDatabaseRole is what creates
        // the role in the first place.
        await MigrationBundle.ApplyAsync(database.MigratorConnectionString, cancellationToken).ConfigureAwait(false);

        await ExecuteAsync(
            database.MigratorConnectionString,
            $"ALTER ROLE [{ApplicationRole}] ADD MEMBER [{ApplicationUser}];",
            cancellationToken).ConfigureAwait(false);

        return database;
    }

    /// <summary>
    /// Creates a database and its application user but runs <strong>no</strong> migration, so a
    /// test can prove that resolving the application's services does not migrate on its own
    /// (spec E7: no migration at startup).
    /// </summary>
    public async Task<TestDatabase> CreateEmptyDatabaseAsync(
        string namePrefix,
        CancellationToken cancellationToken = default)
    {
        // SQL Server names are limited to 128 characters and the suffix takes 40 of them.
        ArgumentOutOfRangeException.ThrowIfGreaterThan(namePrefix.Length, 80, nameof(namePrefix));
        var ordinal = Interlocked.Increment(ref _databaseCount);
        var database = $"{namePrefix}_{ordinal}_{Guid.NewGuid():N}";
        var quoted = database.Replace("]", "]]", StringComparison.Ordinal);

        await ExecuteOnMasterAsync($"CREATE DATABASE [{quoted}];", cancellationToken).ConfigureAwait(false);

        // SA does only what none of the other principals can: create the database, then give the
        // migrator its per-database user and db_owner. That mirrors init-principals.sql, where
        // this is the provisioning step rather than anything the platform does at runtime.
        await ExecuteAsync(
            SaConnectionStringFor(database),
            $"""
             IF USER_ID(N'{MigratorLogin}') IS NULL
             BEGIN
                 CREATE USER [{MigratorLogin}] FOR LOGIN [{MigratorLogin}];
                 ALTER ROLE [db_owner] ADD MEMBER [{MigratorLogin}];
             END
             """,
            cancellationToken).ConfigureAwait(false);

        // From here on the migrator credential does the work, so a right it turns out not to have
        // fails here rather than passing under sysadmin and surfacing in a deployment.
        var migratorConnectionString = ConnectionStringFor(database, MigratorLogin, _migratorPassword);
        await ExecuteAsync(
            migratorConnectionString,
            $"""
             IF USER_ID(N'{ApplicationUser}') IS NULL
                 CREATE USER [{ApplicationUser}] FOR LOGIN [{ApplicationLogin}];
             """,
            cancellationToken).ConfigureAwait(false);

        return new TestDatabase(
            database,
            migratorConnectionString,
            ConnectionStringFor(database, ApplicationLogin, _applicationPassword));
    }

    /// <summary>
    /// Connection string for <paramref name="database"/> under the container's <c>sa</c> account.
    /// Provisioning only — tests receive the migrator and application credentials instead.
    /// </summary>
    /// <remarks>
    /// <c>Current Language</c> is pinned on every connection (hein's ruling, 2026-09-20). SQL
    /// Server localises error messages by session language, and
    /// <c>SqlServerUniqueConstraintTranslator</c> reads the violated constraint's name out of the
    /// message text because no API exposes it. A session in another language would silently stop
    /// producing translations. Pinning it here, in compose and in CI keeps that dependency true
    /// rather than accidental.
    /// </remarks>
    private string SaConnectionStringFor(string database) =>
        new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = database,
            TrustServerCertificate = true,
            CurrentLanguage = SqlServerImage.SessionLanguage
        }.ConnectionString;

    public async ValueTask DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(false);

    private string ConnectionStringFor(string database, string user, string password) =>
        new SqlConnectionStringBuilder(SaConnectionStringFor(database))
        {
            UserID = user,
            Password = password
        }.ConnectionString;

    private Task ExecuteOnMasterAsync(string sql, CancellationToken cancellationToken) =>
        ExecuteAsync(_container.GetConnectionString(), sql, cancellationToken);

    private static async Task ExecuteAsync(string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
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

/// <summary>A database on the shared container, with both credentials that reach it.</summary>
/// <param name="Name">Database name.</param>
/// <param name="MigratorConnectionString">
/// The <c>ycr_migrator</c> credential, which owns schema through <c>db_owner</c> and is not a
/// sysadmin. Only migration and infrastructure characterisation tests use it.
/// </param>
/// <param name="ApplicationConnectionString">
/// The least-privilege credential the platform actually runs under. Application and API tests
/// use this one, so a missing grant fails a test rather than surfacing in production
/// (plan §Test fixture).
/// </param>
public sealed record TestDatabase(
    string Name,
    string MigratorConnectionString,
    string ApplicationConnectionString);

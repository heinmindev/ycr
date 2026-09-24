using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YCR.TestSupport;
using YCR.Worker;

namespace YCR.IntegrationTests.Bootstrap;

/// <summary>
/// The first-administrator command (D10; plan P8; spec S32, S32a; R13): in process through
/// <see cref="BootstrapAdministratorCli.RunAsync"/> with the production composition, and once as a
/// real <c>YCR.Worker</c> process reading its password from a pipe.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class BootstrapAdministratorCommandTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string Password = "kyauk.tan.12.first";

    private TestDatabase database = null!;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() =>
        database = await fixture.Container.ProvisionDatabaseAsync("it_bootstrap", CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Theory]
    [InlineData("Testing")]
    [InlineData("Development")]
    public async Task Bootstrap_FirstRun_CreatesOneMustChangeAdministratorAndExitsZero(string environment)
    {
        var run = await RunAsync(["bootstrap-administrator", "--username", "hein.admin"], Password, environment);

        Assert.Equal(BootstrapAdministratorCli.Created, run.ExitCode);
        Assert.Contains("must be changed", run.Stdout, StringComparison.Ordinal);
        Assert.Equal(1, await ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM [identity].[Users] u
            JOIN [identity].[UserRoles] ur ON ur.[UserId] = u.[Id]
            JOIN [identity].[Roles] r ON r.[Id] = ur.[RoleId]
            WHERE r.[Name] = N'SystemAdministrator' AND u.[MustChangePassword] = 1 AND u.[UserName] = N'hein.admin';
            """));
        Assert.Equal(1, await ScalarAsync<int>(
            "SELECT COUNT(*) FROM [audit].[AuditEvents] WHERE [Action] = N'Identity.UserCreated' AND [ActorUserId] IS NULL AND [ActorRole] IS NULL AND [AuthorizedByPermission] IS NULL AND [ClientIp] IS NULL;"));
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM [audit].[AuditEvents];"));
    }

    /// <summary>
    /// S-1 (ADR-0023 item 4 as amended): in Production the bootstrap refuses a privileged account
    /// until MFA ships — exit 1, <c>Identity.PrivilegedRoleRequiresMfa</c>, nothing written.
    /// </summary>
    [Theory]
    [InlineData("Production")]
    [InlineData("production")]
    public async Task Bootstrap_InProduction_IsRefusedWithPrivilegedRoleRequiresMfaAndWritesNothing(string environment)
    {
        var run = await RunAsync(["bootstrap-administrator", "--username", "hein.admin"], Password, environment);

        Assert.Equal(BootstrapAdministratorCli.Refused, run.ExitCode);
        Assert.Contains("Identity.PrivilegedRoleRequiresMfa", run.Stderr, StringComparison.Ordinal);
        await AssertNothingWrittenAsync();
    }

    [Fact]
    public async Task Bootstrap_SecondRun_ExitsNonZeroAndWritesNothing()
    {
        Assert.Equal(0, (await RunAsync(["bootstrap-administrator", "--username", "hein.admin"], Password)).ExitCode);

        var second = await RunAsync(["bootstrap-administrator", "--username", "second.admin"], Password);

        Assert.Equal(BootstrapAdministratorCli.Refused, second.ExitCode);
        Assert.Contains("Identity.AdministratorAlreadyExists", second.Stderr, StringComparison.Ordinal);
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM [identity].[Users];"));
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM [audit].[AuditEvents];"));
    }

    /// <summary>S32a through the CLI: the R20 username rule, upper case included.</summary>
    [Theory]
    [InlineData("ab")]
    [InlineData("Hein.Admin")]
    [InlineData("hein admin")]
    [InlineData("hein_admin")]
    public async Task Bootstrap_InvalidUserName_ExitsNonZero(string userName)
    {
        var run = await RunAsync(["bootstrap-administrator", "--username", userName], Password);

        Assert.Equal(BootstrapAdministratorCli.Refused, run.ExitCode);
        Assert.Contains("Identity.InvalidUserName", run.Stderr, StringComparison.Ordinal);
        await AssertNothingWrittenAsync();
    }

    [Theory]
    [InlineData("short.pass1")]
    [InlineData("qwertyuiop12")]
    public async Task Bootstrap_PolicyViolatingPassword_ExitsNonZero(string password)
    {
        var run = await RunAsync(["bootstrap-administrator", "--username", "hein.admin"], password);

        Assert.Equal(BootstrapAdministratorCli.Refused, run.ExitCode);
        Assert.Contains("Identity.PasswordRejected", run.Stderr, StringComparison.Ordinal);
        await AssertNothingWrittenAsync();
    }

    /// <summary>R13: the password reaches no output, no log line (captured at Trace) and no ledger row.</summary>
    [Fact]
    public async Task Bootstrap_PasswordAppearsInNoOutputOrLog()
    {
        var created = await RunAsync(["bootstrap-administrator", "--username", "hein.admin"], Password);
        var refused = await RunAsync(["bootstrap-administrator", "--username", "other.admin"], Password);

        Assert.Equal(0, created.ExitCode);
        Assert.Equal(1, refused.ExitCode);
        Assert.NotEmpty(created.Logs);
        var hash = await ScalarAsync<string>("SELECT [PasswordHash] FROM [identity].[Users];");
        var ledger = await ScalarAsync<string>("SELECT STRING_AGG(CONCAT([ActorRole], [BeforeJson], [AfterJson], [ReasonCode]), N'|') FROM [audit].[AuditEvents];");
        foreach (var secret in new[] { Password, hash! })
        {
            foreach (var run in new[] { created, refused })
            {
                Assert.DoesNotContain(secret, run.Stdout, StringComparison.Ordinal);
                Assert.DoesNotContain(secret, run.Stderr, StringComparison.Ordinal);
                Assert.DoesNotContain(run.Logs, line => line.Contains(secret, StringComparison.Ordinal));
            }

            Assert.DoesNotContain(secret, ledger ?? string.Empty, StringComparison.Ordinal);
        }
    }

    /// <summary>P8: a password on the command line is refused before anything is read or written.</summary>
    [Theory]
    [InlineData("--password")]
    [InlineData("--password=kyauk.tan.12.first")]
    [InlineData("-p")]
    public async Task Bootstrap_PasswordOnCommandLine_IsRefused(string flag)
    {
        var run = await RunAsync(["bootstrap-administrator", "--username", "hein.admin", flag, Password], Password);

        Assert.Equal(BootstrapAdministratorCli.UsageError, run.ExitCode);
        Assert.Contains("never accepted on the command line", run.Stderr, StringComparison.Ordinal);
        await AssertNothingWrittenAsync();
    }

    [Theory]
    [InlineData("")]
    [InlineData("--username")]
    [InlineData("--user hein.admin")]
    public async Task Bootstrap_UsageErrorsOrNoPassword_ExitTwo(string arguments)
    {
        string[] args = ["bootstrap-administrator", .. arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries)];

        Assert.Equal(BootstrapAdministratorCli.UsageError, (await RunAsync(args, Password)).ExitCode);
        Assert.Equal(BootstrapAdministratorCli.UsageError, (await RunAsync(["bootstrap-administrator", "--username", "hein.admin"], stdin: string.Empty)).ExitCode);
        await AssertNothingWrittenAsync();
    }

    /// <summary>
    /// The deployed shape: <c>dotnet YCR.Worker.dll bootstrap-administrator --username …</c> with the
    /// connection string in the environment and the password on a pipe; the process exits 0 without
    /// starting the host, and a second run exits 1. Run in <c>Testing</c>: Production refuses (S-1).
    /// </summary>
    [Fact]
    public async Task Bootstrap_AsAProcess_ReadsThePasswordFromStdinAndExits()
    {
        var first = await RunProcessAsync("hein.admin", "Testing");
        var second = await RunProcessAsync("other.admin", "Testing");

        Assert.True(first.ExitCode == 0, $"exit {first.ExitCode}: {first.Stderr}");
        Assert.Equal(1, second.ExitCode);
        Assert.DoesNotContain(Password, first.Stdout + first.Stderr + second.Stdout + second.Stderr, StringComparison.Ordinal);
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM [identity].[Users];"));
    }

    /// <summary>S-1 in the deployed shape: with no <c>DOTNET_ENVIRONMENT</c> the Worker is Production, and refuses.</summary>
    [Fact]
    public async Task Bootstrap_AsAProcessWithNoEnvironment_IsProductionAndRefused()
    {
        var run = await RunProcessAsync("hein.admin", environment: null);

        Assert.Equal(BootstrapAdministratorCli.Refused, run.ExitCode);
        Assert.Contains("Identity.PrivilegedRoleRequiresMfa", run.Stderr, StringComparison.Ordinal);
        await AssertNothingWrittenAsync();
    }

    private async Task<CliRun> RunAsync(string[] args, string stdin, string environment = "Testing")
    {
        var logs = new CapturingLoggerProvider();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Application"] = database.ApplicationConnectionString })
            .Build();
        await using var services = new ServiceCollection()
            .AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(logs))
            .AddBootstrapAdministrator(configuration, environment)
            .BuildServiceProvider();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await BootstrapAdministratorCli.RunAsync(args, new StringReader(stdin), stdout, stderr, services, CancellationToken);

        return new CliRun(exitCode, stdout.ToString(), stderr.ToString(), [.. logs.Lines]);
    }

    /// <param name="environment">The <c>DOTNET_ENVIRONMENT</c> value, or null to leave it unset.</param>
    private async Task<CliRun> RunProcessAsync(string userName, string? environment)
    {
        var worker = Path.Combine(AppContext.BaseDirectory, "YCR.Worker.dll");
        Assert.True(File.Exists(worker), worker);
        var start = new ProcessStartInfo("dotnet")
        {
            ArgumentList = { worker, "bootstrap-administrator", "--username", userName },
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        start.Environment["ConnectionStrings__Application"] = database.ApplicationConnectionString;
        start.Environment.Remove("DOTNET_ENVIRONMENT");
        if (environment is not null)
        {
            start.Environment["DOTNET_ENVIRONMENT"] = environment;
        }

        using var process = Process.Start(start)!;
        await process.StandardInput.WriteLineAsync(Password);
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEndAsync(CancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(CancellationToken);
        await process.WaitForExitAsync(CancellationToken).WaitAsync(TimeSpan.FromMinutes(2), CancellationToken);

        return new CliRun(process.ExitCode, await stdout, await stderr, []);
    }

    private async Task AssertNothingWrittenAsync()
    {
        Assert.Equal(0, await ScalarAsync<int>("SELECT COUNT(*) FROM [identity].[Users];"));
        Assert.Equal(0, await ScalarAsync<int>("SELECT COUNT(*) FROM [audit].[AuditEvents];"));
    }

    private async Task<T?> ScalarAsync<T>(string sql)
    {
        await using var connection = new SqlConnection(database.MigratorConnectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = new SqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync(CancellationToken);
        return value is null or DBNull ? default : (T)value;
    }

    private sealed record CliRun(int ExitCode, string Stdout, string Stderr, IReadOnlyList<string> Logs);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> lines = new();

        public IReadOnlyCollection<string> Lines => lines;

        public ILogger CreateLogger(string categoryName) => new Logger(categoryName, lines);

        public void Dispose()
        {
        }

        private sealed class Logger(string category, ConcurrentQueue<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                ArgumentNullException.ThrowIfNull(formatter);
                var properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
                    ? string.Join(", ", pairs.Select(pair => $"{pair.Key}={pair.Value}"))
                    : string.Empty;
                lines.Enqueue($"{logLevel} {category}: {formatter(state, exception)} [{properties}] {exception}");
            }
        }
    }
}

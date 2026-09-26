using System.Diagnostics;
using Microsoft.Data.SqlClient;
using YCR.TestSupport;

namespace YCR.IntegrationTests.Worker;

/// <summary>
/// F-004 plan P11, ruling Q3: the real <c>YCR.Worker</c> process refuses to start without a
/// <c>Time:LocalTimeZone</c> this host can resolve — before either start path, the
/// <c>bootstrap-administrator</c> command and the bare host — with exit code 1, a clear message on
/// stderr, and nothing written.
/// </summary>
/// <remarks>
/// Run as a process the way <c>BootstrapAdministratorCommandTests</c> runs it, because the check is
/// in <c>Program.Main</c>, which the in-process CLI path does not go through. The environment
/// variable overrides the shipped <c>appsettings.json</c>, as a deployment's would.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public sealed class WorkerStartupTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private TestDatabase database = null!;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() =>
        database = await fixture.Container.ProvisionDatabaseAsync("it_worker_zone", CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Theory]
    [InlineData("", "bootstrap-administrator")]
    [InlineData("", "host")]
    [InlineData("Mars/Olympus", "bootstrap-administrator")]
    [InlineData("Mars/Olympus", "host")]
    public async Task Worker_WithMissingOrUnknownLocalTimeZone_ExitsOneWithClearError(string zone, string startPath)
    {
        var worker = Path.Combine(AppContext.BaseDirectory, "YCR.Worker.dll");
        Assert.True(File.Exists(worker), worker);
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        start.ArgumentList.Add(worker);
        if (startPath == "bootstrap-administrator")
        {
            foreach (var argument in new[] { "bootstrap-administrator", "--username", "hein.admin" })
            {
                start.ArgumentList.Add(argument);
            }
        }

        start.Environment["ConnectionStrings__Application"] = database.ApplicationConnectionString;
        start.Environment["DOTNET_ENVIRONMENT"] = "Testing";
        start.Environment["Time__LocalTimeZone"] = zone;

        using var process = Process.Start(start)!;
        await process.StandardInput.WriteLineAsync("kyauk.tan.12.first");
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEndAsync(CancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(CancellationToken);
        try
        {
            await process.WaitForExitAsync(CancellationToken).WaitAsync(TimeSpan.FromMinutes(1), CancellationToken);
        }
        catch (TimeoutException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        var error = await stderr;
        Assert.True(process.ExitCode == 1, $"exit {process.ExitCode}; stderr: {error}; stdout: {await stdout}");
        Assert.Contains("Time:LocalTimeZone", error, StringComparison.Ordinal);
        if (zone.Length > 0)
        {
            Assert.Contains($"'{zone}'", error, StringComparison.Ordinal);
        }

        Assert.Equal(0, await ScalarAsync("SELECT COUNT(*) FROM [identity].[Users];"));
        Assert.Equal(0, await ScalarAsync("SELECT COUNT(*) FROM [audit].[AuditEvents];"));
    }

    private async Task<int> ScalarAsync(string sql)
    {
        await using var connection = new SqlConnection(database.MigratorConnectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = new SqlCommand(sql, connection);
        return (int)(await command.ExecuteScalarAsync(CancellationToken))!;
    }
}

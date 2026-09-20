using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace YCR.TestSupport;

/// <summary>
/// Builds and runs the EF Core migration bundle — the artifact plan decision P1 chose over a
/// <c>YCR.DbMigrator</c> project, and the subject of verification V6.
/// </summary>
/// <remarks>
/// Tests apply schema the same way a deployment does: one self-contained executable produced by
/// <c>dotnet ef migrations bundle</c>, run under the migrator credential. Using EF's in-process
/// <c>Migrate()</c> here instead would leave V6 unproven — the bundle is what ships, so the
/// bundle is what the suite has to exercise, including the raw-SQL ledger migration.
/// <para>
/// The bundle is built once per test process and reused for every database.
/// </para>
/// </remarks>
public static partial class MigrationBundle
{
    private static readonly SemaphoreSlim BuildGate = new(1, 1);
    private static string? builtBundlePath;

    /// <summary>Builds the bundle if this process has not built it yet, and returns its path.</summary>
    public static async Task<string> EnsureBuiltAsync(CancellationToken cancellationToken = default)
    {
        if (builtBundlePath is not null)
        {
            return builtBundlePath;
        }

        await BuildGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (builtBundlePath is not null)
            {
                return builtBundlePath;
            }

            var repositoryRoot = FindRepositoryRoot();
            var outputDirectory = Directory.CreateTempSubdirectory("ycr-efbundle-").FullName;
            var bundlePath = Path.Combine(
                outputDirectory,
                OperatingSystem.IsWindows() ? "efbundle.exe" : "efbundle");

            // The local tool manifest pins dotnet-ef, so the bundle is built by the same tool
            // version everywhere rather than by whatever happens to be installed globally.
            await RunAsync(repositoryRoot, "dotnet", ["tool", "restore"], cancellationToken)
                .ConfigureAwait(false);

            await RunAsync(
                repositoryRoot,
                "dotnet",
                [
                    "ef", "migrations", "bundle",
                    "--force",
                    "--project", Path.Combine("src", "YCR.Infrastructure"),
                    "--startup-project", Path.Combine("src", "YCR.Infrastructure"),
                    "--output", bundlePath
                ],
                cancellationToken).ConfigureAwait(false);

            builtBundlePath = bundlePath;
            return bundlePath;
        }
        finally
        {
            BuildGate.Release();
        }
    }

    /// <summary>Applies every migration to <paramref name="connectionString"/> using the bundle.</summary>
    public static async Task ApplyAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var bundlePath = await EnsureBuiltAsync(cancellationToken).ConfigureAwait(false);

        await RunAsync(
            Path.GetDirectoryName(bundlePath)!,
            bundlePath,
            ["--connection", connectionString],
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs the bundle and returns its failure output instead of throwing, so a test can assert
    /// on the message a guard produced (ADR-0017 item 5). Returns <c>null</c> when it succeeded.
    /// </summary>
    public static async Task<string?> TryApplyAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await ApplyAsync(connectionString, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (InvalidOperationException failure)
        {
            return failure.Message;
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "YCR.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"No YCR.sln found above '{AppContext.BaseDirectory}'; cannot locate the repository root.");
    }

    private static async Task RunAsync(
        string workingDirectory,
        string fileName,
        string[] arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start '{fileName}'.");

        var output = new StringBuilder();
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        output.Append(await standardOutput.ConfigureAwait(false));
        output.Append(await standardError.ConfigureAwait(false));

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'{fileName}' exited with {process.ExitCode}.{Environment.NewLine}{Redact(output.ToString())}");
        }
    }

    /// <summary>
    /// Strips the password out of anything a failing process printed. Connection strings are
    /// passed on the command line, so tooling echoes them back on error.
    /// </summary>
    private static string Redact(string text) => PasswordPattern().Replace(text, "Password=***");

    [GeneratedRegex("Password=[^;\"']*", RegexOptions.IgnoreCase)]
    private static partial Regex PasswordPattern();
}

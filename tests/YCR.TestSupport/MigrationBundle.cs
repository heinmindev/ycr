using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using YCR.Infrastructure.Persistence;

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
    /// <summary>
    /// Names a bundle that has already been built, so the fixture uses it instead of building
    /// one (hein's ruling, 2026-09-21).
    /// </summary>
    /// <remarks>
    /// CI builds the bundle once, as its own step, and sets this. Every test process then skips
    /// the build entirely — which removes the slowest part of the run and, more importantly,
    /// removes the contention that made the local build need a cross-process lock at all.
    /// Unset, the fixture falls back to building under that lock, which is what a developer's
    /// machine does.
    /// </remarks>
    public const string BundlePathVariable = "YCR_MIGRATION_BUNDLE";

    private static readonly SemaphoreSlim BuildGate = new(1, 1);
    private static string? builtBundlePath;

    /// <summary>Builds the bundle if this process has not built it yet, and returns its path.</summary>
    public static async Task<string> EnsureBuiltAsync(CancellationToken cancellationToken = default)
    {
        if (builtBundlePath is not null)
        {
            return builtBundlePath;
        }

        var supplied = Environment.GetEnvironmentVariable(BundlePathVariable);
        if (!string.IsNullOrWhiteSpace(supplied))
        {
            // Fail loudly rather than silently building a second bundle: if CI set this and the
            // file is missing, the run is not testing what CI thinks it is.
            if (!File.Exists(supplied))
            {
                throw new FileNotFoundException(
                    $"{BundlePathVariable} is set to '{supplied}' but no file exists there.", supplied);
            }

            builtBundlePath = supplied;
            return supplied;
        }

        await BuildGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (builtBundlePath is not null)
            {
                return builtBundlePath;
            }

            var repositoryRoot = FindRepositoryRoot();
            var outputDirectory = BundleDirectoryFor(repositoryRoot);
            Directory.CreateDirectory(outputDirectory);
            var bundlePath = Path.Combine(
                outputDirectory,
                OperatingSystem.IsWindows() ? "efbundle.exe" : "efbundle");

            // Serialised across processes, not just across threads. `dotnet ef migrations bundle`
            // publishes through the Infrastructure project's own obj/ directory, so two test
            // assemblies running in parallel — which is the default for `dotnet test YCR.sln` —
            // would build into the same intermediate output and one would fail with a bare
            // "Build failed". An in-process semaphore cannot see the other process.
            using (await CrossProcessLock.AcquireAsync(
                Path.Combine(outputDirectory, ".build-lock"), cancellationToken).ConfigureAwait(false))
            {
                if (IsUpToDate(bundlePath))
                {
                    builtBundlePath = bundlePath;
                    return bundlePath;
                }

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
            }

            builtBundlePath = bundlePath;
            return bundlePath;
        }
        finally
        {
            BuildGate.Release();
        }
    }

    /// <summary>Applies every migration to <paramref name="connectionString"/> using the bundle.</summary>
    /// <remarks>
    /// S-007A-2: the target is passed in the child's environment, not as
    /// <c>--connection &lt;string&gt;</c>. `efbundle --help` documents that the connection
    /// "[d]efaults to the one specified in AddDbContext or OnConfiguring", and the bundle resolves
    /// that through <see cref="YcrDbContextFactory"/> at run time — verified by running a bundle
    /// with no argument and watching it dial the host named only in the variable. A command line
    /// is visible to anything that can list processes and is echoed back by tools on failure; an
    /// environment variable is neither.
    /// <para>
    /// <see cref="Redact"/> still runs over the output, because the bundle prints the connection
    /// it used in some failure paths regardless of how it was given.
    /// </para>
    /// </remarks>
    public static async Task ApplyAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var bundlePath = await EnsureBuiltAsync(cancellationToken).ConfigureAwait(false);

        await RunAsync(
            Path.GetDirectoryName(bundlePath)!,
            bundlePath,
            [],
            cancellationToken,
            designTimeConnectionOverride: connectionString).ConfigureAwait(false);
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

    /// <summary>
    /// A stable per-checkout directory for the bundle, so a second test process reuses the first
    /// one's build instead of repeating it. Keyed by the repository path, so two worktrees of the
    /// same repository do not share a bundle.
    /// </summary>
    private static string BundleDirectoryFor(string repositoryRoot)
    {
        var key = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes(repositoryRoot.ToUpperInvariant())))[..16];

        return Path.Combine(Path.GetTempPath(), "ycr-migration-bundle", key);
    }

    /// <summary>
    /// Whether an existing bundle can be reused: it must be newer than the Infrastructure
    /// assembly it was built from, or a code change would be tested against a stale migrator.
    /// </summary>
    private static bool IsUpToDate(string bundlePath)
    {
        if (!File.Exists(bundlePath))
        {
            return false;
        }

        var infrastructure = typeof(YCR.Infrastructure.Persistence.YcrDbContext).Assembly.Location;

        return !string.IsNullOrEmpty(infrastructure)
            && File.Exists(infrastructure)
            && File.GetLastWriteTimeUtc(bundlePath) >= File.GetLastWriteTimeUtc(infrastructure);
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

    /// <summary>
    /// A syntactically valid connection string that resolves to nothing, used only so
    /// <c>dotnet ef migrations bundle</c> can construct a context for model discovery.
    /// </summary>
    /// <remarks>
    /// Building a bundle never opens a connection — EF needs the factory only to read the model —
    /// so the value is inert by construction. It deliberately carries no
    /// <c>TrustServerCertificate</c>, because a string that is never opened should not model a
    /// disabled certificate check for anyone who copies it.
    /// </remarks>
    public const string DesignTimeConnectionPlaceholder =
        "Server=ycr-design-time-placeholder;Database=YcrDesignTime;Trusted_Connection=True";

    /// <summary>
    /// Builds the start info for a child process, supplying
    /// <see cref="YcrDbContextFactory.DesignTimeConnectionVariable"/> when
    /// <paramref name="inheritedDesignTimeConnection"/> is absent.
    /// </summary>
    /// <param name="inheritedDesignTimeConnection">
    /// What the parent process has, or null/whitespace when it has nothing. A real value is
    /// **never** overridden by the placeholder: a developer or a CI job that points EF somewhere
    /// deliberately keeps that target.
    /// </param>
    /// <param name="designTimeConnectionOverride">
    /// An explicit target that always wins, used when applying the bundle (S-007A-2). Applying to
    /// a specific database is an argument of the operation, not an ambient default, so it
    /// overrides both the placeholder and anything inherited. Passing it in the environment
    /// instead of on the command line keeps the password out of the process arguments, where any
    /// tool that echoes its own invocation would leak it.
    /// </param>
    /// <remarks>
    /// R-1. The factory requires this variable and has no default, but the fixture builds the
    /// bundle by spawning <c>dotnet ef</c>, which inherits the parent's environment — so on a
    /// clean clone `dotnet test YCR.sln` failed every container-backed test with
    /// "YCR_DESIGN_TIME_CONNECTION must be set". CI never saw it, because CI builds the bundle in
    /// its own step and hands the fixture <see cref="BundlePathVariable"/>, taking the other
    /// branch entirely.
    /// <para>
    /// Taking the inherited value as a parameter rather than reading the environment inside keeps
    /// this testable without any test mutating a process-global variable — which is itself a race
    /// when xUnit runs collections in parallel.
    /// </para>
    /// </remarks>
    public static ProcessStartInfo CreateStartInfo(
        string workingDirectory,
        string fileName,
        IReadOnlyList<string> arguments,
        string? inheritedDesignTimeConnection,
        string? designTimeConnectionOverride = null)
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

        if (!string.IsNullOrWhiteSpace(designTimeConnectionOverride))
        {
            startInfo.Environment[YcrDbContextFactory.DesignTimeConnectionVariable] =
                designTimeConnectionOverride;
        }
        else if (string.IsNullOrWhiteSpace(inheritedDesignTimeConnection))
        {
            startInfo.Environment[YcrDbContextFactory.DesignTimeConnectionVariable] =
                DesignTimeConnectionPlaceholder;
        }

        return startInfo;
    }

    /// <summary>
    /// Internal rather than private so <c>YCR.Infrastructure.Tests</c> can exercise the real
    /// <see cref="Process.Start(ProcessStartInfo)"/> call directly (N-2): the
    /// <see cref="CreateStartInfo"/>-level tests prove what environment a
    /// <see cref="ProcessStartInfo"/> is built with, not what a spawned child actually receives.
    /// </summary>
    internal static async Task RunAsync(
        string workingDirectory,
        string fileName,
        string[] arguments,
        CancellationToken cancellationToken,
        string? designTimeConnectionOverride = null)
    {
        var startInfo = CreateStartInfo(
            workingDirectory,
            fileName,
            arguments,
            Environment.GetEnvironmentVariable(YcrDbContextFactory.DesignTimeConnectionVariable),
            designTimeConnectionOverride);

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

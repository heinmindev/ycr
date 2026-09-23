using YCR.Infrastructure.Persistence;
using YCR.TestSupport;

namespace YCR.Infrastructure.Tests.TestSupport;

/// <summary>
/// N-2 (T-006a re-review at 52326b3): <see cref="MigrationBundleStartInfoTests"/> only proves what
/// <see cref="ProcessStartInfo"/> is built with. The non-override contract — that a real ambient
/// <see cref="YcrDbContextFactory.DesignTimeConnectionVariable"/> reaches the spawned child
/// unchanged — was untested at <see cref="MigrationBundle.RunAsync"/>'s actual
/// <see cref="Process.Start(ProcessStartInfo)"/> call, the fixture's real call site.
/// </summary>
/// <remarks>
/// Follows the same save/restore-in-<c>finally</c> pattern <c>YcrDbContextFactoryTests</c> already
/// uses for this exact variable: mutating it briefly and restoring it is the established way this
/// suite tests ambient-environment behaviour, in-process assertions on <c>ProcessStartInfo</c>
/// cannot reach.
/// <para>
/// The child process is made to exit non-zero so <see cref="MigrationBundle.RunAsync"/> throws
/// with the child's own output in the exception message (see <see cref="MigrationBundle"/>'s
/// <c>Redact</c>) — the only way to observe what the child actually received, since a successful
/// run discards its output.
/// </para>
/// </remarks>
[Collection(DesignTimeConnectionCollection.Name)]
public sealed class MigrationBundleRunAsyncTests
{
    [Fact]
    public async Task RunAsync_WithNoOverride_PassesTheInheritedConnectionToTheChildUnchanged()
    {
        const string inherited = "Server=deliberate-inherited;Database=YcrDesignTime;Trusted_Connection=True";
        var variable = YcrDbContextFactory.DesignTimeConnectionVariable;
        var original = Environment.GetEnvironmentVariable(variable);

        Environment.SetEnvironmentVariable(variable, inherited);
        try
        {
            var (fileName, arguments) = EchoAndFailCommand(variable);

            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                MigrationBundle.RunAsync(
                    ".", fileName, arguments, TestContext.Current.CancellationToken));

            Assert.Contains(inherited, failure.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(
                MigrationBundle.DesignTimeConnectionPlaceholder, failure.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, original);
        }
    }

    [Fact]
    public async Task RunAsync_WithNoInheritedConnection_PassesThePlaceholderToTheChild()
    {
        var variable = YcrDbContextFactory.DesignTimeConnectionVariable;
        var original = Environment.GetEnvironmentVariable(variable);

        Environment.SetEnvironmentVariable(variable, null);
        try
        {
            var (fileName, arguments) = EchoAndFailCommand(variable);

            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                MigrationBundle.RunAsync(
                    ".", fileName, arguments, TestContext.Current.CancellationToken));

            Assert.Contains(
                MigrationBundle.DesignTimeConnectionPlaceholder, failure.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, original);
        }
    }

    /// <summary>
    /// A command that prints <paramref name="variableName"/>'s value to stdout and exits non-zero,
    /// so the caller can read it back out of <see cref="MigrationBundle.RunAsync"/>'s exception
    /// message.
    /// </summary>
    private static (string FileName, string[] Arguments) EchoAndFailCommand(string variableName) =>
        OperatingSystem.IsWindows()
            ? ("cmd.exe", ["/c", $"echo %{variableName}% & exit 7"])
            : ("/bin/sh", ["-c", $"echo ${variableName}; exit 7"]);
}

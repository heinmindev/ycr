using YCR.Infrastructure.Persistence;
using YCR.TestSupport;

namespace YCR.Infrastructure.Tests.TestSupport;

/// <summary>
/// R-1: the fixture spawns <c>dotnet ef migrations bundle</c>, and the design-time factory
/// requires <c>YCR_DESIGN_TIME_CONNECTION</c> with no default. The child must therefore be given
/// one when the parent has none — and must never have a real one replaced.
/// </summary>
/// <remarks>
/// Deliberately CI-independent: no container, no Docker, no <c>dotnet ef</c>, and above all no
/// mutation of a process-global environment variable. The inherited value is a parameter, so this
/// runs identically on a developer's machine and on a runner, and cannot race another test
/// collection the way an <c>Environment.SetEnvironmentVariable</c> test would.
/// <para>
/// The defect this covers was invisible to CI, which builds the bundle in its own step and hands
/// the fixture <c>YCR_MIGRATION_BUNDLE</c> — so a test that only ran green in CI would have proved
/// nothing. This one exercises the exact construction the broken path used.
/// </para>
/// </remarks>
public sealed class MigrationBundleStartInfoTests
{
    private static readonly string[] Arguments = ["ef", "migrations", "bundle", "--force"];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateStartInfo_WithNoInheritedConnection_SuppliesThePlaceholder(string? inherited)
    {
        var startInfo = MigrationBundle.CreateStartInfo(".", "dotnet", Arguments, inherited);

        Assert.Equal(
            MigrationBundle.DesignTimeConnectionPlaceholder,
            startInfo.Environment[YcrDbContextFactory.DesignTimeConnectionVariable]);
    }

    [Fact]
    public void CreateStartInfo_WithAnInheritedConnection_DoesNotOverrideIt()
    {
        const string inherited = "Server=deliberate;Database=YcrDesignTime;Trusted_Connection=True";

        var startInfo = MigrationBundle.CreateStartInfo(".", "dotnet", Arguments, inherited);

        // `ProcessStartInfo.Environment` starts as a **copy of this process's environment**, not as
        // an empty set of overrides, so "the method left it alone" cannot be asserted by the key
        // being absent — on a machine that has the variable set, it is present either way. What
        // must hold is that the entry still says exactly what this process says, which is true
        // whether the ambient value exists or not. Writing the placeholder here would silently
        // redirect a developer or a CI job that had pointed EF somewhere on purpose.
        var ambient = Environment.GetEnvironmentVariable(YcrDbContextFactory.DesignTimeConnectionVariable);
        startInfo.Environment.TryGetValue(YcrDbContextFactory.DesignTimeConnectionVariable, out var actual);

        Assert.Equal(ambient, actual);
        Assert.NotEqual(MigrationBundle.DesignTimeConnectionPlaceholder, actual);
    }

    /// <summary>
    /// S-007A-2: applying the bundle passes the target — and therefore the migrator password — in
    /// the environment rather than on the command line.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("Server=inherited;Database=Other;Trusted_Connection=True")]
    public void CreateStartInfo_WithAnOverride_UsesItWhateverTheParentHas(string? inherited)
    {
        const string target =
            "Server=localhost,1433;Database=YCR;User Id=ycr_migrator;Password=s3cr3t;TrustServerCertificate=True";

        var startInfo = MigrationBundle.CreateStartInfo(".", "efbundle", [], inherited, target);

        // The override wins over both the placeholder and an inherited value: which database to
        // migrate is an argument of the operation, not an ambient default.
        Assert.Equal(
            target,
            startInfo.Environment[YcrDbContextFactory.DesignTimeConnectionVariable]);

        // And the secret is nowhere in the process arguments, which is the whole point.
        Assert.Empty(startInfo.ArgumentList);
        Assert.DoesNotContain("s3cr3t", startInfo.Arguments, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateStartInfo_WithArgumentsAndWorkingDirectory_PassesThemThrough()
    {
        var startInfo = MigrationBundle.CreateStartInfo("/repo", "dotnet", Arguments, null);

        Assert.Equal("/repo", startInfo.WorkingDirectory);
        Assert.Equal("dotnet", startInfo.FileName);
        Assert.Equal(Arguments, startInfo.ArgumentList);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.False(startInfo.UseShellExecute);
    }

    /// <summary>
    /// The placeholder is only ever used to construct a context for model discovery, so it must
    /// parse — but it must not look like a usable target, and it must not model a disabled
    /// certificate check for anyone who copies it.
    /// </summary>
    [Fact]
    public void DesignTimeConnectionPlaceholder_IsInertAndCarriesNoCredential()
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            MigrationBundle.DesignTimeConnectionPlaceholder);

        Assert.Equal("ycr-design-time-placeholder", builder.DataSource);
        Assert.Empty(builder.UserID);
        Assert.Empty(builder.Password);
        Assert.False(builder.TrustServerCertificate);
    }
}

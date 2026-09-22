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

        // Absent from the child's explicit overrides means the child inherits the parent's real
        // value untouched. Writing the placeholder here would silently redirect a developer or a
        // CI job that had pointed EF somewhere on purpose.
        Assert.DoesNotContain(
            YcrDbContextFactory.DesignTimeConnectionVariable,
            startInfo.Environment.Keys);
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

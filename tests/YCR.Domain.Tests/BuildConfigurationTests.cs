using System.Reflection;
using System.Runtime.Versioning;

namespace YCR.Domain.Tests;

/// <summary>
/// Step 1's exit criteria. These prove the SDK pin in global.json and the
/// target framework in Directory.Build.props actually took effect.
///
/// TreatWarningsAsErrors is deliberately not asserted here: it is a build-time
/// property with no runtime trace, and a test that pretended to check it would
/// be worse than none. The evidence is that the build succeeds with it set.
/// </summary>
public sealed class BuildConfigurationTests
{
    [Fact]
    public void RuntimeVersion_UnderPinnedSdk_IsNet10()
    {
        Assert.Equal(10, Environment.Version.Major);
    }

    [Fact]
    public void TestAssembly_TargetsNet10()
    {
        var targetFramework = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<TargetFrameworkAttribute>();

        Assert.NotNull(targetFramework);
        Assert.Equal(".NETCoreApp,Version=v10.0", targetFramework.FrameworkName);
    }
}

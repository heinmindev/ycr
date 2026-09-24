using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using YCR.Domain.Identity;

namespace YCR.Infrastructure.Tests.Identity;

/// <summary>
/// Plan F-002 verifications V1 and V2, run inside the repository against the pinned package set
/// (spec O6: PLAN's scratch check ran outside it, on the 10.0.10 shared framework).
/// </summary>
/// <remarks>
/// This project carries a <c>FrameworkReference</c> to <c>Microsoft.AspNetCore.App</c> for the
/// same reason <c>YCR.Api</c> has one: so both checks run in the shape the API runs in, where the
/// shared framework and the explicit <c>Microsoft.Extensions.Identity.Core</c> pin meet.
/// </remarks>
public sealed class IdentityCoreRegistrationTests
{
    /// <summary>
    /// V1 / D1: <c>AddIdentityCore</c> brings no cookie scheme, so the only scheme the API will
    /// ever have is the one it registers itself (ADR-0023 item 1, ADR-0020 item 5).
    /// </summary>
    /// <remarks>
    /// Since plan step 4, <c>AddInfrastructure</c> itself calls <c>AddIdentityCore&lt;StaffUser&gt;</c>;
    /// the test resolves <c>UserManager</c> to prove Identity really is registered.
    /// </remarks>
    [Fact]
    public async Task AddInfrastructure_RegistersNoAuthenticationScheme()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication();
        services.AddInfrastructure("Server=unused;Database=unused;Integrated Security=true");
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var schemes = scope.ServiceProvider.GetRequiredService<IAuthenticationSchemeProvider>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>();

        Assert.Empty(await schemes.GetAllSchemesAsync());
        Assert.Null(await schemes.GetDefaultAuthenticateSchemeAsync());
        Assert.Null(await schemes.GetDefaultChallengeSchemeAsync());

        // Plan P1/P2: the store supports exactly passwords and security stamps. Lockout stays in
        // the domain on TimeProvider, so UserManager must not think it owns it.
        Assert.True(userManager.SupportsUserPassword);
        Assert.True(userManager.SupportsUserSecurityStamp);
        Assert.False(userManager.SupportsUserLockout);
        Assert.False(userManager.SupportsUserEmail);
        Assert.False(userManager.SupportsUserClaim);
        Assert.False(userManager.SupportsUserTwoFactor);
    }

    /// <summary>
    /// V2 / P13, as resolved at step 1 by the plan's fallback: in a project that references the
    /// ASP.NET Core shared framework — as <c>YCR.Api</c> does — the SDK resolves
    /// <c>Microsoft.Extensions.Identity.Core</c> to the <b>shared framework's</b> copy, not to the
    /// 10.0.12 package (on the development machine at step 1 the runtime was 10.0.10). The API
    /// therefore runs whatever Identity the installed runtime carries (risk R-9). What must hold is
    /// that it never mixes: Identity is the same runtime build as the rest of ASP.NET Core.
    /// </summary>
    [Fact]
    public void IdentityCore_LoadedAssembly_ComesFromTheAspNetCoreRuntime()
    {
        var identity = FileVersionInfo.GetVersionInfo(typeof(UserManager<>).Assembly.Location);
        var authentication = FileVersionInfo.GetVersionInfo(typeof(IAuthenticationSchemeProvider).Assembly.Location);

        Assert.Equal(10, identity.FileMajorPart);
        Assert.Equal(0, identity.FileMinorPart);
        Assert.Equal(authentication.ProductVersion, identity.ProductVersion);
        Assert.Equal(
            Path.GetDirectoryName(typeof(IAuthenticationSchemeProvider).Assembly.Location),
            Path.GetDirectoryName(typeof(UserManager<>).Assembly.Location));
    }
}

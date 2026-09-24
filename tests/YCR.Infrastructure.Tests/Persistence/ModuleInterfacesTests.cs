using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Network;
using YCR.Domain.Network;
using YCR.Infrastructure.Persistence;
using YCR.TestSupport;

namespace YCR.Infrastructure.Tests.Persistence;

/// <summary>
/// S17: within one scope every module context interface resolves to the same
/// <see cref="YcrDbContext"/> instance, and a different scope gets a different one.
/// </summary>
/// <remarks>
/// Run against the real pinned container rather than a connection string that is never
/// opened, so the test proves the shared instance actually shares a transaction — which is
/// the property ADR-0012's one-context-many-interfaces design depends on.
/// <para>
/// Credential: <c>ycr_app</c>, the identity the platform actually runs under (plan §Test
/// fixture). Moved onto it at step 9, when <c>Security_AppDatabaseRole</c> created the role.
/// A grant this scoping depends on therefore fails here rather than in production.
/// </para>
/// </remarks>
[Collection(SqlServerCollection.Name)]
public sealed class ModuleInterfacesTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private TestDatabase database = null!;

    public async ValueTask InitializeAsync() =>
        database = await fixture.Container.ProvisionDatabaseAsync("module_scope", TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ModuleInterfaces_WithinOneScope_ResolveToSameContextInstance()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = new ServiceCollection()
            .AddInfrastructure(database.ApplicationConnectionString)
            .BuildServiceProvider();

        await using var firstScope = provider.CreateAsyncScope();
        var concreteContext = firstScope.ServiceProvider.GetRequiredService<YcrDbContext>();
        var moduleContext = firstScope.ServiceProvider.GetRequiredService<INetworkDbContext>();
        var secondResolution = firstScope.ServiceProvider.GetRequiredService<INetworkDbContext>();

        await using var secondScope = provider.CreateAsyncScope();
        var otherScopeContext = secondScope.ServiceProvider.GetRequiredService<INetworkDbContext>();

        Assert.Same(concreteContext, moduleContext);
        Assert.Same(moduleContext, secondResolution);
        Assert.NotSame(moduleContext, otherScopeContext);

        // The instance is shared in the only sense that matters: work tracked through the
        // module interface is saved by the concrete context, in one SaveChanges.
        var id = Guid.CreateVersion7();
        moduleContext.Stations.Add(Station.Create(
            id,
            StationCode.Create("SHW").Value,
            BilingualName.Create("Shwedagon", "ရွှေတိဂုံ").Value,
            DateTimeOffset.UtcNow));

        await concreteContext.SaveChangesAsync(cancellationToken);

        Assert.True(await otherScopeContext.Stations.AnyAsync(station => station.Id == id, cancellationToken));
    }

    /// <summary>F-002: the Identity module's interface is the same shared instance (ADR-0012).</summary>
    [Fact]
    public async Task ModuleInterfaces_IdentityContext_ResolvesToSameInstance()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = new ServiceCollection()
            .AddInfrastructure(database.ApplicationConnectionString)
            .BuildServiceProvider();

        await using var scope = provider.CreateAsyncScope();
        var concreteContext = scope.ServiceProvider.GetRequiredService<YcrDbContext>();
        var identityContext = scope.ServiceProvider.GetRequiredService<YCR.Application.Identity.IIdentityDbContext>();
        var networkContext = scope.ServiceProvider.GetRequiredService<INetworkDbContext>();

        Assert.Same(concreteContext, identityContext);
        Assert.Same(networkContext, identityContext);

        // Reachable under ycr_app: the seeded catalogue is readable through the interface.
        Assert.Equal(8, await identityContext.Roles.CountAsync(cancellationToken));
    }
}

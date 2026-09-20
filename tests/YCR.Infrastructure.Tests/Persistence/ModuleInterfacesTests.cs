using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Network;
using YCR.Infrastructure;
using YCR.Infrastructure.Persistence;

namespace YCR.Infrastructure.Tests.Persistence;

public sealed class ModuleInterfacesTests
{
    [Fact]
    public void ModuleInterfaces_WithinOneScope_ResolveToSameContextInstance()
    {
        var services = new ServiceCollection()
            .AddInfrastructure("Server=(localdb)\\MSSQLLocalDB;Database=YcrScopeTest");
        using var provider = services.BuildServiceProvider();

        using var firstScope = provider.CreateScope();
        var concreteContext = firstScope.ServiceProvider.GetRequiredService<YcrDbContext>();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<INetworkDbContext>();
        var secondResolution = firstScope.ServiceProvider.GetRequiredService<INetworkDbContext>();

        using var secondScope = provider.CreateScope();
        var differentScopeContext = secondScope.ServiceProvider.GetRequiredService<INetworkDbContext>();

        Assert.Same(concreteContext, firstContext);
        Assert.Same(firstContext, secondResolution);
        Assert.NotSame(firstContext, differentScopeContext);
    }
}

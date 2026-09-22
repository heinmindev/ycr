using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Network.CreateStation;
using YCR.Application.Network.DeactivateStation;
using YCR.Application.Network.GetStation;
using YCR.Application.Network.ListStations;

namespace YCR.Application.Tests;

/// <summary>
/// The handler registration convention (ADR-0004 §Consequences).
/// </summary>
/// <remarks>
/// Scanning is convenient but silent: a handler the convention stops matching would simply never
/// be registered, and only the endpoint that resolves it would fail, at runtime, in production.
/// These tests make the convention's coverage explicit.
/// </remarks>
public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_RegistersEveryHandlerInTheAssembly()
    {
        var services = new ServiceCollection().AddApplication();
        var registered = services.Select(descriptor => descriptor.ServiceType).ToHashSet();

        var handlers = DependencyInjection.HandlerTypes().ToArray();

        Assert.NotEmpty(handlers);
        Assert.All(handlers, handler => Assert.Contains(handler, registered));
    }

    [Fact]
    public void HandlerTypes_IncludesEveryHandlerF001Defines()
    {
        // Named explicitly, so deleting a handler or renaming it out of the convention is a
        // failing test rather than a silently smaller registration.
        var handlers = DependencyInjection.HandlerTypes().ToHashSet();

        Assert.Contains(typeof(CreateStationHandler), handlers);
        Assert.Contains(typeof(DeactivateStationHandler), handlers);
        Assert.Contains(typeof(GetStationHandler), handlers);
        Assert.Contains(typeof(ListStationsHandler), handlers);
        Assert.Equal(4, handlers.Count);
    }

    [Fact]
    public void AddApplication_RegistersHandlersAsScoped()
    {
        // Handlers depend on the scoped DbContext that carries the unit of work; a singleton
        // handler would capture one context for the life of the process.
        var services = new ServiceCollection().AddApplication();

        Assert.All(
            services.Where(descriptor => DependencyInjection.HandlerTypes().Contains(descriptor.ServiceType)),
            descriptor => Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime));
    }
}

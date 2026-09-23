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
    public void HandlerTypes_IncludesEveryHandlerDefined()
    {
        // Named explicitly, so deleting a handler or renaming it out of the convention is a
        // failing test rather than a silently smaller registration.
        var handlers = DependencyInjection.HandlerTypes().ToHashSet();

        Assert.Contains(typeof(CreateStationHandler), handlers);
        Assert.Contains(typeof(DeactivateStationHandler), handlers);
        Assert.Contains(typeof(GetStationHandler), handlers);
        Assert.Contains(typeof(ListStationsHandler), handlers);
        // F-002 step 5: the sign-in slice.
        Assert.Contains(typeof(YCR.Application.Identity.Login.LoginHandler), handlers);
        Assert.Contains(typeof(YCR.Application.Identity.RefreshSession.RefreshSessionHandler), handlers);
        Assert.Contains(typeof(YCR.Application.Identity.Logout.LogoutHandler), handlers);
        Assert.Contains(typeof(YCR.Application.Identity.ChangeOwnPassword.ChangeOwnPasswordHandler), handlers);
        Assert.Contains(typeof(YCR.Application.Identity.GetCurrentUser.GetCurrentUserHandler), handlers);
        Assert.Contains(typeof(YCR.Application.Identity.ResolveSessionPrincipal.ResolveSessionPrincipalHandler), handlers);
        Assert.Equal(10, handlers.Count);
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

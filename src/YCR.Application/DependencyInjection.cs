using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using YCR.Application.Identity;
using YCR.Application.Network;
using YCR.Application.Network.Contracts;

namespace YCR.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers every handler in this assembly (ADR-0004 §Consequences).
    /// </summary>
    /// <remarks>
    /// Scanning rather than a line per handler, because ADR-0004 rejected a mediator: without
    /// scanning, adding a use case would mean editing a central registration file that has no
    /// other reason to exist, and forgetting to would fail at runtime in one endpoint.
    /// <para>
    /// Scoped, because handlers depend on the scoped <c>DbContext</c> that carries the unit of
    /// work. <c>AddApplicationHandlers_RegistersEveryHandler</c> fails if a handler is ever added
    /// that this convention misses, so the convention cannot silently stop covering the assembly.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        foreach (var handler in HandlerTypes())
        {
            services.AddScoped(handler);
        }

        // ADR-0025 item 2 (F-004 plan P14): the Network module's read contract for other modules.
        // Scoped, because it runs on the caller's scoped context and so shares its transaction.
        services.AddScoped<INetworkReader, NetworkReader>();

        // ADR-0023 item 4 (amended; S-1): each host registers the gate for its environment first.
        // This fallback blocks privileged roles, so a host that forgets fails closed.
        services.TryAddSingleton(new PrivilegedRoleGate(blocksPrivilegedRoles: true));

        return services;
    }

    /// <summary>The handler types this assembly exposes: concrete, public, named `*Handler`.</summary>
    public static IEnumerable<Type> HandlerTypes() =>
        typeof(DependencyInjection).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsPublic: true })
            .Where(type => type.Name.EndsWith("Handler", StringComparison.Ordinal));
}

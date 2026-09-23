using Microsoft.Extensions.DependencyInjection;

namespace YCR.Infrastructure.Tests.Identity;

/// <summary>
/// The Infrastructure composition with no database behind it, for the Identity services that
/// never open a connection (hashing, policy, token generation).
/// </summary>
internal static class IdentityServiceProvider
{
    public static ServiceProvider Build(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure("Server=unused.invalid;Database=unused");
        configure?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Common.Abstractions;
using YCR.Application.Network;
using YCR.Infrastructure.Identifiers;
using YCR.Infrastructure.Persistence;

namespace YCR.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<YcrDbContext>(options =>
            options.UseSqlServer(connectionString));
        services.AddScoped<INetworkDbContext>(services =>
            services.GetRequiredService<YcrDbContext>());
        services.AddSingleton<IIdGenerator, SqlServerSequentialGuidIdGenerator>();

        return services;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using YCR.Application.Common.Abstractions;
using YCR.Application.Identity;
using YCR.Application.Network;
using YCR.Infrastructure.Audit;
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
        services.AddScoped<IIdentityDbContext>(services =>
            services.GetRequiredService<YcrDbContext>());
        services.AddSingleton<IIdGenerator, SqlServerSequentialGuidIdGenerator>();

        // ADR-0018: time comes from TimeProvider, never from DateTime.UtcNow, so a test can
        // control the clock that stamps OccurredAtUtc on an append-only row.
        services.TryAddSingleton(TimeProvider.System);
        // Scoped, because it writes into the caller's unit of work.
        services.AddScoped<IAuditWriter, AuditWriter>();

        return services;
    }
}

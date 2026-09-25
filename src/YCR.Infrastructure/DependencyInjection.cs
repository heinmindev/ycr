using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using YCR.Application.Common.Abstractions;
using YCR.Application.Identity;
using YCR.Application.Identity.Abstractions;
using YCR.Application.Network;
using YCR.Application.Timetable;
using YCR.Domain.Identity;
using YCR.Infrastructure.Audit;
using YCR.Infrastructure.Identifiers;
using YCR.Infrastructure.Identity;
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
        services.AddScoped<ITimetableDbContext>(services =>
            services.GetRequiredService<YcrDbContext>());
        services.AddScoped<IIdentityDbContext>(services =>
            services.GetRequiredService<YcrDbContext>());
        services.AddSingleton<IIdGenerator, SqlServerSequentialGuidIdGenerator>();

        // ADR-0018: time comes from TimeProvider, never from DateTime.UtcNow, so a test can
        // control the clock that stamps OccurredAtUtc on an append-only row.
        services.TryAddSingleton(TimeProvider.System);
        // Scoped, because it writes into the caller's unit of work.
        services.AddScoped<IAuditWriter, AuditWriter>();

        services.AddIdentityServices();

        return services;
    }

    /// <summary>
    /// ASP.NET Core Identity via <c>AddIdentityCore</c> — no authentication scheme, no cookie
    /// (ADR-0023 item 1; plan P1, V1) — over the hand-written <see cref="StaffUserStore"/>.
    /// </summary>
    private static void AddIdentityServices(this IServiceCollection services)
    {
        services.AddIdentityCore<StaffUser>(options =>
            {
                // R20: the same alphabet UserName enforces; UserManager's user validator agrees.
                options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyz0123456789.";
                options.User.RequireUniqueEmail = false;
            })
            .AddUserStore<StaffUserStore>();

        // D2: no composition rules. Identity's default PasswordValidator is removed, so the policy
        // is exactly PasswordPolicyValidator's (length and blocklist).
        services.RemoveAll<IPasswordValidator<StaffUser>>();
        services.AddScoped<IPasswordValidator<StaffUser>, PasswordPolicyValidator>();
        services.AddSingleton<CommonPasswordBlocklist>();

        services.AddScoped<IPasswordService, PasswordService>();
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();
        services.AddScoped<IIdentityAdministratorLock, SqlServerIdentityAdministratorLock>();
    }
}

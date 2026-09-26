using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Common.Abstractions;
using YCR.Application.Common.Authorization;
using YCR.Application.Tests.Network;
using YCR.Application.Timetable.CreateService;
using YCR.Application.Timetable.GetService;
using YCR.Application.Timetable.ListServices;
using YCR.Application.Timetable.WithdrawService;
using YCR.Application.Common.Pagination;
using YCR.Domain.Common;
using YCR.Infrastructure;
using YCR.Infrastructure.Time;
using YCR.TestSupport;

namespace YCR.Application.Tests.Timetable;

/// <summary>
/// Shared setup for the service handler suites (F-004 plan §Test plan): a migrated database per
/// test, the real composition under <c>ycr_app</c>, a <see cref="TestClock"/> at
/// <b>2026-10-01 03:00 UTC</b> (09:30 in Asia/Yangon, so "today" is 2026-10-01, spec §4), and
/// <c>Time:LocalTimeZone</c> = <c>Asia/Yangon</c>.
/// </summary>
/// <remarks>
/// Stations and routes are created through the real Network handlers (the F-003 helpers this class
/// inherits), so every service test starts from rows the application itself could have written.
/// </remarks>
public abstract class TimetableHandlerTestBase(SqlServerFixture fixture) : RouteHandlerTestBase(fixture)
{
    protected static readonly DateTimeOffset DefaultNowUtc = new(2026, 10, 1, 3, 0, 0, TimeSpan.Zero);

    protected static readonly DateOnly Today = new(2026, 10, 1);

    /// <summary>The service tests' clock; the inherited route clock is not used here.</summary>
    protected TestClock ServiceClock { get; } = new(DefaultNowUtc);

    /// <summary>A service administrator, as the API would present one.</summary>
    protected static TestCurrentUser ServiceManager { get; } = new()
    {
        UserId = Guid.Parse("0199b3a0-0000-7000-8000-00000000bbbb"),
        Roles = ["RailwayAdministrator"],
        ClientIp = "198.51.100.8",
        CorrelationId = "corr-service",
        AuthorizedByPermission = Permissions.ServicesManage
    };

    /// <summary>
    /// The real composition with the service clock, the configured zone and the service manager.
    /// <paramref name="configure"/> runs last, so a test can decorate a registration.
    /// </summary>
    protected ServiceProvider BuildServiceProvider(
        ICurrentUser? currentUser = null,
        TestClock? clock = null,
        Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection()
            .AddSingleton<TimeProvider>(clock ?? ServiceClock)
            .AddInfrastructure(Database.ApplicationConnectionString)
            .AddApplication()
            .AddScoped(_ => currentUser ?? ServiceManager);
        services.Configure<LocalTimeOptions>(options => options.LocalTimeZone = "Asia/Yangon");
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    /// <summary>Route RC, closed <c>[A, B, C, D, E]</c>, and route RO, open <c>[P, Q, R, S]</c> (spec §4).</summary>
    protected static async Task<TimetableNetwork> CreateNetworkAsync(IServiceProvider provider)
    {
        var stations = new Dictionary<char, Guid>();
        foreach (var letter in "ABCDEPQRSXY")
        {
            stations[letter] = await CreateStationAsync(provider, $"S{letter}");
        }

        // Z names no station at all (S16).
        stations['Z'] = Guid.Parse("0199b3a0-0000-7000-8000-0000000000ee");

        var rc = await CreateRouteOrFailAsync(provider, "RC", isClosed: true, [.. "ABCDE".Select(letter => stations[letter])]);
        var ro = await CreateRouteOrFailAsync(provider, "RO", isClosed: false, [.. "PQRS".Select(letter => stations[letter])]);
        return new TimetableNetwork(rc, ro, stations);
    }

    protected static async Task<Result<Guid>> CreateServiceAsync(IServiceProvider provider, CreateServiceCommand command)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CreateServiceHandler>().Handle(command, CancellationToken);
    }

    protected static async Task<Guid> CreateServiceOrFailAsync(IServiceProvider provider, CreateServiceCommand command)
    {
        var result = await CreateServiceAsync(provider, command);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    protected static async Task<Result> WithdrawServiceAsync(IServiceProvider provider, Guid serviceId, DateOnly withdrawFrom)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<WithdrawServiceHandler>()
            .Handle(new WithdrawServiceCommand(serviceId, withdrawFrom), CancellationToken);
    }

    protected static async Task<Result<ServiceDto>> GetServiceAsync(IServiceProvider provider, Guid serviceId)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<GetServiceHandler>()
            .Handle(new GetServiceQuery(serviceId), CancellationToken);
    }

    protected static async Task<Result<PagedResult<ServiceSummaryDto>>> ListServicesAsync(
        IServiceProvider provider, ListServicesQuery query)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ListServicesHandler>().Handle(query, CancellationToken);
    }

    /// <summary>"Nothing written" (spec §4): no service row, no stop row, no Timetable audit event.</summary>
    protected async Task AssertNothingWrittenAsync(int services = 0, int stops = 0, int events = 0)
    {
        Assert.Equal(services, await CountAsync("[timetable].[Services]"));
        Assert.Equal(stops, await CountAsync("[timetable].[ServiceStops]"));
        Assert.Equal(events, await CountAsync("[audit].[AuditEvents] WHERE [Action] LIKE N'Timetable.%'"));
    }

    protected Task<int> EventCountAsync(string action) =>
        CountAsync($"[audit].[AuditEvents] WHERE [Action] = N'{action}'");

    protected static void AssertFailure(Result result, Error expected)
    {
        Assert.True(result.IsFailure, "expected a failure");
        Assert.Equal(expected, result.Error);
    }
}

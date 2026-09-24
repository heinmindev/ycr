using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Common.Abstractions;
using YCR.Application.Common.Authorization;
using YCR.Application.Network.CreateRoute;
using YCR.Application.Network.CreateStation;
using YCR.Application.Network.DeactivateRoute;
using YCR.Application.Network.DeactivateStation;
using YCR.Domain.Common;
using YCR.Infrastructure;
using YCR.TestSupport;

namespace YCR.Application.Tests.Network;

/// <summary>
/// Shared setup for the route handler suites: the real composition with a <see cref="TestClock"/>,
/// and stations and routes created through the real handlers (F-003 plan §Test plan).
/// </summary>
/// <remarks>
/// Stations are created through <see cref="CreateStationHandler"/>, never inserted by SQL, so
/// every route test starts from rows the application itself could have written, under
/// <c>ycr_app</c>.
/// </remarks>
public abstract class RouteHandlerTestBase(SqlServerFixture fixture) : NetworkHandlerTestBase(fixture)
{
    protected TestClock Clock { get; } = new();

    /// <summary>A route administrator, as the API would present one.</summary>
    protected static TestCurrentUser RouteManager { get; } = new()
    {
        UserId = Guid.Parse("0199b3a0-0000-7000-8000-00000000aaaa"),
        Roles = ["RailwayAdministrator"],
        ClientIp = "198.51.100.7",
        CorrelationId = "corr-route",
        AuthorizedByPermission = Permissions.RoutesManage
    };

    /// <summary><see cref="NetworkHandlerTestBase.BuildProvider"/>, with the test clock.</summary>
    protected ServiceProvider BuildRouteProvider(ICurrentUser? currentUser = null) =>
        new ServiceCollection()
            .AddSingleton<TimeProvider>(Clock)
            .AddInfrastructure(Database.ApplicationConnectionString)
            .AddApplication()
            .AddScoped(_ => currentUser ?? RouteManager)
            .BuildServiceProvider();

    protected static async Task<Guid> CreateStationAsync(IServiceProvider provider, string code, bool active = true)
    {
        Guid id;
        await using (var scope = provider.CreateAsyncScope())
        {
            var created = await scope.ServiceProvider.GetRequiredService<CreateStationHandler>()
                .Handle(new CreateStationCommand(code, $"Station {code}", "ဘူတာ"), CancellationToken);
            Assert.True(created.IsSuccess);
            id = created.Value;
        }

        if (!active)
        {
            await DeactivateStationAsync(provider, id);
        }

        return id;
    }

    protected static async Task<Guid[]> CreateStationsAsync(IServiceProvider provider, params string[] codes)
    {
        var ids = new Guid[codes.Length];
        for (var index = 0; index < codes.Length; index++)
        {
            ids[index] = await CreateStationAsync(provider, codes[index]);
        }

        return ids;
    }

    protected static async Task DeactivateStationAsync(IServiceProvider provider, Guid stationId)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<DeactivateStationHandler>()
            .Handle(new DeactivateStationCommand(stationId), CancellationToken);
        Assert.True(result.IsSuccess);
    }

    protected static async Task<Result<Guid>> CreateRouteAsync(
        IServiceProvider provider,
        string code,
        bool isClosed,
        IReadOnlyList<Guid> stationIds,
        string nameEn = "Circular Route",
        string nameMy = "မြို့ပတ်ရထားလမ်း")
    {
        await using var scope = provider.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<CreateRouteHandler>()
            .Handle(new CreateRouteCommand(code, nameEn, nameMy, isClosed, stationIds), CancellationToken);
    }

    protected static async Task<Guid> CreateRouteOrFailAsync(
        IServiceProvider provider,
        string code,
        bool isClosed,
        IReadOnlyList<Guid> stationIds)
    {
        var result = await CreateRouteAsync(provider, code, isClosed, stationIds);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    protected static async Task<Result> DeactivateRouteAsync(IServiceProvider provider, Guid routeId)
    {
        await using var scope = provider.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<DeactivateRouteHandler>()
            .Handle(new DeactivateRouteCommand(routeId), CancellationToken);
    }

    protected Task<int> CountAsync(string table) => ScalarAsync<int>($"SELECT COUNT(*) FROM {table};");
}

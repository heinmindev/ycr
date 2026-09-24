using YCR.Application.Common.Authorization;
using YCR.Domain.Common;

namespace YCR.Application.Tests.Network;

/// <summary>F-003 S12 and S23 against real SQL Server under <c>ycr_app</c>.</summary>
public sealed class DeactivateRouteHandlerTests(SqlServerFixture fixture) : RouteHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "deactivate_route";

    private const string Deactivated = "[audit].[AuditEvents] WHERE [Action] = N'Network.RouteDeactivated'";

    [Fact]
    public async Task DeactivateRoute_WhenActive_SetsInactiveAndClockTimestampAndWritesOneAuditEvent()
    {
        await using var provider = BuildRouteProvider();
        var ids = await CreateStationsAsync(provider, "AAA", "BBB");
        var routeId = await CreateRouteOrFailAsync(provider, "R1", isClosed: false, ids);
        Clock.Advance(TimeSpan.FromHours(2));

        var result = await DeactivateRouteAsync(provider, routeId);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, await ScalarAsync<int>("SELECT CAST([IsActive] AS int) FROM [network].[Routes];"));
        Assert.Equal(Clock.GetUtcNow(), await ScalarAsync<DateTimeOffset>("SELECT [DeactivatedAtUtc] FROM [network].[Routes];"));

        Assert.Equal(1, await CountAsync(Deactivated));
        Assert.Equal(routeId, await ScalarAsync<Guid>($"SELECT [SubjectId] FROM {Deactivated};"));
        Assert.Equal("Network.Route", await ScalarAsync<string>($"SELECT [SubjectType] FROM {Deactivated};"));
        Assert.Equal(Permissions.RoutesManage, await ScalarAsync<string>($"SELECT [AuthorizedByPermission] FROM {Deactivated};"));

        var stations = $"\"stations\":[{{\"position\":1,\"stationId\":\"{ids[0]}\",\"stationCode\":\"AAA\"}},"
            + $"{{\"position\":2,\"stationId\":\"{ids[1]}\",\"stationCode\":\"BBB\"}}]";
        Assert.Equal(
            "{\"code\":\"R1\",\"nameEn\":\"Circular Route\",\"nameMy\":\"မြို့ပတ်ရထားလမ်း\",\"isClosed\":false,\"isActive\":true,\"deactivatedAtUtc\":null," + stations + "}",
            await ScalarAsync<string>($"SELECT [BeforeJson] FROM {Deactivated};"));
        Assert.Equal(
            "{\"code\":\"R1\",\"nameEn\":\"Circular Route\",\"nameMy\":\"မြို့ပတ်ရထားလမ်း\",\"isClosed\":false,\"isActive\":false,\"deactivatedAtUtc\":\"2026-09-23T05:00:00+00:00\"," + stations + "}",
            await ScalarAsync<string>($"SELECT [AfterJson] FROM {Deactivated};"));
    }

    [Fact]
    public async Task DeactivateRoute_WhenInactive_ReturnsAlreadyInactiveWritesNoAuditAndKeepsTimestamp()
    {
        await using var provider = BuildRouteProvider();
        var routeId = await CreateRouteOrFailAsync(provider, "R1", isClosed: false, await CreateStationsAsync(provider, "AAA", "BBB"));
        Assert.True((await DeactivateRouteAsync(provider, routeId)).IsSuccess);
        var first = Clock.GetUtcNow();
        Clock.Advance(TimeSpan.FromHours(1));

        var second = await DeactivateRouteAsync(provider, routeId);

        Assert.True(second.IsFailure);
        Assert.Equal("Network.RouteAlreadyInactive", second.Error.Code);
        Assert.Equal(ErrorType.BusinessRule, second.Error.Type);
        Assert.Equal(1, await CountAsync(Deactivated));
        Assert.Equal(first, await ScalarAsync<DateTimeOffset>("SELECT [DeactivatedAtUtc] FROM [network].[Routes];"));
    }

    /// <summary>
    /// S23: two concurrent deactivations. <c>IsActive</c> is the concurrency token, so the loser's
    /// <c>UPDATE</c> matches no row; its <c>DeactivatedAtUtc</c> and audit row roll back with it.
    /// </summary>
    [Fact]
    public async Task DeactivateRoute_WithParallelRequests_OneSucceedsOneAlreadyInactiveOneAuditEvent()
    {
        await using var setup = BuildRouteProvider();
        var routeId = await CreateRouteOrFailAsync(setup, "R1", isClosed: false, await CreateStationsAsync(setup, "AAA", "BBB"));
        await using var first = BuildRouteProvider();
        await using var second = BuildRouteProvider();

        var results = await Task.WhenAll(DeactivateRouteAsync(first, routeId), DeactivateRouteAsync(second, routeId));

        Assert.Equal(1, results.Count(result => result.IsSuccess));
        var failure = Assert.Single(results, result => result.IsFailure);
        Assert.Equal("Network.RouteAlreadyInactive", failure.Error.Code);
        Assert.Equal(1, await CountAsync(Deactivated));
        Assert.Equal(0, await ScalarAsync<int>("SELECT CAST([IsActive] AS int) FROM [network].[Routes];"));

        // The ledger's one after-snapshot and the row agree on the timestamp: the loser wrote neither.
        Assert.Equal(
            await ScalarAsync<DateTimeOffset>("SELECT [DeactivatedAtUtc] FROM [network].[Routes];"),
            DateTimeOffset.Parse(
                await ScalarAsync<string>($"SELECT JSON_VALUE([AfterJson], '$.deactivatedAtUtc') FROM {Deactivated};") ?? string.Empty,
                System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task DeactivateRoute_KeepsRouteAndRouteStationsRows()
    {
        await using var provider = BuildRouteProvider();
        var ids = await CreateStationsAsync(provider, "AAA", "BBB", "CCC");
        var routeId = await CreateRouteOrFailAsync(provider, "R1", isClosed: true, ids);

        Assert.True((await DeactivateRouteAsync(provider, routeId)).IsSuccess);

        Assert.Equal(1, await CountAsync("[network].[Routes]"));
        Assert.Equal(
            $"1:{ids[0]}|2:{ids[1]}|3:{ids[2]}",
            await ScalarAsync<string>(
                "SELECT STRING_AGG(CONCAT([Position], ':', LOWER(CONVERT(nvarchar(36), [StationId]))), '|') WITHIN GROUP (ORDER BY [Position]) FROM [network].[RouteStations];"));
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM [network].[Routes] WHERE [Code] = N'R1' AND [IsClosed] = 1;"));
    }

    [Fact]
    public async Task DeactivateRoute_WithUnknownId_ReturnsNotFound()
    {
        await using var provider = BuildRouteProvider();

        var result = await DeactivateRouteAsync(provider, Guid.CreateVersion7());

        Assert.True(result.IsFailure);
        Assert.Equal("Network.RouteNotFound", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal(0, await CountAsync("[audit].[AuditEvents]"));
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Common.Authorization;
using YCR.Application.Network;
using YCR.Domain.Common;
using YCR.Domain.Network;
using YCR.Infrastructure.Persistence;

namespace YCR.Application.Tests.Network;

/// <summary>
/// F-003 S1, S6-S11, S21, S22, S24, S26, S29 and S30 against real SQL Server under <c>ycr_app</c>.
/// </summary>
public sealed class CreateRouteHandlerTests(SqlServerFixture fixture) : RouteHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "create_route";

    [Fact]
    public async Task CreateRoute_WithValidCommand_PersistsRoutePositionsAndOneAuditEvent()
    {
        await using var provider = BuildRouteProvider();
        var (a, b, c) = await ThreeStationsAsync(provider);

        var id = await CreateRouteOrFailAsync(provider, "LOOP1", isClosed: false, [a, b, c]);

        await using (var scope = provider.CreateAsyncScope())
        {
            var route = await scope.ServiceProvider.GetRequiredService<YcrDbContext>()
                .Routes.AsNoTracking().Include(candidate => candidate.Stations)
                .SingleAsync(CancellationToken);
            Assert.Equal(id, route.Id);
            Assert.Equal("LOOP1", route.Code.Value);
            Assert.False(route.IsClosed);
            Assert.True(route.IsActive);
            Assert.Null(route.DeactivatedAtUtc);
            Assert.Equal(Clock.GetUtcNow(), route.CreatedAtUtc);
        }

        Assert.Equal(
            $"1:{a}|2:{b}|3:{c}",
            await ScalarAsync<string>(
                "SELECT STRING_AGG(CONCAT([Position], ':', LOWER(CONVERT(nvarchar(36), [StationId]))), '|') WITHIN GROUP (ORDER BY [Position]) FROM [network].[RouteStations];"));
        Assert.Equal(0, await ScalarAsync<int>("SELECT CAST([IsClosed] AS int) FROM [network].[Routes];"));
        Assert.Equal(1, await ScalarAsync<int>("SELECT CAST([IsActive] AS int) FROM [network].[Routes];"));
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM [network].[Routes] WHERE [DeactivatedAtUtc] IS NULL;"));

        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM [audit].[AuditEvents] WHERE [Action] = N'Network.RouteCreated';"));
        Assert.Equal(NetworkAuditSubjects.Route, await ScalarAsync<string>(
            "SELECT [SubjectType] FROM [audit].[AuditEvents] WHERE [Action] = N'Network.RouteCreated';"));
        Assert.Equal(id, await ScalarAsync<Guid>(
            "SELECT [SubjectId] FROM [audit].[AuditEvents] WHERE [Action] = N'Network.RouteCreated';"));
        Assert.Equal(Permissions.RoutesManage, await ScalarAsync<string>(
            "SELECT [AuthorizedByPermission] FROM [audit].[AuditEvents] WHERE [Action] = N'Network.RouteCreated';"));
        Assert.Null(await ScalarAsync<string>(
            "SELECT [BeforeJson] FROM [audit].[AuditEvents] WHERE [Action] = N'Network.RouteCreated';"));
        Assert.Equal(
            "{\"code\":\"LOOP1\",\"nameEn\":\"Circular Route\",\"nameMy\":\"မြို့ပတ်ရထားလမ်း\",\"isClosed\":false,\"isActive\":true,\"deactivatedAtUtc\":null,"
            + $"\"stations\":[{{\"position\":1,\"stationId\":\"{a}\",\"stationCode\":\"AAA\"}},"
            + $"{{\"position\":2,\"stationId\":\"{b}\",\"stationCode\":\"BBB\"}},"
            + $"{{\"position\":3,\"stationId\":\"{c}\",\"stationCode\":\"CCC\"}}]}}",
            await ScalarAsync<string>("SELECT [AfterJson] FROM [audit].[AuditEvents] WHERE [Action] = N'Network.RouteCreated';"));
    }

    [Fact]
    public async Task CreateRoute_WithUnknownStation_ReturnsRouteStationNotFoundAndWritesNothing()
    {
        await using var provider = BuildRouteProvider();
        var (a, b, _) = await ThreeStationsAsync(provider);
        var unknown = Guid.CreateVersion7();

        var result = await CreateRouteAsync(provider, "R1", isClosed: false, [a, unknown, b]);

        AssertFailure(result, NetworkErrors.RouteStationNotFound(unknown), ErrorType.BusinessRule);
        await AssertNothingWrittenAsync();
    }

    [Fact]
    public async Task CreateRoute_WithInactiveStation_ReturnsRouteStationInactiveAndWritesNothing()
    {
        await using var provider = BuildRouteProvider();
        var (a, b, _) = await ThreeStationsAsync(provider);
        var inactive = await CreateStationAsync(provider, "OFF", active: false);

        var result = await CreateRouteAsync(provider, "R1", isClosed: false, [a, inactive, b]);

        AssertFailure(result, NetworkErrors.RouteStationInactive(inactive), ErrorType.BusinessRule);
        await AssertNothingWrittenAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateRoute_WithRepeatedStation_ReturnsRouteStationRepeatedAndWritesNothing(bool isClosed)
    {
        await using var provider = BuildRouteProvider();
        var (a, b, c) = await ThreeStationsAsync(provider);

        // S8 includes [A, B, C, A] on a closed route: a closed route never repeats its first station.
        var result = await CreateRouteAsync(provider, "R1", isClosed, [a, b, c, a]);

        AssertFailure(result, NetworkErrors.RouteStationRepeated(a), ErrorType.BusinessRule);
        await AssertNothingWrittenAsync();
    }

    [Theory]
    [InlineData(false, 1, false)]
    [InlineData(false, 2, true)]
    [InlineData(true, 2, false)]
    [InlineData(true, 3, true)]
    public async Task CreateRoute_AtAndBelowMinimumLength_CreatesOrReturnsTooFew(bool isClosed, int count, bool created)
    {
        await using var provider = BuildRouteProvider();
        var (a, b, c) = await ThreeStationsAsync(provider);

        var result = await CreateRouteAsync(provider, "R1", isClosed, new[] { a, b, c }.Take(count).ToArray());

        if (created)
        {
            Assert.True(result.IsSuccess);
            Assert.Equal(1, await CountAsync("[network].[Routes]"));
            Assert.Equal(count, await CountAsync("[network].[RouteStations]"));
        }
        else
        {
            AssertFailure(result, NetworkErrors.RouteTooFewStations, ErrorType.BusinessRule);
            await AssertNothingWrittenAsync();
        }
    }

    [Fact]
    public async Task CreateRoute_WithCodeOfActiveRoute_ReturnsConflictAndWritesNothing()
    {
        await using var provider = BuildRouteProvider();
        var (a, b, c) = await ThreeStationsAsync(provider);
        await CreateRouteOrFailAsync(provider, "R1", isClosed: false, [a, b]);

        var result = await CreateRouteAsync(provider, "R1", isClosed: false, [b, c]);

        AssertFailure(result, NetworkErrors.RouteCodeAlreadyExists("R1"), ErrorType.Conflict);
        Assert.Equal(1, await CountAsync("[network].[Routes]"));
        Assert.Equal(2, await CountAsync("[network].[RouteStations]"));
        Assert.Equal(1, await CountAsync("[audit].[AuditEvents] WHERE [Action] = N'Network.RouteCreated'"));
    }

    [Theory]
    [InlineData("R", "Name", "မြန်မာ", "Network.InvalidRouteCode")]
    [InlineData("ABCDEFGHIJK", "Name", "မြန်မာ", "Network.InvalidRouteCode")]
    [InlineData("loop", "Name", "မြန်မာ", "Network.InvalidRouteCode")]
    [InlineData("R-1", "Name", "မြန်မာ", "Network.InvalidRouteCode")]
    [InlineData("R1", "   ", "မြန်မာ", "Network.InvalidRouteName")]
    [InlineData("R1", "Name", "", "Network.InvalidRouteName")]
    [InlineData("R1", "101", "မြန်မာ", "Network.InvalidRouteName")]
    public async Task CreateRoute_WithInvalidCodeOrName_ReturnsValidationErrorAndWritesNothing(
        string code,
        string nameEn,
        string nameMy,
        string errorCode)
    {
        await using var provider = BuildRouteProvider();
        var (a, b, _) = await ThreeStationsAsync(provider);

        // "101" stands for an English name of 101 characters after trimming.
        var result = await CreateRouteAsync(
            provider, code, isClosed: false, [a, b], nameEn == "101" ? $" {new string('A', 101)} " : nameEn, nameMy);

        Assert.True(result.IsFailure);
        Assert.Equal(errorCode, result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        await AssertNothingWrittenAsync();
    }

    /// <summary>
    /// S21: two genuinely concurrent creates of one code yield exactly one route and one event.
    /// Which guard rejects the loser (the pre-check or <c>UX_Routes_Code</c>) depends on scheduling
    /// and is deliberately not asserted; both answer <c>409</c>.
    /// </summary>
    [Fact]
    public async Task CreateRoute_WithParallelDuplicateCodes_PersistsExactlyOneRouteAndOneAuditEvent()
    {
        await using var setup = BuildRouteProvider();
        var (a, b, c) = await ThreeStationsAsync(setup);
        await using var first = BuildRouteProvider();
        await using var second = BuildRouteProvider();

        var results = await Task.WhenAll(
            CreateRouteAsync(first, "RACE1", isClosed: false, [a, b]),
            CreateRouteAsync(second, "RACE1", isClosed: false, [b, c]));

        Assert.Equal(1, results.Count(result => result.IsSuccess));
        var failure = Assert.Single(results, result => result.IsFailure);
        Assert.Equal("Network.RouteCodeAlreadyExists", failure.Error.Code);
        Assert.Equal(ErrorType.Conflict, failure.Error.Type);
        Assert.Equal(1, await CountAsync("[network].[Routes]"));
        Assert.Equal(1, await CountAsync("[audit].[AuditEvents] WHERE [Action] = N'Network.RouteCreated'"));
    }

    /// <summary>S24: the losing create writes no sequence row either; one save, all or nothing.</summary>
    [Fact]
    public async Task CreateRoute_ParallelDuplicateLoser_LeavesNoRouteStationsRows()
    {
        await using var setup = BuildRouteProvider();
        var ids = await CreateStationsAsync(setup, "AAA", "BBB", "CCC", "DDD", "EEE");
        await using var first = BuildRouteProvider();
        await using var second = BuildRouteProvider();

        var results = await Task.WhenAll(
            CreateRouteAsync(first, "RACE2", isClosed: false, [ids[0], ids[1]]),
            CreateRouteAsync(second, "RACE2", isClosed: true, [ids[2], ids[3], ids[4]]));

        var winner = Assert.Single(results, result => result.IsSuccess);
        var winnerStations = results[0].IsSuccess ? 2 : 3;
        Assert.Equal(winnerStations, await CountAsync("[network].[RouteStations]"));
        Assert.Equal(0, await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM [network].[RouteStations] WHERE [RouteId] <> '{winner.Value}';"));
    }

    /// <summary>S22: actor fields come from the authenticated context, never from the command.</summary>
    [Fact]
    public async Task CreateRoute_AuditActor_ComesFromCurrentUserOnly()
    {
        var realActor = Guid.CreateVersion7();
        await using var provider = BuildRouteProvider(new TestCurrentUser
        {
            UserId = realActor,
            Roles = ["SystemAdministrator"],
            ClientIp = "203.0.113.9",
            CorrelationId = "corr-real",
            AuthorizedByPermission = Permissions.RoutesManage
        });
        var (a, b, _) = await ThreeStationsAsync(provider);

        // Every string the caller controls tries to impersonate someone else.
        var result = await CreateRouteAsync(
            provider, "ACT1", isClosed: false, [a, b], "actorUserId=00000000-0000-0000-0000-000000000001", "role=Admin");
        Assert.True(result.IsSuccess);

        const string Where = " FROM [audit].[AuditEvents] WHERE [Action] = N'Network.RouteCreated';";
        Assert.Equal(realActor, await ScalarAsync<Guid>("SELECT [ActorUserId]" + Where));
        Assert.Equal("""["SystemAdministrator"]""", await ScalarAsync<string>("SELECT [ActorRole]" + Where));
        Assert.Equal("203.0.113.9", await ScalarAsync<string>("SELECT [ClientIp]" + Where));
        Assert.Equal("corr-real", await ScalarAsync<string>("SELECT [CorrelationId]" + Where));

        // Structurally, the command has nowhere to carry an actor.
        Assert.Equal(
            ["Code", "NameEn", "NameMy", "IsClosed", "StationIds"],
            typeof(YCR.Application.Network.CreateRoute.CreateRouteCommand).GetProperties().Select(property => property.Name));
    }

    /// <summary>S26: real Myanmar Unicode, written as escapes so no editor can normalise it (F-001 R-10).</summary>
    [Fact]
    public async Task CreateRoute_WithMyanmarName_RoundTripsExactly()
    {
        const string MyanmarName = "မြို့ပတ်ရထား";
        await using var provider = BuildRouteProvider();
        var (a, b, _) = await ThreeStationsAsync(provider);

        await CreateRouteAsync(provider, "MYA1", isClosed: false, [a, b], "Circular", MyanmarName);

        await using var scope = provider.CreateAsyncScope();
        var route = await scope.ServiceProvider.GetRequiredService<YcrDbContext>()
            .Routes.AsNoTracking().SingleAsync(CancellationToken);
        Assert.Equal(MyanmarName, route.Name.My);
        Assert.Equal(MyanmarName.Length, route.Name.My.Length);
    }

    /// <summary>S29: routes may share stations, and each keeps its own positions.</summary>
    [Fact]
    public async Task CreateRoute_SharingStationsWithAnotherRoute_CreatesBothWithOwnPositions()
    {
        await using var provider = BuildRouteProvider();
        var ids = await CreateStationsAsync(provider, "AAA", "BBB", "CCC", "DDD");

        var first = await CreateRouteOrFailAsync(provider, "R1", isClosed: false, [ids[0], ids[1], ids[2]]);
        var second = await CreateRouteOrFailAsync(provider, "R2", isClosed: false, [ids[1], ids[2], ids[3]]);

        Assert.Equal(1, await ScalarAsync<int>(
            $"SELECT [Position] FROM [network].[RouteStations] WHERE [RouteId] = '{second}' AND [StationId] = '{ids[1]}';"));
        Assert.Equal(2, await ScalarAsync<int>(
            $"SELECT [Position] FROM [network].[RouteStations] WHERE [RouteId] = '{first}' AND [StationId] = '{ids[1]}';"));
        Assert.Equal(6, await CountAsync("[network].[RouteStations]"));
    }

    /// <summary>S30: a deactivated route keeps its row, so its code is never reused (R14).</summary>
    [Fact]
    public async Task CreateRoute_WithCodeOfDeactivatedRoute_ReturnsConflictAndWritesNothing()
    {
        await using var provider = BuildRouteProvider();
        var (a, b, c) = await ThreeStationsAsync(provider);
        var original = await CreateRouteOrFailAsync(provider, "R1", isClosed: false, [a, b]);
        Assert.True((await DeactivateRouteAsync(provider, original)).IsSuccess);

        var result = await CreateRouteAsync(provider, "R1", isClosed: false, [b, c]);

        AssertFailure(result, NetworkErrors.RouteCodeAlreadyExists("R1"), ErrorType.Conflict);
        Assert.Equal(1, await CountAsync("[network].[Routes]"));
        Assert.Equal(2, await CountAsync("[network].[RouteStations]"));
        Assert.Equal(1, await CountAsync("[audit].[AuditEvents] WHERE [Action] = N'Network.RouteCreated'"));
    }

    private static async Task<(Guid A, Guid B, Guid C)> ThreeStationsAsync(IServiceProvider provider)
    {
        var ids = await CreateStationsAsync(provider, "AAA", "BBB", "CCC");
        return (ids[0], ids[1], ids[2]);
    }

    private static void AssertFailure(Result<Guid> result, Error expected, ErrorType type)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(expected, result.Error);
        Assert.Equal(type, result.Error.Type);
    }

    private async Task AssertNothingWrittenAsync()
    {
        Assert.Equal(0, await CountAsync("[network].[Routes]"));
        Assert.Equal(0, await CountAsync("[network].[RouteStations]"));
        Assert.Equal(0, await CountAsync("[audit].[AuditEvents] WHERE [Action] = N'Network.RouteCreated'"));
    }
}

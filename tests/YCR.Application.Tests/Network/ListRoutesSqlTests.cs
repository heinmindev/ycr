using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YCR.Application.Common.Abstractions;
using YCR.Application.Network.GetRoute;
using YCR.Application.Network.ListRoutes;
using YCR.Infrastructure;
using YCR.Infrastructure.Persistence;

namespace YCR.Application.Tests.Network;

/// <summary>
/// Proves the route queries and the deactivation write shape their work in SQL, asserted against
/// the generated SQL rather than inferred from results (the <c>ListStationsSqlTests</c> pattern).
/// </summary>
/// <remarks>
/// F-003 plan verifications plan-V1 (deactivation writes one two-column <c>UPDATE</c> and nothing
/// to <c>RouteStations</c>), plan-V2 (one statement for a route's stations) and plan-V3 (the list
/// counts, pages and counts stations in SQL).
/// </remarks>
public sealed class ListRoutesSqlTests(SqlServerFixture fixture) : RouteHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "route_sql";

    /// <summary>plan-V3 / S3.</summary>
    [Fact]
    public async Task ListRoutesSql_PagesCountsAndStationCountsInSql()
    {
        await using var setup = BuildRouteProvider();
        var ids = await CreateStationsAsync(setup, "AAA", "BBB", "CCC");
        await CreateRouteOrFailAsync(setup, "R1", isClosed: false, [ids[0], ids[1]]);
        await CreateRouteOrFailAsync(setup, "R2", isClosed: true, ids);
        await CreateRouteOrFailAsync(setup, "R3", isClosed: false, [ids[1], ids[2]]);

        var commands = new List<string>();
        await using var provider = BuildLoggingProvider(commands);
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<ListRoutesHandler>()
            .Handle(new ListRoutesQuery(Page: 2, PageSize: 1), CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.TotalCount);
        var only = Assert.Single(result.Value.Items);
        Assert.Equal("R2", only.Code);
        Assert.Equal(3, only.StationCount);

        // Exactly two round trips: the count, then the page with its station counts.
        Assert.Equal(2, commands.Count);

        var count = Assert.Single(commands, command => !command.Contains("OFFSET", StringComparison.Ordinal));
        Assert.Contains("COUNT(*)", count, StringComparison.Ordinal);
        Assert.DoesNotContain("RouteStations", count, StringComparison.Ordinal);

        var page = Assert.Single(commands, command => command.Contains("OFFSET", StringComparison.Ordinal));
        Assert.Contains("ORDER BY", page, StringComparison.Ordinal);
        Assert.Contains("FETCH NEXT", page, StringComparison.Ordinal);
        // stationCount is a correlated COUNT over RouteStations inside the page query, so no
        // sequence row crosses the wire.
        Assert.Matches(new Regex(@"COUNT\(\*\)[\s\S]*FROM \[network\]\.\[RouteStations\]"), page);
        Assert.DoesNotContain("[Position]", page, StringComparison.Ordinal);
    }

    /// <summary>plan-V2: a route read is two statements, and the stations are one of them.</summary>
    [Fact]
    public async Task GetRouteSql_ReadsTheRouteAndItsStationsInTwoStatements()
    {
        await using var setup = BuildRouteProvider();
        var ids = await CreateStationsAsync(setup, "AAA", "BBB", "CCC");
        var routeId = await CreateRouteOrFailAsync(setup, "R1", isClosed: false, ids);

        var commands = new List<string>();
        await using var provider = BuildLoggingProvider(commands);
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<GetRouteHandler>()
            .Handle(new GetRouteQuery(routeId), CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Stations.Count);
        Assert.Equal(2, commands.Count);

        var stations = Assert.Single(commands, command => command.Contains("[RouteStations]", StringComparison.Ordinal));
        Assert.Contains("JOIN [network].[Stations]", stations, StringComparison.Ordinal);
        Assert.Contains("ORDER BY", stations, StringComparison.Ordinal);
    }

    /// <summary>
    /// plan-V1 / R10 / S23: deactivation is one <c>UPDATE</c> of the two granted columns, guarded
    /// by the <c>IsActive</c> concurrency token, and writes nothing to <c>RouteStations</c>.
    /// </summary>
    [Fact]
    public async Task DeactivateRouteSql_UpdatesOnlyIsActiveAndDeactivatedAtAndNoRouteStations()
    {
        await using var setup = BuildRouteProvider();
        var ids = await CreateStationsAsync(setup, "AAA", "BBB");
        var routeId = await CreateRouteOrFailAsync(setup, "R1", isClosed: false, ids);

        var commands = new List<string>();
        await using var provider = BuildLoggingProvider(commands);

        Assert.True((await DeactivateRouteAsync(provider, routeId)).IsSuccess);

        var all = string.Join("\n", commands);
        var update = Assert.Single(Regex.Matches(all, @"UPDATE \[network\]\.\[Routes\] SET (?<set>[^\n]*?)\s*OUTPUT[\s\S]*?WHERE (?<where>[^;]*);"));
        Assert.Equal(
            ["[DeactivatedAtUtc]", "[IsActive]"],
            Regex.Matches(update.Groups["set"].Value, @"\[[A-Za-z]+\](?= =)").Select(match => match.Value).Order(StringComparer.Ordinal));
        Assert.Matches(@"^\[Id\] = @\w+ AND \[IsActive\] = @\w+$", update.Groups["where"].Value);

        Assert.DoesNotMatch(@"(INSERT INTO|UPDATE|DELETE FROM) \[network\]\.\[RouteStations\]", all);
        Assert.Contains("INSERT INTO [audit].[AuditEvents]", all, StringComparison.Ordinal);
    }

    /// <summary>
    /// The real composition, with the test clock and a SQL log. <c>ConfigureDbContext</c> adds the
    /// log to the options <c>AddInfrastructure</c> registers, so the context is otherwise the
    /// production one.
    /// </summary>
    private ServiceProvider BuildLoggingProvider(List<string> commands) =>
        new ServiceCollection()
            .AddSingleton<TimeProvider>(Clock)
            .AddInfrastructure(Database.ApplicationConnectionString)
            .ConfigureDbContext<YcrDbContext>(options => options.LogTo(
                commands.Add,
                [DbLoggerCategory.Database.Command.Name],
                LogLevel.Information))
            .AddApplication()
            .AddScoped<ICurrentUser>(_ => RouteManager)
            .BuildServiceProvider();
}

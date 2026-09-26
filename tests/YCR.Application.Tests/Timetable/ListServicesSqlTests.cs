using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YCR.Application.Timetable.ListServices;
using YCR.Infrastructure.Persistence;
using static YCR.Application.Tests.Timetable.TimetableTestData;

namespace YCR.Application.Tests.Timetable;

/// <summary>
/// Plan V3: the service list counts, pages, orders and counts stops in SQL, asserted against the
/// generated SQL rather than inferred from results (the <c>ListRoutesSqlTests</c> pattern).
/// </summary>
public sealed class ListServicesSqlTests(SqlServerFixture fixture) : TimetableHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "service_sql";

    [Fact]
    public async Task ListServicesSql_PagesCountsAndStopCountsInSql()
    {
        await using var setup = BuildServiceProvider();
        var network = await CreateNetworkAsync(setup);
        await CreateServiceOrFailAsync(setup, Command(network, "AC", code: "S1"));
        await CreateServiceOrFailAsync(setup, Command(network, "ABCD", code: "S2"));
        await CreateServiceOrFailAsync(setup, Command(network, "QR", code: "S3", routeId: network.Ro));

        var commands = new List<string>();
        await using var provider = BuildServiceProvider(configure: services => services.ConfigureDbContext<YcrDbContext>(
            options => options.LogTo(commands.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information)));

        var result = await ListServicesAsync(provider, new ListServicesQuery(Page: 2, PageSize: 1));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.TotalCount);
        var only = Assert.Single(result.Value.Items);
        Assert.Equal("S2", only.Code);
        Assert.Equal(4, only.StopCount);
        Assert.Equal("RC", only.RouteCode);

        // Three round trips: the count, the page with its stop counts, and one Network contract
        // read for the page's route codes.
        Assert.Equal(3, commands.Count);

        var count = Assert.Single(commands, command => command.Contains("FROM [timetable].[Services]", StringComparison.Ordinal)
            && !command.Contains("OFFSET", StringComparison.Ordinal));
        Assert.Contains("COUNT(*)", count, StringComparison.Ordinal);
        Assert.DoesNotContain("ServiceStops", count, StringComparison.Ordinal);

        var page = Assert.Single(commands, command => command.Contains("OFFSET", StringComparison.Ordinal));
        Assert.Matches(new Regex(@"ORDER BY \[\w+\]\.\[Code\], \[\w+\]\.\[EffectiveFrom\], \[\w+\]\.\[Id\]"), page);
        Assert.Contains("FETCH NEXT", page, StringComparison.Ordinal);
        // stopCount is a correlated COUNT over ServiceStops inside the page query, so no stop row
        // crosses the wire.
        Assert.Matches(new Regex(@"COUNT\(\*\)[\s\S]*FROM \[timetable\]\.\[ServiceStops\]"), page);
        Assert.DoesNotContain("[Position]", page, StringComparison.Ordinal);

        Assert.Single(commands, command => command.Contains("FROM [network].[Routes]", StringComparison.Ordinal));
    }
}

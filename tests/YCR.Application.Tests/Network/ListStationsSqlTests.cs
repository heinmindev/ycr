using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YCR.Application.Network.CreateStation;
using YCR.Application.Network.GetStation;
using YCR.Application.Network.ListStations;
using YCR.Infrastructure.Persistence;

namespace YCR.Application.Tests.Network;

/// <summary>
/// Proves the station queries shape their work in SQL rather than in memory.
/// </summary>
/// <remarks>
/// Asserted against the generated SQL, not inferred from the results. A query that fetched the
/// whole table and paged it client-side would return exactly the same rows as one that pages on
/// the server, so every behavioural test would still pass while the endpoint quietly became
/// unusable at scale. This is the only kind of test that can tell the two apart.
/// <para>
/// EF also warns rather than fails on client-side evaluation of a <c>Where</c>, so a regression
/// here would otherwise surface as a log line nobody reads.
/// </para>
/// </remarks>
public sealed class ListStationsSqlTests(SqlServerFixture fixture) : NetworkHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "list_stations_sql";

    [Fact]
    public async Task Handle_WithFiveStations_CountsAndPagesInSql()
    {
        await SeedAsync("AAA", "BBB", "CCC", "DDD", "EEE");

        var commands = new List<string>();
        await using var context = NewLoggingContext(commands);

        var result = await new ListStationsHandler(context)
            .Handle(new ListStationsQuery(Page: 2, PageSize: 2), CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(["CCC", "DDD"], result.Value.Items.Select(station => station.Code));
        Assert.Equal(5, result.Value.TotalCount);

        // Exactly two round trips: the count, then the page.
        Assert.Equal(2, commands.Count);

        var count = Assert.Single(commands, command => command.Contains("COUNT(*)", StringComparison.Ordinal));
        Assert.DoesNotContain("OFFSET", count, StringComparison.Ordinal);

        var page = Assert.Single(commands, command => !command.Contains("COUNT(*)", StringComparison.Ordinal));
        Assert.Contains("ORDER BY", page, StringComparison.Ordinal);
        Assert.Contains("OFFSET", page, StringComparison.Ordinal);
        Assert.Contains("FETCH NEXT", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_WithOneStation_SelectsOnlyTheProjectedColumns()
    {
        await SeedAsync("AAA");

        var commands = new List<string>();
        await using var context = NewLoggingContext(commands);

        await new ListStationsHandler(context).Handle(new ListStationsQuery(1, 10), CancellationToken);

        var page = Assert.Single(commands, command => !command.Contains("COUNT(*)", StringComparison.Ordinal));

        // The projection is real: SELECT names columns rather than pulling the entity.
        Assert.Contains("[Code]", page, StringComparison.Ordinal);
        Assert.Contains("[NameEn]", page, StringComparison.Ordinal);
        Assert.Contains("[NameMy]", page, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT *", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetStation_WithSeededStation_ProjectsAndFiltersInSql()
    {
        var stationId = await SeedAsync("YGN");

        var commands = new List<string>();
        await using var context = NewLoggingContext(commands);

        var result = await new GetStationHandler(context)
            .Handle(new GetStationQuery(stationId), CancellationToken);

        Assert.True(result.IsSuccess);
        var command = Assert.Single(commands);
        Assert.Contains("WHERE", command, StringComparison.Ordinal);
        // TOP(1) rather than fetching every row and taking the first in memory.
        Assert.Contains("TOP(1)", command, StringComparison.Ordinal);
    }

    /// <summary>
    /// A context that records the SQL it sends. Built by hand rather than resolved from DI
    /// because <c>AddInfrastructure</c> deliberately exposes no logging hook — this is a test
    /// concern, not a production one.
    /// </summary>
    private YcrDbContext NewLoggingContext(List<string> commands) =>
        new(new DbContextOptionsBuilder<YcrDbContext>()
            .UseSqlServer(Database.ApplicationConnectionString)
            .LogTo(
                line => commands.Add(line),
                [DbLoggerCategory.Database.Command.Name],
                LogLevel.Information)
            .Options);

    private async Task<Guid> SeedAsync(params string[] codes)
    {
        await using var provider = BuildProvider();
        var lastId = Guid.Empty;

        foreach (var code in codes)
        {
            await using var scope = provider.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<CreateStationHandler>()
                .Handle(new CreateStationCommand(code, $"Station {code}", "မြန်မာ"), CancellationToken);

            Assert.True(result.IsSuccess);
            lastId = result.Value;
        }

        return lastId;
    }
}

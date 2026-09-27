using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YCR.Infrastructure.Persistence;
using YCR.TestSupport;
using static YCR.Application.Tests.Timetable.ScheduleTestData;

namespace YCR.Application.Tests.Timetable;

/// <summary>
/// Plan V1, V9 (SV51): each transition is one <c>UPDATE</c> of <c>Status</c> and its one instant,
/// guarded by the original <c>Status</c>, and nothing is written to the child tables — exactly the
/// columns <c>ycr_app</c> may update. The handlers run as <c>ycr_app</c>, so a stray write would
/// also fail on the grants.
/// </summary>
public sealed class ScheduleVersionTransitionSqlTests(SqlServerFixture fixture) : ScheduleHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "schedule_transition_sql";

    [Fact]
    public async Task Transitions_UpdateOnlyStatusAndOneInstantAndNoChildRows()
    {
        await using var setup = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(setup);
        var toPublish = await CreateVersionOrFailAsync(setup, Version("2026-10-05", [ValidS1(net.S1)]));
        var toDiscard = await CreateVersionOrFailAsync(setup, Version("2026-10-06", [ValidS1(net.S1)]));

        var commands = new List<string>();
        await using var provider = BuildScheduleProvider(configure: services => services.ConfigureDbContext<YcrDbContext>(
            options => options.LogTo(commands.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information)));

        Assert.True((await PublishAsync(provider, toPublish.Id)).IsSuccess);
        Assert.True((await DiscardAsync(provider, toDiscard.Id)).IsSuccess);
        Assert.True((await CancelAsync(provider, toPublish.Id)).IsSuccess);

        var all = string.Join("\n", commands);
        var updates = Regex.Matches(all, @"UPDATE \[timetable\]\.\[ScheduleVersions\] SET (?<set>[^\n]*?)\s*OUTPUT[\s\S]*?WHERE (?<where>[^;]*);");
        Assert.Equal(3, updates.Count);
        string[][] expectedSets =
        [
            ["[PublishedAtUtc]", "[Status]"],
            ["[DiscardedAtUtc]", "[Status]"],
            ["[CancelledAtUtc]", "[Status]"],
        ];
        for (var index = 0; index < 3; index++)
        {
            Assert.Equal(
                expectedSets[index],
                Regex.Matches(updates[index].Groups["set"].Value, @"\[[A-Za-z]+\](?= =)").Select(match => match.Value).Order(StringComparer.Ordinal));
            Assert.Matches(@"^\[Id\] = @\w+ AND \[Status\] = @\w+$", updates[index].Groups["where"].Value);
        }

        Assert.DoesNotMatch(@"(INSERT INTO|UPDATE|DELETE FROM) \[timetable\]\.\[(ScheduleVersionServices|ScheduleStopTimes)\]", all);
        Assert.DoesNotMatch(@"DELETE FROM \[timetable\]", all);
        Assert.DoesNotMatch(@"INSERT INTO \[timetable\]\.\[ScheduleVersions\]", all);
        Assert.Equal(3, Regex.Matches(all, "sp_getapplock").Count);
        Assert.Equal(3, Regex.Matches(all, Regex.Escape("INSERT INTO [audit].[AuditEvents]")).Count);
    }
}

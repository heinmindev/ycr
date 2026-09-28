using YCR.Application.Timetable;
using YCR.Domain.Common;
using YCR.Domain.Timetable;
using YCR.TestSupport;
using static YCR.Application.Tests.Timetable.ScheduleTestData;

namespace YCR.Application.Tests.Timetable;

/// <summary>
/// F-005 discard scenarios against real SQL Server under <c>ycr_app</c>, today 2026-10-01
/// (Asia/Yangon): SV35; R25, R28, R47.
/// </summary>
public sealed class DiscardScheduleVersionHandlerTests(SqlServerFixture fixture) : ScheduleHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "discard_schedule";

    /// <summary>SV35: a draft is discarded; the rows are kept and readable; one event.</summary>
    [Fact]
    public async Task DiscardScheduleVersion_Draft_SetsDiscardedKeepsRowsAndWritesOneEvent()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var draft = await CreateVersionOrFailAsync(provider, Version("2026-10-05", [ValidS1(net.S1)]));
        ServiceClock.Advance(TimeSpan.FromMinutes(7));

        var result = await DiscardAsync(provider, draft.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await CountAsync(
            $"""
            [timetable].[ScheduleVersions] WHERE [Id] = '{draft.Id}' AND [Status] = N'Discarded'
              AND [DiscardedAtUtc] = '2026-10-01T03:07:00+00:00' AND [PublishedAtUtc] IS NULL AND [CancelledAtUtc] IS NULL
            """));
        await AssertNoScheduleRowsAsync(versions: 1, entries: 1, stopTimes: 3, events: 2);
        Assert.Equal(1, await EventCountAsync(TimetableAuditActions.ScheduleVersionDiscarded));
    }

    /// <summary>SV35: Published, Cancelled, Discarded → 422 NotDraft; unknown → 404; nothing written.</summary>
    [Theory]
    [InlineData("Published")]
    [InlineData("Cancelled")]
    [InlineData("Discarded")]
    [InlineData("unknown")]
    public async Task DiscardScheduleVersion_WhenNotDraft_ReturnsNotDraft(string state)
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var id = (await CreateVersionOrFailAsync(provider, Version("2026-10-05", [ValidS1(net.S1)]))).Id;
        switch (state)
        {
            case "Published":
                Assert.True((await PublishAsync(provider, id)).IsSuccess);
                break;
            case "Cancelled":
                Assert.True((await PublishAsync(provider, id)).IsSuccess);
                Assert.True((await CancelAsync(provider, id)).IsSuccess);
                break;
            case "Discarded":
                Assert.True((await DiscardAsync(provider, id)).IsSuccess);
                break;
            default:
                id = Guid.Parse("0199b3a0-0000-7000-8000-0000000000ab");
                break;
        }

        var events = await CountAsync("[audit].[AuditEvents]");

        var result = await DiscardAsync(provider, id);

        AssertFailure(result, state == "unknown" ? TimetableErrors.ScheduleVersionNotFound : TimetableErrors.ScheduleVersionNotDraft);
        Assert.Equal(events, await CountAsync("[audit].[AuditEvents]"));
        if (state != "unknown")
        {
            Assert.Equal(state, await StatusOfAsync(id));
        }
    }

    /// <summary>R47: a status changed behind the lock; 409, nothing written, the direct value stands.</summary>
    [Fact]
    public async Task DiscardScheduleVersion_WhenStatusChangesBehindTheLock_Returns409ChangedConcurrently()
    {
        await using var setup = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(setup);
        var draft = await CreateVersionOrFailAsync(setup, Version("2026-10-05", [ValidS1(net.S1)]));
        var migrator = Database.MigratorConnectionString;
        await using var provider = BuildScheduleProvider(configure: services => AuditRecordHook.Register(services, action =>
        {
            if (action == TimetableAuditActions.ScheduleVersionDiscarded)
            {
                PublishScheduleVersionHandlerTests.ExecuteAsMigrator(
                    migrator,
                    $"UPDATE [timetable].[ScheduleVersions] SET [Status] = N'Published', [PublishedAtUtc] = '2026-10-01T03:30:00+00:00' WHERE [Id] = '{draft.Id}';");
            }
        }));

        var result = await DiscardAsync(provider, draft.Id);

        AssertFailure(result, TimetableErrors.ScheduleVersionChangedConcurrently);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Published", await StatusOfAsync(draft.Id));
        Assert.Equal(0, await EventCountAsync(TimetableAuditActions.ScheduleVersionDiscarded));
    }
}

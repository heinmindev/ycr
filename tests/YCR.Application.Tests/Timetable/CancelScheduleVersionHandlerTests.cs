using YCR.Application.Timetable;
using YCR.Domain.Common;
using YCR.Domain.Timetable;
using YCR.TestSupport;
using static YCR.Application.Tests.Timetable.ScheduleTestData;
using static YCR.Application.Tests.Timetable.TimetableTestData;

namespace YCR.Application.Tests.Timetable;

/// <summary>
/// F-005 cancellation scenarios against real SQL Server under <c>ycr_app</c>, today 2026-10-01
/// (Asia/Yangon): SV31, SV33, SV34; R34, R47.
/// </summary>
public sealed class CancelScheduleVersionHandlerTests(SqlServerFixture fixture) : ScheduleHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "cancel_schedule";

    /// <summary>SV31: cancelled before its start date; PublishedAtUtc kept, rows kept, one event.</summary>
    [Fact]
    public async Task CancelScheduleVersion_BeforeItsStart_SetsCancelledKeepsRowsAndWritesOneEvent()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var v3 = await PublishedVersionAsync(provider, "2026-11-02", [ValidS1(net.S1)]);
        ServiceClock.Advance(TimeSpan.FromMinutes(10));

        var result = await CancelAsync(provider, v3);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await CountAsync(
            $"""
            [timetable].[ScheduleVersions] WHERE [Id] = '{v3}' AND [Status] = N'Cancelled'
              AND [PublishedAtUtc] = '2026-10-01T03:00:00+00:00' AND [CancelledAtUtc] = '2026-10-01T03:10:00+00:00' AND [DiscardedAtUtc] IS NULL
            """));
        await AssertNoScheduleRowsAsync(versions: 1, entries: 1, stopTimes: 3, events: 3);
        Assert.Equal(1, await EventCountAsync(TimetableAuditActions.ScheduleVersionCancelled));
    }

    /// <summary>SV33: on its start date, or after it, a published version cannot be cancelled; nothing written.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task CancelScheduleVersion_OnOrAfterItsStart_ReturnsAlreadyEffectiveAndWritesNothing(int daysLater)
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var early = await CreateServiceOrFailAsync(provider, Command(net.Network, code: "S401", effectiveFrom: "2026-09-01"));
        var v4 = await PublishedVersionAsync(provider, "2026-10-01", [ValidThreeStop(early)]);
        ServiceClock.Advance(TimeSpan.FromDays(daysLater));

        var result = await CancelAsync(provider, v4);

        AssertFailure(result, TimetableErrors.ScheduleVersionAlreadyEffective);
        Assert.Equal("Published", await StatusOfAsync(v4));
        Assert.Equal(0, await EventCountAsync(TimetableAuditActions.ScheduleVersionCancelled));
    }

    /// <summary>SV34: Draft, Discarded, Cancelled → 422 NotPublished; unknown → 404.</summary>
    [Theory]
    [InlineData("Draft")]
    [InlineData("Discarded")]
    [InlineData("Cancelled")]
    [InlineData("unknown")]
    public async Task CancelScheduleVersion_WhenNotPublishedOrUnknown_ReturnsNotPublishedOrNotFound(string state)
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var id = (await CreateVersionOrFailAsync(provider, Version("2026-10-05", [ValidS1(net.S1)]))).Id;
        switch (state)
        {
            case "Discarded":
                Assert.True((await DiscardAsync(provider, id)).IsSuccess);
                break;
            case "Cancelled":
                Assert.True((await PublishAsync(provider, id)).IsSuccess);
                Assert.True((await CancelAsync(provider, id)).IsSuccess);
                break;
            case "unknown":
                id = Guid.Parse("0199b3a0-0000-7000-8000-0000000000ab");
                break;
        }

        var events = await CountAsync("[audit].[AuditEvents]");

        var result = await CancelAsync(provider, id);

        AssertFailure(result, state == "unknown" ? TimetableErrors.ScheduleVersionNotFound : TimetableErrors.ScheduleVersionNotPublished);
        Assert.Equal(events, await CountAsync("[audit].[AuditEvents]"));
        if (state != "unknown")
        {
            Assert.Equal(state, await StatusOfAsync(id));
        }
    }

    /// <summary>R47: a status changed behind the lock; 409, nothing written, the direct value stands.</summary>
    [Fact]
    public async Task CancelScheduleVersion_WhenStatusChangesBehindTheLock_Returns409ChangedConcurrently()
    {
        await using var setup = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(setup);
        var v1 = await PublishedVersionAsync(setup, "2026-11-02", [ValidS1(net.S1)]);
        var migrator = Database.MigratorConnectionString;
        await using var provider = BuildScheduleProvider(configure: services => AuditRecordHook.Register(services, action =>
        {
            if (action == TimetableAuditActions.ScheduleVersionCancelled)
            {
                PublishScheduleVersionHandlerTests.ExecuteAsMigrator(
                    migrator,
                    $"UPDATE [timetable].[ScheduleVersions] SET [Status] = N'Cancelled', [CancelledAtUtc] = '2026-10-01T03:30:00+00:00' WHERE [Id] = '{v1}';");
            }
        }));

        var result = await CancelAsync(provider, v1);

        AssertFailure(result, TimetableErrors.ScheduleVersionChangedConcurrently);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(1, await CountAsync($"[timetable].[ScheduleVersions] WHERE [Id] = '{v1}' AND [CancelledAtUtc] = '2026-10-01T03:30:00+00:00'"));
        Assert.Equal(0, await EventCountAsync(TimetableAuditActions.ScheduleVersionCancelled));
    }
}

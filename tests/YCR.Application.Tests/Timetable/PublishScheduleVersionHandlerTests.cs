using System.Text.Json;
using Microsoft.Data.SqlClient;
using YCR.Application.Timetable;
using YCR.Domain.Common;
using YCR.Domain.Timetable;
using YCR.TestSupport;
using static YCR.Application.Tests.Timetable.ScheduleTestData;
using static YCR.Application.Tests.Timetable.TimetableTestData;

namespace YCR.Application.Tests.Timetable;

/// <summary>
/// F-005 publication scenarios against real SQL Server under <c>ycr_app</c>, today 2026-10-01
/// (Asia/Yangon): SV17, SV20, SV21, SV23-SV26, SV55, SV56; R4, R17, R22, R30, R31, R47, R50; plan V8.
/// </summary>
public sealed class PublishScheduleVersionHandlerTests(SqlServerFixture fixture) : ScheduleHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "publish_schedule";

    /// <summary>SV23, R4: the author publishes; one update, children untouched, one event before/after.</summary>
    [Fact]
    public async Task PublishScheduleVersion_Draft_UpdatesStatusAndInstantAndWritesOneEvent()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var draft = await CreateVersionOrFailAsync(provider, Version("2026-10-05", [ValidS1(net.S1)]));
        ServiceClock.Advance(TimeSpan.FromMinutes(5));

        var result = await PublishAsync(provider, draft.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await CountAsync(
            $"""
            [timetable].[ScheduleVersions] WHERE [Id] = '{draft.Id}' AND [Status] = N'Published'
              AND [PublishedAtUtc] = '2026-10-01T03:05:00+00:00' AND [DiscardedAtUtc] IS NULL AND [CancelledAtUtc] IS NULL
            """));
        await AssertNoScheduleRowsAsync(versions: 1, entries: 1, stopTimes: 3, events: 2);
        Assert.Equal(1, await EventCountAsync(TimetableAuditActions.ScheduleVersionPublished));

        // The author (the same schedule manager) is the publisher; the event carries before and after.
        Assert.Equal(ScheduleManager.UserId, await ScalarAsync<Guid>(
            "SELECT [ActorUserId] FROM [audit].[AuditEvents] WHERE [Action] = N'Timetable.ScheduleVersionPublished';"));
        using var before = JsonDocument.Parse((await ScalarAsync<string>(
            "SELECT [BeforeJson] FROM [audit].[AuditEvents] WHERE [Action] = N'Timetable.ScheduleVersionPublished';"))!);
        using var after = JsonDocument.Parse((await ScalarAsync<string>(
            "SELECT [AfterJson] FROM [audit].[AuditEvents] WHERE [Action] = N'Timetable.ScheduleVersionPublished';"))!);
        Assert.Equal("Draft", before.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, before.RootElement.GetProperty("publishedAtUtc").ValueKind);
        Assert.Equal("Published", after.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, after.RootElement.GetProperty("number").GetInt32());
        Assert.Equal("2026-10-05", after.RootElement.GetProperty("effectiveFrom").GetString());
        Assert.False(after.RootElement.TryGetProperty("services", out _));
    }

    /// <summary>SV24, R22: a second published start date is 409 until the first is cancelled.</summary>
    [Fact]
    public async Task PublishScheduleVersion_WithAPublishedStartDate_Returns409ThenSucceedsOnceThatIsCancelled()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var v1 = await PublishedVersionAsync(provider, "2026-10-05", [ValidS1(net.S1)]);
        var draft = await CreateVersionOrFailAsync(provider, Version("2026-10-05", [ValidThreeStop(net.S2)]));

        var taken = await PublishAsync(provider, draft.Id);

        AssertFailure(taken, TimetableErrors.ScheduleVersionEffectiveFromTaken);
        Assert.Equal(ErrorType.Conflict, taken.Error.Type);
        Assert.Equal("Draft", await StatusOfAsync(draft.Id));
        Assert.Equal(1, await EventCountAsync(TimetableAuditActions.ScheduleVersionPublished));

        Assert.True((await CancelAsync(provider, v1)).IsSuccess);
        Assert.True((await PublishAsync(provider, draft.Id)).IsSuccess);
        Assert.Equal("Published", await StatusOfAsync(draft.Id));
        Assert.Equal(2, await EventCountAsync(TimetableAuditActions.ScheduleVersionPublished));
    }

    /// <summary>SV25: Published, Cancelled, Discarded → 422 NotDraft; unknown → 404; nothing written.</summary>
    [Theory]
    [InlineData("Published")]
    [InlineData("Cancelled")]
    [InlineData("Discarded")]
    [InlineData("unknown")]
    public async Task PublishScheduleVersion_WhenNotDraftOrUnknown_ReturnsNotDraftOrNotFound(string state)
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var draft = await CreateVersionOrFailAsync(provider, Version("2026-10-05", [ValidS1(net.S1)]));
        var id = draft.Id;
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

        var result = await PublishAsync(provider, id);

        AssertFailure(result, state == "unknown" ? TimetableErrors.ScheduleVersionNotFound : TimetableErrors.ScheduleVersionNotDraft);
        Assert.Equal(events, await CountAsync("[audit].[AuditEvents]"));
        if (state != "unknown")
        {
            Assert.Equal(state, await StatusOfAsync(id));
        }
    }

    /// <summary>SV26: two drafts with one start date published in parallel serialise; one 204, one 409, one event.</summary>
    [Fact]
    public async Task PublishScheduleVersion_TwoSameStartDateInParallel_OneSucceedsOneConflicts()
    {
        await using var setup = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(setup);
        var a = await CreateVersionOrFailAsync(setup, Version("2026-10-05", [ValidS1(net.S1)]));
        var b = await CreateVersionOrFailAsync(setup, Version("2026-10-05", [ValidThreeStop(net.S2)]));
        await using var first = BuildScheduleProvider();
        await using var second = BuildScheduleProvider();

        var results = await Task.WhenAll(PublishAsync(first, a.Id), PublishAsync(second, b.Id));

        Assert.Single(results, result => result.IsSuccess);
        var refused = Assert.Single(results, result => result.IsFailure);
        AssertFailure(refused, TimetableErrors.ScheduleVersionEffectiveFromTaken);
        Assert.Equal(1, await CountAsync("[timetable].[ScheduleVersions] WHERE [Status] = N'Published'"));
        Assert.Equal(1, await EventCountAsync(TimetableAuditActions.ScheduleVersionPublished));
    }

    /// <summary>SV20: once its start date has passed a draft cannot be published; it stays a readable draft and can be discarded.</summary>
    [Fact]
    public async Task PublishScheduleVersion_AfterItsStartDatePassed_ReturnsInPastStaysDraftAndCanBeDiscarded()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var early = await CreateServiceOrFailAsync(provider, Command(net.Network, code: "S401", effectiveFrom: "2026-09-01"));
        var draft = await CreateVersionOrFailAsync(provider, Version("2026-10-02", [ValidThreeStop(early)]));
        ServiceClock.Advance(TimeSpan.FromDays(2));

        var result = await PublishAsync(provider, draft.Id);

        AssertFailure(result, TimetableErrors.ScheduleVersionEffectiveFromInPast);
        Assert.Equal("Draft", await StatusOfAsync(draft.Id));
        Assert.Equal(0, await EventCountAsync(TimetableAuditActions.ScheduleVersionPublished));
        Assert.True((await DiscardAsync(provider, draft.Id)).IsSuccess);
        Assert.Equal("Discarded", await StatusOfAsync(draft.Id));
    }

    /// <summary>SV21 (Amendment 3): a draft for today, listing a service effective today, publishes today.</summary>
    [Fact]
    public async Task PublishScheduleVersion_OnItsStartDate_Succeeds()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var early = await CreateServiceOrFailAsync(provider, Command(net.Network, code: "S401", effectiveFrom: "2026-09-01"));
        var draft = await CreateVersionOrFailAsync(provider, Version("2026-10-01", [ValidThreeStop(early)]));

        Assert.True((await PublishAsync(provider, draft.Id)).IsSuccess);
        Assert.Equal("Published", await StatusOfAsync(draft.Id));
    }

    /// <summary>SV17, R17: a listed service withdrawn from the start date since creation is re-checked at publication.</summary>
    [Fact]
    public async Task PublishScheduleVersion_WithServiceWithdrawnSinceCreation_ReturnsNotEffectiveAndWritesNothing()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var draft = await CreateVersionOrFailAsync(provider, Version("2026-11-01", [ValidThreeStop(net.S2)]));
        Assert.True((await WithdrawServiceAsync(provider, net.S2, Date("2026-11-01"))).IsSuccess);

        var result = await PublishAsync(provider, draft.Id);

        AssertFailure(result, TimetableErrors.ScheduleServiceNotEffective(net.S2));
        Assert.Equal("Draft", await StatusOfAsync(draft.Id));
        Assert.Equal(0, await EventCountAsync(TimetableAuditActions.ScheduleVersionPublished));
    }

    /// <summary>
    /// SV55 at publication (Amendment 1): an empty version created on 2026-10-01 for 2026-10-02 is
    /// refused on 2026-10-02 and stays a draft; a non-empty draft published on its start date still
    /// publishes. (The in-force read is added at step 8; plan V-numbered deviation in progress.md.)
    /// </summary>
    [Fact]
    public async Task PublishScheduleVersion_EmptyWhoseStartHasBecomeToday_ReturnsEmptyNotInFutureAndWritesNothing()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var early = await CreateServiceOrFailAsync(provider, Command(net.Network, code: "S401", effectiveFrom: "2026-09-01"));
        var empty = await CreateVersionOrFailAsync(provider, Version("2026-10-02", []));
        var full = await CreateVersionOrFailAsync(provider, Version("2026-10-03", [ValidThreeStop(early)]));
        ServiceClock.Advance(TimeSpan.FromDays(1));

        var result = await PublishAsync(provider, empty.Id);

        AssertFailure(result, TimetableErrors.EmptyScheduleVersionNotInFuture);
        Assert.Equal("Draft", await StatusOfAsync(empty.Id));
        Assert.Equal(1, await CountAsync($"[timetable].[ScheduleVersions] WHERE [Id] = '{empty.Id}' AND [PublishedAtUtc] IS NULL"));
        Assert.Equal(0, await EventCountAsync(TimetableAuditActions.ScheduleVersionPublished));

        ServiceClock.Advance(TimeSpan.FromDays(1));
        Assert.True((await PublishAsync(provider, full.Id)).IsSuccess);
    }

    /// <summary>SV56: an empty version starting tomorrow is published, then cancelled before it takes effect.</summary>
    [Fact]
    public async Task PublishScheduleVersion_EmptyStartingTomorrow_PublishesAndCanBeCancelled()
    {
        await using var provider = BuildScheduleProvider();
        var empty = await CreateVersionOrFailAsync(provider, Version("2026-10-02", []));

        Assert.True((await PublishAsync(provider, empty.Id)).IsSuccess);
        Assert.Equal(1, await EventCountAsync(TimetableAuditActions.ScheduleVersionPublished));
        Assert.True((await CancelAsync(provider, empty.Id)).IsSuccess);

        Assert.Equal("Cancelled", await StatusOfAsync(empty.Id));
        Assert.Equal(1, await EventCountAsync(TimetableAuditActions.ScheduleVersionCancelled));
    }

    /// <summary>
    /// R22, plan V8: a writer that bypasses the lock publishes another version with the same start
    /// date between the handler's decision and its save; the filtered unique index refuses the save,
    /// mapped by name to 409, and nothing of this publish is written.
    /// </summary>
    [Fact]
    public async Task PublishScheduleVersion_WhenIndexViolatedBehindTheLock_Returns409EffectiveFromTaken()
    {
        await using var setup = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(setup);
        var draft = await CreateVersionOrFailAsync(setup, Version("2026-10-05", [ValidS1(net.S1)]));
        var migrator = Database.MigratorConnectionString;
        var intruder = Guid.Parse("0199b3a0-0000-7000-8000-00000000eeee");
        await using var provider = BuildScheduleProvider(configure: services => AuditRecordHook.Register(services, action =>
        {
            if (action != TimetableAuditActions.ScheduleVersionPublished)
            {
                return;
            }

            ExecuteAsMigrator(
                migrator,
                $"""
                INSERT INTO [timetable].[ScheduleVersions] ([Id], [Number], [NameEn], [NameMy], [EffectiveFrom], [Status], [CreatedAtUtc], [PublishedAtUtc])
                VALUES ('{intruder}', 99, N'Intruder', N'Intruder', '2026-10-05', N'Published', '2026-10-01T03:00:00+00:00', '2026-10-01T03:00:00+00:00');
                """);
        }));

        var result = await PublishAsync(provider, draft.Id);

        AssertFailure(result, TimetableErrors.ScheduleVersionEffectiveFromTaken);
        Assert.Equal("Draft", await StatusOfAsync(draft.Id));
        Assert.Equal(0, await EventCountAsync(TimetableAuditActions.ScheduleVersionPublished));
    }

    /// <summary>R47: a status changed behind the lock makes the guarded update match no row; 409, nothing written.</summary>
    [Fact]
    public async Task PublishScheduleVersion_WhenStatusChangesBehindTheLock_Returns409ChangedConcurrentlyAndWritesNothing()
    {
        await using var setup = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(setup);
        var draft = await CreateVersionOrFailAsync(setup, Version("2026-10-05", [ValidS1(net.S1)]));
        var migrator = Database.MigratorConnectionString;
        await using var provider = BuildScheduleProvider(configure: services => AuditRecordHook.Register(services, action =>
        {
            if (action == TimetableAuditActions.ScheduleVersionPublished)
            {
                ExecuteAsMigrator(
                    migrator,
                    $"UPDATE [timetable].[ScheduleVersions] SET [Status] = N'Discarded', [DiscardedAtUtc] = '2026-10-01T03:30:00+00:00' WHERE [Id] = '{draft.Id}';");
            }
        }));

        var result = await PublishAsync(provider, draft.Id);

        AssertFailure(result, TimetableErrors.ScheduleVersionChangedConcurrently);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Discarded", await StatusOfAsync(draft.Id));
        Assert.Equal(0, await EventCountAsync(TimetableAuditActions.ScheduleVersionPublished));
    }

    internal static void ExecuteAsMigrator(string connectionString, string sql)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = new SqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }
}

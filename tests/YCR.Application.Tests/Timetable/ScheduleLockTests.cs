using YCR.Application.Timetable;
using YCR.Domain.Common;
using YCR.Domain.Timetable;
using YCR.TestSupport;
using static YCR.Application.Tests.Timetable.ScheduleTestData;

namespace YCR.Application.Tests.Timetable;

/// <summary>
/// F-005 races under the Timetable-wide lock (R46; plan §The Timetable-wide lock), on real SQL Server
/// under <c>ycr_app</c>. Forced orders use <see cref="GatedScheduleVersionsLock"/> over the real
/// applock: the second operation is seen waiting for 500 ms before the first is released.
/// </summary>
public sealed class ScheduleLockTests(SqlServerFixture fixture) : ScheduleHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "schedule_lock";

    /// <summary>
    /// SV42: publish and discard of one draft, forced in each order: the first succeeds, the second
    /// sees the committed status and is refused with <c>422 Timetable.ScheduleVersionNotDraft</c>.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PublishAndDiscard_SameDraft_ForcedBothOrders_OneSucceedsOtherNotDraft(bool publishFirst)
    {
        await using var setup = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(setup);
        var draft = await CreateVersionOrFailAsync(setup, Version("2026-10-05", [ValidS1(net.S1)]));

        var (first, second) = publishFirst
            ? await ForcedOnScheduleLockAsync(provider => PublishAsync(provider, draft.Id), provider => DiscardAsync(provider, draft.Id))
            : await ForcedOnScheduleLockAsync(provider => DiscardAsync(provider, draft.Id), provider => PublishAsync(provider, draft.Id));

        Assert.True(first.IsSuccess);
        AssertFailure(second, TimetableErrors.ScheduleVersionNotDraft);
        Assert.Equal(publishFirst ? "Published" : "Discarded", await StatusOfAsync(draft.Id));
        Assert.Equal(publishFirst ? 1 : 0, await EventCountAsync(TimetableAuditActions.ScheduleVersionPublished));
        Assert.Equal(publishFirst ? 0 : 1, await EventCountAsync(TimetableAuditActions.ScheduleVersionDiscarded));
    }

    /// <summary>SV42, R7: five creates in parallel get five distinct, contiguous numbers.</summary>
    [Fact]
    public async Task CreateScheduleVersion_InParallel_GetDistinctConsecutiveNumbers()
    {
        await using var setup = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(setup);
        var providers = Enumerable.Range(0, 5).Select(_ => BuildScheduleProvider()).ToList();
        try
        {
            var results = await Task.WhenAll(providers.Select((provider, index) =>
                CreateVersionAsync(provider, Version($"2026-10-{5 + index:00}", [ValidS1(net.S1)]))));

            Assert.All(results, result => Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null));
            Assert.Equal([1, 2, 3, 4, 5], results.Select(result => result.Value.Number).Order());
            Assert.Equal(5, await CountAsync("[timetable].[ScheduleVersions]"));
            Assert.Equal(5, await EventCountAsync(TimetableAuditActions.ScheduleVersionCreated));
        }
        finally
        {
            foreach (var provider in providers)
            {
                await provider.DisposeAsync();
            }
        }
    }
}

using YCR.Application.Timetable;
using YCR.Domain.Common;
using YCR.Domain.Timetable;
using YCR.TestSupport;
using static YCR.Application.Tests.Timetable.ScheduleTestData;
using static YCR.Application.Tests.Timetable.TimetableTestData;

namespace YCR.Application.Tests.Timetable;

/// <summary>
/// The F-005 change to F-004's withdrawal (spec R19, R20, R49, §0.12) against real SQL Server under
/// <c>ycr_app</c>, today 2026-10-01 (Asia/Yangon): SV36-SV39, SV54. The plan lists these under
/// <c>WithdrawServiceHandlerTests</c>; they live in their own class so the F-004 class is unchanged
/// (progress.md V10).
/// </summary>
public sealed class WithdrawServiceScheduleGuardTests(SqlServerFixture fixture) : ScheduleHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "withdraw_schedule_guard";

    /// <summary>SV36: V1 from 2026-10-05, open-ended, lists S1: withdrawing S1 from 2026-11-01 is refused; nothing written.</summary>
    [Fact]
    public async Task WithdrawService_WhileAPublishedVersionListsIt_Returns422AndWritesNothing()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        await PublishedVersionAsync(provider, "2026-10-05", [ValidS1(net.S1)]);

        var result = await WithdrawServiceAsync(provider, net.S1, Date("2026-11-01"));

        AssertFailure(result, TimetableErrors.ServiceInPublishedScheduleVersion(net.S1));
        Assert.Equal(ErrorType.BusinessRule, result.Error.Type);
        Assert.Equal(1, await CountAsync($"[timetable].[Services] WHERE [Id] = '{net.S1}' AND [EffectiveTo] IS NULL AND [WithdrawnAtUtc] IS NULL"));
        Assert.Equal(0, await EventCountAsync(TimetableAuditActions.ServiceWithdrawn));
    }

    /// <summary>SV36: F-004's checks come first: a past date is still WithdrawalDateInPast.</summary>
    [Fact]
    public async Task WithdrawService_WithPastDateAndAListingVersion_ReturnsDateInPastFirst()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        await PublishedVersionAsync(provider, "2026-10-05", [ValidS1(net.S1)]);

        var result = await WithdrawServiceAsync(provider, net.S1, Date("2026-09-30"));

        AssertFailure(result, TimetableErrors.WithdrawalDateInPast);
    }

    /// <summary>SV37, R20: V2 from 2027-01-01 drops S1; withdrawing S1 from V2's start succeeds.</summary>
    [Fact]
    public async Task WithdrawService_FromTheStartOfTheVersionThatDropsIt_Succeeds()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        await PublishedVersionAsync(provider, "2026-10-05", [ValidS1(net.S1)]);
        await PublishedVersionAsync(provider, "2027-01-01", [ValidThreeStop(net.S2)]);

        var result = await WithdrawServiceAsync(provider, net.S1, Date("2027-01-01"));

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateTime(2026, 12, 31), await ScalarAsync<DateTime>($"SELECT [EffectiveTo] FROM [timetable].[Services] WHERE [Id] = '{net.S1}';"));
        Assert.Equal(1, await EventCountAsync(TimetableAuditActions.ServiceWithdrawn));
    }

    /// <summary>SV37: from 2026-12-31 instead, V1 still applies on that date; refused.</summary>
    [Fact]
    public async Task WithdrawService_TheDayBefore_Returns422()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        await PublishedVersionAsync(provider, "2026-10-05", [ValidS1(net.S1)]);
        await PublishedVersionAsync(provider, "2027-01-01", [ValidThreeStop(net.S2)]);

        var result = await WithdrawServiceAsync(provider, net.S1, Date("2026-12-31"));

        AssertFailure(result, TimetableErrors.ServiceInPublishedScheduleVersion(net.S1));
        Assert.Equal(0, await EventCountAsync(TimetableAuditActions.ServiceWithdrawn));
    }

    /// <summary>SV38: only a draft, a discarded and a cancelled version list S2; the withdrawal succeeds.</summary>
    [Fact]
    public async Task WithdrawService_ListedOnlyByDraftDiscardedOrCancelledVersions_Succeeds()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        await CreateVersionOrFailAsync(provider, Version("2026-10-05", [ValidThreeStop(net.S2)]));
        var discarded = await CreateVersionOrFailAsync(provider, Version("2026-10-06", [ValidThreeStop(net.S2)]));
        Assert.True((await DiscardAsync(provider, discarded.Id)).IsSuccess);
        var cancelled = await PublishedVersionAsync(provider, "2026-10-07", [ValidThreeStop(net.S2)]);
        Assert.True((await CancelAsync(provider, cancelled)).IsSuccess);

        var result = await WithdrawServiceAsync(provider, net.S2, Date("2026-11-01"));

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateTime(2026, 10, 31), await ScalarAsync<DateTime>($"SELECT [EffectiveTo] FROM [timetable].[Services] WHERE [Id] = '{net.S2}';"));
    }

    /// <summary>
    /// SV39: S4 runs from 2026-09-01; V0 from 2026-09-01 (published when the clock read 2026-09-01)
    /// lists S4; V1 from 2026-10-05 does not. V0 applies only to 2026-10-04, so withdrawing S4 from
    /// 2026-10-05 succeeds.
    /// </summary>
    [Fact]
    public async Task WithdrawService_ListedOnlyByASupersededPastVersion_Succeeds()
    {
        var september = new TestClock(new DateTimeOffset(2026, 9, 1, 3, 0, 0, TimeSpan.Zero));
        await using var past = BuildScheduleProvider(september);
        var net = await CreateScheduleNetworkAsync(past);
        var s4 = await CreateServiceOrFailAsync(past, Command(net.Network, code: "S404", effectiveFrom: "2026-09-01"));
        await PublishedVersionAsync(past, "2026-09-01", [ValidThreeStop(s4)]);
        await using var provider = BuildScheduleProvider();
        await PublishedVersionAsync(provider, "2026-10-05", [ValidS1(net.S1)]);

        var result = await WithdrawServiceAsync(provider, s4, Date("2026-10-05"));

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateTime(2026, 10, 4), await ScalarAsync<DateTime>($"SELECT [EffectiveTo] FROM [timetable].[Services] WHERE [Id] = '{s4}';"));
    }

    /// <summary>
    /// SV54, R49 (Q2 ruled (a)): V1 from 2026-10-05 lists S1, V2 from 2027-01-01 does not. S1 is
    /// withdrawn from 2027-01-01, then V2 is cancelled: the cancel succeeds and S1 stays withdrawn; a
    /// later withdrawal from any D ≥ 2027-01-01 is F-004's WithdrawalDoesNotShorten. (The in-force
    /// read's <c>runsOnDate</c> assertions are added at step 8, progress.md V8.)
    /// </summary>
    [Fact]
    public async Task WithdrawThenCancel_TheWithdrawalStaysAndInForceReportsNotRunning()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        await PublishedVersionAsync(provider, "2026-10-05", [ValidS1(net.S1)]);
        var v2 = await PublishedVersionAsync(provider, "2027-01-01", [ValidThreeStop(net.S2)]);

        Assert.True((await WithdrawServiceAsync(provider, net.S1, Date("2027-01-01"))).IsSuccess);
        Assert.True((await CancelAsync(provider, v2)).IsSuccess);

        Assert.Equal("Cancelled", await StatusOfAsync(v2));
        Assert.Equal(new DateTime(2026, 12, 31), await ScalarAsync<DateTime>($"SELECT [EffectiveTo] FROM [timetable].[Services] WHERE [Id] = '{net.S1}';"));
        AssertFailure(await WithdrawServiceAsync(provider, net.S1, Date("2027-01-04")), TimetableErrors.WithdrawalDoesNotShorten);
        AssertFailure(await WithdrawServiceAsync(provider, net.S1, Date("2027-01-01")), TimetableErrors.WithdrawalDoesNotShorten);
    }
}

using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Timetable;
using YCR.Application.Timetable.Abstractions;
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

    /// <summary>
    /// SV40, publish first: V1 (2026-10-05) lists S2 only; draft D2 (2027-01-01) lists S1. D2 is
    /// published; the withdrawal of S1 from 2027-01-01, which waited on the lock, is refused (D2
    /// applies from that date, open-ended).
    /// </summary>
    [Fact]
    public async Task PublishAndWithdraw_PublishFirst_WithdrawReturns422InPublishedVersion()
    {
        var (net, d2) = await Sv40SetupAsync();

        var (publish, withdraw) = await ForcedOnScheduleLockAsync(
            provider => PublishAsync(provider, d2),
            provider => WithdrawServiceAsync(provider, net.S1, TimetableTestData.Date("2027-01-01")));

        Assert.True(publish.IsSuccess);
        AssertFailure(withdraw, TimetableErrors.ServiceInPublishedScheduleVersion(net.S1));
        Assert.Equal("Published", await StatusOfAsync(d2));
        Assert.Equal(1, await CountAsync($"[timetable].[Services] WHERE [Id] = '{net.S1}' AND [EffectiveTo] IS NULL"));
        Assert.Equal(0, await ListedServicesWithdrawnWhileTheirVersionAppliesAsync());
    }

    /// <summary>SV40, withdraw first: S1 ends 2026-12-31; publishing D2, which waited, is refused (R17 re-checked).</summary>
    [Fact]
    public async Task PublishAndWithdraw_WithdrawFirst_PublishReturns422NotEffective()
    {
        var (net, d2) = await Sv40SetupAsync();

        var (withdraw, publish) = await ForcedOnScheduleLockAsync(
            provider => WithdrawServiceAsync(provider, net.S1, TimetableTestData.Date("2027-01-01")),
            provider => PublishAsync(provider, d2));

        Assert.True(withdraw.IsSuccess);
        AssertFailure(publish, TimetableErrors.ScheduleServiceNotEffective(net.S1));
        Assert.Equal("Draft", await StatusOfAsync(d2));
        Assert.Equal(new DateTime(2026, 12, 31), await ScalarAsync<DateTime>($"SELECT [EffectiveTo] FROM [timetable].[Services] WHERE [Id] = '{net.S1}';"));
        Assert.Equal(0, await ListedServicesWithdrawnWhileTheirVersionAppliesAsync());
    }

    /// <summary>
    /// SV41, cancel first: V1 (2026-10-05) lists S1; V2 (2027-01-01) does not. V2 is cancelled, so
    /// V1 applies open-ended again; the withdrawal of S1 from 2027-01-01, which waited, is refused.
    /// </summary>
    [Fact]
    public async Task CancelAndWithdraw_CancelFirst_WithdrawReturns422InPublishedVersion()
    {
        var (net, v2) = await Sv41SetupAsync();

        var (cancel, withdraw) = await ForcedOnScheduleLockAsync(
            provider => CancelAsync(provider, v2),
            provider => WithdrawServiceAsync(provider, net.S1, TimetableTestData.Date("2027-01-01")));

        Assert.True(cancel.IsSuccess);
        AssertFailure(withdraw, TimetableErrors.ServiceInPublishedScheduleVersion(net.S1));
        Assert.Equal("Cancelled", await StatusOfAsync(v2));
        Assert.Equal(1, await CountAsync($"[timetable].[Services] WHERE [Id] = '{net.S1}' AND [EffectiveTo] IS NULL"));
    }

    /// <summary>
    /// SV41, withdraw first (R49, Q2 ruled (a)): S1 is withdrawn from 2027-01-01 while V2 drops it;
    /// the cancel of V2, which waited, also succeeds; V1 then applies from 2027-01-01 and still lists
    /// S1, which stays withdrawn.
    /// </summary>
    [Fact]
    public async Task CancelAndWithdraw_WithdrawFirst_BothSucceedAndServiceStaysWithdrawn()
    {
        var (net, v2) = await Sv41SetupAsync();

        var (withdraw, cancel) = await ForcedOnScheduleLockAsync(
            provider => WithdrawServiceAsync(provider, net.S1, TimetableTestData.Date("2027-01-01")),
            provider => CancelAsync(provider, v2));

        Assert.True(withdraw.IsSuccess);
        Assert.True(cancel.IsSuccess);
        Assert.Equal("Cancelled", await StatusOfAsync(v2));
        Assert.Equal(new DateTime(2026, 12, 31), await ScalarAsync<DateTime>($"SELECT [EffectiveTo] FROM [timetable].[Services] WHERE [Id] = '{net.S1}';"));
        Assert.Equal(1, await EventCountAsync(TimetableAuditActions.ServiceWithdrawn));
        Assert.Equal(1, await EventCountAsync(TimetableAuditActions.ScheduleVersionCancelled));
    }

    /// <summary>
    /// R46, no deadlock (plan §Ordering proof, executed): 25 rounds, each with fresh data (a draft
    /// listing S from D; S withdrawable from D), publish and withdraw started together, unforced. No
    /// SQL error (1205 deadlock, 50035/50036 lock timeout) and each round ends in exactly one of
    /// SV40's two serial outcomes.
    /// </summary>
    [Fact]
    public async Task PublishAndWithdraw_InParallelRepeatedly_NeverDeadlockAndEndInASerialOutcome()
    {
        await using var setup = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(setup);
        await using var publisher = BuildScheduleProvider();
        await using var withdrawer = BuildScheduleProvider();
        var publishFirst = 0;
        var withdrawFirst = 0;

        for (var round = 0; round < 25; round++)
        {
            var start = new DateOnly(2027, 1, 1).AddDays(round);
            var service = await CreateServiceOrFailAsync(setup, TimetableTestData.Command(net.Network, code: $"R{round:00}"));
            var draft = await CreateVersionOrFailAsync(setup, Version(start.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), [ValidS1(service)]));

            var publish = PublishAsync(publisher, draft.Id);
            var withdraw = WithdrawServiceAsync(withdrawer, service, start);
            await Task.WhenAll(publish, withdraw);

            if (publish.Result.IsSuccess)
            {
                AssertFailure(withdraw.Result, TimetableErrors.ServiceInPublishedScheduleVersion(service));
                publishFirst++;
            }
            else
            {
                AssertFailure(publish.Result, TimetableErrors.ScheduleServiceNotEffective(service));
                Assert.True(withdraw.Result.IsSuccess);
                withdrawFirst++;
            }
        }

        Assert.Equal(25, publishFirst + withdrawFirst);
        Assert.Equal(0, await ListedServicesWithdrawnWhileTheirVersionAppliesAsync());
        TestContext.Current.TestOutputHelper?.WriteLine($"No-deadlock rounds: publish first {publishFirst}, withdraw first {withdrawFirst}.");
    }

    /// <summary>
    /// R46, P9 (the ordering argument, executed): a withdrawal is held just after it takes the
    /// service-code lock; a publish of a version listing that service does not wait for it and
    /// completes; released, the withdrawal then takes the Timetable-wide lock and is refused by R19.
    /// </summary>
    [Fact]
    public async Task Publish_WhileAWithdrawalHoldsTheServiceCodeLock_DoesNotWait()
    {
        await using var setup = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(setup);
        var draft = await CreateVersionOrFailAsync(setup, Version("2026-11-02", [ValidS1(net.S1)]));
        var gate = new ServiceCodeLockGate();
        await using var withdrawer = BuildScheduleProvider(configure: services => GatedServiceCodeLock.Register(services, gate));
        await using var publisher = BuildScheduleProvider();

        var withdraw = WithdrawServiceAsync(withdrawer, net.S1, TimetableTestData.Date("2026-12-01"));
        Result publish;
        try
        {
            await gate.Acquired.Task.WaitAsync(LockWait, CancellationToken);
            publish = await PublishAsync(publisher, draft.Id).WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            Assert.False(withdraw.IsCompleted, "The withdrawal finished while it was held at its code lock.");
        }
        finally
        {
            gate.Release.TrySetResult();
        }

        Assert.True(publish.IsSuccess);
        AssertFailure(await withdraw, TimetableErrors.ServiceInPublishedScheduleVersion(net.S1));
    }

    /// <summary>P9: a withdrawal requests the service-code lock, then the Timetable-wide lock, and nothing else.</summary>
    [Fact]
    public async Task WithdrawService_TakesTheServiceCodeLockBeforeTheScheduleLock()
    {
        await using var setup = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(setup);
        var recorder = new RecordingLocks();
        await using var provider = BuildScheduleProvider(configure: recorder.Register);

        Assert.True((await WithdrawServiceAsync(provider, net.S1, TimetableTestData.Date("2026-11-01"))).IsSuccess);

        Assert.Equal(["timetable.ServiceCode:S101", RecordingLocks.ScheduleVersions], recorder.Requests);
    }

    /// <summary>
    /// P9: no holder of the Timetable-wide lock can request a service-code lock — no schedule
    /// handler depends on <see cref="IServiceCodeLock"/>; the withdrawal is the only handler with both.
    /// </summary>
    [Fact]
    public void ScheduleHandlers_DoNotDependOnTheServiceCodeLock()
    {
        var handlers = YCR.Application.DependencyInjection.HandlerTypes()
            .Where(type => type.GetConstructors().Any(constructor =>
                constructor.GetParameters().Any(parameter => parameter.ParameterType == typeof(IScheduleVersionsLock))))
            .ToList();

        Assert.Equal(
            ["CancelScheduleVersionHandler", "CreateScheduleVersionHandler", "DiscardScheduleVersionHandler", "PublishScheduleVersionHandler", "WithdrawServiceHandler"],
            handlers.Select(type => type.Name).Order(StringComparer.Ordinal));
        Assert.All(
            handlers.Where(type => type.Name != "WithdrawServiceHandler"),
            type => Assert.DoesNotContain(
                type.GetConstructors().SelectMany(constructor => constructor.GetParameters()),
                parameter => parameter.ParameterType == typeof(IServiceCodeLock)));
    }

    /// <summary>R46, §5: service creation does not take the Timetable-wide lock, so it does not wait for it.</summary>
    [Fact]
    public async Task CreateService_WhileTheScheduleLockIsHeld_DoesNotWait()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        await using var holderScope = provider.CreateAsyncScope();
        var db = holderScope.ServiceProvider.GetRequiredService<ITimetableDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(CancellationToken);
        await holderScope.ServiceProvider.GetRequiredService<IScheduleVersionsLock>().AcquireAsync(CancellationToken);

        var created = await CreateServiceAsync(provider, TimetableTestData.Command(net.Network, code: "S909"))
            .WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);

        Assert.True(created.IsSuccess);
        await transaction.CommitAsync(CancellationToken);
    }

    /// <summary>SV40's setting: V1 (2026-10-05) lists S2 only; draft D2 (2027-01-01) lists S1.</summary>
    private async Task<(ScheduleNetwork Net, Guid Draft)> Sv40SetupAsync()
    {
        await using var setup = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(setup);
        await PublishedVersionAsync(setup, "2026-10-05", [ValidThreeStop(net.S2)]);
        var d2 = await CreateVersionOrFailAsync(setup, Version("2027-01-01", [ValidS1(net.S1)]));
        return (net, d2.Id);
    }

    /// <summary>SV41's setting: V1 (2026-10-05) lists S1; V2 (2027-01-01) lists S2 only.</summary>
    private async Task<(ScheduleNetwork Net, Guid V2)> Sv41SetupAsync()
    {
        await using var setup = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(setup);
        await PublishedVersionAsync(setup, "2026-10-05", [ValidS1(net.S1)]);
        var v2 = await PublishedVersionAsync(setup, "2027-01-01", [ValidThreeStop(net.S2)]);
        return (net, v2);
    }

    /// <summary>
    /// SV40's invariant: published versions listing a withdrawn service that ends before the version
    /// stops applying (the day before its successor's start, or never). Zero in every serial order.
    /// </summary>
    private Task<int> ListedServicesWithdrawnWhileTheirVersionAppliesAsync() =>
        ScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM [timetable].[ScheduleVersions] AS v
            JOIN [timetable].[ScheduleVersionServices] AS e ON e.[ScheduleVersionId] = v.[Id]
            JOIN [timetable].[Services] AS s ON s.[Id] = e.[ServiceId]
            OUTER APPLY (
                SELECT MIN(n.[EffectiveFrom]) AS [NextStart] FROM [timetable].[ScheduleVersions] AS n
                WHERE n.[Status] = N'Published' AND n.[EffectiveFrom] > v.[EffectiveFrom]) AS nx
            WHERE v.[Status] = N'Published' AND s.[WithdrawnAtUtc] IS NOT NULL
              AND s.[EffectiveTo] < ISNULL(DATEADD(day, -1, nx.[NextStart]), '9999-12-31');
            """);

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

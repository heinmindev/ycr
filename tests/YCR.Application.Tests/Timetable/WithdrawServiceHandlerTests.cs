using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YCR.Application.Timetable;
using YCR.Domain.Common;
using YCR.Domain.Timetable;
using YCR.Infrastructure.Persistence;
using YCR.TestSupport;
using static YCR.Application.Tests.Timetable.TimetableTestData;

namespace YCR.Application.Tests.Timetable;

/// <summary>
/// F-004 withdrawal scenarios against real SQL Server under <c>ycr_app</c>, today 2026-10-01
/// (Asia/Yangon): S26, S29-S39; R21, R35, R36, R41, R42; plan V2.
/// </summary>
public sealed class WithdrawServiceHandlerTests(SqlServerFixture fixture) : TimetableHandlerTestBase(fixture)
{
    private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(30);

    protected override string DatabasePrefix => "withdraw_service";

    /// <summary>S30, R36: both columns in one update, stops untouched, one event with before and after.</summary>
    [Fact]
    public async Task WithdrawService_FromFutureDate_UpdatesBothColumnsAndWritesOneEventWithBeforeAndAfter()
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);
        var id = await CreateServiceOrFailAsync(provider, Command(network));
        ServiceClock.Advance(TimeSpan.FromHours(2));

        var result = await WithdrawServiceAsync(provider, id, Date("2026-11-01"));

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "S101|2026-10-05|2026-10-31|1|1|1|1|1|0|0|Forward",
            await ScalarAsync<string>(
                """
                SELECT CONCAT([Code], '|', CONVERT(nvarchar(10), [EffectiveFrom], 23), '|', CONVERT(nvarchar(10), [EffectiveTo], 23), '|',
                    CAST([RunsOnMonday] AS int), '|', CAST([RunsOnTuesday] AS int), '|', CAST([RunsOnWednesday] AS int), '|',
                    CAST([RunsOnThursday] AS int), '|', CAST([RunsOnFriday] AS int), '|', CAST([RunsOnSaturday] AS int), '|',
                    CAST([RunsOnSunday] AS int), '|', [Direction])
                FROM [timetable].[Services];
                """));
        Assert.Equal(ServiceClock.GetUtcNow(), await ScalarAsync<DateTimeOffset>("SELECT [WithdrawnAtUtc] FROM [timetable].[Services];"));
        Assert.Equal(DefaultNowUtc, await ScalarAsync<DateTimeOffset>("SELECT [CreatedAtUtc] FROM [timetable].[Services];"));
        Assert.Equal(3, await CountAsync("[timetable].[ServiceStops]"));

        const string Where = " FROM [audit].[AuditEvents] WHERE [Action] = N'Timetable.ServiceWithdrawn';";
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*)" + Where));
        Assert.Equal(TimetableAuditSubjects.Service, await ScalarAsync<string>("SELECT [SubjectType]" + Where));
        Assert.Equal(id, await ScalarAsync<Guid>("SELECT [SubjectId]" + Where));
        Assert.Equal("services.manage", await ScalarAsync<string>("SELECT [AuthorizedByPermission]" + Where));
        var before = await ScalarAsync<string>("SELECT [BeforeJson]" + Where);
        var after = await ScalarAsync<string>("SELECT [AfterJson]" + Where);
        Assert.EndsWith("\"effectiveFrom\":\"2026-10-05\",\"effectiveTo\":null,\"withdrawnAtUtc\":null}", before, StringComparison.Ordinal);
        Assert.EndsWith(
            "\"effectiveFrom\":\"2026-10-05\",\"effectiveTo\":\"2026-10-31\",\"withdrawnAtUtc\":\"2026-10-01T05:00:00+00:00\"}",
            after,
            StringComparison.Ordinal);
        Assert.Contains("\"routeCode\":\"RC\"", after, StringComparison.Ordinal);
        Assert.Contains($"{{\"position\":2,\"stationId\":\"{network['C']}\",\"stationCode\":\"SC\"}}", after, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithdrawService_FromToday_EndsYesterday()
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);
        var id = await CreateServiceOrFailAsync(provider, Command(network, effectiveFrom: "2026-09-01"));

        Assert.True((await WithdrawServiceAsync(provider, id, Today)).IsSuccess);

        Assert.Equal(new DateTime(2026, 9, 30), await ScalarAsync<DateTime>("SELECT [EffectiveTo] FROM [timetable].[Services];"));
    }

    [Fact]
    public async Task WithdrawService_FromPastDate_ReturnsDateInPastAndWritesNothing()
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);
        var id = await CreateServiceOrFailAsync(provider, Command(network));

        var result = await WithdrawServiceAsync(provider, id, Date("2026-09-30"));

        AssertFailure(result, TimetableErrors.WithdrawalDateInPast);
        await AssertUnchangedAsync(effectiveTo: null);
    }

    /// <summary>S33: Y ends 2026-12-31; S34: Z already ended 2026-09-30, before today.</summary>
    [Theory]
    [InlineData("2026-10-05", "2026-12-31", "2027-02-01")]
    [InlineData("2026-10-05", "2026-12-31", "2027-01-01")]
    [InlineData("2026-09-01", "2026-09-30", "2026-10-01")]
    public async Task WithdrawService_ThatDoesNotShorten_ReturnsDoesNotShortenAndWritesNothing(
        string from, string to, string withdrawFrom)
    {
        // Z could only have been created while 2026-09-30 was not yet past (R39).
        await using var earlier = BuildServiceProvider(clock: new TestClock(new DateTimeOffset(2026, 9, 1, 3, 0, 0, TimeSpan.Zero)));
        var network = await CreateNetworkAsync(earlier);
        var id = await CreateServiceOrFailAsync(earlier, Command(network, effectiveFrom: from, effectiveTo: to));
        await using var provider = BuildServiceProvider();

        var result = await WithdrawServiceAsync(provider, id, Date(withdrawFrom));

        AssertFailure(result, TimetableErrors.WithdrawalDoesNotShorten);
        await AssertUnchangedAsync(effectiveTo: to);
    }

    /// <summary>S35: a second withdrawal may shorten further, never lengthen.</summary>
    [Fact]
    public async Task WithdrawService_Twice_ShortensAgainThenRefusesLonger()
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);
        var id = await CreateServiceOrFailAsync(provider, Command(network));
        Assert.True((await WithdrawServiceAsync(provider, id, Date("2026-11-01"))).IsSuccess);
        ServiceClock.Advance(TimeSpan.FromHours(1));

        Assert.True((await WithdrawServiceAsync(provider, id, Date("2026-10-20"))).IsSuccess);
        Assert.Equal(new DateTime(2026, 10, 19), await ScalarAsync<DateTime>("SELECT [EffectiveTo] FROM [timetable].[Services];"));
        Assert.Equal(ServiceClock.GetUtcNow(), await ScalarAsync<DateTimeOffset>("SELECT [WithdrawnAtUtc] FROM [timetable].[Services];"));
        Assert.Equal(2, await EventCountAsync(TimetableAuditActions.ServiceWithdrawn));

        var longer = await WithdrawServiceAsync(provider, id, Date("2026-11-15"));

        AssertFailure(longer, TimetableErrors.WithdrawalDoesNotShorten);
        Assert.Equal(new DateTime(2026, 10, 19), await ScalarAsync<DateTime>("SELECT [EffectiveTo] FROM [timetable].[Services];"));
        Assert.Equal(2, await EventCountAsync(TimetableAuditActions.ServiceWithdrawn));
    }

    /// <summary>S36, R41, R42: withdrawn before it starts, a service never runs and frees its code.</summary>
    [Fact]
    public async Task WithdrawService_BeforeEffectiveFrom_NeverRunsAndFreesTheCode()
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);
        var id = await CreateServiceOrFailAsync(provider, Command(network, effectiveFrom: "2026-11-01"));

        Assert.True((await WithdrawServiceAsync(provider, id, Date("2026-10-15"))).IsSuccess);

        Assert.Equal(
            "2026-11-01|2026-10-14",
            await ScalarAsync<string>("SELECT CONCAT(CONVERT(nvarchar(10), [EffectiveFrom], 23), '|', CONVERT(nvarchar(10), [EffectiveTo], 23)) FROM [timetable].[Services];"));
        await CreateServiceOrFailAsync(provider, Command(network, effectiveFrom: "2026-10-05"));
        Assert.Equal(2, await CountAsync("[timetable].[Services] WHERE [Code] = N'S101'"));
    }

    /// <summary>S26: a timetable change — withdraw from D, create the same code from D.</summary>
    [Fact]
    public async Task WithdrawService_ThenCreateSameCodeFromSameDate_BothSucceed()
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);
        var old = await CreateServiceOrFailAsync(provider, Command(network));

        Assert.True((await WithdrawServiceAsync(provider, old, Date("2027-01-01"))).IsSuccess);
        var replacement = await CreateServiceOrFailAsync(
            provider, Command(network, "SRP", routeId: network.Ro, direction: "Reverse", effectiveFrom: "2027-01-01"));

        Assert.NotEqual(old, replacement);
        Assert.Equal(new DateTime(2026, 12, 31), await ScalarAsync<DateTime>($"SELECT [EffectiveTo] FROM [timetable].[Services] WHERE [Id] = '{old}';"));
        Assert.Equal(2, await CountAsync("[timetable].[Services] WHERE [Code] = N'S101'"));
    }

    [Fact]
    public async Task WithdrawService_WithUnknownId_ReturnsNotFound()
    {
        await using var provider = BuildServiceProvider();

        var result = await WithdrawServiceAsync(provider, Guid.Parse("0199b3a0-0000-7000-8000-0000000000ec"), Date("2026-11-01"));

        AssertFailure(result, TimetableErrors.ServiceNotFound);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    /// <summary>S39: withdrawal has no route or station guard.</summary>
    [Theory]
    [InlineData("route")]
    [InlineData("station")]
    public async Task WithdrawService_AfterRouteOrStopStationDeactivated_Succeeds(string deactivated)
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);
        var id = await CreateServiceOrFailAsync(provider, Command(network));
        if (deactivated == "route")
        {
            Assert.True((await DeactivateRouteAsync(provider, network.Rc)).IsSuccess);
        }
        else
        {
            await DeactivateStationAsync(provider, network['C']);
        }

        Assert.True((await WithdrawServiceAsync(provider, id, Date("2026-11-01"))).IsSuccess);
        Assert.Equal(1, await EventCountAsync(TimetableAuditActions.ServiceWithdrawn));
    }

    /// <summary>
    /// S29, forced "withdraw first": the create waits on the code lock, then sees the shortened
    /// period, so both succeed and the two periods are adjacent.
    /// </summary>
    [Fact]
    public async Task WithdrawAndCreate_SameCode_WithdrawFirst_Returns204Then201()
    {
        await using var setup = BuildServiceProvider();
        var network = await CreateNetworkAsync(setup);
        var x = await CreateServiceOrFailAsync(setup, Command(network));

        var (withdrawal, creation) = await ForcedAsync(
            provider => WithdrawServiceAsync(provider, x, Date("2027-01-01")),
            provider => CreateServiceAsync(provider, Command(network, effectiveFrom: "2027-01-01")));

        Assert.True(withdrawal.IsSuccess);
        Assert.True(creation.IsSuccess);
        Assert.Equal(new DateTime(2026, 12, 31), await ScalarAsync<DateTime>($"SELECT [EffectiveTo] FROM [timetable].[Services] WHERE [Id] = '{x}';"));
        Assert.Equal(2, await CountAsync("[timetable].[Services] WHERE [Code] = N'S101'"));
        Assert.Equal(0, await OverlappingPairsAsync());
    }

    /// <summary>
    /// S29, forced "create first": the create sees the open-ended period and is refused; the
    /// withdrawal, which waited on the lock, then succeeds.
    /// </summary>
    [Fact]
    public async Task WithdrawAndCreate_SameCode_CreateFirst_Returns409Then204()
    {
        await using var setup = BuildServiceProvider();
        var network = await CreateNetworkAsync(setup);
        var x = await CreateServiceOrFailAsync(setup, Command(network));

        var (creation, withdrawal) = await ForcedAsync(
            provider => CreateServiceAsync(provider, Command(network, effectiveFrom: "2027-01-01")),
            provider => WithdrawServiceAsync(provider, x, Date("2027-01-01")));

        AssertFailure(creation, TimetableErrors.ServiceCodePeriodOverlap("S101"));
        Assert.True(withdrawal.IsSuccess);
        Assert.Equal(1, await CountAsync("[timetable].[Services] WHERE [Code] = N'S101'"));
        Assert.Equal(1, await EventCountAsync(TimetableAuditActions.ServiceCreated));
        Assert.Equal(1, await EventCountAsync(TimetableAuditActions.ServiceWithdrawn));
    }

    /// <summary>S29, unforced: whichever wins, two overlapping services with one code are never committed.</summary>
    [Fact]
    public async Task WithdrawAndCreate_SameCodeInParallel_NeverCommitOverlappingServices()
    {
        await using var setup = BuildServiceProvider();
        var network = await CreateNetworkAsync(setup);
        var x = await CreateServiceOrFailAsync(setup, Command(network));
        await using var first = BuildServiceProvider();
        await using var second = BuildServiceProvider();

        var withdrawal = WithdrawServiceAsync(first, x, Date("2027-01-01"));
        var creation = CreateServiceAsync(second, Command(network, effectiveFrom: "2027-01-01"));
        await Task.WhenAll(withdrawal, creation);
        var withdrawn = await withdrawal;
        var created = await creation;

        Assert.True(withdrawn.IsSuccess);
        if (created.IsFailure)
        {
            Assert.Equal(TimetableErrors.ServiceCodePeriodOverlap("S101"), created.Error);
        }

        Assert.Equal(created.IsSuccess ? 2 : 1, await CountAsync("[timetable].[Services] WHERE [Code] = N'S101'"));
        Assert.Equal(0, await OverlappingPairsAsync());
    }

    /// <summary>
    /// S37: two withdrawals of one service serialise on its code. In either order the final end is
    /// 2026-10-19; "11-01 then 10-20" is two successes and two events, "10-20 then 11-01" is a
    /// success, a refusal and one event.
    /// </summary>
    [Theory]
    [InlineData("2026-11-01", "2026-10-20", true, 2)]
    [InlineData("2026-10-20", "2026-11-01", false, 1)]
    public async Task WithdrawService_TwoWithdrawalsForced_EndOnTheShorterDateInEitherOrder(
        string firstFrom, string secondFrom, bool secondSucceeds, int events)
    {
        await using var setup = BuildServiceProvider();
        var network = await CreateNetworkAsync(setup);
        var x = await CreateServiceOrFailAsync(setup, Command(network));

        var (first, second) = await ForcedAsync(
            provider => WithdrawServiceAsync(provider, x, Date(firstFrom)),
            provider => WithdrawServiceAsync(provider, x, Date(secondFrom)));

        Assert.True(first.IsSuccess);
        if (secondSucceeds)
        {
            Assert.True(second.IsSuccess);
        }
        else
        {
            AssertFailure(second, TimetableErrors.WithdrawalDoesNotShorten);
        }

        Assert.Equal(new DateTime(2026, 10, 19), await ScalarAsync<DateTime>("SELECT [EffectiveTo] FROM [timetable].[Services];"));
        Assert.Equal(events, await EventCountAsync(TimetableAuditActions.ServiceWithdrawn));
    }

    /// <summary>
    /// R36: a writer that bypasses the lock changes <c>EffectiveTo</c> between the handler's load and
    /// its save; the concurrency token refuses the stale update, nothing is written, and the other
    /// writer's value stands.
    /// </summary>
    [Fact]
    public async Task WithdrawService_WhenRowChangesBehindTheLock_Returns409ChangedConcurrentlyAndWritesNothing()
    {
        await using var setup = BuildServiceProvider();
        var network = await CreateNetworkAsync(setup);
        var x = await CreateServiceOrFailAsync(setup, Command(network));
        var migrator = Database.MigratorConnectionString;
        await using var provider = BuildServiceProvider(configure: services => AuditRecordHook.Register(services, action =>
        {
            if (action != TimetableAuditActions.ServiceWithdrawn)
            {
                return;
            }

            using var connection = new SqlConnection(migrator);
            connection.Open();
            using var command = new SqlCommand(
                $"UPDATE [timetable].[Services] SET [EffectiveTo] = '2026-12-01', [WithdrawnAtUtc] = '2026-10-01T03:30:00+00:00' WHERE [Id] = '{x}';",
                connection);
            command.ExecuteNonQuery();
        }));

        var result = await WithdrawServiceAsync(provider, x, Date("2026-11-01"));

        AssertFailure(result, TimetableErrors.ServiceChangedConcurrently);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(0, await EventCountAsync(TimetableAuditActions.ServiceWithdrawn));
        Assert.Equal(new DateTime(2026, 12, 1), await ScalarAsync<DateTime>("SELECT [EffectiveTo] FROM [timetable].[Services];"));
    }

    /// <summary>
    /// Plan V2: withdrawal is one <c>UPDATE</c> of the two granted columns, guarded by the original
    /// <c>EffectiveTo</c> (<c>IS NULL</c> the first time), and nothing is written to
    /// <c>ServiceStops</c>.
    /// </summary>
    [Fact]
    public async Task WithdrawServiceSql_UpdatesOnlyEffectiveToAndWithdrawnAtAndNoServiceStops()
    {
        await using var setup = BuildServiceProvider();
        var network = await CreateNetworkAsync(setup);
        var x = await CreateServiceOrFailAsync(setup, Command(network));

        var commands = new List<string>();
        await using var provider = BuildServiceProvider(configure: services => services.ConfigureDbContext<YcrDbContext>(
            options => options.LogTo(commands.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information)));

        Assert.True((await WithdrawServiceAsync(provider, x, Date("2026-11-01"))).IsSuccess);
        ServiceClock.Advance(TimeSpan.FromHours(1));
        Assert.True((await WithdrawServiceAsync(provider, x, Date("2026-10-20"))).IsSuccess);

        var all = string.Join("\n", commands);
        var updates = Regex.Matches(all, @"UPDATE \[timetable\]\.\[Services\] SET (?<set>[^\n]*?)\s*OUTPUT[\s\S]*?WHERE (?<where>[^;]*);");
        Assert.Equal(2, updates.Count);
        Assert.All(updates, update => Assert.Equal(
            ["[EffectiveTo]", "[WithdrawnAtUtc]"],
            Regex.Matches(update.Groups["set"].Value, @"\[[A-Za-z]+\](?= =)").Select(match => match.Value).Order(StringComparer.Ordinal)));
        Assert.Matches(@"^\[Id\] = @\w+ AND \[EffectiveTo\] IS NULL$", updates[0].Groups["where"].Value);
        Assert.Matches(@"^\[Id\] = @\w+ AND \[EffectiveTo\] = @\w+$", updates[1].Groups["where"].Value);

        Assert.DoesNotMatch(@"(INSERT INTO|UPDATE|DELETE FROM) \[timetable\]\.\[ServiceStops\]", all);
        Assert.DoesNotMatch(@"DELETE FROM \[timetable\]", all);
        Assert.Contains("sp_getapplock", all, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO [audit].[AuditEvents]", all, StringComparison.Ordinal);
    }

    /// <summary>
    /// Runs two operations in a fixed order (plan §R35): the first acquires the real code lock and
    /// is held at its gate; the second starts, is seen to be waiting for the lock after 500 ms, and
    /// only then is the first released.
    /// </summary>
    private async Task<(T1 First, T2 Second)> ForcedAsync<T1, T2>(
        Func<IServiceProvider, Task<T1>> first,
        Func<IServiceProvider, Task<T2>> second)
    {
        var firstGate = new ServiceCodeLockGate();
        var secondGate = ServiceCodeLockGate.Open();
        await using var firstProvider = BuildServiceProvider(configure: services => GatedServiceCodeLock.Register(services, firstGate));
        await using var secondProvider = BuildServiceProvider(configure: services => GatedServiceCodeLock.Register(services, secondGate));

        var firstRun = first(firstProvider);
        Task<T2> secondRun;
        try
        {
            await firstGate.Acquired.Task.WaitAsync(LockWait, CancellationToken);
            secondRun = second(secondProvider);
            await secondGate.Acquiring.Task.WaitAsync(LockWait, CancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken);
            Assert.False(
                secondGate.Acquired.Task.IsCompleted,
                "The second operation acquired the code lock while the first still held it.");
        }
        finally
        {
            firstGate.Release.TrySetResult();
        }

        return (await firstRun, await secondRun);
    }

    /// <summary>Pairs of committed services with one code whose non-empty periods overlap (R35, R42).</summary>
    private Task<int> OverlappingPairsAsync() =>
        ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM [timetable].[Services] AS a
            JOIN [timetable].[Services] AS b ON a.[Code] = b.[Code] AND a.[Id] < b.[Id]
            WHERE (a.[EffectiveTo] IS NULL OR a.[EffectiveTo] >= a.[EffectiveFrom])
              AND (b.[EffectiveTo] IS NULL OR b.[EffectiveTo] >= b.[EffectiveFrom])
              AND a.[EffectiveFrom] <= ISNULL(b.[EffectiveTo], '9999-12-31')
              AND b.[EffectiveFrom] <= ISNULL(a.[EffectiveTo], '9999-12-31');
            """);

    private async Task AssertUnchangedAsync(string? effectiveTo)
    {
        Assert.Equal(
            effectiveTo ?? "null",
            await ScalarAsync<string>("SELECT ISNULL(CONVERT(nvarchar(10), [EffectiveTo], 23), 'null') FROM [timetable].[Services];"));
        Assert.Equal(0, await CountAsync("[timetable].[Services] WHERE [WithdrawnAtUtc] IS NOT NULL"));
        Assert.Equal(0, await EventCountAsync(TimetableAuditActions.ServiceWithdrawn));
    }
}

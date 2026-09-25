using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Common.Authorization;
using YCR.Application.Tests.Network;
using YCR.Application.Timetable;
using YCR.Application.Timetable.CreateService;
using YCR.Domain.Common;
using YCR.Domain.Timetable;
using YCR.Infrastructure.Persistence;
using YCR.TestSupport;
using static YCR.Application.Tests.Timetable.TimetableTestData;

namespace YCR.Application.Tests.Timetable;

/// <summary>
/// F-004 create scenarios against real SQL Server under <c>ycr_app</c>, today 2026-10-01
/// (Asia/Yangon): S1, S4-S19, S22-S25, S27, S28, S40, S40a, S45, S46, S49, S52; R35, R37, R38.
/// </summary>
public sealed class CreateServiceHandlerTests(SqlServerFixture fixture) : TimetableHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "create_service";

    [Fact]
    public async Task CreateService_WithValidCommand_PersistsServiceStopsAndOneAuditEvent()
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);

        var id = await CreateServiceOrFailAsync(provider, Command(network));

        Assert.Equal(
            $"S101|Circular|{network.Rc}|Forward|1|1|1|1|1|0|0|2026-10-05|null|null",
            await ScalarAsync<string>(
                """
                SELECT CONCAT([Code], '|', [NameEn], '|', LOWER(CONVERT(nvarchar(36), [RouteId])), '|', [Direction], '|',
                    CAST([RunsOnMonday] AS int), '|', CAST([RunsOnTuesday] AS int), '|', CAST([RunsOnWednesday] AS int), '|',
                    CAST([RunsOnThursday] AS int), '|', CAST([RunsOnFriday] AS int), '|', CAST([RunsOnSaturday] AS int), '|',
                    CAST([RunsOnSunday] AS int), '|', CONVERT(nvarchar(10), [EffectiveFrom], 23), '|',
                    ISNULL(CONVERT(nvarchar(10), [EffectiveTo], 23), 'null'), '|', ISNULL(CONVERT(nvarchar(40), [WithdrawnAtUtc]), 'null'))
                FROM [timetable].[Services];
                """));
        Assert.Equal(id, await ScalarAsync<Guid>("SELECT [Id] FROM [timetable].[Services];"));
        Assert.Equal(ServiceClock.GetUtcNow(), await ScalarAsync<DateTimeOffset>("SELECT [CreatedAtUtc] FROM [timetable].[Services];"));
        Assert.Equal(
            $"1:{network['A']}|2:{network['C']}|3:{network['E']}",
            await ScalarAsync<string>(
                $"SELECT STRING_AGG(CONCAT([Position], ':', LOWER(CONVERT(nvarchar(36), [StationId]))), '|') WITHIN GROUP (ORDER BY [Position]) FROM [timetable].[ServiceStops] WHERE [ServiceId] = '{id}';"));

        const string Where = " FROM [audit].[AuditEvents] WHERE [Action] = N'Timetable.ServiceCreated';";
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*)" + Where));
        Assert.Equal(TimetableAuditSubjects.Service, await ScalarAsync<string>("SELECT [SubjectType]" + Where));
        Assert.Equal(id, await ScalarAsync<Guid>("SELECT [SubjectId]" + Where));
        Assert.Equal(Permissions.ServicesManage, await ScalarAsync<string>("SELECT [AuthorizedByPermission]" + Where));
        Assert.Equal(1, await ScalarAsync<int>("SELECT [PayloadVersion]" + Where));
        Assert.Null(await ScalarAsync<string>("SELECT [BeforeJson]" + Where));
        Assert.Equal(
            $"{{\"code\":\"S101\",\"nameEn\":\"Circular\",\"nameMy\":\"မြို့ပတ်ရထား\",\"routeId\":\"{network.Rc}\",\"routeCode\":\"RC\",\"direction\":\"Forward\","
            + $"\"stops\":[{{\"position\":1,\"stationId\":\"{network['A']}\",\"stationCode\":\"SA\"}},"
            + $"{{\"position\":2,\"stationId\":\"{network['C']}\",\"stationCode\":\"SC\"}},"
            + $"{{\"position\":3,\"stationId\":\"{network['E']}\",\"stationCode\":\"SE\"}}],"
            + "\"operatingDays\":[\"Monday\",\"Tuesday\",\"Wednesday\",\"Thursday\",\"Friday\"],"
            + "\"effectiveFrom\":\"2026-10-05\",\"effectiveTo\":null,\"withdrawnAtUtc\":null}",
            await ScalarAsync<string>("SELECT [AfterJson]" + Where));
    }

    [Theory]
    [InlineData("Forward", "DEAB")] // S4
    [InlineData("Reverse", "BAED")] // S5
    [InlineData("Forward", "DAC")] // S6
    [InlineData("Forward", "CB")] // S6
    [InlineData("Forward", "CDEABC")] // S7
    [InlineData("Forward", "CEBC")] // S7
    [InlineData("Reverse", "CADC")] // S7
    [InlineData("Forward", "QR")] // S10, RO
    [InlineData("Reverse", "SRP")] // S10, RO
    public async Task CreateService_WithValidPatterns_Creates(string direction, string stops)
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);
        var onRo = stops[0] is 'P' or 'Q' or 'R' or 'S';

        var id = await CreateServiceOrFailAsync(
            provider, Command(network, stops, routeId: onRo ? network.Ro : network.Rc, direction: direction));

        Assert.Equal(
            string.Join("|", stops.Select((letter, index) => $"{index + 1}:{network[letter]}")),
            await ScalarAsync<string>(
                $"SELECT STRING_AGG(CONCAT([Position], ':', LOWER(CONVERT(nvarchar(36), [StationId]))), '|') WITHIN GROUP (ORDER BY [Position]) FROM [timetable].[ServiceStops] WHERE [ServiceId] = '{id}';"));
        Assert.Equal(direction, await ScalarAsync<string>("SELECT [Direction] FROM [timetable].[Services];"));
    }

    [Theory]
    [InlineData("RC", "Forward", "CEC", "Timetable.ServiceTooFewStops", '-')] // S8
    [InlineData("RC", "Forward", "CDEABCD", "Timetable.ServiceStopRepeated", 'C')] // S9
    [InlineData("RC", "Forward", "DACE", "Timetable.ServiceStopsOutOfOrder", 'E')] // S9
    [InlineData("RO", "Forward", "RSP", "Timetable.ServiceStopsOutOfOrder", 'P')] // S11
    [InlineData("RO", "Reverse", "QPS", "Timetable.ServiceStopsOutOfOrder", 'S')] // S11
    [InlineData("RO", "Forward", "PQRP", "Timetable.ServiceStopRepeated", 'P')] // S12
    [InlineData("RC", "Forward", "ABBC", "Timetable.ServiceStopRepeated", 'B')] // S13
    [InlineData("RC", "Forward", "ABAC", "Timetable.ServiceStopRepeated", 'A')] // S13
    [InlineData("RC", "Forward", "ACB", "Timetable.ServiceStopsOutOfOrder", 'B')] // S14
    [InlineData("RO", "Forward", "PRQ", "Timetable.ServiceStopsOutOfOrder", 'Q')] // S14
    [InlineData("RC", "Forward", "A", "Timetable.ServiceTooFewStops", '-')] // S15
    [InlineData("RC", "Forward", "AXC", "Timetable.ServiceStopNotOnRoute", 'X')] // S16: a station on no route
    [InlineData("RC", "Forward", "AZC", "Timetable.ServiceStopNotOnRoute", 'Z')] // S16: an id that names no station
    public async Task CreateService_WithInvalidPatterns_ReturnsErrorAndWritesNothing(
        string route, string direction, string stops, string code, char offending)
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);

        var result = await CreateServiceAsync(
            provider, Command(network, stops, routeId: route == "RO" ? network.Ro : network.Rc, direction: direction));

        Assert.True(result.IsFailure);
        Assert.Equal(code, result.Error.Code);
        Assert.Equal(ErrorType.BusinessRule, result.Error.Type);
        if (offending != '-')
        {
            Assert.Contains(network[offending].ToString(), result.Error.Message, StringComparison.Ordinal);
        }

        await AssertNothingWrittenAsync();
    }

    [Fact]
    public async Task CreateService_OnInactiveRoute_ReturnsRouteInactiveAndWritesNothing()
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);
        Assert.True((await DeactivateRouteAsync(provider, network.Rc)).IsSuccess);

        var result = await CreateServiceAsync(provider, Command(network));

        AssertFailure(result, TimetableErrors.ServiceRouteInactive(network.Rc));
        await AssertNothingWrittenAsync();
    }

    [Fact]
    public async Task CreateService_WithInactiveStopStation_ReturnsStopStationInactiveAndWritesNothing()
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);
        await DeactivateStationAsync(provider, network['C']);

        var result = await CreateServiceAsync(provider, Command(network, "ACE"));

        AssertFailure(result, TimetableErrors.ServiceStopStationInactive(network['C']));
        await AssertNothingWrittenAsync();
    }

    [Fact]
    public async Task CreateService_PassingInactiveStation_Creates()
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);
        await DeactivateStationAsync(provider, network['C']);

        await CreateServiceOrFailAsync(provider, Command(network, "BD"));

        Assert.Equal(1, await CountAsync("[timetable].[Services]"));
        Assert.Equal(2, await CountAsync("[timetable].[ServiceStops]"));
    }

    [Fact]
    public async Task CreateService_WithUnknownRoute_ReturnsRouteNotFoundAndWritesNothing()
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);
        var unknown = Guid.Parse("0199b3a0-0000-7000-8000-0000000000ef");

        var result = await CreateServiceAsync(provider, Command(network, routeId: unknown));

        AssertFailure(result, TimetableErrors.ServiceRouteNotFound(unknown));
        Assert.Equal(ErrorType.BusinessRule, result.Error.Type);
        await AssertNothingWrittenAsync();
    }

    [Theory]
    [InlineData("s1", "Circular", "Timetable.InvalidServiceCode")]
    [InlineData("A", "Circular", "Timetable.InvalidServiceCode")]
    [InlineData("ABCDEFGHIJK", "Circular", "Timetable.InvalidServiceCode")]
    [InlineData("S-1", "Circular", "Timetable.InvalidServiceCode")]
    [InlineData("S101", "101", "Timetable.InvalidServiceName")]
    [InlineData("S101", "   ", "Timetable.InvalidServiceName")]
    public async Task CreateService_WithInvalidCodeOrName_ReturnsValidationErrorAndWritesNothing(
        string code, string nameEn, string expected)
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);

        var result = await CreateServiceAsync(
            provider, Command(network, code: code, nameEn: nameEn == "101" ? new string('N', 101) : nameEn));

        Assert.True(result.IsFailure);
        Assert.Equal(expected, result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        await AssertNothingWrittenAsync();
    }

    [Fact]
    public async Task CreateService_WithEffectiveToBeforeEffectiveFrom_ReturnsInvalidPeriod()
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);

        var result = await CreateServiceAsync(provider, Command(network, effectiveTo: "2026-10-04"));

        AssertFailure(result, TimetableErrors.InvalidEffectivePeriod);
        await AssertNothingWrittenAsync();
    }

    [Fact]
    public async Task CreateService_WithOneDayPeriod_Creates()
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);

        await CreateServiceOrFailAsync(provider, Command(network, effectiveTo: "2026-10-05"));

        Assert.Equal(1, await ScalarAsync<int>(
            "SELECT COUNT(*) FROM [timetable].[Services] WHERE [EffectiveFrom] = '2026-10-05' AND [EffectiveTo] = '2026-10-05';"));
    }

    /// <summary>S24, S25, S40, R35: the overlap check ignores route, direction and days.</summary>
    [Theory]
    [InlineData("2026-10-05", null, "RC", "Forward", "2027-01-01", null)] // S24: open-ended
    [InlineData("2026-10-05", "2026-12-31", "RC", "Forward", "2026-12-31", null)] // S25: inclusive edge
    [InlineData("2026-10-05", "2026-12-31", "RO", "Reverse", "2026-11-01", null)] // S25: other route, direction, days
    [InlineData("2026-10-05", null, "RC", "Forward", "2026-09-01", null)] // S40: past start, open-ended
    public async Task CreateService_WithOverlappingPeriodForSameCode_ReturnsConflictAndWritesNothing(
        string existingFrom, string? existingTo, string route, string direction, string from, string? to)
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);
        await CreateServiceOrFailAsync(provider, Command(network, effectiveFrom: existingFrom, effectiveTo: existingTo));

        var result = await CreateServiceAsync(
            provider,
            route == "RO"
                ? Command(network, "SRP", routeId: network.Ro, direction: direction, effectiveFrom: from, effectiveTo: to,
                    days: [DayOfWeek.Saturday, DayOfWeek.Sunday])
                : Command(network, effectiveFrom: from, effectiveTo: to));

        AssertFailure(result, TimetableErrors.ServiceCodePeriodOverlap("S101"));
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        await AssertNothingWrittenAsync(services: 1, stops: 3, events: 1);
    }

    [Theory]
    [InlineData("2026-10-05", "2026-12-31", "2027-01-01", null)] // S25: the day after the end
    [InlineData("2026-10-05", null, "2026-09-01", "2026-10-04")] // S40: ending the day before the start
    public async Task CreateService_WithAdjacentPeriodForSameCode_Creates(
        string existingFrom, string? existingTo, string from, string? to)
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);
        await CreateServiceOrFailAsync(provider, Command(network, effectiveFrom: existingFrom, effectiveTo: existingTo));

        await CreateServiceOrFailAsync(provider, Command(network, effectiveFrom: from, effectiveTo: to));

        Assert.Equal(2, await CountAsync("[timetable].[Services] WHERE [Code] = N'S101'"));
    }

    /// <summary>S40, R39: a past start is accepted and CreatedAtUtc records when it was entered.</summary>
    [Fact]
    public async Task CreateService_WithPastEffectiveFrom_CreatesAndStampsCreatedAtWithClockNow()
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);

        var id = await CreateServiceOrFailAsync(provider, Command(network, effectiveFrom: "2026-09-01"));

        Assert.Equal(new DateTime(2026, 9, 1), await ScalarAsync<DateTime>($"SELECT [EffectiveFrom] FROM [timetable].[Services] WHERE [Id] = '{id}';"));
        Assert.Equal(DefaultNowUtc, await ScalarAsync<DateTimeOffset>($"SELECT [CreatedAtUtc] FROM [timetable].[Services] WHERE [Id] = '{id}';"));
        Assert.Equal(DefaultNowUtc, await ScalarAsync<DateTimeOffset>(
            "SELECT [OccurredAtUtc] FROM [audit].[AuditEvents] WHERE [Action] = N'Timetable.ServiceCreated';"));
    }

    [Fact]
    public async Task CreateService_WithEffectiveToBeforeToday_ReturnsEffectiveToInPastAndWritesNothing()
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);

        var result = await CreateServiceAsync(provider, Command(network, effectiveFrom: "2026-09-01", effectiveTo: "2026-09-30"));

        AssertFailure(result, TimetableErrors.ServiceEffectiveToInPast);
        Assert.Equal(ErrorType.BusinessRule, result.Error.Type);
        await AssertNothingWrittenAsync();
    }

    [Fact]
    public async Task CreateService_WithEffectiveToToday_Creates()
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);

        await CreateServiceOrFailAsync(provider, Command(network, effectiveFrom: "2026-09-01", effectiveTo: "2026-10-01"));

        Assert.Equal(1, await CountAsync("[timetable].[Services]"));
    }

    /// <summary>
    /// R38: "today" is the Asia/Yangon date of the clock's instant. Asia/Yangon is UTC+06:30, so
    /// 2026-09-30 17:29:59Z is still 2026-09-30 there and 17:30:00Z is already 2026-10-01. An
    /// <c>EffectiveTo</c> of 2026-09-30 is today at the first instant and yesterday at the second.
    /// </summary>
    [Theory]
    [InlineData("2026-09-30T17:29:59Z", true)]
    [InlineData("2026-09-30T17:30:00Z", false)]
    public async Task CreateService_AtYangonMidnight_UsesTheYangonDateAsToday(string instant, bool created)
    {
        var clock = new TestClock(DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture));
        await using var provider = BuildServiceProvider(clock: clock);
        var network = await CreateNetworkAsync(provider);

        var result = await CreateServiceAsync(provider, Command(network, effectiveFrom: "2026-09-01", effectiveTo: "2026-09-30"));

        if (created)
        {
            Assert.True(result.IsSuccess);
        }
        else
        {
            AssertFailure(result, TimetableErrors.ServiceEffectiveToInPast);
        }
    }

    /// <summary>R37: when a create breaks several rules, the earliest in R37 order is returned.</summary>
    [Theory]
    [InlineData("code+name", "Timetable.InvalidServiceCode")]
    [InlineData("name+period", "Timetable.InvalidServiceName")]
    [InlineData("period+past", "Timetable.InvalidEffectivePeriod")]
    [InlineData("past+route", "Timetable.ServiceEffectiveToInPast")]
    [InlineData("route+pattern", "Timetable.ServiceRouteNotFound")]
    [InlineData("pattern+overlap", "Timetable.ServiceStopsOutOfOrder")]
    [InlineData("overlap", "Timetable.ServiceCodePeriodOverlap")]
    public async Task CreateService_WithSeveralFailures_ReturnsTheFirstInR37Order(string faults, string expected)
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);
        await CreateServiceOrFailAsync(provider, Command(network));
        var unknownRoute = Guid.Parse("0199b3a0-0000-7000-8000-0000000000ed");

        var command = faults switch
        {
            "code+name" => Command(network, code: "s1", nameEn: " "),
            "name+period" => Command(network, nameEn: " ", effectiveTo: "2026-10-04"),
            "period+past" => Command(network, effectiveFrom: "2026-09-20", effectiveTo: "2026-09-10"),
            "past+route" => Command(network, routeId: unknownRoute, effectiveFrom: "2026-09-01", effectiveTo: "2026-09-30"),
            "route+pattern" => Command(network, "ACB", routeId: unknownRoute),
            "pattern+overlap" => Command(network, "ACB"),
            _ => Command(network, "BD")
        };

        var result = await CreateServiceAsync(provider, command);

        Assert.True(result.IsFailure);
        Assert.Equal(expected, result.Error.Code);
        await AssertNothingWrittenAsync(services: 1, stops: 3, events: 1);
    }

    /// <summary>
    /// S27, R35: five concurrent creates of one code with overlapping periods serialise on the code
    /// lock; exactly one wins. Which one is not asserted (F-003 R-9).
    /// </summary>
    [Fact]
    public async Task CreateService_WithParallelSameCodeOverlappingPeriods_PersistsExactlyOneServiceAndOneAuditEvent()
    {
        await using var setup = BuildServiceProvider();
        var network = await CreateNetworkAsync(setup);
        var providers = Enumerable.Range(0, 5).Select(_ => BuildServiceProvider()).ToList();

        try
        {
            var results = await Task.WhenAll(providers.Select((provider, index) =>
                CreateServiceAsync(provider, Command(network, effectiveFrom: $"2026-10-0{index + 5}"))));

            Assert.Single(results, result => result.IsSuccess);
            Assert.All(
                results.Where(result => result.IsFailure),
                result => Assert.Equal(TimetableErrors.ServiceCodePeriodOverlap("S101"), result.Error));
            Assert.Equal(1, await CountAsync("[timetable].[Services]"));
            Assert.Equal(3, await CountAsync("[timetable].[ServiceStops]"));
            Assert.Equal(1, await EventCountAsync(TimetableAuditActions.ServiceCreated));
        }
        finally
        {
            foreach (var provider in providers)
            {
                await provider.DisposeAsync();
            }
        }
    }

    /// <summary>S28: concurrent creates of one code with adjacent periods both succeed.</summary>
    [Fact]
    public async Task CreateService_WithParallelSameCodeAdjacentPeriods_CreatesBoth()
    {
        await using var setup = BuildServiceProvider();
        var network = await CreateNetworkAsync(setup);
        await using var first = BuildServiceProvider();
        await using var second = BuildServiceProvider();

        var results = await Task.WhenAll(
            CreateServiceAsync(first, Command(network, effectiveFrom: "2026-10-05", effectiveTo: "2026-12-31")),
            CreateServiceAsync(second, Command(network, effectiveFrom: "2027-01-01")));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(2, await CountAsync("[timetable].[Services] WHERE [Code] = N'S101'"));
        Assert.Equal(2, await EventCountAsync(TimetableAuditActions.ServiceCreated));
    }

    /// <summary>R35: the lock names one code, so a create of another code does not wait.</summary>
    [Fact]
    public async Task CreateService_WhileAnotherCodeIsLocked_DoesNotWait()
    {
        await using var setup = BuildServiceProvider();
        var network = await CreateNetworkAsync(setup);
        var held = new ServiceCodeLockGate();
        await using var holder = BuildServiceProvider(configure: services => GatedServiceCodeLock.Register(services, held));
        await using var other = BuildServiceProvider();

        var holding = CreateServiceAsync(holder, Command(network, code: "S101"));
        try
        {
            await held.Acquired.Task.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken);

            var otherCode = await CreateServiceAsync(other, Command(network, code: "S202")).WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);

            Assert.True(otherCode.IsSuccess);
            Assert.False(holding.IsCompleted, "The first create finished before its gate was released.");
        }
        finally
        {
            held.Release.TrySetResult();
        }

        Assert.True((await holding).IsSuccess);
        Assert.Equal(2, await CountAsync("[timetable].[Services]"));
    }

    /// <summary>S45: actor fields come from the authenticated context, never from the command.</summary>
    [Fact]
    public async Task CreateService_AuditActor_ComesFromCurrentUserOnly()
    {
        var realActor = Guid.Parse("0199b3a0-0000-7000-8000-00000000cccc");
        await using var provider = BuildServiceProvider(new TestCurrentUser
        {
            UserId = realActor,
            Roles = ["SystemAdministrator"],
            ClientIp = "203.0.113.10",
            CorrelationId = "corr-real",
            AuthorizedByPermission = Permissions.ServicesManage
        });
        var network = await CreateNetworkAsync(provider);

        // Every string the caller controls tries to impersonate someone else.
        await CreateServiceOrFailAsync(
            provider, Command(network, nameEn: "actorUserId=00000000-0000-0000-0000-000000000001", nameMy: "role=Admin"));

        const string Where = " FROM [audit].[AuditEvents] WHERE [Action] = N'Timetable.ServiceCreated';";
        Assert.Equal(realActor, await ScalarAsync<Guid>("SELECT [ActorUserId]" + Where));
        Assert.Equal("""["SystemAdministrator"]""", await ScalarAsync<string>("SELECT [ActorRole]" + Where));
        Assert.Equal("203.0.113.10", await ScalarAsync<string>("SELECT [ClientIp]" + Where));
        Assert.Equal("corr-real", await ScalarAsync<string>("SELECT [CorrelationId]" + Where));

        // Structurally, the command has nowhere to carry an actor.
        Assert.Equal(
            ["Code", "NameEn", "NameMy", "RouteId", "Direction", "StopStationIds", "OperatingDays", "EffectiveFrom", "EffectiveTo"],
            typeof(CreateServiceCommand).GetProperties().Select(property => property.Name));
    }

    /// <summary>S49: real Myanmar Unicode, written as escapes so no editor can normalise it.</summary>
    [Fact]
    public async Task CreateService_WithMyanmarName_RoundTripsExactly()
    {
        const string MyanmarName = "မြို့ပတ်ရထား";
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);

        await CreateServiceOrFailAsync(provider, Command(network, nameMy: MyanmarName));

        await using var scope = provider.CreateAsyncScope();
        var service = await scope.ServiceProvider.GetRequiredService<YcrDbContext>()
            .Services.AsNoTracking().SingleAsync(CancellationToken);
        Assert.Equal(MyanmarName, service.Name.My);
        Assert.Equal(MyanmarName.Length, service.Name.My.Length);
    }

    /// <summary>S52, R40: days are stored as bits and read back Monday first.</summary>
    [Fact]
    public async Task CreateService_OperatingDays_StoredAsBitsAndReadMondayFirst()
    {
        await using var provider = BuildServiceProvider();
        var network = await CreateNetworkAsync(provider);

        var id = await CreateServiceOrFailAsync(provider, Command(network, days: [DayOfWeek.Sunday, DayOfWeek.Monday]));

        Assert.Equal(
            "1000001",
            await ScalarAsync<string>(
                """
                SELECT CONCAT(CAST([RunsOnMonday] AS int), CAST([RunsOnTuesday] AS int), CAST([RunsOnWednesday] AS int),
                    CAST([RunsOnThursday] AS int), CAST([RunsOnFriday] AS int), CAST([RunsOnSaturday] AS int), CAST([RunsOnSunday] AS int))
                FROM [timetable].[Services];
                """));
        Assert.Equal(["Monday", "Sunday"], (await GetServiceAsync(provider, id)).Value.OperatingDays);
    }
}

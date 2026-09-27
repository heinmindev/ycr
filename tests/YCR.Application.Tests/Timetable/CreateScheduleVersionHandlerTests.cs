using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Common.Authorization;
using YCR.Application.Timetable;
using YCR.Application.Timetable.CreateScheduleVersion;
using YCR.Application.Timetable.CreateService;
using YCR.Application.Tests.Network;
using YCR.Domain.Common;
using YCR.Domain.Timetable;
using YCR.TestSupport;
using static YCR.Application.Tests.Timetable.ScheduleTestData;
using static YCR.Application.Tests.Timetable.TimetableTestData;

namespace YCR.Application.Tests.Timetable;

/// <summary>
/// F-005 creation scenarios against real SQL Server under <c>ycr_app</c>, today 2026-10-01
/// (Asia/Yangon): SV1, SV4-SV16, SV18, SV19, SV22, SV49, SV50, SV55; R7, R33, R41, R45, R50.
/// </summary>
public sealed class CreateScheduleVersionHandlerTests(SqlServerFixture fixture) : ScheduleHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "create_schedule";

    /// <summary>SV1, R39: the rows, the minutes and one event with the full snapshot.</summary>
    [Fact]
    public async Task CreateScheduleVersion_WithValidCommand_PersistsRowsAndOneAuditEvent()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);

        var created = await CreateVersionOrFailAsync(provider, Version("2026-10-05", [ValidS1(net.S1)]));

        Assert.Equal(1, created.Number);
        Assert.Equal(1, await CountAsync(
            $"""
            [timetable].[ScheduleVersions] WHERE [Id] = '{created.Id}' AND [Number] = 1 AND [NameEn] = N'October 2026'
              AND [NameMy] = N'အောက်တိုဘာ' AND [EffectiveFrom] = '2026-10-05' AND [Status] = N'Draft'
              AND [CreatedAtUtc] = '2026-10-01T03:00:00+00:00'
              AND [PublishedAtUtc] IS NULL AND [DiscardedAtUtc] IS NULL AND [CancelledAtUtc] IS NULL
            """));
        Assert.Equal(1, await CountAsync($"[timetable].[ScheduleVersionServices] WHERE [ScheduleVersionId] = '{created.Id}' AND [ServiceId] = '{net.S1}'"));
        Assert.Equal(
            "1::360|2:380:382|3:400:",
            await ScalarAsync<string>(
                $"""
                SELECT STRING_AGG(CONCAT([Position], N':', [ArrivalMinute], N':', [DepartureMinute]), N'|') WITHIN GROUP (ORDER BY [Position])
                FROM [timetable].[ScheduleStopTimes] WHERE [ScheduleVersionId] = '{created.Id}' AND [ServiceId] = '{net.S1}';
                """));

        Assert.Equal(1, await EventCountAsync(TimetableAuditActions.ScheduleVersionCreated));
        await using var connection = new SqlConnection(Database.MigratorConnectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = new SqlCommand(
            """
            SELECT [SubjectType], [SubjectId], [BeforeJson], [AfterJson], [AuthorizedByPermission], [ActorUserId], [PayloadVersion]
            FROM [audit].[AuditEvents] WHERE [Action] = N'Timetable.ScheduleVersionCreated';
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(CancellationToken);
        Assert.True(await reader.ReadAsync(CancellationToken));
        Assert.Equal("Timetable.ScheduleVersion", reader.GetString(0));
        Assert.Equal(created.Id, reader.GetGuid(1));
        Assert.True(reader.IsDBNull(2));
        Assert.Equal(Permissions.SchedulesManage, reader.GetString(4));
        Assert.Equal(ScheduleManager.UserId, reader.GetGuid(5));
        Assert.Equal(1, reader.GetInt32(6));

        using var after = JsonDocument.Parse(reader.GetString(3));
        var root = after.RootElement;
        Assert.Equal(1, root.GetProperty("number").GetInt32());
        Assert.Equal("2026-10-05", root.GetProperty("effectiveFrom").GetString());
        Assert.Equal("Draft", root.GetProperty("status").GetString());
        var service = Assert.Single(root.GetProperty("services").EnumerateArray());
        Assert.Equal(net.S1, service.GetProperty("serviceId").GetGuid());
        Assert.Equal("S101", service.GetProperty("serviceCode").GetString());
        Assert.Equal(3, service.GetProperty("stopCount").GetInt32());
        Assert.Equal(
            ScheduleStopTimesDigest.Compute(
            [
                new(net.S1, 1, null, 360),
                new(net.S1, 2, 380, 382),
                new(net.S1, 3, 400, null)
            ]),
            root.GetProperty("stopTimesSha256").GetString());
    }

    /// <summary>SV4, R7: 1, 2; discard 2; 3 — never reused; a cancelled version keeps its number.</summary>
    [Fact]
    public async Task CreateScheduleVersion_Numbers_AreContiguousAndNeverReused()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);

        var first = await CreateVersionOrFailAsync(provider, Version("2026-10-05", [ValidS1(net.S1)]));
        var second = await CreateVersionOrFailAsync(provider, Version("2026-10-06", [ValidS1(net.S1)]));
        Assert.True((await DiscardAsync(provider, second.Id)).IsSuccess);
        var third = await CreateVersionOrFailAsync(provider, Version("2026-10-07", [ValidS1(net.S1)]));
        Assert.True((await PublishAsync(provider, third.Id)).IsSuccess);
        Assert.True((await CancelAsync(provider, third.Id)).IsSuccess);
        var fourth = await CreateVersionOrFailAsync(provider, Version("2026-10-08", [ValidS1(net.S1)]));

        Assert.Equal([1, 2, 3, 4], [first.Number, second.Number, third.Number, fourth.Number]);
        Assert.Equal(
            "1:Draft|2:Discarded|3:Cancelled|4:Draft",
            await ScalarAsync<string>(
                "SELECT STRING_AGG(CONCAT([Number], N':', [Status]), N'|') WITHIN GROUP (ORDER BY [Number]) FROM [timetable].[ScheduleVersions];"));
    }

    /// <summary>SV5: real Myanmar Unicode text round-trips unchanged.</summary>
    [Fact]
    public async Task CreateScheduleVersion_WithMyanmarName_RoundTripsExactly()
    {
        const string myanmar = "ရန်ကုန်မြို့ပတ်ရထား အချိန်ဇယား";
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);

        var created = await CreateVersionOrFailAsync(provider, Version("2026-10-05", [ValidS1(net.S1)], nameMy: $"  {myanmar}  "));

        Assert.Equal(myanmar, await ScalarAsync<string>($"SELECT [NameMy] FROM [timetable].[ScheduleVersions] WHERE [Id] = '{created.Id}';"));
    }

    public static TheoryData<string, (string?, string?)[], string> InvalidTimes => new()
    {
        { "SV6 arrival at stop 1", [("05:59", "06:00"), ("06:20", "06:22"), ("06:40", null)], "Timetable.ScheduleStopTimeUnexpected" },
        { "SV6 departure at the last stop", [(null, "06:00"), ("06:20", "06:22"), ("06:40", "06:45")], "Timetable.ScheduleStopTimeUnexpected" },
        { "SV7 departure missing at C", [(null, "06:00"), ("06:20", null), ("06:40", null)], "Timetable.ScheduleStopTimesIncomplete" },
        { "SV7 arrival missing at E", [(null, "06:00"), ("06:20", "06:22"), (null, null)], "Timetable.ScheduleStopTimesIncomplete" },
        { "SV7 departure missing at A", [(null, null), ("06:20", "06:22"), ("06:40", null)], "Timetable.ScheduleStopTimesIncomplete" },
        { "SV9 negative dwell", [(null, "06:00"), ("06:22", "06:20"), ("06:40", null)], "Timetable.ScheduleDwellNegative" },
        { "SV10 equal", [(null, "06:00"), ("06:00", "06:22"), ("06:40", null)], "Timetable.ScheduleTimesNotIncreasing" },
        { "SV10 earlier", [(null, "06:00"), ("05:59", "06:22"), ("06:40", null)], "Timetable.ScheduleTimesNotIncreasing" },
        { "SV10 E after C equal", [(null, "06:00"), ("06:20", "06:22"), ("06:22", null)], "Timetable.ScheduleTimesNotIncreasing" },
        { "SV12 crossing midnight", [(null, "23:50"), ("00:10", "00:12"), ("00:30", null)], "Timetable.ScheduleTimesNotIncreasing" },
    };

    /// <summary>SV6, SV7, SV9, SV10, SV12, SV50: each refused with its code, nothing written.</summary>
    [Theory]
    [MemberData(nameof(InvalidTimes))]
    public async Task CreateScheduleVersion_WithInvalidTimes_ReturnsErrorAndWritesNothing(
        string description, (string?, string?)[] stops, string code)
    {
        _ = description;
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);

        var result = await CreateVersionAsync(provider, Version("2026-10-05", [Times(net.S1, stops)]));

        Assert.True(result.IsFailure);
        Assert.Equal(code, result.Error.Code);
        Assert.Equal(ErrorType.BusinessRule, result.Error.Type);
        await AssertNoScheduleRowsAsync();
    }

    /// <summary>SV7 (positions), SV8: position 2 omitted or given twice; position 4 of three.</summary>
    [Theory]
    [InlineData("1,3", "Timetable.ScheduleStopTimesIncomplete")]
    [InlineData("1,2,2,3", "Timetable.ScheduleStopTimesIncomplete")]
    [InlineData("1,2,3,4", "Timetable.ScheduleStopNotInService")]
    public async Task CreateScheduleVersion_WithBadPositions_ReturnsErrorAndWritesNothing(string positions, string code)
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var items = positions.Split(',').Select(int.Parse).Select(position => position switch
        {
            1 => new CreateScheduleStopTimeItem(1, null, "06:00"),
            2 => new CreateScheduleStopTimeItem(2, "06:20", "06:22"),
            3 => new CreateScheduleStopTimeItem(3, "06:40", null),
            _ => new CreateScheduleStopTimeItem(position, "06:50", null)
        });

        var result = await CreateVersionAsync(provider, Version("2026-10-05", [new CreateScheduleServiceItem(net.S1, [.. items])]));

        Assert.Equal(code, result.Error.Code);
        await AssertNoScheduleRowsAsync();
    }

    /// <summary>SV11: the seven malformed strings are 400 InvalidTimetableTime; nothing written.</summary>
    [Theory]
    [InlineData("24:00")]
    [InlineData("24:15")]
    [InlineData("6:00")]
    [InlineData("06:60")]
    [InlineData("06:00:30")]
    [InlineData("0600")]
    [InlineData("")]
    public async Task CreateScheduleVersion_WithBadTimeFormat_ReturnsInvalidTimetableTimeAndWritesNothing(string text)
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);

        var result = await CreateVersionAsync(provider, Version("2026-10-05", [Times(net.S1, (null, "06:00"), (text, "06:22"), ("06:40", null))]));

        AssertFailure(result, TimetableErrors.InvalidTimetableTime);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        await AssertNoScheduleRowsAsync();
    }

    /// <summary>SV9 (dwell 0) and SV11 (00:00 … 23:59) are accepted.</summary>
    [Theory]
    [InlineData(null, "06:00", "06:20", "06:20", "06:40")]
    [InlineData(null, "00:00", "12:00", "12:01", "23:59")]
    public async Task CreateScheduleVersion_WithEdgeTimes_Creates(string? arrivalA, string departureA, string arrivalC, string departureC, string arrivalE)
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);

        var result = await CreateVersionAsync(provider, Version("2026-10-05", [Times(net.S1, (arrivalA, departureA), (arrivalC, departureC), (arrivalE, null))]));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, await CountAsync("[timetable].[ScheduleStopTimes]"));
    }

    /// <summary>SV13: a full circuit's closing stop (C again) has an arrival only.</summary>
    [Fact]
    public async Task CreateScheduleVersion_FullCircuit_Creates()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var s3 = await CreateServiceOrFailAsync(provider, Command(net.Network, stops: "CDEABC", code: "S303"));

        var created = await CreateVersionOrFailAsync(provider, Version("2026-10-05",
        [
            Times(s3, (null, "08:00"), ("08:10", "08:11"), ("08:20", "08:21"), ("08:30", "08:31"), ("08:40", "08:41"), ("08:50", null))
        ]));

        Assert.Equal(1, await CountAsync(
            $"[timetable].[ScheduleStopTimes] WHERE [ScheduleVersionId] = '{created.Id}' AND [Position] = 6 AND [ArrivalMinute] = 530 AND [DepartureMinute] IS NULL"));
        Assert.Equal(6, await CountAsync("[timetable].[ScheduleStopTimes]"));
    }

    /// <summary>SV14: an unknown service, or S1 listed twice; nothing written.</summary>
    [Fact]
    public async Task CreateScheduleVersion_WithUnknownOrRepeatedService_ReturnsErrorAndWritesNothing()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var unknown = Guid.Parse("0199b3a0-0000-7000-8000-0000000000ff");

        var notFound = await CreateVersionAsync(provider, Version("2026-10-05", [ValidS1(net.S1), ValidThreeStop(unknown)]));
        var repeated = await CreateVersionAsync(provider, Version("2026-10-05", [ValidS1(net.S1), ValidS1(net.S1)]));

        AssertFailure(notFound, TimetableErrors.ScheduleServiceNotFound(unknown));
        AssertFailure(repeated, TimetableErrors.ScheduleServiceRepeated(net.S1));
        await AssertNoScheduleRowsAsync();
    }

    /// <summary>SV15: not effective on 2026-10-05 in each of the four ways; nothing written.</summary>
    [Theory]
    [InlineData("starts later")]
    [InlineData("ended before")]
    [InlineData("withdrawn from the start date")]
    [InlineData("never runs")]
    public async Task CreateScheduleVersion_WithServiceNotEffectiveOnStartDate_ReturnsNotEffectiveAndWritesNothing(string kind)
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var service = kind switch
        {
            "starts later" => await CreateServiceOrFailAsync(provider, Command(net.Network, code: "S401", effectiveFrom: "2026-10-06")),
            "ended before" => await CreateServiceOrFailAsync(provider, Command(net.Network, code: "S401", effectiveFrom: "2026-09-01", effectiveTo: "2026-10-04")),
            _ => await CreateServiceOrFailAsync(provider, Command(net.Network, code: "S401", effectiveFrom: kind == "never runs" ? "2026-10-10" : "2026-09-01")),
        };
        if (kind is "withdrawn from the start date" or "never runs")
        {
            Assert.True((await WithdrawServiceAsync(provider, service, Date("2026-10-05"))).IsSuccess);
        }

        var result = await CreateVersionAsync(provider, Version("2026-10-05", [ValidThreeStop(service)]));

        AssertFailure(result, TimetableErrors.ScheduleServiceNotEffective(service));
        await AssertNoScheduleRowsAsync();
    }

    /// <summary>SV16: effective on the start date only just, or from the past (OQ50).</summary>
    [Theory]
    [InlineData("2026-10-05", "2026-10-05")]
    [InlineData("2026-09-01", null)]
    public async Task CreateScheduleVersion_WithServiceEffectiveOnlyOnStartDateOrFromThePast_Creates(string from, string? to)
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var service = await CreateServiceOrFailAsync(provider, Command(net.Network, code: "S401", effectiveFrom: from, effectiveTo: to));

        var result = await CreateVersionAsync(provider, Version("2026-10-05", [ValidThreeStop(service)]));

        Assert.True(result.IsSuccess);
    }

    /// <summary>SV18: a blank or 101-character name after trimming, or a missing one.</summary>
    [Theory]
    [InlineData("", "M")]
    [InlineData("   ", "M")]
    [InlineData("October", "")]
    [InlineData(null, "M")]
    [InlineData("101", "M")]
    [InlineData("October", "101")]
    public async Task CreateScheduleVersion_WithInvalidName_ReturnsInvalidScheduleVersionName(string? nameEn, string? nameMy)
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        static string? Expand(string? value) => value == "101" ? new string('a', 101) : value;

        var result = await CreateVersionAsync(provider, Version("2026-10-05", [ValidS1(net.S1)], Expand(nameEn), Expand(nameMy)));

        AssertFailure(result, TimetableErrors.InvalidScheduleVersionName);
        await AssertNoScheduleRowsAsync();
    }

    /// <summary>SV19: before today refused, nothing written; today (listing a service effective on it) created.</summary>
    [Fact]
    public async Task CreateScheduleVersion_WithStartBeforeToday_ReturnsInPastAndWritesNothing()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var service = await CreateServiceOrFailAsync(provider, Command(net.Network, code: "S401", effectiveFrom: "2026-09-01"));

        var result = await CreateVersionAsync(provider, Version("2026-09-30", [ValidThreeStop(service)]));

        AssertFailure(result, TimetableErrors.ScheduleVersionEffectiveFromInPast);
        await AssertNoScheduleRowsAsync();
    }

    /// <summary>SV19 (Amendment 3): a version listing a service effective on 2026-10-01, starting today.</summary>
    [Fact]
    public async Task CreateScheduleVersion_WithStartToday_Creates()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var service = await CreateServiceOrFailAsync(provider, Command(net.Network, code: "S401", effectiveFrom: "2026-09-01"));

        var created = await CreateVersionOrFailAsync(provider, Version("2026-10-01", [ValidThreeStop(service)]));

        Assert.Equal("Draft", await StatusOfAsync(created.Id));
    }

    /// <summary>
    /// R33: "today" is the Asia/Yangon date. An empty version from 2026-10-01 is allowed while it is
    /// still 2026-09-30 in Yangon (17:29:59Z) and refused from Yangon midnight (17:30:00Z), when it
    /// would start today (R50).
    /// </summary>
    [Theory]
    [InlineData("2026-09-30T17:29:59+00:00", true)]
    [InlineData("2026-09-30T17:30:00+00:00", false)]
    public async Task CreateScheduleVersion_AtYangonMidnight_UsesTheYangonDateAsToday(string nowUtc, bool created)
    {
        await using var provider = BuildScheduleProvider(new TestClock(DateTimeOffset.Parse(nowUtc, System.Globalization.CultureInfo.InvariantCulture)));

        var result = await CreateVersionAsync(provider, Version("2026-10-01", []));

        if (created)
        {
            Assert.True(result.IsSuccess);
        }
        else
        {
            AssertFailure(result, TimetableErrors.EmptyScheduleVersionNotInFuture);
        }
    }

    /// <summary>SV22, R9: an empty version is a header only; its snapshot has no services and the empty digest.</summary>
    [Fact]
    public async Task CreateScheduleVersion_WithNoServices_CreatesHeaderOnlyAndSnapshotWithEmptyServices()
    {
        await using var provider = BuildScheduleProvider();

        var created = await CreateVersionOrFailAsync(provider, Version("2026-10-02", []));

        Assert.Equal(1, created.Number);
        await AssertNoScheduleRowsAsync(versions: 1, events: 1);
        using var after = JsonDocument.Parse((await ScalarAsync<string>(
            "SELECT [AfterJson] FROM [audit].[AuditEvents] WHERE [Action] = N'Timetable.ScheduleVersionCreated';"))!);
        Assert.Equal(0, after.RootElement.GetProperty("services").GetArrayLength());
        Assert.Equal(
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            after.RootElement.GetProperty("stopTimesSha256").GetString());
    }

    /// <summary>
    /// SV55 at creation (Amendment 2): an empty version from today is refused, no number is used; from
    /// yesterday the past-date error comes first.
    /// </summary>
    [Fact]
    public async Task CreateScheduleVersion_WithNoServicesStartingToday_ReturnsEmptyNotInFutureAndWritesNothing()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);

        AssertFailure(await CreateVersionAsync(provider, Version("2026-10-01", [])), TimetableErrors.EmptyScheduleVersionNotInFuture);
        AssertFailure(await CreateVersionAsync(provider, Version("2026-09-30", [])), TimetableErrors.ScheduleVersionEffectiveFromInPast);
        await AssertNoScheduleRowsAsync();

        var next = await CreateVersionOrFailAsync(provider, Version("2026-10-05", [ValidS1(net.S1)]));
        Assert.Equal(1, next.Number);
    }

    public static TheoryData<string, string, string?, (string?, string?)[], string> SeveralFailures => new()
    {
        // 2 before 3: a bad name and a bad time.
        { "2 name before 3 time", "2026-10-05", "", [(null, "6:00"), ("06:20", "06:22"), ("06:40", null)], "Timetable.InvalidScheduleVersionName" },
        // 3 before 4: a bad time and a past start date.
        { "3 time before 4 past", "2026-09-30", "October", [(null, "6:00"), ("06:20", "06:22"), ("06:40", null)], "Timetable.InvalidTimetableTime" },
        // 4 before 6-13: a past start date and a bad dwell.
        { "4 past before 12 dwell", "2026-09-30", "October", [(null, "06:00"), ("06:22", "06:20"), ("06:40", null)], "Timetable.ScheduleVersionEffectiveFromInPast" },
        // 11 before 12: an unexpected time and a bad dwell.
        { "11 unexpected before 12 dwell", "2026-10-05", "October", [("05:00", "06:00"), ("06:22", "06:20"), ("06:40", null)], "Timetable.ScheduleStopTimeUnexpected" },
        // 12 before 13: a bad dwell and times not increasing.
        { "12 dwell before 13 increasing", "2026-10-05", "October", [(null, "06:00"), ("06:22", "06:20"), ("06:10", null)], "Timetable.ScheduleDwellNegative" },
    };

    /// <summary>R45 (plan P4): the first failure in the fixed order is the one returned.</summary>
    [Theory]
    [MemberData(nameof(SeveralFailures))]
    public async Task CreateScheduleVersion_WithSeveralFailures_ReturnsTheFirstInR45Order(
        string description, string effectiveFrom, string? nameEn, (string?, string?)[] stops, string code)
    {
        _ = description;
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);

        var result = await CreateVersionAsync(provider, Version(effectiveFrom, [Times(net.S1, stops)], nameEn));

        Assert.Equal(code, result.Error.Code);
        await AssertNoScheduleRowsAsync();
    }

    /// <summary>R45 checks 4-5 and 6-8 across services: past before empty; one service at a time.</summary>
    [Fact]
    public async Task CreateScheduleVersion_WithSeveralFailuresAcrossServices_ReturnsTheFirstInR45Order()
    {
        await using var provider = BuildScheduleProvider();
        var net = await CreateScheduleNetworkAsync(provider);
        var unknown = Guid.Parse("0199b3a0-0000-7000-8000-0000000000ff");
        var later = await CreateServiceOrFailAsync(provider, Command(net.Network, code: "S401", effectiveFrom: "2026-10-06"));

        // 6 before 8 in one service is impossible to separate; across services, the first service's
        // late check (13) beats the second service's first check (6).
        var firstServiceFirst = await CreateVersionAsync(provider, Version("2026-10-05",
            [Times(net.S1, (null, "06:00"), ("05:00", "05:01"), ("06:40", null)), ValidThreeStop(unknown)]));
        Assert.Equal("Timetable.ScheduleTimesNotIncreasing", firstServiceFirst.Error.Code);

        // 6 before 8: an unknown first service and a not-effective second.
        var unknownFirst = await CreateVersionAsync(provider, Version("2026-10-05", [ValidThreeStop(unknown), ValidThreeStop(later)]));
        AssertFailure(unknownFirst, TimetableErrors.ScheduleServiceNotFound(unknown));

        // 8 before 13 in one service: not effective and a bad time order.
        var notEffectiveFirst = await CreateVersionAsync(provider, Version("2026-10-05",
            [Times(later, (null, "06:00"), ("05:00", "05:01"), ("06:40", null))]));
        AssertFailure(notEffectiveFirst, TimetableErrors.ScheduleServiceNotEffective(later));

        await AssertNoScheduleRowsAsync();
    }

    /// <summary>SV49: the audit actor comes from the authenticated context, never the command.</summary>
    [Fact]
    public async Task CreateScheduleVersion_AuditActor_ComesFromCurrentUserOnly()
    {
        var other = new TestCurrentUser
        {
            UserId = Guid.Parse("0199b3a0-0000-7000-8000-00000000dddd"),
            Roles = ["SystemAdministrator"],
            ClientIp = "203.0.113.4",
            CorrelationId = "corr-other",
            AuthorizedByPermission = Permissions.SchedulesManage
        };
        await using var provider = BuildServiceProvider(other);

        await CreateVersionOrFailAsync(provider, Version("2026-10-02", []));

        Assert.Equal(other.UserId, await ScalarAsync<Guid>("SELECT [ActorUserId] FROM [audit].[AuditEvents] WHERE [Action] = N'Timetable.ScheduleVersionCreated';"));
        Assert.Equal("[\"SystemAdministrator\"]", await ScalarAsync<string>("SELECT [ActorRole] FROM [audit].[AuditEvents] WHERE [Action] = N'Timetable.ScheduleVersionCreated';"));
    }

    /// <summary>
    /// SV50, P5: a writer that bypasses the lock takes the next number between the handler's read
    /// and its save; the unique index refuses the insert, the create fails (the opaque 500 at the
    /// API), and nothing of the create is written.
    /// </summary>
    [Fact]
    public async Task CreateScheduleVersion_WhenNumberIsTakenBehindTheLock_FailsAndWritesNothing()
    {
        var migrator = Database.MigratorConnectionString;
        var intruder = Guid.Parse("0199b3a0-0000-7000-8000-00000000eeee");
        await using var provider = BuildScheduleProvider(configure: services => AuditRecordHook.Register(services, action =>
        {
            if (action != TimetableAuditActions.ScheduleVersionCreated)
            {
                return;
            }

            using var connection = new SqlConnection(migrator);
            connection.Open();
            using var command = new SqlCommand(
                $"""
                INSERT INTO [timetable].[ScheduleVersions] ([Id], [Number], [NameEn], [NameMy], [EffectiveFrom], [Status], [CreatedAtUtc])
                VALUES ('{intruder}', 1, N'Intruder', N'Intruder', '2026-12-01', N'Draft', '2026-10-01T03:00:00+00:00');
                """,
                connection);
            command.ExecuteNonQuery();
        }));

        await Assert.ThrowsAnyAsync<Exception>(() => CreateVersionAsync(provider, Version("2026-10-02", [])));

        Assert.Equal(1, await CountAsync("[timetable].[ScheduleVersions]"));
        Assert.Equal(1, await CountAsync($"[timetable].[ScheduleVersions] WHERE [Id] = '{intruder}'"));
        Assert.Equal(0, await EventCountAsync(TimetableAuditActions.ScheduleVersionCreated));
    }

    /// <summary>
    /// R41, SV47, plan V2: a whole network at the caps — 250 services × 40 stops, 10,000 stop times —
    /// is created in one request. The time it holds the Timetable-wide lock is written to the test
    /// output for <c>progress.md</c>; nothing asserts on it.
    /// </summary>
    [Fact]
    public async Task CreateScheduleVersion_WholeNetworkAtTheCaps_PersistsEveryRow()
    {
        await using var provider = BuildScheduleProvider();
        var stations = new List<Guid>();
        for (var index = 1; index <= 40; index++)
        {
            stations.Add(await CreateStationAsync(provider, $"C{index:00}"));
        }

        var route = await CreateRouteOrFailAsync(provider, "BIG", isClosed: false, stations);
        var services = new List<Guid>();
        for (var index = 1; index <= 250; index++)
        {
            services.Add(await CreateServiceOrFailAsync(provider, new CreateServiceCommand(
                $"B{index:000}", "Service", "ရထား", route, "Forward", stations, Weekdays, Date("2026-10-05"), null)));
        }

        var items = services.Select(service => new CreateScheduleServiceItem(service,
        [
            .. Enumerable.Range(1, 40).Select(position => new CreateScheduleStopTimeItem(
                position,
                position == 1 ? null : Clock(10 * (position - 1)),
                position == 40 ? null : Clock(10 * (position - 1) + (position == 1 ? 0 : 1))))
        ])).ToList();

        var watch = Stopwatch.StartNew();
        var created = await CreateVersionOrFailAsync(provider, Version("2026-10-05", items));
        watch.Stop();
        TestContext.Current.TestOutputHelper?.WriteLine($"V2: create at the caps (250 × 40 = 10,000 stop times) took {watch.ElapsedMilliseconds} ms.");

        await AssertNoScheduleRowsAsync(versions: 1, entries: 250, stopTimes: 10_000, events: 1);
        Assert.Equal(created.Id, await ScalarAsync<Guid>("SELECT [Id] FROM [timetable].[ScheduleVersions];"));

        static string Clock(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";
    }
}

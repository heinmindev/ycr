using System.Reflection;
using YCR.Domain.Common;
using YCR.Domain.Timetable;

namespace YCR.Domain.Tests.Timetable;

/// <summary>
/// The <see cref="ScheduleVersion"/> aggregate (F-005 plan §Domain changes). As spec §4: today is
/// 2026-10-01; S1 is <c>[A, C, E]</c> (3 stops) from 2026-10-05, open-ended; a valid create lists
/// S1 with <c>A dep 06:00</c>, <c>C arr 06:20 dep 06:22</c>, <c>E arr 06:40</c>.
/// </summary>
public sealed class ScheduleVersionTests
{
    private static readonly DateOnly Today = new(2026, 10, 1);
    private static readonly DateOnly Start = new(2026, 10, 5);
    private static readonly DateTimeOffset NowUtc = new(2026, 10, 1, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LaterUtc = new(2026, 10, 2, 4, 0, 0, TimeSpan.Zero);
    private static readonly Guid VersionId = Guid.Parse("5c000000-0000-0000-0000-000000000001");
    private static readonly Guid S1 = Guid.Parse("5e000000-0000-0000-0000-000000000001");
    private static readonly Guid S2 = Guid.Parse("5e000000-0000-0000-0000-000000000002");
    private static readonly Guid S3 = Guid.Parse("5e000000-0000-0000-0000-000000000003");
    private static readonly Guid Unknown = Guid.Parse("5e000000-0000-0000-0000-0000000000ff");

    // ----- Creation (P4 checks 6-13) -----

    [Fact]
    public void CreateDraft_WithValidInput_BuildsDraftWithEntriesAndMinutes()
    {
        var result = Create(Start, [ValidS1()]);

        Assert.True(result.IsSuccess);
        var version = result.Value;
        Assert.Equal(VersionId, version.Id);
        Assert.Equal(1, version.Number);
        Assert.Equal("October", version.Name.En);
        Assert.Equal(Start, version.EffectiveFrom);
        Assert.Equal(ScheduleVersionStatus.Draft, version.Status);
        Assert.Equal(NowUtc, version.CreatedAtUtc);
        Assert.Null(version.PublishedAtUtc);
        Assert.Null(version.DiscardedAtUtc);
        Assert.Null(version.CancelledAtUtc);
        Assert.Empty(version.DomainEvents);
        var entry = Assert.Single(version.Services);
        Assert.Equal(VersionId, entry.ScheduleVersionId);
        Assert.Equal(S1, entry.ServiceId);
        Assert.Equal(
            [(1, (short?)null, (short?)360), (2, (short?)380, (short?)382), (3, (short?)400, (short?)null)],
            entry.StopTimes.Select(stop => (stop.Position, stop.Arrival?.Minutes, stop.Departure?.Minutes)));
        Assert.All(entry.StopTimes, stop => Assert.Equal((VersionId, S1), (stop.ScheduleVersionId, stop.ServiceId)));
    }

    [Fact]
    public void CreateDraft_KeepsEntriesInRequestOrderAndStopTimesInPositionOrder()
    {
        var s2 = new ScheduleServiceText(S2, [new(3, "07:40", null), new(1, null, "07:00"), new(2, "07:20", "07:21")]);

        var version = Create(Start, [s2, ValidS1()]).Value;

        Assert.Equal([S2, S1], version.Services.Select(entry => entry.ServiceId));
        Assert.Equal([1, 2, 3], version.Services[0].StopTimes.Select(stop => stop.Position));
    }

    [Fact]
    public void CreateDraft_WithNoServices_BuildsAnEmptyDraft()
    {
        var result = Create(Start, []);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Services);
        Assert.Equal(ScheduleVersionStatus.Draft, result.Value.Status);
    }

    public static TheoryData<string, ScheduleServiceText, int> UnexpectedTimes => new()
    {
        { "arrival at stop 1", Times(S1, ("05:59", "06:00"), ("06:20", "06:22"), ("06:40", null)), 1 },
        { "departure at the last stop", Times(S1, (null, "06:00"), ("06:20", "06:22"), ("06:40", "06:45")), 3 },
    };

    [Theory]
    [MemberData(nameof(UnexpectedTimes))]
    public void CreateDraft_WithUnexpectedTime_ReturnsStopTimeUnexpected(
        string description, ScheduleServiceText service, int position)
    {
        _ = description;
        var result = Create(Start, [service]);

        AssertFailure(result, "Timetable.ScheduleStopTimeUnexpected", S1, position);
    }

    public static TheoryData<string, ScheduleServiceText, int> IncompleteTimes => new()
    {
        { "departure missing at C", Times(S1, (null, "06:00"), ("06:20", null), ("06:40", null)), 2 },
        { "arrival missing at E", Times(S1, (null, "06:00"), ("06:20", "06:22"), (null, null)), 3 },
        { "departure missing at A", Times(S1, (null, null), ("06:20", "06:22"), ("06:40", null)), 1 },
        { "position 2 omitted", new(S1, [new(1, null, "06:00"), new(3, "06:40", null)]), 2 },
        { "position 2 twice", new(S1, [new(1, null, "06:00"), new(2, "06:20", "06:22"), new(2, "06:20", "06:22"), new(3, "06:40", null)]), 2 },
    };

    [Theory]
    [MemberData(nameof(IncompleteTimes))]
    public void CreateDraft_WithMissingOrRepeatedTimes_ReturnsStopTimesIncomplete(
        string description, ScheduleServiceText service, int position)
    {
        _ = description;
        var result = Create(Start, [service]);

        AssertFailure(result, "Timetable.ScheduleStopTimesIncomplete", S1, position);
    }

    [Fact]
    public void CreateDraft_WithPositionBeyondTheStops_ReturnsStopNotInService()
    {
        var service = new ScheduleServiceText(
            S1, [new(1, null, "06:00"), new(2, "06:20", "06:22"), new(3, "06:40", null), new(4, "06:50", null)]);

        var result = Create(Start, [service]);

        AssertFailure(result, "Timetable.ScheduleStopNotInService", S1, 4);
    }

    [Fact]
    public void CreateDraft_WithDepartureBeforeArrival_ReturnsDwellNegative()
    {
        var result = Create(Start, [Times(S1, (null, "06:00"), ("06:22", "06:20"), ("06:40", null))]);

        AssertFailure(result, "Timetable.ScheduleDwellNegative", S1, 2);
    }

    [Fact]
    public void CreateDraft_WithZeroDwell_Succeeds()
    {
        var result = Create(Start, [Times(S1, (null, "06:00"), ("06:20", "06:20"), ("06:40", null))]);

        Assert.True(result.IsSuccess);
        Assert.Equal((short)380, result.Value.Services[0].StopTimes[1].Departure!.Minutes);
    }

    [Theory]
    [InlineData("06:00", "06:00", "06:22", "06:40", 2)] // C arrives when A departs
    [InlineData("06:00", "05:59", "06:22", "06:40", 2)] // C arrives before A departs
    [InlineData("06:00", "06:20", "06:22", "06:22", 3)] // E arrives when C departs
    public void CreateDraft_WithTimesNotIncreasing_ReturnsTimesNotIncreasing(
        string depA, string arrC, string depC, string arrE, int position)
    {
        var result = Create(Start, [Times(S1, (null, depA), (arrC, depC), (arrE, null))]);

        AssertFailure(result, "Timetable.ScheduleTimesNotIncreasing", S1, position);
    }

    [Fact]
    public void CreateDraft_FromMidnightToLastMinute_Succeeds()
    {
        var result = Create(Start, [Times(S1, (null, "00:00"), ("12:00", "12:01"), ("23:59", null))]);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [(short?)0, (short?)721, (short?)null],
            result.Value.Services[0].StopTimes.Select(stop => stop.Departure?.Minutes));
        Assert.Equal((short)1439, result.Value.Services[0].StopTimes[2].Arrival!.Minutes);
    }

    [Fact]
    public void CreateDraft_CrossingMidnight_ReturnsTimesNotIncreasing()
    {
        // SV12: A dep 23:50, C arr 00:10 dep 00:12, E arr 00:30 would cross midnight (R15).
        var result = Create(Start, [Times(S1, (null, "23:50"), ("00:10", "00:12"), ("00:30", null))]);

        AssertFailure(result, "Timetable.ScheduleTimesNotIncreasing", S1, 2);
    }

    [Fact]
    public void CreateDraft_FullCircuit_TimesTheClosingStopLast()
    {
        // SV13: S3 [C, D, E, A, B, C]: stop 1 departs only, stops 2-5 both, stop 6 (C again) arrives only.
        var s3 = Times(
            S3, (null, "08:00"), ("08:10", "08:11"), ("08:20", "08:21"), ("08:30", "08:31"), ("08:40", "08:41"), ("08:50", null));

        var result = Create(Start, [s3]);

        Assert.True(result.IsSuccess);
        var closing = result.Value.Services[0].StopTimes[^1];
        Assert.Equal(6, closing.Position);
        Assert.Equal((short)530, closing.Arrival!.Minutes);
        Assert.Null(closing.Departure);

        var closingDeparts = Times(
            S3, (null, "08:00"), ("08:10", "08:11"), ("08:20", "08:21"), ("08:30", "08:31"), ("08:40", "08:41"), ("08:50", "08:55"));
        AssertFailure(Create(Start, [closingDeparts]), "Timetable.ScheduleStopTimeUnexpected", S3, 6);
    }

    [Fact]
    public void CreateDraft_WithUnknownOrRepeatedService_ReturnsServiceNotFoundOrRepeated()
    {
        AssertFailure(Create(Start, [ValidS1(), Valid(Unknown)]), "Timetable.ScheduleServiceNotFound", Unknown, null);
        AssertFailure(Create(Start, [ValidS1(), ValidS1()]), "Timetable.ScheduleServiceRepeated", S1, null);
    }

    public static TheoryData<string, ScheduleServiceFacts> NotEffective => new()
    {
        { "starts later", new(S1, "S1", new DateOnly(2026, 10, 6), null, 3) },
        { "ended before", new(S1, "S1", new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 3), 3) },
        { "withdrawn from the start date", new(S1, "S1", new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 4), 3) },
        { "never runs", new(S1, "S1", new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 4), 3) },
    };

    [Theory]
    [MemberData(nameof(NotEffective))]
    public void CreateDraft_WithServiceNotEffectiveOnStartDate_ReturnsServiceNotEffective(
        string description, ScheduleServiceFacts s1)
    {
        _ = description;
        var result = ScheduleVersion.CreateDraft(VersionId, 1, Input(Start, [ValidS1()]), [s1], NowUtc);

        AssertFailure(result, "Timetable.ScheduleServiceNotEffective", S1, null);
    }

    public static TheoryData<string, ScheduleServiceFacts> EffectiveOnStart => new()
    {
        { "ends on the start date", new(S1, "S1", new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 5), 3) },
        { "started in the past", new(S1, "S1", new DateOnly(2026, 9, 1), null, 3) },
        { "starts on the start date", new(S1, "S1", new DateOnly(2026, 10, 5), null, 3) },
    };

    [Theory]
    [MemberData(nameof(EffectiveOnStart))]
    public void CreateDraft_WithServiceEffectiveOnlyOnStartDateOrFromThePast_Succeeds(
        string description, ScheduleServiceFacts s1)
    {
        _ = description;
        var result = ScheduleVersion.CreateDraft(VersionId, 1, Input(Start, [ValidS1()]), [s1], NowUtc);

        Assert.True(result.IsSuccess);
    }

    public static TheoryData<string, ScheduleServiceText[], string, Guid> SeveralFailures => new()
    {
        // 6 before 7: an unknown service listed twice is reported as not found.
        { "6 exists before 7 repeated", [Valid(Unknown), Valid(Unknown)], "Timetable.ScheduleServiceNotFound", Unknown },
        // 7 before 12: the second occurrence of S1 is reported as repeated before its bad dwell.
        { "7 repeated before 12 dwell", [ValidS1(), BadDwell(S1)], "Timetable.ScheduleServiceRepeated", S1 },
        // 8 before 9: not effective, and a position beyond the stops.
        { "8 effective before 9 position", [BeyondStops(S2)], "Timetable.ScheduleServiceNotEffective", S2 },
        // 9 before 10: position 4 and position 2 missing.
        { "9 position before 10 incomplete", [new(S1, [new(1, null, "06:00"), new(3, "06:40", null), new(4, "06:50", null)])], "Timetable.ScheduleStopNotInService", S1 },
        // 10 before 11: position 3 missing, and an arrival at stop 1.
        { "10 incomplete before 11 unexpected", [new(S1, [new(1, "05:00", "06:00"), new(2, "06:20", "06:22")])], "Timetable.ScheduleStopTimesIncomplete", S1 },
        // 11 before 12: arrival at stop 1, and a negative dwell at stop 2.
        { "11 unexpected before 12 dwell", [Times(S1, ("05:00", "06:00"), ("06:22", "06:20"), ("06:40", null))], "Timetable.ScheduleStopTimeUnexpected", S1 },
        // 11 missing before 12: departure missing at stop 1... and a negative dwell at stop 2.
        { "11 missing before 12 dwell", [Times(S1, (null, null), ("06:22", "06:20"), ("06:40", null))], "Timetable.ScheduleStopTimesIncomplete", S1 },
        // 12 before 13: negative dwell at stop 2 and not increasing at stop 3.
        { "12 dwell before 13 increasing", [Times(S1, (null, "06:00"), ("06:22", "06:20"), ("06:10", null))], "Timetable.ScheduleDwellNegative", S1 },
        // One service at a time: the second service fails, the first is valid.
        { "second service fails, first valid", [ValidS1(), BadDwell(S3Short)], "Timetable.ScheduleDwellNegative", S3Short },
        // One service at a time: the first service's late check beats the second's early check.
        { "first service's check 13 before second's check 6", [Times(S1, (null, "06:00"), ("05:00", "05:01"), ("06:40", null)), Valid(Unknown)], "Timetable.ScheduleTimesNotIncreasing", S1 },
    };

    [Theory]
    [MemberData(nameof(SeveralFailures))]
    public void CreateDraft_WithSeveralFailures_ReturnsTheFirstInR45Order(
        string description, ScheduleServiceText[] services, string code, Guid serviceId)
    {
        _ = description;
        var s2NotEffective = new ScheduleServiceFacts(S2, "S2", new DateOnly(2026, 11, 1), null, 3);
        var facts = new[] { Facts(S1), s2NotEffective, Facts(S3Short) };

        var result = ScheduleVersion.CreateDraft(VersionId, 1, Input(Start, services), facts, NowUtc);

        AssertFailure(result, code, serviceId, null);
    }

    [Fact]
    public void CreateDraft_WithNonUtcTime_Throws()
    {
        var nonUtc = new DateTimeOffset(2026, 10, 1, 9, 30, 0, TimeSpan.FromHours(6.5));

        Assert.Throws<ArgumentException>(() =>
            ScheduleVersion.CreateDraft(VersionId, 1, Input(Start, [ValidS1()]), AllFacts(), nonUtc));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateDraft_WithNumberBelowOne_Throws(int number)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ScheduleVersion.CreateDraft(VersionId, number, Input(Start, [ValidS1()]), AllFacts(), NowUtc));
    }

    // ----- Publish (R30, R31, R50, R17) -----

    [Fact]
    public void Publish_Draft_SetsPublishedAndInstant()
    {
        var version = Draft();

        var result = version.Publish(Today, [Facts(S1)], LaterUtc);

        Assert.True(result.IsSuccess);
        Assert.Equal(ScheduleVersionStatus.Published, version.Status);
        Assert.Equal(LaterUtc, version.PublishedAtUtc);
        Assert.Null(version.DiscardedAtUtc);
        Assert.Null(version.CancelledAtUtc);
        Assert.Equal(NowUtc, version.CreatedAtUtc);
        Assert.Single(version.Services);
    }

    [Theory]
    [InlineData(ScheduleVersionStatus.Published)]
    [InlineData(ScheduleVersionStatus.Discarded)]
    [InlineData(ScheduleVersionStatus.Cancelled)]
    public void Publish_WhenNotDraft_ReturnsNotDraftAndChangesNothing(ScheduleVersionStatus status)
    {
        var version = InStatus(status);
        var before = Snapshot(version);

        var result = version.Publish(Today, [Facts(S1)], LaterUtc.AddHours(1));

        AssertFailure(result, TimetableErrors.ScheduleVersionNotDraft);
        Assert.Equal(before, Snapshot(version));
    }

    [Fact]
    public void Publish_WithStartBeforeToday_ReturnsEffectiveFromInPast()
    {
        var version = Draft();

        var result = version.Publish(Start.AddDays(1), [Facts(S1)], LaterUtc);

        AssertFailure(result, TimetableErrors.ScheduleVersionEffectiveFromInPast);
        Assert.Equal(ScheduleVersionStatus.Draft, version.Status);
        Assert.Null(version.PublishedAtUtc);
    }

    [Fact]
    public void Publish_OnTheStartDate_Succeeds()
    {
        var version = Draft();

        Assert.True(version.Publish(Start, [Facts(S1)], LaterUtc).IsSuccess);
        Assert.Equal(ScheduleVersionStatus.Published, version.Status);
    }

    [Fact]
    public void Publish_EmptyVersionWhoseStartHasBecomeToday_ReturnsEmptyNotInFutureAndChangesNothing()
    {
        // Created on 2026-10-01 for 2026-10-02; published on 2026-10-02 (SV55 at publication).
        var tomorrow = Today.AddDays(1);
        var version = ScheduleVersion.CreateDraft(VersionId, 1, Input(tomorrow, []), [], NowUtc).Value;

        var result = version.Publish(tomorrow, [], LaterUtc);

        AssertFailure(result, TimetableErrors.EmptyScheduleVersionNotInFuture);
        Assert.Equal(ScheduleVersionStatus.Draft, version.Status);
        Assert.Null(version.PublishedAtUtc);
    }

    [Fact]
    public void Publish_EmptyVersionStartingTomorrow_Succeeds()
    {
        var version = ScheduleVersion.CreateDraft(VersionId, 1, Input(Today.AddDays(1), []), [], NowUtc).Value;

        Assert.True(version.Publish(Today, [], LaterUtc).IsSuccess);
        Assert.Equal(ScheduleVersionStatus.Published, version.Status);
    }

    [Fact]
    public void Publish_WithListedServiceNoLongerEffective_ReturnsServiceNotEffective()
    {
        // S1 and S3Short are listed; both withdrawn before the start date since creation. The lowest
        // service id is reported.
        var version = ScheduleVersion.CreateDraft(
            VersionId, 1, Input(Start, [Valid(S3Short), ValidS1()]), [Facts(S1), Facts(S3Short)], NowUtc).Value;
        var withdrawn = new[]
        {
            new ScheduleServiceFacts(S3Short, "S3", new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 4), 3),
            new ScheduleServiceFacts(S1, "S1", new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 4), 3)
        };

        var result = version.Publish(Today, withdrawn, LaterUtc);

        AssertFailure(result, "Timetable.ScheduleServiceNotEffective", S1, null);
        Assert.Equal(ScheduleVersionStatus.Draft, version.Status);
        Assert.Null(version.PublishedAtUtc);
    }

    [Fact]
    public void Publish_ChecksNotDraftThenInPastThenEmptyThenNotEffective()
    {
        var published = InStatus(ScheduleVersionStatus.Published);
        Assert.Equal(TimetableErrors.ScheduleVersionNotDraft, published.Publish(Start.AddDays(9), [], LaterUtc).Error);

        var draft = Draft();
        var notEffective = new ScheduleServiceFacts(S1, "S1", new DateOnly(2026, 12, 1), null, 3);
        Assert.Equal(TimetableErrors.ScheduleVersionEffectiveFromInPast, draft.Publish(Start.AddDays(1), [], LaterUtc).Error);
        Assert.Equal(TimetableErrors.EmptyScheduleVersionNotInFuture, draft.Publish(Start, [], LaterUtc).Error);
        Assert.Equal("Timetable.ScheduleServiceNotEffective", draft.Publish(Start, [notEffective], LaterUtc).Error.Code);
    }

    [Fact]
    public void Publish_WithNonUtcTime_Throws()
    {
        var nonUtc = new DateTimeOffset(2026, 10, 1, 9, 30, 0, TimeSpan.FromHours(6.5));

        Assert.Throws<ArgumentException>(() => Draft().Publish(Today, [Facts(S1)], nonUtc));
    }

    // ----- Discard (R28) -----

    [Fact]
    public void Discard_Draft_SetsDiscarded()
    {
        var version = Draft();

        var result = version.Discard(LaterUtc);

        Assert.True(result.IsSuccess);
        Assert.Equal(ScheduleVersionStatus.Discarded, version.Status);
        Assert.Equal(LaterUtc, version.DiscardedAtUtc);
        Assert.Null(version.PublishedAtUtc);
        Assert.Null(version.CancelledAtUtc);
        Assert.Single(version.Services);
    }

    [Theory]
    [InlineData(ScheduleVersionStatus.Published)]
    [InlineData(ScheduleVersionStatus.Discarded)]
    [InlineData(ScheduleVersionStatus.Cancelled)]
    public void Discard_WhenNotDraft_ReturnsNotDraft(ScheduleVersionStatus status)
    {
        var version = InStatus(status);
        var before = Snapshot(version);

        AssertFailure(version.Discard(LaterUtc.AddHours(1)), TimetableErrors.ScheduleVersionNotDraft);
        Assert.Equal(before, Snapshot(version));
    }

    // ----- Cancel (R34) -----

    [Fact]
    public void Cancel_PublishedBeforeItsStart_SetsCancelledAndKeepsPublishedAt()
    {
        var version = InStatus(ScheduleVersionStatus.Published);
        var cancelledAt = LaterUtc.AddHours(1);

        var result = version.Cancel(Start.AddDays(-1), cancelledAt);

        Assert.True(result.IsSuccess);
        Assert.Equal(ScheduleVersionStatus.Cancelled, version.Status);
        Assert.Equal(LaterUtc, version.PublishedAtUtc);
        Assert.Equal(cancelledAt, version.CancelledAtUtc);
        Assert.Null(version.DiscardedAtUtc);
    }

    [Theory]
    [InlineData("2026-10-05")] // on its start date
    [InlineData("2026-10-06")] // after it
    public void Cancel_OnOrAfterItsStart_ReturnsAlreadyEffective(string today)
    {
        var version = InStatus(ScheduleVersionStatus.Published);
        var before = Snapshot(version);

        AssertFailure(version.Cancel(DateOnly.ParseExact(today, "yyyy-MM-dd"), LaterUtc.AddHours(1)), TimetableErrors.ScheduleVersionAlreadyEffective);
        Assert.Equal(before, Snapshot(version));
    }

    [Theory]
    [InlineData(ScheduleVersionStatus.Draft)]
    [InlineData(ScheduleVersionStatus.Discarded)]
    [InlineData(ScheduleVersionStatus.Cancelled)]
    public void Cancel_WhenNotPublished_ReturnsNotPublished(ScheduleVersionStatus status)
    {
        var version = InStatus(status);
        var before = Snapshot(version);

        AssertFailure(version.Cancel(Today, LaterUtc.AddHours(1)), TimetableErrors.ScheduleVersionNotPublished);
        Assert.Equal(before, Snapshot(version));
    }

    // ----- Structure (R27, R32) -----

    [Fact]
    public void ScheduleVersion_ExposesNoMutatorOtherThanItsTransitions()
    {
        const BindingFlags declaredPublic =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var methods = typeof(ScheduleVersion).GetMethods(declaredPublic)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.Equal(["Cancel", "CreateDraft", "Discard", "Publish"], methods);
        Assert.True(typeof(ScheduleVersion).GetMethod(nameof(ScheduleVersion.CreateDraft))!.IsStatic);

        foreach (var type in new[] { typeof(ScheduleVersion), typeof(ScheduleVersionService), typeof(ScheduleStopTime) })
        {
            Assert.All(
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => Assert.True(
                    property.SetMethod is null || !property.SetMethod.IsPublic,
                    $"{type.Name}.{property.Name} has a public setter"));
        }

        Assert.DoesNotContain(typeof(ScheduleVersionService).GetMethods(declaredPublic), method => !method.IsSpecialName);
        Assert.DoesNotContain(typeof(ScheduleStopTime).GetMethods(declaredPublic), method => !method.IsSpecialName);
        Assert.Equal(
            typeof(IReadOnlyList<ScheduleVersionService>),
            typeof(ScheduleVersion).GetProperty(nameof(ScheduleVersion.Services))!.PropertyType);
        Assert.Equal(
            typeof(IReadOnlyList<ScheduleStopTime>),
            typeof(ScheduleVersionService).GetProperty(nameof(ScheduleVersionService.StopTimes))!.PropertyType);
    }

    // ----- Helpers -----

    /// <summary>A second three-stop service, effective from 2026-10-05.</summary>
    private static readonly Guid S3Short = Guid.Parse("5e000000-0000-0000-0000-000000000004");

    private static ScheduleServiceFacts Facts(Guid serviceId) =>
        new(serviceId, "S" + serviceId.ToString("D")[^1], Start, null, 3);

    private static ScheduleServiceFacts[] AllFacts() =>
    [
        Facts(S1),
        Facts(S2),
        new(S3, "S3", Start, null, 6),
        Facts(S3Short)
    ];

    private static ScheduleServiceText ValidS1() => Valid(S1);

    private static ScheduleServiceText Valid(Guid serviceId) =>
        Times(serviceId, (null, "06:00"), ("06:20", "06:22"), ("06:40", null));

    private static ScheduleServiceText BadDwell(Guid serviceId) =>
        Times(serviceId, (null, "06:00"), ("06:22", "06:20"), ("06:40", null));

    private static ScheduleServiceText BeyondStops(Guid serviceId) =>
        new(serviceId, [new(1, null, "06:00"), new(2, "06:20", "06:22"), new(3, "06:40", null), new(9, "07:00", null)]);

    private static ScheduleServiceText Times(Guid serviceId, params (string? Arrival, string? Departure)[] stops) =>
        new(serviceId, [.. stops.Select((stop, index) => new ScheduleStopTimeText(index + 1, stop.Arrival, stop.Departure))]);

    private static ScheduleVersionInput Input(DateOnly effectiveFrom, IReadOnlyList<ScheduleServiceText> services) =>
        ScheduleVersionInput.Parse("October", "Myanmar", effectiveFrom, Today, services).Value;

    private static Result<ScheduleVersion> Create(DateOnly effectiveFrom, IReadOnlyList<ScheduleServiceText> services) =>
        ScheduleVersion.CreateDraft(VersionId, 1, Input(effectiveFrom, services), AllFacts(), NowUtc);

    private static ScheduleVersion Draft() => Create(Start, [ValidS1()]).Value;

    private static ScheduleVersion InStatus(ScheduleVersionStatus status)
    {
        var version = Draft();
        switch (status)
        {
            case ScheduleVersionStatus.Draft:
                break;
            case ScheduleVersionStatus.Published:
                Assert.True(version.Publish(Today, [Facts(S1)], LaterUtc).IsSuccess);
                break;
            case ScheduleVersionStatus.Discarded:
                Assert.True(version.Discard(LaterUtc).IsSuccess);
                break;
            case ScheduleVersionStatus.Cancelled:
                Assert.True(version.Publish(Today, [Facts(S1)], LaterUtc).IsSuccess);
                Assert.True(version.Cancel(Today, LaterUtc).IsSuccess);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        Assert.Equal(status, version.Status);
        return version;
    }

    private static (ScheduleVersionStatus, DateTimeOffset?, DateTimeOffset?, DateTimeOffset?) Snapshot(ScheduleVersion version) =>
        (version.Status, version.PublishedAtUtc, version.DiscardedAtUtc, version.CancelledAtUtc);

    private static void AssertFailure(Result result, Error expected)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(expected, result.Error);
    }

    private static void AssertFailure(Result result, string code, Guid serviceId, int? position)
    {
        Assert.True(result.IsFailure, "expected a failure");
        Assert.Equal(code, result.Error.Code);
        Assert.Equal(ErrorType.BusinessRule, result.Error.Type);
        Assert.Contains(serviceId.ToString(), result.Error.Message, StringComparison.Ordinal);
        if (position is { } stop)
        {
            Assert.Matches($@"\bstop {stop}\b", result.Error.Message);
        }
    }
}

using YCR.Domain.Timetable;

namespace YCR.Domain.Tests.Timetable;

/// <summary>
/// <see cref="ScheduleVersionInput.Parse"/>: F-005 plan P4 checks 2-5 (spec R7, R16, R31, R50).
/// Today is 2026-10-01.
/// </summary>
public sealed class ScheduleVersionInputTests
{
    private static readonly DateOnly Today = new(2026, 10, 1);
    private static readonly Guid S1 = Guid.Parse("5e000000-0000-0000-0000-000000000001");
    private static readonly Guid S2 = Guid.Parse("5e000000-0000-0000-0000-000000000002");

    public static TheoryData<string?, string?> InvalidNames => new()
    {
        { "", "Myanmar" },
        { "   ", "Myanmar" },
        { "October", "" },
        { "October", " " },
        { null, "Myanmar" },
        { "October", null },
        { null, null },
        { new string('a', 101), "Myanmar" },
        { "October", new string('က', 101) },
    };

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public void Parse_WithBlankMissingOrLongName_ReturnsInvalidScheduleVersionName(string? en, string? my)
    {
        var result = ScheduleVersionInput.Parse(en, my, new DateOnly(2026, 10, 5), Today, [Valid(S1)]);

        Assert.True(result.IsFailure);
        Assert.Equal(TimetableErrors.InvalidScheduleVersionName, result.Error);
    }

    [Fact]
    public void Parse_WithValidInput_TrimsNamesAndParsesTimesInRequestOrder()
    {
        var result = ScheduleVersionInput.Parse(
            " October 2026 ", " အောက်တိုဘာ ",
            new DateOnly(2026, 10, 5), Today, [Valid(S1), Valid(S2)]);

        Assert.True(result.IsSuccess);
        var input = result.Value;
        Assert.Equal("October 2026", input.Name.En);
        Assert.Equal("အောက်တိုဘာ", input.Name.My);
        Assert.Equal(new DateOnly(2026, 10, 5), input.EffectiveFrom);
        Assert.Equal([S1, S2], input.Services.Select(service => service.ServiceId));
        Assert.Equal(
            [(1, (short?)null, (short?)360), (2, (short?)380, (short?)382), (3, (short?)400, (short?)null)],
            input.Services[0].StopTimes.Select(stop => (stop.Position, stop.Arrival?.Minutes, stop.Departure?.Minutes)));
    }

    [Fact]
    public void Parse_WithEffectiveFromBeforeToday_ReturnsEffectiveFromInPast()
    {
        var result = ScheduleVersionInput.Parse("October", "M", new DateOnly(2026, 9, 30), Today, [Valid(S1)]);

        Assert.Equal(TimetableErrors.ScheduleVersionEffectiveFromInPast, result.Error);
    }

    [Fact]
    public void Parse_WithEffectiveFromToday_Succeeds()
    {
        // With one service: an empty version starting today is SV55 (Amendment 2).
        var result = ScheduleVersionInput.Parse("October", "M", Today, Today, [Valid(S1)]);

        Assert.True(result.IsSuccess);
        Assert.Equal(Today, result.Value.EffectiveFrom);
    }

    [Fact]
    public void Parse_ReportsTheFirstBadTimeInRequestOrder()
    {
        // The first service is fine; the second has a bad arrival at stop 2 and a bad departure at
        // stop 1. Stop-time order comes first, so the departure at stop 1 is the first bad time, and
        // the error is the same code either way; a bad name still wins over any time.
        ScheduleServiceText[] services =
        [
            Valid(S1),
            new(S2, [new(1, null, "25:00"), new(2, "6:20", "06:22"), new(3, "06:40", null)])
        ];

        var result = ScheduleVersionInput.Parse("October", "M", new DateOnly(2026, 10, 5), Today, services);
        Assert.Equal(TimetableErrors.InvalidTimetableTime, result.Error);

        var nameFirst = ScheduleVersionInput.Parse("", "M", new DateOnly(2026, 10, 5), Today, services);
        Assert.Equal(TimetableErrors.InvalidScheduleVersionName, nameFirst.Error);

        // A bad time wins over a past start date (R45: time format before EffectiveFrom).
        var timeBeforeDate = ScheduleVersionInput.Parse("October", "M", new DateOnly(2026, 9, 1), Today, services);
        Assert.Equal(TimetableErrors.InvalidTimetableTime, timeBeforeDate.Error);
    }

    [Fact]
    public void Parse_WithNoServicesStartingToday_ReturnsEmptyNotInFuture()
    {
        var result = ScheduleVersionInput.Parse("Suspension", "M", Today, Today, []);

        Assert.Equal(TimetableErrors.EmptyScheduleVersionNotInFuture, result.Error);
    }

    [Fact]
    public void Parse_WithNoServicesStartingTomorrow_Succeeds()
    {
        var result = ScheduleVersionInput.Parse("Suspension", "M", Today.AddDays(1), Today, []);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Services);
    }

    [Fact]
    public void Parse_WithNoServicesStartingBeforeToday_ReturnsEffectiveFromInPastFirst()
    {
        var result = ScheduleVersionInput.Parse("Suspension", "M", Today.AddDays(-1), Today, []);

        Assert.Equal(TimetableErrors.ScheduleVersionEffectiveFromInPast, result.Error);
    }

    private static ScheduleServiceText Valid(Guid serviceId) =>
        new(serviceId, [new(1, null, "06:00"), new(2, "06:20", "06:22"), new(3, "06:40", null)]);
}

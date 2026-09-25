using YCR.Domain.Timetable;

namespace YCR.Domain.Tests.Timetable;

public sealed class EffectivePeriodTests
{
    private static readonly DateOnly Today = new(2026, 10, 1);

    [Fact]
    public void ForNewService_WithToBeforeFrom_ReturnsInvalidEffectivePeriod()
    {
        var result = EffectivePeriod.ForNewService(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 4), Today);

        Assert.True(result.IsFailure);
        Assert.Equal(TimetableErrors.InvalidEffectivePeriod, result.Error);
    }

    [Fact]
    public void ForNewService_WithToEqualToFrom_IsAOneDayPeriod()
    {
        var day = new DateOnly(2026, 10, 5);

        var result = EffectivePeriod.ForNewService(day, day, Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(day, result.Value.From);
        Assert.Equal(day, result.Value.To);
    }

    [Fact]
    public void ForNewService_WithFromBeforeToday_Succeeds()
    {
        var result = EffectivePeriod.ForNewService(new DateOnly(2026, 9, 1), null, Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 9, 1), result.Value.From);
        Assert.Null(result.Value.To);
    }

    [Fact]
    public void ForNewService_WithToBeforeToday_ReturnsEffectiveToInPast()
    {
        var result = EffectivePeriod.ForNewService(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), Today);

        Assert.True(result.IsFailure);
        Assert.Equal(TimetableErrors.ServiceEffectiveToInPast, result.Error);
    }

    [Fact]
    public void ForNewService_WithToEqualToToday_Succeeds()
    {
        var result = EffectivePeriod.ForNewService(new DateOnly(2026, 9, 1), Today, Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(Today, result.Value.To);
    }

    [Fact]
    public void ForNewService_WithBothFaults_ReturnsInvalidEffectivePeriodFirst()
    {
        // To is before From (R19) and before today (R39): R37 puts the 400 first.
        var result = EffectivePeriod.ForNewService(new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 10), Today);

        Assert.True(result.IsFailure);
        Assert.Equal(TimetableErrors.InvalidEffectivePeriod, result.Error);
    }

    [Theory]
    // S24: open-ended from 10-05 against a later start.
    [InlineData("2026-10-05", null, "2027-01-01", null, true)]
    // S25: inclusive edge, 10-05..12-31 against a start on 12-31.
    [InlineData("2026-10-05", "2026-12-31", "2026-12-31", null, true)]
    // S25, S28: adjacent, 10-05..12-31 and from 01-01.
    [InlineData("2026-10-05", "2026-12-31", "2027-01-01", null, false)]
    [InlineData("2027-01-01", null, "2026-10-05", "2026-12-31", false)]
    // Both open-ended.
    [InlineData("2027-01-01", null, "2026-10-05", null, true)]
    // S40: a past start, open-ended, against a future open-ended period.
    [InlineData("2026-09-01", null, "2026-10-05", null, true)]
    // S40: a past period that ends the day before the other starts is adjacent.
    [InlineData("2026-09-01", "2026-10-04", "2026-10-05", null, false)]
    // S26: the new service from the withdrawal date against the withdrawn old one, open-ended
    // before and ending 12-31 after the withdrawal.
    [InlineData("2027-01-01", "2027-06-30", "2026-10-05", "2026-12-31", false)]
    public void Overlaps_WithPeriods_FollowsTheInclusiveRule(
        string from, string? to, string otherFrom, string? otherTo, bool expected)
    {
        var period = EffectivePeriod.ForNewService(Date(from), DateOrNull(to), new DateOnly(2026, 1, 1)).Value;

        Assert.Equal(expected, period.Overlaps(Date(otherFrom), DateOrNull(otherTo)));
    }

    [Fact]
    public void Overlaps_WithAPeriodThatNeverRuns_IsFalse()
    {
        // S36, R42: W ran from 11-01 and was withdrawn from 10-15, so its period is 11-01..10-14.
        var period = EffectivePeriod.ForNewService(new DateOnly(2026, 10, 1), null, Today).Value;

        Assert.False(period.Overlaps(new DateOnly(2026, 11, 1), new DateOnly(2026, 10, 14)));
    }

    private static DateOnly Date(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd");

    private static DateOnly? DateOrNull(string? value) => value is null ? null : Date(value);
}

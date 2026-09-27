using YCR.Domain.Timetable;

namespace YCR.Domain.Tests.Timetable;

/// <summary><see cref="ServiceRunningDay"/> (F-005 plan P12; spec R18, R48, SV30, SV54).</summary>
public sealed class ServiceRunningDayTests
{
    private static readonly OperatingDays Weekdays = OperatingDays.Create(
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]);

    [Theory]
    [InlineData("2026-10-10", "2026-10-05", null, false)] // SV30: a Saturday
    [InlineData("2026-10-12", "2026-10-05", null, true)] // SV30: a Monday
    [InlineData("2026-11-02", "2026-10-05", "2026-10-31", false)] // SV30: a Monday after EffectiveTo
    [InlineData("2026-10-31", "2026-10-05", "2026-10-30", false)] // a Saturday, and after EffectiveTo
    [InlineData("2026-10-30", "2026-10-05", "2026-10-30", true)] // a Friday on EffectiveTo
    [InlineData("2026-10-05", "2026-10-05", null, true)] // a Monday on EffectiveFrom
    [InlineData("2026-10-02", "2026-10-05", null, false)] // a Friday before EffectiveFrom
    [InlineData("2027-01-04", "2026-10-05", "2026-12-31", false)] // SV54: withdrawn from 2027-01-01
    [InlineData("2026-12-28", "2026-10-05", "2026-12-31", true)] // SV54: before the withdrawal
    [InlineData("2026-10-05", "2026-10-05", "2026-10-04", false)] // never runs
    public void RunsOn_ChecksThePeriodAndTheWeekday(string date, string from, string? to, bool runs)
    {
        var result = ServiceRunningDay.RunsOn(Date(date), Date(from), to is null ? null : Date(to), Weekdays);

        Assert.Equal(runs, result);
    }

    private static DateOnly Date(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd");
}

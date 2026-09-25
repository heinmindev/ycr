using YCR.Domain.Timetable;

namespace YCR.Domain.Tests.Timetable;

public sealed class OperatingDaysTests
{
    [Fact]
    public void Create_WithSundayAndMonday_HasExactlyThoseDaysMondayFirst()
    {
        var days = OperatingDays.Create([DayOfWeek.Sunday, DayOfWeek.Monday]);

        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Sunday], days.Days);
        Assert.True(days.RunsOnMonday);
        Assert.True(days.RunsOnSunday);
        Assert.False(days.RunsOnTuesday);
        Assert.False(days.RunsOnWednesday);
        Assert.False(days.RunsOnThursday);
        Assert.False(days.RunsOnFriday);
        Assert.False(days.RunsOnSaturday);
    }

    [Fact]
    public void Create_WithNoDays_Throws()
    {
        Assert.Throws<ArgumentException>(() => OperatingDays.Create([]));
    }

    [Fact]
    public void Create_WithRepeatedDay_Throws()
    {
        Assert.Throws<ArgumentException>(() => OperatingDays.Create([DayOfWeek.Monday, DayOfWeek.Monday]));
    }
}

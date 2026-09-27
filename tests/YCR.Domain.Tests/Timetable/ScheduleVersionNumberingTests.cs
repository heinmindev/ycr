using YCR.Domain.Timetable;

namespace YCR.Domain.Tests.Timetable;

/// <summary><see cref="ScheduleVersionNumbering"/> (F-005 plan P5; spec R7, SV4).</summary>
public sealed class ScheduleVersionNumberingTests
{
    [Theory]
    [InlineData(null, 1)]
    [InlineData(1, 2)]
    [InlineData(7, 8)]
    public void Next_StartsAtOneThenHighestPlusOne(int? highest, int expected)
    {
        Assert.Equal(expected, ScheduleVersionNumbering.Next(highest));
    }
}

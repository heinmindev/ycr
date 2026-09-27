using System.Reflection;
using YCR.Domain.Timetable;

namespace YCR.Domain.Tests.Timetable;

/// <summary>
/// <see cref="TimetableTime"/> (ADR-0027; F-005 plan P2; spec R11, R15, R16, SV11).
/// </summary>
public sealed class TimetableTimeTests
{
    [Theory]
    [InlineData("00:00", 0)]
    [InlineData("06:00", 360)]
    [InlineData("06:22", 382)]
    [InlineData("12:59", 779)]
    [InlineData("23:59", 1439)]
    public void Parse_WithValidTime_ReturnsMinutes(string text, short minutes)
    {
        var result = TimetableTime.Parse(text);

        Assert.True(result.IsSuccess);
        Assert.Equal(minutes, result.Value.Minutes);
    }

    [Theory]
    [InlineData("24:00")]
    [InlineData("24:15")]
    [InlineData("6:00")]
    [InlineData("06:60")]
    [InlineData("06:00:30")]
    [InlineData("0600")]
    [InlineData("")]
    [InlineData(" 06:00")]
    [InlineData("06:00 ")]
    [InlineData("06:00\n")]
    [InlineData(null)]
    [InlineData("၀၆:၀၀")] // Myanmar digits ၀၆:၀၀
    [InlineData("٠٦:٠٠")] // Arabic-Indic digits ٠٦:٠٠
    [InlineData("０６:００")] // full-width digits
    [InlineData("0၆:00")] // a Myanmar digit where \d would match it
    [InlineData("06:0၀")]
    [InlineData("06:0٠")] // an Arabic-Indic digit where \d would match it
    public void Parse_WithInvalidText_ReturnsInvalidTimetableTime(string? text)
    {
        var result = TimetableTime.Parse(text);

        Assert.True(result.IsFailure);
        Assert.Equal(TimetableErrors.InvalidTimetableTime, result.Error);
    }

    [Theory]
    [InlineData("00:00")]
    [InlineData("06:05")]
    [InlineData("23:59")]
    public void ToString_WritesTwoDigitHoursAndMinutes(string text)
    {
        Assert.Equal(text, TimetableTime.Parse(text).Value.ToString());
    }

    [Theory]
    [InlineData((short)-1)]
    [InlineData((short)1440)]
    public void FromMinutes_OutsideTheDay_Throws(short minutes)
    {
        var fromMinutes = typeof(TimetableTime).GetMethod("FromMinutes", BindingFlags.NonPublic | BindingFlags.Static)!;

        var thrown = Assert.Throws<TargetInvocationException>(() => fromMinutes.Invoke(null, [minutes]));

        Assert.IsType<ArgumentOutOfRangeException>(thrown.InnerException);
    }

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)1439)]
    public void FromMinutes_InsideTheDay_KeepsTheMinutes(short minutes)
    {
        var fromMinutes = typeof(TimetableTime).GetMethod("FromMinutes", BindingFlags.NonPublic | BindingFlags.Static)!;

        var time = (TimetableTime)fromMinutes.Invoke(null, [minutes])!;

        Assert.Equal(minutes, time.Minutes);
    }
}

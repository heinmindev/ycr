using YCR.Domain.Common;
using YCR.Domain.Timetable;

namespace YCR.Domain.Tests.Timetable;

public sealed class ServiceCodeTests
{
    [Theory]
    [InlineData("S1", "S1")]
    [InlineData("S101", "S101")]
    [InlineData("ABCDEFGHIJ", "ABCDEFGHIJ")]
    [InlineData(" LOOP1 ", "LOOP1")]
    public void ServiceCode_Create_WithValidCode_ReturnsCode(string code, string expected)
    {
        var result = ServiceCode.Create(code);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.Value);
    }

    [Theory]
    [InlineData("s1")]
    [InlineData("A")]
    [InlineData("ABCDEFGHIJK")]
    [InlineData("S-1")]
    [InlineData("S 1")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ServiceCode_Create_WithInvalidCode_ReturnsInvalidServiceCode(string? code)
    {
        var result = ServiceCode.Create(code);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(TimetableErrors.InvalidServiceCode, result.Error);
        Assert.Equal("Timetable.InvalidServiceCode", result.Error.Code);
    }
}

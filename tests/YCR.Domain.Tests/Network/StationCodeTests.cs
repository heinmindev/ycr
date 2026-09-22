using YCR.Domain.Common;
using YCR.Domain.Network;

namespace YCR.Domain.Tests.Network;

public sealed class StationCodeTests
{
    [Fact]
    public void Create_WithValidCode_ReturnsCode()
    {
        var result = StationCode.Create("  YGN01  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("YGN01", result.Value.Value);
    }

    [Fact]
    public void Create_WithTooShortCode_ReturnsValidationError()
    {
        AssertValidationFailure("A");
    }

    [Fact]
    public void Create_WithTooLongCode_ReturnsValidationError()
    {
        AssertValidationFailure("ABCDEFGHIJK");
    }

    [Fact]
    public void Create_WithLowerCaseCode_ReturnsValidationError()
    {
        AssertValidationFailure("ygn");
    }

    [Theory]
    [InlineData("YG-N")]
    [InlineData("YG N")]
    [InlineData("YG.N")]
    public void Create_WithPunctuationCode_ReturnsValidationError(string code)
    {
        AssertValidationFailure(code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankCode_ReturnsValidationError(string code)
    {
        AssertValidationFailure(code);
    }

    private static void AssertValidationFailure(string code)
    {
        var result = StationCode.Create(code);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Network.InvalidStationCode", result.Error.Code);
    }
}

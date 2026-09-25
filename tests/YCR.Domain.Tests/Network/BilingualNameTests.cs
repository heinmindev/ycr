using YCR.Domain.Common;
using YCR.Domain.Network;

namespace YCR.Domain.Tests.Network;

public sealed class BilingualNameTests
{
    [Fact]
    public void Create_WithValidNames_ReturnsTrimmedNames()
    {
        var result = BilingualName.Create("  Yangon  ", "  Yangon Myanmar  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Yangon", result.Value.En);
        Assert.Equal("Yangon Myanmar", result.Value.My);
    }

    [Fact]
    public void Create_WithMissingEnglishName_ReturnsValidationError()
    {
        AssertValidationFailure(null, "Yangon Myanmar");
    }

    [Fact]
    public void Create_WithMissingMyanmarName_ReturnsValidationError()
    {
        AssertValidationFailure("Yangon", null);
    }

    [Theory]
    [InlineData("", "Yangon Myanmar")]
    [InlineData("Yangon", "   ")]
    public void Create_WithWhitespaceOnlyName_ReturnsValidationError(string en, string my)
    {
        AssertValidationFailure(en, my);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_WithOverlongName_ReturnsValidationError(bool englishIsOverlong)
    {
        var overlongName = new string('A', 101);
        var en = englishIsOverlong ? overlongName : "Yangon";
        var my = englishIsOverlong ? "Yangon Myanmar" : overlongName;

        AssertValidationFailure(en, my);
    }

    [Theory]
    [InlineData("", "Route Myanmar")]
    [InlineData("Route", "")]
    [InlineData("   ", "Route Myanmar")]
    [InlineData("Route", "   ")]
    [InlineData("101", "Route Myanmar")]
    [InlineData("Route", "101")]
    public void BilingualName_Create_WithRouteError_WhenInvalid_ReturnsSuppliedError(string en, string my)
    {
        // "101" stands for a name of 101 characters after trimming; InlineData cannot build one.
        var overlong = "  " + new string('A', 101) + "  ";

        var result = BilingualName.Create(
            en == "101" ? overlong : en,
            my == "101" ? overlong : my,
            NetworkErrors.InvalidRouteName);

        Assert.True(result.IsFailure);
        Assert.Equal(NetworkErrors.InvalidRouteName, result.Error);
    }

    [Fact]
    public void BilingualName_Create_WithRouteError_WhenValid_ReturnsTrimmedNames()
    {
        var result = BilingualName.Create("  Loop  ", "  Loop Myanmar  ", NetworkErrors.InvalidRouteName);

        Assert.True(result.IsSuccess);
        Assert.Equal("Loop", result.Value.En);
        Assert.Equal("Loop Myanmar", result.Value.My);
    }

    [Fact]
    public void BilingualName_Create_WithoutErrorArgument_StillReturnsInvalidStationName()
    {
        var result = BilingualName.Create("   ", "Yangon Myanmar");

        Assert.True(result.IsFailure);
        Assert.Equal(NetworkErrors.InvalidStationName, result.Error);
    }

    private static void AssertValidationFailure(string? en, string? my)
    {
        var result = BilingualName.Create(en, my);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Network.InvalidStationName", result.Error.Code);
    }
}

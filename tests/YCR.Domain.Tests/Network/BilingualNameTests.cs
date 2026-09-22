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

    private static void AssertValidationFailure(string? en, string? my)
    {
        var result = BilingualName.Create(en, my);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Network.InvalidStationName", result.Error.Code);
    }
}

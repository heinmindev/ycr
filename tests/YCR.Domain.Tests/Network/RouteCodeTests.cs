using YCR.Domain.Common;
using YCR.Domain.Network;

namespace YCR.Domain.Tests.Network;

public sealed class RouteCodeTests
{
    [Theory]
    [InlineData("R1", "R1")]
    [InlineData("AB", "AB")]
    [InlineData("ABCDEFGHIJ", "ABCDEFGHIJ")]
    [InlineData(" LOOP1 ", "LOOP1")]
    public void RouteCode_Create_WithValidCode_ReturnsCode(string code, string expected)
    {
        var result = RouteCode.Create(code);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.Value);
    }

    [Theory]
    [InlineData("R")]
    [InlineData("ABCDEFGHIJK")]
    [InlineData("loop")]
    [InlineData("R-1")]
    [InlineData("R 1")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RouteCode_Create_WithInvalidCode_ReturnsInvalidRouteCode(string? code)
    {
        var result = RouteCode.Create(code);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Network.InvalidRouteCode", result.Error.Code);
    }
}

using YCR.Domain.Common;

namespace YCR.Domain.Tests.Common;

public sealed class ErrorTests
{
    [Fact]
    public void FactoryMethods_AssignTheExpectedErrorType()
    {
        Assert.Equal(ErrorType.Validation, Error.Validation("x", "x").Type);
        Assert.Equal(ErrorType.Unauthorized, Error.Unauthorized("x", "x").Type);
        Assert.Equal(ErrorType.Forbidden, Error.Forbidden("x", "x").Type);
        Assert.Equal(ErrorType.NotFound, Error.NotFound("x", "x").Type);
        Assert.Equal(ErrorType.Conflict, Error.Conflict("x", "x").Type);
        Assert.Equal(ErrorType.BusinessRule, Error.BusinessRule("x", "x").Type);
    }

    [Fact]
    public void Error_UsesValueEquality()
    {
        var first = Error.Validation("Network.Invalid", "The station is invalid.");
        var second = Error.Validation("Network.Invalid", "The station is invalid.");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Error_RequiresCodeAndMessage()
    {
        Assert.Throws<ArgumentException>(() => Error.Validation("", "message"));
        Assert.Throws<ArgumentException>(() => Error.Validation("code", ""));
    }
}

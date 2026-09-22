using YCR.Domain.Common;

namespace YCR.Domain.Tests.Common;

public sealed class ResultTests
{
    [Fact]
    public void Success_HasNoError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Failure_CarriesError()
    {
        var error = Error.NotFound("Network.StationNotFound", "Station was not found.");

        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void GenericSuccess_ExposesValue()
    {
        var result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void GenericSuccess_WithNullReferenceValue_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Result<string>.Success(null!));
    }

    [Fact]
    public void ReadValue_WhenResultIsFailure_ThrowsInvalidOperationException()
    {
        var result = Result<int>.Failure(Error.Validation("Network.Invalid", "The value is invalid."));

        Assert.True(result.IsFailure);
        Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Equal("Network.Invalid", result.Error.Code);
    }

    [Fact]
    public void Error_ImplicitlyConvertsToFailureResult()
    {
        Error error = Error.Conflict("Network.Duplicate", "The code already exists.");

        Result<int> result = error;

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }
}

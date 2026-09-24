using YCR.Domain.Identity;

namespace YCR.Domain.Tests.Identity;

/// <summary>R20 / S32a: 3–50 characters of lower-case <c>a-z</c>, <c>0-9</c> and <c>.</c>.</summary>
public sealed class UserNameTests
{
    [Theory]
    [InlineData("abc")]
    [InlineData("hein.min")]
    [InlineData("a1.b2")]
    [InlineData("...")]
    [InlineData("12345678901234567890123456789012345678901234567890")]
    public void UserName_Create_WithValidName_ReturnsUserName(string value)
    {
        var result = UserName.Create(value);

        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("123456789012345678901234567890123456789012345678901")]
    [InlineData("Hein")]
    [InlineData("hein min")]
    [InlineData(" hein")]
    [InlineData("hein-min")]
    [InlineData("hein@min")]
    [InlineData("hein_min")]
    [InlineData("heiné")]
    [InlineData("hein၁")]
    [InlineData("hein١")]
    public void UserName_Create_WithInvalidName_ReturnsValidationError(string? value)
    {
        var result = UserName.Create(value);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.InvalidUserName, result.Error);
    }
}

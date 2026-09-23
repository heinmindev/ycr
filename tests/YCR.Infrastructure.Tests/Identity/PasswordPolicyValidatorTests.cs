using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using YCR.Domain.Identity;

namespace YCR.Infrastructure.Tests.Identity;

/// <summary>
/// The password policy as registered (ADR-0023 item 2; spec R18, S16; plan P12): 12–128 Unicode
/// scalar values, no composition rule, not on the blocklist — and nothing else.
/// </summary>
public sealed class PasswordPolicyValidatorTests : IAsyncDisposable
{
    private static readonly StaffUser User =
        StaffUser.Create(Guid.NewGuid(), UserName.Create("policy.user").Value, DateTimeOffset.UnixEpoch);

    private readonly ServiceProvider provider = IdentityServiceProvider.Build();
    private readonly AsyncServiceScope scope;

    public PasswordPolicyValidatorTests()
    {
        scope = provider.CreateAsyncScope();
    }

    [Fact]
    public void Registration_HasOnlyThePolicyValidator()
    {
        var validator = Assert.Single(scope.ServiceProvider.GetServices<IPasswordValidator<StaffUser>>());

        Assert.Equal("PasswordPolicyValidator", validator.GetType().Name);
        Assert.DoesNotContain(
            scope.ServiceProvider.GetServices<IPasswordValidator<StaffUser>>(),
            registered => registered is PasswordValidator<StaffUser>);
    }

    [Fact]
    public async Task PasswordPolicy_ElevenScalarValues_Rejected() =>
        Assert.False(await IsAcceptedAsync("kyauk.tan.1"));

    [Fact]
    public async Task PasswordPolicy_TwelveAccepted() =>
        Assert.True(await IsAcceptedAsync("kyauk.tan.12"));

    [Fact]
    public async Task PasswordPolicy_128Accepted() =>
        Assert.True(await IsAcceptedAsync(new string('m', 120) + "yangon.1"));

    [Fact]
    public async Task PasswordPolicy_129Rejected() =>
        Assert.False(await IsAcceptedAsync(new string('m', 121) + "yangon.1"));

    [Theory]
    [InlineData("123456789012")]
    [InlineData("PasswordPassword")]
    [InlineData("QWERTYUIOPASDFGHJKL")]
    [InlineData("1Q2W3E4R5T6Y")]
    public async Task PasswordPolicy_BlocklistedRejectedCaseInsensitively(string password) =>
        Assert.False(await IsAcceptedAsync(password));

    /// <summary>
    /// Plan P12: eleven emoji are 22 UTF-16 units but 11 characters (rejected); twelve Myanmar
    /// letters (BMP) and twelve emoji are twelve characters (accepted).
    /// </summary>
    [Fact]
    public async Task PasswordPolicy_CountsScalarValuesNotUtf16Units()
    {
        var elevenEmoji = string.Concat(Enumerable.Repeat("\U0001F686", 11));
        var twelveEmoji = string.Concat(Enumerable.Repeat("\U0001F686", 12));
        var twelveMyanmar = "ရန်ကုန်မြို့";

        Assert.Equal(22, elevenEmoji.Length);
        Assert.False(await IsAcceptedAsync(elevenEmoji));
        Assert.True(await IsAcceptedAsync(twelveEmoji));
        Assert.True(await IsAcceptedAsync(twelveMyanmar));
    }

    /// <summary>D2: no composition rule — no digit, upper case or symbol is required.</summary>
    [Fact]
    public async Task PasswordPolicy_HasNoCompositionRule() =>
        Assert.True(await IsAcceptedAsync("circularlinetrainmorning"));

    private async Task<bool> IsAcceptedAsync(string password)
    {
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>();
        var validator = Assert.Single(scope.ServiceProvider.GetServices<IPasswordValidator<StaffUser>>());

        return (await validator.ValidateAsync(manager, User, password)).Succeeded;
    }

    public async ValueTask DisposeAsync()
    {
        await scope.DisposeAsync();
        await provider.DisposeAsync();
    }
}

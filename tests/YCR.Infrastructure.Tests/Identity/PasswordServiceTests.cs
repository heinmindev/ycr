using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using YCR.Application.Identity.Abstractions;
using YCR.Domain.Identity;

namespace YCR.Infrastructure.Tests.Identity;

/// <summary>
/// R23 / S2: every verification path — unknown user, disabled user, locked user, wrong or right
/// password — performs one full hash verification. Asserted by counting calls on the registered
/// hasher, not by timing (spec S2).
/// </summary>
public sealed class PasswordServiceTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 3, 0, 0, TimeSpan.Zero);
    private const string Password = "kyauk.tan.12";

    private readonly CountingHasher hasher = new();
    private readonly ServiceProvider provider;
    private readonly AsyncServiceScope scope;

    public PasswordServiceTests()
    {
        provider = IdentityServiceProvider.Build(services =>
            services.Replace(ServiceDescriptor.Scoped<IPasswordHasher<StaffUser>>(_ => hasher)));
        scope = provider.CreateAsyncScope();
    }

    private IPasswordService Passwords => scope.ServiceProvider.GetRequiredService<IPasswordService>();

    [Fact]
    public async Task Verify_UnknownUser_PerformsFullHashVerification()
    {
        var verified = await Passwords.VerifyAsync(null, Password, TestContext.Current.CancellationToken);

        Assert.False(verified);
        Assert.Equal(1, hasher.Verifications);
        // The dummy is a real Identity V3 hash (format marker 0x01), so PBKDF2 actually runs.
        Assert.Equal(0x01, Convert.FromBase64String(hasher.LastVerifiedHash!)[0]);
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("locked")]
    public async Task Verify_DisabledOrLockedUser_PerformsFullHashVerification(string state)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var user = await CreateUserWithPasswordAsync();
        if (state == "disabled")
        {
            user.Disable(Guid.NewGuid(), Now);
        }
        else
        {
            for (var attempt = 0; attempt < AccountLockoutPolicy.MaxFailedAttempts; attempt++)
            {
                user.RecordFailedSignIn(Now);
            }
        }

        var verified = await Passwords.VerifyAsync(user, Password, cancellationToken);

        // The password is right; the service reports that, and the handler still refuses the
        // disabled or locked account with the uniform 401.
        Assert.True(verified);
        Assert.Equal(1, hasher.Verifications);
        Assert.Equal(user.PasswordHash, hasher.LastVerifiedHash);
    }

    [Fact]
    public async Task Verify_WrongPassword_PerformsFullHashVerificationAndFails()
    {
        var user = await CreateUserWithPasswordAsync();

        Assert.False(await Passwords.VerifyAsync(user, "kyauk.tan.13", TestContext.Current.CancellationToken));
        Assert.Equal(1, hasher.Verifications);
    }

    [Fact]
    public async Task TrySetPassword_Accepted_AppliesAV3HashAndANewStamp()
    {
        var user = StaffUser.Create(Guid.NewGuid(), UserName.Create("hash.user").Value, Now);

        Assert.True(await Passwords.TrySetPasswordAsync(user, Password, TestContext.Current.CancellationToken));

        Assert.NotNull(user.PasswordHash);
        Assert.DoesNotContain(Password, user.PasswordHash, StringComparison.Ordinal);
        Assert.Equal(0x01, Convert.FromBase64String(user.PasswordHash)[0]);
        Assert.Matches("^[0-9A-F]{40}$", user.SecurityStamp);

        var firstStamp = user.SecurityStamp;
        Assert.True(await Passwords.TrySetPasswordAsync(user, "another.pass.12", TestContext.Current.CancellationToken));
        Assert.NotEqual(firstStamp, user.SecurityStamp);
    }

    [Fact]
    public async Task TrySetPassword_Rejected_LeavesUserUnchanged()
    {
        var user = await CreateUserWithPasswordAsync();
        var hash = user.PasswordHash;
        var stamp = user.SecurityStamp;

        Assert.False(await Passwords.TrySetPasswordAsync(user, "short", TestContext.Current.CancellationToken));
        Assert.False(await Passwords.TrySetPasswordAsync(user, "passwordpassword", TestContext.Current.CancellationToken));

        Assert.Equal(hash, user.PasswordHash);
        Assert.Equal(stamp, user.SecurityStamp);
    }

    private async Task<StaffUser> CreateUserWithPasswordAsync()
    {
        var user = StaffUser.Create(Guid.NewGuid(), UserName.Create("verify.user").Value, Now);
        Assert.True(await Passwords.TrySetPasswordAsync(user, Password, TestContext.Current.CancellationToken));
        hasher.Reset();
        return user;
    }

    public async ValueTask DisposeAsync()
    {
        await scope.DisposeAsync();
        await provider.DisposeAsync();
    }

    /// <summary>The real hasher, counting verifications.</summary>
    private sealed class CountingHasher : IPasswordHasher<StaffUser>
    {
        private readonly PasswordHasher<StaffUser> inner = new();

        public int Verifications { get; private set; }

        public string? LastVerifiedHash { get; private set; }

        public string HashPassword(StaffUser user, string password) => inner.HashPassword(user, password);

        public PasswordVerificationResult VerifyHashedPassword(StaffUser user, string hashedPassword, string providedPassword)
        {
            Verifications++;
            LastVerifiedHash = hashedPassword;
            return inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }

        public void Reset()
        {
            Verifications = 0;
            LastVerifiedHash = null;
        }
    }
}

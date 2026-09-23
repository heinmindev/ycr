using YCR.TestSupport;

namespace YCR.Infrastructure.Tests.TestSupport;

/// <summary>The fake clock every F-002 time-dependent test relies on (ADR-0018).</summary>
public sealed class TestClockTests
{
    [Fact]
    public void Advance_MovesUtcNowForwardByExactlyTheStep()
    {
        var clock = new TestClock();
        var before = clock.GetUtcNow();

        clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(before.AddSeconds(30), clock.GetUtcNow());
        Assert.Equal(TimeSpan.Zero, clock.GetUtcNow().Offset);
    }

    [Fact]
    public void Advance_Backwards_Throws()
    {
        var clock = new TestClock();

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public void Create_WithNonUtcStart_Throws()
    {
        Assert.Throws<ArgumentException>(() => new TestClock(new DateTimeOffset(2026, 9, 23, 9, 30, 0, TimeSpan.FromHours(6.5))));
    }
}

/// <summary>The ephemeral ES256 key tests sign with (ADR-0023 item 7).</summary>
public sealed class TestSigningKeyTests
{
    [Fact]
    public void Create_IsP256WithTestKidAndPkcs8Pem()
    {
        using var key = new TestSigningKey();

        Assert.Equal(256, key.Key.KeySize);
        Assert.StartsWith("test-", key.KeyId, StringComparison.Ordinal);
        Assert.StartsWith("-----BEGIN PRIVATE KEY-----", key.PrivateKeyPkcs8Pem, StringComparison.Ordinal);

        var configuration = key.ToConfiguration();
        Assert.Equal(key.KeyId, configuration["Auth:Signing:ActiveKeyId"]);
        Assert.Equal(key.PrivateKeyPkcs8Pem, configuration["Auth:Signing:Keys:0:PrivateKeyPkcs8Pem"]);
        Assert.Equal("true", configuration["Auth:Signing:Keys:0:DevelopmentOnly"]);
    }

    [Fact]
    public void Create_TwiceGeneratesDifferentKeys()
    {
        using var first = new TestSigningKey();
        using var second = new TestSigningKey();

        Assert.NotEqual(first.PrivateKeyPkcs8Pem, second.PrivateKeyPkcs8Pem);
        Assert.NotEqual(first.KeyId, second.KeyId);
    }
}

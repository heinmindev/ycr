using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Identity.Abstractions;

namespace YCR.Infrastructure.Tests.Identity;

/// <summary>R5 / S31: 256-bit random refresh tokens; only their SHA-256 is ever stored.</summary>
public sealed class RefreshTokenGeneratorTests
{
    [Fact]
    public async Task Generate_Returns32RandomBytesAndTheirSha256()
    {
        await using var provider = IdentityServiceProvider.Build();
        var generator = provider.GetRequiredService<IRefreshTokenGenerator>();

        var first = generator.Generate();
        var second = generator.Generate();

        Assert.Equal(32, Base64Url.DecodeFromChars(first.RawToken).Length);
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(first.RawToken)), first.TokenHash);
        Assert.Equal(first.TokenHash, generator.Hash(first.RawToken));
        Assert.NotEqual(first.RawToken, second.RawToken);
        Assert.DoesNotContain(first.RawToken, first.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64url !")]
    public async Task Hash_OfMalformedValue_IsStillA32ByteHash(string presented)
    {
        await using var provider = IdentityServiceProvider.Build();

        Assert.Equal(32, provider.GetRequiredService<IRefreshTokenGenerator>().Hash(presented).Length);
    }
}

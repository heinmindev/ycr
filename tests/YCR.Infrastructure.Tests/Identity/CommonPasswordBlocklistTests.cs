using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace YCR.Infrastructure.Tests.Identity;

/// <summary>
/// Plan P9: the embedded blocklist is present, filtered as documented, and byte-for-byte the file
/// whose provenance <c>THIRD-PARTY-NOTICES.md</c> records.
/// </summary>
public sealed class CommonPasswordBlocklistTests
{
    private const string ResourceName = "YCR.Infrastructure.Identity.common-passwords.txt";

    [Fact]
    public void Blocklist_LoadsFromEmbeddedResource()
    {
        var entries = Entries();

        Assert.True(entries.Count > 40_000, $"Only {entries.Count} entries.");
        Assert.Contains("123456789012", entries);
        Assert.Contains("passwordpassword", entries);
    }

    [Fact]
    public void Blocklist_HasNoEntryShorterThanTwelve()
    {
        var entries = Entries();

        Assert.All(entries, entry =>
        {
            var length = entry.EnumerateRunes().Count();
            Assert.InRange(length, 12, 128);
            Assert.Equal(entry.ToLowerInvariant(), entry);
        });
        Assert.Equal(entries.Count, entries.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Blocklist_Sha256MatchesNotice()
    {
        var notice = File.ReadAllText(Path.Combine(RepositoryRoot(), "THIRD-PARTY-NOTICES.md"));
        var recorded = Regex.Match(notice, @"\*\*Embedded file SHA-256:\*\* `([0-9a-f]{64})`").Groups[1].Value;

        var actual = Convert.ToHexStringLower(SHA256.HashData(ResourceBytes()));

        Assert.Equal(recorded, actual);
    }

    private static List<string> Entries() =>
        Encoding.UTF8.GetString(ResourceBytes()).Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();

    private static byte[] ResourceBytes()
    {
        using var stream = typeof(DependencyInjection).Assembly.GetManifestResourceStream(ResourceName);
        Assert.NotNull(stream);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "YCR.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No YCR.sln above the test directory.");
    }
}

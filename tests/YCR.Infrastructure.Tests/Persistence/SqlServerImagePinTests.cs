using System.Text.RegularExpressions;
using YCR.TestSupport;

namespace YCR.Infrastructure.Tests.Persistence;

/// <summary>
/// Spec E4: one pinned SQL Server 2022 image, by tag and digest, shared by compose and the
/// test fixture. These tests are what make "shared" true — a YAML file cannot reference a C#
/// constant, so without them the two pins would drift the first time one was bumped alone.
/// </summary>
public sealed partial class SqlServerImagePinTests
{
    [Fact]
    public void ComposeFile_PinsExactlyTheFixtureImage()
    {
        var composeImages = ImageLine().Matches(ReadComposeFile())
            .Select(match => match.Groups["image"].Value.Trim())
            .ToArray();

        Assert.NotEmpty(composeImages);
        Assert.All(composeImages, image => Assert.Equal(SqlServerImage.Reference, image));
    }

    [Fact]
    public void PinnedImage_CarriesBothATagAndADigest()
    {
        // A digest alone would be unreadable; a tag alone would float. E4 requires both.
        Assert.StartsWith("mcr.microsoft.com/mssql/server:2022-", SqlServerImage.Tag, StringComparison.Ordinal);
        Assert.StartsWith("sha256:", SqlServerImage.Digest, StringComparison.Ordinal);
        Assert.Equal(71, SqlServerImage.Digest.Length);
        Assert.Equal($"{SqlServerImage.Tag}@{SqlServerImage.Digest}", SqlServerImage.Reference);
    }

    /// <summary>
    /// The 2019 guard image is pinned on the same terms. The S19 test asserts that the migration
    /// reports the version it detected, so which build it runs against is part of what that test
    /// means — a floating tag would let the asserted version drift underneath it.
    /// </summary>
    [Fact]
    public void UnsupportedVersionImage_CarriesBothATagAndADigest()
    {
        Assert.StartsWith("mcr.microsoft.com/mssql/server:2019-", SqlServerImage.Unsupported.Tag, StringComparison.Ordinal);
        Assert.StartsWith("sha256:", SqlServerImage.Unsupported.Digest, StringComparison.Ordinal);
        Assert.Equal(71, SqlServerImage.Unsupported.Digest.Length);
        Assert.Equal(
            $"{SqlServerImage.Unsupported.Tag}@{SqlServerImage.Unsupported.Digest}",
            SqlServerImage.Unsupported.Reference);

        // It must actually be unsupported, or S19 would pass for the wrong reason.
        Assert.Equal(15, SqlServerImage.Unsupported.ProductMajorVersion);
        Assert.NotEqual(SqlServerImage.Digest, SqlServerImage.Unsupported.Digest);
    }

    private static string ReadComposeFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docker-compose.yml");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("docker-compose.yml was not found above the test output directory.");
    }

    [GeneratedRegex(@"^\s*image:\s*(?<image>\S+)\s*$", RegexOptions.Multiline)]
    private static partial Regex ImageLine();
}

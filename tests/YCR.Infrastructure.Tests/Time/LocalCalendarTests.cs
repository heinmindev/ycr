using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using YCR.Application.Common.Abstractions;
using YCR.Infrastructure.Time;
using YCR.TestSupport;

namespace YCR.Infrastructure.Tests.Time;

/// <summary>
/// ADR-0018 §Time, F-004 R38, plan P11 and ruling Q3: <c>Time:LocalTimeZone</c> resolves here, and
/// "today" is the Asia/Yangon date of the <see cref="TimeProvider"/> instant.
/// </summary>
public sealed class LocalCalendarTests
{
    /// <summary>
    /// V6, as amended by plan Amendment 1 (hein, 2026-09-25, T-046 G1): the IANA id bound from
    /// <c>Time:LocalTimeZone</c> resolves on the host the tests run on — the Windows dev machines
    /// through ICU, and in CI the Linux <c>ubuntu-latest</c> runner through its tzdata — and over the
    /// service horizon 2026–2040 it is UTC+06:30 with no daylight saving. The test asserts behaviour,
    /// not <see cref="TimeZoneInfo.SupportsDaylightSavingTime"/>: Linux tzdata carries pre-1946
    /// historical rules for Asia/Yangon, which make that flag true and are allowed.
    /// </summary>
    [Fact]
    public void AsiaYangon_ResolvesInThisEnvironment()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [LocalTimeOptions.ConfigurationKey] = "Asia/Yangon",
            })
            .Build();
        using var provider = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddOptions<LocalTimeOptions>().BindConfiguration(LocalTimeOptions.SectionName).Services
            .BuildServiceProvider();

        var zone = LocalTimeOptions.ResolveZone(provider.GetRequiredService<IOptions<LocalTimeOptions>>().Value.LocalTimeZone);

        var yangon = new TimeSpan(6, 30, 0);
        for (var year = HorizonFirstYear; year <= HorizonLastYear; year++)
        {
            foreach (var month in new[] { 1, 7 })
            {
                var localMidnight = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
                Assert.Equal(yangon, zone.GetUtcOffset(localMidnight));

                // Both sides of that local midnight, as instants: 17:29:59Z and 17:30:00Z the day before.
                var midnightUtc = new DateTimeOffset(localMidnight, yangon).ToUniversalTime();
                Assert.Equal(yangon, zone.GetUtcOffset(midnightUtc.AddSeconds(-1)));
                Assert.Equal(yangon, zone.GetUtcOffset(midnightUtc));
            }
        }

        var horizonStart = new DateTime(HorizonFirstYear, 1, 1);
        var horizonEnd = new DateTime(HorizonLastYear, 12, 31);
        Assert.All(
            zone.GetAdjustmentRules().Where(rule => rule.DateStart <= horizonEnd && rule.DateEnd >= horizonStart),
            rule => Assert.Equal(TimeSpan.Zero, rule.DaylightDelta));
    }

    private const int HorizonFirstYear = 2026;
    private const int HorizonLastYear = 2040;

    /// <summary>R38: local midnight in Asia/Yangon is 17:30:00Z the day before.</summary>
    [Theory]
    [InlineData("2026-09-30T17:29:59Z", "2026-09-30")]
    [InlineData("2026-09-30T17:30:00Z", "2026-10-01")]
    [InlineData("2026-10-01T03:00:00Z", "2026-10-01")]
    public void Today_AroundYangonMidnight_ReturnsTheYangonDate(string instant, string expected)
    {
        var clock = new TestClock(DateTimeOffset.Parse(instant, CultureInfo.InvariantCulture));
        using var provider = new ServiceCollection()
            .AddSingleton<TimeProvider>(clock)
            .AddInfrastructure("Server=(localdb)\\MSSQLLocalDB;Database=YcrCalendarTest")
            .Configure<LocalTimeOptions>(options => options.LocalTimeZone = "Asia/Yangon")
            .BuildServiceProvider();

        var today = provider.GetRequiredService<ILocalCalendar>().Today();

        Assert.Equal(DateOnly.ParseExact(expected, "yyyy-MM-dd", CultureInfo.InvariantCulture), today);
    }

    /// <summary>Ruling Q3: a missing or unknown zone fails with a message naming the key and the id.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Mars/Olympus")]
    [InlineData("+06:30")]
    public void ResolveZone_WithMissingOrUnknownZone_ThrowsWithClearMessage(string? id)
    {
        var failure = Assert.Throws<InvalidOperationException>(() => LocalTimeOptions.ResolveZone(id));

        Assert.Contains("Time:LocalTimeZone", failure.Message, StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(id))
        {
            Assert.Contains($"'{id}'", failure.Message, StringComparison.Ordinal);
            Assert.Contains("install IANA time-zone data", failure.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("is not configured", failure.Message, StringComparison.Ordinal);
        }
    }
}

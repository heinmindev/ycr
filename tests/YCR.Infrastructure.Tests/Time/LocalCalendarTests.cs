using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
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
    /// V6: the IANA id resolves on the host the tests run on — the Windows dev machines through ICU,
    /// and in CI the Linux <c>ubuntu-latest</c> runner through its tzdata. Asia/Yangon is UTC+06:30
    /// and has no daylight saving.
    /// </summary>
    [Fact]
    public void AsiaYangon_ResolvesInThisEnvironment()
    {
        var zone = LocalTimeOptions.ResolveZone("Asia/Yangon");

        Assert.Equal(new TimeSpan(6, 30, 0), zone.BaseUtcOffset);
        Assert.False(zone.SupportsDaylightSavingTime);
        Assert.Empty(zone.GetAdjustmentRules());
    }

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

using YCR.Domain.Timetable;

namespace YCR.Domain.Tests.Timetable;

/// <summary>
/// <see cref="PublishedTimeline"/> (F-005 plan P11; spec R19, R21, SV27-SV29, SV37).
/// </summary>
public sealed class PublishedTimelineTests
{
    private static readonly Guid V1 = Guid.Parse("5c000000-0000-0000-0000-000000000001");
    private static readonly Guid V2 = Guid.Parse("5c000000-0000-0000-0000-000000000002");
    private static readonly Guid V3 = Guid.Parse("5c000000-0000-0000-0000-000000000003");

    /// <summary>SV28/SV29: V1 from 2026-10-05, V3 from 2026-11-01 (inserted), V2 from 2027-01-01.</summary>
    private static PublishedTimeline Timeline() => PublishedTimeline.From(
    [
        (V2, new DateOnly(2027, 1, 1)),
        (V1, new DateOnly(2026, 10, 5)),
        (V3, new DateOnly(2026, 11, 1))
    ]);

    [Theory]
    [InlineData("2026-10-05", 1)] // SV27: V1 on its start date
    [InlineData("2026-10-31", 1)] // SV29
    [InlineData("2026-11-01", 3)]
    [InlineData("2026-11-15", 3)] // SV29
    [InlineData("2026-12-31", 3)]
    [InlineData("2027-01-01", 2)] // SV28
    [InlineData("2030-01-01", 2)] // SV28: open-ended
    public void InForceOn_ReturnsTheLatestStartOnOrBeforeTheDate(string date, int expected)
    {
        var id = expected switch { 1 => V1, 2 => V2, _ => V3 };

        Assert.Equal(id, Timeline().InForceOn(Date(date)));
    }

    [Fact]
    public void InForceOn_BeforeTheFirstVersion_ReturnsNone()
    {
        Assert.Null(Timeline().InForceOn(new DateOnly(2026, 10, 4)));
        Assert.Null(PublishedTimeline.Empty.InForceOn(new DateOnly(2026, 10, 5)));
        Assert.Null(PublishedTimeline.From([]).InForceOn(DateOnly.MaxValue));
    }

    [Fact]
    public void From_WithDuplicateStartDates_Throws()
    {
        Assert.Throws<ArgumentException>(() => PublishedTimeline.From(
            [(V1, new DateOnly(2026, 10, 5)), (V2, new DateOnly(2026, 10, 5))]));
        Assert.Throws<ArgumentException>(() => PublishedTimeline.From(
            [(V1, new DateOnly(2026, 10, 5)), (V1, new DateOnly(2026, 10, 6))]));
    }

    [Theory]
    [InlineData("V2", "2030-01-01", true)] // no successor: applies on every later date
    [InlineData("V1", "2026-10-30", true)] // successor V3 starts after D
    [InlineData("V1", "2026-11-01", false)] // successor V3 starts on D
    [InlineData("V1", "2026-12-01", false)] // successor V3 starts before D
    [InlineData("V3", "2026-12-31", true)] // SV37: successor V2 starts after D
    [InlineData("V3", "2027-01-01", false)] // SV37: successor V2 starts on D
    public void AppliesOnOrAfter_UsesTheSuccessorsStartDate(string version, string date, bool applies)
    {
        var id = version switch { "V1" => V1, "V2" => V2, _ => V3 };

        Assert.Equal(applies, Timeline().AppliesOnOrAfter(id, Date(date)));
    }

    [Fact]
    public void AppliesOnOrAfter_ForAVersionNotInTheTimeline_Throws()
    {
        Assert.Throws<ArgumentException>(() => Timeline().AppliesOnOrAfter(Guid.NewGuid(), new DateOnly(2026, 10, 5)));
    }

    private static DateOnly Date(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd");
}

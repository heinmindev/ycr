using YCR.Domain.Timetable;

namespace YCR.Domain.Tests.Timetable;

/// <summary><see cref="ServiceScheduleCoverage"/> (F-005 plan P10; spec R19, SV37, SV38).</summary>
public sealed class ServiceScheduleCoverageTests
{
    private static readonly Guid V1 = Guid.Parse("5c000000-0000-0000-0000-000000000001");
    private static readonly Guid V2 = Guid.Parse("5c000000-0000-0000-0000-000000000002");

    [Fact]
    public void AppliesOnOrAfter_OnlyForListingVersions()
    {
        // SV37: V1 (2026-10-05) lists S1; V2 (2027-01-01) does not.
        var timeline = PublishedTimeline.From([(V1, new DateOnly(2026, 10, 5)), (V2, new DateOnly(2027, 1, 1))]);
        var s1 = new ServiceScheduleCoverage(timeline, new HashSet<Guid> { V1 });

        Assert.True(s1.AppliesOnOrAfter(new DateOnly(2026, 12, 31)));
        Assert.False(s1.AppliesOnOrAfter(new DateOnly(2027, 1, 1)));

        // A service listed only by V2 is covered from any date: V2 is open-ended.
        var s2 = new ServiceScheduleCoverage(timeline, new HashSet<Guid> { V2 });
        Assert.True(s2.AppliesOnOrAfter(new DateOnly(2030, 1, 1)));

        // SV38: listed by no published version (drafts, discarded and cancelled are never given).
        var s3 = new ServiceScheduleCoverage(timeline, new HashSet<Guid>());
        Assert.False(s3.AppliesOnOrAfter(new DateOnly(2026, 10, 5)));
    }

    [Fact]
    public void None_NeverApplies()
    {
        Assert.False(ServiceScheduleCoverage.None.AppliesOnOrAfter(DateOnly.MinValue));
        Assert.False(ServiceScheduleCoverage.None.AppliesOnOrAfter(new DateOnly(2026, 10, 1)));
    }
}

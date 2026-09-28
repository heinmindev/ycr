using YCR.Application.Timetable;

namespace YCR.Application.Tests.Timetable;

/// <summary>
/// The <c>stopTimesSha256</c> canonical form (F-005 spec §8, E13; plan P18). The pinned vectors were
/// computed outside .NET (<c>printf … | sha256sum</c>) from the canonical text, so the form cannot
/// drift without a failing test and a <c>PayloadVersion</c> bump.
/// </summary>
public sealed class ScheduleStopTimesDigestTests
{
    private static readonly Guid First = Guid.Parse("5E000000-0000-0000-0000-000000000001");
    private static readonly Guid Second = Guid.Parse("5E000000-0000-0000-0000-00000000000B");

    /// <summary>The canonical text of <see cref="Rows"/>, lower-case ids, missing times empty.</summary>
    private const string CanonicalText =
        "5e000000-0000-0000-0000-000000000001|1||360\n" +
        "5e000000-0000-0000-0000-000000000001|2|380|382\n" +
        "5e000000-0000-0000-0000-000000000001|3|400|\n" +
        "5e000000-0000-0000-0000-00000000000b|1||420\n" +
        "5e000000-0000-0000-0000-00000000000b|2|440|\n";

    private static readonly ScheduleStopTimeDigestRow[] Rows =
    [
        new(First, 1, null, 360),
        new(First, 2, 380, 382),
        new(First, 3, 400, null),
        new(Second, 1, null, 420),
        new(Second, 2, 440, null),
    ];

    [Fact]
    public void Compute_WithNoStopTimes_IsTheEmptyVector()
    {
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", ScheduleStopTimesDigest.Compute([]));
    }

    [Fact]
    public void Compute_WithFixedIds_MatchesThePinnedVector()
    {
        var digest = ScheduleStopTimesDigest.Compute(Rows);

        Assert.Equal("55a0e7320eb0d1e88b7fe8e89d646815ebd3cfefae0f96d0931f291b226153d0", digest);
        Assert.Matches("^[0-9a-f]{64}$", digest);
    }

    [Fact]
    public void Compute_IsIndependentOfInputOrder()
    {
        var reversed = Rows.Reverse().ToArray();
        var shuffled = new[] { Rows[3], Rows[1], Rows[4], Rows[0], Rows[2] };

        Assert.Equal(ScheduleStopTimesDigest.Compute(Rows), ScheduleStopTimesDigest.Compute(reversed));
        Assert.Equal(ScheduleStopTimesDigest.Compute(Rows), ScheduleStopTimesDigest.Compute(shuffled));
        Assert.NotEqual(ScheduleStopTimesDigest.Compute(Rows), ScheduleStopTimesDigest.Compute(Rows[..4]));
    }
}

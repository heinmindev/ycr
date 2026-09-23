namespace YCR.TestSupport;

/// <summary>
/// A settable <see cref="TimeProvider"/> (plan F-002 "New packages": a few lines here instead of
/// <c>Microsoft.Extensions.TimeProvider.Testing</c>).
/// </summary>
/// <remarks>
/// ADR-0018: production code reads time only from <see cref="TimeProvider"/>, so a test that owns
/// this clock owns every timestamp the code under test writes — lockout ends, session expiry,
/// token <c>exp</c>, the principal-cache age. Thread-safe, because concurrency tests read it from
/// several requests at once. Always UTC.
/// </remarks>
public sealed class TestClock : TimeProvider
{
    private readonly Lock gate = new();
    private DateTimeOffset utcNow;

    /// <summary>Starts at a fixed, whole-second UTC instant unless told otherwise.</summary>
    public TestClock(DateTimeOffset? startUtc = null)
    {
        var start = startUtc ?? new DateTimeOffset(2026, 9, 23, 3, 0, 0, TimeSpan.Zero);
        if (start.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The test clock runs in UTC (ADR-0018).", nameof(startUtc));
        }

        utcNow = start;
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (gate)
        {
            return utcNow;
        }
    }

    /// <summary>Moves the clock forward; a negative step is refused (time never runs back).</summary>
    public void Advance(TimeSpan by)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(by, TimeSpan.Zero);

        lock (gate)
        {
            utcNow = utcNow.Add(by);
        }
    }
}

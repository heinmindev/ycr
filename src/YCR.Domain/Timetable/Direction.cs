namespace YCR.Domain.Timetable;

/// <summary>
/// The direction a service runs along its route's station order (R9).
/// </summary>
/// <remarks>
/// BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-25; T-044, OQ44; OQ36) — not a
/// Myanma Railways answer: a route has no direction; a service stores one, relative to its route's
/// station order. Stored as text (<c>CK_Services_Direction</c>), so the names are part of the data
/// and must not be renamed.
/// </remarks>
public enum Direction
{
    /// <summary>In route position order (1, 2, …, n; on a closed route, n wraps to 1).</summary>
    Forward,

    /// <summary>Against route position order (n, …, 2, 1; on a closed route, 1 wraps to n).</summary>
    Reverse
}

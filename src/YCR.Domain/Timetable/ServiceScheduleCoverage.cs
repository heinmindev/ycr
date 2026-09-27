namespace YCR.Domain.Timetable;

/// <summary>
/// Which published versions list one service, on the published timeline: what the withdrawal
/// guard (R19) needs to know, given by the handler under the Timetable-wide lock (F-005 plan P10).
/// </summary>
/// <remarks>
/// BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-26; T-053, OQ54) — not a Myanma
/// Railways answer: a service cannot be withdrawn from D while a published version that lists it
/// applies on some date ≥ D.
/// </remarks>
public sealed class ServiceScheduleCoverage
{
    private readonly PublishedTimeline _timeline;
    private readonly IReadOnlySet<Guid> _listingVersionIds;

    /// <param name="listingVersionIds">The published versions that list the service; each must be
    /// in <paramref name="timeline"/>.</param>
    public ServiceScheduleCoverage(PublishedTimeline timeline, IReadOnlySet<Guid> listingVersionIds)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(listingVersionIds);
        _timeline = timeline;
        _listingVersionIds = listingVersionIds;
    }

    /// <summary>No published version lists the service.</summary>
    public static ServiceScheduleCoverage None { get; } = new(PublishedTimeline.Empty, new HashSet<Guid>());

    /// <summary>Whether any published version that lists the service applies on some date ≥
    /// <paramref name="date"/> (R19).</summary>
    public bool AppliesOnOrAfter(DateOnly date) =>
        _listingVersionIds.Any(versionId => _timeline.AppliesOnOrAfter(versionId, date));
}

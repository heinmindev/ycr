namespace YCR.Domain.Timetable;

/// <summary>
/// The published versions ordered by start date: the one home of "in force" (R21) and "applies"
/// (R19) (F-005 plan P11).
/// </summary>
/// <remarks>
/// BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-26; T-053, OQ54, OQ55) — not a
/// Myanma Railways answer: exactly one version is in force on a date on or after the first
/// published start date, the one with the latest start date on or before it; a version applies
/// from its start date to the day before its successor's, or open-ended. Only published versions
/// are given to it, so drafts, discarded and cancelled versions never apply (R34).
/// </remarks>
public sealed class PublishedTimeline
{
    private readonly IReadOnlyList<(Guid Id, DateOnly EffectiveFrom)> _ordered;
    private readonly Dictionary<Guid, int> _indexById;

    private PublishedTimeline(IReadOnlyList<(Guid Id, DateOnly EffectiveFrom)> ordered)
    {
        _ordered = ordered;
        _indexById = new Dictionary<Guid, int>(ordered.Count);
        for (var index = 0; index < ordered.Count; index++)
        {
            _indexById.Add(ordered[index].Id, index);
        }
    }

    /// <summary>No published version: nothing is in force on any date.</summary>
    public static PublishedTimeline Empty { get; } = new([]);

    /// <exception cref="ArgumentException">Two versions share a start date or an id. R22's
    /// filtered unique index makes that impossible for published versions.</exception>
    public static PublishedTimeline From(IEnumerable<(Guid Id, DateOnly EffectiveFrom)> publishedVersions)
    {
        ArgumentNullException.ThrowIfNull(publishedVersions);
        var ordered = publishedVersions.OrderBy(version => version.EffectiveFrom).ToList();
        for (var index = 1; index < ordered.Count; index++)
        {
            if (ordered[index].EffectiveFrom == ordered[index - 1].EffectiveFrom)
            {
                throw new ArgumentException(
                    "Published versions have unique start dates (R22).", nameof(publishedVersions));
            }
        }

        if (ordered.Select(version => version.Id).Distinct().Count() != ordered.Count)
        {
            throw new ArgumentException("A version appears more than once.", nameof(publishedVersions));
        }

        return new PublishedTimeline(ordered.AsReadOnly());
    }

    /// <summary>R21: the version in force on <paramref name="date"/>, or null before the first.</summary>
    public Guid? InForceOn(DateOnly date)
    {
        Guid? inForce = null;
        foreach (var (id, effectiveFrom) in _ordered)
        {
            if (effectiveFrom > date)
            {
                break;
            }

            inForce = id;
        }

        return inForce;
    }

    /// <summary>
    /// R19: whether <paramref name="versionId"/> applies on some date ≥ <paramref name="date"/>:
    /// it has no successor, or its successor starts after <paramref name="date"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The version is not in this timeline.</exception>
    public bool AppliesOnOrAfter(Guid versionId, DateOnly date)
    {
        if (!_indexById.TryGetValue(versionId, out var index))
        {
            throw new ArgumentException("The version is not a published version of this timeline.", nameof(versionId));
        }

        return index == _ordered.Count - 1 || _ordered[index + 1].EffectiveFrom > date;
    }
}

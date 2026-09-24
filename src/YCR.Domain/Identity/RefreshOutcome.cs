namespace YCR.Domain.Identity;

/// <summary>What presenting a refresh token to its session did (ADR-0016 §Refresh; spec R5–R8).</summary>
public enum RefreshOutcome
{
    /// <summary>The current token was rotated; a successor now exists.</summary>
    Rotated,

    /// <summary>The immediate predecessor within the grace window: <c>409</c>, nothing changes (R7).</summary>
    Superseded,

    /// <summary>A predecessor after the grace window, or an older ancestor: the family is revoked (R8).</summary>
    FamilyReused,

    /// <summary>Revoked or expired session, or a hash not in this family: <c>401</c>, nothing changes.</summary>
    Invalid,
}

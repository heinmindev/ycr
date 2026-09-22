namespace YCR.Application.Common.Authorization;

public static class Permissions
{
    /// <summary>
    /// DECISION (tech lead, hein, 2026-09-22; T-014) - final; not a Myanma Railways answer:
    /// station-management permission constants are stable application policy names. Approved
    /// role grants (OQ28, resolved) are recorded in docs/10-authorization-matrix.md:
    /// stations.manage -> Admin and Railway Admin only; stations.read -> any authenticated
    /// operator role. No grant is seeded in code; no identity/role provisioning system exists
    /// yet (ADR-0020; deferred to the ADR-0016 follow-up feature).
    /// </summary>
    public const string StationsManage = "stations.manage";

    public const string StationsRead = "stations.read";
}

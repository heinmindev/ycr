namespace YCR.Application.Common.Authorization;

public static class Permissions
{
    /// <summary>
    /// DECISION (tech lead, hein, 2026-09-22; T-014) - final; not a Myanma Railways answer:
    /// station-management permission constants are stable application policy names. Approved
    /// role grants (OQ28, resolved) are recorded in docs/10-authorization-matrix.md:
    /// stations.manage -> SystemAdministrator and RailwayAdministrator; stations.read -> all
    /// eight roles. Grants are data, seeded by migration into identity.RolePermissions (F-002,
    /// D8), never in code.
    /// </summary>
    public const string StationsManage = "stations.manage";

    public const string StationsRead = "stations.read";

    /// <summary>
    /// Create and deactivate routes (F-003). BUSINESS DECISION - provisional tech-lead ruling
    /// (hein, 2026-09-24; T-032, OQ40) - not a Myanma Railways answer: routes.manage ->
    /// SystemAdministrator and RailwayAdministrator; routes.read -> all eight roles
    /// (docs/10 §Route permission grants). Holding a station permission gives no route right.
    /// Grants are data, seeded by migration, never in code. The name routes.read is an
    /// ENGINEERING DECISION (tech lead, hein, 2026-09-24; E2).
    /// </summary>
    public const string RoutesManage = "routes.manage";

    /// <summary>Read routes and their station sequences (F-003; see <see cref="RoutesManage"/>).</summary>
    public const string RoutesRead = "routes.read";

    /// <summary>
    /// BUSINESS DECISION - provisional tech-lead ruling (hein, 2026-09-23; T-023, D8) - not a
    /// Myanma Railways answer: the four identity permissions are held by SystemAdministrator
    /// only (docs/10 §Identity permission grants).
    /// </summary>
    public const string UsersRead = "users.read";

    /// <summary>Create, disable, enable, unlock and administrator password reset (docs/10).</summary>
    public const string UsersManage = "users.manage";

    /// <summary>Replace a user's role assignments (docs/10).</summary>
    public const string UsersRolesManage = "users.roles.manage";

    /// <summary>Revoke a staff member's auth session (docs/10; ADR-0016).</summary>
    public const string AuthSessionsRevoke = "auth-sessions.revoke";
}

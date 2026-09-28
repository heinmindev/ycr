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
    /// Create and withdraw services (F-004). BUSINESS DECISION - provisional tech-lead ruling
    /// (hein, 2026-09-25; T-044, OQ49) - not a Myanma Railways answer: services.manage ->
    /// SystemAdministrator and RailwayAdministrator; services.read -> all eight roles
    /// (docs/10 §Service permission grants). Holding a station or route permission gives no
    /// service right, and a service permission gives no station or route right. Grants are data,
    /// seeded by migration, never in code. The name services.read is an ENGINEERING DECISION
    /// (tech lead, hein, 2026-09-25; E6). There is no trains.* permission in Phase 1 (OQ42).
    /// </summary>
    public const string ServicesManage = "services.manage";

    /// <summary>Read services and their stops (F-004; see <see cref="ServicesManage"/>).</summary>
    public const string ServicesRead = "services.read";

    /// <summary>
    /// Create, discard, publish and cancel timetable versions (F-005). BUSINESS DECISION -
    /// provisional tech-lead ruling (hein, 2026-09-26; T-053, OQ59) - not a Myanma Railways answer:
    /// schedules.manage -> SystemAdministrator and RailwayAdministrator; schedules.read -> all eight
    /// roles (docs/10 §Schedule permission grants). There is no schedules.publish: publishing is
    /// part of schedules.manage, and a draft's author may publish it (OQ57). Holding a station,
    /// route or service permission gives no schedule right, and a schedule permission gives no
    /// station, route or service right. Grants are data, seeded by migration, never in code. The
    /// name schedules.read is an ENGINEERING DECISION (tech lead, hein, 2026-09-26; E5).
    /// </summary>
    public const string SchedulesManage = "schedules.manage";

    /// <summary>Read timetable versions, their times and the version in force (F-005; see <see cref="SchedulesManage"/>).</summary>
    public const string SchedulesRead = "schedules.read";

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

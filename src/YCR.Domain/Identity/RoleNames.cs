namespace YCR.Domain.Identity;

/// <summary>
/// The eight canonical role identifiers (spec R19), as seeded into <c>identity.Roles</c> and
/// written into <c>audit.AuditEvents.ActorRole</c>.
/// </summary>
/// <remarks>
/// BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-23; OQ12/D7) — not a Myanma
/// Railways answer. Used to validate role names and to find the last active
/// <see cref="SystemAdministrator"/> (R27) — <b>never</b> to authorize: authorization is by
/// permission only (R10).
/// </remarks>
public static class RoleNames
{
    public const string SystemAdministrator = "SystemAdministrator";
    public const string RailwayAdministrator = "RailwayAdministrator";
    public const string StationManager = "StationManager";
    public const string TicketOperator = "TicketOperator";
    public const string TicketInspector = "TicketInspector";
    public const string FinanceOfficer = "FinanceOfficer";
    public const string Auditor = "Auditor";
    public const string ReportingUser = "ReportingUser";

    public static IReadOnlyList<string> All { get; } =
    [
        SystemAdministrator,
        RailwayAdministrator,
        StationManager,
        TicketOperator,
        TicketInspector,
        FinanceOfficer,
        Auditor,
        ReportingUser,
    ];
}

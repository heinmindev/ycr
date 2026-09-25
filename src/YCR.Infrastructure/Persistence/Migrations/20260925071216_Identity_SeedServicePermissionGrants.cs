using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YCR.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Seeds the ten service role→permission grants (F-004 spec R3, R4; <c>docs/10</c> §Service
    /// permission grants; plan §DB changes, migration 2). No role, no user, and no <c>trains.*</c>
    /// or <c>schedules.*</c> grant (OQ42; B1).
    /// </summary>
    /// <remarks>
    /// BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-25; T-044, OQ49) — <b>not a
    /// Myanma Railways answer</b>: <c>services.manage</c> → <c>SystemAdministrator</c> and
    /// <c>RailwayAdministrator</c>; <c>services.read</c> → all eight roles. If Myanma Railways
    /// answers differently, the change is a new reviewed migration, never an API call (F-002 D8:
    /// grants are data, and no endpoint edits them).
    /// <para>
    /// The role ids are the fixed ids <c>Identity_SeedRolesAndPermissionGrants</c> assigned. They
    /// are re-declared here because that migration's constants are private, and one migration must
    /// not depend on another's code. <c>IdentitySeedTests.Seed_MatchesDocs10GrantTables</c> keeps
    /// this file and <c>docs/10</c> in step.
    /// </para>
    /// </remarks>
    public partial class Identity_SeedServicePermissionGrants : Migration
    {
        private const string SystemAdministrator = "0199b3a0-0000-7000-8000-000000000001";
        private const string RailwayAdministrator = "0199b3a0-0000-7000-8000-000000000002";
        private const string StationManager = "0199b3a0-0000-7000-8000-000000000003";
        private const string TicketOperator = "0199b3a0-0000-7000-8000-000000000004";
        private const string TicketInspector = "0199b3a0-0000-7000-8000-000000000005";
        private const string FinanceOfficer = "0199b3a0-0000-7000-8000-000000000006";
        private const string Auditor = "0199b3a0-0000-7000-8000-000000000007";
        private const string ReportingUser = "0199b3a0-0000-7000-8000-000000000008";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // docs/10 §Service permission grants: services.manage → SystemAdministrator and
            // RailwayAdministrator only; services.read → all eight roles. Ten rows; nothing else.
            migrationBuilder.Sql(
                $"""
                INSERT INTO [identity].[RolePermissions] ([RoleId], [Permission]) VALUES
                    ('{SystemAdministrator}', N'services.manage'),
                    ('{RailwayAdministrator}', N'services.manage'),
                    ('{SystemAdministrator}', N'services.read'),
                    ('{RailwayAdministrator}', N'services.read'),
                    ('{StationManager}', N'services.read'),
                    ('{TicketOperator}', N'services.read'),
                    ('{TicketInspector}', N'services.read'),
                    ('{FinanceOfficer}', N'services.read'),
                    ('{Auditor}', N'services.read'),
                    ('{ReportingUser}', N'services.read');
                """);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Deletes exactly the ten seeded pairs. Safe: nothing references <c>RolePermissions</c>.
        /// The effect is that every service endpoint answers <c>403</c>, which is the correct result
        /// of removing the grants.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""
                DELETE FROM [identity].[RolePermissions]
                WHERE ([Permission] = N'services.manage' AND [RoleId] IN ('{SystemAdministrator}', '{RailwayAdministrator}'))
                   OR ([Permission] = N'services.read' AND [RoleId] IN (
                        '{SystemAdministrator}', '{RailwayAdministrator}', '{StationManager}', '{TicketOperator}',
                        '{TicketInspector}', '{FinanceOfficer}', '{Auditor}', '{ReportingUser}'));
                """);
        }
    }
}

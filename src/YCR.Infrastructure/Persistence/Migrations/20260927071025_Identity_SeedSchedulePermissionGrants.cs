using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YCR.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Seeds the ten schedule role→permission grants (F-005 spec R3-R5; <c>docs/10</c> §Schedule
    /// permission grants; plan §DB changes 2, P23). No role, no user, and no <c>schedules.publish</c>
    /// grant (OQ59: publishing is part of <c>schedules.manage</c>).
    /// </summary>
    /// <remarks>
    /// BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-26; T-053, OQ59) — <b>not a
    /// Myanma Railways answer</b>: <c>schedules.manage</c> → <c>SystemAdministrator</c> and
    /// <c>RailwayAdministrator</c>; <c>schedules.read</c> → all eight roles. If Myanma Railways
    /// answers differently, the change is a new reviewed migration, never an API call (F-002 D8:
    /// grants are data, and no endpoint edits them).
    /// <para>
    /// The role ids are the fixed ids <c>Identity_SeedRolesAndPermissionGrants</c> assigned. They
    /// are re-declared here because that migration's constants are private, and one migration must
    /// not depend on another's code. <c>IdentitySeedTests.Seed_MatchesDocs10GrantTables</c> keeps
    /// this file and <c>docs/10</c> in step.
    /// </para>
    /// </remarks>
    public partial class Identity_SeedSchedulePermissionGrants : Migration
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
            // docs/10 §Schedule permission grants: schedules.manage → SystemAdministrator and
            // RailwayAdministrator only; schedules.read → all eight roles. Ten rows; nothing else.
            migrationBuilder.Sql(
                $"""
                INSERT INTO [identity].[RolePermissions] ([RoleId], [Permission]) VALUES
                    ('{SystemAdministrator}', N'schedules.manage'),
                    ('{RailwayAdministrator}', N'schedules.manage'),
                    ('{SystemAdministrator}', N'schedules.read'),
                    ('{RailwayAdministrator}', N'schedules.read'),
                    ('{StationManager}', N'schedules.read'),
                    ('{TicketOperator}', N'schedules.read'),
                    ('{TicketInspector}', N'schedules.read'),
                    ('{FinanceOfficer}', N'schedules.read'),
                    ('{Auditor}', N'schedules.read'),
                    ('{ReportingUser}', N'schedules.read');
                """);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Deletes exactly the ten seeded pairs. Safe: nothing references <c>RolePermissions</c>.
        /// The effect is that every schedule endpoint answers <c>403</c>, which is the correct result
        /// of removing the grants.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""
                DELETE FROM [identity].[RolePermissions]
                WHERE ([Permission] = N'schedules.manage' AND [RoleId] IN ('{SystemAdministrator}', '{RailwayAdministrator}'))
                   OR ([Permission] = N'schedules.read' AND [RoleId] IN (
                        '{SystemAdministrator}', '{RailwayAdministrator}', '{StationManager}', '{TicketOperator}',
                        '{TicketInspector}', '{FinanceOfficer}', '{Auditor}', '{ReportingUser}'));
                """);
        }
    }
}

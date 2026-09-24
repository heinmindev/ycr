using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YCR.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Seeds the eight roles and the fourteen role→permission grants (F-002 spec R11, R19, S30).
    /// No user is seeded: the first administrator comes from the one-time bootstrap command (D10).
    /// </summary>
    /// <remarks>
    /// BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-22, T-014 for
    /// <c>stations.*</c>; hein, 2026-09-23, T-023 for the role identifiers, <c>ReportingUser</c>'s
    /// <c>stations.read</c> and the identity permissions; OQ12, OQ34, D7, D8) — <b>not a Myanma
    /// Railways answer</b>. If Myanma Railways answers differently, the change is a new reviewed
    /// migration, never an API call (D8: grants are data, and no endpoint edits them).
    /// <para>
    /// The role ids are fixed here (application-assigned, ADR-0006) so later migrations can refer
    /// to a role by id. <c>docs/10</c> is the human-readable copy of these grants;
    /// <c>IdentitySeedTests.Seed_MatchesDocs10GrantTables</c> keeps the two in step.
    /// </para>
    /// </remarks>
    public partial class Identity_SeedRolesAndPermissionGrants : Migration
    {
        private const string SystemAdministrator = "0199b3a0-0000-7000-8000-000000000001";
        private const string RailwayAdministrator = "0199b3a0-0000-7000-8000-000000000002";
        private const string StationManager = "0199b3a0-0000-7000-8000-000000000003";
        private const string TicketOperator = "0199b3a0-0000-7000-8000-000000000004";
        private const string TicketInspector = "0199b3a0-0000-7000-8000-000000000005";
        private const string FinanceOfficer = "0199b3a0-0000-7000-8000-000000000006";
        private const string Auditor = "0199b3a0-0000-7000-8000-000000000007";
        private const string ReportingUser = "0199b3a0-0000-7000-8000-000000000008";

        private const string RoleIds =
            $"'{SystemAdministrator}', '{RailwayAdministrator}', '{StationManager}', '{TicketOperator}', " +
            $"'{TicketInspector}', '{FinanceOfficer}', '{Auditor}', '{ReportingUser}'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""
                INSERT INTO [identity].[Roles] ([Id], [Name]) VALUES
                    ('{SystemAdministrator}', N'SystemAdministrator'),
                    ('{RailwayAdministrator}', N'RailwayAdministrator'),
                    ('{StationManager}', N'StationManager'),
                    ('{TicketOperator}', N'TicketOperator'),
                    ('{TicketInspector}', N'TicketInspector'),
                    ('{FinanceOfficer}', N'FinanceOfficer'),
                    ('{Auditor}', N'Auditor'),
                    ('{ReportingUser}', N'ReportingUser');
                """);

            // docs/10 §Station permission grants: stations.manage → SystemAdministrator and
            // RailwayAdministrator only; stations.read → all eight roles.
            // docs/10 §Identity permission grants: the four identity permissions →
            // SystemAdministrator only. Fourteen rows; nothing else.
            migrationBuilder.Sql(
                $"""
                INSERT INTO [identity].[RolePermissions] ([RoleId], [Permission]) VALUES
                    ('{SystemAdministrator}', N'stations.manage'),
                    ('{RailwayAdministrator}', N'stations.manage'),
                    ('{SystemAdministrator}', N'stations.read'),
                    ('{RailwayAdministrator}', N'stations.read'),
                    ('{StationManager}', N'stations.read'),
                    ('{TicketOperator}', N'stations.read'),
                    ('{TicketInspector}', N'stations.read'),
                    ('{FinanceOfficer}', N'stations.read'),
                    ('{Auditor}', N'stations.read'),
                    ('{ReportingUser}', N'stations.read'),
                    ('{SystemAdministrator}', N'users.read'),
                    ('{SystemAdministrator}', N'users.manage'),
                    ('{SystemAdministrator}', N'users.roles.manage'),
                    ('{SystemAdministrator}', N'auth-sessions.revoke');
                """);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Deletes exactly the seeded rows. It fails, correctly, once any user holds one of these
        /// roles: removing a role that is in use is not something a down-migration should do.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""
                DELETE FROM [identity].[RolePermissions] WHERE [RoleId] IN ({RoleIds});
                DELETE FROM [identity].[Roles] WHERE [Id] IN ({RoleIds});
                """);
        }
    }
}

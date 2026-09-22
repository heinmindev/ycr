using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YCR.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Creates the <c>ycr_app</c> database role and grants it exactly what the application needs
    /// and nothing else (spec E7, ADR-0017 item 3).
    /// </summary>
    /// <remarks>
    /// The role is in a migration because it is schema-shaped: it grants on objects the two
    /// earlier migrations create, and it must change through the same review that changes them.
    /// It runs last for that reason.
    ///
    /// The <strong>login and user</strong> are not here. They need a credential, and plan P9 keeps
    /// every credential out of migration files: locally they come from
    /// <c>docker/sqlserver/init-principals.sql</c>, in tests from the container fixture, in CI from
    /// a CI step. The login is <c>ycr_app</c> and the user is <c>ycr_app_user</c>, because a role
    /// and a user are both database principals and cannot share a name.
    ///
    /// What is deliberately absent is the point of this migration:
    /// no DDL permission anywhere, no <c>DELETE</c> on either table, no <c>UPDATE</c> or
    /// <c>DELETE</c> on the audit ledger, and no <c>UPDATE</c> on any station column except
    /// <c>IsActive</c>. S22 asserts the absences, not just the presences — a grant that is too
    /// wide is the failure mode this role exists to prevent.
    /// </remarks>
    public partial class Security_AppDatabaseRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF DATABASE_PRINCIPAL_ID(N'ycr_app') IS NULL
                    EXEC(N'CREATE ROLE [ycr_app];');
                """);

            // BUSINESS DECISION (hein, 2026-09-20): UPDATE is granted on the IsActive column
            // alone, not on the table. DeactivateStation is the only update F-001 performs, and a
            // table-wide grant would also let the application rewrite a station's code or names —
            // neither of which any endpoint offers. A rename feature will need its own grant and
            // its own review, which is the intent.
            //
            // Nothing deletes a station: R3 relies on retained rows to keep codes from being reused.
            migrationBuilder.Sql(
                """
                GRANT SELECT, INSERT ON [network].[Stations] TO [ycr_app];
                GRANT UPDATE ON [network].[Stations]([IsActive]) TO [ycr_app];
                """);

            // ADR-0017 item 3: INSERT and SELECT only. No UPDATE and no DELETE, so the append-only
            // guarantee does not rest on the ledger engine alone.
            migrationBuilder.Sql(
                """
                GRANT INSERT, SELECT ON [audit].[AuditEvents] TO [ycr_app];
                """);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Reverting is safe here and is therefore allowed: it removes permissions and a role, and
        /// destroys no data. That is the opposite of the ledger migration, whose <c>Down()</c>
        /// throws because reverting it would lose history.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF DATABASE_PRINCIPAL_ID(N'ycr_app') IS NOT NULL
                BEGIN
                    REVOKE UPDATE ON [network].[Stations]([IsActive]) FROM [ycr_app];
                    REVOKE SELECT, INSERT ON [network].[Stations] FROM [ycr_app];
                    REVOKE INSERT, SELECT ON [audit].[AuditEvents] FROM [ycr_app];

                    -- A role with members cannot be dropped, so members are removed first.
                    DECLARE @member sysname;
                    DECLARE members CURSOR LOCAL FAST_FORWARD FOR
                        SELECT USER_NAME(m.member_principal_id)
                        FROM sys.database_role_members AS m
                        WHERE m.role_principal_id = DATABASE_PRINCIPAL_ID(N'ycr_app');

                    OPEN members;
                    FETCH NEXT FROM members INTO @member;
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        EXEC(N'ALTER ROLE [ycr_app] DROP MEMBER ' + QUOTENAME(@member) + N';');
                        FETCH NEXT FROM members INTO @member;
                    END;
                    CLOSE members;
                    DEALLOCATE members;

                    DROP ROLE [ycr_app];
                END;
                """);
        }
    }
}

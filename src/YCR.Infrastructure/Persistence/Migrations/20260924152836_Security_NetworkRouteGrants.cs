using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YCR.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Grants the <c>ycr_app</c> role exactly what route management needs on the two route
    /// tables, and nothing else (F-003 spec R10, S25; plan §DB changes, migration 3; the
    /// <c>Security_AppDatabaseRole</c> pattern).
    /// </summary>
    /// <remarks>
    /// These grants are the database-level statement of R9: a stored sequence never changes, and
    /// neither do a route's code, names or <c>IsClosed</c>. So even arbitrary SQL under the
    /// application credential cannot rewrite or delete a route. What is deliberately absent:
    /// <list type="bullet">
    /// <item><c>DELETE</c> on either table — routes are deactivated, never deleted (R15);</item>
    /// <item><c>UPDATE</c> of <c>Routes.Id</c>, <c>Code</c>, <c>NameEn</c>, <c>NameMy</c>,
    /// <c>IsClosed</c> or <c>CreatedAtUtc</c> — only deactivation updates a route (A1);</item>
    /// <item>any <c>UPDATE</c> on <c>RouteStations</c> — the sequence is insert-only;</item>
    /// <item>any DDL.</item>
    /// </list>
    /// ENGINEERING DECISION (tech lead, hein, 2026-09-24; E7 as amended by A1). A wider grant needs
    /// a new migration, a spec change and a review; it is never widened to make a test pass.
    /// <c>DatabasePrivilegeTests</c> asserts the presences and the absences.
    /// </remarks>
    public partial class Security_NetworkRouteGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                GRANT SELECT, INSERT ON [network].[Routes] TO [ycr_app];
                GRANT UPDATE ON [network].[Routes]([IsActive], [DeactivatedAtUtc]) TO [ycr_app];
                GRANT SELECT, INSERT ON [network].[RouteStations] TO [ycr_app];
                """);
        }

        /// <inheritdoc />
        /// <remarks>Safe to revert: it removes permissions and destroys no data.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                REVOKE SELECT, INSERT ON [network].[RouteStations] FROM [ycr_app];
                REVOKE UPDATE ON [network].[Routes]([IsActive], [DeactivatedAtUtc]) FROM [ycr_app];
                REVOKE SELECT, INSERT ON [network].[Routes] FROM [ycr_app];
                """);
        }
    }
}

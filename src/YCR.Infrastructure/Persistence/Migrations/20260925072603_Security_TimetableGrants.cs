using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YCR.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Grants the <c>ycr_app</c> role exactly what service management needs on the two timetable
    /// tables, and nothing else (F-004 spec §7, R33, S47; plan §DB changes, migration 3; the
    /// <c>Security_NetworkRouteGrants</c> pattern).
    /// </summary>
    /// <remarks>
    /// These grants are the database-level statement of R20 and R33: a service is immutable except
    /// for withdrawal, and is never deleted. So even arbitrary SQL under the application credential
    /// cannot rewrite or delete a service or its stops. What is deliberately absent:
    /// <list type="bullet">
    /// <item><c>DELETE</c> on either table — services are withdrawn, never deleted (R33);</item>
    /// <item><c>UPDATE</c> of any <c>Services</c> column other than <c>EffectiveTo</c> and
    /// <c>WithdrawnAtUtc</c> — withdrawal is the only update (R20, R36);</item>
    /// <item>any <c>UPDATE</c> on <c>ServiceStops</c> — stops are insert-only;</item>
    /// <item>any DDL, and any <c>EXECUTE</c>: the service-code lock is <c>sp_getapplock</c>, which
    /// <c>public</c> may execute (plan §R35).</item>
    /// </list>
    /// ENGINEERING DECISION (tech lead, hein, 2026-09-25; E11). A wider grant needs a new migration, a
    /// spec change and a review; it is never widened to make a test pass. <c>DatabasePrivilegeTests</c>
    /// asserts the presences and the absences.
    /// </remarks>
    public partial class Security_TimetableGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                GRANT SELECT, INSERT ON [timetable].[Services] TO [ycr_app];
                GRANT UPDATE ON [timetable].[Services]([EffectiveTo], [WithdrawnAtUtc]) TO [ycr_app];
                GRANT SELECT, INSERT ON [timetable].[ServiceStops] TO [ycr_app];
                """);
        }

        /// <inheritdoc />
        /// <remarks>Safe to revert: it removes permissions and destroys no data.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                REVOKE SELECT, INSERT ON [timetable].[ServiceStops] FROM [ycr_app];
                REVOKE UPDATE ON [timetable].[Services]([EffectiveTo], [WithdrawnAtUtc]) FROM [ycr_app];
                REVOKE SELECT, INSERT ON [timetable].[Services] FROM [ycr_app];
                """);
        }
    }
}

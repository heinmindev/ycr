using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YCR.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Grants the <c>ycr_app</c> role exactly what timetable versions need on their three tables,
    /// and nothing else (F-005 spec §7, R25, R27, R32, SV51; plan §DB changes 3, P22; the
    /// <c>Security_TimetableGrants</c> pattern).
    /// </summary>
    /// <remarks>
    /// These grants are the database-level statement of R25, R27 and R32: a version's content never
    /// changes after creation, only its status moves forward, and nothing is deleted. So even
    /// arbitrary SQL under the application credential cannot rewrite or delete a version, its
    /// entries or its times. What is deliberately absent:
    /// <list type="bullet">
    /// <item><c>DELETE</c> on all three tables — versions are discarded or cancelled, never deleted
    /// (R25);</item>
    /// <item><c>UPDATE</c> of <c>ScheduleVersions.Id</c>, <c>Number</c>, <c>NameEn</c>,
    /// <c>NameMy</c>, <c>EffectiveFrom</c> and <c>CreatedAtUtc</c> — only the status and its three
    /// instants move (R28, R30, R34);</item>
    /// <item>any <c>UPDATE</c> on <c>ScheduleVersionServices</c> or <c>ScheduleStopTimes</c> — they
    /// are insert-only (R27, R32);</item>
    /// <item>any DDL, and any <c>EXECUTE</c>: the Timetable-wide lock is <c>sp_getapplock</c>, which
    /// <c>public</c> may execute (plan §The Timetable-wide lock).</item>
    /// </list>
    /// ENGINEERING DECISION (tech lead, hein, 2026-09-26; E14). The F-004 grants on <c>Services</c>
    /// and <c>ServiceStops</c> are unchanged. A wider grant needs a new migration, a spec change and a
    /// review; it is never widened to make a test pass. <c>DatabasePrivilegeTests</c> asserts the
    /// presences and the absences.
    /// </remarks>
    public partial class Security_TimetableScheduleGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                GRANT SELECT, INSERT ON [timetable].[ScheduleVersions] TO [ycr_app];
                GRANT UPDATE ON [timetable].[ScheduleVersions]([Status], [PublishedAtUtc], [DiscardedAtUtc], [CancelledAtUtc]) TO [ycr_app];
                GRANT SELECT, INSERT ON [timetable].[ScheduleVersionServices] TO [ycr_app];
                GRANT SELECT, INSERT ON [timetable].[ScheduleStopTimes] TO [ycr_app];
                """);
        }

        /// <inheritdoc />
        /// <remarks>Safe to revert: it removes permissions and destroys no data.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                REVOKE SELECT, INSERT ON [timetable].[ScheduleStopTimes] FROM [ycr_app];
                REVOKE SELECT, INSERT ON [timetable].[ScheduleVersionServices] FROM [ycr_app];
                REVOKE UPDATE ON [timetable].[ScheduleVersions]([Status], [PublishedAtUtc], [DiscardedAtUtc], [CancelledAtUtc]) FROM [ycr_app];
                REVOKE SELECT, INSERT ON [timetable].[ScheduleVersions] FROM [ycr_app];
                """);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YCR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Timetable_CreateScheduleVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduleVersions",
                schema: "timetable",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameMy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", nullable: true),
                    DiscardedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleVersions", x => x.Id);
                    table.CheckConstraint("CK_ScheduleVersions_CancelledAtUtc_Utc", "[CancelledAtUtc] IS NULL OR DATEPART(TZOFFSET, [CancelledAtUtc]) = 0");
                    table.CheckConstraint("CK_ScheduleVersions_CreatedAtUtc_Utc", "DATEPART(TZOFFSET, [CreatedAtUtc]) = 0");
                    table.CheckConstraint("CK_ScheduleVersions_DiscardedAtUtc_Utc", "[DiscardedAtUtc] IS NULL OR DATEPART(TZOFFSET, [DiscardedAtUtc]) = 0");
                    table.CheckConstraint("CK_ScheduleVersions_Number", "[Number] >= 1");
                    table.CheckConstraint("CK_ScheduleVersions_PublishedAtUtc_Utc", "[PublishedAtUtc] IS NULL OR DATEPART(TZOFFSET, [PublishedAtUtc]) = 0");
                    table.CheckConstraint("CK_ScheduleVersions_Status", "[Status] IN (N'Draft', N'Published', N'Discarded', N'Cancelled')");
                    table.CheckConstraint("CK_ScheduleVersions_StatusInstants", "([Status] = N'Draft' AND [PublishedAtUtc] IS NULL AND [DiscardedAtUtc] IS NULL AND [CancelledAtUtc] IS NULL) OR ([Status] = N'Published' AND [PublishedAtUtc] IS NOT NULL AND [DiscardedAtUtc] IS NULL AND [CancelledAtUtc] IS NULL) OR ([Status] = N'Discarded' AND [PublishedAtUtc] IS NULL AND [DiscardedAtUtc] IS NOT NULL AND [CancelledAtUtc] IS NULL) OR ([Status] = N'Cancelled' AND [PublishedAtUtc] IS NOT NULL AND [DiscardedAtUtc] IS NULL AND [CancelledAtUtc] IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "ScheduleVersionServices",
                schema: "timetable",
                columns: table => new
                {
                    ScheduleVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleVersionServices", x => new { x.ScheduleVersionId, x.ServiceId });
                    table.ForeignKey(
                        name: "FK_ScheduleVersionServices_ScheduleVersions_ScheduleVersionId",
                        column: x => x.ScheduleVersionId,
                        principalSchema: "timetable",
                        principalTable: "ScheduleVersions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ScheduleVersionServices_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalSchema: "timetable",
                        principalTable: "Services",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ScheduleStopTimes",
                schema: "timetable",
                columns: table => new
                {
                    ScheduleVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    ArrivalMinute = table.Column<short>(type: "smallint", nullable: true),
                    DepartureMinute = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleStopTimes", x => new { x.ScheduleVersionId, x.ServiceId, x.Position });
                    table.CheckConstraint("CK_ScheduleStopTimes_AnyTime", "[ArrivalMinute] IS NOT NULL OR [DepartureMinute] IS NOT NULL");
                    table.CheckConstraint("CK_ScheduleStopTimes_Dwell", "[ArrivalMinute] IS NULL OR [DepartureMinute] IS NULL OR [DepartureMinute] >= [ArrivalMinute]");
                    table.CheckConstraint("CK_ScheduleStopTimes_Minutes", "([ArrivalMinute] IS NULL OR [ArrivalMinute] BETWEEN 0 AND 1439) AND ([DepartureMinute] IS NULL OR [DepartureMinute] BETWEEN 0 AND 1439)");
                    table.ForeignKey(
                        name: "FK_ScheduleStopTimes_ScheduleVersionServices_ScheduleVersionId_ServiceId",
                        columns: x => new { x.ScheduleVersionId, x.ServiceId },
                        principalSchema: "timetable",
                        principalTable: "ScheduleVersionServices",
                        principalColumns: new[] { "ScheduleVersionId", "ServiceId" });
                    table.ForeignKey(
                        name: "FK_ScheduleStopTimes_ServiceStops_ServiceId_Position",
                        columns: x => new { x.ServiceId, x.Position },
                        principalSchema: "timetable",
                        principalTable: "ServiceStops",
                        principalColumns: new[] { "ServiceId", "Position" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleStopTimes_ServiceId_Position",
                schema: "timetable",
                table: "ScheduleStopTimes",
                columns: new[] { "ServiceId", "Position" });

            migrationBuilder.CreateIndex(
                name: "UX_ScheduleVersions_EffectiveFrom_Published",
                schema: "timetable",
                table: "ScheduleVersions",
                column: "EffectiveFrom",
                unique: true,
                filter: "[Status] = N'Published'");

            migrationBuilder.CreateIndex(
                name: "UX_ScheduleVersions_Number",
                schema: "timetable",
                table: "ScheduleVersions",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleVersionServices_ServiceId",
                schema: "timetable",
                table: "ScheduleVersionServices",
                column: "ServiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduleStopTimes",
                schema: "timetable");

            migrationBuilder.DropTable(
                name: "ScheduleVersionServices",
                schema: "timetable");

            migrationBuilder.DropTable(
                name: "ScheduleVersions",
                schema: "timetable");
        }
    }
}

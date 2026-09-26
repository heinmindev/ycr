using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YCR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Timetable_CreateServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "timetable");

            migrationBuilder.CreateTable(
                name: "Services",
                schema: "timetable",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameMy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RunsOnMonday = table.Column<bool>(type: "bit", nullable: false),
                    RunsOnTuesday = table.Column<bool>(type: "bit", nullable: false),
                    RunsOnWednesday = table.Column<bool>(type: "bit", nullable: false),
                    RunsOnThursday = table.Column<bool>(type: "bit", nullable: false),
                    RunsOnFriday = table.Column<bool>(type: "bit", nullable: false),
                    RunsOnSaturday = table.Column<bool>(type: "bit", nullable: false),
                    RunsOnSunday = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", nullable: false),
                    WithdrawnAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                    table.CheckConstraint("CK_Services_CreatedAtUtc_Utc", "DATEPART(TZOFFSET, [CreatedAtUtc]) = 0");
                    table.CheckConstraint("CK_Services_Direction", "[Direction] IN (N'Forward', N'Reverse')");
                    table.CheckConstraint("CK_Services_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom] OR [WithdrawnAtUtc] IS NOT NULL");
                    table.CheckConstraint("CK_Services_OperatingDays", "[RunsOnMonday] = 1 OR [RunsOnTuesday] = 1 OR [RunsOnWednesday] = 1 OR [RunsOnThursday] = 1 OR [RunsOnFriday] = 1 OR [RunsOnSaturday] = 1 OR [RunsOnSunday] = 1");
                    table.CheckConstraint("CK_Services_WithdrawnAtUtc_Utc", "[WithdrawnAtUtc] IS NULL OR DATEPART(TZOFFSET, [WithdrawnAtUtc]) = 0");
                    table.ForeignKey(
                        name: "FK_Services_Routes_RouteId",
                        column: x => x.RouteId,
                        principalSchema: "network",
                        principalTable: "Routes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ServiceStops",
                schema: "timetable",
                columns: table => new
                {
                    ServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    StationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceStops", x => new { x.ServiceId, x.Position });
                    table.CheckConstraint("CK_ServiceStops_Position", "[Position] >= 1");
                    table.ForeignKey(
                        name: "FK_ServiceStops_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalSchema: "timetable",
                        principalTable: "Services",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ServiceStops_Stations_StationId",
                        column: x => x.StationId,
                        principalSchema: "network",
                        principalTable: "Stations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Services_Code_EffectiveFrom",
                schema: "timetable",
                table: "Services",
                columns: new[] { "Code", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_Services_RouteId",
                schema: "timetable",
                table: "Services",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceStops_StationId",
                schema: "timetable",
                table: "ServiceStops",
                column: "StationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceStops",
                schema: "timetable");

            migrationBuilder.DropTable(
                name: "Services",
                schema: "timetable");

            // Added by hand before this migration was ever applied (F-004 plan V8), as
            // Network_CreateStations does for `network`: this migration creates and owns the
            // `timetable` schema, so its rollback removes it. The cross-schema keys went with their
            // tables above, so `network` is untouched.
            migrationBuilder.DropSchema(
                name: "timetable");
        }
    }
}

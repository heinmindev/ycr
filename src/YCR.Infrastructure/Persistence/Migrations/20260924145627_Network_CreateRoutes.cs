using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YCR.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Creates <c>network.Routes</c> and <c>network.RouteStations</c> (F-003 spec §7; plan §DB
    /// changes, migration 1).
    /// </summary>
    /// <remarks>
    /// EF model-built. Every check constraint is declared in the EF model (R-4), so
    /// <c>has-pending-model-changes</c> sees any drift. Both foreign keys are <c>NO ACTION</c>.
    /// There is no <c>IX_RouteStations_StationId</c>: <c>UX_RouteStations_StationId_RouteId</c>
    /// leads with <c>StationId</c> and covers the station foreign key (spec Amendment 1, hein,
    /// 2026-09-24). <c>network.Stations</c> is not altered, and no data is written (R23, OQ1).
    /// <para>
    /// <c>Down()</c> drops the two tables, children first, and never the <c>network</c> schema,
    /// which <c>Network_CreateStations</c> owns. Once real routes exist that destroys route
    /// history, so in production a restore, not a down-migration, is the recovery path.
    /// </para>
    /// </remarks>
    public partial class Network_CreateRoutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Routes",
                schema: "network",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameMy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", nullable: false),
                    DeactivatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => x.Id);
                    table.CheckConstraint("CK_Routes_CreatedAtUtc_Utc", "DATEPART(TZOFFSET, [CreatedAtUtc]) = 0");
                    table.CheckConstraint("CK_Routes_DeactivatedAtUtc_Utc", "[DeactivatedAtUtc] IS NULL OR DATEPART(TZOFFSET, [DeactivatedAtUtc]) = 0");
                });

            migrationBuilder.CreateTable(
                name: "RouteStations",
                schema: "network",
                columns: table => new
                {
                    RouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    StationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteStations", x => new { x.RouteId, x.Position });
                    table.CheckConstraint("CK_RouteStations_Position", "[Position] >= 1");
                    table.ForeignKey(
                        name: "FK_RouteStations_Routes_RouteId",
                        column: x => x.RouteId,
                        principalSchema: "network",
                        principalTable: "Routes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RouteStations_Stations_StationId",
                        column: x => x.StationId,
                        principalSchema: "network",
                        principalTable: "Stations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "UX_Routes_Code",
                schema: "network",
                table: "Routes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_RouteStations_StationId_RouteId",
                schema: "network",
                table: "RouteStations",
                columns: new[] { "StationId", "RouteId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RouteStations",
                schema: "network");

            migrationBuilder.DropTable(
                name: "Routes",
                schema: "network");
        }
    }
}

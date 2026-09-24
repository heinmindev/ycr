using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YCR.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Creates the <c>identity</c> schema: users, roles, grants, auth sessions and refresh tokens
    /// (F-002 spec §7; plan §DB changes).
    /// </summary>
    /// <remarks>
    /// EF model-built. Every check constraint is declared in the EF model (R-4), so
    /// <c>has-pending-model-changes</c> sees any drift. Every foreign key is <c>NO ACTION</c>:
    /// nothing in <c>identity</c> is deleted except <c>UserRoles</c> rows (plan P7). No data is
    /// touched; the roles and grants are seeded by the next migration.
    /// <para>
    /// <c>Down()</c> drops the tables and the schema. Once staff accounts exist that destroys them,
    /// so in production a restore, not a down-migration, is the recovery path (plan §Rollback).
    /// </para>
    /// </remarks>
    public partial class Identity_CreateIdentitySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SecurityStamp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    DisabledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", nullable: true),
                    LockoutEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", nullable: true),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false),
                    PasswordChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_Users_AccessFailedCount", "[AccessFailedCount] BETWEEN 0 AND 10");
                    table.CheckConstraint("CK_Users_CreatedAtUtc_Utc", "DATEPART(TZOFFSET, [CreatedAtUtc]) = 0");
                    table.CheckConstraint("CK_Users_Disabled_Consistent", "([IsDisabled] = 1 AND [DisabledAtUtc] IS NOT NULL) OR ([IsDisabled] = 0 AND [DisabledAtUtc] IS NULL)");
                    table.CheckConstraint("CK_Users_DisabledAtUtc_Utc", "DATEPART(TZOFFSET, [DisabledAtUtc]) = 0");
                    table.CheckConstraint("CK_Users_LockoutEndUtc_Utc", "DATEPART(TZOFFSET, [LockoutEndUtc]) = 0");
                    table.CheckConstraint("CK_Users_PasswordChangedAtUtc_Utc", "DATEPART(TZOFFSET, [PasswordChangedAtUtc]) = 0");
                    table.CheckConstraint("CK_Users_UserName_Format", "LEN([UserName]) BETWEEN 3 AND 50 AND [UserName] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^a-z0-9.]%'");
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                schema: "identity",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Permission = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.Permission });
                    table.CheckConstraint("CK_RolePermissions_Permission_Format", "[Permission] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^-a-z.]%'");
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "identity",
                        principalTable: "Roles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AuthSessions",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", nullable: true),
                    RevocationReason = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthSessions", x => x.Id);
                    table.CheckConstraint("CK_AuthSessions_CreatedAtUtc_Utc", "DATEPART(TZOFFSET, [CreatedAtUtc]) = 0");
                    table.CheckConstraint("CK_AuthSessions_ExpiresAtUtc_Utc", "DATEPART(TZOFFSET, [ExpiresAtUtc]) = 0");
                    table.CheckConstraint("CK_AuthSessions_Expiry", "[ExpiresAtUtc] > [CreatedAtUtc]");
                    table.CheckConstraint("CK_AuthSessions_Revocation_Consistent", "([RevokedAtUtc] IS NULL AND [RevocationReason] IS NULL) OR ([RevokedAtUtc] IS NOT NULL AND [RevocationReason] IS NOT NULL)");
                    table.CheckConstraint("CK_AuthSessions_RevocationReason", "[RevocationReason] IN (N'Logout', N'PasswordChanged', N'AdministratorPasswordReset', N'UserDisabled', N'AdministratorRevoked', N'FamilyReuse')");
                    table.CheckConstraint("CK_AuthSessions_RevokedAtUtc_Utc", "DATEPART(TZOFFSET, [RevokedAtUtc]) = 0");
                    table.ForeignKey(
                        name: "FK_AuthSessions_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "identity",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "identity",
                        principalTable: "Roles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", nullable: false),
                    RotatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", nullable: true),
                    ReplacedByTokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.CheckConstraint("CK_RefreshTokens_IssuedAtUtc_Utc", "DATEPART(TZOFFSET, [IssuedAtUtc]) = 0");
                    table.CheckConstraint("CK_RefreshTokens_NotSelf", "[ReplacedByTokenId] <> [Id]");
                    table.CheckConstraint("CK_RefreshTokens_RotatedAtUtc_Utc", "DATEPART(TZOFFSET, [RotatedAtUtc]) = 0");
                    table.CheckConstraint("CK_RefreshTokens_Rotation_Consistent", "([RotatedAtUtc] IS NULL AND [ReplacedByTokenId] IS NULL) OR ([RotatedAtUtc] IS NOT NULL AND [ReplacedByTokenId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RefreshTokens_AuthSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "identity",
                        principalTable: "AuthSessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RefreshTokens_RefreshTokens_ReplacedByTokenId",
                        column: x => x.ReplacedByTokenId,
                        principalSchema: "identity",
                        principalTable: "RefreshTokens",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuthSessions_UserId",
                schema: "identity",
                table: "AuthSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_SessionId",
                schema: "identity",
                table: "RefreshTokens",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "UX_RefreshTokens_ReplacedByTokenId",
                schema: "identity",
                table: "RefreshTokens",
                column: "ReplacedByTokenId",
                unique: true,
                filter: "[ReplacedByTokenId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_RefreshTokens_TokenHash",
                schema: "identity",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Roles_Name",
                schema: "identity",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                schema: "identity",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "UX_Users_NormalizedUserName",
                schema: "identity",
                table: "Users",
                column: "NormalizedUserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefreshTokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "RolePermissions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AuthSessions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "identity");

            migrationBuilder.DropSchema(
                name: "identity");
        }
    }
}

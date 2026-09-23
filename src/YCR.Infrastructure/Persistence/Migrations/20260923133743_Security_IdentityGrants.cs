using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YCR.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Grants the <c>ycr_app</c> role exactly what the Identity module needs on <c>identity</c>,
    /// and nothing else (F-002 spec §7 Database grants; plan P7; the
    /// <c>Security_AppDatabaseRole</c> pattern).
    /// </summary>
    /// <remarks>
    /// What is deliberately absent is the point, as in <c>Security_AppDatabaseRole</c>:
    /// <list type="bullet">
    /// <item>no DDL anywhere;</item>
    /// <item>no write at all on <c>Roles</c> or <c>RolePermissions</c> — grants change by reviewed
    /// migration only (D8);</item>
    /// <item>no <c>DELETE</c> on <c>AuthSessions</c> or <c>RefreshTokens</c> — F-002 deletes nothing
    /// (D18, plan P7; a retention job is a before-production follow-up, T-026);</item>
    /// <item>no <c>DELETE</c> on <c>Users</c>, and no <c>UPDATE</c> of a user's <c>Id</c>,
    /// <c>UserName</c>, <c>NormalizedUserName</c> or <c>CreatedAtUtc</c>: no endpoint renames a
    /// user;</item>
    /// <item>no <c>UPDATE</c> on <c>UserRoles</c>: role replacement deletes and inserts rows, and
    /// that <c>DELETE</c> is the only one in <c>identity</c>. Role history is in the
    /// <c>Identity.RolesChanged</c> audit rows.</item>
    /// </list>
    /// <c>sp_getapplock</c> (plan P14) needs no grant: it is executable by <c>public</c> (V5).
    /// <c>DatabasePrivilegeTests</c> asserts the presences and the absences.
    /// </remarks>
    public partial class Security_IdentityGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                GRANT SELECT, INSERT ON [identity].[Users] TO [ycr_app];
                GRANT UPDATE ON [identity].[Users]([PasswordHash], [SecurityStamp], [PasswordChangedAtUtc], [MustChangePassword], [IsDisabled], [DisabledAtUtc], [LockoutEndUtc], [AccessFailedCount]) TO [ycr_app];

                GRANT SELECT ON [identity].[Roles] TO [ycr_app];
                GRANT SELECT ON [identity].[RolePermissions] TO [ycr_app];

                GRANT SELECT, INSERT, DELETE ON [identity].[UserRoles] TO [ycr_app];

                GRANT SELECT, INSERT ON [identity].[AuthSessions] TO [ycr_app];
                GRANT UPDATE ON [identity].[AuthSessions]([RevokedAtUtc], [RevocationReason]) TO [ycr_app];

                GRANT SELECT, INSERT ON [identity].[RefreshTokens] TO [ycr_app];
                GRANT UPDATE ON [identity].[RefreshTokens]([RotatedAtUtc], [ReplacedByTokenId]) TO [ycr_app];
                """);
        }

        /// <inheritdoc />
        /// <remarks>Safe to revert: it removes permissions and destroys no data.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                REVOKE UPDATE ON [identity].[RefreshTokens]([RotatedAtUtc], [ReplacedByTokenId]) FROM [ycr_app];
                REVOKE SELECT, INSERT ON [identity].[RefreshTokens] FROM [ycr_app];

                REVOKE UPDATE ON [identity].[AuthSessions]([RevokedAtUtc], [RevocationReason]) FROM [ycr_app];
                REVOKE SELECT, INSERT ON [identity].[AuthSessions] FROM [ycr_app];

                REVOKE SELECT, INSERT, DELETE ON [identity].[UserRoles] FROM [ycr_app];

                REVOKE SELECT ON [identity].[RolePermissions] FROM [ycr_app];
                REVOKE SELECT ON [identity].[Roles] FROM [ycr_app];

                REVOKE UPDATE ON [identity].[Users]([PasswordHash], [SecurityStamp], [PasswordChangedAtUtc], [MustChangePassword], [IsDisabled], [DisabledAtUtc], [LockoutEndUtc], [AccessFailedCount]) FROM [ycr_app];
                REVOKE SELECT, INSERT ON [identity].[Users] FROM [ycr_app];
                """);
        }
    }
}

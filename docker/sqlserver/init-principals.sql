-- Local development principals (plan P9, ADR-0017 item 3).
--
-- Creates the database and the two logins the platform runs under. It deliberately creates
-- NO role and NO grant: the `ycr_app` database role and everything it may do are created by
-- the Security_AppDatabaseRole migration, so privilege changes go through migration review
-- rather than through a provisioning script nobody diffs.
--
-- Run automatically by the `sqlserver-init` compose service. Passwords arrive as sqlcmd
-- variables from the environment, so no credential is written in this file.
--
-- Idempotent: `docker compose up -d` may run it many times against the same volume.
--
-- Both logins pin DEFAULT_LANGUAGE = us_english (hein's ruling, 2026-09-20). SQL Server
-- localises error messages by session language, and SqlServerUniqueConstraintTranslator reads a
-- violated constraint's name out of the message text because no API exposes it. The application
-- connection string pins `Current Language=us_english` for the same reason; the login default is
-- the belt to that braces, covering any tool that connects without naming a language.
-- See .env.example for the connection strings.

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF DB_ID(N'$(YcrDatabase)') IS NULL
BEGIN
    DECLARE @create nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(N'$(YcrDatabase)') + N';';
    EXEC sp_executesql @create;
END
GO

-- The migrator owns schema: it runs the migration bundle and nothing else.
IF SUSER_ID(N'ycr_migrator') IS NULL
BEGIN
    CREATE LOGIN [ycr_migrator] WITH
        PASSWORD = '$(YcrMigratorPassword)',
        CHECK_POLICY = ON,
        DEFAULT_LANGUAGE = us_english,
        DEFAULT_DATABASE = [$(YcrDatabase)];
END
GO

-- The application login. It holds no rights until it is added to the `ycr_app` database role,
-- which the Security_AppDatabaseRole migration creates.
--
-- The login is `ycr_app` but the database user is `ycr_app_user`, because a role and a user are
-- both database principals and SQL Server will not let them share a name. The role keeps the
-- plain name, since the role is what grants are written against and what a reader looks for.
IF SUSER_ID(N'ycr_app') IS NULL
BEGIN
    CREATE LOGIN [ycr_app] WITH
        PASSWORD = '$(YcrAppPassword)',
        CHECK_POLICY = ON,
        DEFAULT_LANGUAGE = us_english,
        DEFAULT_DATABASE = [$(YcrDatabase)];
END
GO

USE [$(YcrDatabase)];
GO

IF USER_ID(N'ycr_migrator') IS NULL
BEGIN
    CREATE USER [ycr_migrator] FOR LOGIN [ycr_migrator];
    ALTER ROLE [db_owner] ADD MEMBER [ycr_migrator];
END
GO

IF USER_ID(N'ycr_app_user') IS NULL
BEGIN
    CREATE USER [ycr_app_user] FOR LOGIN [ycr_app];
END
GO

-- Role membership is applied here only if the migration that creates the role has already run.
-- On a first `docker compose up -d` it has not, so the developer runs the bundle and then this
-- script again; the second run is what joins the two. Both runs are idempotent.
IF DATABASE_PRINCIPAL_ID(N'ycr_app') IS NOT NULL
   AND NOT EXISTS (SELECT 1
                   FROM sys.database_role_members AS m
                   WHERE m.role_principal_id = DATABASE_PRINCIPAL_ID(N'ycr_app')
                     AND m.member_principal_id = DATABASE_PRINCIPAL_ID(N'ycr_app_user'))
BEGIN
    ALTER ROLE [ycr_app] ADD MEMBER [ycr_app_user];
    PRINT 'ycr_app_user added to the ycr_app role.';
END
ELSE IF DATABASE_PRINCIPAL_ID(N'ycr_app') IS NULL
BEGIN
    PRINT 'The ycr_app role does not exist yet. Run the migration bundle as ycr_migrator, then re-run this script.';
END
GO

PRINT 'YCR principals ready.';
GO

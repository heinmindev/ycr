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
        DEFAULT_DATABASE = [$(YcrDatabase)];
END
GO

-- The application login. It holds no rights until Security_AppDatabaseRole creates the
-- `ycr_app` role and a later step adds this user to it (plan step 9).
IF SUSER_ID(N'ycr_app') IS NULL
BEGIN
    CREATE LOGIN [ycr_app] WITH
        PASSWORD = '$(YcrAppPassword)',
        CHECK_POLICY = ON,
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

IF USER_ID(N'ycr_app') IS NULL
BEGIN
    CREATE USER [ycr_app] FOR LOGIN [ycr_app];
END
GO

PRINT 'YCR principals ready. Run the migration bundle as ycr_migrator, then add ycr_app to the ycr_app role.';
GO

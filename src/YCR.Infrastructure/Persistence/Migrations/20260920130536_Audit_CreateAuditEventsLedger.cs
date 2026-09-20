using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YCR.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Creates <c>audit.AuditEvents</c> as a SQL Server 2022 append-only ledger table.
    /// </summary>
    /// <remarks>
    /// Raw SQL, not EF model building, because ADR-0017 item 1 requires it and EF cannot emit
    /// <c>LEDGER = ON</c>. The matching entity is mapped with <c>ExcludeFromMigrations()</c>, so
    /// EF can INSERT into this table but can never generate DDL that converts or drops it
    /// (ADR-0017 item 6).
    ///
    /// Order matters and is deliberate: the guard runs before any DDL, so an unsupported server
    /// leaves nothing half-created. V3 confirmed on the pinned image that ledger DDL runs inside
    /// EF's migration transaction and rolls back cleanly, so no <c>suppressTransaction</c> is
    /// needed and a failure anywhere in this migration leaves no partial schema behind.
    ///
    /// The table, its three check constraints and its two indexes are created together because
    /// ADR-0021 item 5 requires the constraints to exist from the table's creation — retro-fitting
    /// a check constraint onto a populated append-only ledger is materially harder.
    /// </remarks>
    public partial class Audit_CreateAuditEventsLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Guard first (ADR-0017 item 5, spec S19).
            //
            // The version check is the hard one: ledger tables do not exist before SQL Server
            // 2022, and step 12 proves this branch fires against a pinned 2019 image.
            //
            // The second check probes the running engine's ledger surface rather than testing an
            // edition name against a hardcoded list. What it proves: this engine exposes ledger
            // catalog objects. What it does not prove: that a licence permits their use. No
            // container is available that runs a 2022 edition without ledger, so the edition
            // branch is covered by V1 in step 8 and by code review, which is stated here rather
            // than left for a reader to discover.
            migrationBuilder.Sql(
                """
                DECLARE @version nvarchar(128) = CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128));
                DECLARE @major int = TRY_CAST(SERVERPROPERTY('ProductMajorVersion') AS int);
                DECLARE @edition nvarchar(128) = CAST(SERVERPROPERTY('Edition') AS nvarchar(128));
                DECLARE @message nvarchar(2048);

                IF @major IS NULL OR @major < 16
                BEGIN
                    SET @message = N'YCR requires SQL Server 2022 (major version 16) or later for the append-only audit ledger (ADR-0017). Detected version '
                        + ISNULL(@version, N'(unknown)') + N', edition ' + ISNULL(@edition, N'(unknown)') + N'.';
                    THROW 50017, @message, 1;
                END;

                IF OBJECT_ID('sys.database_ledger_transactions') IS NULL
                   OR NOT EXISTS (SELECT 1 FROM sys.all_columns
                                  WHERE object_id = OBJECT_ID('sys.tables') AND name = 'ledger_type')
                BEGIN
                    SET @message = N'This SQL Server does not expose ledger support, which the audit trail requires (ADR-0017). Detected version '
                        + ISNULL(@version, N'(unknown)') + N', edition ' + ISNULL(@edition, N'(unknown)') + N'.';
                    THROW 50017, @message, 1;
                END;
                """);

            // 2. Schema.
            migrationBuilder.Sql(
                """
                IF SCHEMA_ID(N'audit') IS NULL EXEC(N'CREATE SCHEMA [audit] AUTHORIZATION [dbo];');
                """);

            // 3-4. The ledger table with its check constraints, exactly as ADR-0021 defines them.
            //
            // The generated ledger columns (ledger_start_transaction_id, ledger_start_sequence_number)
            // are added and hidden by SQL Server. They are deliberately absent from the entity:
            // the server owns their values.
            migrationBuilder.Sql(
                """
                CREATE TABLE [audit].[AuditEvents]
                (
                    [Id]                     uniqueidentifier  NOT NULL CONSTRAINT [PK_AuditEvents] PRIMARY KEY,
                    [OccurredAtUtc]          datetimeoffset(3) NOT NULL,
                    [Action]                 nvarchar(100)     NOT NULL,
                    [ActorUserId]            uniqueidentifier  NULL,
                    [ActorRole]              nvarchar(1000)    NULL,
                    [SubjectType]            nvarchar(100)     NOT NULL,
                    [SubjectId]              uniqueidentifier  NULL,
                    [BeforeJson]             nvarchar(max)     NULL,
                    [AfterJson]              nvarchar(max)     NULL,
                    [CorrelationId]          nvarchar(100)     NOT NULL,
                    [ClientIp]               nvarchar(45)      NULL,
                    [ReasonCode]             nvarchar(100)     NULL,
                    [AuthorizedByPermission] nvarchar(100)     NULL,
                    [PayloadVersion]         int               NOT NULL,
                    CONSTRAINT [CK_AuditEvents_BeforeJson] CHECK ([BeforeJson] IS NULL OR ISJSON([BeforeJson]) = 1),
                    CONSTRAINT [CK_AuditEvents_AfterJson]  CHECK ([AfterJson]  IS NULL OR ISJSON([AfterJson])  = 1),
                    CONSTRAINT [CK_AuditEvents_ActorRole]  CHECK ([ActorRole]  IS NULL OR ISJSON([ActorRole])  = 1)
                )
                WITH (LEDGER = ON (APPEND_ONLY = ON));
                """);

            // 5. The two investigation indexes (ADR-0021 §Constraints and indexes).
            migrationBuilder.Sql(
                """
                CREATE NONCLUSTERED INDEX [IX_AuditEvents_Subject]
                    ON [audit].[AuditEvents] ([SubjectType], [SubjectId]);
                """);

            migrationBuilder.Sql(
                """
                CREATE NONCLUSTERED INDEX [IX_AuditEvents_OccurredAtUtc]
                    ON [audit].[AuditEvents] ([OccurredAtUtc]);
                """);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Plan P7: this throws rather than dropping the table. Dropping an append-only ledger
        /// table would destroy tamper-evident history, and ADR-0017 item 6 forbids a migration
        /// that silently converts or drops one. Recovery is a documented manual operation against
        /// a restored backup, performed by an administrator who has decided to accept that loss.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                THROW 50017, N'Audit_CreateAuditEventsLedger cannot be reverted: dropping audit.AuditEvents would destroy tamper-evident audit history, which ADR-0017 item 6 forbids. Recovery is a manual operation against a restored backup.', 1;
                """);
        }
    }
}

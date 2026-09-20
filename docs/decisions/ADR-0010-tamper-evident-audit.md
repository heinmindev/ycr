# ADR-0010: Audit Log on SQL Server 2022 Append-Only Ledger Table

## Status

Superseded by ADR-0017

## Decision

1. Production runs **SQL Server 2022** (recorded here; OQ20 resolved).
2. `audit.AuditEvents` is an **append-only ledger table**:
   ```sql
   CREATE TABLE audit.AuditEvents ( ... )
   WITH (LEDGER = ON (APPEND_ONLY = ON));
   ```
   It is created through `migrationBuilder.Sql(...)` in an EF migration, because EF Core doesn't model ledger options.
3. Columns (minimum): `Id`, `OccurredAtUtc`, `ActorUserId`, `ActorRole`, `Action` (e.g. `Ticketing.TicketIssued`), `EntityType`, `EntityId`, `BusinessDate`, `CorrelationId`, `ClientIp`, `DataBefore`/`DataAfter` (JSON, sensitive fields excluded), `ReasonCode`.
4. The audit row is written **in the same transaction** as the business change (`IAuditWriter` adds it to the DbContext before `SaveChangesAsync`). If the audit write fails, the business action fails.
5. Database digests are generated on a schedule (`sys.sp_generate_database_ledger_digest`) and stored **outside** the database server, in immutable/WORM storage owned by a different admin. Verification (`sys.sp_verify_database_ledger`) runs on a schedule and on demand for auditors, and alerts on failure.
6. The app's DB login has only INSERT/SELECT on the `audit` schema.

## Consequences

- Tampering, even by a DBA, becomes detectable, which answers the "audit tampering" and "insider misuse" threats.
- Ledger tables can't be converted back to normal tables. Their schema changes must be additive, so review audit schema carefully.
- Retention (OQ14) must be designed with ledger behaviour in mind: rows can't be deleted from append-only tables.
- Local dev and CI must use a SQL Server 2022 container image.

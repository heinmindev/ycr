# Database Design Proposal

## Core tables

- Stations
- Routes
- RouteStations
- Trains
- TrainServices
- ServiceStops
- ScheduleVersions
- FareRuleSets
- FareRules
- PassengerCategories
- Tickets
- TicketValidations
- Payments
- Refunds
- CashierSessions
- Sales
- audit.AuditEvents (SQL Server ledger table)
- IdempotencyRecords

## Rules

- Use foreign keys.
- Use unique constraints for business identifiers.
- Use `datetimeoffset(3)` for instants, storing UTC values for `*Utc` columns; use `date` for calendar dates.
- Preserve historical fare/schedule versions.
- Never overwrite financial history.
- Add indexes based on measured query patterns.

## Module schemas and approved cross-cutting tables

**ENGINEERING DECISION (tech lead, cite ADR-0012):** operational tables use one schema per module (`network`, `timetable`, `fare`, `ticketing`, `payments`, `operations`, `identity`, `audit`). Reporting reads through read-only views/projections.

**ENGINEERING DECISION (tech lead, cite ADR-0017):** `audit.AuditEvents` is append-only ledger-backed; it is not the former generic `AuditLogs` table.

**ENGINEERING DECISION (tech lead, cite ADR-0019):** Sale, Payment, ticket-cancellation, and Refund records own their `BusinessDate` columns, with nullable `CashierSessionId` where session ownership applies. The operation and date are written atomically; there is no polymorphic `BusinessDateAssignments` table.

Exact columns must be designed after requirements discovery.

## F-001 tables, constraints and indexes

The first two tables the walking skeleton creates. Both are applied by the EF migration bundle
under the `ycr_migrator` credential; the application login has no DDL rights (spec E7, ADR-0017
item 3).

### `network.Stations`

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | no | PK, clustered, application-assigned through `IIdGenerator` (ADR-0006) |
| `Code` | `nvarchar(10)` | no | Human-facing station code |
| `NameEn` | `nvarchar(100)` | no | Owned value object `BilingualName` |
| `NameMy` | `nvarchar(100)` | no | Myanmar Unicode, never Zawgyi (`docs/20` §6; OQ29 covers input handling) |
| `IsActive` | `bit` | no | EF concurrency token, so two concurrent deactivations cannot both win |
| `CreatedAtUtc` | `datetimeoffset(3)` | no | UTC value (ADR-0018) |

| Object | Definition | Why it is there |
|---|---|---|
| `PK_Stations` | clustered on `Id` | Sequential GUIDs keep it from fragmenting (ADR-0006 REQUIRED CONTROL, proved by the trunk-only fragmentation test) |
| `UX_Stations_Code` | unique on `Code` | The concurrency authority for duplicate codes: the pre-check can lose a race, this cannot. Also what makes "codes are never reused" true, because deactivated rows are retained. `CreateStationHandler` matches this exact name to map the violation to `409` |
| `CK_Stations_CreatedAtUtc_Utc` | `DATEPART(TZOFFSET, [CreatedAtUtc]) = 0` | ADR-0018 requires `*Utc` columns to hold UTC. The domain factory rejects a non-zero offset, and this binds every other writer of the column, including direct SQL under the application credential |

Rows are never deleted. Deactivation sets `IsActive = 0` and the row stays, because the unique
index on a retained row is what enforces non-reuse of the code.

### `audit.AuditEvents`

An append-only SQL Server 2022 **ledger** table created by raw SQL in
`Audit_CreateAuditEventsLedger`, not by EF model building (ADR-0017 item 1). **ADR-0021 is
authoritative for its fourteen columns.** EF maps it with `ExcludeFromMigrations()` so it can
INSERT but can never emit DDL that converts or drops it (ADR-0017 item 6).

| Object | Definition | Why it is there |
|---|---|---|
| `PK_AuditEvents` | on `Id` | Application-assigned (ADR-0006) |
| `CK_AuditEvents_BeforeJson` | `BeforeJson IS NULL OR ISJSON(BeforeJson) = 1` | A malformed payload can never be repaired on an append-only table, so it must be refused on the way in (ADR-0021 item 5) |
| `CK_AuditEvents_AfterJson` | `AfterJson IS NULL OR ISJSON(AfterJson) = 1` | ″ |
| `CK_AuditEvents_ActorRole` | `ActorRole IS NULL OR ISJSON(ActorRole) = 1` | ″. `ISJSON` proves the text is JSON, not that it is an array; `IAuditWriter` owns that, with a test |
| `CK_AuditEvents_OccurredAtUtc_Utc` | `DATEPART(TZOFFSET, [OccurredAtUtc]) = 0` | The counterpart of the `Stations` check. It matters more here: `OccurredAtUtc` orders tamper-evident history and the row cannot be corrected afterwards |
| `IX_AuditEvents_Subject` | nonclustered on `(SubjectType, SubjectId)` | "What happened to this thing?" — the investigation path ADR-0021 names |
| `IX_AuditEvents_OccurredAtUtc` | nonclustered on `OccurredAtUtc` | "What happened in this window?" — the other investigation path |

All five check constraints are created **with the table**, because retro-fitting one onto a
populated append-only ledger is materially harder (ADR-0021 item 5). `Down()` throws rather than
dropping the table (plan P7).

Both UTC check constraints are declared in the EF model with `HasCheckConstraint` as well as in
their migrations, and CI runs `dotnet ef migrations has-pending-model-changes`, so a constraint
removed from either side fails the build.

## F-001 station persistence controls

The `network.Stations.CreatedAtUtc` column is `datetimeoffset(3)` and carries
`CK_Stations_CreatedAtUtc_Utc`, which rejects any stored value whose offset is not zero. This is
the database counterpart to the domain factory's UTC guard required by ADR-0018.

The EF design-time factory is `src/YCR.Infrastructure/Persistence/YcrDbContextFactory.cs`. It is
used only by EF tooling and reads the required `YCR_DESIGN_TIME_CONNECTION` environment variable;
it has no fallback connection, so a design-time database update cannot silently target a developer's
LocalDB. **Set `YCR_DESIGN_TIME_CONNECTION` before running `dotnet ef migrations add` or any other
EF design-time command; there is no default and the command fails without it.** The test fixture
supplies an inert placeholder to the `dotnet ef` process it spawns when the variable is absent, so
`dotnet test YCR.sln` needs no setup, and it never replaces a value that is already set.
`src/YCR.Domain/Properties/AssemblyInfo.cs` grants `YCR.Infrastructure` access to the
domain's internal EF materialisation path (`StationCode.From`).

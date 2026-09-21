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

## F-001 station persistence controls

The `network.Stations.CreatedAtUtc` column is `datetimeoffset(3)` and carries
`CK_Stations_CreatedAtUtc_Utc`, which rejects any stored value whose offset is not zero. This is
the database counterpart to the domain factory's UTC guard required by ADR-0018.

The EF design-time factory is `src/YCR.Infrastructure/Persistence/YcrDbContextFactory.cs`. It is
used only by EF tooling and reads the required `YCR_DESIGN_TIME_CONNECTION` environment variable;
it has no fallback connection, so a design-time database update cannot silently target a developer's
LocalDB. `src/YCR.Domain/Properties/AssemblyInfo.cs` grants `YCR.Infrastructure` access to the
domain's internal EF materialisation path (`StationCode.From`).

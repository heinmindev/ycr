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

# ADR-0006: GUID Primary Keys + Human Business Numbers

## Status

Accepted — 2026-09-19

## Decision

1. Every aggregate and entity uses a `Guid` primary key. The key is **generated in the application** (not the DB), so an ID exists before saving. This supports idempotency and a possible later offline mode.
2. The same `Guid` is the resource ID in APIs (`/tickets/{id}`). No separate int/GUID double key.
3. Human-facing identifiers are separate columns with unique constraints, and they are value objects:
   - `StationCode`, e.g. short station code (format: OPEN QUESTION, from authoritative station list)
   - `TicketNumber`, printed on the ticket and encoded in the QR (see ADR-0014)
   - `CashierSessionNumber`, `RefundNumber`, `PaymentReference`
   Their formats and generation rules are defined in the owning module's doc, never invented in code.
4. **SQL Server ordering caveat:** SQL Server sorts `uniqueidentifier` by its *last* bytes first. A raw `Guid.CreateVersion7()` is time-ordered in .NET but **not** sequential in a SQL Server clustered index, so it would cause page splits. Generate keys with a SQL-Server-ordered sequential generator (EF Core's `SequentialGuidValueGenerator`, or a v7 generator with bytes reordered for SQL Server) behind one `IIdGenerator` abstraction. Verify with an integration test that inserts 10k rows and checks index fragmentation.

## Consequences

- IDs in URLs can't be enumerated.
- Keys are 16 bytes instead of 4 or 8. This is acceptable at YCR volumes.
- Business numbers need their own generator and uniqueness tests, including a concurrency test.

## Dated amendment - 2026-09-19

**ENGINEERING DECISION (tech lead, cite ADR-0006):** the application implementation is named `SqlServerSequentialGuidIdGenerator`, behind `IIdGenerator`, and wraps EF Core's SQL Server-oriented `SequentialGuidValueGenerator`. Entity mappings remain `ValueGeneratedNever()` because the application assigns the ID before persistence.

**REQUIRED CONTROL:** the integration test inserts 10,000 rows into a clustered GUID key and compares fragmentation with a SQL Server `NEWSEQUENTIALID()` baseline. The generator passes when fragmentation is no more than 10 percentage points worse than that baseline under the same test setup. The test records row count, index name, fragmentation, and database compatibility level.

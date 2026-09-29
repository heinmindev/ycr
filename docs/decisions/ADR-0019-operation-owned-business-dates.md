# ADR-0019: Operation-Owned Business Dates

## Status

Accepted — 2026-09-19

## Supersedes

The business-date persistence portion of ADR-0018 only. ADR-0018 time and money decisions remain in force.

## Context

**FACT — review B5:** a polymorphic `operations.BusinessDateAssignments` table with `OperationType + OperationId` has no foreign-key integrity and can disagree with the operation row.

**ENGINEERING DECISION (tech lead, cite ADR-0019):** business date is owned by the operation record that is being reported or reconciled.

## Options considered

1. Polymorphic assignment table - flexible, but lacks foreign keys and creates a second source of truth.
2. Business-date columns on operation records plus a documentation rule table - preserves foreign-key integrity and keeps the value with the operation.
3. Infer business date at report time - avoids columns, but makes historical reporting unstable.

## Decision

Store `BusinessDate` on each operation record and `CashierSessionId` where session ownership applies:

| Operation record | Stored fields | Date source | Blocked by |
|---|---|---|---|
| Sale | `BusinessDate`, nullable `CashierSessionId` | Cashier session date when session-owned | OQ2/OQ15 session policy |
| Payment | `BusinessDate` | Same business date as its Sale in the transaction | OQ11 payment policy |
| Ticket cancellation record | `BusinessDate`, nullable `CashierSessionId` | Session date when session-owned, otherwise operation date | OQ10 cancellation policy and Finance confirmation |
| Refund | `BusinessDate`, nullable `CashierSessionId` | Session date when session-owned, otherwise operation date | OQ10 refund policy and Finance confirmation |

The operation row and its business-date columns are written atomically. There is no polymorphic `BusinessDateAssignments` table. The table above is a documentation rule table, not a database table.

Reports group by the operation's stored `BusinessDate` and retain UTC operational timestamps for ordering.

## Blocked behaviour

- **OPEN QUESTION:** maximum cashier-session length and one-open-session policy.
- **OPEN QUESTION:** exact business-date source for no-session Finance refund operations.
- **OPEN QUESTION:** whether future operation types need business-date ownership.
- **2026-09-28 (T-063):** OQ2, OQ10 and OQ11, cited in the Decision table's "Blocked by" column, now have provisional tech-lead rulings in `docs/19-open-questions.md` (hein, 2026-09-28; T-062): every active station sells tickets (OQ2); sold tickets are final, with no cancellation and no refund in Phase 1 (OQ10); cash only in Phase 1 (OQ11). These rulings are **not Myanma Railways answers**; each is **still open with Myanma Railways**. The Decision text, its table and this ADR's status are unchanged; nothing is superseded.

## Consequences

Positive:
- Foreign keys and operation-local constraints remain possible.
- There is one authoritative business date per operation.
- Reports avoid a polymorphic join.

Negative:
- Every operation schema and migration must carry the relevant columns.
- Cross-module sale/payment workflows must copy the same business date atomically.

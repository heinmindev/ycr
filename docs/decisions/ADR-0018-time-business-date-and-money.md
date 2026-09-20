# ADR-0018: Time, Business Date and Money Representation

## Status

Accepted — 2026-09-19

## Supersedes

ADR-0011.

## Context

**FACT:** YCR operations need UTC ordering, Asia/Yangon calendar dates, and a business date that can remain attached to an open cashier session across local midnight.

**ENGINEERING DECISION (tech lead, cite ADR-0018):** use `DateTimeOffset` in application code and `datetimeoffset(3)` for stored instants.

**OPEN QUESTION:** MMK rounding and future currency-scale policy remain unresolved.

## Options considered

1. `DateTime`/`datetime2` UTC - familiar, but loses offset information at persistence boundaries.
2. `DateTimeOffset`/`datetimeoffset(3)` - preserves offset-aware values while allowing UTC operational ordering.
3. Local wall-clock timestamps - simpler for reports, but unsafe across services and audit ordering.

## Decision

### Time

- Instants use `DateTimeOffset` in code and `datetimeoffset(3)` in SQL columns, with UTC values for `*Utc` fields.
- Calendar dates use `DateOnly` mapped to SQL `date` for `TravelDate`, `BusinessDate`, and effective dates.
- The configured local zone is Asia/Yangon and is never hard-coded in domain logic.
- The clock is read only through `TimeProvider`.

### Business date

- A cashier session owns its business date from the Asia/Yangon local date at session opening.
- Business-date ownership and persistence are superseded by ADR-0019. The operation-owned columns and rule table in ADR-0019 are binding.
- Operations outside a cashier session receive the local date at operation time unless a future Finance decision changes that rule.
- Reports group by the assigned business date and retain operational UTC timestamps for ordering.

### Money

- `Money` remains `decimal(18,2)` amount plus uppercase ISO currency `char(3)`.
- Arithmetic requires matching currencies; mixed-currency operations are programming errors.
- Rounding is policy data, not hard-coded arithmetic.

## Blocked behaviour

- **OPEN QUESTION — OQ21:** MMK rounding and whole-kyat behavior.
- **OPEN QUESTION:** maximum session length and whether one operator may have more than one open session.
- **OPEN QUESTION:** Finance confirmation for no-session refund business dates.

## Consequences

Positive:
- Offset-aware persistence removes ambiguity when values cross process boundaries.
- Business-date assignment is auditable per operation rather than inferred later.
- Money retains explicit currency and precision.

Negative:
- Reports must distinguish `BusinessDate` from operational timestamps.
- Operation-owned business-date columns are written atomically with their operation according to ADR-0019.

## Partial supersession — 2026-09-19

The `Business date` persistence decision in this ADR is superseded by ADR-0019. The time and money sections remain in force.
- Currency and rounding policy still block complete fare/refund implementation.

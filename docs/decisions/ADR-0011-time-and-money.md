# ADR-0011: Time, Business Date and Money Representation

## Status

Superseded by ADR-0018

## Decision

### Time

- Instants are stored in UTC: `datetime2(3)` columns with the suffix `Utc` (`IssuedAtUtc`), and `DateTimeOffset` / `DateTime` (Kind=Utc) in code.
- Calendar dates are `DateOnly` → SQL `date`: `BusinessDate`, `TravelDate`, fare/timetable `EffectiveFrom/To`.
- Local zone: **Asia/Yangon (UTC+06:30, no DST)**, from configuration and never hard-coded in logic.
- The clock is only read through `TimeProvider` (built into .NET), so tests can control time.

### Business date

- A **cashier session owns its business date**. It is set to the Asia/Yangon local date when the session is opened. Every sale, cancellation and payment in that session gets that `BusinessDate`, even after local midnight.
- Operations outside a cashier session (e.g. a Finance refund approval) get the local date at the moment of the operation. BUSINESS DECISION to confirm with Finance.
- Reports group by `BusinessDate`. Operational timestamps (`*Utc`) are kept for audit and ordering (docs/14).
- ASSUMPTION to confirm: an operator can have at most one open cashier session (filtered unique index). The maximum session length is policy.

### Money

- `Money` value object = `Amount` (`decimal(18,2)`) + `Currency` (ISO 4217, `char(3)`, default `MMK`).
- Arithmetic is only allowed between amounts in the same currency. Mixing currencies is a programming error.
- The rounding rule is part of the **fare policy** (FareRuleSet), not code. OPEN QUESTION: how MMK fares are rounded.
- Never use `double`/`float` for money. EF configures precision explicitly for every money column.

## Consequences

- Reports and reconciliation have one unambiguous day boundary.
- Every money column takes two DB columns (amount + currency). This is accepted to avoid hidden currency assumptions.

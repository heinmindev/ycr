# ADR-0008: Counter Sales Are Online-Only in Phase 1

## Status

Superseded by ADR-0015

## Decision

- Selling, cancelling and refunding require a live connection to `YCR.Api`. There is no offline sale queue in Phase 1.
- The design keeps an offline mode possible later:
  - Client-generated `Idempotency-Key` on every financial command (see docs/20 §Idempotency). A counter can safely retry after a timeout without selling twice.
  - Application-generated GUID IDs (ADR-0006).
  - Fare quotes carry the `fareRuleVersion` they were calculated with.
- If the counter can't reach the server, the UI must say so clearly and must not show an ambiguous "maybe sold" state. After a timeout, the client retries with the **same** idempotency key and gets back either the original result or a definite failure.

## Risks (recorded, not solved)

- **ASSUMPTION:** stations have enough connectivity for online sales. If not, passengers can't buy tickets during outages.
- Whether a manual paper fallback (with sales entered afterwards) is allowed is a BUSINESS DECISION → OQ18.

## Consequences

- Much simpler Phase 1: no sync, no conflict resolution, no ticket-number block allocation.
- Availability of the API and network becomes directly customer-visible. Health checks, alerting and uptime targets (OQ15) matter from day one.

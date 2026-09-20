# ADR-0015: Online Counter Recovery and Session-Bound Idempotency

## Status

Accepted — 2026-09-19

## Supersedes

ADR-0008.

## Context

**FACT — ADR-0008:** Phase 1 counter sales, cancellations, and refunds require a live API connection.

**ASSUMPTION:** station connectivity is sufficient for online operations; paper/offline fallback remains BLOCKED by OQ18.

**ENGINEERING DECISION (tech lead, cite ADR-0015):** recovery is server-authoritative and does not depend on IndexedDB surviving a browser restart.

## Options considered

1. Online-only with no recovery view - simplest, but operators may create duplicate intents after lost responses.
2. Online-only with server-authoritative recovery and best-effort IndexedDB - preserves availability of recovery without treating browser storage as trusted.
3. Controlled offline/manual fallback - improves outage availability, but requires unresolved reconciliation and fraud policy.

## Decision

1. The primary recovery view is `GET /api/v1/cashier-sessions/current/recent-operations`, returning the last N sales, cancellations, and refunds in the caller's own open cashier session. After a lost response, the UI shows this list before allowing a new sale.
2. IndexedDB is best-effort only. It stores the idempotency key and non-personal request fields, reuses the same key on retry, clears data on resolution and logout, and never becomes the source of truth.
3. Idempotency records are written in the same transaction as the business change:
   - record exists: the operation committed and the stored response is replayed;
   - no record: the operation did not commit or rolled back, so same-key retry is safe;
   - in-flight duplicate: return `409 Idempotency.InProgress` using a lock/unique constraint, not a persisted pending state.
4. Store final 4xx business outcomes for replay. Never store 5xx responses.
5. `GET /api/v1/idempotency/{key}` is optional. If implemented, it is scoped to the authenticated caller's user and current cashier session and returns no other user's metadata.
6. Idempotency keys are bound to the cashier session. Retention is the maximum cashier-session length plus an operational margin. Retry after session close returns `409 Operations.SessionClosed`.
7. Closing a cashier session lists device-side unresolved intents and requires resolution before close.
8. Required tests cover crash before commit, crash after commit, server crash mid-transaction, concurrent tabs, cleared IndexedDB, session closure before retry, and another user's key/session lookup returning `403` or `404` as appropriate.
9. An outage runbook is required. Paper/offline fallback is not Phase 1 and remains BLOCKED by OQ18.

## Blocked behaviour

- **OPEN QUESTION — OQ18:** paper/offline fallback and later entry/reconciliation.
- **OPEN QUESTION — OQ15:** availability and peak-load targets.
- **ASSUMPTION:** station connectivity is sufficient for online operations; it must be validated by a survey.

## Consequences

Positive:
- Recovery remains possible even when browser-local state is cleared.
- Idempotency semantics follow the atomic transaction rather than a guessed pending status.
- Session scoping limits cross-operator key disclosure and stale retries.

Negative:
- The recent-operation query must be reliable during the exact failure mode that caused the lost response.
- IndexedDB still requires privacy and clear-on-logout tests.
- API/database availability is directly customer-visible until OQ18 is decided.

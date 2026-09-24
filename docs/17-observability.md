# Observability

Use structured logs and correlation IDs.

Important events:

- TicketIssued
- PaymentRecorded
- TicketValidated
- TicketCancelled
- SaleVoided
- RefundCreated
- RefundApproved
- RefundDisbursed
- RefundRejected
- PaymentReversed
- CashierOpened
- CashierClosed
- AuthLoginSucceeded
- AuthLoginFailed
- AuthRefreshSuperseded
- AuthRefreshFamilyRevoked
- LedgerDigestVerificationFailed

Metrics should include:

- Ticket issuance rate
- Validation rate
- Error rate
- Payment failures
- Refund failures
- Request latency
- Database latency
- Idempotency recovery operations
- Session/permission cache revocation latency
- QR authenticity failures by reason

Do not log secrets or authentication tokens.

## Implemented in F-002 — Identity metrics and log events

Defined in `src/YCR.Application/Identity/IdentityTelemetry.cs`. The `Auth*` names above are
log and metric names, not audit actions; the audit actions are `Identity.*` (ADR-0023 item 6,
`docs/09`).

### Log events

| EventId | Name | Level | Message | When |
|---|---|---|---|---|
| 2001 | `AuthLoginSucceeded` | Information | `Sign-in succeeded.` | A sign-in issued a session |
| 2002 | `AuthLoginFailed` | Information | `Sign-in failed.` | Every rejected sign-in: wrong password, unknown username, disabled or locked account |
| 2003 | `AuthRefreshSuperseded` | Information | `Refresh token presented again within the grace window for session {SessionId}; answered 409.` | The immediate predecessor within 20 seconds (not audited) |
| 2004 | `AuthRefreshFamilyRevoked` | Warning | `Refresh token reuse outside the grace window; session {SessionId} revoked.` | A reused refresh token revoked its session |

The only structured property is `SessionId`, an opaque id. No event carries a username, user id,
password, token, token hash or cookie (`docs/20` §7; F-002 spec R13, S28). A rejected sign-in
logs nothing that says why it failed; the reason is only in the audit row.

### Metrics

Published on the `System.Diagnostics.Metrics` meter **`YCR.Identity`**:

| Instrument | Type | Unit | Recorded when |
|---|---|---|---|
| `ycr.auth.login_succeeded` | counter | — | with `AuthLoginSucceeded` |
| `ycr.auth.login_failed` | counter | — | with `AuthLoginFailed` |
| `ycr.auth.refresh_superseded` | counter | — | with `AuthRefreshSuperseded` |
| `ycr.auth.refresh_family_revoked` | counter | — | with `AuthRefreshFamilyRevoked` |
| `ycr.auth.principal_cache.entry_age` | histogram | `s` | Each use of a cached principal: its age. This is the "session/permission cache revocation latency" metric above; it stays at or below `Auth:PrincipalCacheSeconds` (at most 30) |

The instruments carry no tags. **No metrics exporter or collector is configured yet**: the
observability stack is a hosting decision (`docs/15`), so until one is chosen the meter is visible
only to an in-process listener such as `dotnet-counters`.

Requests refused before a handler runs — `Auth.OriginRejected`, `Auth.TooManyRequests`,
`Auth.Unauthenticated`, `Auth.PasswordChangeRequired` — write no Identity log event, metric or
audit row.

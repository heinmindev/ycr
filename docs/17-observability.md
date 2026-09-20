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

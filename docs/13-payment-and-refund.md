# Payment and Refund

## Principles

Separate:

- Ticket state
- Sale state
- Payment state
- Refund state

Validation is append-only evidence (`TicketValidation`) and is not a Ticket state. Refunds are created against Sale/Payment, not as a financial operation on Ticket.

Phase 1 currently assumes cash only as an **ASSUMPTION — design default, business to confirm**. This is not an approved railway business decision. Pending/gateway payment states are out of scope until a gateway is approved.

Refund flow is `Requested -> Approved -> Disbursed` or `Requested -> Rejected`; eligibility, amount, and void/cancellation guards remain BLOCKED by OQ10 and related questions.

Financial operations require:

- Idempotency
- Transaction boundaries
- Unique business references
- Audit trail
- Operator identity
- Amount/currency recording
- Reason codes

Do not integrate a real payment gateway until the payment domain model and reconciliation rules are approved.

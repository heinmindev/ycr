# Payment and Refund

## Principles

Separate:

- Ticket state
- Sale state
- Payment state
- Refund state

Validation is append-only evidence (`TicketValidation`) and is not a Ticket state. Refunds are created against Sale/Payment, not as a financial operation on Ticket.

**Phase 1 scope (provisional tech-lead rulings, hein, 2026-09-28; T-062) — not Myanma Railways answers; still open with Myanma Railways:**

- **Payment is cash only in Phase 1** (`docs/19-open-questions.md` OQ11).
- **Cancel and Refund are out of Phase 1 scope.** Sold tickets are final: no cancellation and no refund in Phase 1 (OQ10). The refund flow below and the Cancel/Refund transitions in `docs/11-ticket-lifecycle.md` stay documented and are not deleted; they are not built in Phase 1.

Payment methods: see the OQ11 provisional tech-lead ruling in `docs/19-open-questions.md` (cash only in Phase 1; not a Myanma Railways answer; still open with Myanma Railways). Pending/gateway payment states are out of scope until a gateway is approved.

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

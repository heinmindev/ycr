# ADR-0013: Independent Ticket and Financial Lifecycles

## Status

Accepted — 2026-09-19

## Context

**FACT — docs/11 and docs/13:** ticket status was mixed with sale, payment, and refund state.

**ENGINEERING DECISION (tech lead, cite ADR-0013):** validation is an append-only record, not a Ticket state machine. “Used up” is derived from validation records and the repeat-use policy.

**OPEN QUESTION:** ticket unit, validity window, cancellation policy, refund eligibility/amount, repeat-use policy, and reprint invalidation remain blocked by OQ3, OQ10, OQ19, and the new reprint question in `docs/19-open-questions.md`.

## Options considered

1. One combined ticket/financial state machine - simpler diagram, but couples financial and ticket invariants.
2. Independent state sets plus append-only validation evidence - preserves domain separation and leaves business guards explicit.
3. Status fields without transitions - quickest to scaffold, but agents would invent allowed commands and guards.

## Decision

### State sets

These are the starting state sets. Guards marked `BLOCKED` must not be implemented as guessed railway rules.

| Entity | From | Command | Permission | Guard | To | Domain event | Audit action | Error code |
|---|---|---|---|---|---|---|---|---|
| Sale + Payment + Ticket | None | Sell | sales.sell | BLOCKED: ticket/fare/payment business rules | Completed + Recorded + Active | SaleCompleted, PaymentRecorded, TicketIssued | Payments.SaleCompleted, Payments.PaymentRecorded, Ticketing.TicketIssued | Payments.SaleNotSellable |
| Ticket | Active | Cancel | tickets.cancel | BLOCKED: cancellation policy | Cancelled | TicketCancelled | Ticketing.TicketCancelled | Ticketing.TicketNotCancellable |
| Sale | Completed | Void | sales.void | BLOCKED: void rules, including session ownership | Voided | SaleVoided | Payments.SaleVoided | Payments.SaleNotVoidable |
| Payment | Recorded | Reverse | payments.reverse | ASSUMPTION: cash-only is the current Phase 1 design; payment policy to confirm (OQ11) | Reversed | PaymentReversed | Payments.PaymentReversed | Payments.PaymentNotReversible |
| Refund | None | RequestRefund | refunds.request | BLOCKED: OQ10 eligibility and amount | Requested | RefundRequested | Payments.RefundRequested | Payments.RefundNotRequestable |
| Refund | Requested | Approve | refunds.approve | BLOCKED: OQ10 eligibility and amount | Approved | RefundApproved | Payments.RefundApproved | Payments.RefundNotApprovable |
| Refund | Approved | Disburse | refunds.disburse | BLOCKED: approved refund and disbursement rules | Disbursed | RefundDisbursed | Payments.RefundDisbursed | Payments.RefundNotDisbursable |
| Refund | Requested | Reject | refunds.reject | BLOCKED: OQ10 eligibility | Rejected | RefundRejected | Payments.RefundRejected | Payments.RefundNotRejectable |
| Existing Ticket | Existing | Reprint | tickets.reprint | BLOCKED: OQ23 reprint eligibility/invalidation | Unchanged | ReprintRecorded | Ticketing.ReprintRecorded | Ticketing.TicketNotReprintable |
| Ticket | Active | RefundApproved | refunds.approve | ASSUMPTION: engineering default, business to confirm | Cancelled | TicketCancelled | Ticketing.TicketCancelled | Ticketing.TicketNotCancellable |

**ENGINEERING DECISION (tech lead, cite ADR-0013):** a counter sale creates Sale, Payment, and Ticket in one transaction. There is no Draft state. Pending/gateway states may be introduced only if gateway payments are approved later.

**ENGINEERING DECISION (tech lead, cite ADR-0013):** expiry is derived as `now > ValidUntil`; it is not stored as a Ticket state and requires no Worker expiry job.

**ENGINEERING DECISION (tech lead, cite ADR-0013):** `TicketValidation` is append-only and records ticket, validator user, station, time, and result. Only `Valid` writes a validation record. `AuthenticUnverified` and `Invalid(reason)` do not write a successful validation record.

**OPEN QUESTION:** whether a ticket is accepted when its signature is authentic but usage is unverified is OQ8.

### Engineering defaults

These are engineering defaults, not invented railway policy; business confirmation remains required where stated.

- **ASSUMPTION — engineering default, business to confirm:** refund approver must differ from requester (maker-checker).
- **ASSUMPTION — engineering default:** Ticket has a `rowversion`; cancel and validation operations are serialized for the same ticket.
- **ASSUMPTION — engineering default, business to confirm:** approving a refund cancels the Ticket in the same transaction if it is not already cancelled.
- **ENGINEERING DECISION (tech lead, cite ADR-0013):** reprint creates a `ReprintEvent`, increments `PrintCount`, and emits an audit action; it does not change Ticket status.
- **OPEN QUESTION:** does a reprint invalidate earlier prints? If yes, signed QR payloads carry `printSequence` and validation accepts only the current sequence.

## Consequences

Positive:
- Financial history and ticket status remain separate.
- Expiry cannot drift because of a missed background job.
- Validation evidence is append-only and supports derived repeat-use checks.

Negative:
- Every transition needs explicit guards and error codes before implementation.
- Refund approval/cancellation requires one transaction across module contracts.
- Reprint behavior remains blocked until the business decision is recorded.

## Open questions — dated note 2026-09-28 (T-063)

**2026-09-28 (T-063):** open questions this ADR lists now have provisional tech-lead rulings in `docs/19-open-questions.md` (hein, 2026-09-28; T-062): **OQ3** — one ticket is one journey, not tied to a train or service; **OQ10** — sold tickets are final, no cancellation and no refund in Phase 1, and the Cancel and Refund transitions are out of Phase 1 scope, not deleted; **OQ11** — cash only in Phase 1; **OQ19** — a ticket is valid on the business date it was sold and is printed in English and Myanmar with Myanmar numerals, the printed layout staying open; **OQ23** — a reprint invalidates every earlier copy, and validation accepts only the current signed `printSequence`. These rulings are **not Myanma Railways answers**; each is **still open with Myanma Railways**. The Decision text, the guards in the state table and this ADR's status are unchanged; nothing is superseded. Reprint eligibility is OQ62, open with no provisional value.

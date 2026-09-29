# Ticket and Financial Lifecycle

## Status

This document follows ADR-0013. Business-dependent guards remain blocked until the referenced open questions are decided.

**Phase 1 scope (provisional tech-lead rulings, hein, 2026-09-28; T-062) — not Myanma Railways answers; still open with Myanma Railways:**

- **Cancel and Refund are out of Phase 1 scope.** Sold tickets are final: no cancellation and no refund in Phase 1 (`docs/19-open-questions.md` OQ10). The Cancel, RequestRefund, Approve, Disburse, Reject and RefundApproved transitions below stay documented and are not deleted; they are not built in Phase 1.
- **Payment is cash only in Phase 1** (OQ11).

## Independent records

Ticket status, Sale status, Payment status, Refund status, and validation evidence are separate. A refund is against a Sale/Payment, not against Ticket as a financial object.

## State transition table

| Entity | From | Command | Permission | Guard | To | Domain event | Audit action | Error code |
|---|---|---|---|---|---|---|---|---|
| Sale + Payment + Ticket | None | Sell | sales.sell | Phase 1 basis — provisional tech-lead rulings (hein, 2026-09-28; T-062), not Myanma Railways answers, still open with Myanma Railways: one ticket is one journey, not tied to a train or service (OQ3, OQ4); flat 800 MMK fare held as fare-table data, in whole kyat (OQ9, OQ17, OQ21); cash only (OQ11); valid on the business date it was sold (OQ19); one Sale may hold several Tickets, all for the same origin, destination and fare, paid as one cash amount (OQ24). Loop-length measurement and direction choice stay open (OQ17) | Completed + Recorded + Active | SaleCompleted, PaymentRecorded, TicketIssued | Payments.SaleCompleted, Payments.PaymentRecorded, Ticketing.TicketIssued | Payments.SaleNotSellable |
| Ticket | Active | Cancel | tickets.cancel | Out of Phase 1 scope (OQ10 provisional tech-lead ruling, hein, 2026-09-28; T-062: sold tickets are final; still open with Myanma Railways); kept for later; cancellation policy awaits an official Myanma Railways answer | Cancelled | TicketCancelled | Ticketing.TicketCancelled | Ticketing.TicketNotCancellable |
| Sale | Completed | Void | sales.void | BLOCKED: void rules and cashier-session ownership | Voided | SaleVoided | Payments.SaleVoided | Payments.SaleNotVoidable |
| Payment | Recorded | Reverse | payments.reverse | Cash only in Phase 1 — OQ11 provisional tech-lead ruling (hein, 2026-09-28; T-062), still open with Myanma Railways | Reversed | PaymentReversed | Payments.PaymentReversed | Payments.PaymentNotReversible |
| Refund | None | RequestRefund | refunds.request | Out of Phase 1 scope (OQ10 provisional tech-lead ruling, hein, 2026-09-28; T-062: sold tickets are final); kept for later; eligibility and amount await an official Myanma Railways answer | Requested | RefundRequested | Payments.RefundRequested | Payments.RefundNotRequestable |
| Refund | Requested | Approve | refunds.approve | Out of Phase 1 scope (OQ10 provisional tech-lead ruling, hein, 2026-09-28; T-062: sold tickets are final); kept for later; eligibility and amount await an official Myanma Railways answer | Approved | RefundApproved | Payments.RefundApproved | Payments.RefundNotApprovable |
| Refund | Approved | Disburse | refunds.disburse | Out of Phase 1 scope (OQ10 provisional tech-lead ruling, hein, 2026-09-28; T-062: sold tickets are final); kept for later; eligibility, amount and disbursement policy await an official Myanma Railways answer | Disbursed | RefundDisbursed | Payments.RefundDisbursed | Payments.RefundNotDisbursable |
| Refund | Requested | Reject | refunds.reject | Out of Phase 1 scope (OQ10 provisional tech-lead ruling, hein, 2026-09-28; T-062: sold tickets are final); kept for later; eligibility and amount await an official Myanma Railways answer | Rejected | RefundRejected | Payments.RefundRejected | Payments.RefundNotRejectable |
| Existing Ticket | Existing | Reprint | tickets.reprint | Invalidation: OQ23 provisional tech-lead ruling (hein, 2026-09-28; T-062), still open with Myanma Railways — a reprint invalidates every earlier copy. Reprint eligibility: no rule yet (follow-up in T-063) | Unchanged | ReprintRecorded | Ticketing.ReprintRecorded | Ticketing.TicketNotReprintable |
| Ticket | Active | RefundApproved | refunds.approve | Out of Phase 1 scope (OQ10 provisional tech-lead ruling, hein, 2026-09-28; T-062: sold tickets are final; still open with Myanma Railways); kept for later. For the later flow — ASSUMPTION: engineering default, business to confirm | Cancelled | TicketCancelled | Ticketing.TicketCancelled | Ticketing.TicketNotCancellable |

## Derived and append-only behavior

- **ENGINEERING DECISION (tech lead, cite ADR-0013):** a counter sale creates Sale + Payment + Ticket in one transaction. There is no Draft state.
- **ENGINEERING DECISION (tech lead, cite ADR-0013):** expiry is derived as `now > ValidUntil`; no stored Expired state and no Worker expiry job are required.
- **ENGINEERING DECISION (tech lead, cite ADR-0013):** `TicketValidation` is an append-only record containing ticket, validator user, station, time, and result. Only `Valid` writes a successful validation record. “Used up” is derived from validation records plus the repeat-use policy. OQ3 and OQ19 now have provisional tech-lead rulings (hein, 2026-09-28; T-062), still open with Myanma Railways: one ticket is one journey, valid on the business date it was sold. Because “Used up” still depends on how validation is performed, it remains BLOCKED by OQ7.
- **ENGINEERING DECISION (tech lead, cite ADR-0013):** reprinting emits a `ReprintEvent`, increments `PrintCount`, and writes an audit record without changing Ticket status.
- **Provisional tech-lead ruling (hein, 2026-09-28; T-062) — OQ23, not a Myanma Railways answer, still open with Myanma Railways:** reprinting invalidates every earlier copy, so signed QR validation accepts only the current `printSequence` (ADR-0013/ADR-0014).

## Engineering defaults

- **ASSUMPTION — engineering default, business to confirm:** refund requester and approver are different users.
- **ASSUMPTION — engineering default:** Ticket has a rowversion; cancel and validation for one Ticket are serialized.
- **ASSUMPTION — engineering default, business to confirm:** refund approval cancels the Ticket in the same transaction if it is not already cancelled.

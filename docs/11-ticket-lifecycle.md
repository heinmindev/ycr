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
| Sale + Payment + Ticket | None | Sell | sales.sell | BLOCKED: ticket/fare/payment business rules | Completed + Recorded + Active | SaleCompleted, PaymentRecorded, TicketIssued | Payments.SaleCompleted, Payments.PaymentRecorded, Ticketing.TicketIssued | Payments.SaleNotSellable |
| Ticket | Active | Cancel | tickets.cancel | BLOCKED: cancellation policy | Cancelled | TicketCancelled | Ticketing.TicketCancelled | Ticketing.TicketNotCancellable |
| Sale | Completed | Void | sales.void | BLOCKED: void rules and cashier-session ownership | Voided | SaleVoided | Payments.SaleVoided | Payments.SaleNotVoidable |
| Payment | Recorded | Reverse | payments.reverse | ASSUMPTION: cash-only is the current Phase 1 design; payment policy to confirm (OQ11) | Reversed | PaymentReversed | Payments.PaymentReversed | Payments.PaymentNotReversible |
| Refund | None | RequestRefund | refunds.request | BLOCKED: OQ10 eligibility and amount | Requested | RefundRequested | Payments.RefundRequested | Payments.RefundNotRequestable |
| Refund | Requested | Approve | refunds.approve | BLOCKED: OQ10 | Approved | RefundApproved | Payments.RefundApproved | Payments.RefundNotApprovable |
| Refund | Approved | Disburse | refunds.disburse | BLOCKED: refund disbursement policy | Disbursed | RefundDisbursed | Payments.RefundDisbursed | Payments.RefundNotDisbursable |
| Refund | Requested | Reject | refunds.reject | BLOCKED: OQ10 | Rejected | RefundRejected | Payments.RefundRejected | Payments.RefundNotRejectable |
| Existing Ticket | Existing | Reprint | tickets.reprint | BLOCKED: OQ23 reprint eligibility/invalidation | Unchanged | ReprintRecorded | Ticketing.ReprintRecorded | Ticketing.TicketNotReprintable |
| Ticket | Active | RefundApproved | refunds.approve | ASSUMPTION: engineering default, business to confirm | Cancelled | TicketCancelled | Ticketing.TicketCancelled | Ticketing.TicketNotCancellable |

## Derived and append-only behavior

- **ENGINEERING DECISION (tech lead, cite ADR-0013):** a counter sale creates Sale + Payment + Ticket in one transaction. There is no Draft state.
- **ENGINEERING DECISION (tech lead, cite ADR-0013):** expiry is derived as `now > ValidUntil`; no stored Expired state and no Worker expiry job are required.
- **ENGINEERING DECISION (tech lead, cite ADR-0013):** `TicketValidation` is an append-only record containing ticket, validator user, station, time, and result. Only `Valid` writes a successful validation record. “Used up” is derived from validation records plus the repeat-use policy, which is BLOCKED by OQ3/OQ19.
- **ENGINEERING DECISION (tech lead, cite ADR-0013):** reprinting emits a `ReprintEvent`, increments `PrintCount`, and writes an audit record without changing Ticket status.
- **OPEN QUESTION:** whether reprinting invalidates earlier prints. If yes, signed QR validation must accept only the current `printSequence`.

## Engineering defaults

- **ASSUMPTION — engineering default, business to confirm:** refund requester and approver are different users.
- **ASSUMPTION — engineering default:** Ticket has a rowversion; cancel and validation for one Ticket are serialized.
- **ASSUMPTION — engineering default, business to confirm:** refund approval cancels the Ticket in the same transaction if it is not already cancelled.

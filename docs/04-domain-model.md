# Domain Model

## Candidate aggregates

- Station
- Route
- Service
- ScheduleVersion
- FareRuleSet
- Ticket
- Sale
- Payment
- Refund
- CashierSession

## Candidate value objects

- StationCode
- TicketNumber
- Money
- TravelDate
- RouteSegment
- PassengerCategory
- TicketValidation
- ReprintEvent

## Candidate domain services

- FareCalculator
- TicketValidator
- TicketNumberGenerator
- RefundCalculator
- RouteSegmentResolver

`TicketValidation` is append-only evidence, not a Ticket state machine. Expiry is derived from `ValidUntil`; it is not a stored lifecycle state.

**Naming correction (hein, 2026-09-21).** This document previously called it `TicketValidationRecord`. The canonical name is `TicketValidation`: that is what Accepted ADR-0013 (line 44), ADR-0014 (line 64) and `docs/11-ticket-lifecycle.md` (line 30) use, and only this file disagreed. No superseding ADR was required, because the ADRs already agreed with each other. See `docs/glossary.md`.

## Candidate domain events

- TicketIssued
- TicketValidated
- TicketCancelled
- SaleVoided
- PaymentRecorded
- PaymentReversed
- RefundCreated
- RefundApproved
- RefundDisbursed
- RefundRejected
- ReprintRecorded
- SchedulePublished
- FareRulePublished

These are proposals. Confirm aggregate boundaries through discovery.

## Boundary clarifications

**ENGINEERING DECISION (tech lead, cite ADR-0012):** `Sale` belongs to the Payments boundary, while `Ticket` belongs to Ticketing. Cross-module operations use application Contracts and identifiers, not navigation properties.

**ENGINEERING DECISION (tech lead, cite ADR-0013):** `TicketValidation` is append-only evidence and is not a Ticket state. `ScheduleVersion` remains a Timetable aggregate; Ticketing does not take a direct navigation dependency on it.

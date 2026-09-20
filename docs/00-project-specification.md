# Project Specification

## Objective

Provide a reliable ticketing platform for Yangon Circular Railway operations.

## Initial scope

- Station management
- Railway line/route management
- Train and service management
- Timetable management
- Fare management
- Ticket issuance
- Ticket validation
- Cancellation
- Refund
- Cashier/session operations
- Payment records
- Audit
- Reporting
- Administration

## Out of scope until explicitly approved

- Automatic fare assumptions
- Real-time train tracking
- National identity integration
- Bank/payment gateway integration
- Smart-card clearing
- Offline validator synchronization

These may become later bounded contexts.

## Product principles

- Correctness over convenience
- Auditability
- Configurable policy
- Idempotent financial operations
- Secure by default
- Observable production behavior

# Bounded Contexts

Initial proposal:

1. Infrastructure/Identity
2. Station & Network
3. Service & Timetable
4. Fare
5. Ticketing
6. Payment & Refund
7. Operations
8. Reporting
9. Audit

Do not create distributed microservices initially. Keep these as modular boundaries inside a deployable modular monolith unless scale or organizational requirements justify service extraction.

## Context-to-module map

| Original context name | Canonical module name | Notes |
|---|---|---|
| Infrastructure/Identity | `Identity` | Shared identity and authorization infrastructure |
| Station & Network | `Network` | Stations, routes, stable station indices |
| Service & Timetable | `Timetable` | Services, schedules, published timetable versions |
| Fare | `Fare` | Versioned fare policy and quotes |
| Ticketing | `Ticketing` | Tickets, validation evidence, reprints |
| Payment & Refund | `Payments` | Sales, payments, refunds, reversals |
| Operations | `Operations` | Cashier sessions and operational controls |
| Reporting | `Reporting` | Read-only projections and reports |
| Audit | `Audit` | Audit events and ledger integration |

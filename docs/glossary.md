# Yangon Circular Railway Glossary

## Purpose and authority

This is the canonical vocabulary for YCR product, domain, API, data, and documentation work. Use the English terms in the **Canonical English** column exactly, including the code identifier where one is shown.

The requested source documents do not provide authoritative Myanmar-script translations. Every missing Myanmar term is therefore recorded as **OPEN QUESTION - authoritative term not supplied**. No agent may invent a Myanmar translation. A proposed translation must be supplied by the appropriate Myanma Railways language or business owner and then reviewed into this glossary.

Accepted ADRs are binding. Superseded ADRs are retained as historical context only; their terms do not override the current ADR listed in the source column. Business-dependent meanings remain OPEN QUESTION where the source says so.

The **Forbidden synonyms** column applies when referring to the exact canonical concept in that row. A listed word may still be used for a genuinely different concept.

### Source key

- **03** - `docs/03-use-cases.md`
- **04** - `docs/04-domain-model.md`
- **05** - `docs/05-bounded-contexts.md`
- **10** - `docs/10-authorization-matrix.md`
- **11** - `docs/11-ticket-lifecycle.md`
- **20** - `docs/20-coding-conventions.md`
- **ADR-0001 through ADR-0021** - `docs/decisions/`

## Architecture and domain language

| Canonical English | Myanmar term | Meaning and source | Forbidden synonyms |
|---|---|---|---|
| Yangon Circular Railway (YCR) | OPEN QUESTION - authoritative term not supplied | The railway platform and operating domain. | Yangon Circular Rail, YCR system (when the railway itself is meant) |
| Bounded context | OPEN QUESTION - authoritative term not supplied | A domain boundary used to organize language and ownership. **05**, ADR-0001. | Service (unless a deployable service is actually meant), subsystem |
| Module | OPEN QUESTION - authoritative term not supplied | A bounded-context folder and application boundary inside the modular monolith. **05**, ADR-0012. | Microservice, component (when the module boundary is meant) |
| Modular monolith | OPEN QUESTION - authoritative term not supplied | One deployable application with explicit module boundaries. ADR-0001. | Distributed monolith, microservices |
| Aggregate | OPEN QUESTION - authoritative term not supplied | A consistency boundary such as `Station`, `Ticket`, `Sale`, or `CashierSession`. **04**, ADR-0013. | Record, table, entity (when the consistency boundary is meant) |
| Value object | OPEN QUESTION - authoritative term not supplied | An immutable domain value such as `StationCode` or `Money`, identified by its value rather than a database identity. **04**, ADR-0006. | Entity, DTO |
| Domain service | OPEN QUESTION - authoritative term not supplied | Domain logic that does not belong to one aggregate, such as `FareCalculator`. **04**. | Utility, helper |
| Domain event | OPEN QUESTION - authoritative term not supplied | A past-tense business event such as `TicketIssued` or `PaymentRecorded`. **04**, ADR-0017. | Log entry, notification |
| Application Contract | OPEN QUESTION - authoritative term not supplied | The explicit cross-module application interface used instead of another module's context or domain namespace. ADR-0012. | Shared repository, direct module call |
| Persistence boundary | OPEN QUESTION - authoritative term not supplied | The module-scoped context interface exposing only that module's data surface. ADR-0012. | Database boundary, repository |
| Reporting read context | OPEN QUESTION - authoritative term not supplied | `IReportingReadContext`, a read-only projection/query surface with no write context. ADR-0012. | Reporting database, reporting repository |
| Handler | OPEN QUESTION - authoritative term not supplied | One application handler per use case; it coordinates validation, domain behavior, persistence, and results. ADR-0004. | Controller, service (when a use-case handler is meant), repository |
| Result | OPEN QUESTION - authoritative term not supplied | The explicit success/error return value used for expected business and validation outcomes. ADR-0004. | Exception, HTTP response |
| Data transfer object (DTO) | OPEN QUESTION - authoritative term not supplied | An API request or response shape; it is not an EF entity. **20**, ADR-0004. | Entity, model (when an API contract is meant) |
| ProblemDetails | OPEN QUESTION - authoritative term not supplied | RFC 9457 error response with `errorCode` and `traceId`. ADR-0004, **20**. | Error JSON, exception payload |

## Canonical module names

Use these names for namespaces, schemas, ownership boundaries, and module references. The historical context names from **05** are not module names.

| Canonical English | Myanmar term | Meaning and source | Forbidden synonyms |
|---|---|---|---|
| Identity | OPEN QUESTION - authoritative term not supplied | Identity and authorization infrastructure module. **05**, ADR-0012. | Infrastructure/Identity, Auth module |
| Network | OPEN QUESTION - authoritative term not supplied | Stations, routes, and stable station indices. **05**, ADR-0012. | Station & Network, Stations module |
| Timetable | OPEN QUESTION - authoritative term not supplied | Services, schedules, and published timetable versions. **05**, ADR-0012. | Service & Timetable, Schedule module |
| Fare | OPEN QUESTION - authoritative term not supplied | Versioned fare policy and quotes. **05**, ADR-0002/0012. | Fares, Pricing |
| Ticketing | OPEN QUESTION - authoritative term not supplied | Tickets, validation evidence, and reprints. **05**, ADR-0012/0013. | Ticket, Tickets module |
| Payments | OPEN QUESTION - authoritative term not supplied | Sales, payments, refunds, and reversals. **05**, ADR-0012/0013. | Payment & Refund, Payment module |
| Operations | OPEN QUESTION - authoritative term not supplied | Cashier sessions and operational controls. **05**, ADR-0012/0015. | Operation, Cashier module |
| Reporting | OPEN QUESTION - authoritative term not supplied | Read-only projections and reports. **05**, ADR-0012. | Reports, Analytics |
| Audit | OPEN QUESTION - authoritative term not supplied | Audit events and SQL Server ledger integration. **05**, ADR-0017/0021. | Audit Log, Logging |

## Actor and role labels

These are canonical labels for the proposed actors named in **03**. Their inclusion does not approve the role model or any role-to-permission grant; those remain **OPEN QUESTION** under OQ12 and OQ28. The shorter labels in the proposal table in **10** are not canonical outside compact table headings.

| Canonical English | Myanmar term | Meaning and source | Forbidden synonyms |
|---|---|---|---|
| System Administrator | OPEN QUESTION - authoritative term not supplied | Proposed actor responsible for system administration. **03**, **10** proposal only. | Admin, System Admin |
| Railway Administrator | OPEN QUESTION - authoritative term not supplied | Proposed actor responsible for railway administration. **03**, **10** proposal only. | Railway Admin, Admin |
| Station Manager | OPEN QUESTION - authoritative term not supplied | Proposed station-management actor. **03**, **10** proposal only. | Station Admin, Manager |
| Ticket Operator | OPEN QUESTION - authoritative term not supplied | Proposed ticket-selling/operator actor. **03**, **10** proposal only. | Operator, Cashier |
| Ticket Inspector | OPEN QUESTION - authoritative term not supplied | Proposed ticket-validation/inspection actor. **03**, **10** proposal only. | Inspector, Validator |
| Finance Officer | OPEN QUESTION - authoritative term not supplied | Proposed finance actor. **03**, **10** proposal only. | Finance, Accountant |
| Auditor | OPEN QUESTION - authoritative term not supplied | Proposed audit-review actor. **03**, **10** proposal only. | Audit user, Reviewer |
| Reporting User | OPEN QUESTION - authoritative term not supplied | Proposed reporting actor. **03**. | Report user, Analyst |

## Network, timetable, and fares

| Canonical English | Myanmar term | Meaning and source | Forbidden synonyms |
|---|---|---|---|
| Station | OPEN QUESTION - authoritative term not supplied | A railway station aggregate in the `Network` module. **03**, **04**, **05**. | Stop, location (when a railway station is meant) |
| StationCode | OPEN QUESTION - authoritative term not supplied | The human-facing station identifier value object. Its format remains an open business question (OQ26). ADR-0006, F-001 provisional rule. | Station ID, station number, short name |
| Station index | OPEN QUESTION - authoritative term not supplied | A stable short numeric index used in the signed QR mapping; it is distinct from `StationCode`. ADR-0014. | Station code, database ID |
| Route | OPEN QUESTION - authoritative term not supplied | An ordered station sequence used for network and fare resolution. **04**, **05**. | Service, line (unless an approved railway term is intended) |
| Route segment | OPEN QUESTION - authoritative term not supplied | A resolved portion of a route used by fare calculation. **04**, `docs/12-fare-engine.md`. | Leg, trip segment (unless the product definition says so) |
| Service | OPEN QUESTION - authoritative term not supplied | A timetable/service aggregate distinct from a physical train. **04**, **05**. | Train, route |
| ScheduleVersion | OPEN QUESTION - authoritative term not supplied | A versioned timetable aggregate that can be published and then treated as immutable. **04**, ADR-0002 pattern. | Schedule (when a version is meant), service |
| FareRuleSet | OPEN QUESTION - authoritative term not supplied | A versioned set of fare policy data with an effective period. ADR-0002, `docs/12-fare-engine.md`. | Fare, price list, tariff (unless MR formally adopts that term) |
| Fare quote | OPEN QUESTION - authoritative term not supplied | A calculated fare result for specified journey/product inputs before a sale. `docs/12-fare-engine.md`. | Fare rule, ticket price (when the calculation result is meant) |
| FareCalculator | OPEN QUESTION - authoritative term not supplied | Domain service that calculates a fare after route resolution and rule selection. **04**, `docs/12-fare-engine.md`. | Pricing service, fare table |
| RouteSegmentResolver | OPEN QUESTION - authoritative term not supplied | Domain service that resolves route segments before fare calculation. **04**, `docs/12-fare-engine.md`. | Route calculator, path finder |
| PassengerCategory | OPEN QUESTION - authoritative term not supplied | A fare input representing the passenger category; the category catalogue remains an open business question (OQ9). **04**, `docs/12-fare-engine.md`. | Customer type, user role |

## Tickets and validation

| Canonical English | Myanmar term | Meaning and source | Forbidden synonyms |
|---|---|---|---|
| Ticket | OPEN QUESTION - authoritative term not supplied | The ticketing aggregate representing the passenger entitlement; its product, validity, and repeat-use rules remain business questions. **03**, **04**, **11**, ADR-0013. | Sale, receipt, booking, journey |
| TicketNumber | OPEN QUESTION - authoritative term not supplied | A human-facing ticket identifier, separate from the GUID resource ID. ADR-0006. | Ticket ID, QR ID |
| Ticket validation | OPEN QUESTION - authoritative term not supplied | The act of checking a ticket credential and applying the approved validation policy. **03**, **11**, ADR-0013/0014. | Ticket status change, ticket use |
| TicketValidationRecord | OPEN QUESTION - authoritative term not supplied | Append-only evidence containing ticket, validator, station, time, and result; it is not a Ticket state. **04**, **11**, ADR-0013. | Used flag, ticket status, validation log |
| TicketValidator | OPEN QUESTION - authoritative term not supplied | Domain service responsible for ticket validation behavior. **04**. | Inspector service, scanner |
| ReprintEvent | OPEN QUESTION - authoritative term not supplied | An append-only record of a ticket reprint; reprinting does not change Ticket status. **04**, **11**, ADR-0013. | Replacement ticket, ticket renewal |
| PrintSequence | OPEN QUESTION - authoritative term not supplied | The signed sequence value associated with a printed ticket; whether an earlier sequence is invalidated remains OQ23. ADR-0014. | Print count, ticket version |
| Valid | OPEN QUESTION - authoritative term not supplied | A validation result that writes a successful `TicketValidationRecord`. ADR-0014. | Accepted (unless the UI label is explicitly defined), authentic |
| Invalid | OPEN QUESTION - authoritative term not supplied | A validation result with a reason that does not write a successful validation record. ADR-0014. | Rejected ticket (unless explaining the result to a passenger) |
| AuthenticUnverified | OPEN QUESTION - authoritative term not supplied | A signature-authentic result whose usage state cannot be verified; admission during network loss is blocked by OQ8. ADR-0014. | Valid, accepted, used |
| Signed ticket QR | OPEN QUESTION - authoritative term not supplied | The fixed-binary, ECDSA P-256 ticket credential encoded with `YCR1:` and Base45. ADR-0014. | QR token, barcode payload, online ticket token |
| `kid` (key ID) | OPEN QUESTION - authoritative term not supplied | The signing-key identifier carried in the signed QR header. ADR-0014. | Ticket ID, key name |
| Trust list | OPEN QUESTION - authoritative term not supplied | The authenticated public-key list cached by inspector devices for offline signature checks. ADR-0014. | Allowlist (unless a security design explicitly means an allowlist) |
| Base45 | OPEN QUESTION - authoritative term not supplied | The RFC 9285 text encoding used after the `YCR1:` prefix. ADR-0014. | Base64, QR format |
| Validity window | OPEN QUESTION - authoritative term not supplied | The business-defined period in which a ticket may be used; its policy is OQ19. ADR-0013/0014. | Expiry date (unless referring to a concrete field), service time |
| Expired | OPEN QUESTION - authoritative term not supplied | A derived condition (`now > ValidUntil`), not a stored Ticket lifecycle state. ADR-0013. | Ticket status, expiry state |

## Sales, payments, refunds, and operations

| Canonical English | Myanmar term | Meaning and source | Forbidden synonyms |
|---|---|---|---|
| Sale | OPEN QUESTION - authoritative term not supplied | The Payments aggregate for a completed counter sale; it creates Sale, Payment, and Ticket in one transaction. **03**, **04**, **11**, ADR-0013. | Order, booking, invoice, receipt |
| Payment | OPEN QUESTION - authoritative term not supplied | The financial record of money received for a Sale; it has its own lifecycle. **04**, **11**, ADR-0013. | Sale, fare, receipt |
| Payment reversal | OPEN QUESTION - authoritative term not supplied | The lifecycle operation that moves a recorded Payment to Reversed. **11**, ADR-0013. | Refund, cancellation, chargeback |
| Refund | OPEN QUESTION - authoritative term not supplied | A financial record against a Sale/Payment, with request, approval, disbursement, or rejection states. **04**, **11**, ADR-0013. | Ticket cancellation, reversal, reimbursement (unless MR approves it) |
| Refund disbursement | OPEN QUESTION - authoritative term not supplied | The operation that pays an approved Refund. **11**, ADR-0013. | Refund approval, payment reversal |
| CashierSession | OPEN QUESTION - authoritative term not supplied | An Operations aggregate representing an operator's open/closed cashier session and its session-bound operations. **03**, **04**, ADR-0015. | Shift, till session, workday |
| BusinessDate | OPEN QUESTION - authoritative term not supplied | The operation-owned calendar date used for reporting/reconciliation; it is distinct from the UTC operational instant. ADR-0018/0019. | Transaction date, issue timestamp, local time |
| Cancellation | OPEN QUESTION - authoritative term not supplied | The Ticket lifecycle operation that moves an eligible Ticket to Cancelled; eligibility remains OQ10. **11**, ADR-0013. | Refund, void, deletion |
| Void | OPEN QUESTION - authoritative term not supplied | The Sale lifecycle operation that moves a Completed Sale to Voided; void policy remains open. **11**, ADR-0013. | Cancel ticket, delete sale, refund |
| Reconciliation | OPEN QUESTION - authoritative term not supplied | The operational/financial comparison of recorded activity and expected totals. **03**, ADR-0015, ADR-0019. | Settlement (unless a payment-provider settlement is meant), report |
| Idempotency-Key | OPEN QUESTION - authoritative term not supplied | A client-generated UUID reused for retries of one financial or retryable command. **20**, ADR-0015. | Request ID, correlation ID, transaction ID |
| IdempotencyRecord | OPEN QUESTION - authoritative term not supplied | The server record atomically saved with the business change to replay a committed result or store a final 4xx outcome. **20**, ADR-0015. | Pending request, retry log, cache entry |
| Recent operations | OPEN QUESTION - authoritative term not supplied | The server-authoritative recovery view for the caller's current cashier session. ADR-0015. | Browser history, local transaction log |

## Identity, authentication, and authorization

| Canonical English | Myanmar term | Meaning and source | Forbidden synonyms |
|---|---|---|---|
| Authentication | OPEN QUESTION - authoritative term not supplied | Establishing the identity of the caller. ADR-0016, ADR-0020. | Authorization, permission check |
| Authorization | OPEN QUESTION - authoritative term not supplied | Deciding whether an authenticated caller may perform an action. **10**, ADR-0016/0020. | Authentication, role login |
| Role | OPEN QUESTION - authoritative term not supplied | A named organizational access grouping such as `Station Manager` or `Auditor`; role-to-permission grants remain open. **03**, **10**, OQ12/OQ28. | Permission, user type, passenger category |
| Permission | OPEN QUESTION - authoritative term not supplied | A machine-readable capability such as `stations.manage` or `tickets.validate`. **10**, **20**, ADR-0020. | Role, claim (JWT claims are not the permission authority) |
| Access token | OPEN QUESTION - authoritative term not supplied | A short-lived JWT containing only `sub` and `sid`; it carries no permission claims. ADR-0016. | Refresh token, API key, session cookie |
| Refresh token | OPEN QUESTION - authoritative term not supplied | A random token stored hashed and rotated through the refresh cookie flow. ADR-0016. | Access token, password, API key |
| AuthSession | OPEN QUESTION - authoritative term not supplied | The server-side session record used for revocation and per-request permission resolution. ADR-0016. | Login, access token, cashier session |
| Test authentication handler | OPEN QUESTION - authoritative term not supplied | A test-only handler registered through `WebApplicationFactory`; it must never ship in `src/`. ADR-0020. | Mock production authentication, bypass, development login |
| Origin check | OPEN QUESTION - authoritative term not supplied | Validation of the request `Origin` on cookie-bearing endpoints. ADR-0016, `docs/18-threat-model.md`. | CORS check, CSRF token |
| Cross-site request forgery (CSRF) | OPEN QUESTION - authoritative term not supplied | A threat controlled by same-origin deployment, `SameSite=Strict`, and Origin checks. ADR-0016. | XSS, CORS |
| Content Security Policy (CSP) | OPEN QUESTION - authoritative term not supplied | Browser policy requiring, among other controls, `script-src 'self'` and no `unsafe-inline`/`unsafe-eval`. ADR-0016, `docs/18-threat-model.md`. | CORS policy, CSRF policy |

## Audit, records, and observability

| Canonical English | Myanmar term | Meaning and source | Forbidden synonyms |
|---|---|---|---|
| Audit event | OPEN QUESTION - authoritative term not supplied | An append-only, business-significant record written through the audit boundary. ADR-0017/0021. | Log entry, metric, domain event (unless the domain event is separately meant) |
| Audit action | OPEN QUESTION - authoritative term not supplied | The `<Module>.<Event>` name describing an audit event, such as `Network.StationCreated`. **20**, ADR-0021. | Permission, endpoint name |
| Actor | OPEN QUESTION - authoritative term not supplied | The server-derived authenticated user and roles responsible for an audit event. ADR-0017/0021. | Request user, client-supplied user |
| Subject | OPEN QUESTION - authoritative term not supplied | The aggregate/entity/concept an audit event concerns; `SubjectId` may be null for subject-less events. ADR-0021. | Actor, resource ID |
| Correlation ID | OPEN QUESTION - authoritative term not supplied | The request correlation value from `traceparent`. **20**, ADR-0021. | Audit ID, idempotency key, trace ID (unless the exact tracing field is meant) |
| BeforeJson / AfterJson | OPEN QUESTION - authoritative term not supplied | Versioned prior/result state payloads in an audit event; they must not contain secrets, tokens, full QR payloads, or private keys. ADR-0021. | Snapshot, log message, request body |
| PayloadVersion | OPEN QUESTION - authoritative term not supplied | The schema version for audit state payloads, incremented rather than rewriting old ledger rows. ADR-0021. | API version, database migration version |
| Audit ledger | OPEN QUESTION - authoritative term not supplied | The SQL Server 2022 append-only ledger table `audit.AuditEvents`. ADR-0017/0021. | Audit log, ordinary audit table |
| Ledger digest | OPEN QUESTION - authoritative term not supplied | An externally protected digest used to detect alteration of committed ledger history. ADR-0017. | Backup, audit event, encryption key |
| Traceparent | OPEN QUESTION - authoritative term not supplied | The request header/source for the correlation ID. **20**, ADR-0021. | Correlation ID (the header and value are related but not identical) |

## Time and money

| Canonical English | Myanmar term | Meaning and source | Forbidden synonyms |
|---|---|---|---|
| Instant | OPEN QUESTION - authoritative term not supplied | An offset-aware point in time represented by `DateTimeOffset` and stored as `datetimeoffset(3)`. ADR-0018. | Date, local time, timestamp (when a calendar date is meant) |
| Calendar date | OPEN QUESTION - authoritative term not supplied | A date represented by `DateOnly` and stored as SQL `date`. ADR-0018. | Instant, timestamp |
| TravelDate | OPEN QUESTION - authoritative term not supplied | The calendar date associated with travel; it is not an issuance instant. ADR-0018, **04**. | Departure timestamp, issue date |
| Asia/Yangon local date | OPEN QUESTION - authoritative term not supplied | The configured local calendar date used to derive business dates; it is not hard-coded into domain logic. ADR-0018. | Server date, UTC date |
| Money | OPEN QUESTION - authoritative term not supplied | `decimal(18,2)` amount plus uppercase ISO currency `char(3)`; arithmetic requires matching currencies. ADR-0018. | Amount, price, integer kyat |
| Currency | OPEN QUESTION - authoritative term not supplied | The uppercase ISO `char(3)` code carried with `Money`, such as `MMK`; future scale/rounding policy remains open. ADR-0018. | Currency symbol, amount |
| Rounding policy | OPEN QUESTION - authoritative term not supplied | Policy data that determines money rounding; MMK whole-kyat behavior is OQ21. ADR-0018. | Format, truncation, hard-coded arithmetic |
| TimeProvider | OPEN QUESTION - authoritative term not supplied | The injected clock abstraction used to read time. **20**, ADR-0018. | `DateTime.Now`, system clock (as an application dependency) |

## Maintenance rules

1. Use the canonical English term in code, API documentation, database documentation, and tests.
2. Do not introduce a new synonym to avoid an unresolved business question. Add an OPEN QUESTION to `docs/19-open-questions.md` instead.
3. Do not use a role name as a permission, or a permission as a role. OQ12 and OQ28 govern role grants.
4. Keep `Ticket`, `Sale`, `Payment`, `Refund`, and `TicketValidationRecord` as independent concepts. ADR-0013 prohibits collapsing their lifecycles.
5. Treat `Expired` and `Used` as derived conditions where the ADRs say so; do not add them as stored Ticket states without a superseding ADR.
6. When an authoritative Myanmar term is approved, update the Myanmar term column and cite the approving source; do not silently replace `OPEN QUESTION` entries.

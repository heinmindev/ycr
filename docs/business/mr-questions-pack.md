# Myanma Railways Business Questions Pack

**Purpose:** provide a decision-ready list of the unresolved business questions in [`docs/19-open-questions.md`](../19-open-questions.md). This pack explains why each answer matters, what work it blocks or constrains, and presents neutral options for discussion with Myanma Railways.

**Status:** all questions below are **OPEN QUESTION** items. OQ20 is resolved, OQ22 is an **ENGINEERING DECISION**, and OQ32 is an **ENGINEERING DECISION** for its schedule and ownership clauses with a remaining OPEN QUESTION that is not a Myanma Railways question either; all three are listed in §Items not requiring a Myanma Railways answer so that no gap in the numbering is unexplained. The options are discussion prompts only; none is a recommendation or a recorded business decision. Existing **ENGINEERING DECISION** references describe implementation constraints already accepted by the project and do not answer the business questions.

**Suggested response owner:** Myanma Railways should nominate the accountable role for each answer (for example, Network Operations, Commercial/Fares, Finance, Customer Service, Security, or IT). The role labels below are proposed routing labels, not assumed authorities.

## How to use this pack

For each question, record the selected policy, the accountable approver, its effective date, and any exceptions. An answer should be added to `docs/19-open-questions.md` and, where it changes architecture or a binding constraint, captured in a superseding or new ADR before implementation.

## Coverage against `docs/19-open-questions.md`

The sections below are ordered thematically, for a workshop agenda. This table is ordered by OQ number, so coverage can be checked by eye. Every entry in `docs/19-open-questions.md` appears exactly once.

| OQ | Section in this pack | Status |
|---|---|---|
| OQ1 | Network and operating model | Asked |
| OQ2 | Network and operating model | Asked |
| OQ3 | Ticket product and validation | Asked |
| OQ4 | Ticket product and validation | Asked |
| OQ5 | Ticket product and validation | Asked — a project-side **ASSUMPTION** exists; see its Status line |
| OQ6 | Ticket product and validation | Asked |
| OQ7 | Ticket product and validation | Asked |
| OQ8 | Ticket product and validation | Asked |
| OQ9 | Fares and money | Asked |
| OQ10 | Payments, refunds, and sales | Asked |
| OQ11 | Payments, refunds, and sales | Asked — a project-side **ASSUMPTION** exists; see its Status line |
| OQ12 | Network and operating model | Asked |
| OQ13 | Network and operating model | Asked |
| OQ14 | Records, governance, and key operations | Asked |
| OQ15 | Network and operating model | Asked |
| OQ16 | Network and operating model | Asked |
| OQ17 | Fares and money | Asked |
| OQ18 | Payments, refunds, and sales | Asked |
| OQ19 | Ticket product and validation | Asked |
| OQ20 | Items not requiring a Myanma Railways answer | **Resolved** by ADR-0017 (SQL Server 2022) |
| OQ21 | Fares and money | Asked |
| OQ22 | Items not requiring a Myanma Railways answer | **Excluded** — **ENGINEERING DECISION** for the tech lead (ADR-0005) |
| OQ23 | Ticket product and validation | Asked |
| OQ24 | Payments, refunds, and sales | Asked |
| OQ25 | Records, governance, and key operations | Asked |
| OQ26 | Network and operating model | Asked — F-001 runs on a provisional value; see its Status line |
| OQ27 | Network and operating model | Asked — F-001 runs on a provisional value; see its Status line |
| OQ28 | Network and operating model | Asked — F-001 runs on a provisional value; see its Status line |
| OQ29 | Network and operating model | Asked — F-001 implements no control; see its Status line |
| OQ30 | Payments, refunds, and sales | Asked — a provisional tech-lead **ASSUMPTION** unblocks engineering; see its Status line |
| OQ31 | Payments, refunds, and sales | Asked |
| OQ32 | Items not requiring a Myanma Railways answer | **Excluded** — schedule and alert/DR ownership are **ENGINEERING DECISION**s (ADR-0017); the remaining storage-provider clause is blocked on an undecided hosting choice, not a Myanma Railways question |
| OQ33 | Network and operating model | Asked |


## Network and operating model

### OQ1 — What is the authoritative current YCR station list?

- **Why it matters:** Station identity, spelling, language forms, short codes, and stable QR indices must be consistent across routes, fares, timetables, tickets, reports, and printed material.
- **Blocks or constrains:** Network seed data; station and route APIs; fare inputs; timetable references; the authenticated station-index mapping required by ADR-0014.
- **Suggested options (non-binding):** (a) nominate one controlled MR master list and version it; (b) source the list from an existing operations registry with a named reconciliation owner; (c) publish a jointly approved list for an initial release and a change process for later additions.
- **Suggested decision owner:** Network Operations / Planning (role to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ1; ADR-0014 station-index decision; `docs/20-coding-conventions.md` station-code note.

### OQ2 — Which stations sell tickets?

- **Why it matters:** Sales capability, cashier-session setup, staffing, cash reconciliation, and passenger guidance depend on the list of selling stations.
- **Blocks or constrains:** Station-level sales permissions; cashier configuration; operational reports; rollout and counter testing.
- **Suggested options (non-binding):** (a) every station; (b) a nominated subset; (c) station-dependent hours or temporary service flags managed through an approved schedule.
- **Suggested decision owner:** Network Operations / Commercial (role to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ2; ADR-0019 sale business-date ownership; `docs/10-authorization-matrix.md`.

### OQ26 — Does a YCR station have an official station code, and what is its format?

- **Status:** **OPEN QUESTION** for Myanma Railways. F-001 currently uses a provisional tech-lead **ASSUMPTION** (hein, 2026-09-20): 2–10 characters, uppercase `A`–`Z` and `0`–`9`, unique across active and inactive stations, and never reused. This is a placeholder for the walking skeleton, **not a Myanma Railways answer**.
- **Why it matters:** Station codes are human-facing identifiers used in master data, routes, fares, timetables, printed material, and authenticated QR station-index mappings.
- **Blocks or constrains:** Authoritative station data; `StationCode` validation and database constraints; data migration; code reuse after deactivation; interoperability with existing MR systems.
- **Suggested options (non-binding):** (a) approve an MR-owned official code standard; (b) adopt an existing railway code catalogue; (c) use centrally assigned codes with a permanent non-reuse register; (d) define separate display and machine identifiers if MR requires both.
- **Suggested decision owner:** Network Operations / Planning (role to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ26; F-001 spec R3; ADR-0006; ADR-0014 station-index mapping; T-014 release gate.

### OQ27 — What are the naming rules for a station?

- **Status:** **OPEN QUESTION** for Myanma Railways. F-001 currently uses a provisional tech-lead **ASSUMPTION** (hein, 2026-09-20): `NameEn` and `NameMy` are both required, each 1–100 characters after trimming, and neither is unique. This is a placeholder for the walking skeleton, **not a Myanma Railways answer**. Unicode storage and the prohibition on Zawgyi remain separate project facts.
- **Why it matters:** Naming rules affect passenger-facing language, station search and display, printed tickets, data quality, deduplication, and historical reporting.
- **Blocks or constrains:** `BilingualName` validation; station master-data import; API and print contracts; uniqueness and rename policy; language/transliteration support.
- **Suggested options (non-binding):** (a) require English and Myanmar names with MR-approved maximum lengths; (b) make one language optional for stations without an approved translation; (c) require unique official names, with a separate short or printed name; (d) define an approved transliteration field and change process.
- **Suggested decision owner:** Network Operations / Customer Service (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ27; F-001 spec R4; `docs/20-coding-conventions.md` §3 and §6; T-014 release gate.

### OQ29 — How must the system treat Zawgyi-encoded Myanmar text on input?

- **Status:** **OPEN QUESTION** for Myanma Railways. F-001 validates presence and length only and implements **no Zawgyi control**; no detector will be built against a guessed rule. `docs/20-coding-conventions.md` §6 states "never store Zawgyi" as a project rule, but nothing currently enforces it. This is the absence of a control, **not a Myanma Railways answer**. Replacement tracked by T-014.
- **Why it matters:** Zawgyi text occupies the same Unicode code points as standard Myanmar, so storing a name as `nvarchar` prevents Zawgyi only if Zawgyi never arrives. Zawgyi stored as though it were Unicode renders as unreadable text for every later reader, on tickets, reports and printed material, and cannot be reliably repaired once it is in an append-only record.
- **Blocks or constrains:** `BilingualName` and every other Myanmar-text field, not only station names; API input validation; data import from existing systems; printing and display; audit rows, which cannot be edited after the fact.
- **Suggested options (non-binding):** (a) reject Zawgyi at the API boundary with a detector; (b) detect and convert to Unicode on input; (c) no server-side control, on the basis that only first-party clients producing Unicode may submit text. Separately: are Zawgyi-font devices still in use at YCR counters, and what should a clerk see if input is rejected?
- **Suggested decision owner:** MR IT with Operations and Customer Service (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ29; F-001 spec R4; `docs/features/F-001-walking-skeleton/review-claude.md` C-11; `docs/20-coding-conventions.md` §6; T-014 release gate.

### OQ12 — What operator roles exist?

- **Why it matters:** Named roles determine who may sell, validate, cancel, refund, manage fares, close sessions, and view reports. They also support segregation of duties and audit attribution.
- **Blocks or constrains:** Authorization matrix approval; permission grants; user provisioning; maker-checker controls; security review of privileged actions.
- **Suggested options (non-binding):** (a) approve the proposed roles in `docs/10-authorization-matrix.md`; (b) replace them with MR’s existing role catalogue; (c) use a core role set plus station- or duty-specific assignments.
- **Suggested decision owner:** MR HR/Operations with IT Security (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ12; `docs/10-authorization-matrix.md`; ADR-0016.

### OQ28 — Which operator roles may manage stations, and is there a separate read permission?

- **Status:** **OPEN QUESTION** for Myanma Railways. F-001 currently uses the provisional permission names `stations.manage` and `stations.read`, seeds no role-to-permission grants, and mints permissions directly in tests. This is a placeholder and an authorization containment measure, **not a Myanma Railways answer** about role grants.
- **Why it matters:** Station management and read access need explicit segregation-of-duties, least-privilege, provisioning, and audit rules. The existing matrix table is only a proposal; the permission inventory governs until grants are approved.
- **Blocks or constrains:** Authorization matrix approval; role provisioning; station endpoint access; audit attribution; release of production role seed data; F-001 replacement work tracked by T-014.
- **Suggested options (non-binding):** (a) approve role grants in the existing matrix; (b) use a separate station-manager role for `stations.manage` and one or more read-only roles for `stations.read`; (c) grant permissions by station/region assignment rather than global role; (d) require dual control for changes while allowing broader read access.
- **Suggested decision owner:** MR Operations / IT Security (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ28; `docs/10-authorization-matrix.md`; F-001 spec R8; ADR-0020; T-014 release gate.

### OQ13 — What reports are mandatory?

- **Why it matters:** Reporting requirements determine which events, dimensions, reconciliations, exports, and retention controls must be designed from the first release.
- **Blocks or constrains:** Reporting endpoints; data projections; observability metrics; acceptance tests; operational and finance sign-off.
- **Suggested options (non-binding):** (a) approve the initial sales and reconciliation reports in `docs/08-api-specification.md`; (b) provide an MR report catalogue with frequency, audience, and required fields; (c) phase reports by operational priority with a named owner for each phase.
- **Suggested decision owner:** Operations and Finance reporting leads (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ13; `docs/08-api-specification.md`; `docs/17-observability.md`.

### OQ15 — What availability and peak-load targets are required?

- **Why it matters:** Counter response time, acceptable outage duration, capacity, monitoring, and recovery design must reflect actual passenger and station demand.
- **Blocks or constrains:** Infrastructure sizing; service-level objectives; load tests; alert thresholds; the online-only assumptions in ADR-0015.
- **Suggested options (non-binding):** (a) define targets by station and operating period; (b) define a system-wide availability and latency target plus peak transactions per minute; (c) run a demand study first and set targets from measured volumes.
- **Suggested decision owner:** MR IT/Operations leadership (role to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ15; ADR-0015; `docs/17-observability.md`.

### OQ33 — Is station connectivity sufficient for online counter operations?

- **Status:** **OPEN QUESTION** for Myanma Railways. No provisional value is assumed. ADR-0015 records connectivity sufficiency as an **ASSUMPTION** that "must be validated by a survey"; that survey is an outstanding action, not a decision already taken, and the ASSUMPTION stands unchanged until Myanma Railways answers.
- **Why it matters:** The current architecture assumes online-only counter operation. If connectivity is not reliably sufficient at all selling stations, that assumption and the outage/fallback policy in OQ18 need to be revisited together.
- **Blocks or constrains:** Validation of the online-only operating model in ADR-0015; the paper-fallback decision in OQ18; the availability and peak-load targets in OQ15; station rollout sequencing.
- **Suggested options (non-binding):** (a) confirm connectivity is currently sufficient at all selling stations; (b) commission a station-by-station connectivity survey before rollout; (c) treat a defined subset of stations as connectivity-constrained and scope a fallback for them under OQ18.
- **Suggested decision owner:** MR IT / Network Operations (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ33; ADR-0015 line 44; related to OQ15 and OQ18.

### OQ16 — What existing Laravel system must be migrated, if applicable?

- **Why it matters:** Existing data, identifiers, business history, integrations, and cutover constraints affect scope and data integrity.
- **Blocks or constrains:** Migration inventory and mapping; coexistence or cutover design; historical reporting; reconciliation and rollback planning.
- **Suggested options (non-binding):** (a) no migration; start with a clean system; (b) migrate selected master and financial history; (c) full migration with a defined cutover and reconciliation window; (d) operate both systems temporarily with an agreed source of truth.
- **Suggested decision owner:** MR IT and business system owners (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ16; `docs/07-database-design.md` if migration is confirmed.

## Ticket product and validation

### OQ3 — Is ticketing per journey, per day, or another model?

- **Why it matters:** The ticket unit determines what a sale creates, how validity and repeat use are evaluated, and what passengers receive for a payment.
- **Blocks or constrains:** Sale/Ticket aggregate shape; validity and expiry; validation evidence; fare inputs; customer-facing wording and print layout.
- **Suggested options (non-binding):** (a) one ticket for one journey; (b) a day-based ticket; (c) another explicitly bounded product such as a time-window or multi-journey product.
- **Suggested decision owner:** Commercial / Customer Service (role to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ3; ADR-0013; `docs/11-ticket-lifecycle.md`.

### OQ4 — Are tickets tied to a specific train or service?

- **Why it matters:** Binding a ticket to a service changes sale inputs, timetable dependency, validation checks, disruption handling, and the information printed or encoded.
- **Blocks or constrains:** Ticket fields and QR payload interpretation; service selection; validation policy; refund/cancellation handling after service changes.
- **Suggested options (non-binding):** (a) ticket is service-specific; (b) ticket is valid on any eligible service for the chosen product; (c) service binding is optional by product category.
- **Suggested decision owner:** Operations / Commercial (role to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ4; ADR-0014 payload scope; `docs/12-fare-engine.md` potential service input.

### OQ5 — Is seat reservation required?

- **Status:** **OPEN QUESTION** for Myanma Railways. `docs/19-open-questions.md` item 5 carries an initial project-side **ASSUMPTION** of no reservation. That is a starting position for discussion, **not a Myanma Railways answer**; option (a) below restates it.
- **Why it matters:** Reservations introduce inventory, assignment, change, no-show, and concurrency rules that are absent from an unreserved ticket.
- **Blocks or constrains:** Ticket and service capacity model; sale concurrency; passenger UI and printed content; operational handling of changes.
- **Suggested options (non-binding):** (a) no reservation — this restates the project's current provisional position; (b) reservation for selected services/products; (c) reservation in a later phase after capacity data is available.
- **Suggested decision owner:** Operations / Customer Service (role to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ5; ADR-0013 ticket lifecycle.

### OQ6 — Are QR/barcodes required?

- **Why it matters:** A machine-readable credential affects print design, scanners, validation workflow, key management, and inspection procedures.
- **Blocks or constrains:** Ticket print specification; inspector tools; physical verification; production key/trust-list operations. If QR is required, ADR-0014 already sets an engineering format constraint.
- **Suggested options (non-binding):** (a) require the signed QR defined by ADR-0014; (b) require QR plus a human-readable fallback; (c) choose another approved machine-readable format; (d) do not require machine-readable tickets and use a different inspection process.
- **Suggested decision owner:** Operations / Security / Customer Service (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ6; ADR-0014; `docs/18-threat-model.md`.

### OQ7 — How is ticket validation performed?

- **Why it matters:** Validation determines who or what performs the check, what evidence is recorded, how repeat use is handled, and how passengers are treated at inspection.
- **Blocks or constrains:** Validation API and device workflow; append-only validation records; station/inspector permissions; customer dispute handling.
- **Suggested options (non-binding):** (a) inspector scans a signed credential; (b) counter or gate staff validate through a server-backed workflow; (c) combine device scan with a server lookup when connectivity is available.
- **Suggested decision owner:** Operations / Inspection (role to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ7; ADR-0013; ADR-0014; `docs/08-api-specification.md`.

### OQ8 — Is validation online, offline, or hybrid?

- **Why it matters:** Network loss changes whether a signature can be trusted as sufficient, how duplicate use is detected, and what happens to `AuthenticUnverified` results.
- **Blocks or constrains:** Handling of `AuthenticUnverified`; offline device behavior; reconciliation; inspector training; threat and fraud controls.
- **Suggested options (non-binding):** (a) online validation only; (b) offline signature validation with later synchronization; (c) hybrid validation with explicit rules for an unavailable server and `AuthenticUnverified`.
- **Suggested decision owner:** Operations / Security / IT (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ8; ADR-0013; ADR-0014 (explicitly blocks `AuthenticUnverified` handling).

### OQ19 — What is a ticket's validity window, and what must be printed on it?

- **Why it matters:** Validity and print content define what a passenger may use, what inspectors can verify, and which fields belong in the signed/printed representation.
- **Blocks or constrains:** Ticket validity calculation; QR fields and printer layout; language and numeral requirements; cancellation/refund timing; customer communications.
- **Suggested options (non-binding):** (a) same calendar day; (b) a duration from issue; (c) a specific train/service window; (d) product-specific windows. Printed content may be defined as a minimum set plus optional operational fields.
- **Suggested decision owner:** Commercial / Operations / Customer Service (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ19; ADR-0013; ADR-0014; `docs/11-ticket-lifecycle.md`.

### OQ23 — Does a reprint invalidate earlier printed tickets?

- **Why it matters:** The answer determines whether a lost or duplicated print can still be used and whether validation must compare a signed `printSequence` to the current record.
- **Blocks or constrains:** Reprint command guard; validation behavior; customer support; fraud controls; audit wording.
- **Suggested options (non-binding):** (a) invalidate all earlier prints and accept only the current sequence; (b) keep all prints valid until ticket cancellation/expiry; (c) invalidate earlier prints only under a defined reprint reason or approval flow.
- **Suggested decision owner:** Commercial / Security / Operations (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ23; ADR-0013; ADR-0014; `docs/11-ticket-lifecycle.md`.

## Fares and money

### OQ9 — What are current fares and passenger categories?

- **Why it matters:** Fare values and category eligibility are the commercial basis of every sale and must remain explainable for historical tickets.
- **Blocks or constrains:** Fare rule data; quote and sale flows; category validation; reports and reconciliation; fare approval and publication.
- **Suggested options (non-binding):** (a) provide an MR-approved fare table and category catalogue; (b) define fares by route/service/category in versioned rule sets; (c) publish an initial set with an explicit effective period and change authority.
- **Suggested decision owner:** Commercial / Finance (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ9; `docs/12-fare-engine.md`; ADR-0002.

### OQ17 — How is the loop fare determined, and can a passenger choose the direction?

- **Why it matters:** A circular route has more than one path between two stations; the policy changes the fare, displayed journey, and audit explanation.
- **Blocks or constrains:** `RouteSegmentResolver`; fare calculation; quote and ticket details; dispute and refund explanations.
- **Suggested options (non-binding):** (a) direction travelled; (b) shortest arc; (c) flat origin/destination fare; (d) zones; and separately decide whether the passenger may choose direction where more than one path exists.
- **Suggested decision owner:** Commercial / Network Planning (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ17; `docs/12-fare-engine.md`.

### OQ21 — How are MMK fares rounded?

- **Why it matters:** Rounding affects the amount collected, refunds, reconciliation, and whether historical calculations reproduce exactly.
- **Blocks or constrains:** Fare calculator and `Money` policy; payment totals; refund amounts; reports and financial tests.
- **Suggested options (non-binding):** (a) retain two decimal places; (b) round to whole kyats using a named rule; (c) use denomination-aware rounding; (d) define different rules by operation, if Finance requires it.
- **Suggested decision owner:** Finance (role to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ21; ADR-0018; `docs/12-fare-engine.md`.

## Payments, refunds, and sales

### OQ10 — What are cancellation/refund rules, including eligibility and amount?

- **Why it matters:** Cancellation and refund policy controls customer rights, financial exposure, fraud prevention, and the allowed lifecycle transitions.
- **Blocks or constrains:** Ticket cancellation; refund request/approval/disbursement/rejection; refund amount calculation; business-date assignment; authorization and audit tests.
- **Suggested options (non-binding):** (a) no refund except defined operational cancellation; (b) full refund before a cutoff; (c) partial refund by time/product/reason; (d) case-by-case approval under a documented policy.
- **The workshop may also want to capture:** fees, evidence requirements, deadlines, and whether a refunded ticket is also cancelled. These are prompts for a complete answer, not requirements on it.
- **Suggested decision owner:** Finance / Commercial / Customer Service (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ10; ADR-0013; ADR-0019; `docs/11-ticket-lifecycle.md`.

### OQ11 — What payment methods are required?

- **Status:** **OPEN QUESTION** for Myanma Railways. `docs/19-open-questions.md` item 11 carries an **ASSUMPTION**: "Phase 1 currently models cash only; **this is not a BUSINESS DECISION**." Option (a) below restates that provisional position, **not a Myanma Railways answer**.
- **Why it matters:** Payment methods determine integration, settlement, failure states, cashier reconciliation, and whether the current cash-only assumption is acceptable.
- **Blocks or constrains:** Payment model and permissions; sale transaction flow; reversal/refund mechanics; cashier close; PCI/security scope.
- **Suggested options (non-binding):** (a) cash only for Phase 1 — this restates the project's current provisional position; (b) cash plus approved electronic methods; (c) electronic methods only at selected stations; (d) phase payment methods by rollout, with a named settlement owner.
- **Suggested decision owner:** Finance / Treasury / IT (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ11 (cash-only is labelled an assumption); ADR-0013; ADR-0019.

### OQ18 — Is a manual paper-ticket fallback allowed during server outages?

- **Why it matters:** An outage policy determines whether stations can continue serving passengers and how unrecorded sales, cash, numbering, fraud, and later reconciliation are controlled.
- **Blocks or constrains:** The online-only operating model; outage runbook; reconciliation workflow; inventory/control of paper stock; audit and migration of offline sales.
- **Suggested options (non-binding):** (a) no fallback; suspend sales until service returns; (b) controlled paper fallback with pre-numbered stock and later keyed reconciliation; (c) approved offline digital mode with signed local records and reconciliation; (d) fallback only for specified stations or outage durations.
- **Suggested decision owner:** Operations / Finance / IT Security (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ18; ADR-0015 (paper/offline fallback remains blocked).

### OQ24 — Can one Sale contain multiple Tickets, and what payment/fare semantics apply?

- **Why it matters:** The sale boundary affects atomicity, receipts, per-passenger pricing, refunds, cancellation, and reconciliation.
- **Blocks or constrains:** Sale aggregate and API shape; fare calculation; payment allocation; partial cancellation/refund; audit and reporting.
- **Suggested options (non-binding):** (a) one sale contains one ticket; (b) one sale may contain multiple tickets with one total payment; (c) multiple tickets share a sale but retain itemized fares and independent cancellation/refund; (d) introduce an order/booking layer above ticket items.
- **Suggested decision owner:** Commercial / Finance (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ24; ADR-0013; `docs/08-api-specification.md`.

### OQ30 — What is the maximum cashier-session length, and may one operator hold more than one open session at a time?

- **Status:** **OPEN QUESTION** for Myanma Railways. Engineering currently runs on a provisional tech-lead **ASSUMPTION** (hein, 2026-09-22): 12-hour maximum session length; one open session per operator, enforced by the system (a second session attempt is rejected, not silently allowed). This is a placeholder to unblock an idempotency-retention rule, **not a Myanma Railways answer**.
- **Why it matters:** Session length and concurrency bound how long a cashier's in-progress work is valid, affect shift handover, and set the retention window the system must keep idempotency keys for.
- **Blocks or constrains:** `docs/20-coding-conventions.md` §5's idempotency-key retention rule, defined as "the maximum cashier-session length plus an operational margin"; cashier-session lifecycle and timeout design; shift/handover procedure.
- **Suggested options (non-binding):** (a) approve a fixed maximum session length, for example 12 hours — this restates the project's current provisional position; (b) define session length by shift pattern; (c) allow configurable session length per station or role, with a stated maximum.
- **Suggested decision owner:** Operations / Finance (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ30; ADR-0018 line 50; ADR-0019 line 40; `docs/20-coding-conventions.md` §5.

### OQ31 — What is the exact business-date source for refund operations that have no cashier session, and does Finance confirm it?

- **Status:** **OPEN QUESTION** for Myanma Railways / Finance. No provisional value is assumed, and nothing currently blocks on this question.
- **Why it matters:** ADR-0019 makes each operation own its `BusinessDate`, but a refund processed with no active cashier session has no session to derive that date from, and Finance has not confirmed the substitute rule.
- **Blocks or constrains:** Business-date assignment for no-session refunds; refund reconciliation and financial reporting for that case.
- **Suggested options (non-binding):** (a) use the calendar date at processing time; (b) use the business date of the original sale; (c) require Finance sign-off case by case until a rule is set.
- **Suggested decision owner:** Finance (role to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ31; ADR-0018 line 51; ADR-0019 line 41.

## Records, governance, and key operations

### OQ14 — What retention period applies to audit and financial records?

- **Why it matters:** Retention affects legal compliance, passenger and financial investigations, storage, archival, deletion requests, and the cost of ledger verification.
- **Blocks or constrains:** Audit/financial schema policy; archival and deletion procedures; ledger digest storage; disaster recovery; reporting history.
- **Suggested options (non-binding):** (a) adopt statutory/regulatory retention periods by record class; (b) define a common minimum period plus longer periods for disputes or investigations; (c) retain append-only audit rows indefinitely while archiving other records under approved controls.
- **Suggested decision owner:** MR Legal/Compliance with Finance and IT (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ14; ADR-0017; `docs/18-threat-model.md`.

### OQ25 — Who owns and approves the key-compromise response procedure?

- **Why it matters:** A compromised signing key can permit fraudulent credentials. The response must identify who can revoke, approve emergency rotation, publish trust changes, and refresh inspector devices.
- **Blocks or constrains:** Operational QR key management; incident response; trust-list publication; revocation timing; device refresh and audit evidence.
- **Suggested options (non-binding):** (a) assign a single MR Security owner with an independent approver; (b) use a joint Security + Operations incident authority; (c) delegate key custody to an approved service while MR retains policy approval.
- **The workshop may also want to capture:** compromise detection, approval thresholds, revocation timing, trust-list distribution, inspector-device refresh, and passenger remediation. These are prompts for a complete answer, not requirements on it.
- **Suggested decision owner:** MR IT Security / Operations leadership (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ25; ADR-0014; `docs/18-threat-model.md`.

## Items not requiring a Myanma Railways answer

All three are listed so that every gap in the OQ numbering is explained rather than silent.

### OQ20 — Production SQL Server version

This item is **RESOLVED** as SQL Server 2022 by ADR-0017. It is included here only to make clear why the pack skips its numbering; no Myanma Railways decision is requested in this pack.

### OQ22 — SPA framework and repository location

This item is an **ENGINEERING DECISION** for the tech lead under ADR-0005, not a Myanma Railways question. It is included here only to make clear why the pack skips its numbering; no Myanma Railways decision is requested in this pack.

### OQ32 — Ledger-digest custody

The generation-schedule and alert/disaster-recovery-ownership parts of this item are an **ENGINEERING DECISION** for the tech lead under ADR-0017 (schedule: daily; alert routing and DR ownership: the implementing engineering team, internally) — an internal engineering call, not something Myanma Railways needs to answer. The remaining storage-provider clause is genuinely open but blocked on an undecided hosting/infrastructure choice, not on a ruling; it stays in `docs/19-open-questions.md` OQ32 rather than in this pack. It is included here only to make clear why the pack skips its numbering; no Myanma Railways decision is requested in this pack.

## Cross-question dependencies

The following dependencies should be considered when scheduling decisions:

- **Ticket product first:** OQ3, OQ4, OQ5, OQ19, and OQ23 jointly define what is sold and what validation accepts.
- **Fare and payment:** OQ1, OQ2, OQ9, OQ11, OQ17, OQ21, and OQ24 jointly determine a reproducible sale and reconciliation result.
- **Outage and validation:** OQ8 and OQ18 must align with the fraud, reconciliation, and operational controls in ADR-0014 and ADR-0015.
- **Station master data and access:** OQ1, OQ2, OQ12, OQ26, OQ27, OQ28, and OQ29 determine authoritative station data, naming, text encoding, identifiers, and access ownership.
- **Governance:** OQ13, OQ14, OQ15, OQ16, OQ25, and the role decision in OQ28 determine ownership, evidence, and operating readiness.

No dependency listed here is a proposed sequencing decision; it is a prompt for the MR workshop agenda.

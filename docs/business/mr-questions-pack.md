# Myanma Railways Business Questions Pack

**Purpose:** provide a decision-ready list of the unresolved business questions in [`docs/19-open-questions.md`](../19-open-questions.md). This pack explains why each answer matters, what work it blocks or constrains, and presents neutral options for discussion with Myanma Railways.

**Status:** all questions below remain **OPEN QUESTION** items for Myanma Railways. OQ20 is resolved, OQ22 is an **ENGINEERING DECISION**, and OQ32 is an **ENGINEERING DECISION** for its schedule and ownership clauses with a remaining OPEN QUESTION that is not a Myanma Railways question either; all three are listed in §Items not requiring a Myanma Railways answer so that no gap in the numbering is unexplained. OQ2–OQ4, OQ9–OQ11, OQ17, OQ19, OQ21, OQ23, OQ24, OQ28 and OQ34–OQ60 have provisional tech-lead rulings that unblock engineering; each ruling is explicitly **not a Myanma Railways answer** and remains open with Myanma Railways. The options are discussion prompts only; none is a recommendation or a recorded business decision. Existing **ENGINEERING DECISION** references describe implementation constraints already accepted by the project and do not answer the business questions.

**Priority questions (hein, 2026-09-28; T-062):** OQ9, OQ17, OQ21 and OQ10. Each is marked **Priority** in the table and in its entry below.

**Suggested response owner:** Myanma Railways should nominate the accountable role for each answer (for example, Network Operations, Commercial/Fares, Finance, Customer Service, Security, or IT). The role labels below are proposed routing labels, not assumed authorities.

## How to use this pack

For each question, record the selected policy, the accountable approver, its effective date, and any exceptions. An answer should be added to `docs/19-open-questions.md` and, where it changes architecture or a binding constraint, captured in a superseding or new ADR before implementation.

## Coverage against `docs/19-open-questions.md`

The sections below are ordered thematically, for a workshop agenda. This table is ordered by OQ number, so coverage can be checked by eye. Every entry in `docs/19-open-questions.md` appears exactly once.

| OQ | Section in this pack | Status |
|---|---|---|
| OQ1 | Network and operating model | Asked |
| OQ2 | Network and operating model | Asked — provisional ruling recorded (T-062); still open with Myanma Railways |
| OQ3 | Ticket product and validation | Asked — provisional ruling recorded (T-062); still open with Myanma Railways |
| OQ4 | Ticket product and validation | Asked — provisional ruling recorded (T-062); still open with Myanma Railways |
| OQ5 | Ticket product and validation | Asked — a project-side **ASSUMPTION** exists; see its Status line |
| OQ6 | Ticket product and validation | Asked |
| OQ7 | Ticket product and validation | Asked |
| OQ8 | Ticket product and validation | Asked |
| OQ9 | Fares and money | **Priority** — Asked — provisional ruling recorded (T-062); still open with Myanma Railways |
| OQ10 | Payments, refunds, and sales | **Priority** — Asked — provisional ruling recorded (T-062); still open with Myanma Railways |
| OQ11 | Payments, refunds, and sales | Asked — provisional ruling recorded (T-062); still open with Myanma Railways (the former cash-only ASSUMPTION) |
| OQ12 | Network and operating model | Asked |
| OQ13 | Network and operating model | Asked |
| OQ14 | Records, governance, and key operations | Asked |
| OQ15 | Network and operating model | Asked |
| OQ16 | Network and operating model | Asked |
| OQ17 | Fares and money | **Priority** — Asked — provisional ruling recorded (T-062); still open with Myanma Railways |
| OQ18 | Payments, refunds, and sales | Asked |
| OQ19 | Ticket product and validation | Asked — provisional ruling recorded (T-062); still open with Myanma Railways |
| OQ20 | Items not requiring a Myanma Railways answer | **Resolved** by ADR-0017 (SQL Server 2022) |
| OQ21 | Fares and money | **Priority** — Asked — provisional ruling recorded (T-062); still open with Myanma Railways |
| OQ22 | Items not requiring a Myanma Railways answer | **Excluded** — **ENGINEERING DECISION** for the tech lead (ADR-0005) |
| OQ23 | Ticket product and validation | Asked — provisional ruling recorded (T-062); still open with Myanma Railways |
| OQ24 | Payments, refunds, and sales | Asked — provisional ruling recorded (T-062); still open with Myanma Railways |
| OQ25 | Records, governance, and key operations | Asked |
| OQ26 | Network and operating model | Asked — F-001 runs on a provisional value; see its Status line |
| OQ27 | Network and operating model | Asked — F-001 runs on a provisional value; see its Status line |
| OQ28 | Network and operating model | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ29 | Network and operating model | Asked — F-001 implements no control; see its Status line |
| OQ30 | Payments, refunds, and sales | Asked — a provisional tech-lead **ASSUMPTION** unblocks engineering; see its Status line |
| OQ31 | Payments, refunds, and sales | Asked |
| OQ32 | Items not requiring a Myanma Railways answer | **Excluded** — schedule and alert/DR ownership are **ENGINEERING DECISION**s (ADR-0017); the remaining storage-provider clause is blocked on an undecided hosting choice, not a Myanma Railways question |
| OQ33 | Network and operating model | Asked |
| OQ34 | Staff authentication and governance | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ35 | Staff authentication and governance | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ36 | Network and operating model | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ37 | Network and operating model | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ38 | Network and operating model | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ39 | Network and operating model | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ40 | Network and operating model | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ41 | Network and operating model | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ42 | Timetable and services | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ43 | Timetable and services | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ44 | Timetable and services | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ45 | Timetable and services | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ46 | Timetable and services | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ47 | Timetable and services | Asked — partly resolved; holiday and date exceptions remain open with Myanma Railways |
| OQ48 | Timetable and services | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ49 | Timetable and services | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ50 | Timetable and services | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ51 | Timetable and services | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ52 | Timetable and services | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ53 | Timetable and services | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ54 | Timetable and services | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ55 | Timetable and services | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ56 | Timetable and services | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ57 | Timetable and services | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ58 | Timetable and services | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ59 | Timetable and services | Asked — provisional ruling recorded; still open with Myanma Railways |
| OQ60 | Timetable and services | Asked — provisional ruling recorded, with Amendments 1–2; still open with Myanma Railways |
| OQ61 | Ticket product and validation | Asked |
| OQ62 | Ticket product and validation | Asked |


## Network and operating model

### OQ1 — What is the authoritative current YCR station list?

- **Why it matters:** Station identity, spelling, language forms, short codes, and stable QR indices must be consistent across routes, fares, timetables, tickets, reports, and printed material.
- **Blocks or constrains:** Authoritative station and route data; fare inputs; timetable data that references stations and routes; the authenticated station-index mapping required by ADR-0014.
- **Suggested options (non-binding):** (a) nominate one controlled MR master list and version it; (b) source the list from an existing operations registry with a named reconciliation owner; (c) publish a jointly approved list for an initial release and a change process for later additions.
- **Suggested decision owner:** Network Operations / Planning (role to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ1; ADR-0014 station-index decision; `docs/20-coding-conventions.md` station-code note.

### OQ2 — Which stations sell tickets?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Provisional tech-lead ruling (hein, 2026-09-28; T-062) — not a Myanma Railways answer:** every active station sells tickets; a per-station switch is added only if Myanma Railways asks. The ruling remains open with Myanma Railways.
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

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-22; T-014) — not a Myanma Railways answer:** `stations.manage` → `SystemAdministrator` and `RailwayAdministrator` only; `stations.read` → all eight roles: `SystemAdministrator`, `RailwayAdministrator`, `StationManager`, `TicketOperator`, `TicketInspector`, `FinanceOfficer`, `Auditor` and `ReportingUser` (the eighth role added under OQ12). The ruling remains open with Myanma Railways. F-001 seeds no role-to-permission grants and mints permissions directly in tests; the approved grants are recorded in `docs/10-authorization-matrix.md` for the F-002 provisioning work.
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

## Staff authentication and governance

### OQ34 — How are staff accounts governed?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-23; T-023) — not a Myanma Railways answer:** `SystemAdministrator` creates and disables accounts, assigns roles and resets passwords; no user may change their own roles; no second approver in Phase 1; usernames are 3–50 characters from lowercase `a-z`, `0-9` and `.`; hein holds the first administrator account until Myanma Railways names a holder. The ruling remains open with Myanma Railways.
- **Blocks or constrains:** F-002 account administration and first-administrator bootstrap.
- **Sources:** `docs/19-open-questions.md` OQ34; F-002 spec D9, D10, R20.

### OQ35 — Does Myanma Railways or a government policy mandate an authentication policy?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-23; T-023) — not a Myanma Railways answer:** no mandated policy is known, so password, lockout, MFA and session-length rules are engineering decisions recorded in ADR-0023. The ruling remains open with Myanma Railways.
- **Blocks or constrains:** F-002 password, lockout, MFA and session-length rules where an external policy would apply.
- **Sources:** `docs/19-open-questions.md` OQ35; F-002 spec D2, D3, D4, D12; ADR-0023.

## Routes and network operations

### OQ36 — Which routes does the Yangon Circular Railway have for this system's purposes?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-24; T-032) — not a Myanma Railways answer:** the system may hold several routes, routes may share stations, routes have no direction attribute, there is no separate `Line` concept, and the YCR loop is entered as one route with no seed data. The ruling remains open with Myanma Railways.
- **Blocks or constrains:** F-003 route shape, route direction, route master data and seed data.
- **Sources:** `docs/19-open-questions.md` OQ36; F-003 spec R5, §0.8; OQ1.

### OQ37 — What does a route's ordered station sequence look like?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-24; T-032) — not a Myanma Railways answer:** each route has a closed/open setting; closed routes connect last to first without repeating the first station; stations do not repeat; minimum length is two for open routes and three for closed routes. The ruling remains open with Myanma Railways.
- **Blocks or constrains:** F-003 sequence validation and database constraints.
- **Sources:** `docs/19-open-questions.md` OQ37; F-003 spec R6–R8, §0.8.

### OQ38 — Can a route's station sequence change after it is first defined?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-24; T-032) — not a Myanma Railways answer:** routes are immutable; a network change creates a new route and deactivates the old one, with `RouteStations` insert-only. The ruling remains open with Myanma Railways.
- **Blocks or constrains:** F-003 sequence-change operation and historical reconstruction.
- **Sources:** `docs/19-open-questions.md` OQ38; F-003 spec R9, R10, §0.8; `docs/07-database-design.md`.

### OQ39 — How do routes and inactive stations interact?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-24; T-032) — not a Myanma Railways answer:** an inactive station cannot be placed in a route; deactivation of a station already in a route is allowed and leaves it in the sequence. The ruling remains open with Myanma Railways.
- **Blocks or constrains:** F-003 sequence validation and the interaction with F-001 station deactivation.
- **Sources:** `docs/19-open-questions.md` OQ39; F-003 spec R11, R12, S16–S18.

### OQ40 — Which operator roles may manage routes, and which may view them?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-24; T-032) — not a Myanma Railways answer:** `routes.manage` → `SystemAdministrator` and `RailwayAdministrator`; `routes.read` → all eight roles. The ruling remains open with Myanma Railways.
- **Blocks or constrains:** F-003 endpoint permissions and the grant seed migration.
- **Sources:** `docs/19-open-questions.md` OQ40; `docs/10-authorization-matrix.md` §Route permission grants; F-003 spec R2, R3.

### OQ41 — What identifies a route, and what is its lifecycle?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-24; T-032) — not a Myanma Railways answer:** routes use a code and `BilingualName` under the station rules; codes are unique across active and inactive routes and never reused; routes may be deactivated but not reactivated, deleted or edited. The ruling remains open with Myanma Railways.
- **Blocks or constrains:** F-003 route fields, validation, uniqueness and deactivation.
- **Sources:** `docs/19-open-questions.md` OQ41; F-003 spec R13–R15, §0.8; OQ26 and OQ27.

## Timetable and services

### OQ42 — What is a "train" for this system, and does Phase 1 need to record trains separately from services?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-25; T-044) — not a Myanma Railways answer:** Phase 1 has no `Train` concept; core use case 3 is met by services, with no `/trains` endpoint, `Trains` table or `trains.manage` grant. The ruling remains open with Myanma Railways.
- **Blocks or constrains:** F-004's train/service aggregate, endpoints, permission and tables.
- **Sources:** `docs/19-open-questions.md` OQ42; F-004 spec R5, §6, §7; `docs/03-use-cases.md` core use case 3.

### OQ43 — What identifies a service?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-25; T-044) — not a Myanma Railways answer:** a service has a code and `BilingualName` under the station and route rules; a code is unique only among overlapping effective periods and may be reused after a non-overlapping period; there is no service type. The ruling remains open with Myanma Railways.
- **Blocks or constrains:** F-004 service fields, validation and unique constraints.
- **Sources:** `docs/19-open-questions.md` OQ43; F-004 spec R6, R7, R35; OQ26, OQ27 and OQ41.

### OQ44 — How does a service express its direction and its extent on a route?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-25; T-044) — not a Myanma Railways answer:** services store `Forward` or `Reverse` relative to route order; closed routes may wrap; a full circuit repeats the first stop as the last, only on a closed route and at most once; partial and reverse service on open routes are allowed. The ruling remains open with Myanma Railways.
- **Blocks or constrains:** F-004 direction, extent and stopping-pattern order checks.
- **Sources:** `docs/19-open-questions.md` OQ44; F-004 spec R9, R10, R12; OQ36 and OQ37.

### OQ45 — What are the rules for a service's stopping pattern?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-25; T-044) — not a Myanma Railways answer:** every stop is on the one route; stops follow direction order, passed stations are allowed, first and last stops are the extent, at least two stops are required, only a full-circuit closing stop may repeat, and no stop attributes exist in Phase 1. The ruling remains open with Myanma Railways.
- **Blocks or constrains:** F-004 stopping-pattern validation and `ServiceStops` shape.
- **Sources:** `docs/19-open-questions.md` OQ45; F-004 spec R10–R14, §7; OQ44.

### OQ46 — How do services interact with inactive routes and inactive stations?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-25; T-044) — not a Myanma Railways answer:** creation on an inactive route or with an inactive stop station is refused; later route or station deactivation remains allowed and existing services are unchanged. The ruling remains open with Myanma Railways.
- **Blocks or constrains:** F-004 service-creation validation and changes to F-003/F-001 deactivation behavior.
- **Sources:** `docs/19-open-questions.md` OQ46; F-004 spec R15, R16; OQ39; ADR-0025.

### OQ47 — On which days does a service run?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Partly resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-25; T-044) — not a Myanma Railways answer:** operating days are days of the week only and the operating date is the Asia/Yangon date on which the service starts; public-holiday calendars and per-date exceptions remain open with Myanma Railways and are a known limitation. The ruling remains open with Myanma Railways.
- **Blocks or constrains:** F-004 operating-day model and storage.
- **Sources:** `docs/19-open-questions.md` OQ47; F-004 spec R17, R18, §9.

### OQ48 — What is a service's effective period, and can a service change after it is defined?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-25; T-044) — not a Myanma Railways answer:** services are immutable except for withdrawal; `EffectiveFrom` is required, `EffectiveTo` is inclusive and nullable; withdrawal only shortens the period, cannot reopen or delete, and a timetable change withdraws the old service and creates a new one with the same code. The ruling remains open with Myanma Railways.
- **Note:** Superseded for timetables by OQ54 (hein, 2026-09-26; T-053).
- **Blocks or constrains:** F-004 service mutability, `PATCH /services`, grants and ADR-0024 applicability.
- **Sources:** `docs/19-open-questions.md` OQ48; F-004 spec R19–R21, R35, R36; OQ38 and OQ4.

### OQ49 — Which roles may manage and read services, trains and timetable versions?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-25; T-044) — not a Myanma Railways answer:** `services.manage` → `SystemAdministrator` and `RailwayAdministrator`; `services.read` → all eight roles; no `trains.*` grants; `schedules.*` is deferred to FR-004. The ruling remains open with Myanma Railways.
- **Note:** `schedules.*` resolved by OQ59 (hein, 2026-09-26; T-053).
- **Blocks or constrains:** F-004 endpoint permissions and grant seed migration.
- **Sources:** `docs/19-open-questions.md` OQ49; `docs/10-authorization-matrix.md` §Service permission grants; F-004 spec R3, R4; OQ42.

### OQ50 — May a service be created with an effective period that starts, or ends, before today's Asia/Yangon date?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-25; T-044) — not a Myanma Railways answer:** `EffectiveFrom` may be earlier than today's Asia/Yangon date; `EffectiveTo`, if given, may not be earlier than today; the overlap rule is unchanged and creation records `CreatedAtUtc` and the audit event. The ruling remains open with Myanma Railways.
- **Blocks or constrains:** F-004 effective-period validation at creation.
- **Sources:** `docs/19-open-questions.md` OQ50; F-004 spec §0.10, R39, S40, S40a; OQ48 and OQ1.

### OQ51 — What is a timetable version, what does it cover, and how is it identified and dated?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-26; T-053) — not a Myanma Railways answer:** one version covers **the whole network**; it is identified by a system-assigned sequential **number**, never reused, plus an English and a Myanmar name; it has a **start date only** and is in force until the next published version's start date. The ruling remains open with Myanma Railways.
- **Why it matters:** The scope and dating of a version decide what "the timetable on a given date" means for staff, for later ticketing and for reporting.
- **Blocks or constrains:** The `ScheduleVersion` record, its identity and its dates; the "version in force on a date" read.
- **Suggested options (non-binding):** Scope: (a) whole network; (b) per route; (c) per service. Identity: (a) system number; (b) bilingual name; (c) a code; or a combination. Dates: (a) start date only, in force until replaced; (b) start and end dates.
- **Suggested decision owner:** Network Operations / Planning (timetabling role to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ51; `docs/features/F-005-timetables/spec.md` §0.10, R6–R8.

### OQ52 — Which times does a timetable give at each stop?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-26; T-053) — not a Myanma Railways answer:** the first stop has a departure only, the last stop an arrival only, every other stop both; whole minutes; at each stop arrival ≤ departure; each arrival strictly later than the previous stop's departure; no passing times at stations a service does not stop at; the same times on every operating day of the service. The ruling remains open with Myanma Railways.
- **Why it matters:** Stop times are the content of a timetable; their shape and precision fix what can be published, displayed and, later, printed or bound to tickets.
- **Blocks or constrains:** The stop-time fields, their validation and storage; any later departure-based ticket rule (OQ4).
- **Suggested options (non-binding):** Times: (a) arrival and departure at every stop; (b) departure at the first stop, arrival at the last, both in between; (c) departure only. Precision: minutes or seconds. Passing times: none, optional or required. Per weekday: the same on every operating day, or allowed to differ.
- **Suggested decision owner:** Network Operations / Planning (timetabling role to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ52; F-005 spec §0.10, R10–R14.

### OQ53 — May a service run past midnight?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-26; T-053) — not a Myanma Railways answer:** **no running past midnight in Phase 1** — every time is 00:00–23:59 on the service's operating date, and a journey that would cross midnight cannot be entered. A known limitation. The ruling remains open with Myanma Railways.
- **Why it matters:** A late-evening service that ends after midnight cannot be timetabled under the current ruling; how such times are written also affects printing and any ticket validity window (OQ19).
- **Blocks or constrains:** The time range and past-midnight handling (ADR-0027 keeps a later change possible).
- **Suggested options (non-binding):** (a) no; (b) yes, written `24:15`-style, with a journey under 24 hours; (c) yes, written `00:15` with a next-day marker.
- **Suggested decision owner:** Network Operations / Planning (timetabling role to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ53; F-005 spec §0.10, R15, R16; ADR-0027.

### OQ54 — How do timetable versions relate to the services' own effective periods, and what happens when a listed service is withdrawn?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-26; T-053) — not a Myanma Railways answer:** a service runs on a date only if the version in force that day lists it, the date is in the service's own period and it is one of its operating days; every listed service must be in effect on the version's start date; **withdrawing a service is refused** while a published version that lists it still applies on or after the withdrawal date; to drop a service, publish a version without it, then withdraw it from that version's start date; stopping-pattern changes use services, time changes use versions. A second ruling (Q2, same date) keeps a service withdrawn if the version that made the withdrawal possible is later cancelled; the earlier version then lists it but it does not run. The ruling remains open with Myanma Railways.
- **Why it matters:** Two dating mechanisms (a service's period and a version's start date) must agree on which trains run on a date; the answer also decides how staff drop or replace a service.
- **Blocks or constrains:** Version-content validation; the service-withdrawal operation (it now depends on published versions); how a timetable change is carried out.
- **Suggested options (non-binding):** Period: (a) a service must be in effect throughout the version; (b) at some point in it; (c) independent. Withdrawal of a service in a published version: (a) allowed, version unchanged; (b) refused; (c) requires a new version. Timetable change: (a) a new version; (b) withdraw and recreate the service; (c) both.
- **Suggested decision owner:** Network Operations / Planning (timetabling role to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ54 (and its Q2 note), OQ48; F-005 spec §0.10, §0.12, R17–R20, R49.

### OQ55 — How do timetable versions follow one another?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-26; T-053) — not a Myanma Railways answer:** exactly one version is in force on each date — the published, not-cancelled version with the latest start date on or before it; no two published versions share a start date; before the first version, no service runs; a version may be inserted between two future ones; every version is kept. The ruling remains open with Myanma Railways.
- **Why it matters:** Staff, and later ticketing and reports, must be able to tell which timetable applied on any past or future date.
- **Blocks or constrains:** The supersession rule; the "version in force on a date" read; historical reconstruction.
- **Suggested options (non-binding):** (a) one in force per date, superseded by the next start date, no overlaps; (b) explicit periods, no overlap; (c) overlaps allowed with a precedence rule. Gaps: allowed or refused. Insertion between versions: allowed or refused.
- **Suggested decision owner:** Network Operations / Planning (timetabling role to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ55; F-005 spec §0.10, R21–R25.

### OQ56 — How is a timetable version prepared before it is published?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-26; T-053) — not a Myanma Railways answer:** a draft is **created whole** — every service with all its times, in one step — and never edited; a wrong draft is discarded (kept, never deleted) and created again; several drafts may exist at once. A known limitation: a draft cannot be corrected in place. The ruling remains open with Myanma Railways.
- **Why it matters:** How timetablers work — building up a draft over days, or preparing it complete elsewhere — decides whether the system must support editing drafts.
- **Blocks or constrains:** Draft operations; whether draft editing (and ADR-0024) is ever needed.
- **Suggested options (non-binding):** (a) created whole, never changed, discarded if wrong; (b) built up by adding and removing whole service entries; (c) freely edited. Discarded drafts: kept or deleted. Several drafts at once: yes or no.
- **Suggested decision owner:** Network Operations / Planning (timetabling role to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ56; F-005 spec §0.10, R26–R29.

### OQ57 — Who publishes a timetable version, and under what conditions?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-26; T-053) — not a Myanma Railways answer:** any holder of the timetable-management permission may publish, including the person who prepared the draft; **no second approver** in Phase 1; the start date must be today or later at publication (a version may take effect on the day it is published); a start date in the past is refused. The ruling remains open with Myanma Railways.
- **Why it matters:** Publishing changes which trains run for the whole network; the approval control and the notice period are governance choices.
- **Blocks or constrains:** The publish operation, its guards and whether a separate publish permission exists.
- **Suggested options (non-binding):** Approver: (a) any holder of the right, including the author; (b) a different person; (c) a different role. Start date: (a) today or later; (b) at least N days ahead; (c) past dates allowed.
- **Suggested decision owner:** Network Operations / Planning (timetabling role to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ57; F-005 spec §0.10, R30, R31; OQ34.

### OQ58 — Can a published timetable version be cancelled or withdrawn?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-26; T-053) — not a Myanma Railways answer:** a published version may be **cancelled only while its start date is later than today**; after that it is corrected only by publishing a later version; a cancelled version is kept and never applies. Withdrawing a version that has already taken effect is not provided. A second ruling (Q2, same date): cancelling is not refused because a service was withdrawn while that version applied; the service stays withdrawn. The ruling remains open with Myanma Railways.
- **Why it matters:** A mistake found after publication must be correctable; once a version is in force, changing it retroactively would change what already ran.
- **Blocks or constrains:** The cancel operation; the absence of a withdraw-version operation.
- **Suggested options (non-binding):** (a) never; correct by publishing a later version; (b) cancel only before it takes effect; (c) withdraw from a date, the previous version resumes; (d) withdraw from a date, no version in force.
- **Suggested decision owner:** Network Operations / Planning (timetabling role to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ58 (and its Q2 note); F-005 spec §0.10, R34, R49.

### OQ59 — Which roles may prepare, publish and read timetable versions?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-26; T-053) — not a Myanma Railways answer:** `schedules.manage` (create, discard, publish, cancel) → `SystemAdministrator` and `RailwayAdministrator`; `schedules.read` → all eight roles; no separate publish permission. The ruling remains open with Myanma Railways.
- **Why it matters:** Least privilege and segregation of duties for a change that affects the whole network.
- **Blocks or constrains:** The seeded role grants (a reviewed migration); endpoint access.
- **Suggested options (non-binding):** for example the service-management pattern (manage: `SystemAdministrator`, `RailwayAdministrator`; read: all eight roles) — a prompt, not a proposal of grants.
- **Suggested decision owner:** Network Operations / Planning (timetabling role to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ59; `docs/10-authorization-matrix.md` §Schedule permission grants; F-005 spec §0.10, R3–R5.

### OQ60 — May a timetable version list no services?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Resolved for engineering by a provisional tech-lead ruling (hein, 2026-09-26; T-053) — not a Myanma Railways answer:** **yes** — a version may list no services; once in force, no service runs until the next version (a network-wide suspension). **Amendments 1–2** (hein, 2026-09-27; T-054): such a version may be created and published only with a start date **later than today**, so it can always be cancelled before it takes effect. The ruling remains open with Myanma Railways.
- **Why it matters:** An empty version stops every train on the network; whether that is a legitimate operation, and with what safeguards, is a business decision.
- **Blocks or constrains:** Request validation for creating a version; the publication guard for an empty version.
- **Suggested options (non-binding):** (a) refused: at least one service; (b) allowed.
- **Suggested decision owner:** Network Operations / Planning (timetabling role to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ60 (with Amendments 1–2); F-005 spec §0.10, §0.11, R9, R50.

## Ticket product and validation

### OQ3 — Is ticketing per journey, per day, or another model?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Provisional tech-lead ruling (hein, 2026-09-28; T-062) — not a Myanma Railways answer:** one ticket is one journey, not tied to a train or service (ruled together with OQ4; consistent with OQ5, no seat reservation). The ruling remains open with Myanma Railways.
- **Why it matters:** The ticket unit determines what a sale creates, how validity and repeat use are evaluated, and what passengers receive for a payment.
- **Blocks or constrains:** Sale/Ticket aggregate shape; validity and expiry; validation evidence; fare inputs; customer-facing wording and print layout.
- **Suggested options (non-binding):** (a) one ticket for one journey; (b) a day-based ticket; (c) another explicitly bounded product such as a time-window or multi-journey product.
- **Suggested decision owner:** Commercial / Customer Service (role to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ3; ADR-0013; `docs/11-ticket-lifecycle.md`.

### OQ4 — Are tickets tied to a specific train or service?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Provisional tech-lead ruling (hein, 2026-09-28; T-062) — not a Myanma Railways answer:** one ticket is one journey, not tied to a train or service (ruled together with OQ3; consistent with OQ5, no seat reservation). The ruling remains open with Myanma Railways.
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

- **Status:** **OPEN QUESTION** for Myanma Railways. **Provisional tech-lead ruling (hein, 2026-09-28; T-062) — not a Myanma Railways answer:** a ticket is valid on the business date it was sold, and is printed in English and Myanmar with Myanmar numerals; the printed layout stays open. The ruling remains open with Myanma Railways.
- **Why it matters:** Validity and print content define what a passenger may use, what inspectors can verify, and which fields belong in the signed/printed representation.
- **Blocks or constrains:** Ticket validity calculation; QR fields and printer layout; language and numeral requirements; cancellation/refund timing; customer communications.
- **Suggested options (non-binding):** (a) same calendar day; (b) a duration from issue; (c) a specific train/service window; (d) product-specific windows. Printed content may be defined as a minimum set plus optional operational fields.
- **Suggested decision owner:** Commercial / Operations / Customer Service (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ19; ADR-0013; ADR-0014; `docs/11-ticket-lifecycle.md`.

### OQ23 — Does a reprint invalidate earlier printed tickets?

- **Status:** **OPEN QUESTION** for Myanma Railways. **Provisional tech-lead ruling (hein, 2026-09-28; T-062) — not a Myanma Railways answer:** a reprint invalidates every earlier copy; validation accepts only the current signed `printSequence` (ADR-0013/ADR-0014). The ruling remains open with Myanma Railways.
- **Why it matters:** The answer determines whether a lost or duplicated print can still be used and whether validation must compare a signed `printSequence` to the current record.
- **Blocks or constrains:** Reprint command guard; validation behavior; customer support; fraud controls; audit wording.
- **Suggested options (non-binding):** (a) invalidate all earlier prints and accept only the current sequence; (b) keep all prints valid until ticket cancellation/expiry; (c) invalidate earlier prints only under a defined reprint reason or approval flow.
- **Suggested decision owner:** Commercial / Security / Operations (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ23; ADR-0013; ADR-0014; `docs/11-ticket-lifecycle.md`.

### OQ61 — Is a ticket still valid for a journey that crosses midnight, or on a late-running train?

- **Question:** Is a ticket still valid for a journey that starts before midnight but ends after it, or for a late-running train? If so, for how long after the end of its business date?
- **Status:** **OPEN QUESTION** for Myanma Railways. No provisional value is assumed.
- **Why it matters:** The current engineering rule (ADR-0014, T-063) ends a ticket's validity at 00:00 Asia/Yangon on the calendar day after its business date, with no grace period. A passenger still travelling at that time would hold an expired ticket.
- **Blocks or constrains:** the `validUntil` signed into each ticket; validation near midnight.
- **Suggested decision owner:** Operations / Commercial (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ61; ADR-0014; related to OQ19.

### OQ62 — When may a ticket be reprinted?

- **Question:** When may a ticket be reprinted: how many times, by which role, and up to when (same business date, before first use, or any time)?
- **Status:** **OPEN QUESTION** for Myanma Railways. No provisional value is assumed.
- **Why it matters:** OQ23's provisional ruling says what a reprint does to earlier copies, but not who may reprint, how often, or until when.
- **Blocks or constrains:** the reprint feature only (the Reprint command guard in `docs/11-ticket-lifecycle.md` and ADR-0013).
- **Suggested decision owner:** Commercial / Operations (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ62; ADR-0013; `docs/11-ticket-lifecycle.md`; related to OQ23.

## Fares and money

### OQ9 — What are current fares and passenger categories?

- **Status:** **Priority question.** **OPEN QUESTION** for Myanma Railways. **Provisional tech-lead ruling (hein, 2026-09-28; T-062) — not a Myanma Railways answer:** the fare is 800 MMK per journey; one fare for every passenger in Phase 1. Passenger categories and an AC-coach fare are expected later and stay open. The ruling remains open with Myanma Railways.
- **Why it matters:** Fare values and category eligibility are the commercial basis of every sale and must remain explainable for historical tickets.
- **Blocks or constrains:** Fare rule data; quote and sale flows; category validation; reports and reconciliation; fare approval and publication.
- **Suggested options (non-binding):** (a) provide an MR-approved fare table and category catalogue; (b) define fares by route/service/category in versioned rule sets; (c) publish an initial set with an explicit effective period and change authority.
- **Suggested decision owner:** Commercial / Finance (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ9; `docs/12-fare-engine.md`; ADR-0002.

### OQ17 — How is the loop fare determined, and can a passenger choose the direction?

- **Status:** **Priority question.** **OPEN QUESTION** for Myanma Railways. **Provisional tech-lead ruling (hein, 2026-09-28; T-062) — not a Myanma Railways answer:** in principle the fare depends on journey length; Phase 1 charges the flat 800 MMK for every origin → destination, but the fare is held as data (a fare table), so distance bands or an AC-coach fare can be added later without a code change. How length is measured on the loop (shortest arc or direction travelled), and whether a passenger may choose the direction, stay open. The ruling remains open with Myanma Railways.
- **Why it matters:** A circular route has more than one path between two stations; the policy changes the fare, displayed journey, and audit explanation.
- **Blocks or constrains:** `RouteSegmentResolver`; fare calculation; quote and ticket details; dispute and refund explanations.
- **Suggested options (non-binding):** (a) direction travelled; (b) shortest arc; (c) flat origin/destination fare; (d) zones; and separately decide whether the passenger may choose direction where more than one path exists.
- **Suggested decision owner:** Commercial / Network Planning (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ17; `docs/12-fare-engine.md`.

### OQ21 — How are MMK fares rounded?

- **Status:** **Priority question.** **OPEN QUESTION** for Myanma Railways. **Provisional tech-lead ruling (hein, 2026-09-28; T-062) — not a Myanma Railways answer:** fares are whole kyat; no fractional amounts. The ruling remains open with Myanma Railways.
- **Why it matters:** Rounding affects the amount collected, refunds, reconciliation, and whether historical calculations reproduce exactly.
- **Blocks or constrains:** Fare calculator and `Money` policy; payment totals; refund amounts; reports and financial tests.
- **Suggested options (non-binding):** (a) retain two decimal places; (b) round to whole kyats using a named rule; (c) use denomination-aware rounding; (d) define different rules by operation, if Finance requires it.
- **Suggested decision owner:** Finance (role to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ21; ADR-0018; `docs/12-fare-engine.md`.

## Payments, refunds, and sales

### OQ10 — What are cancellation/refund rules, including eligibility and amount?

- **Status:** **Priority question.** **OPEN QUESTION** for Myanma Railways. **Provisional tech-lead ruling (hein, 2026-09-28; T-062) — not a Myanma Railways answer:** sold tickets are final: no cancellation and no refund in Phase 1. The documented Cancel and Refund transitions are out of Phase 1 scope; they are not deleted. The ruling remains open with Myanma Railways.
- **Why it matters:** Cancellation and refund policy controls customer rights, financial exposure, fraud prevention, and the allowed lifecycle transitions.
- **Blocks or constrains:** Ticket cancellation; refund request/approval/disbursement/rejection; refund amount calculation; business-date assignment; authorization and audit tests.
- **Suggested options (non-binding):** (a) no refund except defined operational cancellation; (b) full refund before a cutoff; (c) partial refund by time/product/reason; (d) case-by-case approval under a documented policy.
- **The workshop may also want to capture:** fees, evidence requirements, deadlines, and whether a refunded ticket is also cancelled. These are prompts for a complete answer, not requirements on it.
- **Suggested decision owner:** Finance / Commercial / Customer Service (roles to be nominated).
- **Sources:** `docs/19-open-questions.md` OQ10; ADR-0013; ADR-0019; `docs/11-ticket-lifecycle.md`.

### OQ11 — What payment methods are required?

- **Status:** **OPEN QUESTION** for Myanma Railways. `docs/19-open-questions.md` item 11 carries an **ASSUMPTION**: "Phase 1 currently models cash only; **this is not a BUSINESS DECISION**." Option (a) below restates that provisional position, **not a Myanma Railways answer**. **Provisional tech-lead ruling (hein, 2026-09-28; T-062) — not a Myanma Railways answer:** cash only in Phase 1 — the existing assumption, now a ruling. The ruling remains open with Myanma Railways.
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

- **Status:** **OPEN QUESTION** for Myanma Railways. **Provisional tech-lead ruling (hein, 2026-09-28; T-062) — not a Myanma Railways answer:** one Sale may hold several Tickets, all for the same origin, destination and fare, paid as one cash amount. The ruling remains open with Myanma Railways.
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
- **Fare and payment:** OQ1, OQ2, OQ9, OQ11, OQ17, OQ21, OQ24, and OQ31 jointly determine a reproducible sale and reconciliation result.
- **Outage and validation:** OQ8, OQ18, and OQ33 must align with the fraud, reconciliation, and operational controls in ADR-0014 and ADR-0015.
- **Station master data and access:** OQ1, OQ2, OQ12, OQ26, OQ27, OQ28, and OQ29 determine authoritative station data, naming, text encoding, identifiers, and access ownership.
- **Governance:** OQ13, OQ14, OQ15, OQ16, OQ25, and the role decision in OQ28 determine ownership, evidence, and operating readiness.
- **Timetables:** OQ51–OQ60 are answered together; OQ47 (holidays and per-date exceptions, still open) and OQ4 and OQ19 (whether tickets bind to a departure) depend on the same timetable model.
- **Session length and idempotency:** OQ30 directly blocks `docs/20-coding-conventions.md` §5's idempotency-key retention rule. The pack's provisional 12-hour value is a placeholder that keeps engineering moving; it is not a decision already made, and should not be read as one.

No dependency listed here is a proposed sequencing decision; it is a prompt for the MR workshop agenda.

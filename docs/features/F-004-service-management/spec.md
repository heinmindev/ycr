# F-004: Service management

Status: **Approved (hein, 2026-09-25).** Written by claude (T-044). Stages 1–2 of `docs/workflows/02-feature-development.md`. hein's rulings of 2026-09-25 on B, OQ42–OQ49, ADR-0025 and E1–E13 are recorded in §0.9, and his final rulings of the same day on OQ50, Q2, Q3, the two readings and the overlap consequence in §0.10; all are applied throughout. The business rulings are **provisional tech-lead rulings — not a Myanma Railways answer**.

Module(s): `Timetable` (new; first feature in it); read-only consumer of `Network` through a contract (ADR-0025); cross-cutting `Audit`, `Identity` (permission grants only)
Related: FR-003, FR-004 (boundary only), UC 3 "Manage trains" (`docs/03-use-cases.md` §Core use cases, item 3; met by services, OQ42 ruling), FR-002 / F-003 (routes), ADR-0002, ADR-0004, ADR-0006, ADR-0012, ADR-0014, ADR-0017, ADR-0018, ADR-0019, ADR-0021, ADR-0025 (Accepted 2026-09-25); ADR-0024 (Proposed) is not used (E10)

Decision owner:
- Business rules (what a train and a service are, service identity, direction and extent, stopping patterns, interaction with inactive routes and stations, operating days, effective periods and change, role grants): **Myanma Railways**, routed through `hein` (`docs/19-open-questions.md` OQ42–OQ50). Suggested routing label, following `docs/business/mr-questions-pack.md`: Network Operations / Planning (timetabling role to be nominated). **For F-004, hein gave provisional tech-lead rulings on 2026-09-25 (T-044) — not a Myanma Railways answer** (§0.9). Still open with Myanma Railways.
- Scope boundary between FR-003 and FR-004, and engineering decisions: **tech lead (`hein`)**, recorded as ADRs per `docs/decisions/README.md`.

Authoritative sources: `docs/00-project-specification.md`; `docs/01-functional-requirements.md` §FR-003, §FR-004; `docs/03`; `docs/04`; `docs/05`; `docs/07`; `docs/08`; `docs/10`; `docs/12`; `docs/19`; `docs/20`; `docs/glossary.md`; ADR-0002/0004/0006/0012/0014/0017/0018/0019/0021; the F-003 spec and the existing `Network` code. **No Myanma Railways-authoritative source defines any train, service, stopping pattern, operating calendar, effective-period rule or timetable permission grant.**

---

## 0. Discovery notes

Stage-1 output. Every note cites its source and names the decision owner. §0.1–§0.8 are kept as written at discovery, with closure notes added; where they list options or proposals, the rulings in §0.9 decide.

### 0.1 What the sources say

| # | Finding | Source |
|---|---|---|
| D1 | **FACT:** "Authorized users can define train services, operating days, effective periods and stopping patterns." This is the whole of FR-003. | `docs/01` §FR-003 |
| D2 | **FACT:** "Authorized users can publish timetable versions with effective dates." This is the whole of FR-004. | `docs/01` §FR-004 |
| D3 | **FACT:** core use case 3 is "Manage trains", use case 4 "Publish schedules". No actor is attached to either. | `docs/03` §Core use cases |
| D4 | **FACT:** `Service` and `ScheduleVersion` are candidate aggregates; there is **no `Train` aggregate**. `SchedulePublished` is a candidate event. "These are proposals. Confirm aggregate boundaries through discovery." `ScheduleVersion` "remains a Timetable aggregate; Ticketing does not take a direct navigation dependency on it" (ENGINEERING DECISION, ADR-0013). | `docs/04` §Candidate aggregates, §Candidate domain events, §Boundary clarifications |
| D5 | **FACT:** the `Timetable` module owns "Services, schedules, published timetable versions". | `docs/05` §Context-to-module map |
| D6 | **FACT:** core tables include `Trains`, `TrainServices`, `ServiceStops`, `ScheduleVersions`. Rules: "Use foreign keys", "use `date` for calendar dates", "**Preserve historical fare/schedule versions**". "Exact columns must be designed after requirements discovery." Module schema `timetable` (ENGINEERING DECISION, ADR-0012). | `docs/07` §Core tables, §Rules, §Module schemas |
| D7 | **FACT:** the API proposal lists `GET/POST/PATCH /trains`, `GET/POST/PATCH /services`, `POST /schedules/versions`, `POST /schedules/versions/{id}/publish`, and says "Published fare and **timetable versions are immutable**; do not PATCH a published version." | `docs/08` §Initial resources |
| D8 | **FACT:** "Versioned policy (fares, timetables): create a version, then publish it. **Never PATCH a published version** (ADR-0002)." | `docs/20` §4 |
| D9 | **FACT:** `trains.manage`, `services.manage` and `schedules.manage` are in the permission inventory with role grants OPEN QUESTION: "agents must not infer grants from the names". No read permission exists for any of them, and the proposal table has no row for them. | `docs/10` §Permission inventory, table |
| D10 | **FACT:** glossary `Service` = "A timetable/service aggregate distinct from a physical train." Forbidden synonyms: "Train, route". `ScheduleVersion` = "A versioned timetable aggregate that can be published and then treated as immutable"; forbidden: "Schedule (when a version is meant), service". There is **no glossary entry for `Train`**, `ServiceStop`, stopping pattern, operating day or effective period. | `docs/glossary.md` §Network, timetable, and fares |
| D11 | **FACT (provisional tech-lead ruling, not a Myanma Railways answer):** "A route has **no direction attribute**; direction belongs to services (FR-003) and fares (OQ17)." Routes may share stations; the YCR loop is entered as one route. | `docs/19` OQ36 ruling; F-003 spec R5 |
| D12 | **FACT (provisional rulings):** a route is open or closed (`IsClosed`); a closed route runs from its last station back to its first and never repeats its first station; a station appears at most once per route; sequences, `IsClosed`, code and names never change; a route can only be deactivated (`DeactivatedAtUtc` recorded). Deactivating a station leaves it in every route, shown `isActive = false`. | `docs/19` OQ37–OQ39, OQ41 rulings; F-003 spec R6–R15; `docs/07` §F-003 |
| D13 | **FACT:** `network.RouteStations` has PK `(RouteId, Position)` and unique `UX_RouteStations_StationId_RouteId` on `(StationId, RouteId)`; `ycr_app` has no `DELETE` on any `network` table. | `docs/07` §F-003; F-003 spec §7 |
| D14 | **FACT:** calendar dates, including **effective dates**, are `DateOnly` → SQL `date`. The configured local zone is Asia/Yangon and "is never hard-coded in domain logic". The clock is read only through `TimeProvider`. (Superseded ADR-0011 named them "fare/timetable `EffectiveFrom/To`".) | ADR-0018 §Time; ADR-0011 §Decision (superseded, naming only) |
| D15 | **FACT:** fare rules are versioned with effective periods because "Historical tickets must remain explainable". ADR-0002 is about fares; `docs/20` §4 extends the create-then-publish pattern to timetables. | ADR-0002; `docs/20` §4 |
| D16 | **FACT:** `ITimetableDbContext` is one of the named module interfaces. `YCR.Application.<A>` may depend only on its own context and domain, Common, and another module's `YCR.Application.<B>.Contracts`. Cross-module references are by identifier. **ADR-0012 is silent on database foreign keys between module schemas.** | ADR-0012 §Decision items 2, 4, 6 |
| D17 | **FACT:** no module has a `Contracts` namespace yet; `src/YCR.Application` has `Common`, `Identity`, `Network`. The architecture rule `ApplicationModuleMayDependOnlyOnAllowedTypes` already lists `Timetable` and admits any type in `YCR.Application.<B>.Contracts`. `INetworkDbContext` exposes `Stations` and `Routes` (no `DbSet<RouteStation>`). | `src/YCR.Application/`; `tests/YCR.ArchitectureTests/ArchitectureRules.cs`; `src/YCR.Application/Network/INetworkDbContext.cs` |
| D18 | **FACT:** the signed QR v1 payload carries ticket id, `validFrom`, `validUntil`, origin and destination station short indices, passenger category and `printSequence` — **no service or train field** — and "contains no fare-rule version or route data". | ADR-0014 §Binary layout v1 |
| D19 | **FACT:** potential fare inputs include "Service type" and "Travel date". | `docs/12` |
| D20 | **FACT:** "Train and service management" and "Timetable management" are in initial scope; "Real-time train tracking" is out of scope until approved. | `docs/00` §Initial scope, §Out of scope |
| D21 | **FACT:** only operations (Sale, Payment, cancellation, Refund) own a `BusinessDate`; master data does not. Idempotency is required for financial and retryable commands only; master data is not on the list. | ADR-0019 §Decision; `docs/20` §5 |
| D22 | **FACT:** ADR-0024 (client-held `version` / `expectedVersion` for edits composed from an earlier read) is **Proposed**; its Context names "fare and timetable drafts" as a future user, and its Proposed-stage note says a change to child rows does not advance the parent's `rowversion`. | ADR-0024 §Context, §Follow-up |
| D23 | **FACT:** OQ4 ("Are tickets tied to a specific train/service?") and OQ19 (validity window; pack option (c) "a specific train/service window") are open. | `docs/19` OQ4, OQ19; `docs/business/mr-questions-pack.md` §OQ4, §OQ19 |
| D24 | **FACT:** F-003 set `POST /routes` a 32 KB body limit as a REQUIRED CONTROL after review S-1; other endpoints' limits are T-042. | F-003 spec R27; `TASKS.md` T-042 |

### 0.2 The gaps, one by one

**G1 — Train versus service.** Four documents name *trains* as a managed thing (D3 "Manage trains", D6 `Trains`/`TrainServices`, D7 `/trains`, D9 `trains.manage`); the domain model has no `Train` (D4) and the glossary says a service is "distinct from a physical train" and forbids "Train" as a synonym (D10). FR-003 says "train services" (D1). Nothing says what a train is — rolling stock, a numbered working, or both — nor whether Phase 1 needs one at all, given real-time tracking is out of scope (D20). → **OPEN QUESTION, OQ42.** The naming mismatch is C1/C2.

**G2 — Service identity.** Nothing defines a service's identifier (number? code?), names, format, uniqueness scope or reuse, or whether a service has a type or class, which `docs/12` lists as a potential fare input (D19). → **OPEN QUESTION, OQ43.** OQ26/OQ27/OQ41 answered the same questions for stations and routes; hein may choose to rule the same way, but that is a business ruling, not an inference.

**G3 — Direction and extent.** The OQ36 ruling put direction on services (D11), and a route stores only an order and `IsClosed` (D12). Nothing says how a service states direction on a closed route (clockwise/anticlockwise relative to route order), where it starts and ends, whether it may run a full circuit back to its start or more than one circuit, or, on an open route, whether it may run in reverse or over part of the route. → **OPEN QUESTION, OQ44.** Engineering observation, not a rule: if a service's stops are an ordered list, direction can be *derived* from that list on a route with at least three stops; storing it separately would then be redundant but self-describing. That choice waits for OQ44.

**G4 — Stopping pattern versus route sequence.** Nothing says whether every stop must be a station of the route, whether stops must follow route order in the service's direction, whether stations may be skipped, whether the first and last stops are the service's start and end, the minimum number of stops, whether a service may use more than one route, whether a stop may repeat (a full circuit needs its start station twice, which a route never has — D12), or whether a stop has attributes (set-down only, pick-up only, request). → **OPEN QUESTION, OQ45.**

**G5 — Inactive routes and stations.** Nothing says whether a service may be defined on an inactive route or with an inactive stop, or what deactivating a route or station does to services that use it. OQ39 answered this for stations in routes (allow; stay in sequence). → **OPEN QUESTION, OQ46.** Engineering consequence for hein: an answer of "refuse the deactivation" makes `Network` call a `Timetable` contract, i.e. a two-way module dependency (ADR-0025 Follow-up). "Allow, service unchanged" keeps the dependency one-way.

**G6 — Operating days.** FR-003 names operating days (D1) and nothing defines them: days of week, a public-holiday calendar (who owns it; lunar-dated Myanmar holidays), per-date exceptions, or which date counts as the operating day. → **OPEN QUESTION, OQ47.** What *is* fixed: dates are `DateOnly`/`date` and the zone is Asia/Yangon from configuration (D14).

**G7 — Effective periods and versioning.** FR-003 gives services "effective periods" (D1); FR-004 gives timetable versions "effective dates" (D2); `docs/07` preserves schedule versions (D6); `docs/08`/`docs/20` make published timetable versions immutable (D7, D8). Three readings are possible and the sources do not choose:
- **(a) Immutable services, like routes.** A service is created whole with its pattern, days and period, never edited; a change is a new service plus withdrawing (ending) the old one. The rows are the history (F-003 A1 pattern).
- **(b) Services are draft content of a timetable version.** A service is edited freely while its version is a draft; publishing freezes the version (`docs/08`); a later version supersedes it from its effective date. This makes FR-003 and FR-004 one model.
- **(c) Services edited in place**, history only in the audit ledger. Conflicts with `docs/07` "preserve schedule versions" if services are part of a schedule, and with F-003's position that business history must not need the ledger (F-003 §8).

Whether a service can change, be withdrawn early, reactivated or deleted, and whether definitions may overlap, are business rules → **OPEN QUESTION, OQ48.** The model follows from the answer plus the boundary ruling (G8). Under (b), ADR-0024 applies to draft edits and its Proposed-stage note (child rows do not bump the parent's `rowversion`) must be resolved first, because service stops are child rows (D22).

**G8 — Where FR-003 ends and FR-004 begins.** The sources give FR-003 services, days, periods and patterns, and FR-004 versions and publication; neither mentions **times** (arrival/departure per stop), yet a timetable without times is not a timetable. This is a scope decision for hein, not a business rule. Options in §0.7.

**G9 — Permissions.** `trains.manage`, `services.manage`, `schedules.manage` exist by name only; no read permissions (D9) — the gap F-001 and F-003 found (F-001 C2, F-003 C2). Names are ENGINEERING (E6); grants are BUSINESS → **OPEN QUESTION, OQ49.**

**G10 — Cross-module reads and integrity.** Timetable must read route existence, `IsActive`, `IsClosed`, the ordered station ids and each station's active flag and code (validation, responses, audit snapshots). ADR-0012 item 4 requires a Network contract; none exists (D17). ADR-0012 is silent on cross-schema foreign keys (D16) while `docs/07` asks for foreign keys (D6). → **ENGINEERING DECISION, proposed in ADR-0025 (Proposed)** and §0.4 E3–E5.

**G11 — OQ4 and OQ19.** Neither blocks F-004. What each does and does not do:
- **OQ4 (tickets tied to a service?)** does **not** block defining services: a service exists whether or not a ticket later names it. It **constrains** F-004 in one direction: if the answer is "service-specific", a ticket will reference a service id, so service ids must be stable and service rows never deleted or rewritten in place. E11 proposes exactly that, which keeps every OQ4 answer open. A service-bound ticket would also need a service field the QR v1 payload does not have (D18) — a new QR version or a server-side lookup by ticket id; that is Ticketing's problem, not F-004's.
- **OQ19 (validity window)** does **not** block F-004. Its option (c) "a specific train/service window" needs departure times, which are FR-004 under every boundary option except B3.

**G12 — Out of scope, deliberately not designed:** fares and direction pricing (OQ17), the fare input "service type" beyond asking whether services have a type (OQ43), ticketing and validation, seed data (OQ1), real-time tracking (D20), the ADR-0014 station short index.

### 0.3 Contradictions found

| # | Contradiction | Sources | Proposed handling | Owner |
|---|---|---|---|---|
| C1 | UC 3 "Manage trains", `docs/07` `Trains`, `docs/08` `/trains` and `docs/10` `trains.manage` treat trains as managed data; `docs/04` has no `Train` aggregate and the glossary forbids "Train" as a synonym for `Service`. | `docs/03`; `docs/07`; `docs/08`; `docs/10`; `docs/04`; `docs/glossary.md` (`Service`) | Folded into OQ42. This spec uses `Service` for the scheduled thing and says "train" only when quoting. No glossary edit in this stage. | Myanma Railways (terminology); glossary owner after the ruling |
| C2 | `docs/07` names the service table `TrainServices`; `docs/04` and the glossary name the aggregate `Service`, and `docs/20` §2 names tables as the plural of the aggregate. | `docs/07` §Core tables; `docs/04`; `docs/20` §2 | ENGINEERING: §7 proposes `timetable.Services` and `timetable.ServiceStops` (E2). `docs/07` is corrected at stage 8. | hein |
| C3 | `docs/08` proposes `PATCH /services` and `PATCH /trains`, while the same file and `docs/20` §4 forbid patching a published timetable version. If services are part of a version (G7 (b)), patching a service in a published version is exactly what is forbidden. | `docs/08` §Initial resources; `docs/20` §4 | Depends on OQ48 and B. Under (a) there is no `PATCH`; under (b) `PATCH` applies to draft versions only. `docs/08` updated at stage 8. | hein, after OQ48 |
| C4 | `docs/10`'s inventory has `trains.manage`, `services.manage`, `schedules.manage` and no read permission; the proposal table has no row for them. | `docs/10` §table, §inventory | Same shape as F-001 C2 and F-003 C2. E6 proposes read names; OQ49 asks for grants. `docs/10` is not edited in this stage. | hein (names); Myanma Railways (grants) |
| C5 | FR-003 gives *services* effective periods; FR-004 gives *timetable versions* effective dates. Two effective-dating mechanisms with no stated relationship. | `docs/01` §FR-003, §FR-004 | Carried by OQ48 (business) and B (scope). | Myanma Railways; hein |
| C6 | *(Outside F-004; reported, not changed.)* `docs/business/mr-questions-pack.md` §OQ1 lists "timetable references" as blocked by OQ1. By the F-001/F-003 precedent OQ1 blocks **data**, not APIs (F-003 C3, being fixed for routes by T-043 item 5). | `docs/business/mr-questions-pack.md` §OQ1; F-003 spec C3 | This spec follows the precedent (R27). The pack is not edited here; T-043's item 5 could cover timetable references in the same sentence. | hein |
| C7 | *(Minor.)* The glossary's `Service` definition, "A timetable/service aggregate", defines the term with itself. | `docs/glossary.md` (`Service`) | Stage 8 rewrites it once OQ42/OQ43 are ruled. | glossary owner |
**Closure notes (hein's rulings, 2026-09-25):** **C1 closed** by OQ42 — there is no `Train` concept in Phase 1, and use case 3 is met by services. **C2 closed** by OQ42 and E2 — the table is `timetable.Services`. **C3 closed** by B1 and OQ48 — services are immutable except for withdrawal, so there is no `PATCH /services` and no `/trains`; `docs/08` is updated at stage 8. **C4 closed** by E6 and OQ49 — `services.read` added and both service permissions granted in `docs/10`; `trains.manage` marked not used in Phase 1; `schedules.*` stays open for FR-004. **C5 closed for F-004** by B1 and OQ48 — a service has its own effective period; how timetable-version effective dates relate to it is FR-004's question. C6 and C7 are unchanged.


### 0.4 Engineering choices proposed (hein decides)

| # | Proposal | Label | Why |
|---|---|---|---|
| E1 | **Module and schema.** Everything F-004 stores lives in schema `timetable`, behind a new `ITimetableDbContext` exposing only Timetable `DbSet`s, registered to the same scoped `YcrDbContext` as the other module interfaces. Code in `YCR.Domain.Timetable`, `YCR.Application.Timetable`, `Configurations/Timetable`, `Endpoints/Timetable`. | ENGINEERING DECISION (ADR-0012 items 2–3; `docs/07` §Module schemas; `docs/20` §1) | Binding already; stated so PLAN starts from it. |
| E2 | **`Service` is an aggregate owning its `ServiceStop` rows**; it references a route by `RouteId` and each stop's station by `StationId`, with no navigation property to Network. Tables `timetable.Services` and `timetable.ServiceStops` (not `TrainServices`, C2). No `DbSet<ServiceStop>` (F-003 plan P1 pattern). | ENGINEERING DECISION (ADR-0012 item 6; `docs/04`; `docs/20` §2) | Same shape as `Route`/`RouteStation`. |
| E3 | **Network contract.** New `YCR.Application.Network.Contracts.INetworkReader` (name open), read-only, returning primitive records: `GetRouteAsync(Guid routeId) → RouteReference?` with `RouteReference { Id, Code, NameEn, NameMy, IsClosed, IsActive, Stations: RouteStationReference[] }` and `RouteStationReference { Position, StationId, StationCode, StationNameEn, StationNameMy, StationIsActive }` (station fields are current values); `GetStationsAsync(IReadOnlyCollection<Guid> ids) → IReadOnlyDictionary<Guid, StationReference>`. Implemented by an `internal` class in `YCR.Application.Network` over `INetworkDbContext` with `AsNoTracking`, so it runs on the caller's connection and transaction. No write methods: Timetable never changes Network data. | ENGINEERING DECISION, proposed in ADR-0025 (Proposed) items 1–3 | ADR-0012 item 4 requires it (D16); the shape keeps `YCR.Domain.Network` out of Timetable (D17). |
| E4 | **Architecture rule for Contracts.** A type in any `YCR.Application.*.Contracts` namespace must not depend on a module domain namespace or a module context interface; with a violating fixture. | REQUIRED CONTROL, proposed in ADR-0025 item 4 | The existing rule admits any Contracts type (D17), so a leaky contract is caught only at its callers. |
| E5 | **Cross-schema foreign keys.** `timetable.Services.RouteId` → `network.Routes(Id)` and `timetable.ServiceStops.StationId` → `network.Stations(Id)`, `NO ACTION`, configured in Infrastructure without navigations, created by the Timetable migration. **Not** a composite key to `network.RouteStations(StationId, RouteId)` (ADR-0025 option B3): "this stop is on the route" is checked by the aggregate against the contract's route (R11), because the route's sequence is immutable (D12) and so cannot change under a stored service. | ENGINEERING DECISION, proposed in ADR-0025 (Proposed) items 5–7 | `docs/07` "Use foreign keys"; ADR-0012 is silent (D16); Network rows are never deleted (D13), so the keys cost nothing on delete. |
| E6 | **Permission names.** Add `services.read` to the `docs/10` inventory. Add `trains.read` only if OQ42 keeps trains, and `schedules.read` when timetable versions are in scope (B3, or FR-004). Grants: OQ49. | ENGINEERING DECISION (name); BUSINESS (grants) | F-001 C2 and F-003 E2 precedent. |
| E7 | **Errors and audit names.** Error codes `Timetable.<Reason>` mapped per ADR-0004; audit actions `Timetable.<Event>`; `SubjectType` constants in a new `TimetableAuditSubjects` (`Timetable.Service`, and `Timetable.Train` if OQ42 keeps trains); explicit snapshot records. | ENGINEERING DECISION (ADR-0004; ADR-0021; `docs/20` §2) | Existing convention. |
| E8 | **No `Idempotency-Key`.** | ENGINEERING DECISION (`docs/20` §5) | Master data, not financial or retryable (D21). |
| E9 | **Dates.** Effective dates are `DateOnly` → `date`, named `EffectiveFrom` / `EffectiveTo`; operating days are Asia/Yangon calendar dates with the zone from configuration; instants are `DateTimeOffset` → `datetimeoffset(3)` UTC with `CK_…_Utc` checks. Services own no `BusinessDate`. | ENGINEERING DECISION (ADR-0018 §Time; ADR-0019; ADR-0011 naming) | D14, D21. Whether `EffectiveTo` is inclusive and whether it may be null is OQ48. |
| E10 | **Concurrency follows the change model.** Under G7 (a): no edits, so `Services.IsActive` (or the withdrawal column) is the EF concurrency token for withdrawal, no `rowversion`, ADR-0024 unused (F-003 pattern). Under (b): draft edits compose from an earlier read, so ADR-0024 must be Accepted first, with its child-row note resolved. | ENGINEERING DECISION, conditional on OQ48 and B | D22. |
| E11 | **No hard delete, stable ids.** Whatever OQ48 rules, F-004 offers no `DELETE` and `ycr_app` gets no `DELETE` on `timetable` tables; a withdrawn or superseded service keeps its row and id. | ENGINEERING DECISION (AGENTS.md rule 5; `docs/07` "Preserve historical … schedule versions"; G11) | Keeps OQ4's service-bound-ticket option open and makes history answerable from rows, not the ledger (F-003 §8). If Myanma Railways wants deletion, that is a later, explicit change. |
| E12 | **Input-size limits.** Each `POST`/`PUT` body gets an explicit endpoint limit and each array a maximum count (stops ≤ 200, the route maximum, F-003 R26), refused before binding as in F-003 R27. | REQUIRED CONTROL (F-003 R26/R27 precedent; `docs/18` API abuse) | D24; T-042 sets the others. |
| E13 | **Grants to `ycr_app`** by a reviewed `Security_TimetableGrants` migration, never wider than the endpoints need; the grant set is the database statement of the OQ48 ruling (F-003 R10 pattern). Role grants seeded by a separate `Identity_Seed…` migration (F-002 R11 mechanism). | ENGINEERING DECISION (ADR-0017 item 3 spirit; F-003 E7) | Existing pattern. |

### 0.5 New OPEN QUESTIONs added to `docs/19`

OQ42 what a train is, and whether Phase 1 records trains · OQ43 service identity (number/code, names, format, uniqueness, reuse, type) · OQ44 direction and extent on closed and open routes · OQ45 stopping-pattern rules · OQ46 services versus inactive routes and stations · OQ47 operating days, holidays and exceptions · OQ48 effective periods, change, withdrawal and versioning · OQ49 role grants for services, trains and timetable versions. Each has a **BLOCKS:** line naming the rules below. `docs/business/mr-questions-pack.md` is **not** edited in this stage; `progress.md` lists the eight for hein.

### 0.6 What discovery did **not** find

No document defines: any YCR service, train or train number; what a train is; a service's identifier, names or type; how direction is expressed; any stopping-pattern rule; any operating-day or holiday rule; any effective-period or change rule for services; the relationship between service effective periods and timetable-version effective dates; who manages or reads services, trains or timetable versions. All are business rules → OQ42–OQ49. Missing engineering rules: cross-module contract shape and cross-schema foreign keys → ADR-0025 (Proposed). Nothing in the sources mentions arrival or departure **times** under FR-003; they are FR-004 by default (§0.7).

### 0.7 Proposed FR-003 / FR-004 boundary — options for hein, not a decision

| Option | F-004 (FR-003) contains | Later FR-004 feature contains | For | Against |
|---|---|---|---|---|
| **B1 — Service master data only** | Services: identity, route, direction, stopping pattern (ordered stops, no times), operating days, effective period; withdrawal. Trains only if OQ42 keeps them. Services immutable (G7 (a)). | Times per stop; `ScheduleVersion` create/publish; how versions reference services. | Smallest slice; mirrors F-003; blocks only on OQ42–OQ49; no ADR-0024 dependency. | If OQ48 turns out to be (b), FR-004 reshapes F-004's model (services move inside versions) — rework. A service without times is not yet usable by passengers. |
| **B2 — Services inside draft versions, publish deferred** | `ScheduleVersion` as a draft container; services (no times) created and edited inside a draft; no publish. | Times; publish; effective-date switching. | The FR-004 model is fixed early, so no reshaping later. | Splits one lifecycle across two features; needs ADR-0024 Accepted; needs `schedules.manage` grants now; a draft that cannot be published ships no business value on its own. |
| **B3 — Merge FR-003 and FR-004** | Services, stopping patterns with times, operating days, versions, publish, immutability after publish. | Nothing. | One coherent model and one set of rulings; the first deliverable is a real timetable. | Much larger feature; adds business questions not yet asked (times, dwell, overnight running, publication lead time, who publishes), so more OQs before approval; needs ADR-0024 and `schedules.*` grants. |
| **B4 — Trains first** | Only the `Train` catalogue, if OQ42 says trains are a separate thing. | Services under B1, B2 or B3. | Tiny, unblocks nothing else. | Only makes sense if OQ42 (a); likely not worth a feature on its own. |

Whichever is chosen, `ScheduleVersion`, `SchedulePublished` and `schedules.*` stay out of F-004 under B1 and B4. §6 and §7 are written against **B1** as a worked example so PLAN has a concrete shape; they change if hein picks B2 or B3.

**Outcome (hein, 2026-09-25): B1.** B2–B4 are kept above as history only; nothing in §1–§9 describes them.

### 0.8 Where the existing code constrains the design

- No `Timetable` folder exists in any layer; F-004 creates the module (E1). `DependencyInjection.AddInfrastructure` registers each module interface against `YcrDbContext` (`src/YCR.Infrastructure/DependencyInjection.cs`); `ITimetableDbContext` is added the same way.
- `Route.Create` takes the facts it needs (station existence and activity) from its handler and decides their meaning itself (F-003 plan P4). `Service.Create` can do the same with a `RouteReference` from the contract, so every pattern rule is testable without a database.
- `INetworkDbContext` has no `DbSet<RouteStation>`; the contract implementation reads the sequence through `Routes` as `GetRouteHandler` does (`src/YCR.Application/Network/GetRoute/GetRouteHandler.cs`).
- `NetworkConstraints` / `RouteModelTests` pattern for constraint names a handler matches; `TimetableConstraints` would follow it.
- `DeactivateRouteHandler` and `DeactivateStationHandler` change only if OQ46 rules "refuse" — which would also need the reverse contract (G5).

### 0.9 Rulings (hein, 2026-09-25)

The stage-2 ⛔ asked hein to rule on B, OQ42–OQ49, ADR-0025 and E1–E13. hein ruled on all of them on 2026-09-25.

**The business rulings (OQ42–OQ49) are provisional tech-lead rulings.** For each one: **Resolved by tech-lead ruling (hein, 2026-09-25; T-044) — not a Myanma Railways answer.** This project chose not to wait for one. **Still open with Myanma Railways.** If Myanma Railways later gives an official, different answer, that supersedes this ruling and needs its own follow-up task. The same wording is in `docs/19-open-questions.md` under each OQ and, for OQ49, in `docs/10-authorization-matrix.md` §Service permission grants.

| ID | Ruling | Label | Applied in |
|---|---|---|---|
| **B** | **B1.** F-004 is service master data only: identity, route, direction, stopping pattern (no times), operating days, effective period, withdrawal. Times, `ScheduleVersion` and publication are FR-004, a later feature. | ENGINEERING DECISION (tech lead, hein, 2026-09-25; scope) | R2, R22; §4–§9 |
| **OQ42** | No `Train` concept in Phase 1. `docs/03` use case 3 "Manage trains" is met by services. `trains.manage` stays in the `docs/10` inventory marked "not used in Phase 1 (OQ42 ruling)". No `/trains` endpoint, no `Trains` table. Closes C1 and C2 (the table is `timetable.Services`). | BUSINESS DECISION — provisional tech-lead ruling, not a Myanma Railways answer | R5; §6; §7; `docs/10` |
| **OQ43** | A service has a code under the station/route format (2–10 characters, `A`–`Z`/`0`–`9`) and a `BilingualName` under the station name rules. A code is unique only among services whose effective periods overlap; a service whose period does not overlap may reuse it, so a train number continues across timetable changes. No service type (deferred to fares, OQ9/OQ17). | BUSINESS DECISION — provisional tech-lead ruling, not a Myanma Railways answer | R6, R7, R35; S21–S29 |
| **OQ44** | A service stores `Direction` = `Forward` \| `Reverse`, relative to its route's station order. Stops follow the route order in that direction. On a closed route the stops may wrap from the route's last station to its first (`Forward`) or from its first to its last (`Reverse`). A full circuit is expressed by repeating the first stop as the last stop: only on a closed route, only as the final stop, at most one circuit. Part-route running is allowed on open and closed routes; reverse running on an open route is allowed. | BUSINESS DECISION — provisional tech-lead ruling, not a Myanma Railways answer | R9, R10, R12, R14; S4–S14 |
| **OQ45** | Every stop is a station of the service's route; stops are in route order for the direction (with the OQ44 wrap); stations may be passed without stopping; the first and last stops are the service's extent; at least 2 stops (a full circuit needs at least 3 distinct stations); one route per service; no repeated stop except the full-circuit closure; no stop attributes in Phase 1. The 200-stop cap (E12) stays. | BUSINESS DECISION — provisional tech-lead ruling, not a Myanma Railways answer; cap: REQUIRED CONTROL | R10–R14, R31; S6–S16 |
| **OQ46** | Creating a service on an inactive route, or with an inactive station as a stop, is refused (`422`, stable codes). Deactivating a route or station stays allowed and existing services are unchanged; service reads show the current route and station active flags. `Network` never calls `Timetable` (ADR-0025 Follow-up). Known limitation recorded. | BUSINESS DECISION — provisional tech-lead ruling, not a Myanma Railways answer | R15, R16; S17–S20, S39; §9 |
| **OQ47** | Operating days are a set of days of the week only, at least one day. A public-holiday calendar and per-date exceptions **stay open with Myanma Railways** (known limitation). The operating date is the Asia/Yangon calendar date on which the service starts, meaningful only once FR-004 adds times. | BUSINESS DECISION — provisional tech-lead ruling, not a Myanma Railways answer (partial; holidays and exceptions still OPEN) | R17, R18; S21, S52; §9; Blocked behaviour |
| **OQ48** | Immutable except for withdrawal. `EffectiveFrom` required; `EffectiveTo` inclusive, may be null (open-ended). Withdrawal takes a date D and sets `EffectiveTo` = D − 1 day; it may only shorten the period (never extend, never reopen), and D must not be earlier than today's Asia/Yangon date. If D ≤ `EffectiveFrom`, the service never runs. No other edit, no reactivation, no delete. A timetable change = withdraw the old service from D + create a new one with the same code from D. **Engineering consequence (hein):** the no-overlap rule cannot be a unique index; create and withdraw must serialise on the code (mechanism left to PLAN), with concurrency scenarios. Withdrawal is the one `UPDATE`; propose its concurrency token and record `WithdrawnAtUtc` (F-003 A1 pattern). | BUSINESS DECISION — provisional tech-lead ruling, not a Myanma Railways answer; serialisation: ENGINEERING DECISION (tech lead, hein, 2026-09-25) | R19–R21, R35, R36, R41; S23–S38; §5; §7 |
| **OQ49** | `services.manage` → `SystemAdministrator`, `RailwayAdministrator`; `services.read` → all eight roles. No `trains.*` grants. `schedules.*` deferred to FR-004, unchanged in the inventory. | BUSINESS DECISION — provisional tech-lead ruling, not a Myanma Railways answer | R3, R4; §2; `docs/10` §Service permission grants; S41, S42 |
| **ADR-0025** | **Accepted** (hein, 2026-09-25). Status and README row set to Accepted; its Decision section is final. | ENGINEERING DECISION (tech lead, hein, 2026-09-25) | R28, R29; E3–E5; S48, S50 |
| **E6** | Add `services.read` only (no `trains.read`, no `schedules.read`). | ENGINEERING DECISION (tech lead, hein, 2026-09-25) | R4; `docs/10` |
| **E10** | The immutable path: no version token, no `rowversion`; ADR-0024 stays unused. | ENGINEERING DECISION (tech lead, hein, 2026-09-25) | R36; §6; §7 |
| **E11** | Accepted: no hard delete; stable ids. | ENGINEERING DECISION (tech lead, hein, 2026-09-25) | R33; §7 grants |
| **E1–E5, E7–E9, E12, E13** | Accepted. The body-size limit **value** is set at PLAN, as in F-003. | ENGINEERING DECISION (tech lead, hein, 2026-09-25) | R8, R23–R32; §6–§8 |

### 0.10 Raised while applying the rulings — ruled (hein, 2026-09-25)

Raised by the rulings revision (`6cf674d`) and ruled by hein on 2026-09-25 (final rulings, recorded in the T-044 row). The table below is kept as asked; the rulings follow it.

| ID | Question | Why it is not settled by the rulings | Options (non-binding) | Blocks |
|---|---|---|---|---|
| **OQ50** (new in `docs/19`) | May a service be created with an `EffectiveFrom` — or an `EffectiveTo` — earlier than today's Asia/Yangon date? | OQ48 bars a *withdrawal* date in the past but says nothing about *creation*. A past `EffectiveFrom` records that a service ran on dates already over — for example the timetable in force at go-live. This is a business rule, so it is asked, not assumed. | (a) refused (`422`) · (b) allowed · (c) allowed only for an initial load | R39, S40 |
| **Q2** | The OQ45 ruling keeps a 200-stop cap, and F-003 allows routes of up to 200 stations (F-003 R26). A full circuit that stops at every station of a 200-station route needs 201 stop entries (the closure repeats the first). | Two rulings meet at the edge: the cap as written refuses a service the OQ44/OQ45 rulings otherwise allow. The spec applies the cap as ruled (200) and does not choose. | (a) keep 200 (accepted edge case; YCR's loop is far shorter) · (b) 201, so every valid pattern on any valid route fits | R31; S21 |
| **Q3** | A service created while its route, or one of its stop stations, is being deactivated. | Not covered by the rulings. The end state equals the serial order "create, then deactivate", which the OQ46 ruling allows. | Accept, not serialised (F-003 §5 precedent) · serialise | S51; §5 |

**Final rulings (hein, 2026-09-25):**

| ID | Ruling | Label | Applied in |
|---|---|---|---|
| **OQ50** | A service may be created with `EffectiveFrom` earlier than today's Asia/Yangon date. `EffectiveTo`, if given, must not be earlier than today (`422 Timetable.ServiceEffectiveToInPast`). The overlap rule (R35) applies unchanged. `CreatedAtUtc` and the `Timetable.ServiceCreated` audit event record when the service was entered. | BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-25; T-044), not a Myanma Railways answer; the error code: ENGINEERING DECISION | R37, R39; S40, S40a; `docs/19` OQ50 |
| **Q2** | Keep the 200-stop cap. A full circuit of a route with 200 stations (201 stops) is therefore not supported: a known limitation, well beyond YCR's size. | REQUIRED CONTROL (tech lead, hein, 2026-09-25) | R31; §9 |
| **Q3** | The race between creating a service and deactivating its route or a stop station is **accepted**, not serialised (ADR-0025 consequence). | ENGINEERING DECISION (tech lead, hein, 2026-09-25) | §5; S51 |
| **Reading 1** | Confirmed: a stop list that travels round the loop more than once without the closing repeat is refused as out of order (R12, S9). | Confirmed by hein, 2026-09-25 | R12; S9 |
| **Reading 2** | Confirmed: only stop stations are checked for being active; passed-through stations are not (R15, S18). | Confirmed by hein, 2026-09-25 | R15; S18 |
| **Overlap consequence** | Accepted: the overlap rule compares periods only, so services with the same code cannot run at the same time in both directions, or as weekday/weekend variants; they need different codes. | Accepted consequence (hein, 2026-09-25) | R35; §9 known limitations |

**Consequences of the rulings, recorded so they are seen (not conflicts):**
- The overlap rule compares **periods only**, not routes, directions or operating days (OQ43 ruling). So a code cannot be used at the same time by a `Forward` and a `Reverse` service, nor by a weekday service and a weekend service with overlapping periods; each needs its own code.
- **Interpretation of "at most one circuit" (OQ44):** a stop sequence whose cumulative travel along the route, in its direction, reaches the route's length without being the full-circuit closure passes its first stop again, and is refused as out of order (R12). Example: `[D, A, C, E]` `Forward` on a five-station closed route.
- **Interpretation of "an inactive station as a stop" (OQ46):** only stations the service **stops** at are checked. An inactive station on the route that the service passes without stopping does not refuse the creation (S18).
- A withdrawn service that never runs (D ≤ `EffectiveFrom`) has an empty period, so it overlaps nothing and its code is free for any period (R42).

---

## 1. Goal

Railway administrators need to define the services that run over the network — which route, which direction, which stations they stop at, on which days of the week and over which period — and to withdraw them, so that later features (timetable times and publication, ticketing, reporting) have one authoritative place to read services from, and every creation and withdrawal is authorised, validated and audited.

---

## 2. Actors and permissions

| Actor | Permission | Held by |
|---|---|---|
| Staff user managing services (create, withdraw) | `services.manage` | `SystemAdministrator`, `RailwayAdministrator` — provisional tech-lead ruling (hein, 2026-09-25; T-044, OQ49), not a Myanma Railways answer |
| Staff user reading services | `services.read` | All eight roles: `SystemAdministrator`, `RailwayAdministrator`, `StationManager`, `TicketOperator`, `TicketInspector`, `FinanceOfficer`, `Auditor`, `ReportingUser` — same ruling |

Holding a station or route permission gives no service right, and holding a service permission gives no station or route right. `trains.manage` is not used in Phase 1 (OQ42). `schedules.*` belongs to FR-004. Grants are data seeded by a reviewed migration (F-002 R11 mechanism); no API edits them.

---

## 3. Business rules

Provisional rulings are labelled "BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-25; T-044, OQnn), not a Myanma Railways answer", shortened below to **PROVISIONAL RULING (OQnn)**. Each is still open with Myanma Railways; an official, different answer supersedes the ruling and needs its own follow-up task.

In the rules, *n* is the number of stations in the service's route, and a station's position is its 1-based place in the route's sequence.

| # | Rule | Label | Source |
|---|---|---|---|
| R1 | Authorized users can define train services, operating days, effective periods and stopping patterns. | FACT | `docs/01` §FR-003 |
| R2 | F-004 is service master data only. Times, `ScheduleVersion` and timetable publication (FR-004) are a later feature. | ENGINEERING DECISION (tech lead, hein, 2026-09-25; ruling B → B1) | §0.9 B |
| R3 | Creating and withdrawing services requires `services.manage`, held by `SystemAdministrator` and `RailwayAdministrator`. | Name: FACT. Grants: **PROVISIONAL RULING (OQ49)** | `docs/10` §Service permission grants |
| R4 | Reading services requires `services.read`, held by all eight roles. Station and route permissions give no service right. | Name: ENGINEERING DECISION (tech lead, hein, 2026-09-25, E6). Grants: **PROVISIONAL RULING (OQ49)** | `docs/10` §Service permission grants |
| R5 | There is no `Train` concept in Phase 1: no `Train` aggregate, no `/trains` endpoint, no `Trains` table, no `trains.*` grant. Use case 3 "Manage trains" is met by services. | **PROVISIONAL RULING (OQ42)** | `docs/19` OQ42 |
| R6 | A service has a code: 2–10 characters of `A`–`Z` and `0`–`9` after trimming (`400 Timetable.InvalidServiceCode`). A code is unique only among services whose effective periods overlap (R35); it may be reused by a service whose period does not overlap. | **PROVISIONAL RULING (OQ43)** | `docs/19` OQ43; F-001 R3; F-003 R13 |
| R7 | A service has a `BilingualName`: `NameEn` and `NameMy` both required, each 1–100 characters after trimming (`400 Timetable.InvalidServiceName`); neither is unique. There is no service type or class. | **PROVISIONAL RULING (OQ43)** | `docs/19` OQ43; F-001 R4 |
| R8 | A service belongs to the `Timetable` module and references its route and stop stations by identifier only. | FACT (`docs/05`); ENGINEERING DECISION (ADR-0012 items 2, 6; E1, E2 accepted) | `docs/05`; ADR-0012 |
| R9 | A service stores `Direction` = `Forward` or `Reverse`, relative to its route's station order. `direction` is required on creation, with no default (`400 Common.ValidationFailed`). A route has no direction. | **PROVISIONAL RULING (OQ44; OQ36)**; no default: ENGINEERING DECISION (F-003 R25 pattern) | `docs/19` OQ36, OQ44 |
| R10 | A service has exactly one route, and its first and last stops are its extent. It may run over part of a route, on open and closed routes, in either direction. | **PROVISIONAL RULING (OQ44, OQ45)** | `docs/19` OQ44, OQ45 |
| R11 | Every stop is a station of the service's route: `422 Timetable.ServiceStopNotOnRoute`. An id that names no station at all is not on the route either. | **PROVISIONAL RULING (OQ45)** | `docs/19` OQ45 |
| R12 | Stops follow the route order in the service's direction. **Open route:** positions strictly increase (`Forward`) or strictly decrease (`Reverse`); no wrap. **Closed route:** each step between consecutive stops is measured cyclically in the direction (`Forward`: `(p₂ − p₁) mod n`; `Reverse`: `(p₁ − p₂) mod n`), so a sequence may wrap from the last station to the first (`Forward`) or from the first to the last (`Reverse`); the sum of the steps must be at most *n* − 1, or exactly *n* for a full circuit (R14). Anything else is `422 Timetable.ServiceStopsOutOfOrder`. | Order and wrap: **PROVISIONAL RULING (OQ44, OQ45)**. The step formulation and "a sum ≥ *n* that is not the closure is more than one circuit": ENGINEERING interpretation of the ruling (§0.10) | `docs/19` OQ44, OQ45 |
| R13 | Stations may be passed without stopping. A service has at least 2 stops (`422 Timetable.ServiceTooFewStops`). | **PROVISIONAL RULING (OQ45)** | `docs/19` OQ45 |
| R14 | No station appears twice among the stops (`422 Timetable.ServiceStopRepeated`), except the **full-circuit closure**: the last stop repeats the first, only on a closed route, only as the final stop, covering exactly one circuit (step sum = *n*), with at least 3 distinct stations (else `422 Timetable.ServiceTooFewStops`). A closure on an open route is a repeated stop. There are no stop attributes (set-down, pick-up, request) in Phase 1. | **PROVISIONAL RULING (OQ44, OQ45)** | `docs/19` OQ44, OQ45 |
| R15 | Creating a service on an inactive route is refused (`422 Timetable.ServiceRouteInactive`); so is a stop at an inactive station (`422 Timetable.ServiceStopStationInactive`). Only stations the service stops at are checked. An unknown route is `422 Timetable.ServiceRouteNotFound`. Nothing is written. | **PROVISIONAL RULING (OQ46)**; "only stops are checked": interpretation (§0.10); not-found code: ENGINEERING DECISION (F-003 E8 pattern) | `docs/19` OQ46 |
| R16 | Deactivating a route (F-003) or a station (F-001) stays allowed and changes no service. Service reads show the route's and each stop station's **current** active flag. `Network` never calls `Timetable`; F-001's and F-003's deactivation code does not change. | **PROVISIONAL RULING (OQ46)**; ADR-0025 Follow-up | `docs/19` OQ46; ADR-0025 |
| R17 | Operating days are a set of days of the week, at least one, no repeats (`400 Common.ValidationFailed` otherwise). There is no public-holiday calendar and no per-date exception in F-004. | Days of week: **PROVISIONAL RULING (OQ47)**. Holidays and exceptions: **OPEN QUESTION (OQ47, still open with Myanma Railways)** — not implemented, no placeholder | `docs/19` OQ47 |
| R18 | A service's operating date is the Asia/Yangon calendar date on which it starts. F-004 stores no times, so the rule has no observable effect until FR-004 adds them. Dates are `DateOnly`/`date`; the zone comes from configuration and is never hard-coded. | Operating date: **PROVISIONAL RULING (OQ47)**. Storage and zone: ENGINEERING DECISION (ADR-0018 §Time; E9) | `docs/19` OQ47; ADR-0018 |
| R19 | `EffectiveFrom` is required. `EffectiveTo` is inclusive and may be null (open-ended). At creation, `EffectiveTo`, if given, is not earlier than `EffectiveFrom` (`400 Timetable.InvalidEffectivePeriod`), so a new service always has a period of at least one day. | Required / inclusive / nullable: **PROVISIONAL RULING (OQ48)**. Non-empty at creation: ENGINEERING DECISION (input consistency) | `docs/19` OQ48 |
| R20 | A service is immutable except for withdrawal: code, names, route, direction, stops, operating days and `EffectiveFrom` never change, and there is no reactivation and no delete. A timetable change is: withdraw the old service from D, and create a new one with the same code from D. | **PROVISIONAL RULING (OQ48)** | `docs/19` OQ48 |
| R21 | **Withdrawal** takes a date D (the first date on which the service no longer runs) and sets `EffectiveTo` = D − 1 day. D must not be earlier than today's Asia/Yangon date (`422 Timetable.WithdrawalDateInPast`). The new `EffectiveTo` must be earlier than the current end (the current `EffectiveTo`, or unbounded when it is null); otherwise the withdrawal would extend or not change the period and is refused (`422 Timetable.WithdrawalDoesNotShorten`). **A second or later withdrawal is allowed when it shortens further**, under the same two checks; one that does not is refused with the same code. Withdrawal has no route or station guard (R16). | **PROVISIONAL RULING (OQ48)**; "a later withdrawal may shorten further": decided here as instructed (T-044) | `docs/19` OQ48 |
| R22 | No arrival or departure times, no `ScheduleVersion`, no publication. | ENGINEERING DECISION (tech lead, hein, 2026-09-25; B1) | §0.9 B |
| R23 | Service identifiers are application-generated GUIDs through `IIdGenerator`, mapped `ValueGeneratedNever()`. | ENGINEERING DECISION | ADR-0006 |
| R24 | Creation and every withdrawal are audited in the same `SaveChangesAsync`, with actor fields from the authenticated server-side context only. | ENGINEERING DECISION | ADR-0017 §Decision items 1–2; ADR-0021; F-003 R20 |
| R25 | Service management takes no `Idempotency-Key`. | ENGINEERING DECISION (E8, accepted) | `docs/20` §5 |
| R26 | Instants are `DateTimeOffset` / `datetimeoffset(3)` UTC with a UTC check constraint; services own no `BusinessDate`. | ENGINEERING DECISION | ADR-0018; ADR-0019 |
| R27 | No real service data ships: no seed migration or fixture of real YCR services. Tests build their own stations, routes and services. OQ1 blocks data, not the service API. | FACT (consequence of OQ1, open) | `docs/19` OQ1; F-003 R23 |
| R28 | Timetable reads Network data only through the read-only `YCR.Application.Network.Contracts` interface (E3), which returns primitive records; it never uses `INetworkDbContext` or `YCR.Domain.Network`. | ENGINEERING DECISION (ADR-0012 item 4; ADR-0025 items 1–3, Accepted) | ADR-0025 |
| R29 | `timetable.Services.RouteId` → `network.Routes(Id)` and `timetable.ServiceStops.StationId` → `network.Stations(Id)` are `NO ACTION` foreign keys, created by the Timetable migration with no navigation property. No key to `network.RouteStations`; R11 is enforced by the aggregate. | ENGINEERING DECISION (ADR-0025 items 5–7, Accepted; E5) | ADR-0025; `docs/07` §Rules |
| R30 | Service responses show the route's and each stop station's **current** code, names and active flag, read at query time through the contract; a service row stores only ids. | ENGINEERING DECISION (E3 accepted; F-003 R24 pattern) | ADR-0025 |
| R31 | `stopStationIds` has at most **200** elements (`400 Common.ValidationFailed`). Each body-carrying service endpoint has an explicit body-size limit, value set at PLAN, refused with `413` before JSON binding; malformed JSON is a bounded `400`. Consequently a full circuit of a route with 200 stations (201 stops) is not supported — a known limitation, well beyond YCR's size (§9). | REQUIRED CONTROL (tech lead, hein, 2026-09-25; OQ45 ruling, E12; Q2 ruling: keep 200) | F-003 R26, R27; `docs/18` |
| R32 | Error codes are `Timetable.<Reason>`, mapped per ADR-0004. | ENGINEERING DECISION (E7 accepted) | ADR-0004; `docs/20` §2 |
| R33 | Service rows are never deleted and ids never change; there is no `DELETE` endpoint and `ycr_app` has no `DELETE` on `timetable` tables. | ENGINEERING DECISION (tech lead, hein, 2026-09-25; E11) | AGENTS.md rule 5; `docs/07` §Rules |
| R34 | OQ4 and OQ19 do not block F-004; R33 keeps OQ4's "service-specific ticket" option open. | FACT (analysis, G11) | `docs/19` OQ4, OQ19; ADR-0014 |
| R35 | **No overlapping periods for one code.** Two services with the same code may not both have non-empty periods that overlap (inclusive dates; a null `EffectiveTo` is unbounded): `409 Timetable.ServiceCodePeriodOverlap`. This cannot be a unique index. The create and withdraw handlers **serialise on the code**: each takes an exclusive lock scoped to that code inside the transaction that reads the code's services and writes, so two requests on one code run one after the other and requests on different codes do not block each other. The mechanism (an application lock such as `sp_getapplock` on a code-derived resource, or an `UPDLOCK, HOLDLOCK` range read on `IX_Services_Code`) is chosen at PLAN. | Rule: **PROVISIONAL RULING (OQ43, OQ48)**. Serialisation: ENGINEERING DECISION (tech lead, hein, 2026-09-25) | `docs/19` OQ43, OQ48; §0.9 OQ48 |
| R36 | Withdrawal is the only `UPDATE`. It writes `EffectiveTo` and `WithdrawnAtUtc` (the clock's UTC now, `TimeProvider`) in the same `UPDATE`; `WithdrawnAtUtc` is null until the first withdrawal and holds the **latest** withdrawal's instant (each withdrawal's before and after state is in the audit ledger). `EffectiveTo` is the EF concurrency token, as a backstop behind R35's lock: if it fires, the request returns `409 Timetable.ServiceChangedConcurrently` and writes nothing. No `rowversion`, no client-held version. | ENGINEERING DECISION (tech lead, hein, 2026-09-25; OQ48 consequence, E10; F-003 A1 pattern). The `409` code is proposed here | F-003 §0.8 A1, R17 |
| R37 | When a create breaks several rules, the checks run in this order and the first failure is returned: request validation (`400 Common.ValidationFailed`); code; names; effective period (`400`); `EffectiveTo` not before today (`422`, R39); route exists; route active; stops on the route; repeated stop; too few stops; order; inactive stop station; then, under the lock, the code-period overlap (`409`). Within one check, the first offending stop in sequence order is reported. | ENGINEERING DECISION (proposed; F-003 plan P4 pattern) | F-003 `Route.Create` |
| R38 | Today's date for R21 is `TimeProvider.GetUtcNow()` converted to the configured zone (Asia/Yangon), never the server's local time. | ENGINEERING DECISION (ADR-0018) | ADR-0018 §Time |
| R39 | At creation, `EffectiveFrom` may be earlier than today's Asia/Yangon date (R38). `EffectiveTo`, if given, must not be earlier than today: `422 Timetable.ServiceEffectiveToInPast`, nothing written. The overlap rule (R35) applies unchanged, including against past periods. `CreatedAtUtc` and the `Timetable.ServiceCreated` audit event record when the service was entered, which may be after it started. | **PROVISIONAL RULING (OQ50)**; the error code: ENGINEERING DECISION (R32) | `docs/19` OQ50; §0.10 |
| R40 | Operating days are stored as seven `bit` columns and exchanged as an array of English day names (`"Monday"` … `"Sunday"`); responses list them Monday first. | ENGINEERING DECISION (proposed) | §7 |
| R41 | A withdrawn service whose `EffectiveTo` is earlier than its `EffectiveFrom` **never runs**. It reads back with those two dates as stored, `withdrawnAtUtc` set, and a derived `neverRuns: true` (computed, not stored; `false` for every other service). | Never runs: **PROVISIONAL RULING (OQ48)**. Read-back shape: ENGINEERING DECISION (proposed) | `docs/19` OQ48 |
| R42 | A service that never runs has an empty period, so it overlaps nothing, and R35 ignores it. | Consequence of the OQ43 and OQ48 rulings | R35, R41 |

**Blocking open questions:** none. OQ50, Q2 and Q3 were ruled on 2026-09-25 (§0.10). OQ42–OQ50 are resolved for F-004 by provisional tech-lead rulings and remain open with Myanma Railways; OQ47's holiday and exception part stays OPEN and does not block (§9). OQ1, OQ4, OQ17 and OQ19 stay open and do not block.

---

## 4. Scenarios (Given / When / Then)

Status codes follow ADR-0004; every error is ProblemDetails with `errorCode` and `traceId`. Unless a scenario says otherwise: route **RC** is closed, `[A, B, C, D, E]` (*n* = 5); route **RO** is open, `[P, Q, R, S]`; all are active; the caller holds `services.manage`; today (Asia/Yangon, from a test clock) is **2026-10-01**; a create uses a valid code, names, `Monday`–`Friday`, `effectiveFrom = 2026-10-05` and `effectiveTo = null`. "Nothing written" means no service row, no stop row and no audit event.

### Happy path

- **S1.** POST `/services` with `code = "S101"`, `routeId = RC`, `direction = Forward`, `stopStationIds = [A, C, E]` → `201 Created`, `Location: /api/v1/services/{id}`, `{ id }`; one `timetable.Services` row (`Direction = Forward`, `RunsOnMonday`–`RunsOnFriday` = 1, the others 0, `EffectiveTo` null, `WithdrawnAtUtc` null) and three `timetable.ServiceStops` rows, positions 1–3; one `Timetable.ServiceCreated` event.
- **S2.** A caller holding `services.read` GETs `/services/{id}` → `200` with `ServiceResponse`: code, names, direction, operating days (Monday first), dates, `neverRuns = false`, `withdrawnAtUtc = null`, the route's current code, names, `isClosed` and `isActive`, and the stops in position order with each station's current code, names and `isActive` (R30). No EF entity is serialised.
- **S3.** Given several services, one withdrawn and two sharing a code with adjacent periods, GET `/services?page=1&pageSize=50` → `200` with `items, page, pageSize, totalCount`, ordered by code then `effectiveFrom`, withdrawn services included; `&routeId=RC` returns only RC's services.

### Direction, order and circuits (OQ44, OQ45)

- **S4.** `Forward` wrap on a closed route: RC, `[D, E, A, B]` → `201` (steps 1, 1, 1; sum 3 ≤ 4).
- **S5.** `Reverse` wrap on a closed route: RC, `Reverse`, `[B, A, E, D]` → `201` (steps 1, 1, 1).
- **S6.** Wrap with skipped stations: RC, `Forward`, `[D, A, C]` → `201` (steps 2, 2; sum 4). RC, `Forward`, `[C, B]` → `201` (one step of 4: the service runs C, D, E, A, B and stops only at C and B).
- **S7.** Full circuit: RC, `Forward`, `[C, D, E, A, B, C]` → `201` (sum 5 = *n*); with skipped stations `[C, E, B, C]` → `201`; `Reverse`, `[C, A, D, C]` → `201` (steps 2, 2, 1). Read-back keeps the closing stop at the last position.
- **S8.** A full circuit with fewer than 3 distinct stations: RC, `Forward`, `[C, E, C]` → `422 Timetable.ServiceTooFewStops`; nothing written.
- **S9.** More than one circuit: RC, `Forward`, `[C, D, E, A, B, C, D]` → `422 Timetable.ServiceStopRepeated` (C appears other than as the closure); RC, `Forward`, `[D, A, C, E]` → `422 Timetable.ServiceStopsOutOfOrder` (steps 2, 2, 2; sum 6 > 4: it passes D again). Nothing written.
- **S10.** Open route, part route and reverse: RO, `Forward`, `[Q, R]` → `201`; RO, `Reverse`, `[S, R, P]` → `201`.
- **S11.** Wrap on an open route is refused: RO, `Forward`, `[R, S, P]` → `422 Timetable.ServiceStopsOutOfOrder`; RO, `Reverse`, `[Q, P, S]` → `422 Timetable.ServiceStopsOutOfOrder`. Nothing written.
- **S12.** A full circuit on an open route is refused: RO, `Forward`, `[P, Q, R, P]` → `422 Timetable.ServiceStopRepeated`; nothing written.
- **S13.** Repeated stop: RC, `Forward`, `[A, B, B, C]` → `422 Timetable.ServiceStopRepeated`; `[A, B, A, C]` → same. Nothing written.
- **S14.** Stops out of order: RC, `Forward`, `[A, C, B]` → `422 Timetable.ServiceStopsOutOfOrder` (steps 2, 4; sum 6 > 4); RO, `Forward`, `[P, R, Q]` → same. Nothing written.
- **S15.** One stop: `[A]` → `422 Timetable.ServiceTooFewStops`; nothing written.
- **S16.** A stop not on the route: a station that exists but is not in RC, or an id that names no station → `422 Timetable.ServiceStopNotOnRoute`; nothing written.

### Routes and stations (OQ46)

- **S17.** Inactive route: RC deactivated, then a create on RC → `422 Timetable.ServiceRouteInactive`; nothing written.
- **S18.** Inactive stop station: station C deactivated, then RC, `[A, C, E]` → `422 Timetable.ServiceStopStationInactive`; nothing written. RC, `Forward`, `[B, D]` (passes C without stopping) → `201` (R15).
- **S19.** Unknown `routeId` → `422 Timetable.ServiceRouteNotFound`; nothing written.
- **S20.** Given the S1 service on RC, stopping at C, when RC is deactivated (`POST /routes/{RC}/deactivate`) or C is deactivated (`POST /stations/{C}/deactivate`), then `204` exactly as today; the service's rows are unchanged; GET shows `route.isActive = false`, or C's stop with `isActive = false`.

### Validation and identity (OQ43, OQ47, OQ48)

- **S21.** `400 Common.ValidationFailed`, nothing read or written: a missing or blank `code`, `nameEn` or `nameMy`; a missing `routeId`; a missing `direction` or a value other than `Forward`/`Reverse`; a missing or empty `stopStationIds`; a `null` or non-GUID stop id; more than 200 stop ids (R31; exactly 200 reaches the handler); a missing or empty `operatingDays`; a repeated or unknown day name; a missing or malformed `effectiveFrom`; a malformed `effectiveTo`.
- **S22.** Code `"s1"`, `"A"`, `"ABCDEFGHIJK"` or `"S-1"` → `400 Timetable.InvalidServiceCode`; a 101-character name after trimming → `400 Timetable.InvalidServiceName`.
- **S23.** `effectiveTo = 2026-10-04` with `effectiveFrom = 2026-10-05` → `400 Timetable.InvalidEffectivePeriod`; `effectiveTo = effectiveFrom` → `201` (a one-day period).

### Codes and periods (OQ43, OQ48; R35)

- **S24.** Overlap, open-ended: `S101` exists from 2026-10-05, open-ended. A create of `S101` from 2027-01-01 → `409 Timetable.ServiceCodePeriodOverlap`; nothing written.
- **S25.** Overlap, inclusive edge: `S101` exists for 2026-10-05 – 2026-12-31. A create of `S101` from 2026-12-31 → `409 Timetable.ServiceCodePeriodOverlap`. From 2027-01-01 (adjacent) → `201`. The overlap check ignores route, direction and operating days: `S101` on RO, `Reverse`, `Saturday`–`Sunday`, from 2026-11-01 → `409`.
- **S26.** Timetable change: `S101` exists from 2026-10-05, open-ended. Withdraw it from 2027-01-01 → `204`, `EffectiveTo = 2026-12-31`. Create `S101` from 2027-01-01, on the same or another route → `201`. Both read back; the list shows them in `effectiveFrom` order.
- **S27.** Parallel creates, same code, overlapping periods → exactly one `201` and one `409 Timetable.ServiceCodePeriodOverlap`; one service row and one `Timetable.ServiceCreated` event.
- **S28.** Parallel creates, same code, adjacent periods (2026-10-05 – 2026-12-31, and from 2027-01-01) → both `201`.
- **S29.** Withdrawal racing a create: `S101` (X) exists from 2026-10-05, open-ended. In parallel, withdraw X from 2027-01-01 and create `S101` from 2027-01-01. The two serialise on the code (R35). Withdraw-then-create → `204`, `201`. Create-then-withdraw → create `409`, withdraw `204`. In no interleaving are two overlapping `S101` rows committed; the test forces both orders.

### Withdrawal (OQ48; R21, R36)

- **S30.** Withdraw X (from 2026-10-05, open-ended) with `withdrawFrom = 2026-11-01` → `204`; `EffectiveTo = 2026-10-31` and `WithdrawnAtUtc` = the clock's UTC now, both in one `UPDATE`; stops and every other column unchanged; one `Timetable.ServiceWithdrawn` event with before and after snapshots.
- **S31.** `withdrawFrom` = today (2026-10-01) → `204`, `EffectiveTo = 2026-09-30`.
- **S32.** `withdrawFrom` in the past (2026-09-30) → `422 Timetable.WithdrawalDateInPast`; nothing written.
- **S33.** Would extend or not change: Y has `EffectiveTo = 2026-12-31`. `withdrawFrom = 2027-02-01` (new end 2027-01-31) → `422 Timetable.WithdrawalDoesNotShorten`; `withdrawFrom = 2027-01-01` (new end equals the current one) → same. Nothing written.
- **S34.** Already ended: Z has `EffectiveTo = 2026-09-30`, before today. Any `withdrawFrom` ≥ today → `422 Timetable.WithdrawalDoesNotShorten` (it would reopen the period).
- **S35.** Second withdrawal: after S30 (`EffectiveTo = 2026-10-31`), `withdrawFrom = 2026-10-20` → `204`, `EffectiveTo = 2026-10-19`, `WithdrawnAtUtc` updated, a second `Timetable.ServiceWithdrawn` event; then `withdrawFrom = 2026-11-15` → `422 Timetable.WithdrawalDoesNotShorten`.
- **S36.** Never runs: W from 2026-11-01, open-ended; withdraw from 2026-10-15 → `204`, `EffectiveTo = 2026-10-14`. GET W → `effectiveFrom = 2026-11-01`, `effectiveTo = 2026-10-14`, `withdrawnAtUtc` set, `neverRuns = true` (R41). A new service with W's code and any period → `201` (R42).
- **S37.** Parallel withdrawals of one service, from 2026-11-01 and from 2026-10-20: they serialise (R35). In either order the final `EffectiveTo` is 2026-10-19. "11-01 then 10-20" gives `204`, `204` and two events; "10-20 then 11-01" gives `204`, `422 Timetable.WithdrawalDoesNotShorten` and one event.
- **S38.** Withdraw an unknown id → `404 Timetable.ServiceNotFound`; a missing or malformed `withdrawFrom` → `400 Common.ValidationFailed`.
- **S39.** Withdraw a service whose route, or one of its stop stations, has been deactivated → `204` (no route or station guard, R21).
- **S40.** Past `EffectiveFrom` accepted (R39): a create with `effectiveFrom = 2026-09-01` (before today, 2026-10-01) and `effectiveTo = null` → `201`; the row has `EffectiveFrom = 2026-09-01` and `CreatedAtUtc` = the clock's now, and the `Timetable.ServiceCreated` event carries the same instant. The overlap rule still applies: with `S101` existing from 2026-10-05, a create of `S101` from 2026-09-01, open-ended → `409 Timetable.ServiceCodePeriodOverlap`; from 2026-09-01 to 2026-10-04 → `201` (adjacent).
- **S40a.** Past `EffectiveTo` refused (R39): a create with `effectiveFrom = 2026-09-01` and `effectiveTo = 2026-09-30` (before today) → `422 Timetable.ServiceEffectiveToInPast`; nothing written. With `effectiveTo = 2026-10-01` (today) → `201`.

### Access, input limits, integrity

- **S41.** Anonymous request to any `/services` endpoint → `401 Auth.Unauthenticated`.
- **S42.** `403`: a caller with only `services.read` POSTs `/services` or `/services/{id}/withdraw`; a caller with only `routes.manage`, `routes.read`, `stations.manage` or `stations.read` POSTs or GETs `/services` (R4).
- **S43.** A body over the endpoint's limit, declared or chunked → `413` before JSON binding, framework ProblemDetails with no stack trace, exception type or path; malformed JSON → bounded `400` (R31).
- **S44.** `pageSize=201` → `400 Timetable.InvalidPageRequest`.
- **S45.** An audit event's actor fields come from the authenticated context; actor fields in the request body are ignored (ADR-0017 §2).
- **S46.** A failed create writes nothing (one `SaveChangesAsync`); a successful one has stop positions contiguous `1..n` in request order.
- **S47.** Database privileges: `ycr_app` holds exactly §7's grants; a test asserts the absences — no `DELETE` on either table; no `UPDATE` of any `Services` column other than `EffectiveTo` and `WithdrawnAtUtc`; no `UPDATE` on `ServiceStops`; no DDL (F-003 S25 pattern).
- **S48.** Foreign keys (ADR-0025): inserting a `ServiceStops` row whose `StationId` names no station, or a `Services` row whose `RouteId` names no route, by direct SQL under `ycr_app`, is refused by the database.
- **S49.** A Myanmar service name round-trips unchanged, using real Myanmar Unicode text.
- **S50.** Architecture: `YCR.Application.Timetable` depends on no `YCR.Domain.Network` type and not on `INetworkDbContext`; a type in `YCR.Application.Network.Contracts` depends on no module domain or context type (ADR-0025 item 4); each is proven with a violating fixture.
- **S51.** A service created while its route or a stop station is being deactivated: an **accepted race**, not serialised and not tested for its interleaving (Q3 ruling, §5); the end state equals "create, then deactivate", which R16 allows.
- **S52.** Operating days round-trip: `["Sunday", "Monday"]` stores `RunsOnSunday = 1` and `RunsOnMonday = 1`, the others 0, and reads back as `["Monday", "Sunday"]`.

---

## 5. State changes

| Entity | From | Event | Guard | To |
|---|---|---|---|---|
| Service | (none) | `CreateService` | `services.manage`; R6, R7, R9–R15, R17, R19, R39; no overlapping service with the same code (R35, under the code lock) | Created, with its period |
| Service | any | `WithdrawService(D)` | `services.manage`; D ≥ today, Asia/Yangon (R21); D − 1 earlier than the current end (R21); under the code lock (R35) | Same service; `EffectiveTo` = D − 1; `WithdrawnAtUtc` = now |
| Route / Station | Active | `DeactivateRoute` / `DeactivateStation` | unchanged from F-003 / F-001 | Inactive; services unchanged (R16) |

A service has no status column. Whether it runs on a date follows from its period and operating days; `neverRuns` (R41) is the only derived flag the API reports. No other transition exists: there is no edit, reactivation or delete (R20, R33).

**Race between service creation and route or station deactivation — accepted, no serialisation (ENGINEERING DECISION, tech lead, hein, 2026-09-25; Q3; ADR-0025 consequence).** A service creation can race a deactivation of its route or of one of its stop stations: the creation reads the route and stations as active through the Network contract, the deactivation commits, then the creation commits. The end state is the same as the serial order "create, then deactivate", which the OQ46 ruling allows (the service is unchanged and its reads show the inactive flags). The active checks (R15) are therefore not serialised with deactivation, and no scenario tests the interleaving (S51). F-003 accepted the same race for route creation against station deactivation (F-003 §5).

---

## 6. API

Base path `/api/v1`, JSON camelCase, GUID ids, dates `YYYY-MM-DD`, ProblemDetails with `errorCode` and `traceId` (`docs/20` §4; ADR-0004). No `Idempotency-Key` (R25). No version token (E10). Every endpoint also has the responses in `docs/08` §Applies to every protected endpoint.

| Method | Path | Request | Success | Error codes | Permission |
|---|---|---|---|---|---|
| POST | `/services` | `CreateServiceRequest`; body limit set at PLAN (R31) | `201` + `CreateServiceResponse { id }` and `Location: /api/v1/services/{id}` | `400 Common.ValidationFailed` · `400 Timetable.InvalidServiceCode` · `400 Timetable.InvalidServiceName` · `400 Timetable.InvalidEffectivePeriod` · `400` malformed JSON · `401` · `403` · `409 Timetable.ServiceCodePeriodOverlap` · `413` · `422 Timetable.ServiceRouteNotFound` · `422 Timetable.ServiceRouteInactive` · `422 Timetable.ServiceStopNotOnRoute` · `422 Timetable.ServiceStopRepeated` · `422 Timetable.ServiceTooFewStops` · `422 Timetable.ServiceStopsOutOfOrder` · `422 Timetable.ServiceStopStationInactive` · `422 Timetable.ServiceEffectiveToInPast` | `services.manage` |
| GET | `/services/{id}` | — | `200` + `ServiceResponse` | `401` · `403` · `404 Timetable.ServiceNotFound` | `services.read` |
| GET | `/services` | `?page=1&pageSize=50` (max 200) `&routeId=` (optional); ordered by `code`, then `effectiveFrom`; withdrawn services included | `200` + `{ items: ServiceSummaryResponse[], page, pageSize, totalCount }` | `400 Timetable.InvalidPageRequest` · `401` · `403` | `services.read` |
| POST | `/services/{id}/withdraw` | `WithdrawServiceRequest { withdrawFrom }`; body limit set at PLAN | `204` | `400 Common.ValidationFailed` · `400` malformed JSON · `401` · `403` · `404 Timetable.ServiceNotFound` · `409 Timetable.ServiceChangedConcurrently` (R36 backstop) · `413` · `422 Timetable.WithdrawalDateInPast` · `422 Timetable.WithdrawalDoesNotShorten` | `services.manage` |

A withdrawal can never cause `409 Timetable.ServiceCodePeriodOverlap`, because it only shortens a period.

**Contracts.**

```text
CreateServiceRequest   { code, nameEn, nameMy, routeId, direction: "Forward" | "Reverse",
                         stopStationIds: string[],            // in stop order, 1..200
                         operatingDays: string[],             // "Monday".."Sunday", 1..7, no repeats
                         effectiveFrom: date, effectiveTo: date | null }
CreateServiceResponse  { id }
WithdrawServiceRequest { withdrawFrom: date }                 // first date the service no longer runs
ServiceResponse        { id, code, nameEn, nameMy, direction,
                         route: { id, code, nameEn, nameMy, isClosed, isActive },
                         stops: ServiceStopResponse[],        // in position order
                         operatingDays: string[],             // Monday first
                         effectiveFrom, effectiveTo, neverRuns, createdAtUtc, withdrawnAtUtc }
ServiceStopResponse    { position, stationId, code, nameEn, nameMy, isActive }
ServiceSummaryResponse { id, code, nameEn, nameMy, routeId, routeCode, direction, stopCount,
                         operatingDays, effectiveFrom, effectiveTo, neverRuns, createdAtUtc,
                         withdrawnAtUtc }
```

Route and station `code`, names and `isActive` are **current** values, read at query time through the Network contract (R30). `effectiveTo` and `withdrawnAtUtc` are `null` until set. No request has an actor field.

**Deliberately absent:** `PATCH /services` and any other edit (R20); reactivation; `DELETE` (R33); `/trains` (R5); `/schedules/versions*` and any time field (R22).

---

## 7. Data

Schema `timetable` (E1). Every `*Utc` column is `datetimeoffset(3)` with `CK_<Table>_<Column>_Utc` (`DATEPART(TZOFFSET, …) = 0`; a null passes), declared in the EF model as well as the migration. Foreign keys are `NO ACTION`.

### `timetable.Services`

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | no | PK, clustered, application-assigned (R23) |
| `Code` | `nvarchar(10)` | no | R6; **not unique** (R35) |
| `NameEn` | `nvarchar(100)` | no | Owned `BilingualName` (R7) |
| `NameMy` | `nvarchar(100)` | no | Myanmar Unicode, never Zawgyi (`docs/20` §6; OQ29 ruling) |
| `RouteId` | `uniqueidentifier` | no | `FK_Services_Routes_RouteId` → `network.Routes(Id)` (R29) |
| `Direction` | `nvarchar(10)` | no | `Forward` or `Reverse` (R9); stored as text so ledger readers and reports need no lookup |
| `RunsOnMonday` … `RunsOnSunday` | `bit` × 7 | no | Operating days (R17, R40) |
| `EffectiveFrom` | `date` | no | R19 |
| `EffectiveTo` | `date` | yes | Inclusive; null = open-ended (R19). EF concurrency token (R36) |
| `CreatedAtUtc` | `datetimeoffset(3)` | no | UTC |
| `WithdrawnAtUtc` | `datetimeoffset(3)` | yes | Null until the first withdrawal; then the latest withdrawal's instant (R36) |

| Object | Definition | Why |
|---|---|---|
| `PK_Services` | clustered on `Id` | ADR-0006 |
| `IX_Services_Code_EffectiveFrom` | nonclustered on `(Code, EffectiveFrom)` | The overlap read (R35) and its lock; also the list order. Deliberately **not unique** |
| `IX_Services_RouteId` | nonclustered on `RouteId` | Covers `FK_Services_Routes_RouteId` and the `routeId` filter |
| `CK_Services_Direction` | `[Direction] IN (N'Forward', N'Reverse')` | R9, from any writer |
| `CK_Services_OperatingDays` | at least one `RunsOn…` column is `1` | R17, from any writer |
| `CK_Services_EffectivePeriod` | `[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom] OR [WithdrawnAtUtc] IS NOT NULL` | A period can be empty only after a withdrawal (R19, R41) |
| `CK_Services_CreatedAtUtc_Utc`, `CK_Services_WithdrawnAtUtc_Utc` | UTC checks | ADR-0018 |

### `timetable.ServiceStops`

| Column | Type | Null | Notes |
|---|---|---|---|
| `ServiceId` | `uniqueidentifier` | no | `FK_ServiceStops_Services_ServiceId` → `timetable.Services(Id)` |
| `Position` | `int` | no | 1-based, contiguous in stop order. Not the route position and not the ADR-0014 short index |
| `StationId` | `uniqueidentifier` | no | `FK_ServiceStops_Stations_StationId` → `network.Stations(Id)` (R29) |

| Object | Definition | Why |
|---|---|---|
| `PK_ServiceStops` | clustered on `(ServiceId, Position)` | One station per position. Contiguity, order, the closure and the minimum are enforced by the aggregate, since a check constraint cannot see other rows |
| `CK_ServiceStops_Position` | `[Position] >= 1` | From any writer |
| `IX_ServiceStops_StationId` | nonclustered on `StationId` | Covers the station foreign key (EF adds it by convention otherwise). Not unique: the full-circuit closure repeats a station (R14) |

Both tables are the history: a service's stops never change, and `EffectiveFrom`, `EffectiveTo` and `WithdrawnAtUtc` answer when it applied without reading the audit ledger.

### Grants to `ycr_app` (`Security_TimetableGrants`)

| Table | Granted | Deliberately absent |
|---|---|---|
| `Services` | `SELECT`, `INSERT`, `UPDATE(EffectiveTo, WithdrawnAtUtc)` | `DELETE`; `UPDATE` of any other column |
| `ServiceStops` | `SELECT`, `INSERT` | `UPDATE`; `DELETE` |

No DDL. If PLAN chooses `sp_getapplock` for R35, no grant is needed (`public` may execute it, as F-002's last-administrator lock does, `docs/07`). `DatabasePrivilegeTests` asserts presences and absences (S47). The Network tables and grants are unchanged.

### Migrations

`yyyyMMddHHmmss_Timetable_CreateServices` (schema `timetable`, both tables, all objects above and the two cross-schema foreign keys; EF model-built), `…_Identity_SeedServicePermissionGrants` (ten grants: `services.manage` × 2, `services.read` × 8, matching `docs/10` §Service permission grants), `…_Security_TimetableGrants`. Reviewed per `docs/workflows/04-database-change.md`. No `network` column, index or key changes (ADR-0025 item 6).

---

## 8. Audit, logging, metrics

- **Audit** (ADR-0017, ADR-0021): through `IAuditWriter`, in the same `SaveChangesAsync` as the change, so a refused or losing request writes no event.

  | Action | When | `BeforeJson` | `AfterJson` |
  |---|---|---|---|
  | `Timetable.ServiceCreated` | S1 | null | snapshot |
  | `Timetable.ServiceWithdrawn` | S30, and each later withdrawal (S35) | snapshot | snapshot |

  `SubjectType` = `Timetable.Service` (new `TimetableAuditSubjects` constant, never a CLR name); `SubjectId` = service id; `AuthorizedByPermission` = `services.manage`; `PayloadVersion` = 1. The snapshot is an explicit record, never the aggregate: `ServiceAuditSnapshot { code, nameEn, nameMy, routeId, routeCode, direction, stops: [{ position, stationId, stationCode }], operatingDays, effectiveFrom, effectiveTo, withdrawnAtUtc }`. Route and station codes are included so a ledger row reads without a join; they are stable because codes are never reused (F-001 R3, F-003 R14). No personal data, token or key (ADR-0021 rule 4).
- **History.** Services are immutable except for `EffectiveTo` and `WithdrawnAtUtc`, so the rows are the history; each withdrawal's before and after dates are also in the ledger. No feature reads the ledger as business history.
- **Logging** (`docs/20` §7): message templates, no personal data.
- **Metrics** (`docs/17`): none; request metrics come from the F-001 baseline.

---

## 9. Out of scope

- Arrival and departure times, dwell, `ScheduleVersion`, publication, `schedules.*` — FR-004, a later feature (B1).
- Trains in any form (OQ42).
- A public-holiday calendar and per-date operating exceptions (OQ47, still open with Myanma Railways).
- Service type or class (OQ43; fares, OQ9/OQ17).
- Editing a service in any way other than withdrawal; reactivation; deletion (OQ48, E11).
- Any change to station or route endpoints (OQ46).
- Ticketing, ticket validation, binding tickets to services (OQ4, OQ19), the QR payload.
- Real service data (OQ1, R27); real-time train tracking (`docs/00`); the ADR-0014 station short index.
- Editing `docs/business/mr-questions-pack.md` and `docs/glossary.md` in stages 1–2; stage 8 updates the glossary, `docs/07` and `docs/08` per `docs/21`.

**Known limitations (accepted by the rulings, hein, 2026-09-25):**
- **OQ46:** a service keeps its route and stops after either is deactivated. It stays readable and withdrawable, and its reads show the inactive flags. Nothing withdraws it automatically.
- **OQ47:** a service runs on its days of the week throughout its period, public holidays included; no single date can be added or cancelled. Until Myanma Railways answers, an exceptional pattern needs its own service, with its own code while periods overlap (R35).
- **OQ48:** a mistake in a service can be corrected only by withdrawing it and creating a new one; a code whose service is open-ended cannot be reused until that service is withdrawn.
- **Overlap compares periods only (accepted consequence, hein, 2026-09-25):** services with the same code cannot run at the same time in both directions, or as weekday and weekend variants; they need different codes (R35).
- **Q2 (hein, 2026-09-25):** the 200-stop cap means a full circuit of a route with 200 stations (201 stops) is not supported — well beyond YCR's size (R31).
- **OQ50 (hein, 2026-09-25):** a service entered late may carry a past `EffectiveFrom`; the dates it ran before `CreatedAtUtc` are asserted by whoever entered it, and routes have no effective dates to check them against (F-003 R22).

---

## Blocked behaviour

Required by `docs/21` §Specification.

| Behaviour | Blocking item | Effect on F-004 |
|---|---|---|
| Public holidays and per-date exceptions | **OQ47** (open part, still open with Myanma Railways) | Not implemented, no placeholder; known limitation (§9) |
| Times, timetable versions, publication | FR-004 (B1) | Out of scope |
| Real service data | **OQ1** | No seed; tests build their own (R27) |
| Fare direction and service type | **OQ17**, **OQ9** | Out of scope |
| Official Myanma Railways answers to OQ42–OQ50 | **Still open with Myanma Railways** | Not blocking: provisional tech-lead rulings apply (§0.9). An official, different answer supersedes the ruling and needs its own follow-up task |

No placeholder rule may be implemented for any of these (AGENTS.md §When a business rule is missing).

---

## Notes for the next stage

- Stage 3 (PLAN, `docs/templates/plan.md`) may start: this spec is Approved (hein, 2026-09-25). It is a separate task (T-045) and is not started by T-044.
- PLAN chooses the R35 lock (application lock or key-range lock), proves it with S27–S29 and S37, and sets the body-size limits (R31).
- PLAN adds the ADR-0025 item 4 architecture rule and its violating fixture before the first Timetable handler, together with the Network contract (E3) and its `internal` implementation.
- PLAN extends `IdentitySeedTests` to parse `docs/10` §Service permission grants (today it reads only the station, route and identity sections) and moves its grant count from 24 to 34.
- PLAN verifies how EF Core maps the cross-schema foreign keys without navigations (for example `HasOne<Route>().WithMany()` in `Configurations/Timetable`) and that `has-pending-model-changes` sees them.
- Stage 8: `docs/07`, `docs/08` (remove `/trains` and `PATCH /services`, add the service endpoints), the glossary (`Service`, `ServiceStop`, direction, operating days, effective period, withdrawal; C7) and ADR-0025's `docs/20` §1 follow-up.
- Add OQ42–OQ50 to `docs/business/mr-questions-pack.md` (hein; listed in `progress.md`), all marked as provisionally ruled.

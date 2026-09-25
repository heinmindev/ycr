# F-004: Service management

Status: **Draft** — stage 2 ⛔, awaiting hein's rulings (§0.9). Written by claude (T-044). Stages 1–2 of `docs/workflows/02-feature-development.md`. **Not approvable as written:** OQ42–OQ49 are blocking OPEN QUESTIONs and the FR-003/FR-004 boundary (B) is unruled.

Module(s): `Timetable` (new; first feature in it); read-only consumer of `Network` through a contract; cross-cutting `Audit`, `Identity` (permission grants only)
Related: FR-003, FR-004 (boundary only), UC 3 "Manage trains" and UC 4 "Publish schedules" (`docs/03-use-cases.md` §Core use cases, items 3–4), FR-002 / F-003 (routes), ADR-0002, ADR-0004, ADR-0006, ADR-0012, ADR-0014, ADR-0017, ADR-0018, ADR-0019, ADR-0021, ADR-0024 (Proposed), ADR-0025 (Proposed, this task)

Decision owner:
- Business rules (what a train and a service are, service identity, direction and extent, stopping patterns, interaction with inactive routes and stations, operating days, effective periods and change, role grants): **Myanma Railways**, routed through `hein` (`docs/19-open-questions.md` OQ42–OQ49). Suggested routing label, following `docs/business/mr-questions-pack.md`: Network Operations / Planning (timetabling role to be nominated). hein may instead give provisional tech-lead rulings, labelled "not a Myanma Railways answer", as for OQ36–OQ41.
- Scope boundary between FR-003 and FR-004, and engineering decisions: **tech lead (`hein`)**, recorded as ADRs per `docs/decisions/README.md`.

Authoritative sources: `docs/00-project-specification.md`; `docs/01-functional-requirements.md` §FR-003, §FR-004; `docs/03`; `docs/04`; `docs/05`; `docs/07`; `docs/08`; `docs/10`; `docs/12`; `docs/19`; `docs/20`; `docs/glossary.md`; ADR-0002/0004/0006/0012/0014/0017/0018/0019/0021; the F-003 spec and the existing `Network` code. **No Myanma Railways-authoritative source defines any train, service, stopping pattern, operating calendar, effective-period rule or timetable permission grant.**

---

## 0. Discovery notes

Stage-1 output. Every note cites its source and names the decision owner. Where a note lists options, they are non-binding; §0.9 is the table hein rules on.

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

### 0.8 Where the existing code constrains the design

- No `Timetable` folder exists in any layer; F-004 creates the module (E1). `DependencyInjection.AddInfrastructure` registers each module interface against `YcrDbContext` (`src/YCR.Infrastructure/DependencyInjection.cs`); `ITimetableDbContext` is added the same way.
- `Route.Create` takes the facts it needs (station existence and activity) from its handler and decides their meaning itself (F-003 plan P4). `Service.Create` can do the same with a `RouteReference` from the contract, so every pattern rule is testable without a database.
- `INetworkDbContext` has no `DbSet<RouteStation>`; the contract implementation reads the sequence through `Routes` as `GetRouteHandler` does (`src/YCR.Application/Network/GetRoute/GetRouteHandler.cs`).
- `NetworkConstraints` / `RouteModelTests` pattern for constraint names a handler matches; `TimetableConstraints` would follow it.
- `DeactivateRouteHandler` and `DeactivateStationHandler` change only if OQ46 rules "refuse" — which would also need the reverse contract (G5).

### 0.9 Rulings needed from hein

| ID | Question | Options (non-binding) | Blocks |
|---|---|---|---|
| **B** | Where does F-004 (FR-003) end and FR-004 begin? | B1 service master data only · B2 services inside draft versions, no publish · B3 merge FR-003 and FR-004 · B4 trains first (§0.7) | The whole of §5–§7; which of OQ42–OQ49 must be ruled now; whether ADR-0024 and `schedules.*` are needed now |
| **OQ42** | What is a train, and does Phase 1 record trains separately from services? | (a) physical rolling stock assigned to services · (b) a numbered working = a service; drop `Train`/`/trains`/`trains.manage` · (c) not in Phase 1 | R5; `Train` aggregate, `/trains`, `trains.*`, `Trains` table; C1 |
| **OQ43** | What identifies a service, and does it have names and a type? | (a) code/number under the station and route code rules (2–10 `A`–`Z`/`0`–`9`, unique across all services, never reused) plus `BilingualName` · (b) number unique only per route and direction · (c) number unique per effective period (reusable later) · type: none / a fixed list / defer to fares (OQ17) | R6, R7; §7 columns and unique index |
| **OQ44** | How is direction and extent expressed? | (a) explicit direction (in route order / reverse) plus start and end stations · (b) derived from the ordered stops only · closed route: full circuit back to start allowed or not; more than one circuit allowed or not · open route: reverse and part-route allowed or not | R9, R10 |
| **OQ45** | What are the stopping-pattern rules? | Stops ⊆ route stations: yes / no · in route order in the service's direction: yes / no · skipping: allowed / not · first and last stops = start and end: yes / no · minimum stops: 2 / other · one route per service: yes / no · stop attributes: none / set-down / pick-up / request | R11–R14; `ServiceStops` shape |
| **OQ46** | Services versus inactive routes and stations? | Creation: refuse inactive route/stop (OQ39 analogue) / allow · on deactivation: allow, service unchanged (one-way module dependency) / refuse while services use it (two-way dependency) / withdraw services automatically | R15, R16; any change to F-001/F-003 deactivation |
| **OQ47** | On which days does a service run? | (a) days of the week only · (b) days of the week + a holiday calendar (owner to name) with runs/doesn't-run-on-holidays · (c) days of the week + per-date exceptions · (d) explicit list of dates · operating day = date of leaving the first stop: yes / no | R17, R18; §7 |
| **OQ48** | Effective period, change and versioning? | (a) immutable like routes: create whole, withdraw with an end date, change = new service · (b) versioned inside timetable versions (draft → published, immutable once published) · (c) edited in place, history in the ledger only · end date: required / open · early withdrawal, reactivation: allowed / not · overlap of two definitions of one service: allowed / not | R19–R21; §5; §6 (`PATCH`?); §7 grants; E10 |
| **OQ49** | Which roles manage and read services (and trains, timetable versions)? | e.g. `services.manage` → `SystemAdministrator`, `RailwayAdministrator`; `services.read` → all eight roles (the OQ40 pattern) — or narrower | R3, R4; §2; grant seed |
| **E3–E5 / ADR-0025** | Accept ADR-0025: primitive-record Network contract, Contracts architecture rule, `NO ACTION` foreign keys from `timetable` to `network` primary keys? | Accept · amend (e.g. no cross-schema FKs, option B1) · reject | R28, R29; §7 foreign keys |
| **E6** | Add `services.read` (and `trains.read` / `schedules.read` where in scope) to `docs/10`? | Accept · other names | R4 |
| **E11** | No hard delete of services; stable ids, whatever OQ48 rules? | Accept · reject | R33; §7 grants |
| **E1, E2, E7–E10, E12, E13** | Accept the remaining engineering proposals? | Accept · amend | §6, §7, §8 |

---

## 1. Goal

Railway administrators need to define the services that run over the network — which route, which direction, which stations they stop at, on which days and over which period — so that later features (timetable publication, ticketing, reporting) have one authoritative place to read services from, and every change is authorised, validated and audited.

---

## 2. Actors and permissions

| Actor | Permission | Held by |
|---|---|---|
| Staff user managing services | `services.manage` | **OPEN QUESTION — OQ49** |
| Staff user reading services | `services.read` (name proposed, E6) | **OPEN QUESTION — OQ49** |
| Staff user managing trains | `trains.manage` — only if OQ42 keeps trains | **OPEN QUESTION — OQ42, OQ49** |
| Staff user managing timetable versions | `schedules.manage` — not in F-004 under B1/B4 | **OPEN QUESTION — B, OQ49** |

Holding a station or route permission gives no service right (F-003 R3 pattern). Grants are data seeded by a reviewed migration; no API edits them.

---

## 3. Business rules

| # | Rule | Label | Source |
|---|---|---|---|
| R1 | Authorized users can define train services, operating days, effective periods and stopping patterns. | FACT | `docs/01` §FR-003 |
| R2 | Authorized users can publish timetable versions with effective dates. How much of this is in F-004 is ruling B. | FACT; scope: **OPEN — ruling B (hein)** | `docs/01` §FR-004; §0.7 |
| R3 | Managing services requires `services.manage`. Which roles hold it is not decided. | Name: FACT. Grants: **OPEN QUESTION (OQ49)** | `docs/10` §Permission inventory |
| R4 | Reading services requires a read permission, proposed `services.read`. Station and route permissions give no service right. | Name: ENGINEERING DECISION proposed (E6). Grants: **OPEN QUESTION (OQ49)** | `docs/10`; F-003 R3 precedent |
| R5 | Whether a `Train` exists separately from a `Service`, and what it is. | **OPEN QUESTION (OQ42)** | `docs/19` OQ42; C1 |
| R6 | A service's identifier: number or code, format, uniqueness scope, reuse. | **OPEN QUESTION (OQ43)** | `docs/19` OQ43 |
| R7 | A service's names and type/class. | **OPEN QUESTION (OQ43)** | `docs/19` OQ43; `docs/12` |
| R8 | A service belongs to the `Timetable` module and references its route and stations by identifier only. | FACT (`docs/05`); ENGINEERING DECISION (ADR-0012 items 2, 6; E1, E2) | `docs/05`; ADR-0012 |
| R9 | A route has no direction; direction belongs to services. How a service expresses it. | "belongs to services": **BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-24; OQ36), not a Myanma Railways answer**. How: **OPEN QUESTION (OQ44)** | `docs/19` OQ36, OQ44 |
| R10 | A service's start and end stations and extent (part route, full circuit, several circuits). | **OPEN QUESTION (OQ44)** | `docs/19` OQ44 |
| R11 | Whether every stop must be a station of the service's route, and whether a service may use more than one route. | **OPEN QUESTION (OQ45)** | `docs/19` OQ45 |
| R12 | Whether stops must follow the route's order in the service's direction. | **OPEN QUESTION (OQ45)** | `docs/19` OQ45 |
| R13 | Skipping stations; first and last stops; minimum number of stops. | **OPEN QUESTION (OQ45)** | `docs/19` OQ45 |
| R14 | Repeated stops and stop attributes (set-down, pick-up, request). | **OPEN QUESTION (OQ45)** | `docs/19` OQ45 |
| R15 | Whether a service may be defined on an inactive route or with an inactive stop. | **OPEN QUESTION (OQ46)** | `docs/19` OQ46; OQ39 analogue |
| R16 | What deactivating a route or station does to services that use it. Until ruled, F-001's and F-003's deactivation endpoints are unchanged. | **OPEN QUESTION (OQ46)** | `docs/19` OQ46 |
| R17 | On which days a service runs: days of week, holidays, per-date exceptions. | **OPEN QUESTION (OQ47)** | `docs/19` OQ47 |
| R18 | Operating days are calendar dates (`DateOnly`/`date`) in the configured Asia/Yangon zone, never hard-coded. Which date counts as a service's operating day is OQ47. | Storage and zone: ENGINEERING DECISION (ADR-0018 §Time; E9). Which date: **OPEN QUESTION (OQ47)** | ADR-0018 |
| R19 | A service's effective period: start required, end optional, inclusive or not. Stored as `EffectiveFrom`/`EffectiveTo` `date`. | Shape: **OPEN QUESTION (OQ48)**. Storage: ENGINEERING DECISION (ADR-0018; E9) | `docs/19` OQ48; ADR-0018 |
| R20 | Whether a service can change after it is defined, and whether earlier definitions are kept (immutable / versioned / edited in place). Historical schedule versions are preserved and published timetable versions are immutable. | Preservation and immutability of published versions: FACT. Services' change model: **OPEN QUESTION (OQ48)** | `docs/07` §Rules; `docs/08`; `docs/20` §4; `docs/19` OQ48 |
| R21 | Withdrawal, reactivation and overlap of service definitions. | **OPEN QUESTION (OQ48)** | `docs/19` OQ48 |
| R22 | Arrival and departure times per stop are not in F-004 under B1, B2 and B4. | **OPEN — ruling B (hein)** | §0.7 |
| R23 | Service identifiers are application-generated GUIDs through `IIdGenerator`, mapped `ValueGeneratedNever()`. | ENGINEERING DECISION | ADR-0006 §Decision item 1 and amendment |
| R24 | Every accepted service change is audited in the same `SaveChangesAsync`, with actor fields from the authenticated server-side context only. | ENGINEERING DECISION | ADR-0017 §Decision items 1–2; ADR-0021; F-003 R20 |
| R25 | Service management takes no `Idempotency-Key`. | ENGINEERING DECISION (E8) | `docs/20` §5 |
| R26 | Instants are `DateTimeOffset` / `datetimeoffset(3)` UTC with a UTC check constraint; services own no `BusinessDate`. | ENGINEERING DECISION | ADR-0018; ADR-0019; `docs/20` §2 |
| R27 | No real service data ships: no seed migration or fixture of real YCR services. Tests build their own stations, routes and services. OQ1 blocks data, not the service API. | FACT (consequence of OQ1, open) | `docs/19` OQ1; F-003 R23 precedent; C6 |
| R28 | Timetable reads Network data only through a read-only `YCR.Application.Network.Contracts` interface returning primitive records; it never uses `INetworkDbContext` or `YCR.Domain.Network`. | ENGINEERING DECISION (ADR-0012 item 4); contract shape proposed (ADR-0025 Proposed; E3, E4) | ADR-0012; ADR-0025 |
| R29 | `timetable` rows reference `network.Routes(Id)` and `network.Stations(Id)` by `NO ACTION` foreign keys. | ENGINEERING DECISION proposed (ADR-0025 Proposed; E5) — **pending hein** | ADR-0025; `docs/07` §Rules |
| R30 | Service responses show the route's and each stop station's **current** code, names and active flag, read at query time through the contract; a service row stores only ids. | ENGINEERING DECISION proposed | F-003 R24 precedent; E3 |
| R31 | Every body-carrying service endpoint has an explicit body-size limit and every array a maximum length (stops ≤ 200), refused before binding. | REQUIRED CONTROL proposed (E12) | F-003 R26/R27; `docs/18` |
| R32 | Error codes are `Timetable.<Reason>`, mapped per ADR-0004. | ENGINEERING DECISION (E7) | ADR-0004; `docs/20` §2 |
| R33 | Service rows are never deleted and ids never change; there is no `DELETE` endpoint and `ycr_app` has no `DELETE` on `timetable` tables. | ENGINEERING DECISION proposed (E11) — **pending hein** | AGENTS.md rule 5; `docs/07` §Rules; G11 |
| R34 | OQ4 and OQ19 do not block F-004; R33 keeps OQ4's "service-specific ticket" option open. | FACT (analysis, G11) | `docs/19` OQ4, OQ19; ADR-0014 |

**Blocking open questions:** **OQ42, OQ43, OQ44, OQ45, OQ46, OQ47, OQ48, OQ49**, and the scope ruling **B**. Under B3, further business questions about times would have to be raised before approval. The spec cannot be Approved until each is ruled — by Myanma Railways, or by hein as a provisional tech-lead ruling labelled "not a Myanma Railways answer".

---

## 4. Scenarios (Given / When / Then)

Written against boundary **B1** (§0.7). Status codes follow ADR-0004; every error is ProblemDetails with `errorCode` and `traceId`. Each scenario names the OQs whose ruling fixes its final form; a scenario tagged **[blocked: …]** is a placeholder for the rule, not a rule. Error-code names are proposals (E7).

### Happy path

- **S1.** *[blocked: OQ43, OQ44, OQ45, OQ47, OQ48]* Given an active route R with active stations, and a caller holding `services.manage`, when they POST `/services` with the identity fields (OQ43), `routeId = R`, a direction (OQ44), an ordered list of stop station ids, the operating days (OQ47) and the effective period (OQ48), then `201 Created` with `Location: /api/v1/services/{id}` and `{ id }`; one `timetable.Services` row and one `timetable.ServiceStops` row per stop, in order `1..n`; one `Timetable.ServiceCreated` audit event.
- **S2.** Given that service, a caller holding `services.read` GETs `/services/{id}` → `200` with `ServiceResponse`: the service's fields, the route's current code and names, and the stops in order, each with the station's current code, names and `isActive` (R30). No EF entity is serialised (AGENTS.md rule 4).
- **S3.** Given several services, one of them withdrawn, a caller holding `services.read` GETs `/services?page=1&pageSize=50` → `200` with `items, page, pageSize, totalCount`, in a stable order (proposed: by identifier, OQ43), withdrawn services included and marked. A `routeId` filter is proposed.

### Failures, each with its error code

- **S4.** Validation → `400 Common.ValidationFailed`: a missing required field; an empty stop list; a non-GUID id; more than 200 stops (R31); a missing effective start date *[blocked: OQ48 whether required]*; an empty operating-day set *[blocked: OQ47]*. Nothing is read or written.
- **S5.** Unknown `routeId` → `422 Timetable.ServiceRouteNotFound`; nothing written (the addressed resource is the new service, so not `404`; F-003 E8 precedent).
- **S6.** *[blocked: OQ46]* Inactive route → `422 Timetable.ServiceRouteInactive`, **if** OQ46 refuses; otherwise `201`.
- **S7.** Unknown stop station id → `422 Timetable.ServiceStopStationNotFound`; nothing written.
- **S8.** *[blocked: OQ46]* Inactive stop station → `422 Timetable.ServiceStopStationInactive`, **if** OQ46 refuses.
- **S9.** *[blocked: OQ45]* A stop that is not a station of the route → `422 Timetable.ServiceStopNotOnRoute`, **if** OQ45 requires stops ⊆ route.
- **S10.** *[blocked: OQ44, OQ45]* Stops out of route order for the stated direction → `422 Timetable.ServiceStopsOutOfOrder`, **if** OQ45 requires order. Cases to pin once ruled: on a closed route `[A, B, C, D]`, stops `[C, D, A]` in route order wrap past the end (valid if order is cyclic); `[C, A, D]` is out of order; reverse direction reads the route backwards.
- **S11.** *[blocked: OQ45]* Too few stops → `422 Timetable.ServiceTooFewStops`; a repeated stop → `422 Timetable.ServiceStopRepeated` unless OQ44/OQ45 allow a full circuit.
- **S12.** *[blocked: OQ48]* `EffectiveTo` before `EffectiveFrom` → `400 Timetable.InvalidEffectivePeriod`.
- **S13.** *[blocked: OQ43]* Duplicate identifier within its uniqueness scope → `409 Timetable.ServiceCodeAlreadyExists`, also for the loser of two parallel creates (unique-index violation, ADR-0004).
- **S14.** *[blocked: OQ43]* Malformed identifier or name → `400 Timetable.InvalidServiceCode` / `400 Timetable.InvalidServiceName`.
- **S15.** GET or withdraw an unknown service id → `404 Timetable.ServiceNotFound`.
- **S16.** `pageSize=201` → `400 Timetable.InvalidPageRequest` (`docs/20` §4).
- **S17.** Anonymous request to any `/services` endpoint → `401 Auth.Unauthenticated`.
- **S18.** Authenticated without the permission → `403`: only `services.read` POSTs `/services`; only `routes.manage` or `routes.read` or `stations.*` POSTs or GETs `/services` (R4).
- **S19.** A body over the endpoint's limit → `413` before JSON binding, framework ProblemDetails with no stack trace or path; malformed JSON → bounded `400` (R31; F-003 R27).

### Lifecycle

- **S20.** *[blocked: OQ48]* Withdraw (proposed `POST /services/{id}/withdraw`, under G7 (a)) an active service → `204`, the row keeps its id and stops, the withdrawal date or instant is recorded, one `Timetable.ServiceWithdrawn` event; again → `422 Timetable.ServiceAlreadyWithdrawn`, no event; two in parallel → exactly one `204`, one `422`, one event (F-003 S23 pattern).
- **S21.** *[blocked: OQ46]* Given service S uses route R and station B, when R is deactivated (F-003) or B is deactivated (F-001), then — under "allow, service unchanged" — `204` exactly as today, S's rows are unchanged, and S2 shows the route or stop with `isActive = false`.

### Concurrency and data integrity

- **S22.** An audit event's actor fields come from the authenticated context; actor fields in the request body are ignored (ADR-0017 §2).
- **S23.** A failed create writes neither the service nor any stop row (one `SaveChangesAsync`, ADR-0004); a successful one has stops contiguous `1..n`.
- **S24.** *[pending ADR-0025]* A stop row naming a station id that does not exist is refused by the database even when written by direct SQL under `ycr_app` (the foreign key, R29); likewise a service naming a missing route.
- **S25.** Database privileges: `ycr_app`'s grants on `timetable.Services` and `timetable.ServiceStops` are exactly those §7 lists, and a test asserts the absences — no `DELETE`, no `UPDATE` of identity, route or stop columns, no DDL (F-003 S25 pattern; final list after OQ48).
- **S26.** A Myanmar service name (if OQ43 gives services names) round-trips unchanged with real Myanmar Unicode text.
- **S27.** Architecture: `YCR.Application.Timetable` depends on no `YCR.Domain.Network` type and not on `INetworkDbContext`; a type in `YCR.Application.Network.Contracts` depends on no module domain type (E4), each proven with a violating fixture.
- **S28.** *[accept or refuse the race, hein]* A service created while its route (or a stop station) is being deactivated: the end state equals the serial order "create, then deactivate". Proposed: accepted, not serialised, as F-003 accepted route creation against station deactivation (F-003 §5).

---

## 5. State changes

Under boundary B1 and G7 (a). Everything here is *[blocked: OQ48]*.

| Entity | From | Event | Guard | To |
|---|---|---|---|---|
| Service | (none) | `CreateService` | `services.manage`; identity valid and unused (OQ43); route exists (R28) and is active (OQ46); stops valid for the route and direction (OQ44, OQ45); operating days valid (OQ47); effective period valid (OQ48) | Active |
| Service | Active | `WithdrawService` | `services.manage`; OQ48 allows early withdrawal | Withdrawn, withdrawal recorded |
| Service | Withdrawn | `WithdrawService` | — | rejected, `422 Timetable.ServiceAlreadyWithdrawn` |
| Route / Station | Active | `DeactivateRoute` / `DeactivateStation` | unchanged from F-003 / F-001 unless OQ46 rules otherwise | Inactive; services unchanged (OQ46 option "allow") |

Under G7 (b) the table is replaced by a `ScheduleVersion` lifecycle (Draft → Published, immutable once published), which is FR-004's under B1.

---

## 6. API

Proposal under **B1**. Base path `/api/v1`, JSON camelCase, GUID ids, ProblemDetails with `errorCode` and `traceId` (`docs/20` §4; ADR-0004). No `Idempotency-Key` (R25). Field names in *italics* wait for their OQ.

| Method | Path | Request | Success | Error codes | Permission |
|---|---|---|---|---|---|
| POST | `/services` | `CreateServiceRequest { `*`code`*`, `*`nameEn`*`, `*`nameMy`*`, routeId, `*`direction`*`, stopStationIds: Guid[] (≤ 200), `*`operatingDays`*`, effectiveFrom, `*`effectiveTo`*` }`; explicit body limit (R31) | `201` + `CreateServiceResponse { id }` and `Location` | `400 Common.ValidationFailed` · `400 Timetable.InvalidServiceCode` · `400 Timetable.InvalidServiceName` · `400 Timetable.InvalidEffectivePeriod` · `400` malformed JSON · `401` · `403` · `409 Timetable.ServiceCodeAlreadyExists` · `413` · `422 Timetable.ServiceRouteNotFound` · `422 Timetable.ServiceRouteInactive` · `422 Timetable.ServiceStopStationNotFound` · `422 Timetable.ServiceStopStationInactive` · `422 Timetable.ServiceStopNotOnRoute` · `422 Timetable.ServiceStopsOutOfOrder` · `422 Timetable.ServiceStopRepeated` · `422 Timetable.ServiceTooFewStops` — each conditional on its OQ (§4) | `services.manage` |
| GET | `/services/{id}` | — | `200` + `ServiceResponse { id, `*`code, nameEn, nameMy`*`, route: { id, code, nameEn, nameMy, isClosed, isActive }, `*`direction`*`, stops: [{ position, stationId, code, nameEn, nameMy, isActive }], `*`operatingDays`*`, effectiveFrom, `*`effectiveTo`*`, isActive, createdAtUtc, `*`withdrawnAt…`*` }` | `401` · `403` · `404 Timetable.ServiceNotFound` | `services.read` |
| GET | `/services` | `?page=1&pageSize=50` (max 200); proposed `&routeId=` filter | `200` + `{ items: ServiceSummaryResponse[], page, pageSize, totalCount }` | `400 Timetable.InvalidPageRequest` · `401` · `403` | `services.read` |
| POST | `/services/{id}/withdraw` | *[blocked: OQ48]* possibly `{ effectiveTo }` | `204` | `401` · `403` · `404 Timetable.ServiceNotFound` · `422 Timetable.ServiceAlreadyWithdrawn` | `services.manage` |

**Deliberately absent under B1 and G7 (a):** `PATCH /services` (C3), `DELETE` (R33), any times. **Trains:** `GET/POST /trains` exist only if OQ42 is (a); under (b) or (c) `docs/08`'s `/trains` line is removed at stage 8. **Schedules:** `/schedules/versions*` are FR-004's under B1.

---

## 7. Data

Proposal under **B1** and G7 (a). Schema `timetable` (E1). Every `*Utc` column is `datetimeoffset(3)` with `CK_<Table>_<Column>_Utc`, declared in the EF model as well as the migration. Foreign keys are `NO ACTION`. Italic columns wait for their OQ.

### `timetable.Services`

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | no | PK, clustered, application-assigned (R23) |
| *`Code`* | `nvarchar(n)` | *OQ43* | Identifier; unique index scope per OQ43 |
| *`NameEn`*, *`NameMy`* | `nvarchar(100)` | *OQ43* | `BilingualName`, if services have names |
| `RouteId` | `uniqueidentifier` | no | `FK_Services_Routes_RouteId` → `network.Routes(Id)` *(ADR-0025, pending)*; indexed for the `routeId` filter |
| *`Direction`* | `tinyint` | *OQ44* | With a `CK_` listing the allowed values, if direction is stored |
| *operating days* | e.g. seven `bit` columns `RunsMonday`…`RunsSunday` with a check that at least one is set | *OQ47* | Holiday or per-date exception tables if OQ47 (b)/(c)/(d) |
| `EffectiveFrom` | `date` | *OQ48* | ADR-0018 |
| `EffectiveTo` | `date` | *OQ48* | `CK_Services_EffectivePeriod`: `EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom` |
| `IsActive` | `bit` | no | EF concurrency token for withdrawal (E10) |
| `CreatedAtUtc` | `datetimeoffset(3)` | no | UTC check |
| *`WithdrawnAtUtc`* | `datetimeoffset(3)` | yes | *OQ48*; UTC check; set in the same `UPDATE` as `IsActive = 0` (F-003 A1 pattern) |

### `timetable.ServiceStops`

| Column | Type | Null | Notes |
|---|---|---|---|
| `ServiceId` | `uniqueidentifier` | no | `FK_ServiceStops_Services_ServiceId` |
| `Position` | `int` | no | 1-based, contiguous per service; `CK_ServiceStops_Position` `>= 1`. Not the route position and not the ADR-0014 short index |
| `StationId` | `uniqueidentifier` | no | `FK_ServiceStops_Stations_StationId` → `network.Stations(Id)` *(ADR-0025, pending)* |
| *stop attributes* | | | *OQ45* |

PK `(ServiceId, Position)`. A unique `(StationId, ServiceId)` only if OQ44/OQ45 forbid repeated stops; with `StationId` leading it also covers the station foreign key (F-003 Amendment 1 lesson: otherwise EF adds `IX_ServiceStops_StationId` by convention). Order-against-route is enforced by the aggregate, not the database (E5).

### Trains

`timetable.Trains` only if OQ42 is (a); columns wait for that ruling.

### Grants to `ycr_app` (`Security_TimetableGrants`, E13)

Under G7 (a): `Services` `SELECT`, `INSERT`, `UPDATE(IsActive, WithdrawnAtUtc)` only; `ServiceStops` `SELECT`, `INSERT` only. No `DELETE` (R33), no other `UPDATE`, no DDL. Under (b) the set is wider for draft rows and must be designed with FR-004.

Migrations `yyyyMMddHHmmss_Timetable_<Change>`, `…_Identity_SeedServicePermissionGrants`, `…_Security_TimetableGrants`, reviewed per `docs/workflows/04-database-change.md`. Adding the `timetable` schema and its foreign keys alters no `network` column or index (ADR-0025 item 6 rules out alternate keys on Network tables).

---

## 8. Audit, logging, metrics

- **Audit** (ADR-0017, ADR-0021): through `IAuditWriter`, in the same `SaveChangesAsync` as the change, so a losing concurrent request writes no event.

  | Action | When | `BeforeJson` | `AfterJson` |
  |---|---|---|---|
  | `Timetable.ServiceCreated` | S1 | null | snapshot |
  | `Timetable.ServiceWithdrawn` | S20 *[OQ48]* | snapshot | snapshot |
  | `Timetable.TrainCreated` … | only if OQ42 (a) | | |

  `SubjectType` = `Timetable.Service` (new `TimetableAuditSubjects` constant, never a CLR name); `SubjectId` = service id; `AuthorizedByPermission` = `services.manage`; `PayloadVersion` = 1. Snapshot is an explicit record: `ServiceAuditSnapshot { `*`code, nameEn, nameMy`*`, routeId, routeCode, `*`direction`*`, stops: [{ position, stationId, stationCode }], `*`operatingDays`*`, effectiveFrom, `*`effectiveTo`*`, isActive, `*`withdrawnAtUtc`*` }`. Route and station codes are included so a ledger row reads without a join; they are stable because codes are never reused. No personal data, token or key (ADR-0021 rule 4).
- **History.** Under G7 (a) the rows are the history, as for routes (F-003 §8): no feature reads the ledger as business history.
- **Logging** (`docs/20` §7): message templates, no personal data.
- **Metrics** (`docs/17`): none proposed; request metrics come from the F-001 baseline.

---

## 9. Out of scope

- Arrival and departure times, `ScheduleVersion`, publication, `schedules.*` — FR-004 under B1 (ruling B decides).
- Fares and direction pricing (OQ17); "service type" beyond asking whether it exists (OQ43).
- Ticketing, ticket validation, binding tickets to services (OQ4, OQ19), the QR payload.
- Real service or train data (OQ1, R27).
- Real-time train tracking (`docs/00`).
- The ADR-0014 station short index.
- Any change to station or route endpoints, unless OQ46 rules "refuse".
- Editing `docs/business/mr-questions-pack.md`, `docs/glossary.md` and `docs/10-authorization-matrix.md` in stages 1–2 (T-044 instructions; stage 8 updates the glossary and `docs/10` per `docs/21`).

---

## Blocked behaviour

Required by `docs/21` §Specification.

| Behaviour | Blocking item | Effect on F-004 |
|---|---|---|
| Scope of the feature | **Ruling B** | §5–§7 are written against B1 and change with the ruling |
| Trains | **OQ42** | No `Train` aggregate, endpoint or table until ruled |
| Service identity, names, type | **OQ43** | No identifier format or unique index until ruled |
| Direction and extent | **OQ44** | No direction attribute or order check until ruled |
| Stopping-pattern validation | **OQ45** | No stop rule beyond "stations exist" until ruled |
| Inactive routes and stations | **OQ46** | No refusal or cascade; F-001/F-003 deactivation unchanged |
| Operating days | **OQ47** | No operating-day model until ruled |
| Effective period, change, withdrawal | **OQ48** | No change model, `PATCH` or withdrawal until ruled |
| Role grants | **OQ49** | No grant seed until ruled |
| Cross-schema foreign keys, contract shape | **ADR-0025** (Proposed) | R28 stands (ADR-0012); R29 and E4 wait for acceptance |
| Real service data | **OQ1** | No seed; tests build their own (R27) |
| Fare direction | **OQ17** | Out of scope |

No placeholder rule may be implemented for any of these (AGENTS.md §When a business rule is missing).

---

## Notes for the next stage

- Stage 2 continues after hein's rulings: a revision applies them to §0.9 (as F-003 §0.8), relabels §3, makes §4–§8 definite and removes what the rulings remove; `docs/19` gets a resolution block under each ruled OQ, and `docs/10` the approved grants and read-permission names. Then hein approves. Stage 3 (PLAN) must not start before that.
- If B3 is chosen, discovery must first raise the timing questions (times per stop, overnight running, publication lead time, who publishes) as new OQs.
- If ADR-0025 is accepted, PLAN adds the Contracts architecture rule (E4) before the first Timetable handler, so the rule is proven on a violating fixture while the real contract is still small.
- Add OQ42–OQ49 to `docs/business/mr-questions-pack.md` (hein; listed in `progress.md`).

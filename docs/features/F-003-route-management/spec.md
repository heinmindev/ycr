# F-003: Route management

Status: **Draft** (claude, T-032, 2026-09-24). Stages 1–2 of `docs/workflows/02-feature-development.md`. **Not approvable as written:** six blocking OPEN QUESTIONs (OQ36–OQ41) need a ruling first; see §0.8.

Module(s): `Network` (routes, and a possible change to station deactivation); cross-cutting `Audit`, `Identity` (permission grants only)
Related: FR-002, UC 2 "Manage routes" (`docs/03-use-cases.md` §Core use cases, item 2), FR-001 (stations), ADR-0004, ADR-0006, ADR-0012, ADR-0014, ADR-0017, ADR-0018, ADR-0021, ADR-0024 (Proposed), `docs/20-coding-conventions.md` §3

Decision owner:
- Business rules (which routes exist, sequence shape, history, interaction with inactive stations, route identity and lifecycle, role grants): **Myanma Railways**, routed through `hein` (`docs/19-open-questions.md` OQ36–OQ41). Suggested routing label, following `docs/business/mr-questions-pack.md`: Network Operations / Planning (role to be nominated). If hein chooses not to wait, a ruling is a **provisional tech-lead ruling — not a Myanma Railways answer**, in the OQ26–OQ28 and OQ34 pattern.
- Engineering decisions: **tech lead (`hein`)**, recorded as ADRs per `docs/decisions/README.md`.

Authoritative sources: `docs/01-functional-requirements.md` §FR-002; `docs/03-use-cases.md`; `docs/04-domain-model.md`; `docs/05-bounded-contexts.md`; `docs/07-database-design.md`; `docs/08-api-specification.md`; `docs/10-authorization-matrix.md`; `docs/glossary.md`; ADR-0004/0006/0012/0014/0017/0018/0021; the F-001 and F-002 specs and the existing `Network` code. **No Myanma Railways-authoritative source defines any route, route shape, route history rule, or route permission grant.** Business decision required; see §0.8.

---

## 0. Discovery notes

Stage-1 output. Every note cites its source file and section, and names the decision owner.

### 0.1 What the sources say about routes

| # | Finding | Source |
|---|---|---|
| D1 | **FACT:** "Administrators can define railway lines/routes and ordered station sequences." This is the whole of the requirement. | `docs/01-functional-requirements.md` §FR-002 |
| D2 | **FACT:** "Manage routes" is core use case 2. No actor is attached to it; the actor list is `docs/03`'s eight roles. | `docs/03-use-cases.md` §Core use cases |
| D3 | **FACT:** `Route` is a candidate aggregate, separate from `Station`. `RouteSegment` is a candidate value object and `RouteSegmentResolver` a candidate domain service. They are "proposals" and aggregate boundaries must be confirmed through discovery. | `docs/04-domain-model.md` §Candidate aggregates, §Candidate value objects, §Candidate domain services |
| D4 | **FACT:** the `Network` module owns "Stations, routes, stable station indices". | `docs/05-bounded-contexts.md` §Context-to-module map |
| D5 | **FACT:** `Routes` and `RouteStations` are core tables. "Exact columns must be designed after requirements discovery." The rules require foreign keys and unique constraints for business identifiers, and "Preserve historical fare/schedule versions". Routes are not in that last sentence. | `docs/07-database-design.md` §Core tables, §Rules |
| D6 | **FACT:** the API proposal lists `GET/POST/PATCH /routes` and nothing more. "Published fare and timetable versions are immutable" is stated for fares and timetables only. | `docs/08-api-specification.md` §Initial resources |
| D7 | **FACT:** `routes.manage` is in the permission inventory, and its role grants are an OPEN QUESTION: "agents must not infer grants from the names". The proposal table has no route row. No `routes.read` exists. | `docs/10-authorization-matrix.md` §Permission inventory, table |
| D8 | **FACT:** glossary `Route` = "An ordered station sequence used for network and fare resolution." Forbidden synonyms: "Service, line (unless an approved railway term is intended)". `Station index` = "A stable short numeric index used in the signed QR mapping; it is distinct from `StationCode`." | `docs/glossary.md` §Network |
| D9 | **FACT:** "YCR is a loop. How is the fare for origin → destination determined: by direction travelled, shortest arc, flat fare or zones? Can a passenger choose the direction?" This is OQ17, open, and it blocks the fare engine. It is the only place any document describes the network's shape. | `docs/19-open-questions.md` OQ17 |
| D10 | **FACT:** fare calculation flows `FareCalculationRequest → RouteSegmentResolver → ApplicableFareRuleSelector → FareCalculator`. | `docs/12-fare-engine.md` |
| D11 | **FACT:** "Historical tickets must remain explainable even after fares change. […] Past transactions retain their applied fare context." | ADR-0002 §Rationale, §Consequences |
| D12 | **FACT:** station short indices are `uint16` fields in the signed QR payload; they "are stable and are never reused. The mapping is authenticated and versioned outside the payload." "The payload contains no fare-rule version or route data." | ADR-0014 §Binary layout v1, §Decision |
| D13 | **FACT:** F-001 implemented station create, deactivate, get-by-id and list, and deferred update, reactivate and search (§9). `Station.Deactivate()` has no guard other than "already inactive"; nothing else references a station today. Deactivated rows are retained because codes are never reused. | F-001 spec R1, R3, §9; `src/YCR.Domain/Network/Station.cs`; `src/YCR.Application/Network/DeactivateStation/DeactivateStationHandler.cs` |
| D14 | **FACT:** `ycr_app` has `SELECT, INSERT` on `network.Stations` and `UPDATE` on the `IsActive` column only; there is no `DELETE`. A new table gets no rights until a reviewed migration grants them. | `src/YCR.Infrastructure/Persistence/Migrations/20260920135245_Security_AppDatabaseRole.cs` |
| D15 | **FACT:** role→permission grants are data, seeded by a reviewed migration; no API edits grants. Permission **names** are an ENGINEERING decision; **grants** are BUSINESS (OQ12/OQ28 family). | F-002 spec R11, §0.5 D8; `docs/10` §Identity permission grants |
| D16 | **FACT:** audit subject types are stable module-prefixed constants (`Network.Station`), and audit payloads are explicit snapshot records, never entities. | `src/YCR.Application/Network/NetworkAuditSubjects.cs`; `src/YCR.Application/Network/StationAuditSnapshot.cs`; ADR-0021 rules 3–4 |

### 0.2 The gaps, one by one

**G1 — Permissions.** `routes.manage` exists by name only (D7). There is no read permission, which is exactly the gap F-001 found for stations (F-001 spec §0.3 C2, closed by adding `stations.read`). Two separate decisions are needed:
- the **name** `routes.read`: an ENGINEERING DECISION (D15). Proposed in R3; hein decides.
- the **grants** of `routes.manage` and `routes.read`: a BUSINESS DECISION. No source answers it → **OPEN QUESTION, OQ40.** Decided by Myanma Railways, or by hein as a provisional tech-lead ruling.

**G2 — Route shape.** No document names a route or describes the network beyond OQ17's "YCR is a loop" (D9). Whether the system holds one loop, one route per direction, or also short workings, spurs, branches or other lines is not stated anywhere → **OPEN QUESTION, OQ36.** Whether a sequence is closed or open, whether a station may repeat (for example the start station repeated to close the loop), and the minimum length → **OPEN QUESTION, OQ37.** The glossary's "line" warning and FR-002's "lines/routes" disagree (C1), which OQ36 also has to settle.

**G3 — History.** `docs/07` preserves fare and schedule versions and names nothing for routes (D5). If fares or timetables resolve through a route (D8, D10) and a route's sequence can be edited in place, a past ticket's fare may stop being explainable, which ADR-0002 requires (D11). Whether a sequence can change, and whether earlier sequences must be kept with effective dates → **OPEN QUESTION, OQ38.** Consumers that would need history, if they read routes at all:
- **Fare** — `RouteSegmentResolver` (D10), to explain a past quote (ADR-0002). Whether fares depend on routes at all is OQ17.
- **Timetable** — services and stopping patterns (FR-003, FR-004) will probably reference a route's stations; published timetable versions are immutable (`docs/08`), so a version that references a changed route could stop being reproducible.
- **Ticketing and Reporting** — only indirectly. ADR-0014 keeps route data out of the QR (D12), and a ticket carries origin and destination stations, not a route.

The answer changes the database design, not only the API: mutable master data needs `DELETE` (or `UPDATE`) on `RouteStations`, while versions are insert-only (R10).

**G4 — Inactive stations.** Nothing says whether an inactive station may be placed in a route, or what deactivating a station that is in a route does (D13) → **OPEN QUESTION, OQ39.** If the answer is "refuse the deactivation", F-003 changes F-001's `DeactivateStation` behaviour, which is in scope only with that ruling. If the answer is "remove the station from the sequence", that is a sequence change, so OQ38 applies to it.

**G5 — Station index.** ADR-0014's short index is a QR concern with its own stability and non-reuse rule (D12). A station's **position** in a route is a different thing: it belongs to one route and changes when the sequence changes. F-003 does not need the short index: no route operation reads or writes it. → **ENGINEERING DECISION (R18):** position is its own column, never the short index and never derived from it; the short index is **out of scope** (§9). Its allocation belongs to whichever feature first signs a QR (ADR-0014 §Consequences: "an integrity-sensitive data process"), and `docs/05` places it in `Network`.

**G6 — OQ1 (no authoritative station list).** OQ1 is open. Its effect on F-003 is the same as its effect on F-001 (F-001 spec §Blocked behaviour, "Real station data"): **no seed or fixture route ships**, because the real loop cannot be written down without the real station list and OQ36. Tests build their own stations and routes. The real routes can be entered later through the API by an administrator, or loaded by a seed migration once OQ1 and OQ36 are answered; choosing between those is deferred to that time. OQ1 does **not** block the route API itself (C3).

**G7 — Route identity and lifecycle.** Nothing says whether a route has a code or names, whether they are unique, or whether a route can be withdrawn (deactivated, reactivated, deleted). `docs/08` offers only GET, POST and PATCH. → **OPEN QUESTION, OQ41.** For stations, OQ26/OQ27 answered the same questions.

**G8 — FR-001 remainder.** Station update, reactivate and search are deferred (F-001 §9). F-003 depends on none of them unconditionally:
- **Search.** Not needed. A client building a sequence can page `GET /stations` (ordered by code) as it stands.
- **Update/rename.** Not needed. A route references stations by id, and route responses read the current station names at query time (§6), so a rename changes no route row. F-001 §9's history question about renames concerns tickets, not routes.
- **Reactivate.** **Conditional.** If OQ39 rules that an inactive station may not be placed in a route, a station deactivated by mistake can never rejoin a route until reactivation ships, and it cannot be recreated either, because codes are never reused (F-001 R3). That would be an operational dead end to record, not a blocker.

**G9 — Out of scope, deliberately not designed here:** fare and direction resolution (OQ17, `RouteSegmentResolver`, `RouteSegment`); services, stopping patterns, trains and timetables (FR-003, FR-004). F-003 must not pick a direction or segment rule for them. Where a route attribute (closed/open, direction) would serve them, it is asked in OQ36/OQ37 as a business question, not added speculatively.

### 0.3 Contradictions found

| # | Contradiction | Sources | Proposed handling | Owner |
|---|---|---|---|---|
| C1 | FR-002 and the project specification say "railway **lines**/routes", while the glossary lists "line" as a forbidden synonym for `Route` "unless an approved railway term is intended". | `docs/01` §FR-002; `docs/00-project-specification.md` ("Railway line/route management"); `docs/glossary.md` §Network (`Route`) | Folded into OQ36: if Myanma Railways distinguishes a line from a route, the glossary needs a `Line` entry and the model may need a second concept. Until then this spec uses `Route` only. **No glossary edit in this stage.** | Myanma Railways (terminology); glossary owner after the ruling |
| C2 | `docs/10`'s inventory has `routes.manage` and no read permission, and its proposal table has no route row at all. | `docs/10` §table, §inventory | Same shape as F-001 C2. R3 proposes the name `routes.read`; OQ40 asks for the grants. `docs/10` is not edited until hein rules. | hein (name); Myanma Railways (grants) |
| C3 | `docs/business/mr-questions-pack.md` §OQ1 says OQ1 blocks "station and route APIs", but F-001 shipped the station API without OQ1, blocking only seed data (F-001 §Blocked behaviour). | `docs/business/mr-questions-pack.md` §OQ1; F-001 spec §Blocked behaviour | This spec follows F-001's precedent: OQ1 blocks route **data**, not the route API (G6). The pack is not edited in this stage; hein may want its wording aligned later. | hein |
| C4 | `docs/08` proposes `PATCH /routes` (and `PATCH /stations`), but the implemented slices use an action endpoint (`POST /stations/{id}/deactivate`) and a whole-set `PUT` (`PUT /users/{id}/roles`). | `docs/08` §Initial resources, §Implemented in F-001, §Implemented in F-002 | ENGINEERING. §6 proposes `PUT /routes/{id}/stations` for the sequence (E3), and leaves `PATCH /routes/{id}` for identity fields, blocked by OQ41. `docs/08` is updated at stage 8. | hein |
| C5 | `docs/07` preserves only "fare/schedule versions", while the glossary says a route is "used for … fare resolution" and ADR-0002 requires past tickets to stay explainable. | `docs/07` §Rules; `docs/glossary.md` (`Route`); ADR-0002 | Not a contradiction until OQ17 and OQ38 are answered; recorded because a mutable route can undermine ADR-0002 if fares turn out to depend on it. Carried by OQ38. | Myanma Railways |
| C6 | *(Outside F-003; reported, not changed.)* `docs/19` OQ28's resolution text and F-001 spec R8 give `stations.read` to seven roles; `docs/10` and the F-002 seed give it to eight, since `ReportingUser` was added on 2026-09-23 (recorded under OQ12). | `docs/19` OQ28 vs OQ12; F-001 spec R8; `docs/10` §Station permission grants | `docs/10` governs, and OQ12's resolution records the addition. OQ28's text is stale history. No action in F-003. | hein |
| C7 | *(Outside F-003; reported, not changed.)* F-001 spec line 8 cites "UC 'Manage stations' (`docs/03-use-cases.md` §16)"; `docs/03` has no §16 — "Manage stations" is core use case 1. | F-001 spec header; `docs/03` | Cosmetic. No action in F-003. | — |

### 0.4 Engineering choices proposed (hein decides)

| # | Proposal | Label | Why |
|---|---|---|---|
| E1 | **Client-held version for sequence edits — ADR-0024 (Proposed).** `Route` gets a `rowversion`; `RouteResponse` carries `version`; `PUT /routes/{id}/stations` requires `expectedVersion`; a mismatch is `409 Network.RouteVersionMismatch`. | ENGINEERING DECISION, pending ADR-0024 acceptance | A whole-sequence replacement composed from an earlier read loses a concurrent edit silently under a server-side-only token (ADR-0024 §Context). Material and reusable by fares and timetables, so an ADR rather than a spec-local choice. |
| E2 | **Permission name `routes.read`**, added to the `docs/10` inventory. Grants: OQ40. | ENGINEERING DECISION (name); BUSINESS (grants) | F-001 C2 precedent; F-002 D8 splits name from grant. |
| E3 | **`PUT /routes/{id}/stations`** replaces the whole sequence. No per-position insert or move endpoints. | ENGINEERING DECISION | One request = one validated, audited sequence; no intermediate invalid states. F-002 `PUT /users/{id}/roles` precedent (C4). |
| E4 | **A route is created with its initial sequence** in one `POST /routes`. | ENGINEERING DECISION | A route never exists without a sequence that passes the OQ37 rules, so no "empty route" state has to be specified. |
| E5 | **`Route` is its own aggregate and owns its `RouteStation` rows**; a `RouteStation` references a station by `StationId` only, with no navigation property, and the database has a foreign key to `network.Stations`. | ENGINEERING DECISION (ADR-0012 item 6; `docs/04` lists `Station` and `Route` separately; `docs/07` §Rules "Use foreign keys") | Same module, so the FK is allowed, and it is the database authority that a sequence never names a missing station. Aggregates stay separate so a route edit does not lock stations. |
| E6 | **Position** is a 1-based `int`, contiguous `1..n` within the route, unique per route. It is neither the ADR-0014 short index nor the station code. | ENGINEERING DECISION (ADR-0014; glossary `Station index`) | G5. |
| E7 | **Database grants to `ycr_app`** by a reviewed migration, never wider than the endpoints need. Which grants depends on OQ38 (R10). | ENGINEERING DECISION (ADR-0017 item 3 spirit; D14 precedent) | The grant set is the database-level statement of the history rule. |
| E8 | **Error codes** as listed in §4 and §6, `Network.<Reason>`, mapped per ADR-0004. An unknown station id in a sequence is `422 Network.RouteStationNotFound`, not `404`: the addressed resource exists and the request is well-formed. | ENGINEERING DECISION (ADR-0004; `docs/20` §2) | Consistent with F-001/F-002. |

### 0.5 New OPEN QUESTIONs added to `docs/19`

OQ36 route catalogue and shape · OQ37 sequence topology (closed/open, repeats, minimum length) · OQ38 sequence changes and history · OQ39 inactive stations and station deactivation · OQ40 route permission grants · OQ41 route identity and lifecycle. Each has a **BLOCKS:** line naming the rules below. `docs/business/mr-questions-pack.md` is **not** edited in this stage; progress.md lists the six for hein to add.

### 0.6 What discovery did **not** find

No document defines: any YCR route, its stations or their order; whether a route has a direction; whether a sequence is closed; whether a station may repeat; a minimum route length; any route code, name or lifecycle; route history rules; the interaction between routes and inactive stations; who manages or reads routes. All are business rules → OQ36–OQ41. No engineering rule was missing that an existing ADR does not cover, apart from API-level concurrency (ADR-0024, Proposed).

### 0.7 Where the existing code constrains the design

- `INetworkDbContext` exposes only `Stations` (`src/YCR.Application/Network/INetworkDbContext.cs`); F-003 adds `Routes`. Nothing crosses a module boundary.
- `NetworkConstraints` holds the unique-index names a handler matches, kept honest by a model test (`src/YCR.Application/Network/NetworkConstraints.cs`); F-003 adds the route ones the same way.
- `DeactivateStationHandler` would need a route check only if OQ39 rules "refuse" (G4).
- The `Security_AppDatabaseRole` pattern (D14) is the model for F-003's grants migration.

### 0.8 Rulings needed from hein

Options are **non-binding** discussion prompts; none is decided. For the business questions, hein may wait for Myanma Railways or give a **provisional tech-lead ruling — not a Myanma Railways answer**.

| ID | Question | Options (non-binding) | Blocks |
|---|---|---|---|
| **OQ36** | Which routes exist (one loop; one per direction; plus short workings, spurs, branches or other lines)? Is "line" a separate concept from "route" (C1)? | (a) one route, the full loop, no direction; (b) two routes, one per direction of travel; (c) (a) or (b) plus further routes that share stations, such as short workings or branches; (d) a separate `Line` concept that groups routes | R4, R5; the route fields in §6/§7 (whether a direction attribute exists); every scenario |
| **OQ37** | Is a sequence closed or open; may a station repeat; what is the minimum length? | Topology: (a) always closed; (b) always open; (c) per-route attribute. Repeats: (a) never; (b) only the first station repeated as the last, to close a loop; (c) freely. Minimum: (a) 2 stations; (b) another number | R6–R8; S8, S9; the `(RouteId, StationId)` unique index and any topology column |
| **OQ38** | Can a sequence change, and must earlier sequences be kept with effective dates? | (a) mutable master data: replace in place, history only in the audit ledger; (b) versioned like fares/timetables: new version with an effective date, published versions immutable; (c) immutable: a changed route is a new route, the old one withdrawn | R9, R10; S4, S19, S20, S27; the `PUT` endpoint; `RouteStations` shape and grants |
| **OQ39** | May an inactive station be placed in a route; what does deactivating a station that is in a route do? | Placement: (a) refused; (b) allowed. Deactivation: (a) refused while the station is in an active route; (b) allowed, the station stays in the sequence and consumers treat it as not served; (c) allowed, and the station is removed from every sequence (a sequence change, so OQ38 applies) | R11, R12; S7, S16–S18; any change to F-001 `DeactivateStation` |
| **OQ40** | Who holds `routes.manage`, and who holds `routes.read`? | Manage: (a) `SystemAdministrator` and `RailwayAdministrator`, as for `stations.manage`; (b) another set. Read: (a) all eight roles, as for `stations.read`; (b) a narrower set | R2, R3; §2; the grant seed migration; S14, S15 |
| **OQ41** | Does a route have a code and names; format, uniqueness, reuse; can it be withdrawn? | Identity: (a) code and `BilingualName` under the station rules (R3/R4 of F-001); (b) names only; (c) another format. Lifecycle: (a) deactivate only, rows retained; (b) deactivate and reactivate; (c) no withdrawal in F-003 | R13–R15; S10, S11, S23, S26; `POST /routes` fields; `UX_Routes_Code` |
| **E1** | Accept ADR-0024 (client-held `version` / `expectedVersion`, `409 <Module>.<Resource>VersionMismatch`)? | (a) accept option 3 as proposed; (b) option 2, `ETag`/`If-Match` with 412/428 (needs an ADR-0004 superseding change); (c) last writer wins | R17; S19, S20; the `PUT` contract |
| **E2** | Add the permission name `routes.read` to `docs/10`? | (a) add it; (b) reuse `stations.read` for routes; (c) no read permission (every authenticated user) | R3; §6 read endpoints |
| **E3–E8** | Accept the engineering proposals in §0.4 (PUT sub-resource, create-with-sequence, aggregate and FK shape, position model, grants approach, error codes)? | Accept, or amend each | Stage 3 (PLAN) |

---

## 1. Goal

Railway administrators need to define the network's routes as ordered sequences of existing stations, so that later features (fares, services, timetables) have one authoritative place to read a route from, and every change to a route is authorised, validated and audited.

---

## 2. Actors and permissions

| Actor | Permission | Notes |
|---|---|---|
| Staff user managing routes | `routes.manage` | Name: FACT (`docs/10` inventory). **Grants: OPEN QUESTION, OQ40.** |
| Staff user reading routes | `routes.read` | **Name proposed (E2, ENGINEERING DECISION pending hein). Grants: OPEN QUESTION, OQ40.** |
| Staff user deactivating stations | `stations.manage` | Unchanged from F-001. Affected only if OQ39 adds a guard. |

No grant is proposed as decided. Holding `stations.manage` gives no route right, and the reverse, unless OQ40 says so.

---

## 3. Business rules

| # | Rule | Label | Source |
|---|---|---|---|
| R1 | Administrators can define railway lines/routes and ordered station sequences. | FACT | `docs/01` §FR-002 |
| R2 | Managing routes requires `routes.manage`. **Which roles hold it** is not decided. | Permission name: FACT. **Grants: OPEN QUESTION (OQ40)** | `docs/10` §Permission inventory |
| R3 | Reading routes requires `routes.read`. **Which roles hold it** is not decided. | Name: **ENGINEERING DECISION, proposed (E2)**, pending hein. **Grants: OPEN QUESTION (OQ40)** | F-001 spec C2 precedent; F-002 D8 |
| R4 | A route is an ordered sequence of stations, in the `Network` module. | FACT | `docs/glossary.md` (`Route`); `docs/04`; `docs/05` |
| R5 | Which routes the system holds, and whether a route has a direction of travel. | **OPEN QUESTION (OQ36)** | — |
| R6 | Whether a sequence is closed (wraps from last to first) or open (two ends). | **OPEN QUESTION (OQ37)** | — |
| R7 | Whether a station may appear more than once in one sequence. | **OPEN QUESTION (OQ37)** | — |
| R8 | The minimum number of stations in a route. | **OPEN QUESTION (OQ37)** | — |
| R9 | Whether a route's sequence may change after creation, and whether earlier sequences are kept with effective dates. | **OPEN QUESTION (OQ38)** | `docs/07` §Rules names only fare and schedule versions; ADR-0002 |
| R10 | The database grants on `RouteStations` follow R9: mutable master data needs row removal or update; versions are insert-only. No grant wider than the answer needs. | ENGINEERING DECISION (E7), **its content blocked by OQ38** | ADR-0017 item 3 spirit; `Security_AppDatabaseRole` |
| R11 | Whether an inactive station may be placed in a route. | **OPEN QUESTION (OQ39)** | — |
| R12 | What deactivating a station that is in a route does. | **OPEN QUESTION (OQ39)** | F-001 `DeactivateStation` has no such guard today |
| R13 | Whether a route has a code and names, and their formats. | **OPEN QUESTION (OQ41)** | — |
| R14 | Whether a route code is unique and may be reused. | **OPEN QUESTION (OQ41)** | — |
| R15 | Whether a route can be withdrawn (deactivated, reactivated, deleted). | **OPEN QUESTION (OQ41)** | `docs/08` lists only GET/POST/PATCH |
| R16 | Every station in a sequence exists in `network.Stations`. The database foreign key is the authority; the handler's pre-check gives the error code. | ENGINEERING DECISION (E5) | `docs/07` §Rules ("Use foreign keys"); AGENTS.md rule 5 |
| R17 | A sequence replacement carries the `version` the caller read; a mismatch is refused with `409 Network.RouteVersionMismatch` and writes nothing. | ENGINEERING DECISION, **pending acceptance of ADR-0024 (Proposed)** | ADR-0024; `docs/20` §6 |
| R18 | A station's position in a route is a 1-based contiguous ordinal within that route. It is not the ADR-0014 station short index and not the station code, and F-003 neither reads nor allocates the short index. | ENGINEERING DECISION (E6) | ADR-0014 §Decision; `docs/glossary.md` (`Station index`) |
| R19 | Route identifiers are application-generated GUIDs through `IIdGenerator`, mapped `ValueGeneratedNever()`. | ENGINEERING DECISION | ADR-0006 §Decision item 1 and 2026-09-19 amendment |
| R20 | Creating a route and changing its sequence (and withdrawing it, if OQ41 allows) are audited, with actor fields from the authenticated server-side context only. | ENGINEERING DECISION | ADR-0017 §Decision items 1–2; ADR-0021; F-001 R10 precedent |
| R21 | Route management is not financial or retryable; no `Idempotency-Key`. | ENGINEERING DECISION | `docs/20` §5 (required list excludes master data); F-001 R11 |
| R22 | Instants are `DateTimeOffset` / `datetimeoffset(3)` UTC. Routes own no `BusinessDate`. If OQ38 chooses effective dates, they are `DateOnly` / `date`. | ENGINEERING DECISION | ADR-0018 §Time; ADR-0019 |
| R23 | No real route data ships: no seed migration and no fixture of the YCR loop. Tests build their own stations and routes. | FACT (consequence of OQ1, open) | `docs/19` OQ1; F-001 §Blocked behaviour precedent |
| R24 | Route responses show each station's **current** code, names and active flag, read at query time; a route row stores only `StationId`. | ENGINEERING DECISION | E5; G8 |

**Blocking open questions:** **OQ36, OQ37, OQ38, OQ39, OQ40, OQ41.** Each is blocking: none of create, read, sequence change, permissions or the table design can be approved while any of them is open. The spec **cannot be Approved** until hein rules on them (§0.8), and on E1/E2.

---

## 4. Scenarios (Given / When / Then)

Scenarios marked **[OQnn]** depend on a ruling; their shape follows the default proposal and changes with the answer. Status codes follow ADR-0004. Every error response is ProblemDetails with `errorCode` and `traceId`.

### Happy path

- **S1.** Given stations A, B, C exist and are active, and a caller holding `routes.manage`, when they POST `/routes` with the route's identity fields [OQ41] and `stationIds = [A, B, C]`, then the response is `201 Created` with `Location: /api/v1/routes/{id}` and `{ id, version }`, `network.Routes` has one row, `network.RouteStations` has positions 1, 2, 3 for A, B, C, and one `Network.RouteCreated` audit event is written.
- **S2.** Given that route, when a caller holding `routes.read` GETs `/routes/{id}`, then `200` with `RouteResponse` including `version` and `stations` in position order, each with the station's current code, names and `isActive` (R24). No EF entity is serialised (AGENTS.md rule 4).
- **S3.** Given three routes, when a caller holding `routes.read` GETs `/routes?page=1&pageSize=50`, then `200` with `items`, `page`, `pageSize`, `totalCount`, in a stable order (by code if OQ41 gives routes a code, otherwise by `CreatedAtUtc` then `Id`).
- **S4.** **[OQ38]** Given the route at version `v1`, when a caller holding `routes.manage` PUTs `/routes/{id}/stations` with `expectedVersion = v1` and `stationIds = [A, C, B]`, then `200` with the new `version`, the positions are rewritten, and one `Network.RouteStationsReplaced` audit event carries the before and after sequences.

### Failures, each with its error code

- **S5.** Validation → `400 Common.ValidationFailed`: missing `stationIds`; empty `stationIds`; a non-GUID station id; missing identity fields [OQ41]; on PUT, missing or malformed `expectedVersion` (R17).
- **S6.** A station id that does not exist → `422 Network.RouteStationNotFound`; nothing is written (R16).
- **S7.** **[OQ39]** An inactive station in the sequence, if placement is refused → `422 Network.RouteStationInactive`.
- **S8.** **[OQ37]** A station repeated in the sequence, if repeats are refused (or refused except as loop closure) → `422 Network.RouteStationRepeated`.
- **S9.** **[OQ37]** Fewer stations than the minimum → `422 Network.RouteTooFewStations`.
- **S10.** **[OQ41]** A duplicate route code → `409 Network.RouteCodeAlreadyExists`, including the code of a withdrawn route if codes are never reused.
- **S11.** **[OQ41]** A malformed route code or name → `400 Network.InvalidRouteCode` / `400 Network.InvalidRouteName`.
- **S12.** GET or PUT an unknown route id → `404 Network.RouteNotFound`.
- **S13.** `pageSize=201` → `400 Network.InvalidPageRequest` (`docs/20` §4; existing code).
- **S14.** Anonymous request to any `/routes` endpoint → `401 Auth.Unauthenticated`.
- **S15.** Authenticated without the permission → `403`: a caller with only `routes.read` POSTs or PUTs; a caller with only `stations.manage` POSTs `/routes`; a caller with neither GETs `/routes`.

### Station deactivation — **[OQ39]**

- **S16.** *If deactivation is refused:* given station B is in an active route, when a caller holding `stations.manage` POSTs `/stations/{B}/deactivate`, then `422 Network.StationInUseByRoute`, B stays active, no audit event.
- **S17.** *If deactivation is allowed and B stays in the sequence:* `204`; the route is unchanged and S2 shows B with `isActive = false`. *If B is removed instead:* `204`, B is removed from each route's sequence (positions renumbered), and one `Network.RouteStationsReplaced` per affected route shares the deactivation's correlation id; this option also has to satisfy OQ37's minimum length and OQ38.
- **S18.** Concurrency: a station deactivation and a sequence replacement that adds that station race. The outcome must match the OQ39 ruling in every interleaving — for example, under "placement refused" plus "deactivation refused", either the deactivation fails or the replacement fails, never both succeed. The serialisation mechanism is a PLAN decision.

### Concurrency and data integrity

- **S19.** **[OQ38, E1]** Two parallel PUTs with the same `expectedVersion` → exactly one `200`; the other `409 Network.RouteVersionMismatch`; exactly one `Network.RouteStationsReplaced` event; the stored sequence is the winner's, complete, never a mix of both.
- **S20.** **[OQ38, E1]** A PUT with a stale `expectedVersion` (another edit happened after the read) → `409 Network.RouteVersionMismatch`; nothing written.
- **S21.** **[OQ41]** Two parallel POSTs with the same route code → one `201`, one `409 Network.RouteCodeAlreadyExists` through the unique-index violation (ADR-0004); exactly one route row.
- **S22.** An audit event's actor fields come from the authenticated context; actor fields supplied in the request body are ignored (ADR-0017 §2; F-001 S20).
- **S23.** **[OQ41]** If routes can be withdrawn: `POST /routes/{id}/deactivate` → `204` and `Network.RouteDeactivated`; again → `422 Network.RouteAlreadyInactive`; two in parallel → one `204`, one `422`, one event (F-001 S27 pattern).
- **S24.** The sequence is always contiguous `1..n` after any write, and a failed write leaves the previous sequence intact (one `SaveChangesAsync`, ADR-0004).
- **S25.** Database privileges: `ycr_app` has exactly the grants R10 settles and no DDL; a test asserts the absences as well as the presences (F-001 S22 pattern).
- **S26.** **[OQ41]** If routes have a Myanmar name, it round-trips unchanged (F-001 S14 pattern).
- **S27.** **[OQ38 option (b)]** If sequences are versioned: reading a route as of a past date returns the sequence effective on that date, and a published version cannot be changed. Shape to be specified after the ruling.

---

## 5. State changes

| Entity | From | Event | Guard | To |
|---|---|---|---|---|
| Route | (none) | `CreateRoute` | `routes.manage`; identity fields valid and unique [OQ41]; sequence valid (R16, [OQ37], [OQ39]) | Active |
| Route | Active | `ReplaceRouteStations` [OQ38] | `routes.manage`; `expectedVersion` matches (R17); sequence valid | Active, new version |
| Route | Active | `DeactivateRoute` [OQ41] | `routes.manage` | Inactive |
| Route | Inactive | `DeactivateRoute` [OQ41] | — | rejected, `Network.RouteAlreadyInactive` |
| Station | Active | `DeactivateStation` | `stations.manage`; **[OQ39]** possibly "not in any active route" | Inactive |

Whether an inactive route's sequence may still be replaced is part of OQ41.

---

## 6. API

Base path `/api/v1`, JSON camelCase, GUID ids, ProblemDetails with `errorCode` and `traceId` (`docs/20` §4; ADR-0004). No endpoint takes an `Idempotency-Key` (R21). Fields marked [OQ41] exist only if the ruling gives routes those fields; a topology or direction field is added only if OQ36/OQ37 say so.

| Method | Path | Request | Success | Error codes | Permission | Status |
|---|---|---|---|---|---|---|
| POST | `/routes` | `CreateRouteRequest { code [OQ41], nameEn [OQ41], nameMy [OQ41], stationIds: Guid[] }` | `201` + `CreateRouteResponse { id, version }` and `Location` | `400 Common.ValidationFailed` · `400 Network.InvalidRouteCode` [OQ41] · `400 Network.InvalidRouteName` [OQ41] · `401` · `403` · `409 Network.RouteCodeAlreadyExists` [OQ41] · `422 Network.RouteStationNotFound` · `422 Network.RouteStationInactive` [OQ39] · `422 Network.RouteStationRepeated` [OQ37] · `422 Network.RouteTooFewStations` [OQ37] | `routes.manage` [OQ40] | Blocked by OQ36, OQ37, OQ39, OQ40, OQ41 |
| GET | `/routes/{id}` | — | `200` + `RouteResponse { id, code [OQ41], nameEn [OQ41], nameMy [OQ41], isActive [OQ41], createdAtUtc, version, stations: [{ position, stationId, code, nameEn, nameMy, isActive }] }` | `401` · `403` · `404 Network.RouteNotFound` | `routes.read` [E2, OQ40] | Blocked by OQ40, E2 |
| GET | `/routes` | `?page=1&pageSize=50` (max 200) | `200` + `{ items: RouteSummaryResponse[], page, pageSize, totalCount }`; `RouteSummaryResponse { id, code [OQ41], nameEn [OQ41], nameMy [OQ41], isActive [OQ41], stationCount, createdAtUtc }` | `400 Network.InvalidPageRequest` · `401` · `403` | `routes.read` [E2, OQ40] | Blocked by OQ40, E2 |
| PUT | `/routes/{id}/stations` | `ReplaceRouteStationsRequest { expectedVersion, stationIds: Guid[] }` — the complete new sequence | `200` + `ReplaceRouteStationsResponse { version }` | `400 Common.ValidationFailed` · `401` · `403` · `404 Network.RouteNotFound` · `409 Network.RouteVersionMismatch` · `422 Network.RouteStationNotFound` · `422 Network.RouteStationInactive` [OQ39] · `422 Network.RouteStationRepeated` [OQ37] · `422 Network.RouteTooFewStations` [OQ37] | `routes.manage` [OQ40] | Blocked by OQ38 (does not exist under option (c); becomes version create/publish under option (b)), E1 |
| PATCH | `/routes/{id}` | identity fields | — | — | `routes.manage` | **Not specified.** Listed in `docs/08`; blocked by OQ41 (whether routes have editable identity fields), with the same rename-history question as F-001 §9 |
| POST | `/routes/{id}/deactivate` | — | `204` | `401` · `403` · `404 Network.RouteNotFound` · `422 Network.RouteAlreadyInactive` | `routes.manage` | Blocked by OQ41 |
| POST | `/stations/{id}/deactivate` | unchanged | unchanged | adds `422 Network.StationInUseByRoute` only under OQ39 deactivation option (a) | `stations.manage` | Existing F-001 endpoint; changes only with OQ39 |

**If OQ38 chooses versions (option b):** the `PUT` is replaced by `POST /routes/{id}/versions` (a draft with an effective date) and `POST /routes/{id}/versions/{versionId}/publish`, following `docs/08`'s fare and schedule pattern, and published versions are never edited (`docs/20` §4). That shape is specified after the ruling, not here.

---

## 7. Data

A proposal. Schema `network` (ADR-0012; `docs/07` §Module schemas). Every `*Utc` column carries `CK_<Table>_<Column>_Utc` (`DATEPART(TZOFFSET, …) = 0`), declared in the EF model as well as the migration (`docs/07` §F-001 station persistence controls). Foreign keys are `NO ACTION`.

### `network.Routes`

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | no | PK, clustered, application-assigned (R19) |
| `Code` | `nvarchar(10)` | no | **[OQ41]** only if routes have a code; width to follow the ruling |
| `NameEn` | `nvarchar(100)` | no | **[OQ41]** owned `BilingualName`, if routes have names |
| `NameMy` | `nvarchar(100)` | no | **[OQ41]** Myanmar Unicode, never Zawgyi (`docs/20` §6) |
| `IsActive` | `bit` | no | **[OQ41]** only if routes can be withdrawn |
| *topology / direction* | — | — | **[OQ36, OQ37]** only if the ruling makes it a per-route attribute; no column is proposed until then |
| `CreatedAtUtc` | `datetimeoffset(3)` | no | UTC (ADR-0018) |
| `RowVersion` | `rowversion` | no | Concurrency token; its base64 form is the API `version` (R17, ADR-0024) |

| Object | Definition | Why |
|---|---|---|
| `PK_Routes` | clustered on `Id` | ADR-0006 |
| `UX_Routes_Code` | unique on `Code` | **[OQ41]** concurrency authority for duplicate codes, including withdrawn routes if codes are never reused; matched by name in `NetworkConstraints` |
| `CK_Routes_CreatedAtUtc_Utc` | UTC check | ADR-0018 |

### `network.RouteStations`

| Column | Type | Null | Notes |
|---|---|---|---|
| `RouteId` | `uniqueidentifier` | no | `FK_RouteStations_Routes_RouteId` |
| `Position` | `int` | no | 1-based, contiguous per route (R18) |
| `StationId` | `uniqueidentifier` | no | `FK_RouteStations_Stations_StationId` (R16) |

| Object | Definition | Why |
|---|---|---|
| `PK_RouteStations` | on `(RouteId, Position)` | One station per position. Contiguity (`1..n`, no gaps) is enforced by the aggregate, since a check constraint cannot see other rows |
| `CK_RouteStations_Position` | `Position >= 1` | Rejects zero and negatives from any writer |
| `UX_RouteStations_RouteId_StationId` | unique on `(RouteId, StationId)` | **[OQ37]** only if a station may never repeat; absent under the repeat options |
| `IX_RouteStations_StationId` | nonclustered on `StationId` | "Which routes contain this station?" — the station-deactivation check [OQ39] and the FK's own lookup when a station row is checked |

**If OQ38 chooses versions**, `RouteStations` hangs off a `network.RouteVersions` table (`Id`, `RouteId`, `EffectiveFrom date`, `PublishedAtUtc`, …) instead of `Routes`, and every table is insert-only. Specified after the ruling.

### Grants to `ycr_app` (new `Security_NetworkRouteGrants` migration)

| Table | Proposed | Deliberately absent |
|---|---|---|
| `Routes` | `SELECT`, `INSERT`; `UPDATE` only of `IsActive` [OQ41], and of whatever column a sequence change must touch so the `rowversion` advances | `DELETE`; `UPDATE` of `Id`, `CreatedAtUtc`, and of code/names until an endpoint edits them |
| `RouteStations` | `SELECT`, `INSERT`; plus `DELETE` **only** under OQ38 option (a) | `UPDATE`; `DELETE` under options (b) and (c) |

No DDL anywhere. `DatabasePrivilegeTests` asserts presences and absences (S25).

Migrations are named `yyyyMMddHHmmss_Network_<Change>` (`docs/20` §6) and reviewed per `docs/workflows/04-database-change.md`. Adding tables and an FK to `Stations` alters no existing column. `network.Stations` itself is unchanged.

---

## 8. Audit, logging, metrics

- **Audit** (ADR-0017, ADR-0021; F-001 R10 pattern): through `IAuditWriter`, in the same `SaveChangesAsync` as the change, so a losing concurrent request writes no event (F-001 `DeactivateStationHandler` pattern).

  | Action | When | `BeforeJson` | `AfterJson` |
  |---|---|---|---|
  | `Network.RouteCreated` | S1 | null | snapshot |
  | `Network.RouteStationsReplaced` | S4 [OQ38]; S17 removal option [OQ39] | snapshot | snapshot |
  | `Network.RouteDeactivated` | S23 [OQ41] | snapshot | snapshot |
  | `Network.StationDeactivated` | unchanged from F-001 | — | — |

  `SubjectType` = `Network.Route`, a new constant in `NetworkAuditSubjects` (never a CLR name); `SubjectId` = route id; `AuthorizedByPermission` = `routes.manage`; `PayloadVersion` = 1. The snapshot is an explicit record, never the aggregate: `RouteAuditSnapshot { code [OQ41], nameEn [OQ41], nameMy [OQ41], isActive [OQ41], stations: [{ position, stationId, stationCode }] }`. The station code is included so a ledger row is readable without a join, and it is stable because codes are never reused (F-001 R3). No personal data, token or key can appear (ADR-0021 rule 4).
- **Under OQ38 option (a)** the audit ledger is the only record of earlier sequences. That is enough for investigation, but no feature may read the ledger as business history; if fares or timetables need the past sequence, that is option (b).
- **Logging** (`docs/20` §7): message templates, no personal data.
- **Metrics** (`docs/17`): none. No business event listed there belongs to routes; request metrics come from F-001's baseline.

---

## 9. Out of scope

- Fare and direction resolution: OQ17, `RouteSegmentResolver`, `RouteSegment`, fare quotes (`docs/12`).
- Services, trains, stopping patterns, operating days and timetables (FR-003, FR-004).
- The ADR-0014 station short index: allocation, storage, the authenticated versioned mapping (G5, R18).
- Station update, reactivate and search (F-001 §9); F-003 depends on none of them (G8).
- Seed or fixture route data (R23, OQ1).
- Route identity edits (`PATCH /routes/{id}`) until OQ41 is ruled.
- Editing `docs/business/mr-questions-pack.md`, `docs/glossary.md` and `docs/10` — after hein's rulings (stage 8, or with the ruling commit).

---

## Blocked behaviour

Required by `docs/21` §Specification.

| Behaviour | Blocking item | Effect on F-003 |
|---|---|---|
| Route catalogue, direction attribute | **OQ36** | No route shape can be fixed; C1 unresolved |
| Sequence validation (closed/open, repeats, minimum) | **OQ37** | S8, S9 and the `(RouteId, StationId)` index undecided |
| Changing a sequence; history | **OQ38** | `PUT /routes/{id}/stations` and the `RouteStations` grants undecided |
| Inactive stations in routes; station deactivation | **OQ39** | S7, S16–S18; possible F-001 change |
| Route permissions | **OQ40** | No endpoint can be authorised; no grant seeded |
| Route code, names, withdrawal | **OQ41** | Request fields, unique index, `POST /routes/{id}/deactivate` |
| API version token | **ADR-0024 (Proposed)** | R17, S19, S20 |
| Real route data | **OQ1** | No seed; tests build their own (R23) |

No placeholder rule may be implemented for any of these (AGENTS.md §When a business rule is missing).

---

## Notes for the next stage

- Stage 3 (PLAN, `docs/templates/plan.md`) starts only after hein rules on OQ36–OQ41, E1 and E2, and sets the spec to Approved. Rulings that are provisional must carry "not a Myanma Railways answer" in `docs/19`, `docs/10` and here.
- After the rulings: update §3 labels, drop the `[OQnn]` markers that no longer apply, resolve the §6 Status column, fix the `Routes` columns, and write the OQ40 grants into `docs/10` and a seed migration (F-002 R11 mechanism). If OQ39 changes station deactivation, F-001's scenarios S2/S7/S27 must still pass.
- Add OQ36–OQ41 to `docs/business/mr-questions-pack.md` (hein, listed in `progress.md`).

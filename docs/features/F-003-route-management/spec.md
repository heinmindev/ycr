# F-003: Route management

Status: **Approved (hein, 2026-09-24)**, amended by Amendments 1–2 (hein, 2026-09-24, T-034; §0.9). Written by claude (T-032). Stages 1–2 of `docs/workflows/02-feature-development.md`. hein's rulings of 2026-09-24 on OQ36–OQ41 and E1–E8, and the approval-time decisions (A1, R25, `IX_RouteStations_StationId`, the creation/deactivation race), are recorded in §0.8 and applied throughout. The business rulings are **provisional tech-lead rulings — not a Myanma Railways answer**.

Module(s): `Network` (routes only; station deactivation is unchanged); cross-cutting `Audit`, `Identity` (permission grants only)
Related: FR-002, UC 2 "Manage routes" (`docs/03-use-cases.md` §Core use cases, item 2), FR-001 (stations), ADR-0004, ADR-0006, ADR-0012, ADR-0014, ADR-0017, ADR-0018, ADR-0021, `docs/20-coding-conventions.md` §3

Decision owner:
- Business rules (which routes exist, sequence shape, history, interaction with inactive stations, route identity and lifecycle, role grants): **Myanma Railways**, routed through `hein` (`docs/19-open-questions.md` OQ36–OQ41). Suggested routing label, following `docs/business/mr-questions-pack.md`: Network Operations / Planning (role to be nominated). **For F-003, hein gave provisional tech-lead rulings on 2026-09-24 (T-032) — not a Myanma Railways answer**, in the OQ26–OQ28 and OQ34 pattern (§0.8). Still open with Myanma Railways.
- Engineering decisions: **tech lead (`hein`)**, recorded as ADRs per `docs/decisions/README.md`.

Authoritative sources: `docs/01-functional-requirements.md` §FR-002; `docs/03-use-cases.md`; `docs/04-domain-model.md`; `docs/05-bounded-contexts.md`; `docs/07-database-design.md`; `docs/08-api-specification.md`; `docs/10-authorization-matrix.md`; `docs/glossary.md`; ADR-0004/0006/0012/0014/0017/0018/0021; the F-001 and F-002 specs and the existing `Network` code. **No Myanma Railways-authoritative source defines any route, route shape, route history rule, or route permission grant.** The rules below rest on hein's provisional rulings (§0.8).

---

## 0. Discovery notes

Stage-1 output. Every note cites its source file and section, and names the decision owner. §0.1–§0.7 are kept as written at discovery; where they list options or proposals, the rulings in §0.8 decide.

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
| C1 | FR-002 and the project specification say "railway **lines**/routes", while the glossary lists "line" as a forbidden synonym for `Route` "unless an approved railway term is intended". | `docs/01` §FR-002; `docs/00-project-specification.md` ("Railway line/route management"); `docs/glossary.md` §Network (`Route`) | Folded into OQ36: if Myanma Railways distinguishes a line from a route, the glossary needs a `Line` entry and the model may need a second concept. Until then this spec uses `Route` only. **No glossary edit in this stage.** **Closed 2026-09-24 by the OQ36 ruling:** there is no `Line` concept; "line" in FR-002 is read as "route", and the glossary's forbidden-synonym rule stands. | Myanma Railways (terminology); glossary owner after the ruling |
| C2 | `docs/10`'s inventory has `routes.manage` and no read permission, and its proposal table has no route row at all. | `docs/10` §table, §inventory | Same shape as F-001 C2. R3 proposes the name `routes.read`; OQ40 asks for the grants. `docs/10` is not edited until hein rules. | hein (name); Myanma Railways (grants) |
| C3 | `docs/business/mr-questions-pack.md` §OQ1 says OQ1 blocks "station and route APIs", but F-001 shipped the station API without OQ1, blocking only seed data (F-001 §Blocked behaviour). | `docs/business/mr-questions-pack.md` §OQ1; F-001 spec §Blocked behaviour | This spec follows F-001's precedent: OQ1 blocks route **data**, not the route API (G6). The pack is not edited in this stage; hein may want its wording aligned later. | hein |
| C4 | `docs/08` proposes `PATCH /routes` (and `PATCH /stations`), but the implemented slices use an action endpoint (`POST /stations/{id}/deactivate`) and a whole-set `PUT` (`PUT /users/{id}/roles`). | `docs/08` §Initial resources, §Implemented in F-001, §Implemented in F-002 | ENGINEERING. §6 proposes `PUT /routes/{id}/stations` for the sequence (E3), and leaves `PATCH /routes/{id}` for identity fields, blocked by OQ41. `docs/08` is updated at stage 8. **Settled 2026-09-24:** under the OQ38 and OQ41 rulings there is neither a `PUT` nor a `PATCH` for routes; the only action endpoint is `POST /routes/{id}/deactivate`. | hein |
| C5 | `docs/07` preserves only "fare/schedule versions", while the glossary says a route is "used for … fare resolution" and ADR-0002 requires past tickets to stay explainable. | `docs/07` §Rules; `docs/glossary.md` (`Route`); ADR-0002 | Not a contradiction until OQ17 and OQ38 are answered; recorded because a mutable route can undermine ADR-0002 if fares turn out to depend on it. Carried by OQ38. | Myanma Railways |
| C6 | *(Outside F-003; reported, not changed.)* `docs/19` OQ28's resolution text and F-001 spec R8 give `stations.read` to seven roles; `docs/10` and the F-002 seed give it to eight, since `ReportingUser` was added on 2026-09-23 (recorded under OQ12). | `docs/19` OQ28 vs OQ12; F-001 spec R8; `docs/10` §Station permission grants | `docs/10` governs, and OQ12's resolution records the addition. OQ28's text is stale history. No action in F-003. | hein |
| C7 | *(Outside F-003; reported, not changed.)* F-001 spec line 8 cites "UC 'Manage stations' (`docs/03-use-cases.md` §16)"; `docs/03` has no §16 — "Manage stations" is core use case 1. | F-001 spec header; `docs/03` | Cosmetic. No action in F-003. | — |

### 0.4 Engineering choices proposed (hein decides)

| # | Proposal | Label | Why |
|---|---|---|---|
| E1 | **Outcome 2026-09-24: not adopted; ADR-0024 stays Proposed, unused by F-003 (§0.8).** **Client-held version for sequence edits — ADR-0024 (Proposed).** `Route` gets a `rowversion`; `RouteResponse` carries `version`; `PUT /routes/{id}/stations` requires `expectedVersion`; a mismatch is `409 Network.RouteVersionMismatch`. | ENGINEERING DECISION, pending ADR-0024 acceptance | A whole-sequence replacement composed from an earlier read loses a concurrent edit silently under a server-side-only token (ADR-0024 §Context). Material and reusable by fares and timetables, so an ADR rather than a spec-local choice. |
| E2 | **Permission name `routes.read`**, added to the `docs/10` inventory. Grants: OQ40. | ENGINEERING DECISION (name); BUSINESS (grants) | F-001 C2 precedent; F-002 D8 splits name from grant. |
| E3 | **Outcome 2026-09-24: moot, no `PUT` (§0.8).** **`PUT /routes/{id}/stations`** replaces the whole sequence. No per-position insert or move endpoints. | ENGINEERING DECISION | One request = one validated, audited sequence; no intermediate invalid states. F-002 `PUT /users/{id}/roles` precedent (C4). |
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

### 0.8 Rulings (hein, 2026-09-24)

The stage-2 ⛔ asked hein to rule on OQ36–OQ41 and E1–E8. hein ruled on all of them on 2026-09-24.

**The business rulings (OQ36–OQ41) are provisional tech-lead rulings.** For each one: **Resolved by tech-lead ruling (hein, 2026-09-24; T-032) — not a Myanma Railways answer.** This project chose not to wait for one. **Still open with Myanma Railways.** If Myanma Railways later gives an official, different answer, that supersedes this ruling and needs its own follow-up task. The same wording is in `docs/19-open-questions.md` under each OQ and, for OQ40, in `docs/10-authorization-matrix.md` §Route permission grants.

| ID | Ruling | Label | Applied in |
|---|---|---|---|
| **OQ36** | The system may hold several routes, and routes may share stations. A route has **no direction attribute**: direction belongs to services (FR-003) and fares (OQ17). There is **no separate `Line` concept**: "line" in FR-002 is read as "route", which closes C1, and the glossary's forbidden-synonym rule stands. The YCR loop is entered as one route. No seed data (OQ1). | BUSINESS DECISION — provisional tech-lead ruling, not a Myanma Railways answer | R5, R23; §6; §7 (no direction column) |
| **OQ37** | Each route has a yes/no closed-or-open setting, stored as a column (`IsClosed bit`). A closed route runs from its last station back to its first; that connection comes from the setting, and the first station is never repeated at the end. A station never appears twice in one sequence, so a unique index on the route–station pair applies (`UX_RouteStations_StationId_RouteId` since Amendment 1). Minimum length: **2 stations for an open route, 3 for a closed one**. | BUSINESS DECISION — provisional tech-lead ruling, not a Myanma Railways answer | R6–R8; S8, S9, S28; §7 `IsClosed`, `UX_RouteStations_StationId_RouteId` |
| **OQ38** | Option **(c), immutable.** A route's sequence can never change after creation. To change the network, create a new route and deactivate the old one. There is no `PUT /routes/{id}/stations` and no sequence-replacement operation; S4, S19, S20, S27 and the `RouteStationsReplaced` audit action are removed. `RouteStations` is insert-only. | BUSINESS DECISION — provisional tech-lead ruling, not a Myanma Railways answer | R9, R10; §4 (removals); §6; §7 grants; §8 |
| **OQ39** | An inactive station cannot be placed in a route (`422 Network.RouteStationInactive`). Deactivating a station that is in a route is allowed, and the station stays in the sequence; route reads show it with `isActive = false`. F-001's `DeactivateStation` does **not** change. S16, S18 and `Network.StationInUseByRoute` are dropped; S17's "stays in sequence" case is kept. Accepted consequence: a station deactivated by mistake cannot be placed in a new route until station reactivation ships (accepted for now; no task created). | BUSINESS DECISION — provisional tech-lead ruling, not a Myanma Railways answer | R11, R12; S7, S17; §5; §9 Known limitation |
| **OQ40** | `routes.manage` → `SystemAdministrator` and `RailwayAdministrator`. `routes.read` → all eight roles: `SystemAdministrator`, `RailwayAdministrator`, `StationManager`, `TicketOperator`, `TicketInspector`, `FinanceOfficer`, `Auditor`, `ReportingUser`. Holding `stations.*` gives no route right. | BUSINESS DECISION — provisional tech-lead ruling, not a Myanma Railways answer | R2, R3; §2; `docs/10` §Route permission grants; S14, S15 |
| **OQ41** | A route has a code and a `BilingualName`, under exactly the station rules (F-001 R3/R4; OQ26/OQ27): code 2–10 characters of `A`–`Z` and `0`–`9`, unique across all routes including inactive ones, never reused; `NameEn`/`NameMy` required, 1–100 characters after trimming, not unique. A route can be **deactivated only** (`POST /routes/{id}/deactivate`, `204`, `Network.RouteDeactivated`; again → `422 Network.RouteAlreadyInactive`), and the row is kept. No reactivation, no delete. **No `PATCH /routes/{id}`**: code and names are fixed after creation, because of the immutable model and the F-001 §9 rename-history question. | BUSINESS DECISION — provisional tech-lead ruling, not a Myanma Railways answer | R13–R15; S10, S11, S21, S23, S26, S30; §6; §9 |
| **E1** | ADR-0024 stays **Proposed** and F-003 does not use it. No `version` / `expectedVersion`, no `rowversion` on `Routes`. The ADR file and its README row are unchanged, except for a Proposed-stage review note added to its Follow-up (hein review, 2026-09-24). | ENGINEERING DECISION (tech lead, hein, 2026-09-24) | R17; §6; §7 |
| **E2** | Add the permission name `routes.read` to the `docs/10` inventory. | ENGINEERING DECISION (tech lead, hein, 2026-09-24) | R3; `docs/10` |
| **E3** | Moot: there is no `PUT` (OQ38). | — | — |
| **E4** | Accepted. `POST /routes` creates the route with its full sequence. | ENGINEERING DECISION (tech lead, hein, 2026-09-24) | S1; §6 |
| **E5** | Accepted. `Route` is its own aggregate, owning its `RouteStation` rows, with an FK to `network.Stations` and no navigation property. | ENGINEERING DECISION (tech lead, hein, 2026-09-24; ADR-0012 item 6) | R16; §7 |
| **E6** | Accepted. `Position` is `1..n` and contiguous. | ENGINEERING DECISION (tech lead, hein, 2026-09-24; ADR-0014) | R18; S24 |
| **E7** | Accepted. Grants to `ycr_app` in a reviewed migration: `Routes` `SELECT`, `INSERT`, `UPDATE(IsActive)` only; `RouteStations` `SELECT`, `INSERT` only; no `DELETE`, no other `UPDATE`, no DDL. **Amended by A1:** `Routes` `UPDATE(IsActive, DeactivatedAtUtc)`. | ENGINEERING DECISION (tech lead, hein, 2026-09-24; ADR-0017 item 3 spirit) | R10; §7; S25 |
| **E8** | Accepted. Error codes follow ADR-0004. `Routes.IsActive` is the EF concurrency token for deactivation (F-001 Amendment 1 pattern); no `rowversion` on `Routes`. | ENGINEERING DECISION (tech lead, hein, 2026-09-24; ADR-0004) | R17; §6; §7; S23 |

**Decisions at approval (hein, 2026-09-24):**

| ID | Decision | Label | Applied in |
|---|---|---|---|
| **A1** | Add `DeactivatedAtUtc datetimeoffset(3) NULL` to `network.Routes`, with `CK_Routes_DeactivatedAtUtc_Utc` (NULL or offset 0). Deactivation sets `IsActive = 0` and `DeactivatedAtUtc` = now (UTC, from the clock) in the same `UPDATE`. `RouteResponse`, `RouteSummaryResponse` and the audit snapshot gain `deactivatedAtUtc` (null while active). Grant: `Routes` `SELECT`, `INSERT`, `UPDATE(IsActive, DeactivatedAtUtc)`. Reason: because sequences are immutable, the rows are the business history, and "which routes were active on date X" must be answerable without reading the audit ledger. | ENGINEERING DECISION (tech lead, hein, 2026-09-24) | R10, R15, R17; S23, S25; §6; §7; §8 |
| **R25** | `isClosed` required on `POST /routes`, no default: accepted. | ENGINEERING DECISION (tech lead, hein, 2026-09-24) | R25; S5 |
| **IX** | `IX_RouteStations_StationId` is left to PLAN: kept only if a named query uses it, otherwise dropped at stage 3. **Outcome: dropped** (no F-003 query uses it; plan §DB changes), by the mechanism of Amendment 1 (§0.9). | ENGINEERING DECISION (tech lead, hein, 2026-09-24) | §7; Notes for the next stage |
| **Race** | A route created while one of its stations is being deactivated: accepted, no serialisation required. | ENGINEERING DECISION (tech lead, hein, 2026-09-24) | §5 |
| **ADR-0024** | Stays Proposed. Its Follow-up line naming F-003 as first user is replaced by "F-003 does not use this ADR (OQ38 ruling, immutable sequences; hein, 2026-09-24). The first user will be the first feature that edits a document composed from an earlier read." | ENGINEERING DECISION (tech lead, hein, 2026-09-24) | ADR-0024 Follow-up |

### 0.9 Amendments after approval

Recorded after the spec was Approved. They come from hein's rulings on the stage-3 plan questions (T-034, `plan.md` §Questions for hein). The approval stands; these amendments change only what they name.

| ID | Amendment | Label | Applied in |
|---|---|---|---|
| **Amendment 1 (hein, 2026-09-24, T-034 Q1)** | `UX_RouteStations_RouteId_StationId` on `(RouteId, StationId)` is replaced by **`UX_RouteStations_StationId_RouteId` on `(StationId, RouteId)`**. It enforces the same rule: a station appears at most once in one route. **`IX_RouteStations_StationId` is dropped.** No F-003 query filters `RouteStations` by `StationId` alone, and stations are never deleted, so nothing needs a separate index (the "IX" decision above). EF Core adds an index for every foreign key that no other index *leads* with, and adds it back if it is removed (verified on EF Core 10.0.12 at PLAN, `plan.md` O1–O3). With `StationId` as the unique index's leading column, the foreign key is covered and EF adds no extra index. | ENGINEERING DECISION (tech lead, hein, 2026-09-24; T-034 Q1 (a)) | R7; §7 |
| **Amendment 2 (hein, 2026-09-24, T-034 Q2, Q3)** | **Q2:** `stationIds` has at most **200** elements; more is `400 Common.ValidationFailed` (new **R26**, **S5**). **Q3:** spec §9's glossary line is narrowed. Editing `docs/glossary.md` is out of scope for stages 1–2 only, and stage 8 updates it per `docs/21`. Editing `docs/business/mr-questions-pack.md` stays out of scope. | Q2: ENGINEERING DECISION / REQUIRED CONTROL (tech lead, hein, 2026-09-24; T-034 Q2 (b)). Q3: ENGINEERING DECISION (tech lead, hein, 2026-09-24; T-034 Q3 (a)) | R26; S5; §6; §9 |

---

## 1. Goal

Railway administrators need to define the network's routes as ordered sequences of existing stations, so that later features (fares, services, timetables) have one authoritative place to read a route from, and every route creation and withdrawal is authorised, validated and audited.

---

## 2. Actors and permissions

| Actor | Permission | Held by |
|---|---|---|
| Staff user managing routes (create, deactivate) | `routes.manage` | `SystemAdministrator`, `RailwayAdministrator` — provisional tech-lead ruling (hein, 2026-09-24; T-032, OQ40), not a Myanma Railways answer |
| Staff user reading routes | `routes.read` | All eight roles: `SystemAdministrator`, `RailwayAdministrator`, `StationManager`, `TicketOperator`, `TicketInspector`, `FinanceOfficer`, `Auditor`, `ReportingUser` — same ruling |
| Staff user deactivating stations | `stations.manage` | Unchanged from F-001; the route rulings do not affect it |

Holding `stations.manage` or `stations.read` gives no route right, and holding a route permission gives no station right. Grants are data seeded by a reviewed migration (F-002 R11 mechanism); no API edits them.

---

## 3. Business rules

Provisional rulings are labelled "BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-24; T-032, OQnn), not a Myanma Railways answer", shortened below to **PROVISIONAL RULING (OQnn)**. Each is still open with Myanma Railways; an official, different answer supersedes the ruling and needs its own follow-up task.

| # | Rule | Label | Source |
|---|---|---|---|
| R1 | Administrators can define railway lines/routes and ordered station sequences. | FACT | `docs/01` §FR-002 |
| R2 | Creating and deactivating routes requires `routes.manage`, held by `SystemAdministrator` and `RailwayAdministrator`. | Permission name: FACT. Grants: **PROVISIONAL RULING (OQ40)** | `docs/10` §Permission inventory, §Route permission grants |
| R3 | Reading routes requires `routes.read`, held by all eight roles. Station permissions give no route right. | Name: ENGINEERING DECISION (tech lead, hein, 2026-09-24, E2). Grants: **PROVISIONAL RULING (OQ40)** | `docs/10` §Route permission grants; F-001 spec C2 precedent |
| R4 | A route is an ordered sequence of stations, in the `Network` module. | FACT | `docs/glossary.md` (`Route`); `docs/04`; `docs/05` |
| R5 | The system may hold several routes, and routes may share stations. A route has no direction attribute; direction belongs to services and fares. There is no `Line` concept: "line" in R1 means "route". The YCR loop is entered as one route. | **PROVISIONAL RULING (OQ36)** | `docs/19` OQ36 |
| R6 | Each route is either open or closed, stored as `IsClosed`. A closed route runs from its last station back to its first; that connection comes from `IsClosed`, and the first station is never repeated at the end of the sequence. | **PROVISIONAL RULING (OQ37)** | `docs/19` OQ37 |
| R7 | A station appears at most once in one route's sequence. `UX_RouteStations_StationId_RouteId` (unique on `(StationId, RouteId)`, Amendment 1) is the database authority; the handler's pre-check gives the error code (`422 Network.RouteStationRepeated`). | **PROVISIONAL RULING (OQ37)**; enforcement: ENGINEERING DECISION (tech lead, hein, 2026-09-24; ADR-0004) | `docs/19` OQ37 |
| R8 | A route has at least 2 stations if open and at least 3 if closed; fewer is `422 Network.RouteTooFewStations`. | **PROVISIONAL RULING (OQ37)** | `docs/19` OQ37 |
| R9 | A route's sequence, `IsClosed`, code and names never change after creation. To change the network, an administrator creates a new route and deactivates the old one. There is no sequence-replacement operation. | **PROVISIONAL RULING (OQ38 option (c), OQ41)** | `docs/19` OQ38, OQ41 |
| R10 | Database grants to `ycr_app`, by a reviewed migration: `network.Routes` `SELECT`, `INSERT`, `UPDATE(IsActive, DeactivatedAtUtc)` only; `network.RouteStations` `SELECT`, `INSERT` only. No `DELETE`, no other `UPDATE`, no DDL. The grant set is the database-level statement of R9. | ENGINEERING DECISION (tech lead, hein, 2026-09-24, E7 as amended by A1; ADR-0017 item 3 spirit) | `Security_AppDatabaseRole` precedent |
| R11 | An inactive station cannot be placed in a route: `422 Network.RouteStationInactive`, nothing written. | **PROVISIONAL RULING (OQ39)** | `docs/19` OQ39 |
| R12 | Deactivating a station that is in a route is allowed. The station stays in every sequence it is in, and route reads show it with `isActive = false`. F-001's `DeactivateStation` does not change. A station deactivated by mistake cannot be placed in a new route until station reactivation ships; this is accepted for now and no task is created. | **PROVISIONAL RULING (OQ39)** | `docs/19` OQ39; F-001 R1, R3, §9 |
| R13 | A route has a code and a `BilingualName`. Code: 2–10 characters of `A`–`Z` and `0`–`9` (`400 Network.InvalidRouteCode`). `NameEn` and `NameMy`: both required, each 1–100 characters after trimming (`400 Network.InvalidRouteName`); neither is unique. These are exactly the station rules. | **PROVISIONAL RULING (OQ41)** | `docs/19` OQ41; F-001 R3/R4; OQ26/OQ27 |
| R14 | A route code is unique across all routes, including inactive ones, and is never reused: `409 Network.RouteCodeAlreadyExists`. `UX_Routes_Code` is the concurrency authority. | **PROVISIONAL RULING (OQ41)** | `docs/19` OQ41; F-001 R3 |
| R15 | A route can be deactivated only; the row and its sequence are kept. There is no reactivation, no delete and no `PATCH /routes/{id}`. Deactivating an inactive route is `422 Network.RouteAlreadyInactive`. Deactivation sets `IsActive = 0` and `DeactivatedAtUtc` = now (UTC, from the clock, `TimeProvider.GetUtcNow()`) in the same `UPDATE`; `DeactivatedAtUtc` is null while the route is active. | Deactivate only: **PROVISIONAL RULING (OQ41)**. `DeactivatedAtUtc`: ENGINEERING DECISION (tech lead, hein, 2026-09-24, A1) | `docs/19` OQ41; §0.8 A1 |
| R16 | Every station in a sequence exists in `network.Stations`. The database foreign key is the authority; the handler's pre-check gives the error code (`422 Network.RouteStationNotFound`). | ENGINEERING DECISION (tech lead, hein, 2026-09-24, E5; ADR-0012 item 6) | `docs/07` §Rules ("Use foreign keys"); AGENTS.md rule 5 |
| R17 | Concurrent deactivations of one route: `Routes.IsActive` is the EF concurrency token, so exactly one succeeds and the other gets `422 Network.RouteAlreadyInactive` with no audit event and changes neither `IsActive` nor `DeactivatedAtUtc` (the winner's timestamp stands). `Routes` has no `rowversion`, and no request or response carries a version. | ENGINEERING DECISION (tech lead, hein, 2026-09-24, E1/E8; F-001 Amendment 1 pattern) | F-001 §7; `DeactivateStationHandler` |
| R18 | A station's position in a route is a 1-based contiguous ordinal within that route. It is not the ADR-0014 station short index and not the station code, and F-003 neither reads nor allocates the short index. | ENGINEERING DECISION (tech lead, hein, 2026-09-24, E6; ADR-0014) | ADR-0014 §Decision; `docs/glossary.md` (`Station index`) |
| R19 | Route identifiers are application-generated GUIDs through `IIdGenerator`, mapped `ValueGeneratedNever()`. | ENGINEERING DECISION | ADR-0006 §Decision item 1 and 2026-09-19 amendment |
| R20 | Creating a route and deactivating a route are audited, with actor fields from the authenticated server-side context only. | ENGINEERING DECISION | ADR-0017 §Decision items 1–2; ADR-0021; F-001 R10 precedent |
| R21 | Route management is not financial or retryable; no `Idempotency-Key`. | ENGINEERING DECISION | `docs/20` §5 (required list excludes master data); F-001 R11 |
| R22 | Instants are `DateTimeOffset` / `datetimeoffset(3)` UTC. Routes own no `BusinessDate` and no effective dates. | ENGINEERING DECISION | ADR-0018 §Time; ADR-0019 |
| R23 | No real route data ships: no seed migration and no fixture of the YCR loop. Tests build their own stations and routes. The real loop is entered later as one route (R5). | FACT (consequence of OQ1, open) | `docs/19` OQ1; F-001 §Blocked behaviour precedent |
| R24 | Route responses show each station's **current** code, names and active flag, read at query time; a route row stores only `StationId`. | ENGINEERING DECISION | E5; G8 |
| R25 | `isClosed` is a required boolean on `POST /routes`; a missing value is `400 Common.ValidationFailed`. There is no default, so the API never picks a topology on the caller's behalf. | ENGINEERING DECISION (tech lead, hein, 2026-09-24) | R6; AGENTS.md rule 1 |
| R26 | `stationIds` on `POST /routes` has at most 200 elements; a longer list is `400 Common.ValidationFailed`, and nothing is read or written. This is an input-size limit against API abuse, not a statement about how long a real route can be. | ENGINEERING DECISION / REQUIRED CONTROL (tech lead, hein, 2026-09-24; Amendment 2, T-034 Q2) | `docs/18` (API abuse); §0.9 |

**Blocking open questions:** none. OQ36–OQ41 are resolved for F-003 by provisional tech-lead rulings and remain open with Myanma Railways. OQ1 (real station and route data) and OQ17 (fare direction) stay open and do not block this spec (R23, §9).

---

## 4. Scenarios (Given / When / Then)

Status codes follow ADR-0004. Every error response is ProblemDetails with `errorCode` and `traceId`. Numbers are stable: removed scenarios keep their number and say why.

### Happy path

- **S1.** Given stations A, B, C exist and are active, and a caller holding `routes.manage`, when they POST `/routes` with `code`, `nameEn`, `nameMy`, `isClosed = false` and `stationIds = [A, B, C]`, then the response is `201 Created` with `Location: /api/v1/routes/{id}` and `{ id }`, `network.Routes` has one row with `IsClosed = 0` and `IsActive = 1`, `network.RouteStations` has positions 1, 2, 3 for A, B, C, and one `Network.RouteCreated` audit event is written.
- **S2.** Given that route, when a caller holding `routes.read` GETs `/routes/{id}`, then `200` with `RouteResponse` including `code`, `nameEn`, `nameMy`, `isClosed`, `isActive` and `stations` in position order, each with the station's current code, names and `isActive` (R24). No EF entity is serialised (AGENTS.md rule 4).
- **S3.** Given three routes, one of them inactive, when a caller holding `routes.read` GETs `/routes?page=1&pageSize=50`, then `200` with `items`, `page`, `pageSize`, `totalCount`, ordered by code, and the inactive route is listed with `isActive = false`.
- **S4.** *Removed (OQ38 ruling, 2026-09-24): a sequence cannot be replaced.*

### Failures, each with its error code

- **S5.** Validation → `400 Common.ValidationFailed`: missing `stationIds`; empty `stationIds`; a non-GUID station id; missing `code`, `nameEn` or `nameMy`; missing `isClosed` (R25); more than 200 `stationIds` (R26, Amendment 2). A list of exactly 200 passes validation and reaches the handler.
- **S6.** A station id that does not exist → `422 Network.RouteStationNotFound`; nothing is written (R16).
- **S7.** An inactive station in the sequence → `422 Network.RouteStationInactive`; nothing is written (R11).
- **S8.** A station repeated in the sequence → `422 Network.RouteStationRepeated`; nothing is written (R7). This includes `[A, B, C, A]` with `isClosed = true`: a closed route never repeats its first station (R6).
- **S9.** Minimum length (R8), each case nothing written on failure:
  - open route (`isClosed = false`) with 1 station → `422 Network.RouteTooFewStations`; with 2 stations → `201`;
  - closed route (`isClosed = true`) with 2 stations → `422 Network.RouteTooFewStations`; with 3 stations → `201`.
- **S10.** A code already used by an active route → `409 Network.RouteCodeAlreadyExists`; nothing is written (R14).
- **S11.** A malformed code (1 character, 11 characters, lowercase, punctuation) → `400 Network.InvalidRouteCode`; a blank, whitespace-only or 101-character name after trimming → `400 Network.InvalidRouteName` (R13).
- **S12.** GET, or POST `/routes/{id}/deactivate`, for an unknown route id → `404 Network.RouteNotFound`.
- **S13.** `pageSize=201` → `400 Network.InvalidPageRequest` (`docs/20` §4; existing code).
- **S14.** Anonymous request to any `/routes` endpoint → `401 Auth.Unauthenticated`.
- **S15.** Authenticated without the permission → `403`: a caller with only `routes.read` POSTs `/routes` or POSTs `/routes/{id}/deactivate`; a caller with only `stations.manage` POSTs `/routes`; a caller with only `stations.read` GETs `/routes` (R3).

### Station deactivation

- **S16.** *Removed (OQ39 ruling, 2026-09-24): station deactivation is never refused because of routes.*
- **S17.** Given station B is in an active route, when a caller holding `stations.manage` POSTs `/stations/{B}/deactivate`, then `204` exactly as in F-001; the route's rows are unchanged, and S2 shows B at its position with `isActive = false` (R12).
- **S18.** *Removed (OQ39 ruling, 2026-09-24).*

### Concurrency and data integrity

- **S19.** *Removed (OQ38 ruling and E1, 2026-09-24): no sequence replacement, no client-held version.*
- **S20.** *Removed (OQ38 ruling and E1, 2026-09-24).*
- **S21.** Two parallel POSTs with the same route code → one `201`, one `409 Network.RouteCodeAlreadyExists` through the unique-index violation (ADR-0004); exactly one route row and one `Network.RouteCreated` event.
- **S22.** An audit event's actor fields come from the authenticated context; actor fields supplied in the request body are ignored (ADR-0017 §2; F-001 S20).
- **S23.** Route deactivation (R15, R17): `POST /routes/{id}/deactivate` on an active route → `204` and one `Network.RouteDeactivated` event; the row has `IsActive = 0` and `DeactivatedAtUtc` set to the clock's UTC now, both written by the same `UPDATE`; the route row and its `RouteStations` rows are kept, and S2 shows `isActive = false`, `deactivatedAtUtc` set and the unchanged sequence. Again → `422 Network.RouteAlreadyInactive`, no event, and `DeactivatedAtUtc` keeps its first value. **Two in parallel on the same active route → exactly one `204`, one `422 Network.RouteAlreadyInactive`, and exactly one `Network.RouteDeactivated` event; the losing request changes neither `IsActive` nor `DeactivatedAtUtc`** (F-001 S27 pattern). An active route reads back with `deactivatedAtUtc = null` (S2, S3).
- **S24.** After a successful create the sequence is contiguous `1..n`; a failed create writes neither the route nor any `RouteStations` row (one `SaveChangesAsync`, ADR-0004).
- **S25.** Database privileges (R10): `ycr_app` holds `SELECT`, `INSERT` and `UPDATE(IsActive, DeactivatedAtUtc)` on `network.Routes` and `SELECT`, `INSERT` on `network.RouteStations`. A test asserts the absences as well: no `DELETE` on either table; no `UPDATE` of `Routes.Id`, `Code`, `NameEn`, `NameMy`, `IsClosed` or `CreatedAtUtc`; no `UPDATE` on `RouteStations`; no DDL (F-001 S22 pattern).
- **S26.** A route's Myanmar name round-trips unchanged, using real Myanmar Unicode text (F-001 S14 pattern).
- **S27.** *Removed (OQ38 ruling, 2026-09-24): no sequence versions.*
- **S28.** Closed route read-back: given a closed route created from `[A, B, C]`, GET returns `isClosed = true` and exactly three stations, positions 1–3, with A only at position 1 (R6).
- **S29.** Routes share stations: given route R1 with `[A, B, C]`, a second route R2 with `[B, C, D]` is created → `201`; both read back with their own positions (R5).
- **S30.** Code reuse from an inactive route: given route `R1` was deactivated, a POST `/routes` with code `R1` → `409 Network.RouteCodeAlreadyExists`; nothing is written (R14).

---

## 5. State changes

| Entity | From | Event | Guard | To |
|---|---|---|---|---|
| Route | (none) | `CreateRoute` | `routes.manage`; code and names valid (R13) and code unused by any route (R14); every station exists (R16) and is active (R11); no repeated station (R7); minimum length for its `IsClosed` value (R8) | Active |
| Route | Active | `DeactivateRoute` | `routes.manage` | Inactive, `DeactivatedAtUtc` set (A1) |
| Route | Inactive | `DeactivateRoute` | — | rejected, `422 Network.RouteAlreadyInactive` |
| Station | Active | `DeactivateStation` | `stations.manage` (unchanged from F-001) | Inactive; stays in every route sequence (R12) |

A route has no other transition: its sequence, `IsClosed`, code and names never change (R9), and an inactive route stays inactive (R15).

**Race between route creation and station deactivation — accepted, no serialisation (ENGINEERING DECISION, tech lead, hein, 2026-09-24).** A route creation can race a deactivation of one of its stations: the creation reads the station as active, the deactivation commits, then the creation commits. The end state is the same as the serial order "create, then deactivate", which the OQ39 ruling allows (the station stays in the sequence, shown inactive). The active-station check (R11) is therefore not serialised with station deactivation, and no scenario tests the interleaving.

---

## 6. API

Base path `/api/v1`, JSON camelCase, GUID ids, ProblemDetails with `errorCode` and `traceId` (`docs/20` §4; ADR-0004). No endpoint takes an `Idempotency-Key` (R21). No request or response carries a version token (R17).

| Method | Path | Request | Success | Error codes | Permission |
|---|---|---|---|---|---|
| POST | `/routes` | `CreateRouteRequest { code, nameEn, nameMy, isClosed, stationIds: Guid[] }` — the complete sequence, in order, at most 200 ids (R26) | `201` + `CreateRouteResponse { id }` and `Location` | `400 Common.ValidationFailed` · `400 Network.InvalidRouteCode` · `400 Network.InvalidRouteName` · `401` · `403` · `409 Network.RouteCodeAlreadyExists` · `422 Network.RouteStationNotFound` · `422 Network.RouteStationInactive` · `422 Network.RouteStationRepeated` · `422 Network.RouteTooFewStations` | `routes.manage` |
| GET | `/routes/{id}` | — | `200` + `RouteResponse { id, code, nameEn, nameMy, isClosed, isActive, createdAtUtc, deactivatedAtUtc, stations: [{ position, stationId, code, nameEn, nameMy, isActive }] }` | `401` · `403` · `404 Network.RouteNotFound` | `routes.read` |
| GET | `/routes` | `?page=1&pageSize=50` (max 200); ordered by code; inactive routes included | `200` + `{ items: RouteSummaryResponse[], page, pageSize, totalCount }`; `RouteSummaryResponse { id, code, nameEn, nameMy, isClosed, isActive, stationCount, createdAtUtc, deactivatedAtUtc }` | `400 Network.InvalidPageRequest` · `401` · `403` | `routes.read` |
| POST | `/routes/{id}/deactivate` | — | `204` | `401` · `403` · `404 Network.RouteNotFound` · `422 Network.RouteAlreadyInactive` | `routes.manage` |
| POST | `/stations/{id}/deactivate` | unchanged | unchanged | unchanged — no new error code | `stations.manage` (existing F-001 endpoint, not modified) |

**Deliberately absent:**
- **No `PUT /routes/{id}/stations`** and no other sequence-replacement operation (OQ38 ruling, R9).
- **No `PATCH /routes/{id}`**, although `docs/08` lists `PATCH /routes`: code and names are fixed after creation, because of the immutable model and the F-001 §9 rename-history question (OQ41 ruling, R15). `docs/08` is updated at stage 8.
- No reactivation and no `DELETE` (R15).

---

## 7. Data

Schema `network` (ADR-0012; `docs/07` §Module schemas). Every `*Utc` column carries `CK_<Table>_<Column>_Utc` (`DATEPART(TZOFFSET, …) = 0`), declared in the EF model as well as the migration (`docs/07` §F-001 station persistence controls). Foreign keys are `NO ACTION`.

### `network.Routes`

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | no | PK, clustered, application-assigned (R19) |
| `Code` | `nvarchar(10)` | no | 2–10 characters, `A`–`Z`/`0`–`9` (R13); unique, never reused (R14) |
| `NameEn` | `nvarchar(100)` | no | owned `BilingualName` (R13) |
| `NameMy` | `nvarchar(100)` | no | Myanmar Unicode, never Zawgyi (`docs/20` §6; OQ29 ruling) |
| `IsClosed` | `bit` | no | open (`0`) or closed (`1`), fixed at creation (R6, R9) |
| `IsActive` | `bit` | no | EF concurrency token for deactivation (R17); `ycr_app` may update it and `DeactivatedAtUtc`, nothing else (R10) |
| `CreatedAtUtc` | `datetimeoffset(3)` | no | UTC (ADR-0018) |
| `DeactivatedAtUtc` | `datetimeoffset(3)` | yes | Null while active; set to the clock's UTC now in the same `UPDATE` that sets `IsActive = 0` (R15, A1). Because sequences are immutable, the rows are the business history, and "which routes were active on date X" must be answerable without reading the audit ledger |

There is no `rowversion` and no direction column.

| Object | Definition | Why |
|---|---|---|
| `PK_Routes` | clustered on `Id` | ADR-0006 |
| `UX_Routes_Code` | unique on `Code` | Concurrency authority for duplicate codes, including inactive routes (R14, S21, S30); matched by name in `NetworkConstraints` |
| `CK_Routes_CreatedAtUtc_Utc` | UTC check | ADR-0018 |
| `CK_Routes_DeactivatedAtUtc_Utc` | `DeactivatedAtUtc IS NULL OR DATEPART(TZOFFSET, [DeactivatedAtUtc]) = 0` | ADR-0018; A1 |

### `network.RouteStations`

| Column | Type | Null | Notes |
|---|---|---|---|
| `RouteId` | `uniqueidentifier` | no | `FK_RouteStations_Routes_RouteId` |
| `Position` | `int` | no | 1-based, contiguous per route (R18) |
| `StationId` | `uniqueidentifier` | no | `FK_RouteStations_Stations_StationId` (R16) |

| Object | Definition | Why |
|---|---|---|
| `PK_RouteStations` | on `(RouteId, Position)` | One station per position. Contiguity (`1..n`, no gaps) and the minimum length (R8) are enforced by the aggregate, since a check constraint cannot see other rows |
| `CK_RouteStations_Position` | `Position >= 1` | Rejects zero and negatives from any writer |
| `UX_RouteStations_StationId_RouteId` | unique on `(StationId, RouteId)` | A station never repeats in one sequence (R7, S8); matched by name in `NetworkConstraints`. `StationId` leads, so the index also covers `FK_RouteStations_Stations_StationId` (Amendment 1) |

**No `IX_RouteStations_StationId` (Amendment 1, hein, 2026-09-24, T-034 Q1).** Its original reason, the station-deactivation check, went away with the OQ39 ruling, and no F-003 query filters `RouteStations` by `StationId` alone. Stations are never deleted, so the foreign key's reverse check never runs. EF Core would add the index by convention for an uncovered foreign key, and adds it back if it is removed. Putting `StationId` first in the unique index covers the foreign key, so EF adds nothing. A later "routes through station X" query can use the unique index.

`RouteStations` is insert-only (R9, R10). The database, not only the application, therefore guarantees that a stored sequence never changes.

### Grants to `ycr_app` (new `Security_NetworkRouteGrants` migration)

| Table | Granted | Deliberately absent |
|---|---|---|
| `Routes` | `SELECT`, `INSERT`, `UPDATE(IsActive, DeactivatedAtUtc)` | `DELETE`; `UPDATE` of any other column |
| `RouteStations` | `SELECT`, `INSERT` | `UPDATE`; `DELETE` |

No DDL anywhere. `DatabasePrivilegeTests` asserts presences and absences (S25).

Migrations are named `yyyyMMddHHmmss_Network_<Change>` (`docs/20` §6) and reviewed per `docs/workflows/04-database-change.md`. Adding tables and an FK to `Stations` alters no existing column. `network.Stations` itself is unchanged.

---

## 8. Audit, logging, metrics

- **Audit** (ADR-0017, ADR-0021; F-001 R10 pattern): through `IAuditWriter`, in the same `SaveChangesAsync` as the change, so a losing concurrent request writes no event (F-001 `DeactivateStationHandler` pattern).

  | Action | When | `BeforeJson` | `AfterJson` |
  |---|---|---|---|
  | `Network.RouteCreated` | S1 | null | snapshot |
  | `Network.RouteDeactivated` | S23 | snapshot | snapshot |
  | `Network.StationDeactivated` | unchanged from F-001 (S17) | — | — |

  `SubjectType` = `Network.Route`, a new constant in `NetworkAuditSubjects` (never a CLR name); `SubjectId` = route id; `AuthorizedByPermission` = `routes.manage`; `PayloadVersion` = 1. The snapshot is an explicit record, never the aggregate: `RouteAuditSnapshot { code, nameEn, nameMy, isClosed, isActive, deactivatedAtUtc, stations: [{ position, stationId, stationCode }] }`. The station code is included so a ledger row is readable without a join, and it is stable because codes are never reused (F-001 R3). No personal data, token or key can appear (ADR-0021 rule 4).
- **History.** Because sequences are immutable (R9), the stored rows are the history: a route that existed on a past date still has exactly the sequence it had then, and `CreatedAtUtc` with `DeactivatedAtUtc` (A1) answers which routes were active on a given date. No feature needs to read the audit ledger as business history.
- **Logging** (`docs/20` §7): message templates, no personal data.
- **Metrics** (`docs/17`): none. No business event listed there belongs to routes; request metrics come from F-001's baseline.

---

## 9. Out of scope

- Fare and direction resolution: OQ17, `RouteSegmentResolver`, `RouteSegment`, fare quotes (`docs/12`). A route carries no direction (R5).
- Services, trains, stopping patterns, operating days and timetables (FR-003, FR-004).
- The ADR-0014 station short index: allocation, storage, the authenticated versioned mapping (G5, R18).
- Station update, reactivate and search (F-001 §9).
- Seed or fixture route data (R23, OQ1).
- **Editing a route in any way** after creation: no sequence replacement, no `PATCH /routes/{id}` for code or names, no reactivation, no delete (R9, R15). Code and names are fixed because of the immutable model and the F-001 §9 rename-history question.
- ADR-0024 (client-held version): stays Proposed and is not used by F-003 (E1).
- Editing `docs/business/mr-questions-pack.md`.
- Editing `docs/glossary.md` is out of scope for stages 1–2; stage 8 updates it per `docs/21` (Amendment 2, hein, 2026-09-24, T-034 Q3).

**Known limitation (accepted, OQ39 ruling, hein, 2026-09-24):** a station deactivated by mistake cannot be placed in a new route until station reactivation ships, and it cannot be recreated because station codes are never reused (F-001 R3). Accepted for now; no task created.

---

## Blocked behaviour

Required by `docs/21` §Specification.

| Behaviour | Blocking item | Effect on F-003 |
|---|---|---|
| Real route data (the YCR loop as one route) | **OQ1** (no authoritative station list) | No seed; tests build their own (R23). The route API is not blocked |
| Fare direction on a closed route | **OQ17** | Out of scope; F-003 stores no direction (R5) |
| Official Myanma Railways answers to OQ36–OQ41 | **Still open with Myanma Railways** | Not blocking: provisional tech-lead rulings apply (§0.8). An official, different answer supersedes the ruling and needs its own follow-up task |

No placeholder rule may be implemented for any of these (AGENTS.md §When a business rule is missing).

---

## Notes for the next stage

- Stage 3 (PLAN, `docs/templates/plan.md`) may start: this spec is Approved (hein, 2026-09-24). It is a separate task and is not started by T-032.
- PLAN writes the route grants into a seed migration (F-002 R11 mechanism) matching `docs/10` §Route permission grants, and the `Security_NetworkRouteGrants` migration (R10). F-001's station scenarios, including S27, must still pass unchanged.
- PLAN keeps `IX_RouteStations_StationId` only if a named query uses it, otherwise drops it (§7). **Done at PLAN:** dropped, by Amendment 1 (§0.9).
- `docs/08` gets the route endpoints and the note that `PATCH /routes` is not provided (stage 8).
- Add OQ36–OQ41 to `docs/business/mr-questions-pack.md` (hein, listed in `progress.md`), marked as provisionally ruled.

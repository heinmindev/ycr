# F-005: Timetables

Status: **Approved (hein, 2026-09-26)**, amended by Amendments 1–3 (hein, 2026-09-27; Amendments 1–2 T-054, §0.10, R50; Amendment 3 T-055, §0.10). Written by claude (T-053). Stages 1–2 of `docs/workflows/02-feature-development.md`. hein's rulings of 2026-09-26 on SC, OQ51–OQ59, H, ADR-0027, E1–E14 and C4/C5, and the final rulings at approval on **OQ60**, **Q2** and the five engineering choices made while applying them, are recorded in §0.10 and applied throughout; the business rulings are **provisional tech-lead rulings — not a Myanma Railways answer**. §0.11 keeps the two items raised while applying the first rulings, now ruled. §0.12 spells out the change to F-004's withdrawal.

Module(s): `Timetable` (existing, F-004); no new cross-module dependency (E12); **changes F-004's `WithdrawService`** (§0.12); cross-cutting `Audit`, `Identity` (permission grants only)
Related: FR-004 (and FR-003 through F-004), UC 4 "Publish schedules" (`docs/03-use-cases.md` §Core use cases, item 4), F-004 (services), ADR-0002, ADR-0004, ADR-0006, ADR-0012, ADR-0013, ADR-0017, ADR-0018, ADR-0019, ADR-0021, ADR-0025, ADR-0026, ADR-0027 (Accepted 2026-09-26); ADR-0024 (Proposed) is not used (OQ56 ruling)

Decision owner:
- Business rules (what a timetable version is and contains, stop times, past-midnight running, how versions relate to service periods and to each other, drafts, publication and its approval, withdrawal of a published version, role grants): **Myanma Railways**, routed through `hein` (`docs/19-open-questions.md` OQ51–OQ59). Suggested routing label, following `docs/business/mr-questions-pack.md`: Network Operations / Planning (timetabling role to be nominated), as for OQ42–OQ50. **For F-005, hein gave provisional tech-lead rulings on 2026-09-26 (T-053) — not a Myanma Railways answer** (§0.10), including OQ60 at approval. Still open with Myanma Railways.
- Scope boundary and engineering decisions: **tech lead (`hein`)**, recorded as ADRs per `docs/decisions/README.md`.

Authoritative sources: `docs/00`; `docs/01` §FR-003, §FR-004; `docs/03`; `docs/04`; `docs/05`; `docs/07`; `docs/08`; `docs/10`; `docs/11`; `docs/12`; `docs/19`; `docs/20`; `docs/21`; `docs/glossary.md`; ADR-0002/0004/0006/0012/0013/0017/0018/0019/0021/0024/0025/0026; the F-004 spec, plan and review, and the `Timetable` code at `main` `60d624a`. **No Myanma Railways-authoritative source defines any timetable version, stop time, publication rule or timetable permission grant.**

---

## 0. Discovery notes

Stage-1 output. Every note cites its source and names the decision owner. §0.1–§0.9 are kept as written at discovery, with closure notes added; where they list options, proposals or the worked example W, the rulings in §0.10 decide. §0.11 and §0.12 were added when the rulings were applied.

### 0.1 What the sources say

| # | Finding | Source |
|---|---|---|
| D1 | **FACT:** "Authorized users can publish timetable versions with effective dates." This is the whole of FR-004. | `docs/01` §FR-004 |
| D2 | **FACT:** "Authorized users can define train services, operating days, effective periods and stopping patterns." (FR-003, implemented by F-004.) | `docs/01` §FR-003 |
| D3 | **FACT:** core use case 4 is "Publish schedules". No actor is attached. | `docs/03` §Core use cases |
| D4 | **FACT:** `ScheduleVersion` is a candidate aggregate and `SchedulePublished` a candidate event, "proposals… Confirm aggregate boundaries through discovery". ENGINEERING DECISION (ADR-0013): "`ScheduleVersion` remains a Timetable aggregate; Ticketing does not take a direct navigation dependency on it." | `docs/04` §Candidate aggregates, §Candidate domain events, §Boundary clarifications |
| D5 | **FACT:** the `Timetable` module owns "Services, schedules, published timetable versions". | `docs/05` §Context-to-module map |
| D6 | **FACT:** `ScheduleVersions` is a core table; "**Preserve historical fare/schedule versions**"; "use `date` for calendar dates"; "Use foreign keys"; "Exact columns must be designed after requirements discovery". No table for stop times is named. | `docs/07` §Core tables, §Rules |
| D7 | **FACT:** the API proposal lists `POST /schedules/versions` and `POST /schedules/versions/{id}/publish`, and: "Published fare and timetable versions are immutable; do not PATCH a published version." No read, discard, withdraw or cancel endpoint is proposed. | `docs/08` §Initial resources (lines 13–14, 35) |
| D8 | **FACT:** "Versioned policy (fares, timetables): create a version, then publish it. **Never PATCH a published version** (ADR-0002)." So a version exists before it is published (a draft state is implied); nothing says whether a draft may change. | `docs/20` §4 |
| D9 | **FACT:** ADR-0002 is about fares only ("Fare rules must be versioned with effective periods"; "Historical tickets must remain explainable"). Its extension to timetables is made by `docs/20` §4, not by the ADR. | ADR-0002 |
| D10 | **FACT:** `schedules.manage` is in the permission inventory with role grants OPEN QUESTION ("agents must not infer grants from the names"); no read permission for timetable versions exists; the OQ49 ruling deferred `schedules.*` to FR-004. | `docs/10` §Service permission grants, §Permission inventory; `docs/19` OQ49 |
| D11 | **FACT:** glossary `ScheduleVersion` = "A versioned timetable aggregate that can be published and then treated as immutable"; forbidden synonyms "Schedule (when a version is meant), service". Glossary `Effective period` (a service's): "It is not a `ScheduleVersion`'s effective date (FR-004)." | `docs/glossary.md` §Network, timetable, and fares |
| D12 | **FACT (F-004, provisional tech-lead rulings, not Myanma Railways answers):** a service has a code (unique only among overlapping periods), one route, a direction, 2–200 ordered stops with **no times**, days of the week, and an effective period (`EffectiveFrom` required, `EffectiveTo` inclusive or open). It is immutable except for **withdrawal** from a date D ≥ today, which only shortens the period; a withdrawn service may **never run**. "A timetable change is: withdraw the old service from D, and create a new one with the same code from D." | F-004 spec R2, R6–R21, R35, R41, R42; `docs/19` OQ42–OQ50 |
| D13 | **FACT:** F-004 deferred to FR-004 exactly: "Times, `ScheduleVersion` and publication", "`schedules.*`", and the relationship between service periods and version effective dates (C5: "how timetable-version effective dates relate to it is FR-004's question"). | F-004 spec §0.9 B, R2, R22, §0.3 C5 closure note, §9 |
| D14 | **FACT (provisional ruling, OQ47):** a service's operating date is the Asia/Yangon calendar date on which it **starts**, "meaningful only once FR-004 adds times". Holidays and per-date exceptions are **still OPEN** with Myanma Railways; a service runs on its weekdays throughout its period, holidays included. | `docs/19` OQ47; F-004 spec R17, R18, §9 |
| D15 | **FACT:** `ycr_app` may `UPDATE` only `EffectiveTo` and `WithdrawnAtUtc` on `timetable.Services`, and nothing on `timetable.ServiceStops`. **Times therefore cannot be added to existing service rows**; they must live in new rows (or in new services). | `docs/07` §F-004 §Grants to `ycr_app` |
| D16 | **FACT:** `timetable.ServiceStops` has PK `(ServiceId, Position)`, so a stop is addressable by that pair, and a foreign key from a stop-time row to it is a same-module key to a primary key. | `docs/07` §F-004 `timetable.ServiceStops` |
| D17 | **FACT:** calendar dates, including effective dates, are `DateOnly` / `date`; instants `DateTimeOffset` / `datetimeoffset(3)` UTC; the zone is configured, never hard-coded; the clock only through `TimeProvider`. **ADR-0018 has no representation for a time of day.** "Today" is `ILocalCalendar.Today()`. | ADR-0018 §Time; `docs/20` §8 |
| D18 | **FACT:** Asia/Yangon is UTC+06:30 with no DST over 2026–2040, asserted by `LocalCalendarTests.AsiaYangon_ResolvesInThisEnvironment`, so every local time on a local date maps to exactly one instant. | F-004 plan Amendment 1, P11 |
| D19 | **FACT:** ADR-0024 (client-held `version` / `expectedVersion` for edits composed from an earlier read) is **Proposed**. Its Context names "fare and timetable drafts" as a future user; its Proposed-stage note: "a change that touches only child rows does not advance the parent's rowversion… the ADR must name how the parent row is forced to update". | ADR-0024 §Context, §Follow-up |
| D20 | **FACT:** ADR-0026 (Accepted 2026-09-26): a handler may open its own transaction and take a keyed `sp_getapplock` **only** for a set invariant one unique index cannot enforce; a rule one unique index can enforce uses the index. | ADR-0026 §Decision items 1–2 |
| D21 | **FACT:** a cross-module read goes through the owning module's `Contracts` interface; no Timetable contract exists (only `INetworkReader`). Within one module, code uses its own `ITimetableDbContext`. | ADR-0012 item 4; ADR-0025; `src/YCR.Application/Network/Contracts/` |
| D22 | **FACT:** OQ4 ("Are tickets tied to a specific train/service?") and OQ19 (validity window; "what must be printed on it (languages, Myanmar numerals)") are open. The signed QR v1 has no service or departure field. | `docs/19` OQ4, OQ19; ADR-0014 §Binary layout v1 |
| D23 | **FACT:** "Timetable management" is in initial scope; "Real-time train tracking" is out of scope. No document names a passenger-facing or printed timetable; `docs/03` lists staff actors only. OQ19's printing and Myanmar-numeral question is about **tickets**. | `docs/00`; `docs/03`; `docs/19` OQ19 |
| D24 | **FACT:** "Travel date" and "Service type" are potential fare inputs; fares are out of F-005's scope. | `docs/12` |
| D25 | **FACT:** master data owns no `BusinessDate`; idempotency is required only for financial and retryable commands. | ADR-0019; `docs/20` §5 |
| D26 | **FACT:** F-003 and F-004 set explicit request-body limits and array caps as REQUIRED CONTROLs (`POST /services` 32 KiB, 200 stops). | F-003 R26–R27; F-004 R31; `docs/08` §F-004 |

### 0.2 The gaps, one by one

**G1 — What a version is and contains.** FR-004 says "timetable versions with effective dates" (D1) and nothing else: not whether one version covers every service on the network, one route's services or one service; not its identity (number, code, bilingual name, label); not whether it has an end date or runs until replaced. → **OPEN QUESTION, OQ51.** Engineering note: a per-service version would duplicate F-004's own dating (a service already has a period and is replaced by withdraw + create, D12); a network-wide or per-route version is a set of services plus their times.

**G2 — Stop times.** No source says which times a stop has (arrival, departure, both; first and last stops), their precision, dwell rules, whether times must increase, whether non-stopping stations get passing times, or whether times vary by weekday (F-004 operating days are weekday flags, D12). → **OPEN QUESTION, OQ52.** Fixed already: times cannot go on service rows (D15); a stop is addressable as `(ServiceId, Position)` (D16).

**G3 — Running past midnight.** The operating date is the start date (D14). Nothing says whether a service may cross midnight, how a later time is written, or the maximum journey length. → **OPEN QUESTION, OQ53.** The representation is an engineering choice that must survive either answer → **ADR-0027 (Proposed)**, minutes after the operating date's local midnight (D17, D18).

**G4 — Versions versus service periods (F-004 C5).** Two dating mechanisms meet (D2, D13): must a service be effective throughout a version, at some point in it, or independently of it? May a version list a withdrawn or never-running service? What does an F-004 withdrawal do to a **published** version that lists the service (D7 says published is immutable; D12 says withdrawal is allowed from any D ≥ today)? And is "withdraw + recreate" (D12) still how a timetable changes, or does a new version replace it? → **OPEN QUESTION, OQ54**; contradictions C1, C2.

**G5 — Versions over time.** One version in force per date or several; automatic supersession by a later start date or explicit end dates; overlaps; gaps (dates with no version); inserting a version before an already-published future one; history kept (D6 says yes, as a FACT). → **OPEN QUESTION, OQ55.** Engineering consequence: if "at most one published version per start date" is the whole invariant, a filtered unique index enforces it and ADR-0026 does not apply; explicit, non-overlapping periods are a set invariant and need an ADR-0026 lock (E6).

**G6 — Drafts.** A draft exists (D8); nothing says whether it may change. Three shapes, each with a different engineering price:
- **(a) Created whole, immutable** (the F-003/F-004 pattern): the draft is created in one request with all services and times; a wrong draft is discarded and created again. No ADR-0024. Cost: one large request; its body limit must fit the largest real timetable, which no source sizes (OQ1).
- **(b) Built up by adding and removing whole service entries** (header first, then one request per service's times; a correction is remove + add). No request edits a document composed from an earlier read, so ADR-0024 item 6 suggests it is not needed — **but** an "add entry" racing "publish" can change a version after it is published unless both write the parent row; that is exactly ADR-0024's child-row note (D19) in another form, and needs a parent-row concurrency token that every entry change updates (E7). Removal needs `DELETE` on draft child rows or a removed flag.
- **(c) Edited freely** (PATCH a draft's times). Needs ADR-0024 Accepted and its child-row note resolved first.

Which one the timetablers need, whether a discarded draft is kept, and whether several drafts may coexist → **OPEN QUESTION, OQ56.**

**G7 — Publication and after.** Who publishes; whether a second person must approve (maker-checker exists only as an ASSUMPTION for refunds, ADR-0013; OQ34 ruled "no second approver" for staff accounts only); lead time, today, or a past start date (OQ50 allowed a past `EffectiveFrom` for services); whether a service may run only once a published version includes it. → **OPEN QUESTION, OQ57.** Whether a published version can be cancelled before it takes effect or withdrawn while in force (D7 forbids PATCH only) → **OPEN QUESTION, OQ58.**

**G8 — Permissions.** `schedules.manage` exists by name only; no read permission (D10). Names are ENGINEERING (E5); grants are BUSINESS → **OPEN QUESTION, OQ59.** A separate publish permission is needed only if OQ57 separates publishing from preparing.

**G9 — Holidays and per-date exceptions (OQ47, still open).** Versions do not answer them: a version applies to a date range; a holiday is a single date on which a service does or does not run. F-005 adds no holiday calendar and no placeholder; F-004's known limitation (services run on their weekdays, holidays included) carries over unchanged. **Not a new OQ.** Rulings table row H asks hein to confirm F-005 proceeds with OQ47 still open.

**G10 — OQ4 and OQ19.** Neither blocks F-005. What each needs from it:
- **OQ4 (tickets tied to a service or departure?)** If yes, a ticket will name a service and an operating date, and a departure time must be reconstructible for any past date. F-005 keeps that possible by: published versions immutable (D7), rows never deleted and ids stable (E9), version-in-force for a date derivable from stored rows (R21–R22). F-005 does **not** decide whether tickets bind to departures, does not add a QR field (D22) and does not build a Timetable contract (E12).
- **OQ19 (validity window, printing)** — its "specific train/service window" option needs departure times, which F-005 provides as data. Printing and Myanmar numerals are about tickets (D23), not timetables.

**G11 — Public or printed timetable.** No source requires one (D23): no passenger actor, no FR, no printing rule for timetables. → **Out of scope** (§9). A published version is readable through the staff API; any public output is a new FR. Not an OQ.

**G12 — Engineering.** Module `Timetable`, schema `timetable` (D5; ADR-0012) — binding. Time representation → ADR-0027 (Proposed). Concurrency: publish races (E6, E7), service withdrawal racing publish (E8). Input limits (D26 → E10). Cross-module: none needed — services are in the same module, and route/station data is already reachable through `INetworkReader` if a read shows it (E12).

**G13 — Out of scope, deliberately not designed:** fares (OQ9, OQ17, OQ21), ticketing, validation, the QR payload, real timetable data (OQ1), real-time tracking (`docs/00`), a public timetable (G11).

### 0.3 Contradictions found

| # | Contradiction | Sources | Proposed handling | Owner |
|---|---|---|---|---|
| C1 | **Two ways to change a timetable.** The OQ48 ruling: "A timetable change is: withdraw the old service from D, and create a new one with the same code from D." FR-004: a timetable changes by publishing a new version with effective dates, which supersedes the old one. Neither mentions the other. | `docs/19` OQ48; F-004 spec R20; `docs/01` §FR-004; `docs/08` | Folded into OQ54 (last part). Not resolved by inference. | Myanma Railways; hein |
| C2 | **Service withdrawal vs published-version immutability.** F-004 lets any service be withdrawn from any D ≥ today. A published version is immutable (no PATCH). Withdrawing a service a published version lists changes what that version runs from D without changing the version's rows — or, if refused, makes F-004's `WithdrawService` depend on versions. | F-004 spec R21; `docs/08` line 35; `docs/20` §4 | Folded into OQ54. If "refused", `WithdrawService` changes (same module, no ADR-0025 issue) and needs a lock shared with publish (E8). | Myanma Railways; hein |
| C3 | **Terminology.** FR-004 "timetable versions"; UC 4 "Publish **schedules**"; `docs/04` and the glossary `ScheduleVersion` (glossary forbids "Schedule" when a version is meant); `docs/05` "schedules, published timetable versions" (as if two things); `docs/08` `/schedules/versions`; `docs/10` `schedules.manage`. | `docs/01`; `docs/03`; `docs/04`; `docs/05`; `docs/08`; `docs/10`; glossary | ENGINEERING (E1): the aggregate is `ScheduleVersion`, table `timetable.ScheduleVersions`, paths `/schedules/versions` (as `docs/08`), permissions `schedules.*` (as `docs/10`). "Timetable version" is read as the same thing. The glossary is not edited in this stage; stage 8 decides whether "timetable version" is an allowed synonym. | hein; glossary owner |
| C4 | *(Minor.)* `docs/20` §4 cites ADR-0002 for timetable versioning; ADR-0002 is about fares only. | `docs/20` §4; ADR-0002 | No conflict in substance: `docs/20` §4, `docs/07` and `docs/08` are the binding sources for timetables (D6–D9). Reported only. | hein |
| C5 | *(Stale context.)* ADR-0024's Context names `PATCH /routes`, `/trains` and `/services` as future users; the OQ38, OQ42 and OQ48 rulings removed all three. Its remaining named user is "fare and timetable drafts". | ADR-0024 §Context; `docs/19` OQ38, OQ42, OQ48 | If OQ56 → (c) and ADR-0024 is taken up, its Context is refreshed before acceptance (it is Proposed, so it may be edited). | hein |

**Closure notes (hein's rulings, 2026-09-26):** **C1 closed** by OQ54 — timetable changes use both mechanisms: services for stopping patterns, versions for times (R20). **C2 closed** by OQ54 — withdrawing a service is refused while a published version that lists it applies on or after the withdrawal date (R19, §0.12). **C3** handled by E1 (accepted); the glossary is settled at stage 8. **C4** and **C5** are listed for stage 8 and not fixed now (ADR-0024 may take a Proposed-stage note if needed; not needed by this revision).

### 0.4 Engineering choices proposed (hein decides)

| # | Proposal | Label | Why |
|---|---|---|---|
| E1 | **Module, names.** Everything F-005 stores lives in schema `timetable`, behind the existing `ITimetableDbContext` (new `DbSet<ScheduleVersion>` only). Aggregate `ScheduleVersion` (C3); tables `timetable.ScheduleVersions` and its child tables; paths `/schedules/versions`; audit subject `Timetable.ScheduleVersion`; error codes `Timetable.<Reason>`; audit actions `Timetable.ScheduleVersion<Event>`. | ENGINEERING DECISION (ADR-0012 items 2–3; ADR-0004; ADR-0021; `docs/20` §1–§2) | Binding patterns; C3. |
| E2 | **`ScheduleVersion` owns its entries and times.** Child rows reference a service by `ServiceId` (same-schema FK to `timetable.Services(Id)`) and a stop by `(ServiceId, Position)` (same-schema FK to `PK_ServiceStops`, D16). No `DbSet` for child rows (F-003/F-004 pattern). Shape in §7 depends on OQ51/OQ52. | ENGINEERING DECISION (ADR-0012 item 6; `docs/07` "Use foreign keys") | The database is the authority that a time names a real stop of a real service. |
| E3 | **Times** per ADR-0027 (Proposed): an integer offset in minutes (or seconds, OQ52) after local midnight of the operating date; `smallint`; API `HH:mm`, hours past 23 only if OQ53 allows. | ENGINEERING DECISION, proposed in ADR-0027 | D17 (ADR-0018 has no time of day); D18; survives either OQ53 answer. |
| E4 | **Dates.** `EffectiveFrom` (and `EffectiveTo`, if OQ51/OQ55 keep one) are `DateOnly` / `date`, inclusive; "today" only through `ILocalCalendar.Today()`; instants `datetimeoffset(3)` UTC with `CK_…_Utc`; no `BusinessDate`. | ENGINEERING DECISION (ADR-0018; ADR-0019; `docs/20` §8) | D17, D25. |
| E5 | **Permission names.** Add `schedules.read`. Keep `schedules.manage` for prepare/discard/publish; add `schedules.publish` **only if** OQ57 separates publishing from preparing. Grants: OQ59. | ENGINEERING DECISION (name); BUSINESS (grants) | F-001 C2, F-003 E2, F-004 E6 precedent. |
| E6 | **Concurrency of publication follows OQ55.** If the only invariant is "no two published versions share a start date" (supersession by start date), a **filtered unique index** `UX_ScheduleVersions_EffectiveFrom` (`WHERE Status = N'Published'`, within the OQ51 scope) is the authority, mapped to `409`, and **no ADR-0026 lock** is needed (ADR-0026 item 1). If versions carry explicit periods that may not overlap, that is a set invariant: publish takes an ADR-0026 lock `timetable.ScheduleVersions` (network-wide scope) or `timetable.ScheduleVersionScope:<key>` (per route). | ENGINEERING DECISION, conditional on OQ51/OQ55 (ADR-0026) | D20. |
| E7 | **Draft concurrency follows OQ56.** (a) created whole: `Status` is the EF concurrency token for publish/discard (F-003 `IsActive` pattern); no `rowversion`; ADR-0024 unused. (b) entries added/removed: every entry change also updates the parent row (for example a `RowVersion` plus a touched column), and publish checks it, so "add entry" and "publish" cannot both commit; ADR-0024 item 6 means no client-held version is required. (c) free edits: ADR-0024 must be Accepted first, with its child-row note resolved the same way. | ENGINEERING DECISION, conditional on OQ56 (ADR-0024, D19) | G6. |
| E8 | **Service withdrawal vs publish.** If OQ54 lets a published version's services be withdrawn with the version unchanged, the race between publish and withdrawal is **accepted, not serialised** (end state = serial order; F-004 Q3 precedent), with publish re-checking services under its own transaction. If OQ54 refuses such withdrawals, `WithdrawServiceHandler` must read versions, and publish and withdraw must share a lock (publish takes each listed service's `timetable.ServiceCode:<code>` lock in code order, or both take one `timetable.ScheduleVersions` lock); PLAN chooses. | ENGINEERING DECISION, conditional on OQ54 (ADR-0026) | C2. |
| E9 | **No hard delete, stable ids.** No `DELETE` endpoint for a **published** version and no `DELETE` grant on its rows, whatever OQ56/OQ58 rule; a discarded or withdrawn version keeps its row and id (a status). If OQ56 (b) needs removal of a **draft** entry, `DELETE` is granted on draft child rows only if the handler can prove the parent is a draft in the same transaction; otherwise a removed flag. | ENGINEERING DECISION (AGENTS.md rule 5; `docs/07` "Preserve historical … schedule versions"; G10) | Keeps OQ4's departure-bound ticket reconstructible from rows, not the ledger (F-003 §8). |
| E10 | **Input limits.** Every body-carrying endpoint gets an explicit body limit (`413` before binding) and every array a cap: stops per service ≤ 200 (F-004), services per request ≤ a cap set at PLAN. Under OQ56 (a) the whole timetable is one request, and **no source sizes it** (OQ1): PLAN states the size assumption, and if the real timetable could exceed a safe limit, OQ56 (b) is the fallback. | REQUIRED CONTROL (F-003 R26–R27, F-004 R31 precedent; `docs/18` API abuse) | D26. |
| E11 | **No `Idempotency-Key`.** | ENGINEERING DECISION (`docs/20` §5) | D25. |
| E12 | **No Timetable contract in F-005.** No other module reads timetables yet; ADR-0025 contracts are created by the first consumer's feature (Ticketing or Fare, after OQ4/OQ19). F-005 reads services through its own context and, only for display, routes and stations through `INetworkReader`. | ENGINEERING DECISION (ADR-0012 item 4; ADR-0025; AGENTS.md rule 11) | D21; no speculative interface. |
| E13 | **Audit snapshot size.** A version's times can be large (services × stops). Proposed snapshot: the header, the listed services `[{ serviceId, serviceCode, stopCount }]`, and a SHA-256 of a canonical serialisation of all stop times; the times themselves are in insert-only rows that never change after publication. Alternative: full times in `AfterJson` (`nvarchar(max)`, larger ledger rows). | ENGINEERING DECISION, proposed (ADR-0021 rule 3; ADR-0017) | Ledger rows are permanent (ADR-0017 §6); the rows are the history. |
| E14 | **Grants to `ycr_app`** by a reviewed `Security_Timetable…` migration, never wider than the endpoints need: header `SELECT`, `INSERT`, `UPDATE` of status columns only; child rows `SELECT`, `INSERT` (plus draft-only removal under E9 if OQ56 (b)). Role grants by an `Identity_Seed…` migration matching `docs/10`. | ENGINEERING DECISION (ADR-0017 item 3 spirit; F-003/F-004 pattern) | Existing pattern. |

**Closure note (hein, 2026-09-26):** E1–E7 and E9–E14 accepted; E6 and E7 take their OQ55/OQ56 branches (filtered unique index; `Status` token; no ADR-0024). **E8 not accepted:** publish, cancel and service withdrawal are serialised by one Timetable-wide ADR-0026 lock (R46). E10's numbers are sized at PLAN for about 200 services × 40 stops (R41). ADR-0027 Accepted.

### 0.5 New OPEN QUESTIONs added to `docs/19`

OQ51 what a version is, its scope, identity and effective dates · OQ52 stop times (arrival/departure, first/last stop, precision, dwell, ordering, passing times, per-weekday times) · OQ53 running past midnight · OQ54 versions vs service effective periods, withdrawn services, withdrawal of a service in a published version, and C1 · OQ55 how versions follow one another (one in force, supersession, overlap, gaps, insertion, history) · OQ56 how a draft is prepared (created whole, built up, edited; discard; several drafts) · OQ57 who publishes, maker-checker, lead time, past start date · OQ58 cancelling or withdrawing a published version · OQ59 role grants for `schedules.*`. Each has a **BLOCKS:** line. `docs/business/mr-questions-pack.md`, the glossary and `docs/10` are **not** edited in this stage; `progress.md` lists the nine for hein.

**Closure note (2026-09-26):** OQ51–OQ59 are resolved by hein's provisional tech-lead rulings (§0.10), with resolution blocks in `docs/19`. Applying them raised **OQ60** (§0.11), added to `docs/19` and resolved at approval by a provisional tech-lead ruling (§0.10, resolution block in `docs/19`).

### 0.6 What discovery did **not** find

No document defines: any YCR timetable, version or time; what a version contains or its scope; its identity; any stop-time, dwell, precision or past-midnight rule; how versions relate to service periods or to each other; whether drafts change; who publishes or approves; whether a published version can be withdrawn; who manages or reads versions; a public timetable. All business rules → OQ51–OQ59. Missing engineering rule: a time-of-day representation → ADR-0027 (Proposed).

### 0.7 Where the existing code constrains the design

- `ITimetableDbContext` exposes `Services` and `Database`; `ScheduleVersion` is added beside it (E1). `Service.EffectiveFrom`, `EffectiveTo`, `NeverRuns`, `OperatingDays` and `Stops` give every fact a version check needs, in the same module (no contract).
- `Service` has no times and cannot get them (D15). A stop time refers to `(ServiceId, Position)`; `Position` is 1-based and contiguous, and a full circuit's closing stop is the last position (F-004 R14, S46), so "first stop" and "last stop" are position 1 and position *k*.
- `ILocalCalendar.Today()` exists and is the only "today" (`docs/20` §8).
- `IServiceCodeLock` / `SqlServerServiceCodeLock` exist (`timetable.ServiceCode:<code>`); E8's shared-lock option would reuse or extend that pattern.
- `WithdrawServiceHandler` does not know about versions; it changes only if OQ54 refuses withdrawal of a published version's service (C2, E8).

### 0.8 Proposed scope boundary — options for hein, not a decision

FR-004 is small in words (D1) but, with its gaps, larger than F-004. Options:

| Option | F-005 contains | Later feature(s) contain | For | Against |
|---|---|---|---|---|
| **SC1 — All of FR-004** | Versions, times, drafts per OQ56, publish (and approval per OQ57), supersession, withdraw/cancel per OQ58, reads incl. "version in force on date D". | Nothing. | One model, one set of rulings; the first deliverable is a complete timetable. | Every one of OQ51–OQ59 must be ruled before approval; largest slice. |
| **SC2 — Core first** | Versions created **whole** (OQ56 (a)), times, publish, supersession, discard of a draft, reads incl. "in force on date D". | Withdraw/cancel of a published version (OQ58), maker-checker if later required (OQ57), incremental drafts (OQ56 (b)/(c)), holiday exceptions (OQ47), a Timetable contract for Ticketing (OQ4). | Smallest coherent slice; no ADR-0024; OQ58 need not be ruled now (only "not in F-005"). | A mistaken published version can be corrected only by publishing a later one; a large network needs one large request (E10). |
| **SC3 — Times without versions** | Stop times attached per service (new services, D15), no `ScheduleVersion`. | Versions and publication. | Smallest. | Contradicts FR-004's substance ("publish timetable versions"); C1 unresolved; rework when versions arrive. |

At discovery, §4–§7 were written against a **worked example W** (§0.9) inside SC2 so that PLAN would have a concrete shape. W was an illustration, not a recommendation and not a ruling.

**Outcome (hein, 2026-09-26): SC2, plus cancelling a published version before it takes effect.** SC1 and SC3 are kept above as history only. §1–§9 now describe the ruled design, not W.



| # | W assumes | Open question |
|---|---|---|
| W1 | One version covers **every service on the network**. Identity: `Id` plus a `BilingualName` (station name rules, not unique). One `EffectiveFrom`; no end date. At least one service. | OQ51 |
| W2 | Whole minutes. Stop 1: departure only. Last stop (including a full circuit's closing stop): arrival only. Other stops: arrival and departure, departure ≥ arrival (dwell may be 0). Each arrival is later than the previous stop's departure. No passing times. The same times on every operating day. | OQ52 |
| W3 | No running past midnight: times `00:00`–`23:59` of the operating date. | OQ53 |
| W4 | A listed service must not have ended before the version's `EffectiveFrom` and must not be a never-running service; it may start later. On date *d* a service runs iff *d* is in its own period, *d*'s weekday is an operating day, and the version in force on *d* lists it. Withdrawing a listed service stays allowed; the version is unchanged. | OQ54 |
| W5 | The version in force on *d* is the **published** version with the greatest `EffectiveFrom` ≤ *d*. No two published versions share an `EffectiveFrom`. Before the first published version, none is in force. Inserting a version between two published ones is allowed. Every version is kept. | OQ55 |
| W6 | A draft is created **whole** and never changed; a wrong draft is **discarded** (row kept, status `Discarded`); several drafts may exist. | OQ56 |
| W7 | Any holder of the publish right may publish, including the draft's author; no second approver. `EffectiveFrom` ≥ today at creation and again at publication (today allowed). | OQ57 |
| W8 | A published version cannot be withdrawn or cancelled in F-005. | OQ58 |
| W9 | `schedules.manage` for create, discard and publish; `schedules.read` for reads. Grants: none assumed. | OQ59 |

**Outcome (hein, 2026-09-26):** W is replaced by the rulings in §0.10. W matched them except: W8 (no cancellation) — a published version may now be cancelled before it takes effect (OQ58); W9 — cancel is added to `schedules.manage`; W1 — identity adds a system-assigned sequential number; W4 — a listed service must be effective **on** the start date (W allowed a later start), and withdrawing a listed service is **refused** while a published version that lists it applies (W allowed it); W5 — cancelled versions never apply. W1's "at least one service" was never ruled and became OQ60, ruled at approval the other way: a version may list no services (§0.10).

### 0.10 Rulings (hein, 2026-09-26)

The stage-2 ⛔ asked hein for the rulings below (the request table is kept as asked, under "Asked"). hein ruled on all of them on 2026-09-26 (T-053; recorded in the `TASKS.md` T-053 row at `417c400`).

**The business rulings (OQ51–OQ59, and H) are provisional tech-lead rulings.** For each one: **Resolved by tech-lead ruling (hein, 2026-09-26; T-053) — not a Myanma Railways answer.** This project chose not to wait for one. **Still open with Myanma Railways.** If Myanma Railways later gives an official, different answer, that supersedes this ruling and needs its own follow-up task. The same wording is in `docs/19-open-questions.md` under each OQ and, for OQ59, in `docs/10-authorization-matrix.md` §Schedule permission grants.

**Asked (stage 2, `4ea9ed2`):**

At stage 2, each business item was an OPEN QUESTION until ruled; the options were non-binding prompts, not recommendations.

| ID | Question | Options (non-binding) | Blocks |
|---|---|---|---|
| **SC** | Scope boundary for F-005 (§0.8). | SC1 · SC2 · SC3 | All of §3–§9 |
| **OQ51** | What a version contains and its scope; its identity; its dates. | Scope: (a) whole network · (b) per route · (c) per service. Identity: (a) system number · (b) bilingual name · (c) code under the station format · combinations. Dates: (a) start only, runs until replaced · (b) start and end | R6–R9; §6; §7 |
| **OQ52** | Which times per stop; precision; dwell; ordering; passing times; per-weekday times. | Times: (a) arrival + departure at every stop · (b) departure at first, arrival at last, both between · (c) departure only. Precision: minutes · seconds. Passing times: none · optional · required. Per weekday: same every operating day · may differ | R10–R14 |
| **OQ53** | May a service run past midnight? | (a) no · (b) yes, written `24:15`-style, journey under 24 h · (c) yes, written `00:15` with a next-day marker | R15; ADR-0027 max value |
| **OQ54** | Versions vs service periods; withdrawn services; withdrawing a published version's service; C1. | Period: (a) effective throughout the version · (b) effective at some point · (c) independent. Withdrawal: (a) allowed, version unchanged · (b) refused · (c) requires a new version. Timetable change: (a) new version · (b) withdraw + recreate (F-004) · (c) both | R17–R20; C1; C2; E8 |
| **OQ55** | How versions follow one another. | (a) one in force per date, superseded by the next start date, no overlaps · (b) explicit periods, no overlap · (c) overlaps allowed with a precedence rule. Gaps: allowed · refused. Insertion: allowed · refused | R21–R25; E6 |
| **OQ56** | How drafts are prepared. | (a) created whole, immutable, discarded if wrong · (b) built up by adding/removing whole service entries · (c) freely edited (needs ADR-0024). Discarded drafts: kept · deleted. Several drafts: yes · no | R26–R29; E7; ADR-0024 |
| **OQ57** | Who publishes; second approver; lead time; past start date. | Approver: (a) any holder, including the author · (b) a different person · (c) a different role. Start: (a) ≥ today · (b) ≥ today + N days · (c) past allowed (as OQ50) | R30–R33; E5 |
| **OQ58** | Can a published version be cancelled or withdrawn? | (a) never; correct by publishing a later version · (b) cancel only before it takes effect · (c) withdraw from D, the previous version resumes · (d) withdraw from D, none in force | R34 (and out of F-005 under SC2) |
| **OQ59** | Role grants for `schedules.manage`, `schedules.read` (and `schedules.publish` if OQ57 splits it). | e.g. the `services.*` pattern (manage: `SystemAdministrator`, `RailwayAdministrator`; read: all eight) — a prompt, not a proposal of grants | R3–R5; §2 |
| **H** | Confirm F-005 proceeds with OQ47 (holidays, per-date exceptions) still open, as a known limitation. | Proceed · wait for OQ47 | R35; §9 |
| **ADR-0027** | Accept the time representation? | Accept · amend · reject (then `TimeOnly` + day offset) | R16; E3 |
| **ADR-0024** | Only if OQ56 → (c) (or (b) and hein wants a client-held version): accept, with the child-row note resolved as E7 describes. | Accept · keep Proposed | R27 |
| **E1–E14** | Accept, amend or reject each engineering proposal (§0.4). E6–E8 follow the OQ54–OQ56 rulings. | — | §3 engineering rules; §6–§8 |

**Rulings:**

| ID | Ruling | Label | Applied in |
|---|---|---|---|
| **SC** | **SC2, plus cancelling a published version before it takes effect** (from OQ58). Out of F-005: withdrawing a version that has already taken effect, a second approver, editable drafts, holidays and per-date exceptions, running past midnight. | ENGINEERING DECISION (tech lead, hein, 2026-09-26; scope) | R34; §5; §9 |
| **OQ51** | One version covers **the whole network**. Identity: a system-assigned **sequential number**, never reused, plus a `BilingualName` (the shared rules). A **start date only** (`DateOnly`); in force until the next published version's start date. | BUSINESS DECISION — provisional tech-lead ruling, not a Myanma Railways answer | R6–R8; §6; §7 |
| **OQ52** | First stop: departure only. Last stop: arrival only. Intermediate stops: both. Whole minutes. At each stop arrival ≤ departure. Each stop's arrival strictly later than the previous stop's departure. No passing times. The same times on every operating day of the service. | BUSINESS DECISION — provisional tech-lead ruling, not a Myanma Railways answer | R10–R14; SV6–SV13 |
| **OQ53** | **No running past midnight in Phase 1.** Every time is 00:00–23:59 on the operating date, and the whole journey falls within one operating date. A known limitation. | BUSINESS DECISION — provisional tech-lead ruling, not a Myanma Railways answer | R15, R16; SV11, SV12; §9 |
| **OQ54** | A service runs on a date only if the version in force on that date lists it (plus its own period and weekdays). Every listed service must be effective, and not withdrawn, on the version's start date. **Withdrawing a service from D is refused** (a stable `422 Timetable` code) while any published, not-cancelled version that lists it applies on any date ≥ D; a version applies from its start date to the day before the next published start date, or open-ended. Dropping a service = publish a version without it, then withdraw the service from that version's start date. Timetable changes use both mechanisms: services for stopping patterns, versions for times. **Closes C1 and C2.** Changes F-004's withdraw behaviour (§0.12). | BUSINESS DECISION — provisional tech-lead ruling, not a Myanma Railways answer | R17–R20; SV14–SV17, SV36–SV42; §0.12 |
| **OQ55** | Exactly one version in force per date: the published, not-cancelled version with the latest start date ≤ that date. Published start dates unique among published, not-cancelled versions (a filtered unique index). Before the first version, no service runs. A version may be published with a start date between two future ones. Every version is kept. | BUSINESS DECISION — provisional tech-lead ruling, not a Myanma Railways answer; index: ENGINEERING DECISION (E6) | R21–R25; SV24–SV30 |
| **OQ56** | A draft is **created whole** (all services, with all their times, in one request) and never edited. A wrong draft is **discarded** (status `Discarded`, row kept, never deleted). Several drafts may exist at once. ADR-0024 is not used and stays Proposed. Lifecycle: Draft → Published → (Cancelled), or Draft → Discarded. | BUSINESS DECISION — provisional tech-lead ruling, not a Myanma Railways answer; ADR-0024: ENGINEERING DECISION (tech lead, hein, 2026-09-26) | R26–R29; §5; SV35 |
| **OQ57** | Any holder of `schedules.manage` may publish, including the draft's author. No second approver in Phase 1. The start date is set when the draft is created and must be ≥ today (Asia/Yangon) at publish time. | BUSINESS DECISION — provisional tech-lead ruling, not a Myanma Railways answer | R30, R31; SV18–SV21 |
| **OQ58** | A published version may be **cancelled only while its start date is later than today**; after that it is corrected by publishing a later version. Cancelled versions are kept and never apply. Cancelling is a status change (Published → Cancelled). | BUSINESS DECISION — provisional tech-lead ruling, not a Myanma Railways answer | R34; SV31–SV34 |
| **OQ59** | `schedules.manage` (create, discard, publish, cancel) → `SystemAdministrator`, `RailwayAdministrator`. `schedules.read` → all eight roles. No `schedules.publish` permission. | BUSINESS DECISION — provisional tech-lead ruling, not a Myanma Railways answer | R3–R5; §2; `docs/10` §Schedule permission grants; SV45, SV46 |
| **H** | Proceed. OQ47 (holidays, per-date exceptions) stays open as a known limitation. | ENGINEERING DECISION (tech lead, hein, 2026-09-26; scope); OQ47 stays OPEN with Myanma Railways | R35; §9 |
| **ADR-0027** | **Accepted** (hein, 2026-09-26), with a maximum of **1439 minutes** under the OQ53 ruling. Status and README row set to Accepted. | ENGINEERING DECISION (tech lead, hein, 2026-09-26) | R16; E3; §7 |
| **E1–E7, E9–E14** | Accepted. E6 and E7 take their OQ55 and OQ56 branches: a filtered unique index for start dates; `Status` as the concurrency token; no `rowversion`; no client-held version. | ENGINEERING DECISION (tech lead, hein, 2026-09-26) | R25, R38–R44, R47; §6–§8 |
| **E8** | **Not accepted.** The race between publishing and withdrawing a service is **not** accepted. Publish, cancel and service withdrawal are **serialised by one Timetable-wide application lock** under ADR-0026 (the smallest key covering the OQ54/OQ55 rule across versions and services); the mechanism is detailed at PLAN. Scenarios for publish vs withdraw and cancel vs withdraw, in both orders. | ENGINEERING DECISION (tech lead, hein, 2026-09-26; ADR-0026) | R46; §5; SV40, SV41 |
| **E10** | Body limits and array caps are sized at PLAN from a realistic whole-network version (assume up to about **200 services × about 40 stops**; the plan justifies the numbers). The spec states the rule and leaves the numbers to PLAN. | REQUIRED CONTROL (tech lead, hein, 2026-09-26) | R41; SV47 |
| **C4, C5** | Listed for stage 8, not fixed now. ADR-0024 may be corrected in a Proposed-stage note if needed (not needed by this revision). | ENGINEERING DECISION (tech lead, hein, 2026-09-26) | Notes for the next stage |

**Final rulings at approval (hein, 2026-09-26 08:28; T-053 row at `97b5d7d`):**

| ID | Ruling | Label | Applied in |
|---|---|---|---|
| **OQ60** | **(b) A version may list no services.** Publishing one means no service runs from its start date until the next published version — a network-wide suspension. It is corrected like any other version: cancelled before it takes effect (R34), or superseded by a later version. | BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-26; T-053), not a Myanma Railways answer | R9; SV22, SV53; `docs/19` OQ60 |
| **Q2** | **(a) Accepted as ruled.** A service withdrawn from D stays withdrawn if the version starting on D is later cancelled. The earlier version applies again from D and reports the service as not running from D (`runsOnDate = false`). The cancel is not refused. The same holds for the analogous publish (a version whose range includes dates after a listed service's end) — already the §0.10 consequence "a listed service may end mid-version". | BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-26; T-053), not a Myanma Railways answer | R49; SV41, SV54 |
| **Number assignment** | Confirmed: `1`, then the highest number ever assigned + 1, under the Timetable-wide lock — contiguous, no gaps, never reused. | ENGINEERING DECISION (tech lead, hein, 2026-09-26) | R7; SV4, SV42 |
| **Start date at creation** | Confirmed: a create whose `EffectiveFrom` is before today is refused (`422 Timetable.ScheduleVersionEffectiveFromInPast`). | ENGINEERING DECISION (tech lead, hein, 2026-09-26) | R31; SV19 |
| **Re-check at publish** | Confirmed: every listed service is checked again at publication (R17). | ENGINEERING DECISION (tech lead, hein, 2026-09-26) | R17; SV17, SV40 |
| **Lock for create and discard** | Confirmed: create and discard also take the Timetable-wide lock, with publish, cancel and service withdrawal. | ENGINEERING DECISION (tech lead, hein, 2026-09-26; ADR-0026) | R46; §5; SV42 |
| **`runsOnDate`** | Confirmed: the in-force read reports, for each listed service, whether it runs on the requested date. | ENGINEERING DECISION (tech lead, hein, 2026-09-26) | R48; SV30, SV53, SV54 |

**Approval:** with these rulings recorded and applied, hein approved the spec on 2026-09-26. No blocking OPEN QUESTION remains. OQ47 (holidays, per-date exceptions) stays open with Myanma Railways as a known limitation (ruling H).

**Amendment 1 after approval (hein, 2026-09-27; T-054 row, ruling 1):**

| ID | Ruling | Label | Applied in |
|---|---|---|---|
| **Amendment 1** | **A version that lists no services may only be published with a start date later than today** (Asia/Yangon, R33). Publishing an empty version whose `EffectiveFrom` is today → `422 Timetable.EmptyScheduleVersionNotInFuture`, nothing written; the version stays `Draft`. So a network-wide suspension (R9) can always be cancelled before it takes effect (R34). It closes the risk that OQ57 (publish on the start date) and OQ60 (an empty version) together let one request stop every service today with no chance to cancel. Creating an empty draft is unchanged (R9, SV22). A non-empty version is unchanged. | BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-27; T-054), not a Myanma Railways answer. The code: ENGINEERING DECISION (E1) | R50; SV55, SV56; §5; §6; §9; `docs/19` OQ60 |

The approved text above is kept; R9 points to R50, and §5, §6, §9 and Blocked behaviour carry the change.

**Amendment 2 after approval (hein, 2026-09-27; T-054 row, plan ruling Q3):**

| ID | Ruling | Label | Applied in |
|---|---|---|---|
| **Amendment 2** | **An empty draft whose start date is not later than today is refused at creation too**, with the same `422 Timetable.EmptyScheduleVersionNotInFuture`; nothing is written. Amendment 1's "Creating an empty draft is unchanged" no longer holds for a start date of today: such a draft could never be published (Amendment 1, and dates only move forward), so it is refused when it is created, as R31 refuses a past start date at creation. An empty draft with a start date later than today is created as before (R9, SV22); the publication check stays, for a draft that was valid when created and whose start date has since become today. A non-empty version is unchanged. | BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-27; T-054 Q3), not a Myanma Railways answer. The code: ENGINEERING DECISION (E1) | R45; R50; SV55; §5; §6; §9; `docs/19` OQ60 |

The Amendment 1 row above is kept as ruled; R50, SV55, §5, §6, §9 and Blocked behaviour carry Amendment 2.

**Amendment 3 after approval (hein, 2026-09-27; T-055 row, rulings before stage 4):**

| ID | Ruling | Label | Applied in |
|---|---|---|---|
| **Amendment 3** | Three alignments with the Approved plan (revision 2, `5901d53`); **no business rule changes**. (1) The list endpoint `GET /schedules/versions` refuses an unknown `status` filter value with `400 Common.ValidationFailed` (plan Q2). (2) R41 and SV47 add the **per-version total of 10,000 stop times** → `400 Common.ValidationFailed`, next to the caps of 250 services per version and 200 stop times per service and the 2 MiB body limit on `POST /schedules/versions` (plan Q1, P15). (3) SV19 and SV21 read "a version listing a service effective on its start date" (not empty), matching the plan's test rows: an empty version starting today is refused at creation and at publication (R50, SV55). | ENGINEERING DECISION (tech lead, hein, 2026-09-27; T-055); the caps are REQUIRED CONTROLs (R41) | §6; R41; SV19; SV21; SV47 |

**Consequences of the rulings, recorded so they are seen (not conflicts):**
- **A service can run only inside a version.** Because a listed service must be effective on the version's start date (OQ54), a service whose `EffectiveFrom` is later than the start of the version in force cannot run until a version starting on or after its `EffectiveFrom` lists it. A new service is therefore introduced by creating it, then publishing a version from its first day.
- **A listed service may end mid-version.** The start-date check (OQ54) is the only period check at publication, so a service created with an `EffectiveTo` inside the version's range may be listed; it stops running after its `EffectiveTo` (R18). Only a *withdrawal* is refused while a version that lists the service still applies (R19).
- **Publishing or cancelling changes which dates earlier versions apply to.** Inserting a version shortens the range of the version before it; cancelling one lengthens it again. Both are serialised with withdrawal by the one lock (R46). The one case this left open, Q2 (§0.11), is ruled at approval: R49.
- **An empty version (OQ60) is in force like any other.** On its dates the in-force read returns it (`200`, no services), not `404 Timetable.ScheduleVersionNotInForce`, which means only "before the first published version" (R21). It lists no service, so it never refuses a withdrawal (R19).
- **The start-date rule at creation.** OQ57 requires start ≥ today at publish time. Because dates only move forward, a draft created with a start date before today could never be published, so it is refused at creation as well (R31, ENGINEERING DECISION, input consistency; the F-004 R19 pattern).

### 0.11 Raised while applying the rulings — for hein

| ID | Question | Why the rulings do not settle it | Options (non-binding) | Blocks |
|---|---|---|---|---|
| **OQ60** (new in `docs/19`) | May a timetable version list **no services**? | OQ51 makes a version network-wide and OQ55 says a date is covered by exactly one version, but neither says whether a version may be empty. An empty version would mean "no service runs from this date" — for example a network-wide suspension. That is a business rule, so it is asked, not assumed. | (a) refused: at least one service (`400 Common.ValidationFailed`) · (b) allowed | R9; SV22 |
| **Q2** | Withdraw S from D, then cancel the version whose start date D made that withdrawal legal. | Under the rulings as written, both succeed: the withdrawal is allowed because the next version (from D) does not list S, and the cancellation is allowed because D is later than today. The earlier version then applies again from D and lists S, which no longer runs from D. Nothing is corrupted — S simply does not run (R18), exactly as for a listed service that ends mid-version — but the withdrawal guard (R19) would have refused that withdrawal in the post-cancel state. The rulings do not say whether cancellation must also be refused here. | (a) accept: cancel succeeds, S does not run from D (the letter of the rulings; the spec applies this) · (b) refuse the cancel (a new `422` code) while a withdrawn service would be listed by a version that then applies after its end. The same question applies to publishing a version that would then apply after a listed service's withdrawal date, which OQ54's start-date rule also allows | R34; SV41 (second order) |

**Outcome (hein, 2026-09-26):** **OQ60 → (b)**, a version may list no services (R9). **Q2 → (a)**, accepted as ruled (R49). Nothing here blocks any more; the table above is kept as asked.

### 0.12 The change to F-004's withdrawal (OQ54)

F-004 (Approved, merged at `63bea71`) lets `POST /services/{id}/withdraw` shorten any service from a date D ≥ today (F-004 R21). The OQ54 ruling **adds a refusal**, and E8's rejection adds a lock. Stated completely:

- **New rule (R19).** A withdrawal of service S from D is refused with **`422 Timetable.ServiceInPublishedScheduleVersion`**, and nothing is written, if any **published, not-cancelled** version that lists S **applies on some date ≥ D**. A version V applies from `V.EffectiveFrom` to the day before the `EffectiveFrom` of the next published, not-cancelled version (by start date), or open-ended if there is none. So the refusal fires exactly when V lists S and either V has no successor or its successor starts after D. Drafts, discarded versions and cancelled versions never cause it.
- **Check order.** Unchanged up to and including F-004's checks: request validation (`400`); unknown service (`404 Timetable.ServiceNotFound`); then, under the locks, `422 Timetable.WithdrawalDateInPast`, `422 Timetable.WithdrawalDoesNotShorten`; **then the new check**, last. `409 Timetable.ServiceChangedConcurrently` stays the token backstop.
- **Lock.** `WithdrawServiceHandler` keeps its `timetable.ServiceCode:<code>` lock (F-004 R35) and **also** takes the Timetable-wide lock (R46), before reading versions. It is the only handler that takes both; the order (code lock first) and whether one lock can replace the other are PLAN decisions.
- **What does not change.** Creating a service; the four F-004 endpoints' paths, bodies and other codes; the `timetable.Services` / `ServiceStops` columns, constraints and grants; a withdrawal never causes `409 Timetable.ServiceCodePeriodOverlap`. Every existing F-004 test stays valid: none of them publishes a version.
- **Scenarios:** SV36–SV42 and SV54 (§4).
- **Stage 4 (code and tests, planned at stage 3):** `WithdrawServiceHandler` (lock + check), `TimetableErrors.ServiceInPublishedScheduleVersion`, the withdraw endpoint's OpenAPI `422` metadata, `YCR.Api.http`; new handler integration tests (refused while a version applies; allowed after the dropping version; drafts and cancelled versions ignored), an API test for the `422`, the forced-order race tests SV40 and SV41, and a `DatabasePrivilegeTests` case that `ycr_app` can take the new lock.
- **Stage 8 (F-004 documents):** `docs/08` §Implemented in F-004 (the withdraw row's error codes and the withdrawal check order); `docs/07` §F-004 §The service-code lock (withdrawal also takes the Timetable-wide lock); `docs/20` §6 (the list of ADR-0026 uses); the glossary `Withdrawal` entry; a dated amendment note in the F-004 spec at R21 pointing to F-005 R19 (the Approved text itself is not rewritten); `docs/19` OQ48 already points here through the OQ54 resolution.

---

## 1. Goal

Railway administrators need to publish timetable versions — the stop times of every service on the network, with the date from which they apply — so that exactly one authoritative timetable is in force on any date, a published version never changes, a future version can still be cancelled, and later features (ticketing, reporting) can tell which timetable applied on a past date.

---

## 2. Actors and permissions

| Actor | Permission | Held by |
|---|---|---|
| Staff user creating, discarding, publishing and cancelling versions | `schedules.manage` | `SystemAdministrator`, `RailwayAdministrator` — provisional tech-lead ruling (hein, 2026-09-26; T-053, OQ59), not a Myanma Railways answer |
| Staff user reading versions, times and the version in force | `schedules.read` | All eight roles: `SystemAdministrator`, `RailwayAdministrator`, `StationManager`, `TicketOperator`, `TicketInspector`, `FinanceOfficer`, `Auditor`, `ReportingUser` — same ruling |

There is no `schedules.publish`: publishing is part of `schedules.manage`, and the draft's author may publish it (OQ57, OQ59). Holding a station, route or service permission gives no schedule right, and holding a schedule permission gives no station, route or service right. Grants are data seeded by a reviewed migration; no API edits them. Withdrawing a service still needs `services.manage` (F-004).

---

## 3. Business rules

Provisional rulings are labelled "BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-26; T-053, OQnn), not a Myanma Railways answer", shortened below to **PROVISIONAL RULING (OQnn)**. Each is still open with Myanma Railways; an official, different answer supersedes the ruling and needs its own follow-up task.

"Today" is today's Asia/Yangon date (R33). A version is **published** when its status is `Published` (not `Cancelled`, `Discarded` or `Draft`). The **successor** of a published version V is the published version with the smallest `EffectiveFrom` greater than V's. V **applies** from its `EffectiveFrom` to the day before its successor's `EffectiveFrom`, or open-ended if it has none.

| # | Rule | Label | Source |
|---|---|---|---|
| R1 | Authorized users can publish timetable versions with effective dates. | FACT | `docs/01` §FR-004 |
| R2 | A timetable version is the `Timetable` aggregate `ScheduleVersion`; Ticketing never navigates to it. | FACT (`docs/04`, `docs/05`); ENGINEERING DECISION (ADR-0013; E1 accepted) | `docs/04` §Boundary clarifications |
| R3 | Creating, discarding, publishing and cancelling versions requires `schedules.manage`, held by `SystemAdministrator` and `RailwayAdministrator`. | Name: FACT (`docs/10`). Grants: **PROVISIONAL RULING (OQ59)** | `docs/10` §Schedule permission grants |
| R4 | Any holder of `schedules.manage` may publish, including the draft's author. There is no `schedules.publish` permission and no second approver in Phase 1. | **PROVISIONAL RULING (OQ57, OQ59)** | `docs/19` OQ57, OQ59 |
| R5 | Reading versions, their times and the version in force requires `schedules.read`, held by all eight roles. Station, route and service permissions give no schedule right, and schedule permissions give no other right. | Name: ENGINEERING DECISION (tech lead, hein, 2026-09-26; E5). Grants: **PROVISIONAL RULING (OQ59)** | `docs/10` §Schedule permission grants |
| R6 | A version covers **the whole network**: it lists the services that run while it is in force, from any route. | **PROVISIONAL RULING (OQ51)** | `docs/19` OQ51 |
| R7 | A version is identified by a system-assigned sequential **number** and a `BilingualName` (`NameEn` and `NameMy` required, each 1–100 characters after trimming, `400 Timetable.InvalidScheduleVersionName`; not unique). The number is assigned at creation — `1` for the first version, then one more than the highest number ever assigned, under the Timetable-wide lock (R46) — so numbers are contiguous in creation order, unique (`UX_ScheduleVersions_Number`), and never reused: a discarded or cancelled version keeps its number. | Number, name: **PROVISIONAL RULING (OQ51)**; name rules: F-001 R4 (OQ27 ruling). Assignment mechanism: ENGINEERING DECISION (tech lead, hein, 2026-09-26) | `docs/19` OQ51 |
| R8 | A version has a start date only, `EffectiveFrom` (`DateOnly` / `date`), set at creation and never changed. It is in force until the next published version's start date. | **PROVISIONAL RULING (OQ51, OQ55)**; type: ENGINEERING DECISION (ADR-0018; E4) | `docs/19` OQ51 |
| R9 | A version may list **no services** (an empty `services` array; the array itself is still required, SV18). Publishing an empty version means no service runs from its start date until the next published version's start date — a network-wide suspension (R18). It is corrected like any other version: cancelled while its start date is later than today (R34), or superseded by a later version (R23). An empty version is in force on its dates like any other (R21). *Amendments 1–2 (hein, 2026-09-27): an empty version may be created and published only with a start date later than today — R50.* | **PROVISIONAL RULING (OQ60)** | `docs/19` OQ60 |
| R10 | Stop 1 has a **departure** only; the last stop (including a full circuit's closing stop, F-004 R14) an **arrival** only; every other stop both. A time given where none is allowed → `422 Timetable.ScheduleStopTimeUnexpected`; a required time, or a stop position, missing or repeated → `422 Timetable.ScheduleStopTimesIncomplete`; a position greater than the service's stop count → `422 Timetable.ScheduleStopNotInService`. | **PROVISIONAL RULING (OQ52)**; codes: ENGINEERING DECISION (E1) | `docs/19` OQ52 |
| R11 | Times are whole minutes. | **PROVISIONAL RULING (OQ52)** | `docs/19` OQ52; ADR-0027 |
| R12 | At each stop, arrival ≤ departure (a dwell of 0 is allowed); otherwise `422 Timetable.ScheduleDwellNegative`. Each stop's arrival is **strictly later** than the previous stop's departure; otherwise `422 Timetable.ScheduleTimesNotIncreasing`. | **PROVISIONAL RULING (OQ52)**; codes: ENGINEERING DECISION (E1) | `docs/19` OQ52 |
| R13 | No passing times: stations a service passes without stopping have no time. | **PROVISIONAL RULING (OQ52)** | `docs/19` OQ52; F-004 R13 |
| R14 | A service listed in a version has one set of times in that version, the same on every one of its operating days. | **PROVISIONAL RULING (OQ52)** | `docs/19` OQ52; F-004 R17 |
| R15 | **No running past midnight in Phase 1.** Every time is 00:00–23:59 on the operating date (the date the service starts, OQ47), and the whole journey falls within that date: with R12, a later stop can never have an earlier clock time. | **PROVISIONAL RULING (OQ53; OQ47)** | `docs/19` OQ47, OQ53 |
| R16 | A time is a whole number of minutes after local midnight of the operating date, `0`–`1439`, stored as `smallint` and exchanged as `HH:mm` (`00`–`23`, `00`–`59`, exactly two digits each); anything else → `400 Timetable.InvalidTimetableTime`. | ENGINEERING DECISION (ADR-0027, Accepted 2026-09-26) | ADR-0027 |
| R17 | Every service a version lists must exist (`422 Timetable.ScheduleServiceNotFound`), be listed once (`422 Timetable.ScheduleServiceRepeated`), and be **effective and not withdrawn on the version's start date** — `EffectiveFrom` ≤ start ≤ `EffectiveTo` (or open-ended) — otherwise `422 Timetable.ScheduleServiceNotEffective`. A service that never runs (F-004 R41) fails this check. Checked at creation and again at publication. | Rule: **PROVISIONAL RULING (OQ54)**. Checking at both points: ENGINEERING DECISION (tech lead, hein, 2026-09-26) | `docs/19` OQ54 |
| R18 | A service **runs on date *d*** only if the version in force on *d* (R21) lists it, *d* is within the service's own effective period, and *d*'s weekday is one of its operating days. Before the first published version, no service runs. | **PROVISIONAL RULING (OQ54, OQ55)** | `docs/19` OQ54, OQ55 |
| R19 | **Withdrawal guard (changes F-004).** Withdrawing service S from D is refused — `422 Timetable.ServiceInPublishedScheduleVersion`, nothing written — while any published version that lists S applies on any date ≥ D. Drafts, discarded and cancelled versions never refuse a withdrawal. §0.12 states the whole change. | Rule: **PROVISIONAL RULING (OQ54)**; the code: ENGINEERING DECISION (E1) | `docs/19` OQ54; F-004 R21 |
| R20 | Timetable changes use both mechanisms: a change of stopping pattern is a new service (F-004: withdraw + create); a change of times is a new version. Dropping a service is: publish a version without it, then withdraw the service from that version's start date. | **PROVISIONAL RULING (OQ54)**; closes C1, C2 | `docs/19` OQ48, OQ54 |
| R21 | Exactly one version is **in force** on a date *d* on or after the first published start date: the published version with the latest `EffectiveFrom` ≤ *d*. On earlier dates none is. | **PROVISIONAL RULING (OQ55)** | `docs/19` OQ55 |
| R22 | Start dates are unique among published versions: publishing a version whose `EffectiveFrom` equals a published version's → `409 Timetable.ScheduleVersionEffectiveFromTaken`. Drafts, discarded and cancelled versions may share a start date with anything. Enforced by the filtered unique index `UX_ScheduleVersions_EffectiveFrom_Published`. | Rule: **PROVISIONAL RULING (OQ55)**; index: ENGINEERING DECISION (E6 accepted) | `docs/19` OQ55 |
| R23 | Supersession is automatic: publishing a version ends the range of the version before it. There is no gap between the first published version and any later date. | **PROVISIONAL RULING (OQ55)** | `docs/19` OQ55 |
| R24 | A version may be published with a start date between two published future versions (insertion). | **PROVISIONAL RULING (OQ55)** | `docs/19` OQ55 |
| R25 | Every version is kept and stays readable, whatever its status; no version row is deleted, ids and numbers never change, and there is no `DELETE` endpoint or grant. | **PROVISIONAL RULING (OQ55, OQ56)**; FACT (`docs/07` §Rules); ENGINEERING DECISION (E9 accepted) | `docs/07` §Rules; AGENTS.md rule 5 |
| R26 | A version is created, then published; a published version is never patched. | FACT | `docs/20` §4; `docs/08` §Initial resources |
| R27 | A draft is created whole — every service it lists, with all their times, in one request — and never edited. No endpoint adds, removes or changes a draft's services or times. ADR-0024 is not used. | **PROVISIONAL RULING (OQ56)**; ADR-0024 unused: ENGINEERING DECISION (tech lead, hein, 2026-09-26) | `docs/19` OQ56 |
| R28 | A draft can be **discarded** (Draft → Discarded): the row is kept, never deleted, and never applies. Discarding anything but a draft → `422 Timetable.ScheduleVersionNotDraft`. | **PROVISIONAL RULING (OQ56)** | `docs/19` OQ56 |
| R29 | Several drafts may exist at once, with any start dates. | **PROVISIONAL RULING (OQ56)** | `docs/19` OQ56 |
| R30 | Publishing (Draft → Published) is allowed only from Draft (`422 Timetable.ScheduleVersionNotDraft`). | **PROVISIONAL RULING (OQ56, OQ57)** | `docs/19` OQ57 |
| R31 | `EffectiveFrom` must be ≥ today **at publication**; otherwise `422 Timetable.ScheduleVersionEffectiveFromInPast` and nothing is written. A draft whose start date has passed therefore can never be published; it stays a draft until discarded. The same check refuses a create whose `EffectiveFrom` is before today. | Publication: **PROVISIONAL RULING (OQ57)**. At creation: ENGINEERING DECISION (tech lead, hein, 2026-09-26; input consistency, §0.10 consequences) | `docs/19` OQ57 |
| R32 | A published version is immutable: no field, entry or time changes; only its status may move to `Cancelled` (R34). | FACT; **PROVISIONAL RULING (OQ58)** for the status change | `docs/08` line 35; `docs/20` §4 |
| R33 | "Today" is `ILocalCalendar.Today()` (the configured Asia/Yangon zone), never server local time or a hard-coded offset. | ENGINEERING DECISION | ADR-0018; `docs/20` §8 |
| R34 | A published version may be **cancelled** (Published → Cancelled) only while its `EffectiveFrom` is **later than today**; on or after its start date → `422 Timetable.ScheduleVersionAlreadyEffective`. Cancelling anything but a published version → `422 Timetable.ScheduleVersionNotPublished`. A cancelled version is kept and never applies. A version that has taken effect is corrected only by publishing a later version; withdrawing it is not provided. | **PROVISIONAL RULING (OQ58)**; scope: SC ruling | `docs/19` OQ58 |
| R35 | No public-holiday calendar and no per-date exception: services run on their weekdays throughout, holidays included. | **OPEN QUESTION (OQ47, still open with Myanma Railways)** — not implemented, no placeholder; proceeding accepted (ruling H) | `docs/19` OQ47 |
| R36 | OQ4 and OQ19 do not block F-005. R25, R32 and R21 keep a departure-bound ticket (if OQ4 so rules) reconstructible for any past date from rows alone. | FACT (analysis, G10) | `docs/19` OQ4, OQ19 |
| R37 | No real timetable data ships; tests build their own stations, routes, services and versions. OQ1 blocks data, not the API. | FACT (consequence of OQ1, open) | `docs/19` OQ1; F-004 R27 |
| R38 | Identifiers are application-generated GUIDs through `IIdGenerator`, `ValueGeneratedNever()`. | ENGINEERING DECISION | ADR-0006 |
| R39 | Every create, publish, discard and cancel is audited in the same `SaveChangesAsync`, with actor fields from the authenticated server-side context only. A refused request writes no event. | ENGINEERING DECISION | ADR-0017 §1–2; ADR-0021 |
| R40 | No `Idempotency-Key`. | ENGINEERING DECISION (E11 accepted) | `docs/20` §5 |
| R41 | Every body-carrying endpoint has an explicit body limit, refused with `413` before binding; every array has a cap (`400 Common.ValidationFailed`): stop times per service ≤ 200 (F-004's stop cap) and services per version ≤ a cap. The values are set at PLAN from a realistic whole-network version of up to about 200 services × about 40 stops, with the reasoning in the plan. *Amendment 3 (T-055; plan Q1): the plan sets a 2 MiB body limit on `POST /schedules/versions`, at most 250 services per version, 200 stop times per service, and a per-version total of 10,000 stop times; each cap over → `400 Common.ValidationFailed`.* | REQUIRED CONTROL (tech lead, hein, 2026-09-26; E10) | F-003 R27; F-004 R31 |
| R42 | Error codes are `Timetable.<Reason>`, mapped per ADR-0004. | ENGINEERING DECISION (E1 accepted) | ADR-0004 |
| R43 | Timetable reads services through `ITimetableDbContext`, and station display data only through `INetworkReader`. No Timetable contract is added in F-005. | ENGINEERING DECISION (E12 accepted; ADR-0012; ADR-0025) | ADR-0025 |
| R44 | No public or printed timetable. | FACT (no source requires one) | `docs/00`; `docs/03` |
| R45 | When a create breaks several rules, the checks run in a fixed order and the first failure is returned: request validation (`400`), name, time format, then `EffectiveFrom` ≥ today, then an empty version's `EffectiveFrom` > today (R50, Amendment 2), then per service in request order: exists, repeated, effective on the start date, stop positions, times per stop, dwell, increasing. The exact order is fixed at PLAN (F-004 R37 pattern). | ENGINEERING DECISION (proposed) | F-004 R37 |
| R46 | **One Timetable-wide application lock** (ADR-0026) serialises every operation that decides or changes which versions apply or which services they cover: publish, cancel and service withdrawal, and also version creation (number assignment, R7) and discard. It is exclusive and transaction-owned, taken before the reads that decide, with a 30 s timeout (a timeout is the opaque `500`). Its resource name (for example `timetable.Schedule`), the order in which `WithdrawService` takes it with the service-code lock, and its proof are PLAN decisions. Service creation does not take it. | ENGINEERING DECISION (tech lead, hein, 2026-09-26; E8 not accepted → lock; create and discard confirmed at approval; ADR-0026 items 1–8) | ADR-0026 |
| R47 | `ScheduleVersions.Status` is the EF concurrency token, a backstop behind R46 for a writer that bypasses the lock: if it fires, `409 Timetable.ScheduleVersionChangedConcurrently` and nothing is written. No `rowversion`, no client-held version. | ENGINEERING DECISION (E7 accepted; ADR-0026 item 7; F-003/F-004 pattern) | ADR-0026 |
| R48 | The version-in-force read reports, for each listed service, whether it **runs on the requested date** (R18: within its period and on an operating day). | ENGINEERING DECISION (tech lead, hein, 2026-09-26; makes R18 observable and tested) | R18 |
| R49 | **A withdrawal stays in force when a later cancellation revives an earlier version.** If service S was withdrawn from D (allowed by R19 because the version starting on D does not list S) and that version is then cancelled (R34), the cancel succeeds and S stays withdrawn: the earlier version applies again from D, still lists S, and reports S as not running from D (R18, R48). Nothing refuses the cancel. Likewise a version may be published whose range includes dates after a listed service's end (§0.10 consequences). | **PROVISIONAL RULING (Q2; OQ54, OQ58)** | §0.10 Q2; `docs/19` OQ54, OQ58 |
| R50 | **Amendments 1–2 — an empty version starts after today.** A version that lists no services (R9) may be created and published only while its `EffectiveFrom` is **later than today**; otherwise `422 Timetable.EmptyScheduleVersionNotInFuture` and nothing is written. **At creation** (Amendment 2): checked after R31 (a start date before today is still `422 Timetable.ScheduleVersionEffectiveFromInPast`) and before any per-service check (R45); no version row is created. **At publication** (Amendment 1): checked after R30 and R31; the version stays `Draft`. The publication check still matters for a draft created with a later start date that has since become today. Once published, an empty version can therefore always be cancelled before it takes effect (R34). | **PROVISIONAL RULING (Amendment 1 at publication, Amendment 2 at creation; hein, 2026-09-27; T-054; OQ60, OQ57)**; the code: ENGINEERING DECISION (E1) | §0.10 Amendments 1–2; `docs/19` OQ60 |

**Blocking open questions:** none. OQ51–OQ60 are resolved for F-005 by provisional tech-lead rulings and remain open with Myanma Railways. OQ47's open part (R35) does not block (ruling H) and stays a known limitation. OQ1, OQ4, OQ17 and OQ19 stay open and do not block.

---

## 4. Scenarios (Given / When / Then)

Status codes follow ADR-0004; every error is ProblemDetails with `errorCode` and `traceId`. Unless a scenario says otherwise: today (Asia/Yangon, from a test clock) is **2026-10-01**, a Thursday; route **RC** is closed, `[A, B, C, D, E]`; services **S1** (`[A, C, E]`, `Forward`, Monday–Friday, from 2026-10-05, open-ended) and **S2** (`[E, C, A]`, `Reverse`, every day, from 2026-10-05, open-ended) exist; the caller holds `schedules.manage` and `schedules.read` (and `services.manage` where it withdraws). A **valid create** has `nameEn`, `nameMy`, `effectiveFrom = 2026-10-05` and S1 with `A dep 06:00`, `C arr 06:20 dep 06:22`, `E arr 06:40`. "Nothing written" means no version row, no child row, no service change and no audit event. Versions named V*n* below are created and published by the scenario's setup through the API, moving the test clock where a past start date is needed.

### Create, read, numbers

- **SV1.** POST `/schedules/versions` with a valid create → `201 Created`, `Location: /api/v1/schedules/versions/{id}`, `{ id, number }`; one `ScheduleVersions` row (`Number = 1`, `Status = Draft`, all transition instants null), one `ScheduleVersionServices` row, three `ScheduleStopTimes` rows (`DepartureMinute 360`; `370`/`372`… as given); one `Timetable.ScheduleVersionCreated` event.
- **SV2.** A caller holding only `schedules.read` GETs `/schedules/versions/{id}` → `200` with number, names, `effectiveFrom`, status, instants and each listed service's id, current code, names, direction and stop count; GETs `/schedules/versions/{id}/services/{S1}` → `200` with the three stops in position order, each station's current code and names (through `INetworkReader`), `arrival`/`departure` as `HH:mm` or `null`. A service not in the version → `404 Timetable.ScheduleServiceNotInVersion`. No EF entity is serialised.
- **SV3.** GET `/schedules/versions?page=1&pageSize=50` → `200` with `items, page, pageSize, totalCount`, ordered by `number`; `&status=Published` filters.
- **SV4.** Numbers: a first create gets `number = 1`, a second `2`; discard 2; a third create gets `3` (2 is never reused); a cancelled version keeps its number.
- **SV5.** A Myanmar version name round-trips unchanged (real Myanmar Unicode text).

### Times (OQ52, OQ53; R10–R16)

- **SV6.** An arrival at stop 1, or a departure at the last stop → `422 Timetable.ScheduleStopTimeUnexpected`; nothing written.
- **SV7.** A missing departure at C, a missing arrival at E, a missing departure at A, position 2 omitted, or position 2 given twice → `422 Timetable.ScheduleStopTimesIncomplete`; nothing written.
- **SV8.** A stop not in the service: position 4 for three-stop S1 → `422 Timetable.ScheduleStopNotInService`; position 0 or negative → `400 Common.ValidationFailed`. Nothing written.
- **SV9.** At C `arr 06:22 dep 06:20` → `422 Timetable.ScheduleDwellNegative`. `arr 06:20 dep 06:20` (dwell 0) → `201`.
- **SV10.** C `arr 06:00` after A `dep 06:00` (equal) → `422 Timetable.ScheduleTimesNotIncreasing`; C `arr 05:59` → same; E `arr 06:22` after C `dep 06:22` → same. Nothing written.
- **SV11.** Range and format: `"00:00"` at A and `"23:59"` at E (with increasing times between) → `201`. `"24:00"`, `"24:15"`, `"6:00"`, `"06:60"`, `"06:00:30"`, `"0600"`, `""` → `400 Timetable.InvalidTimetableTime`; nothing written.
- **SV12.** No running past midnight: A `dep 23:50`, C `arr 00:10 dep 00:12`, E `arr 00:30` → `422 Timetable.ScheduleTimesNotIncreasing` (the journey would cross midnight; R15). Nothing written.
- **SV13.** A full-circuit service S3 on RC, `Forward`, `[C, D, E, A, B, C]`: stop 1 departure only, stops 2–5 both, stop 6 (C again) arrival only, increasing → `201`.

### Services in a version (OQ54; R17)

- **SV14.** An unknown `serviceId` → `422 Timetable.ScheduleServiceNotFound`; S1 listed twice → `422 Timetable.ScheduleServiceRepeated`. Nothing written.
- **SV15.** Not effective on the start date (2026-10-05): a service from 2026-10-06 → `422 Timetable.ScheduleServiceNotEffective`; a service created with `effectiveTo = 2026-10-04`, or withdrawn from 2026-10-05 → same; a never-running service → same. Nothing written.
- **SV16.** Effective on the start date only just: a service with `effectiveTo = 2026-10-05` → `201` (it may end mid-version, §0.10 consequences); a service from 2026-09-01 (past `EffectiveFrom`, F-004 OQ50) → `201`.
- **SV17.** Re-checked at publication: a draft from 2026-11-01 lists S2; no published version lists S2; S2 is then withdrawn from 2026-11-01 (allowed, R19: only a draft lists it) → publishing the draft → `422 Timetable.ScheduleServiceNotEffective`; nothing written.

### Version fields and start date (OQ51, OQ57; R7, R9, R31)

- **SV18.** A blank or 101-character name after trimming → `400 Timetable.InvalidScheduleVersionName`; a missing or malformed `effectiveFrom`, a missing `services` array, a `null` or non-GUID `serviceId` → `400 Common.ValidationFailed`.
- **SV19.** Create with `effectiveFrom = 2026-09-30` (before today) → `422 Timetable.ScheduleVersionEffectiveFromInPast`; nothing written. A version listing a service effective on its start date, with `effectiveFrom = 2026-10-01` (today) → `201`. *(Amendment 3: "listing a service effective on its start date", not empty — an empty one starting today is SV55.)*
- **SV20.** Publish with a start date in the past: a draft for 2026-10-02 is created on 2026-10-01; the clock moves to 2026-10-03; publishing → `422 Timetable.ScheduleVersionEffectiveFromInPast`; nothing written; the draft stays `Draft`, stays readable, and discarding it → `204`.
- **SV21.** Publish on the start date: a draft for 2026-10-01 listing a service effective on its start date, published on 2026-10-01 → `204`. *(Amendment 3: not empty — an empty one is SV55.)*
- **SV22.** An empty `services` array (R9, OQ60) → `201`; one `ScheduleVersions` row (`Status = Draft`), no `ScheduleVersionServices` or `ScheduleStopTimes` rows; one `Timetable.ScheduleVersionCreated` event whose snapshot has `services: []`. A missing `services` array is still `400 Common.ValidationFailed` (SV18).

### Publish (OQ55, OQ57; R22, R30)

- **SV23.** Publish a draft → `204`; `Status = Published`, `PublishedAtUtc` = the clock's UTC now, in one `UPDATE`; child rows untouched; one `Timetable.ScheduleVersionPublished` event. The author of the draft may publish it (R4).
- **SV24.** Duplicate start date: V1 from 2026-10-05 is published; publishing another draft from 2026-10-05 → `409 Timetable.ScheduleVersionEffectiveFromTaken`; nothing written. Once V1 is cancelled, publishing that draft → `204` (R22).
- **SV25.** Publish a `Published`, `Cancelled` or `Discarded` version → `422 Timetable.ScheduleVersionNotDraft`; an unknown id → `404 Timetable.ScheduleVersionNotFound`.
- **SV26.** Two drafts from the same start date published in parallel → they serialise (R46): one `204`, one `409 Timetable.ScheduleVersionEffectiveFromTaken`; one event.

### Creating and publishing an empty version (Amendments 1–2; R50)

- **SV55.** *(Added by Amendment 1; rewritten by Amendment 2.)* **An empty version starting today is refused at creation, and at publication once its start date has become today.**
  - *At creation (Amendment 2):* creating a version listing no services with `effectiveFrom = 2026-10-01` (today) → `422 Timetable.EmptyScheduleVersionNotInFuture`; nothing written (no version row, no number assigned, no event). With `effectiveFrom = 2026-09-30` → `422 Timetable.ScheduleVersionEffectiveFromInPast` (R31 first). A non-empty create from 2026-10-01 (SV19) → `201`.
  - *At publication (Amendment 1):* a version E listing no services, `effectiveFrom = 2026-10-02` (tomorrow), is created on 2026-10-01 → `201`; the clock moves to 2026-10-02; publishing E → `422 Timetable.EmptyScheduleVersionNotInFuture`; nothing written (E stays `Draft`, `PublishedAtUtc` null, no event); `in-force?date=2026-10-02` is unchanged. A non-empty draft published on its start date is unchanged (SV21 → `204`).
- **SV56.** *(Added by Amendment 1.)* **An empty version starting tomorrow is accepted and can be cancelled.** A version E listing no services, `effectiveFrom = 2026-10-02` (tomorrow), is created and published on 2026-10-01 → `204`; one `Timetable.ScheduleVersionPublished` event. Cancelling E on 2026-10-01 → `204` (R34: its start date is later than today); `Status = Cancelled`; one `Timetable.ScheduleVersionCancelled` event.

### In force on date D (OQ55; R18, R21–R24, R48)

- **SV27.** Before the first version: no published version exists → GET `/schedules/versions/in-force?date=2026-10-05` → `404 Timetable.ScheduleVersionNotInForce`. V1 (from 2026-10-05) is published → `date=2026-10-04` → `404 Timetable.ScheduleVersionNotInForce` (no service runs, R18); `date=2026-10-05` → `200` V1.
- **SV28.** Across a supersession: V1 from 2026-10-05 and V2 from 2027-01-01 are published → `2026-12-31` → V1; `2027-01-01` → V2; `2030-01-01` → V2 (open-ended).
- **SV29.** Inserting between two future versions: with SV28's V1 and V2 (both start after today), publish V3 from 2026-11-01 → `204`; `2026-10-31` → V1; `2026-11-15` → V3; `2027-01-01` → V2. V1 stays readable, unchanged.
- **SV30.** Runs on date (R48): V1 lists S1 (Monday–Friday). `date=2026-10-10` (a Saturday) → `200` V1 with S1 `runsOnDate = false`; `2026-10-12` (Monday) → `true`. A service S5 created with `effectiveTo = 2026-10-31` and listed by V1: `2026-11-02` (Monday) → `false` for S5.
- **SV53.** *(Added at approval, OQ60; R9, R21.)* **Publishing an empty version, and the in-force read after it.** V1 from 2026-10-05 lists S1 and is published. A version E from 2026-11-02 listing no services is created (`201`) and published → `204`; one `Timetable.ScheduleVersionPublished` event. Then `in-force?date=2026-11-01` → `200` V1 with S1; `date=2026-11-02` (Monday) → `200` E with `services: []` — not `404`, because a version is in force and no service runs; `date=2027-06-01` → `200` E (open-ended). Cancelling E on 2026-10-01 → `204`, and `date=2026-11-02` → `200` V1 with S1 `runsOnDate = true`.

### Cancel (OQ58; R34)

- **SV31.** Cancel before the start date: V3 from 2026-11-01 (after SV29) → POST `/schedules/versions/{V3}/cancel` → `204`; `Status = Cancelled`, `CancelledAtUtc` set, `PublishedAtUtc` kept, rows kept; one `Timetable.ScheduleVersionCancelled` event.
- **SV32.** After a cancellation: `in-force?date=2026-11-15` → V1 again; V3 still reads back as `Cancelled`.
- **SV33.** Cancel on or after the start date: V4 from 2026-10-01, published on 2026-10-01 → cancel on 2026-10-01 → `422 Timetable.ScheduleVersionAlreadyEffective`; with the clock moved to 2026-10-02 → same. Nothing written.
- **SV34.** Cancel a `Draft`, `Discarded` or `Cancelled` version → `422 Timetable.ScheduleVersionNotPublished`; unknown id → `404 Timetable.ScheduleVersionNotFound`.

### Discard (OQ56; R28)

- **SV35.** POST `/schedules/versions/{id}/discard` on a draft → `204`, `Status = Discarded`, `DiscardedAtUtc` set, rows kept and readable; one `Timetable.ScheduleVersionDiscarded` event. Discarding a `Published`, `Cancelled` or `Discarded` version → `422 Timetable.ScheduleVersionNotDraft`. A discarded version never applies and never refuses a withdrawal.

### F-004 withdrawal refused while a published version applies (OQ54; R19, §0.12)

- **SV36.** V1 from 2026-10-05, open-ended, lists S1. POST `/services/{S1}/withdraw` with `withdrawFrom = 2026-11-01` → `422 Timetable.ServiceInPublishedScheduleVersion`; S1 unchanged; no event. With `withdrawFrom = 2026-09-30` → `422 Timetable.WithdrawalDateInPast` (F-004's checks come first).
- **SV37.** Dropping a service (R20): V2 from 2027-01-01 is published without S1. Withdraw S1 from 2027-01-01 → `204` (`EffectiveTo = 2026-12-31`). Withdraw S1 from 2026-12-31 instead → `422 Timetable.ServiceInPublishedScheduleVersion` (V1 applies on 2026-12-31).
- **SV38.** No published version lists S2; only a draft, a discarded version and a cancelled version do → withdraw S2 from 2026-11-01 → `204`.
- **SV39.** A superseded past version: service S4 runs from 2026-09-01, open-ended; V0 from 2026-09-01 (published when the clock read 2026-09-01) lists S4; V1 from 2026-10-05 does not. Withdraw S4 from 2026-10-05 → `204` (V0 applies only to 2026-10-04).
- **SV54.** *(Added at approval, Q2; R49.)* **A withdrawal survives the cancellation that made it legal.** V1 from 2026-10-05 lists S1 (Monday–Friday); V2 from 2027-01-01 does not list S1; both published. Withdraw S1 from 2027-01-01 → `204` (`EffectiveTo = 2026-12-31`). Cancel V2 (today 2026-10-01) → `204`, not refused. S1 keeps `EffectiveTo = 2026-12-31`. `in-force?date=2026-12-28` (Monday) → `200` V1 with S1 `runsOnDate = true`; `date=2027-01-04` (Monday) → `200` V1, still listing S1, with `runsOnDate = false`. A later withdrawal of S1 from any D ≥ 2027-01-01 → `422 Timetable.WithdrawalDoesNotShorten` (F-004's check, before R19).

### Races under the Timetable-wide lock (E8 not accepted; R46)

Each is a forced-order test on real SQL Server (ADR-0026 item 8): both orders are run, and the invariant holds in each.

- **SV40.** **Publish vs withdraw.** V1 from 2026-10-05 is published and lists S2 only; a draft D2 from 2027-01-01 lists S1. In parallel: publish D2, and withdraw S1 from 2027-01-01.
  - Publish first → publish `204`; withdraw `422 Timetable.ServiceInPublishedScheduleVersion` (D2 applies from 2027-01-01 and lists S1).
  - Withdraw first → withdraw `204` (`EffectiveTo = 2026-12-31`); publish `422 Timetable.ScheduleServiceNotEffective`.
  - In neither order does a published version list a service that was withdrawn while that version applied.
- **SV41.** **Cancel vs withdraw.** V1 from 2026-10-05 lists S1; V2 from 2027-01-01 does not. In parallel: cancel V2, and withdraw S1 from 2027-01-01.
  - Cancel first → cancel `204`; withdraw `422 Timetable.ServiceInPublishedScheduleVersion` (V1 now applies open-ended).
  - Withdraw first → withdraw `204`; cancel `204` (R49, Q2 ruled (a)): V1 then applies from 2027-01-01 and lists S1, which does not run from that date (R18); S1 stays withdrawn.
- **SV42.** **Publish vs cancel vs discard.** Operations on versions in parallel serialise (R46): publish and discard of one draft → one succeeds, the other `422 Timetable.ScheduleVersionNotDraft`; two creates in parallel get numbers `n` and `n + 1`, never the same.

### Access, limits, integrity

- **SV43.** Anonymous → `401 Auth.Unauthenticated` on every `/schedules` endpoint.
- **SV44.** No `PATCH`, `PUT` or `DELETE` route exists under `/schedules/versions` (R26, R27, R32).
- **SV45.** `403`: a caller with only `schedules.read` POSTs create, publish, cancel or discard; a caller with only `services.*`, `routes.*` or `stations.*` calls any `/schedules` endpoint; a caller with only `schedules.*` POSTs `/services/{id}/withdraw` (R5).
- **SV46.** Grants: each of the eight roles can read; only `SystemAdministrator` and `RailwayAdministrator` can create, publish, cancel and discard (the seeded grants match `docs/10` §Schedule permission grants).
- **SV47.** A body over the endpoint's limit, declared or chunked → `413` before binding, framework ProblemDetails with no stack trace, type or path; more stop times than the cap for one service (200), more services than the cap (250), or more stop times in the version than the total cap (10,000; Amendment 3) → `400 Common.ValidationFailed`; exactly the caps → reaches the handler (2 MiB body limit); malformed JSON → bounded `400` (R41). The caps admit a version of 200 services × 40 stops.
- **SV48.** `pageSize=201` → `400 Timetable.InvalidPageRequest`; `in-force` with no `date` or a malformed one → `400 Common.ValidationFailed`.
- **SV49.** Audit actor fields come from the authenticated context; actor fields in the body are ignored (ADR-0017 §2). A refused request writes no event.
- **SV50.** A failed create writes nothing (one `SaveChangesAsync`).
- **SV51.** Database privileges: `ycr_app` holds exactly §7's grants; a test asserts the absences — no `DELETE` on any schedule table; no `UPDATE` of any `ScheduleVersions` column but the status columns; no `UPDATE` on child rows; no DDL; and it can take the Timetable-wide lock.
- **SV52.** Database backstops, by direct SQL under `ycr_app`: a stop-time row whose `(ServiceId, Position)` names no service stop, or an entry whose `ServiceId` names no service → refused (FK); a minute outside `0`–`1439`, an arrival later than the departure in one row, or a second `Published` row with the same `EffectiveFrom` → refused (checks, filtered unique index); a second row with the same `Number` → refused.

---

## 5. State changes

| Entity | From | Event | Guard | To |
|---|---|---|---|---|
| ScheduleVersion | (none) | `CreateScheduleVersion` | `schedules.manage`; R7–R17, R31; if it lists no services, `EffectiveFrom` > today (R50, Amendment 2); under the lock (R46, number) | Draft |
| ScheduleVersion | Draft | `PublishScheduleVersion` | `schedules.manage`; `EffectiveFrom` ≥ today (R31); if it lists no services, `EffectiveFrom` > today (R50, Amendment 1); every listed service effective on the start date (R17); no published version with that start date (R22); under the lock | Published |
| ScheduleVersion | Draft | `DiscardScheduleVersion` | `schedules.manage`; under the lock | Discarded |
| ScheduleVersion | Published | `CancelScheduleVersion` | `schedules.manage`; `EffectiveFrom` > today (R34); under the lock | Cancelled |
| Service (F-004) | any | `WithdrawService(D)` | F-004 R21 checks; **no published version listing S applies on a date ≥ D (R19)**; under the service-code lock and the Timetable-wide lock | Same service, shorter period |

`Published` → `Cancelled`, `Discarded` and `Cancelled` are final. "In force" and "applies" are derived from stored rows, never stored (R21). A version's content never changes after creation (R27, R32).

**Serialisation (R46; E8 not accepted).** Create (for its number), publish, cancel, discard and service withdrawal all take one exclusive, transaction-owned Timetable-wide `sp_getapplock` before the reads that decide, so each decision sees the committed result of the one before it. No race between these operations is accepted; SV26, SV40–SV42 prove the orders. Service **creation** does not take it: a new service is listed by no version, so it cannot affect a decision. Reads do not take it.

---

## 6. API

Base path `/api/v1`, JSON camelCase, GUID ids, dates `YYYY-MM-DD`, times `HH:mm` (ADR-0027), ProblemDetails with `errorCode` and `traceId` (`docs/20` §4; ADR-0004). No `Idempotency-Key` (R40). No version token (R47). Every endpoint also has the responses in `docs/08` §Applies to every protected endpoint.

| Method | Path | Request | Success | Error codes | Permission |
|---|---|---|---|---|---|
| POST | `/schedules/versions` | `CreateScheduleVersionRequest`; body limit and caps at PLAN (R41) | `201` + `{ id, number }`, `Location: /api/v1/schedules/versions/{id}` | `400 Common.ValidationFailed` · `400 Timetable.InvalidScheduleVersionName` · `400 Timetable.InvalidTimetableTime` · `400` malformed JSON · `401` · `403` · `413` · `422 Timetable.ScheduleVersionEffectiveFromInPast` · `422 Timetable.EmptyScheduleVersionNotInFuture` (Amendment 2, R50) · `422 Timetable.ScheduleServiceNotFound` · `422 Timetable.ScheduleServiceRepeated` · `422 Timetable.ScheduleServiceNotEffective` · `422 Timetable.ScheduleStopNotInService` · `422 Timetable.ScheduleStopTimesIncomplete` · `422 Timetable.ScheduleStopTimeUnexpected` · `422 Timetable.ScheduleDwellNegative` · `422 Timetable.ScheduleTimesNotIncreasing` | `schedules.manage` |
| GET | `/schedules/versions/{id}` | — | `200` + `ScheduleVersionResponse` | `401` · `403` · `404 Timetable.ScheduleVersionNotFound` | `schedules.read` |
| GET | `/schedules/versions/{id}/services/{serviceId}` | — | `200` + `ScheduleServiceTimesResponse` | `401` · `403` · `404 Timetable.ScheduleVersionNotFound` · `404 Timetable.ScheduleServiceNotInVersion` | `schedules.read` |
| GET | `/schedules/versions` | `?page=1&pageSize=50` (max 200) `&status=` (optional); ordered by `number` | `200` + `{ items: ScheduleVersionSummaryResponse[], page, pageSize, totalCount }` | `400 Timetable.InvalidPageRequest` · `400 Common.ValidationFailed` (an unknown `status` value; Amendment 3) · `401` · `403` | `schedules.read` |
| GET | `/schedules/versions/in-force` | `?date=YYYY-MM-DD` (required) | `200` + `ScheduleVersionInForceResponse` | `400 Common.ValidationFailed` · `401` · `403` · `404 Timetable.ScheduleVersionNotInForce` | `schedules.read` |
| POST | `/schedules/versions/{id}/publish` | no body | `204` | `401` · `403` · `404 Timetable.ScheduleVersionNotFound` · `409 Timetable.ScheduleVersionEffectiveFromTaken` · `409 Timetable.ScheduleVersionChangedConcurrently` · `422 Timetable.ScheduleVersionNotDraft` · `422 Timetable.ScheduleVersionEffectiveFromInPast` · `422 Timetable.EmptyScheduleVersionNotInFuture` (Amendment 1, R50) · `422 Timetable.ScheduleServiceNotEffective` | `schedules.manage` |
| POST | `/schedules/versions/{id}/cancel` | no body | `204` | `401` · `403` · `404 Timetable.ScheduleVersionNotFound` · `409 Timetable.ScheduleVersionChangedConcurrently` · `422 Timetable.ScheduleVersionNotPublished` · `422 Timetable.ScheduleVersionAlreadyEffective` | `schedules.manage` |
| POST | `/schedules/versions/{id}/discard` | no body | `204` | `401` · `403` · `404 Timetable.ScheduleVersionNotFound` · `409 Timetable.ScheduleVersionChangedConcurrently` · `422 Timetable.ScheduleVersionNotDraft` | `schedules.manage` |
| POST | `/services/{id}/withdraw` (F-004, **changed**) | unchanged | unchanged (`204`) | F-004's codes **plus `422 Timetable.ServiceInPublishedScheduleVersion`** (§0.12) | `services.manage` (unchanged) |

The route `/schedules/versions/in-force` is matched before `/schedules/versions/{id}` (a GUID route constraint on `{id}`), confirmed at PLAN.

```text
CreateScheduleVersionRequest  { nameEn, nameMy, effectiveFrom: date,
                                services: [ { serviceId,
                                              stopTimes: [ { position,                      // 1..k of the service
                                                             arrival: "HH:mm" | null,
                                                             departure: "HH:mm" | null } ] } ] }
CreateScheduleVersionResponse { id, number }
ScheduleVersionResponse       { id, number, nameEn, nameMy, effectiveFrom, status,
                                createdAtUtc, publishedAtUtc, discardedAtUtc, cancelledAtUtc,
                                services: [ { serviceId, code, nameEn, nameMy, direction, stopCount,
                                              effectiveFrom, effectiveTo, neverRuns } ] }
ScheduleServiceTimesResponse  { versionId, serviceId, code,
                                stops: [ { position, stationId, stationCode, stationNameEn,
                                           stationNameMy, arrival, departure } ] }
ScheduleVersionSummaryResponse { id, number, nameEn, nameMy, effectiveFrom, status, serviceCount,
                                 createdAtUtc, publishedAtUtc, discardedAtUtc, cancelledAtUtc }
ScheduleVersionInForceResponse { date, id, number, nameEn, nameMy, effectiveFrom, publishedAtUtc,
                                 services: [ { serviceId, code, runsOnDate } ] }
```

`status` is `"Draft"`, `"Published"`, `"Discarded"` or `"Cancelled"`. Service fields are current values from `ITimetableDbContext`; station fields current values through `INetworkReader` (R43). No request carries an actor field.

**Deliberately absent:** `PATCH`/`PUT`/`DELETE` on versions (R26, R27, R32, R25); adding or removing a draft's services (R27); withdrawing a version that has taken effect (R34); `schedules.publish` (R4); any public, unauthenticated read (R44).

---

## 7. Data

Schema `timetable` (E1). Every `*Utc` column is `datetimeoffset(3)` with `CK_<Table>_<Column>_Utc` (`DATEPART(TZOFFSET, …) = 0`; a null passes); every check is declared in the EF model as well as the migration; every foreign key is `NO ACTION`. All keys are within the `timetable` schema, so ADR-0025's cross-module conditions do not apply.

### `timetable.ScheduleVersions`

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | no | PK, clustered, application-assigned (R38) |
| `Number` | `int` | no | R7; unique, contiguous, never reused |
| `NameEn`, `NameMy` | `nvarchar(100)` | no | Owned `BilingualName`; `NameMy` Myanmar Unicode, never Zawgyi |
| `EffectiveFrom` | `date` | no | R8 |
| `Status` | `nvarchar(10)` | no | `Draft`, `Published`, `Discarded`, `Cancelled`; EF concurrency token (R47) |
| `CreatedAtUtc` | `datetimeoffset(3)` | no | |
| `PublishedAtUtc`, `DiscardedAtUtc`, `CancelledAtUtc` | `datetimeoffset(3)` | yes | Set by the transition |

| Object | Definition | Why |
|---|---|---|
| `PK_ScheduleVersions` | clustered on `Id` | ADR-0006 |
| `UX_ScheduleVersions_Number` | unique on `Number` | R7 backstop behind the lock |
| `UX_ScheduleVersions_EffectiveFrom_Published` | unique on `EffectiveFrom` `WHERE [Status] = N'Published'` | R22 authority; mapped to `409` by constraint name. Also the in-force seek (latest `EffectiveFrom` ≤ *d*) and the successor lookup |
| `CK_ScheduleVersions_Number` | `[Number] >= 1` | From any writer |
| `CK_ScheduleVersions_Status` | `[Status] IN (N'Draft', N'Published', N'Discarded', N'Cancelled')` | From any writer |
| `CK_ScheduleVersions_StatusInstants` | `Draft`: all three null · `Published`: `PublishedAtUtc` only · `Discarded`: `DiscardedAtUtc` only · `Cancelled`: `PublishedAtUtc` and `CancelledAtUtc` | The lifecycle (§5) from any writer |
| four `CK_ScheduleVersions_*Utc_Utc` | UTC checks | ADR-0018 |

### `timetable.ScheduleVersionServices`

| Column | Type | Null | Notes |
|---|---|---|---|
| `ScheduleVersionId` | `uniqueidentifier` | no | FK → `ScheduleVersions(Id)` |
| `ServiceId` | `uniqueidentifier` | no | FK → `timetable.Services(Id)` |

`PK_ScheduleVersionServices` on `(ScheduleVersionId, ServiceId)` (a service is listed once, R17); `IX_ScheduleVersionServices_ServiceId` covers the service FK and serves the withdrawal guard ("which versions list S", R19).

### `timetable.ScheduleStopTimes`

| Column | Type | Null | Notes |
|---|---|---|---|
| `ScheduleVersionId`, `ServiceId` | `uniqueidentifier` | no | FK → `ScheduleVersionServices` |
| `Position` | `int` | no | FK `(ServiceId, Position)` → `PK_ServiceStops` (D16) |
| `ArrivalMinute`, `DepartureMinute` | `smallint` | yes | ADR-0027; which may be null follows R10 |

| Object | Definition | Why |
|---|---|---|
| `PK_ScheduleStopTimes` | clustered on `(ScheduleVersionId, ServiceId, Position)` | One time row per stop; covers the FK to `ScheduleVersionServices` |
| `CK_ScheduleStopTimes_Minutes` | each non-null minute in `0`–`1439` | R15, R16 |
| `CK_ScheduleStopTimes_AnyTime` | at least one of the two not null | R10 |
| `CK_ScheduleStopTimes_Dwell` | `ArrivalMinute IS NULL OR DepartureMinute IS NULL OR DepartureMinute >= ArrivalMinute` | R12 |
| covering index for `(ServiceId, Position)` | decided at PLAN | EF adds one by convention otherwise (`docs/07` §F-004 lesson) |

First- and last-stop rules, contiguity and ordering across stops are enforced by the aggregate, because a check cannot see other rows.

### Grants to `ycr_app`

| Table | Granted | Deliberately absent |
|---|---|---|
| `ScheduleVersions` | `SELECT`, `INSERT`, `UPDATE(Status, PublishedAtUtc, DiscardedAtUtc, CancelledAtUtc)` | `DELETE`; `UPDATE` of `Id`, `Number`, names, `EffectiveFrom`, `CreatedAtUtc` |
| `ScheduleVersionServices`, `ScheduleStopTimes` | `SELECT`, `INSERT` | `UPDATE`, `DELETE` |

No DDL and no `EXECUTE` (`sp_getapplock` is executable by `public`). The F-004 grants on `Services` and `ServiceStops` are unchanged.

### Migrations

`…_Timetable_CreateScheduleVersions` (the three tables and every object above; EF model-built), `…_Identity_SeedSchedulePermissionGrants` (ten grants: `schedules.manage` × 2, `schedules.read` × 8, matching `docs/10` §Schedule permission grants), `…_Security_TimetableScheduleGrants`. Reviewed per `docs/workflows/04-database-change.md`. No `network` change. `IdentitySeedTests` gains the `## Schedule permission grants` heading in its parser and its expected grant count moves from 34 to 44.

The rows are the history: a published version's rows never change, so "which times applied on *d*" is answered from rows, not the ledger (R25, R36).

---

## 8. Audit, logging, metrics

- **Audit** (ADR-0017, ADR-0021), through `IAuditWriter`, in the same `SaveChangesAsync` as the change, so a refused or losing request writes no event:

  | Action | When | `BeforeJson` | `AfterJson` |
  |---|---|---|---|
  | `Timetable.ScheduleVersionCreated` | SV1 | null | full snapshot |
  | `Timetable.ScheduleVersionPublished` | SV23 | header snapshot | header snapshot |
  | `Timetable.ScheduleVersionDiscarded` | SV35 | header snapshot | header snapshot |
  | `Timetable.ScheduleVersionCancelled` | SV31 | header snapshot | header snapshot |

  `SubjectType` = `Timetable.ScheduleVersion` (new `TimetableAuditSubjects` constant); `SubjectId` = version id; `AuthorizedByPermission` = `schedules.manage`; `PayloadVersion` = 1. **Header snapshot:** `{ number, nameEn, nameMy, effectiveFrom, status, publishedAtUtc, discardedAtUtc, cancelledAtUtc }`. **Full snapshot (E13 accepted):** the header plus `services: [{ serviceId, serviceCode, stopCount }]` and `stopTimesSha256`, a SHA-256 over a canonical serialisation of every `(serviceId, position, arrivalMinute, departureMinute)` in order (the canonical form is fixed at PLAN). No personal data, token or key (ADR-0021 rule 4).
- `Timetable.ServiceWithdrawn` (F-004) is unchanged; a withdrawal refused by R19 writes no event.
- **Logging** (`docs/20` §7): message templates, no personal data.
- **Metrics** (`docs/17`): none required; request metrics from the F-001 baseline.

---

## 9. Out of scope

- Withdrawing a version that has already taken effect (SC ruling, R34); a second approver (OQ57); editing a draft, or adding and removing its services (OQ56); running past midnight (OQ53).
- Public holidays and per-date exceptions (OQ47, still open; ruling H).
- Fares and fare inputs (OQ9, OQ17, OQ21); ticketing, validation, binding tickets to departures (OQ4), validity windows and ticket printing (OQ19), the QR payload (ADR-0014).
- A Timetable contract for other modules (R43); a public, printed or passenger-facing timetable (R44).
- Real timetable data (OQ1, R37); real-time tracking (`docs/00`).
- Any change to services, routes or stations other than the F-004 withdrawal guard (§0.12).
- Editing the glossary, `docs/business/mr-questions-pack.md`, `docs/07`, `docs/08` and `docs/20` in stage 2; stage 8 updates them.

**Known limitations (accepted by the rulings, hein, 2026-09-26):**
- **OQ53:** no service may run past midnight; a journey that would cross midnight cannot be entered (R15).
- **OQ47:** services run on their weekdays on public holidays; no single date can be added or cancelled.
- **OQ52:** one set of times per service per version, the same on every operating day; a timing difference between days needs two services.
- **OQ58:** a version that has taken effect cannot be cancelled or withdrawn; a mistake in it is corrected by publishing a later version.
- **OQ56:** a draft cannot be corrected; a wrong draft is discarded and created again, whole.
- **OQ54:** a service cannot be withdrawn while a published version that lists it still applies; dropping it takes a new version first. A new service does not run until a version from its first day lists it.
- **Q2 (ruled (a), R49):** cancelling a future version may leave the earlier version applying again over dates on which a service it lists was withdrawn; that service stays withdrawn and does not run (R18), and the in-force read reports it with `runsOnDate = false`.
- **OQ60 (ruled (b), R9):** an empty version, once published and in force, stops every service on the network from its start date; nothing asks for confirmation beyond `schedules.manage` (R4). Amendments 1–2 (R50) require it to start after today, at creation and at publication, so it can always be cancelled before it takes effect.

---

## Blocked behaviour

Required by `docs/21` §Specification.

| Behaviour | Blocking item | Effect on F-005 |
|---|---|---|
| Whether a version may list no services | OQ60 ruling (yes, hein, 2026-09-26; provisional) | Not blocked; allowed (R9, SV22, SV53); creation and publication only with a start date after today (R50, Amendments 1–2; SV55, SV56) |
| Public holidays and per-date exceptions | **OQ47** (open part, still open with Myanma Railways) | Not implemented, no placeholder (R35) |
| Running past midnight | OQ53 ruling (no, in Phase 1) | Refused; a later ruling needs its own follow-up task |
| Withdrawing a version in force; second approver; draft editing | SC, OQ56–OQ58 rulings | Not provided |
| Real timetable data | **OQ1** | No seed (R37) |
| Ticket binding to departures; validity window | **OQ4**, **OQ19** | Out of scope; F-005 keeps them possible (R36) |
| Official Myanma Railways answers to OQ51–OQ60 | **Still open with Myanma Railways** | Not blocking: provisional tech-lead rulings apply (§0.10). An official, different answer supersedes the ruling and needs its own follow-up task |

No placeholder rule may be implemented for any of these (AGENTS.md §When a business rule is missing).

---

## Notes for the next stage

- Stage 2 is complete: spec Approved (hein, 2026-09-26). Stage 3 (PLAN) is a separate task (T-054).
- PLAN decides: the Timetable-wide lock's resource name, the order in which `WithdrawService` takes it with the service-code lock (or whether one replaces the other), and its proof (R46; SV26, SV40–SV42); the number-assignment query under the lock (R7); the body limits and caps from 200 services × 40 stops, with the reasoning (R41); the exact check order (R45); the canonical serialisation for `stopTimesSha256` (§8); the covering index for `(ServiceId, Position)`; the `in-force` route precedence (§6).
- PLAN lists the F-004 code and tests that change (§0.12).
- Stage 8: `docs/07` (§F-005 tables, the lock, the F-004 lock paragraph), `docs/08` (replace the two proposed `/schedules/versions` lines with §Implemented in F-005; the F-004 withdraw row), `docs/20` §6 (ADR-0026 uses) and — **C4** — its §4 citation of ADR-0002 for timetables, the glossary (`ScheduleVersion`, version number, stop time, timetable time, in force, applies, cancel, discard; C3; the `Withdrawal` entry), a dated amendment note in the F-004 spec at R21, and — **C5** — ADR-0024's stale Context (it stays Proposed; a Proposed-stage note may correct it).
- Add OQ51–OQ60 to `docs/business/mr-questions-pack.md` (hein; listed in `progress.md`), all marked as provisionally ruled.

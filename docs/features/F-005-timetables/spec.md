# F-005: Timetables

Status: **Draft — awaiting hein's rulings (stage-2 ⛔).** Written by claude (T-053). Stages 1–2 of `docs/workflows/02-feature-development.md`. Not approvable yet: nine new business questions (OQ51–OQ59) block it (§3 Blocking open questions). §0.10 lists every ruling needed.

Module(s): `Timetable` (existing, F-004); no new cross-module dependency proposed (E12); cross-cutting `Audit`, `Identity` (permission grants only)
Related: FR-004 (and FR-003 through F-004), UC 4 "Publish schedules" (`docs/03-use-cases.md` §Core use cases, item 4), F-004 (services), ADR-0002, ADR-0004, ADR-0006, ADR-0012, ADR-0013, ADR-0017, ADR-0018, ADR-0019, ADR-0021, ADR-0024 (Proposed), ADR-0025, ADR-0026, ADR-0027 (Proposed, new)

Decision owner:
- Business rules (what a timetable version is and contains, stop times, past-midnight running, how versions relate to service periods and to each other, drafts, publication and its approval, withdrawal of a published version, role grants): **Myanma Railways**, routed through `hein` (`docs/19-open-questions.md` OQ51–OQ59). Suggested routing label, following `docs/business/mr-questions-pack.md`: Network Operations / Planning (timetabling role to be nominated), as for OQ42–OQ50. hein may give provisional tech-lead rulings, as for F-004.
- Scope boundary and engineering decisions: **tech lead (`hein`)**, recorded as ADRs per `docs/decisions/README.md`.

Authoritative sources: `docs/00`; `docs/01` §FR-003, §FR-004; `docs/03`; `docs/04`; `docs/05`; `docs/07`; `docs/08`; `docs/10`; `docs/11`; `docs/12`; `docs/19`; `docs/20`; `docs/21`; `docs/glossary.md`; ADR-0002/0004/0006/0012/0013/0017/0018/0019/0021/0024/0025/0026; the F-004 spec, plan and review, and the `Timetable` code at `main` `60d624a`. **No Myanma Railways-authoritative source defines any timetable version, stop time, publication rule or timetable permission grant.**

---

## 0. Discovery notes

Stage-1 output. Every note cites its source and names the decision owner. Nothing in §0.1–§0.9 is a decision; §0.10 lists what hein must rule.

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

### 0.5 New OPEN QUESTIONs added to `docs/19`

OQ51 what a version is, its scope, identity and effective dates · OQ52 stop times (arrival/departure, first/last stop, precision, dwell, ordering, passing times, per-weekday times) · OQ53 running past midnight · OQ54 versions vs service effective periods, withdrawn services, withdrawal of a service in a published version, and C1 · OQ55 how versions follow one another (one in force, supersession, overlap, gaps, insertion, history) · OQ56 how a draft is prepared (created whole, built up, edited; discard; several drafts) · OQ57 who publishes, maker-checker, lead time, past start date · OQ58 cancelling or withdrawing a published version · OQ59 role grants for `schedules.*`. Each has a **BLOCKS:** line. `docs/business/mr-questions-pack.md`, the glossary and `docs/10` are **not** edited in this stage; `progress.md` lists the nine for hein.

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

§4–§7 are written against a **worked example W** (§0.9) inside SC2 so that PLAN would have a concrete shape. W is an illustration, **not a recommendation and not a ruling**; every W choice is an OPEN QUESTION until hein rules, and §3–§7 change with the rulings.

### 0.9 Worked example W (illustration only)

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

### 0.10 Rulings needed from hein

Each business item is an OPEN QUESTION until ruled; options are non-binding prompts, not recommendations.

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

---

## 1. Goal

Railway administrators need to publish timetable versions — the stop times of the network's services, with the dates from which they apply — so that everyone reading the system sees one authoritative timetable for any date, published versions never change, and later features (ticketing, reporting) can tell which timetable applied on a past date.

---

## 2. Actors and permissions

| Actor | Permission | Held by |
|---|---|---|
| Staff user preparing and discarding drafts | `schedules.manage` | **OPEN QUESTION (OQ59)** — `docs/10` inventory, grants not approved |
| Staff user publishing a version | `schedules.manage`, or `schedules.publish` if OQ57 separates it (E5) | **OPEN QUESTION (OQ57, OQ59)** |
| Staff user reading versions and times | `schedules.read` (name proposed, E5) | **OPEN QUESTION (OQ59)** |

No grant is inferred from a name (`docs/10`). Holding `services.*`, `routes.*` or `stations.*` gives no schedule right, and `schedules.*` gives no service, route or station right (the F-003/F-004 pattern; confirmed by the OQ59 ruling).

---

## 3. Business rules

Rules labelled **OPEN QUESTION** are not implemented in any form until ruled (AGENTS.md §When a business rule is missing). Where a rule says "W:", that is the worked example (§0.9), not a decision.

| # | Rule | Label | Source |
|---|---|---|---|
| R1 | Authorized users can publish timetable versions with effective dates. | FACT | `docs/01` §FR-004 |
| R2 | A timetable version is the `Timetable` aggregate `ScheduleVersion`; Ticketing never navigates to it. | FACT (`docs/04`, `docs/05`); ENGINEERING DECISION (ADR-0013; E1) | `docs/04` §Boundary clarifications |
| R3 | Preparing and discarding drafts requires `schedules.manage`. Grants: OQ59. | Name: FACT (`docs/10`). Use for drafts: ENGINEERING proposal (E5). Grants: **OPEN QUESTION (OQ59)** | `docs/10` §Permission inventory |
| R4 | Publishing requires `schedules.manage`, or a separate `schedules.publish` if OQ57 separates it. | **OPEN QUESTION (OQ57, OQ59)**; name: ENGINEERING proposal (E5) | `docs/19` OQ57 |
| R5 | Reading versions requires `schedules.read`. | Name: ENGINEERING proposal (E5). Grants: **OPEN QUESTION (OQ59)** | `docs/10` (no read permission, D10) |
| R6 | A version's scope: every service on the network, one route's services, or one service. W: the whole network. | **OPEN QUESTION (OQ51)** | `docs/19` OQ51 |
| R7 | A version's identity: number, code and/or bilingual name; formats; uniqueness. W: `BilingualName`, not unique. | **OPEN QUESTION (OQ51)** | `docs/19` OQ51 |
| R8 | A version's dates: `EffectiveFrom` only, or `EffectiveFrom` and `EffectiveTo`. Dates are `DateOnly` / `date`, inclusive. | Dates: **OPEN QUESTION (OQ51, OQ55)**. Types: ENGINEERING DECISION (ADR-0018; E4) | `docs/01` §FR-004 "with effective dates" |
| R9 | Whether a version may be empty. W: at least one service (`400 Common.ValidationFailed`). | **OPEN QUESTION (OQ51)** | — |
| R10 | Which times each stop has (arrival, departure, both; first and last stop). W: stop 1 departure only; last stop arrival only; others both. | **OPEN QUESTION (OQ52)** | `docs/19` OQ52 |
| R11 | Time precision. W: whole minutes. | **OPEN QUESTION (OQ52)** | `docs/19` OQ52 |
| R12 | Dwell and ordering. W: departure ≥ arrival at a stop (`422 Timetable.ScheduleDwellNegative`); each arrival later than the previous stop's departure (`422 Timetable.ScheduleTimesNotIncreasing`). | **OPEN QUESTION (OQ52)**; codes: ENGINEERING proposal (E1) | `docs/19` OQ52 |
| R13 | Passing times at stations passed without stopping. W: none. | **OPEN QUESTION (OQ52)** | `docs/19` OQ52; F-004 R13 |
| R14 | Whether a service's times may differ by weekday. W: one set of times per service per version. | **OPEN QUESTION (OQ52)** | `docs/19` OQ52; F-004 R17 |
| R15 | Whether a service may run past midnight, and the maximum journey length. W: no (`00:00`–`23:59`). The operating date is the date the service starts. | Past midnight: **OPEN QUESTION (OQ53)**. Operating date: FACT (provisional ruling OQ47) | `docs/19` OQ47, OQ53 |
| R16 | A time is stored as an integer offset from local midnight of the operating date, exchanged as `HH:mm`. | ENGINEERING DECISION, proposed in ADR-0027 (Proposed) | ADR-0027 |
| R17 | How a listed service's effective period must relate to the version's dates. W: not ended before `EffectiveFrom` (`422 Timetable.ScheduleServiceNotEffective`). | **OPEN QUESTION (OQ54)** | `docs/19` OQ54; F-004 C5 |
| R18 | Whether a withdrawn or never-running service may be listed. W: a never-running service may not (same code). | **OPEN QUESTION (OQ54)** | `docs/19` OQ54; F-004 R41 |
| R19 | What F-004 withdrawal of a service listed in a published version does. W: allowed; the version is unchanged; the service stops running from D. | **OPEN QUESTION (OQ54)**; C2 | F-004 R21; `docs/08` line 35 |
| R20 | How a timetable changes: a new version, F-004's withdraw + recreate, or both. | **OPEN QUESTION (OQ54)**; C1 | `docs/19` OQ48; `docs/01` §FR-004 |
| R21 | How many versions may be in force on one date. W: at most one. | **OPEN QUESTION (OQ55)** | `docs/19` OQ55 |
| R22 | Supersession. W: in force on *d* = the published version with the greatest `EffectiveFrom` ≤ *d*; no two published versions share an `EffectiveFrom` (`409 Timetable.ScheduleVersionEffectiveFromTaken`). | **OPEN QUESTION (OQ55)**; enforcement: ENGINEERING proposal (E6) | `docs/19` OQ55 |
| R23 | Gaps: dates on which no version is in force. W: only before the first published version. | **OPEN QUESTION (OQ55)** | `docs/19` OQ55 |
| R24 | Inserting a version before an already-published future version. W: allowed. | **OPEN QUESTION (OQ55)** | `docs/19` OQ55 |
| R25 | Every version, published or not, is kept and stays readable; no version row is deleted and ids never change. | Keep history: FACT (`docs/07` §Rules). No delete, stable ids: ENGINEERING proposal (E9) | `docs/07` §Rules; AGENTS.md rule 5 |
| R26 | A version is created, then published; a published version is never patched. | FACT | `docs/20` §4; `docs/08` §Initial resources |
| R27 | Whether a draft may change, and how. W: created whole, never changed. | **OPEN QUESTION (OQ56)**; engineering consequence E7 (ADR-0024 if edited) | `docs/19` OQ56; ADR-0024 |
| R28 | Whether a draft may be discarded or deleted, and whether a discarded draft is kept. W: discarded, kept (status `Discarded`). | **OPEN QUESTION (OQ56)** | `docs/19` OQ56 |
| R29 | Whether several drafts may exist at once. W: yes. | **OPEN QUESTION (OQ56)** | `docs/19` OQ56 |
| R30 | Who publishes; whether a second person approves. W: any holder of the publish right, including the author. | **OPEN QUESTION (OQ57)** | `docs/19` OQ57 |
| R31 | Earliest `EffectiveFrom` at creation and at publication. W: ≥ today at both (`422 Timetable.ScheduleVersionEffectiveFromInPast`). | **OPEN QUESTION (OQ57)** | `docs/19` OQ57, OQ50 |
| R32 | A published version is immutable: no field, entry or time changes. | FACT | `docs/08` line 35; `docs/20` §4; glossary `ScheduleVersion` |
| R33 | "Today" is `ILocalCalendar.Today()` (Asia/Yangon from configuration), never server local time or a hard-coded offset. | ENGINEERING DECISION | ADR-0018; `docs/20` §8 |
| R34 | Whether a published version may be cancelled before it takes effect, or withdrawn while in force, and what is in force afterwards. W: neither, in F-005. | **OPEN QUESTION (OQ58)** | `docs/19` OQ58 |
| R35 | No public-holiday calendar and no per-date exception. Services run on their weekdays throughout the period and the version, holidays included. | **OPEN QUESTION (OQ47, still open with Myanma Railways)** — not implemented, no placeholder | `docs/19` OQ47; F-004 §9 |
| R36 | OQ4 and OQ19 do not block F-005. R25, R32 and E9 keep a departure-bound ticket (if OQ4 so rules) reconstructible for any past date. | FACT (analysis, G10) | `docs/19` OQ4, OQ19; ADR-0014 |
| R37 | No real timetable data ships; tests build their own stations, routes, services and versions. OQ1 blocks data, not the API. | FACT (consequence of OQ1, open) | `docs/19` OQ1; F-004 R27 |
| R38 | Identifiers are application-generated GUIDs through `IIdGenerator`, `ValueGeneratedNever()`. | ENGINEERING DECISION | ADR-0006 |
| R39 | Every create, publish and discard is audited in the same `SaveChangesAsync`, with actor fields from the authenticated server-side context only. | ENGINEERING DECISION | ADR-0017 §1–2; ADR-0021 |
| R40 | No `Idempotency-Key`. | ENGINEERING proposal (E11) | `docs/20` §5 |
| R41 | Every body-carrying endpoint has an explicit body limit (`413` before binding); stops per service ≤ 200; services per request ≤ a cap set at PLAN. | REQUIRED CONTROL (E10) | F-003 R27; F-004 R31 |
| R42 | Error codes are `Timetable.<Reason>`, mapped per ADR-0004. | ENGINEERING DECISION (E1) | ADR-0004 |
| R43 | Timetable reads services through `ITimetableDbContext`; route and station display data, if shown, only through `INetworkReader`. No Timetable contract is added. | ENGINEERING DECISION (ADR-0012; ADR-0025); proposal E12 | ADR-0025 |
| R44 | No public or printed timetable. | FACT (no source requires one, D23) | `docs/00`; `docs/03` |
| R45 | When a create breaks several rules, checks run in a fixed order and the first failure is returned; the order is fixed at PLAN once the rulings settle which checks exist (F-004 R37 pattern). | ENGINEERING DECISION (proposed) | F-004 R37 |

**Blocking open questions:** **OQ51, OQ52, OQ53, OQ54, OQ55, OQ56, OQ57, OQ59** block this spec's approval. **OQ58** blocks it under SC1; under SC2 it only needs hein to confirm that withdrawal and cancellation of a published version are out of F-005. OQ47's open part (R35) does not block if hein confirms row H. OQ1, OQ4, OQ17 and OQ19 stay open and do not block.

---

## 4. Scenarios (Given / When / Then)

Written against **worked example W** (§0.9) inside SC2. Every scenario whose outcome depends on an OPEN QUESTION names it; the expected result is W's and changes with the ruling. Error codes are ENGINEERING proposals (R42). Status codes follow ADR-0004; every error is ProblemDetails with `errorCode` and `traceId`.

Unless a scenario says otherwise: today (Asia/Yangon, test clock) is **2026-10-01**; route **RC** is closed, `[A, B, C, D, E]`; services **S1** (`[A, C, E]`, `Forward`, Monday–Friday, from 2026-10-05, open-ended) and **S2** (`[E, C, A]`, `Reverse`, same days and period) exist; the caller holds `schedules.manage` and `schedules.read`. A valid create has `nameEn`, `nameMy`, `effectiveFrom = 2026-10-05`, and S1 with times `A dep 06:00, C arr 06:20 dep 06:22, E arr 06:40`. "Nothing written" means no version row, no child row and no audit event.

### Happy path

- **SV1.** POST `/schedules/versions` → `201 Created`, `Location: /api/v1/schedules/versions/{id}`, `{ id }`; one `ScheduleVersions` row (`Status = Draft`, `PublishedAtUtc` null), one entry row per listed service, one stop-time row per stop; one `Timetable.ScheduleVersionCreated` event.
- **SV2.** GET `/schedules/versions/{id}` (holder of `schedules.read`) → `200` `ScheduleVersionResponse`: header, status, and each listed service's id, current code, name, direction and stop count. GET `/schedules/versions/{id}/services/{serviceId}` → `200` with the service's stops in position order, each station's current code and names (through `INetworkReader`), and its arrival and departure as `HH:mm`. No EF entity is serialised.
- **SV3.** GET `/schedules/versions?page=1&pageSize=50&status=Published` → `200` with `items, page, pageSize, totalCount`, ordered by `effectiveFrom` then `createdAtUtc`; drafts and discarded versions appear only when asked for.
- **SV4.** POST `/schedules/versions/{id}/publish` on a draft → `204`; `Status = Published`, `PublishedAtUtc` = the clock's UTC now, in one `UPDATE`; child rows untouched; one `Timetable.ScheduleVersionPublished` event.
- **SV5.** GET `/schedules/versions/in-force?date=2026-10-06` → `200` with the header of the version in force on that date (R22).

### Times (OQ52, OQ53; R10–R16)

- **SV6.** An arrival at stop 1, or a departure at the last stop → `422 Timetable.ScheduleStopTimeUnexpected`; nothing written.
- **SV7.** A missing departure at an intermediate stop, a missing arrival at the last stop, a stop position missing, repeated or beyond the service's stop count → `422 Timetable.ScheduleStopTimesIncomplete`; nothing written.
- **SV8.** At C, `arr 06:22 dep 06:20` → `422 Timetable.ScheduleDwellNegative`; `arr 06:20 dep 06:20` (dwell 0) → `201`.
- **SV9.** C `arr 06:00` after A `dep 06:00` → `422 Timetable.ScheduleTimesNotIncreasing` (equal is refused); C `arr 05:59` → same. Nothing written.
- **SV10.** `"24:15"` → `400 Timetable.InvalidTimetableTime` under W3 (OQ53 (a)); under OQ53 (b) it is accepted as 00:15 on the next day. `"6:00"`, `"06:60"`, `"06:00:30"` (W2 minutes), `""` → `400 Timetable.InvalidTimetableTime`.
- **SV11.** A full-circuit service S3 on RC, `Forward`, `[C, D, E, A, B, C]`: stop 6 (C again) has an arrival only; its arrival is after stop 5's departure → `201`.

### Services in a version (OQ54; R17–R19)

- **SV12.** An unknown `serviceId` → `422 Timetable.ScheduleServiceNotFound`; the same service listed twice → `422 Timetable.ScheduleServiceRepeated`. Nothing written.
- **SV13.** A service withdrawn so that its `EffectiveTo` (2026-10-04) is before the version's `EffectiveFrom` (2026-10-05), or a never-running service → `422 Timetable.ScheduleServiceNotEffective`; nothing written. A service starting 2026-11-01 (after the version starts) → `201` (W4).
- **SV14.** An empty `services` array → `400 Common.ValidationFailed` (W1; OQ51).
- **SV15.** S1 is listed in published version V1. Withdraw S1 from 2026-11-01 (`POST /services/{S1}/withdraw`) → `204` exactly as F-004 (W4; OQ54 (a)); V1's rows are unchanged; SV2 on V1 shows S1 with its current `effectiveTo = 2026-10-31`.

### Version fields and dates (OQ51, OQ57; R7, R31)

- **SV16.** A blank or 101-character name after trimming → `400 Timetable.InvalidScheduleVersionName`; a missing or malformed `effectiveFrom` → `400 Common.ValidationFailed`.
- **SV17.** `effectiveFrom = 2026-09-30` (before today) → `422 Timetable.ScheduleVersionEffectiveFromInPast`; `2026-10-01` (today) → `201` (W7).

### Publish (OQ55, OQ57; R22, R30–R32)

- **SV18.** Publish a version that is `Published` or `Discarded` → `422 Timetable.ScheduleVersionNotDraft`; an unknown id → `404 Timetable.ScheduleVersionNotFound`.
- **SV19.** A draft created on 2026-10-01 for 2026-10-02 is published on 2026-10-03 (test clock moved) → `422 Timetable.ScheduleVersionEffectiveFromInPast`; nothing written.
- **SV20.** A published version from 2026-10-05 exists; publishing another draft from 2026-10-05 → `409 Timetable.ScheduleVersionEffectiveFromTaken`; nothing written.
- **SV21.** Two drafts from the same date published in parallel → one `204`, one `409 Timetable.ScheduleVersionEffectiveFromTaken` (the filtered unique index decides, E6); one event.
- **SV22.** Publish and discard of the same draft in parallel → exactly one succeeds; the other returns `422 Timetable.ScheduleVersionNotDraft` (it read the new status) or `409 Timetable.ScheduleVersionChangedConcurrently` (the `Status` token fired, E7); one event.
- **SV23.** A draft lists S1; S1 is then withdrawn so that it ends before the draft's `EffectiveFrom`; publishing → `422 Timetable.ScheduleServiceNotEffective` (publish re-checks R17).

### Supersession (OQ55; R21–R24)

- **SV24.** V1 from 2026-10-05 and V2 from 2027-01-01 are published. In force on 2026-12-31 → V1; 2027-01-01 → V2; 2026-10-04 → `404 Timetable.ScheduleVersionNotInForce` (W5).
- **SV25.** Then V3 from 2026-11-01 is published (insertion, W5) → in force on 2026-11-15 → V3; on 2027-01-01 → V2. V1 stays readable.

### Discard (OQ56; R28)

- **SV26.** POST `/schedules/versions/{id}/discard` on a draft → `204`, `Status = Discarded`, `DiscardedAtUtc` set, rows kept; one `Timetable.ScheduleVersionDiscarded` event. Discarding a published version → `422 Timetable.ScheduleVersionNotDraft`.

### Access, limits, integrity

- **SV27.** Anonymous → `401 Auth.Unauthenticated` on every `/schedules` endpoint.
- **SV28.** A caller with only `schedules.read` POSTs create, publish or discard → `403`; a caller with only `services.*`, `routes.*` or `stations.*` calls any `/schedules` endpoint → `403` (R3–R5).
- **SV29.** A body over the endpoint's limit → `413` before binding, no stack trace, type or path; more stop times than 200 for one service, or more services than the PLAN cap → `400 Common.ValidationFailed`; malformed JSON → bounded `400` (R41).
- **SV30.** `pageSize=201` → `400 Timetable.InvalidPageRequest`; `in-force` without a `date` or with a malformed one → `400 Common.ValidationFailed`.
- **SV31.** Audit actor fields come from the authenticated context; body fields claiming an actor are ignored (ADR-0017 §2).
- **SV32.** A failed create writes nothing (one `SaveChangesAsync`).
- **SV33.** Database privileges: `ycr_app` holds exactly §7's grants; a test asserts the absences — no `DELETE` on any schedule table; no `UPDATE` of any header column other than the status columns; no `UPDATE` on child rows; no DDL.
- **SV34.** Foreign keys: a stop-time row whose `(ServiceId, Position)` names no service stop, or an entry whose `ServiceId` names no service, inserted by direct SQL under `ycr_app`, is refused by the database.
- **SV35.** A Myanmar version name round-trips unchanged (real Myanmar Unicode text).
- **SV36.** No `PATCH`, `PUT` or `DELETE` route exists under `/schedules/versions` (R26, R32).

Not specified, because blocked: withdrawal or cancellation of a published version (OQ58); maker-checker (OQ57 (b)/(c)); incremental or edited drafts (OQ56 (b)/(c)); past-midnight times (OQ53 (b)/(c)); passing times (OQ52).

---

## 5. State changes

Under W (OQ56, OQ58 decide):

| Entity | From | Event | Guard | To |
|---|---|---|---|---|
| ScheduleVersion | (none) | `CreateScheduleVersion` | `schedules.manage`; R7–R18 as ruled; `EffectiveFrom` ≥ today (R31) | Draft |
| ScheduleVersion | Draft | `PublishScheduleVersion` | publish right (R4); `EffectiveFrom` ≥ today (R31); services re-checked (R17, R18); no published version with the same `EffectiveFrom` (R22, unique index) | Published |
| ScheduleVersion | Draft | `DiscardScheduleVersion` | `schedules.manage` | Discarded |
| Service | any | `WithdrawService` (F-004) | unchanged under W4 (OQ54 (a)); changes if OQ54 (b) (C2, E8) | Same service, shorter period |

Published and Discarded are final in F-005 (R32, R34). "In force" is derived, never stored (R22). A version's content never changes after creation under W6.

**Race between publish and service withdrawal (W4).** Publish re-checks the listed services inside its transaction; a withdrawal committing after that check produces the same end state as "publish, then withdraw", which W4 allows. Proposed as an accepted race, not serialised (E8; F-004 Q3 precedent). If OQ54 refuses such withdrawals, it must be serialised instead.

---

## 6. API (proposal, under W)

Base path `/api/v1`, JSON camelCase, GUID ids, dates `YYYY-MM-DD`, times `HH:mm` (ADR-0027), ProblemDetails with `errorCode` and `traceId`. No `Idempotency-Key` (R40). No version token (E7, under W6).

| Method | Path | Request | Success | Error codes | Permission |
|---|---|---|---|---|---|
| POST | `/schedules/versions` | `CreateScheduleVersionRequest`; body limit at PLAN (R41) | `201` + `{ id }`, `Location` | `400 Common.ValidationFailed` · `400 Timetable.InvalidScheduleVersionName` · `400 Timetable.InvalidTimetableTime` · `400` malformed JSON · `401` · `403` · `413` · `422 Timetable.ScheduleVersionEffectiveFromInPast` · `422 Timetable.ScheduleServiceNotFound` · `422 Timetable.ScheduleServiceRepeated` · `422 Timetable.ScheduleServiceNotEffective` · `422 Timetable.ScheduleStopTimesIncomplete` · `422 Timetable.ScheduleStopTimeUnexpected` · `422 Timetable.ScheduleDwellNegative` · `422 Timetable.ScheduleTimesNotIncreasing` | `schedules.manage` |
| GET | `/schedules/versions/{id}` | — | `200` + `ScheduleVersionResponse` | `401` · `403` · `404 Timetable.ScheduleVersionNotFound` | `schedules.read` |
| GET | `/schedules/versions/{id}/services/{serviceId}` | — | `200` + `ScheduleServiceTimesResponse` | `401` · `403` · `404 Timetable.ScheduleVersionNotFound` · `404 Timetable.ScheduleServiceNotInVersion` | `schedules.read` |
| GET | `/schedules/versions` | `?page&pageSize` (max 200) `&status=` | `200` + page | `400 Timetable.InvalidPageRequest` · `401` · `403` | `schedules.read` |
| GET | `/schedules/versions/in-force` | `?date=YYYY-MM-DD` | `200` + `ScheduleVersionSummaryResponse` | `400 Common.ValidationFailed` · `401` · `403` · `404 Timetable.ScheduleVersionNotInForce` | `schedules.read` |
| POST | `/schedules/versions/{id}/publish` | no body | `204` | `401` · `403` · `404 Timetable.ScheduleVersionNotFound` · `409 Timetable.ScheduleVersionEffectiveFromTaken` · `409 Timetable.ScheduleVersionChangedConcurrently` · `422 Timetable.ScheduleVersionNotDraft` · `422 Timetable.ScheduleVersionEffectiveFromInPast` · `422 Timetable.ScheduleServiceNotEffective` | `schedules.manage` or `schedules.publish` (OQ57) |
| POST | `/schedules/versions/{id}/discard` | no body | `204` | `401` · `403` · `404 Timetable.ScheduleVersionNotFound` · `409 Timetable.ScheduleVersionChangedConcurrently` · `422 Timetable.ScheduleVersionNotDraft` | `schedules.manage` |

```text
CreateScheduleVersionRequest { nameEn, nameMy, effectiveFrom: date,
                               services: [ { serviceId,
                                             stopTimes: [ { position, arrival: "HH:mm" | null,
                                                            departure: "HH:mm" | null } ] } ] }
ScheduleVersionResponse      { id, nameEn, nameMy, effectiveFrom, status, createdAtUtc,
                               publishedAtUtc, discardedAtUtc,
                               services: [ { serviceId, code, nameEn, nameMy, direction, stopCount,
                                             effectiveFrom, effectiveTo, neverRuns } ] }
ScheduleServiceTimesResponse { versionId, serviceId, code,
                               stops: [ { position, stationId, stationCode, stationNameEn,
                                          stationNameMy, arrival, departure } ] }
ScheduleVersionSummaryResponse { id, nameEn, nameMy, effectiveFrom, status, publishedAtUtc,
                                 serviceCount }
```

Service fields are current values from `ITimetableDbContext`; station fields are current values through `INetworkReader` (R43). No request carries an actor field.

**Deliberately absent:** `PATCH`/`PUT`/`DELETE` on versions (R26, R32, E9); withdraw or cancel of a published version (R34, OQ58); any public, unauthenticated read (R44).

---

## 7. Data (proposal, under W)

Schema `timetable`. `*Utc` columns `datetimeoffset(3)` with `CK_<Table>_<Column>_Utc`; every check declared in the EF model as well as the migration; every FK `NO ACTION`.

### `timetable.ScheduleVersions`

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | no | PK, clustered, application-assigned (R38) |
| `NameEn`, `NameMy` | `nvarchar(100)` | no | Owned `BilingualName` (W1; OQ51) |
| `EffectiveFrom` | `date` | no | R8 |
| `Status` | `nvarchar(10)` | no | `Draft`, `Published`, `Discarded`; EF concurrency token (E7) |
| `CreatedAtUtc` | `datetimeoffset(3)` | no | |
| `PublishedAtUtc`, `DiscardedAtUtc` | `datetimeoffset(3)` | yes | Set by the transition |

| Object | Definition | Why |
|---|---|---|
| `UX_ScheduleVersions_EffectiveFrom_Published` | unique on `EffectiveFrom` `WHERE [Status] = N'Published'` | R22 authority (E6); `409` mapping by constraint name. Also the in-force seek (greatest `EffectiveFrom` ≤ *d*) |
| `CK_ScheduleVersions_Status` | `[Status] IN (N'Draft', N'Published', N'Discarded')` | From any writer |
| `CK_ScheduleVersions_StatusInstants` | `Published` ⇔ `PublishedAtUtc` not null; `Discarded` ⇔ `DiscardedAtUtc` not null; never both | From any writer |

### `timetable.ScheduleVersionServices`

| Column | Type | Null | Notes |
|---|---|---|---|
| `ScheduleVersionId` | `uniqueidentifier` | no | FK → `ScheduleVersions(Id)` |
| `ServiceId` | `uniqueidentifier` | no | FK → `timetable.Services(Id)` (same module) |

PK `(ScheduleVersionId, ServiceId)` (no repeated service, SV12); `IX_ScheduleVersionServices_ServiceId` covers the service FK ("which versions list S").

### `timetable.ScheduleStopTimes`

| Column | Type | Null | Notes |
|---|---|---|---|
| `ScheduleVersionId`, `ServiceId` | `uniqueidentifier` | no | FK → `ScheduleVersionServices` |
| `Position` | `int` | no | FK `(ServiceId, Position)` → `timetable.ServiceStops` PK (D16) |
| `ArrivalMinute`, `DepartureMinute` | `smallint` | yes | ADR-0027; W2 nullability per stop |

PK `(ScheduleVersionId, ServiceId, Position)`. Checks: each minute in `0..1439` (W3; the bound follows OQ53); at least one of the two not null; `DepartureMinute >= ArrivalMinute` when both are set. First/last-stop rules and ordering across stops are enforced by the aggregate (a check cannot see other rows). An index to cover the `(ServiceId, Position)` FK is decided at PLAN (EF convention-index lesson, `docs/07` §F-004).

### Grants to `ycr_app`

| Table | Granted | Deliberately absent |
|---|---|---|
| `ScheduleVersions` | `SELECT`, `INSERT`, `UPDATE(Status, PublishedAtUtc, DiscardedAtUtc)` | `DELETE`; `UPDATE` of any other column |
| `ScheduleVersionServices`, `ScheduleStopTimes` | `SELECT`, `INSERT` | `UPDATE`, `DELETE` |

No DDL. No lock grant needed (E6 needs no lock under W5). **Migrations:** `…_Timetable_CreateScheduleVersions`, `…_Identity_SeedSchedulePermissionGrants` (after OQ59), `…_Security_TimetableScheduleGrants`. No `network` change; no change to `timetable.Services` or `ServiceStops` (their grants stay as F-004).

The rows are the history: a published version's rows never change, so "which times applied on *d*" is answered from rows, not the ledger (R25, R36).

---

## 8. Audit, logging, metrics

- **Audit** (ADR-0017, ADR-0021), through `IAuditWriter`, in the same `SaveChangesAsync` as the change:

  | Action | When | `BeforeJson` | `AfterJson` |
  |---|---|---|---|
  | `Timetable.ScheduleVersionCreated` | SV1 | null | snapshot |
  | `Timetable.ScheduleVersionPublished` | SV4 | header snapshot | header snapshot |
  | `Timetable.ScheduleVersionDiscarded` | SV26 | header snapshot | header snapshot |

  `SubjectType` = `Timetable.ScheduleVersion` (new `TimetableAuditSubjects` constant); `SubjectId` = version id; `AuthorizedByPermission` = the permission used; `PayloadVersion` = 1. Snapshot (E13, proposed): `{ nameEn, nameMy, effectiveFrom, status, publishedAtUtc, discardedAtUtc, services: [{ serviceId, serviceCode, stopCount }], stopTimesSha256 }`, where the hash covers a canonical serialisation of every `(serviceId, position, arrival, departure)` in order. No personal data, token or key (ADR-0021 rule 4).
- **Logging** (`docs/20` §7): message templates, no personal data.
- **Metrics** (`docs/17`): none required; request metrics from the F-001 baseline.

---

## 9. Out of scope

- Fares, fare inputs (travel date, service type) — OQ9, OQ17, OQ21.
- Ticketing, ticket validation, binding tickets to services or departures (OQ4), validity windows and ticket printing (OQ19), the QR payload (ADR-0014).
- A Timetable contract for other modules (E12) — the first consumer's feature defines it.
- A public, printed or passenger-facing timetable, languages and Myanmar numerals for it (R44; no source).
- Public holidays and per-date exceptions (OQ47, still open) — R35.
- Real timetable data (OQ1, R37); real-time tracking (`docs/00`).
- Any change to services, routes or stations, except what OQ54 may require of `WithdrawService` (C2).
- Under SC2: withdrawing or cancelling a published version (OQ58), maker-checker (OQ57), incremental or edited drafts (OQ56 (b)/(c)).
- Editing `docs/business/mr-questions-pack.md`, `docs/glossary.md` and `docs/10` in stages 1–2; stage 8 updates them, `docs/07` and `docs/08`.

**Known limitations under W (for hein to accept or reject with the rulings):**
- A mistake in a published version is corrected only by publishing a later version (W8).
- Services run on their weekdays on public holidays (OQ47).
- One set of times per service per version: a weekday/weekend timing difference needs two services (F-004's operating-day model, W2).
- Under W4, a published version may list a service that has since been withdrawn; the version is unchanged and the service simply stops running.

---

## Blocked behaviour

Required by `docs/21` §Specification.

| Behaviour | Blocking item | Effect on F-005 |
|---|---|---|
| Version scope, identity, dates | **OQ51** | Spec not approvable; W is illustration only |
| Stop-time shape, precision, dwell, passing times | **OQ52** | Same |
| Past-midnight running | **OQ53** | Same; ADR-0027's upper bound waits for it |
| Versions vs service periods; withdrawal of a published version's service; C1 | **OQ54** | Same; `WithdrawService` unchanged until ruled |
| Supersession, overlap, gaps, insertion | **OQ55** | Same |
| Draft preparation | **OQ56** | Same; ADR-0024 untouched until ruled |
| Publication authority, approval, lead time | **OQ57** | Same |
| Withdraw or cancel a published version | **OQ58** | Blocks under SC1; out of F-005 under SC2 |
| Role grants | **OQ59** | No grant seeded; no endpoint can be authorised |
| Public holidays, per-date exceptions | **OQ47** (open part) | Not implemented, no placeholder (R35) |
| Real timetable data | **OQ1** | No seed (R37) |
| Ticket binding, validity window | **OQ4**, **OQ19** | Out of scope; F-005 keeps them possible (R36) |

No placeholder rule may be implemented for any of these (AGENTS.md §When a business rule is missing).

---

## Notes for the next stage

- Stage 2 stops here (⛔). After hein rules on §0.10, the spec is revised: W replaced by the rulings, §3 relabelled, §4–§7 corrected, `docs/19` resolution blocks added for each ruled OQ, and — if OQ59 is ruled — `docs/10` gains §Schedule permission grants.
- Stage 3 (PLAN) then fixes the check order (R45), the body limits and service cap (R41), the in-force query plan, the FK-covering index for `ScheduleStopTimes`, and, if OQ55/OQ56 require them, the ADR-0026 lock or the parent-row token (E6, E7).
- Stage 8: `docs/07`, `docs/08` (replace the two proposed `/schedules/versions` lines), `docs/10`, the glossary (`ScheduleVersion`, stop time, timetable time, in force; C3), and ADR-0027's `docs/20` follow-up if Accepted.
- Add OQ51–OQ59 to `docs/business/mr-questions-pack.md` (hein; T-043-style housekeeping; listed in `progress.md`).

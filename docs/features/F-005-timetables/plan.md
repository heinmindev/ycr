# Plan: F-005 Timetables — the `ScheduleVersion` aggregate, stop times, publication, the Timetable-wide lock and the F-004 withdrawal guard

Spec: `spec.md` — **Approved (hein, 2026-09-26)** at `1469a92`, **amended by Amendment 1** (hein, 2026-09-27; T-054; spec §0.10, R50) in the same commit as revision 1 of this plan, and by **Amendment 2** (hein, 2026-09-27; T-054 Q3; spec §0.10, R45, R50, SV55) in the commit before revision 2. Every ruling is in spec §0.10–§0.12; the plan rulings Q1–Q5 are in §Rulings on Q1–Q5.
Stage: 3 (PLAN), `docs/workflows/02-feature-development.md`. Task T-054.
Binding inputs: ADR-0004, ADR-0006, ADR-0012, ADR-0017, ADR-0018, ADR-0021, ADR-0022, ADR-0025, **ADR-0026 (Accepted — application locks)**, **ADR-0027 (Accepted — minutes after local midnight)**; `docs/07`, `docs/08`, `docs/10` §Schedule permission grants; `docs/20`; `docs/21`; `docs/workflows/04-database-change.md`. Worked examples: the F-004 plan and `review-codex.md`, and the `Timetable` code at `feature/F-005` `1469a92` (= `main` for `src/` and `tests/`).

**Status: Approved (hein, 2026-09-27).** Revision 2. hein's rulings on Q1–Q5 are in §Rulings on Q1–Q5 and applied throughout. No production code is written in this stage; stage 4 is T-055.

---

## Understanding

F-005 adds timetable **versions** to the `Timetable` module. A version is network-wide: a system-assigned number, a bilingual name, a start date (`EffectiveFrom`) and the list of services that run while it is in force, each with one set of stop times in whole minutes after local midnight (ADR-0027). A version is **created whole** as a `Draft` and never edited; it is then **published** (Draft → Published), **discarded** (Draft → Discarded), or, once published and while its start date is still in the future, **cancelled** (Published → Cancelled). Nothing is ever deleted. On any date, the version **in force** is the published version with the latest start date on or before that date; a service runs on a date only if that version lists it and the date is inside the service's own period and on one of its weekdays (R18, R21, R48). F-004's service withdrawal gains a guard (R19): a service cannot be withdrawn from D while a published version that lists it still applies on some date ≥ D.

The feature has seven parts:

1. **Domain.** `ScheduleVersion` (root) owns `ScheduleVersionService` rows, which own `ScheduleStopTime` rows. `TimetableTime` is ADR-0027's value object. Every creation rule (R7, R9–R17, R31) is decided by the domain from facts the handler loads, in the fixed R45 order; every transition rule (R28, R30, R31, R34, R50) is a domain method; "in force" and "applies" (R19, R21) are one domain type, `PublishedTimeline`; "runs on date" (R18, R48) is one domain function.
2. **Persistence.** Three new tables in schema `timetable` with check constraints for minutes, dwell, the status lifecycle and UTC instants; a filtered unique index on the published start date (R22's authority); a unique number; same-module `NO ACTION` foreign keys into `Services` and `ServiceStops`; least-privilege grants; ten permission grants (34 → 44).
3. **Serialisation.** One Timetable-wide `sp_getapplock`, `timetable.ScheduleVersions`, taken by create, publish, discard, cancel and service withdrawal (R46). Withdrawal takes it **after** F-004's service-code lock, which no other lock holder ever requests, so the two locks cannot deadlock (§The Timetable-wide lock).
4. **The F-004 change.** `Service.Withdraw` takes the service's published-version coverage and refuses with `422 Timetable.ServiceInPublishedScheduleVersion` after F-004's own checks (R19, spec §0.12).
5. **Reads.** Get a version, get one service's times in a version, list versions, and the version in force on a date with `runsOnDate` per service.
6. **API.** Eight endpoints under `/api/v1/schedules/versions`; the create body sized from a whole-network version (2 MiB limit, caps 250 services, 200 stop times per service, **10,000 stop times per version**), proved on real Kestrel.
7. **Tests.** Every live scenario (SV1–SV56: 56) mapped to named tests; a no-deadlock test for withdraw vs publish; forced-order races SV40, SV41 in both orders; the complete list of existing tests whose counts or lists change.

The risks are new in kind for this codebase: a whole-network request two orders of magnitude larger than any before, a global lock next to a keyed lock (ordering), a change to an approved and merged feature's behaviour (F-004 withdrawal), and a filtered unique index as a business authority.

---

## Facts / Assumptions / Open questions

**FACT — `src/YCR.Domain/Timetable/Service.cs`:** `Withdraw(DateOnly withdrawFrom, DateOnly today, DateTimeOffset nowUtc)` decides F-004 R21 and mutates only on success. `ServiceTests.Service_ExposesNoMutatorOtherThanWithdraw` pins the public method names to exactly `["Create", "Withdraw"]`, so an overload would fail it and a `RunsOn` method on `Service` would too. Hence P10 (replace the signature) and P12 (a separate `ServiceRunningDay`).
**FACT — `src/YCR.Application/Timetable/WithdrawService/WithdrawServiceHandler.cs`:** reads the code without a lock, begins a transaction, takes `timetable.ServiceCode:<code>`, reloads the service with its stops, reads route and station codes through `INetworkReader` (inside the transaction), calls `Withdraw`, audits, saves once, commits. Nothing in it knows about versions.
**FACT — `src/YCR.Infrastructure/Timetable/SqlServerServiceCodeLock.cs`:** the ADR-0026 shape (exclusive, transaction-owned, parameter resource, 30 s, `THROW 50035` on a negative result). The new lock copies it with its own error number.
**FACT — `src/YCR.Infrastructure/Persistence/SqlServerUniqueConstraintTranslator.cs`:** translates error 2601 ("with unique index '<name>'") into `UniqueConstraintViolationException(name)`. A filtered unique index raises 2601 like any other, so R22's index can be mapped by name (V8). There is **no** `TimetableConstraints` class yet (F-004 P22: no unique index in `timetable`); F-005 adds it, on the `NetworkConstraints` pattern.
**FACT — `src/YCR.Application/DependencyInjection.cs` + `DependencyInjectionTests.HandlerTypes_IncludesEveryHandlerDefined`:** 30 handlers today; F-005 adds 8 → **38**.
**FACT — the grant count 34 is pinned in eight assertions and permission lists in five more** (§Existing tests whose counts or lists change). The schedule seed adds ten rows (`docs/10` §Schedule permission grants, already written at T-053), so 34 → 44.
**FACT — `docs/10` §Schedule permission grants** already exists in the bullet format `IdentitySeedTests.ReadDocs10Grants` parses; the test does not read it yet (its heading list names three sections). Adding the heading before the seed exists would fail the test, so it changes in the seed step.
**FACT — `tests/YCR.Api.Tests/Identity/MustChangePasswordTests.cs:47`:** the "every protected endpoint answers `403 Auth.PasswordChangeRequired`" test enumerates the endpoint table and replaces only `{id}` with a GUID. F-005's `/schedules/versions/{id}/services/{serviceId}` would keep a literal `{serviceId}`, fail the `:guid` constraint and answer `404`. The test changes (item 18 of the changed-tests table).
**FACT — `tests/YCR.Api.Tests/Timetable/ServiceEndpointsTests.cs:717`:** F-004's `AbsentEndpoints_AreNotRouted` asserts `POST /api/v1/schedules/versions` is **not** routed (F-004 R22). F-005 routes it, so that row cannot stay (item 17; Q4).
**FACT — `TimetableMigrationTests.TimetableGrantsAsync`** reads every `ycr_app` grant in the `timetable` **schema**, not only on F-004's two tables, and its upgrade test lists the schema's tables; both see F-005's objects (item 6).
**FACT — `UseSqlServer` without `EnableRetryOnFailure`** (ADR-0026 Context): the explicit transactions need no execution strategy.
**FACT — the Yangon test clock:** F-004 tests run at **2026-10-01 03:00 UTC** (09:30 Asia/Yangon); the spec's scenarios use today = **2026-10-01**, a Thursday.

### Verifications done at PLAN

Run in a scratch console app outside the repository (`net10.0`, SDK 10.0.302, `Microsoft.EntityFrameworkCore.SqlServer` 10.0.12), referencing `YCR.Infrastructure` at `1469a92` so the **real** F-004 model is the diff's source, with stand-in F-005 types configured as §DB changes describes. The model was read through `IDesignTimeModel`, the diff through `IMigrationsModelDiffer` (what `has-pending-model-changes` compares), the DDL through `IMigrationsSqlGenerator`, query SQL through `ToQueryString()`, and body sizes with `System.Text.Json` web defaults (the minimal-API serializer).

| # | Question | Result |
|---|---|---|
| O1 | Which indexes does EF add by convention for the new foreign keys? | **Exactly two:** `IX_ScheduleVersionServices_ServiceId` (FK to `Services`; the PK leads with `ScheduleVersionId`) and **`IX_ScheduleStopTimes_ServiceId_Position`** (FK to `PK_ServiceStops`; the PK leads with `ScheduleVersionId`). The FK `ScheduleVersionServices → ScheduleVersions` and the FK `ScheduleStopTimes → ScheduleVersionServices` are covered by PK prefixes, so no index for them. Both convention indexes are declared explicitly and named (P21). |
| O2 | With F-004's model in place, what does adding the F-005 entities emit? | **Exactly** `CreateTable` × 3 (`ScheduleVersions`, `ScheduleVersionServices`, `ScheduleStopTimes`) and `CreateIndex` × 4 (the two above, `UX_ScheduleVersions_Number`, `UX_ScheduleVersions_EffectiveFrom_Published`). Nothing against `Services`, `ServiceStops` or `network`. The reverse diff is exactly three `DropTable`s (children first); the `timetable` schema stays (F-004's `Timetable_CreateServices` owns it). |
| O3 | The filtered unique index and the queries that use it | DDL: `CREATE UNIQUE INDEX [UX_ScheduleVersions_EffectiveFrom_Published] ON [timetable].[ScheduleVersions] ([EffectiveFrom]) WHERE [Status] = N'Published';`. The timeline query emits the **literal** `WHERE [s].[Status] = N'Published'` (an enum constant with a string converter is inlined, not parameterised), so the optimizer can match the filtered index. |
| O4 | SQL of the withdrawal guard's read | `SELECT [s].[Id], [s].[EffectiveFrom], CASE WHEN EXISTS (SELECT 1 FROM [timetable].[ScheduleVersionServices] AS [s0] WHERE [s].[Id] = [s0].[ScheduleVersionId] AND [s0].[ServiceId] = @s) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS [Lists] FROM [timetable].[ScheduleVersions] AS [s] WHERE [s].[Status] = N'Published'` — a scan of the filtered index (it carries `Id` as the clustering key, so it covers) and one `PK_ScheduleVersionServices` seek per published version. |
| O5 | Number query | `SELECT TOP(@p) [s].[Number] FROM [timetable].[ScheduleVersions] AS [s] ORDER BY [s].[Number] DESC` — a backward seek on `UX_ScheduleVersions_Number`. |
| O6 | Largest valid bodies | §API changes, "Body limits and caps": **554,267 bytes** compact at the proposed caps, **1,153,793** indented; 443,667 / 923,293 for the realistic 200 × 40. Without a per-version total cap (250 × 200) it would be 2,739,517 / 5,699,043. |

### Verifications left to implementation, each with a step and a fallback

| # | VERIFY | Step | If it fails |
|---|---|---|---|
| V1 | Publish, discard and cancel each emit **one** `UPDATE [timetable].[ScheduleVersions] SET [Status] = @p0, [<one instant>] = @p1 WHERE [Id] = @p2 AND [Status] = @p3` and nothing against the child tables | 6 | Fix the mapping (the header is loaded without its children; P7). **Never widen the grant.** The handler tests run as `ycr_app`, so a stray `UPDATE` fails them first. |
| V2 | A create at the caps (250 services, 10,000 stop times) completes, and how long it holds the Timetable-wide lock (recorded in `progress.md`, no timing assertion) | 6 | If it holds the lock longer than 5 s on the CI runner, stop and report with the measurement; options (EF `MaxBatchSize`, a smaller cap) go to hein. Never a bulk-copy path that bypasses the model silently. |
| V3 | `has-pending-model-changes` is clean after each migration | 2–4 | Fix the model; never hand-edit a migration to match. |
| V4 | `ValidationFilter<T>` validates a query record bound with `[AsParameters]` (the in-force `date`), giving `400 Common.ValidationFailed` | 9 | Bind `string? date` and run the same validator in a filter on that endpoint; the response shape must be identical (asserted). Record which. |
| V5 | `/schedules/versions/in-force` is not shadowed by `/schedules/versions/{id:guid}` (and `in-force` is not a GUID) | 9 | `InForceRoute_IsNotShadowedByTheIdRoute` fails by name; fix the route order or constraint. |
| V6 | `sp_getapplock` on `timetable.ScheduleVersions` under `ycr_app` returns `0`/`1`, blocks a second transaction on the same resource, and neither blocks nor is blocked by a `timetable.ServiceCode:*` lock | 5 | `ApplicationCredential_CanTakeTheScheduleVersionsApplock` fails; fix the call. The resource is a parameter even though it is constant. |
| V7 | `TimetableTime?` with a value converter maps to `smallint NULL`, and a `null` stays `NULL` (converters are not applied to nulls) | 2 | `ScheduleModel_MapsTablesColumnsTypesAndKeys` fails; map the minutes as `short?` on the child and expose `TimetableTime?` through a computed property. |
| V8 | A second published row with the same `EffectiveFrom` is SQL error 2601 naming `UX_ScheduleVersions_EffectiveFrom_Published`, which the translator maps | 6 | `PublishScheduleVersion_WhenIndexViolatedBehindTheLock_Returns409EffectiveFromTaken` fails; fix the translator (Infrastructure only), never the index. |
| V9 | EF writes `Status` from its string converter in the `WHERE` of the transition update (the original value) | 6 | Same test as V1. |
| V10 | How the binder treats `position` given as `"1"` (web defaults read numbers from strings) or `1.5` (framework `400`, no `errorCode`, the F-003/F-004 malformed-JSON class) | 9 | Record in `progress.md`; T-042 owns framework `400`s. |

**ASSUMPTION:** none. Every business rule used is a spec rule (R1–R50) resting on hein's rulings in spec §0.10.

**OPEN QUESTION:** none new in `docs/19`. Q1–Q5 were engineering and spec-interpretation questions for hein, not Myanma Railways questions; all five are ruled (§Rulings on Q1–Q5). Q3's ruling amends the spec (Amendment 2) and is recorded under OQ60 in `docs/19`.

---

## Rulings on Q1–Q5 (hein, 2026-09-27)

Revision 1 asked hein five questions (Q1–Q5). hein ruled on all five on **2026-09-27** (recorded in the `TASKS.md` T-054 row) and approved the plan. Revision 2 applies the rulings; the questions as asked are in revision 1 (`23995fc`).

| # | Question (revision 1) | Ruling (hein, 2026-09-27) | Label | Applied in |
|---|---|---|---|---|
| **Q1** | A per-version total cap on stop times, next to the per-service and services caps. | **Accepted:** at most **10,000 stop times per version** (`400 Common.ValidationFailed`), with the **2 MiB** body limit, plus **250 services** per version and **200 stop times per service**. All four are REQUIRED CONTROLs. The size calculation stays in the plan (§API changes, "Body limits and caps"). | REQUIRED CONTROL (tech lead, hein, 2026-09-27; T-054 Q1; spec R41, E10) | P15; §Contracts validators; §Body limits and caps; §Security impact; `ScheduleVersionRequestLimitTests`; Risks R-5 |
| **Q2** | `GET /schedules/versions?status=` with an unknown value. | **`400 Common.ValidationFailed`.** (What counts as known is P17: exactly `Draft`, `Published`, `Discarded`, `Cancelled`, case-sensitive.) | ENGINEERING DECISION (tech lead, hein, 2026-09-27; T-054 Q2) | P13; P17; §Endpoint inventory; `List_WithUnknownStatus_Returns400ValidationFailed` |
| **Q3** | Amendment 1 at creation: refuse an empty draft whose start date is today when it is created? | **Yes — spec Amendment 2.** An empty draft whose `EffectiveFrom` is not later than today is refused **at creation too**, with the same `422 Timetable.EmptyScheduleVersionNotInFuture`; nothing is written. The publication check stays, for a draft that was valid at creation and whose start date has since become today. Spec R45, R50, SV55, §5, §6, §9 and Blocked behaviour updated; `docs/19` OQ60 gains the ruling. | BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-27; T-054 Q3), **not a Myanma Railways answer** | P3; P4 (new check 5); P6; §Domain changes; §Endpoint inventory; §OpenAPI, `.http`, smoke; §Test plan (SV55 rows); Risks R-10 |
| **Q4** | The F-004 row `POST /api/v1/schedules/versions` in `ServiceEndpointsTests.AbsentEndpoints_AreNotRouted`. | **Remove that row**; keep the other absent rows. | ENGINEERING DECISION (tech lead, hein, 2026-09-27; T-054 Q4; AGENTS.md rule 7 — removal approved, not made to pass a build) | Changed-tests item 17; §Counts; stage-8 F-004 spec note |
| **Q5** | The lock's resource name. | **`timetable.ScheduleVersions`.** | ENGINEERING DECISION (tech lead, hein, 2026-09-27; T-054 Q5; ADR-0026 item 3; spec R46) | P8; §The Timetable-wide lock |

---

## Decisions (P1–P26) — made by this plan, approved with it (hein, 2026-09-27)

ENGINEERING DECISION (plan, T-054) unless stated. Each cites the rule or precedent it implements.

| # | Decision | Why | Rejected alternative |
|---|---|---|---|
| **P1** | **Aggregate shape.** `ScheduleVersion` (root, `AggregateRoot`) owns `ScheduleVersionService` (`ScheduleVersionId`, `ServiceId`, `StopTimes`), which owns `ScheduleStopTime` (`ScheduleVersionId`, `ServiceId`, `Position`, `Arrival`, `Departure`). Plain `sealed class`es with internal factories, exposed as `IReadOnlyList<…>` over private lists; `HasMany(...).WithOne().HasForeignKey(...).OnDelete(NoAction)`, field access. `ITimetableDbContext` gains `DbSet<ScheduleVersion> ScheduleVersions`; no `DbSet` for the children. | Spec E2; F-004 P1 (`Service`/`ServiceStop`). | `OwnsMany` (would own the key and hide the FK to `ServiceStops`) |
| **P2** | **`TimetableTime`** (ADR-0027 item 1; the name ADR-0027 left open): `sealed record` in `YCR.Domain.Timetable`, `short Minutes` in `0..1439`. `Parse(string? text)` accepts exactly `^([01][0-9]|2[0-3]):[0-5][0-9]$` (ordinal, **`[0-9]`, not `\d`**, which in .NET also matches Myanmar and other Unicode digits) and returns `Timetable.InvalidTimetableTime` otherwise; `ToString()` is `HH:mm`; `internal FromMinutes(short)` for EF throws outside `0..1439`. ADR-0027 item 4's "time → instant" function is **not** built: nothing in F-005 consumes an instant (AGENTS.md rule 11). | R11, R15, R16; ADR-0027 items 1–3. | `TimeOnly` (ADR-0027 rejected it); `\d` in the regex |
| **P3** | **Creation is two domain calls, split by what they need.** `ScheduleVersionInput.Parse(nameEn, nameMy, effectiveFrom, today, services)` decides the checks that need no stored data — name (R7), every time's format (R16), start ≥ today (R31), and an empty version's start > today (R50, Amendment 2; Q3) — and returns parsed input. `ScheduleVersion.CreateDraft(id, number, input, facts, nowUtc)` decides the per-service checks (R17, R10, R12, R15) from `ScheduleServiceFacts(ServiceId, Code, EffectiveFrom, EffectiveTo, StopCount)` the handler loads **under the lock**, then builds the draft. | AGENTS.md rule 3; F-004 P4 (facts given to the domain). The split lets the pure checks run before the transaction while the R17 read is under the lock (P6). | One call under the lock (holds the lock across parsing 10,000 times) |
| **P4** | **The exact check order (R45)**, first failure returned: (1) request validation, `400 Common.ValidationFailed` (§Contracts); (2) name, `400 Timetable.InvalidScheduleVersionName`; (3) every time's format, in request order (service order, then stop-time order; arrival before departure), `400 Timetable.InvalidTimetableTime`; (4) `EffectiveFrom` ≥ today, `422 Timetable.ScheduleVersionEffectiveFromInPast`; (5) if `services` is empty, `EffectiveFrom` > today, `422 Timetable.EmptyScheduleVersionNotInFuture` (R50, Amendment 2; Q3 — after (4), as at publication, so a past start date is still reported as in the past); then **for each service in request order**, completing one service before the next: (6) exists, `422 ScheduleServiceNotFound`; (7) not listed earlier in the request, `422 ScheduleServiceRepeated` (reported on the second occurrence); (8) effective and not withdrawn on the start date, `422 ScheduleServiceNotEffective`; (9) every entry's position ≤ the service's stop count, `422 ScheduleStopNotInService` (first offending entry); (10) positions `1..k` each given exactly once, `422 ScheduleStopTimesIncomplete`; (11) for stops `1..k` in order: a time where none is allowed (arrival at stop 1, departure at stop k), `422 ScheduleStopTimeUnexpected`, then a required time missing, `422 ScheduleStopTimesIncomplete`; (12) for stops in order, arrival ≤ departure, `422 ScheduleDwellNegative`; (13) for stops `2..k` in order, arrival > previous departure, `422 ScheduleTimesNotIncreasing`. Check (5) and the per-service checks never both apply (one needs no services, the others at least one). | R45 fixes the phases (and, with Amendment 2, the empty-version check after the start date) and leaves the exact order to PLAN (F-004 R37 pattern). Phases (9)–(13) follow R45's "stop positions, times per stop, dwell, increasing". A later stop can never have an earlier clock time, so a journey across midnight fails (13) (R15, SV12). | Interleaving the checks stop by stop (the spec's phases would no longer be phases) |
| **P5** | **Numbers.** `ScheduleVersionNumbering.Next(int? highestAssigned)` = `(highestAssigned ?? 0) + 1`. The handler reads the highest number under the lock (O5) and passes it in. `UX_ScheduleVersions_Number` is the backstop; a violation (only possible from a writer that bypasses the lock) is not caught: the opaque `500`, nothing written (SV50 test). | R7 (ruling "Number assignment"); ADR-0026 items 1, 7. | `IDENTITY` or a sequence (gaps on rollback; R7 requires none) |
| **P6** | **`CreateScheduleVersionHandler` order:** `ScheduleVersionInput.Parse` (checks 2–5) → **begin transaction → take the Timetable-wide lock** → load `ScheduleServiceFacts` for the requested ids (one query) → read the highest number → `ScheduleVersion.CreateDraft` (checks 6–13) → add + audit → one `SaveChangesAsync` → commit. | R46 ("taken before the reads that decide"; create takes it for the number, and its R17 read is then serialised with withdrawals too); ADR-0026 item 4. No other-module read is needed (R43). | R17 read before the lock (it would be the only unserialised decision among the R46 operations) |
| **P7** | **Transitions.** `ScheduleVersion.Publish(today, listedServices, nowUtc)`: not `Draft` → `ScheduleVersionNotDraft` (R30); `EffectiveFrom < today` → `ScheduleVersionEffectiveFromInPast` (R31); `listedServices` empty and `EffectiveFrom ≤ today` → **`EmptyScheduleVersionNotInFuture`** (R50, Amendment 1); any listed service not effective on the start date, first in ascending `ServiceId` order → `ScheduleServiceNotEffective` (R17); else `Status = Published`, `PublishedAtUtc = nowUtc`. `Discard(nowUtc)`: not `Draft` → `ScheduleVersionNotDraft` (R28). `Cancel(today, nowUtc)`: not `Published` → `ScheduleVersionNotPublished`; `EffectiveFrom ≤ today` → `ScheduleVersionAlreadyEffective` (R34); else `Cancelled`, `CancelledAtUtc = nowUtc` (`PublishedAtUtc` kept). A refused call changes nothing. Handlers: begin transaction → lock → load the header **tracked, without its children** (`404 ScheduleVersionNotFound`) → (publish only) load `ScheduleServiceFacts` for the listed services → domain → audit → one save (→ `409 ScheduleVersionEffectiveFromTaken` if the save hits `UX_ScheduleVersions_EffectiveFrom_Published`, `409 ScheduleVersionChangedConcurrently` on `DbUpdateConcurrencyException`) → commit. **Publish order:** `404` → `422 NotDraft` → `422 EffectiveFromInPast` → `422 EmptyScheduleVersionNotInFuture` → `422 ScheduleServiceNotEffective` → `409 EffectiveFromTaken` → `409 ChangedConcurrently`. **Cancel:** `404` → `422 NotPublished` → `422 AlreadyEffective` → `409`. **Discard:** `404` → `422 NotDraft` → `409`. | R22 is a unique-index rule, so the index decides it (ADR-0026 item 1), not a read. `Status` is the only concurrency token (R47), so each transition is one guarded `UPDATE` of exactly the granted columns (V1, V9). Publish takes facts rather than the child rows, so no transition ever depends on children that were not loaded. | Loading the children for publish (10,000 rows for one status change); a pre-read for R22 (a second home for the index rule) |
| **P8** | **The Timetable-wide lock: `sp_getapplock` on `timetable.ScheduleVersions`** (Q5), exclusive, transaction-owned, 30 s, parameter resource, `THROW 50036` on a negative result; `IScheduleVersionsLock.AcquireAsync(CancellationToken)` in `YCR.Application/Timetable/Abstractions`, `SqlServerScheduleVersionsLock` in `YCR.Infrastructure/Timetable`, refusing to run without an open transaction. | R46; ADR-0026 items 2–6; F-004 P9 shape. | A second keyed lock per service (R46 rules a single Timetable-wide lock) |
| **P9** | **Lock order: the service-code lock, then the Timetable-wide lock.** Only `WithdrawServiceHandler` takes both; it takes the code lock first. No holder of the Timetable-wide lock ever requests a service-code lock. §The Timetable-wide lock proves this deadlock-free. | Spec §0.12 leaves the order to PLAN. Code-first keeps F-004's handler shape (its code lock and its reload unchanged) and holds the global lock only for the version read and the save. | Timetable-wide first (holds the global lock across F-004's reload and its Network reads); the global lock alone (loses F-004's per-code serialisation with create, R35) |
| **P10** | **The withdrawal guard lives in `Service.Withdraw`.** New signature `Withdraw(DateOnly withdrawFrom, DateOnly today, DateTimeOffset nowUtc, ServiceScheduleCoverage coverage)`: F-004's checks first, unchanged (R21: date in past, does not shorten); then `coverage.AppliesOnOrAfter(withdrawFrom)` → `ServiceInPublishedScheduleVersion` (R19); only then mutate. `ServiceScheduleCoverage` = a `PublishedTimeline` plus the ids of the published versions that list the service; `ServiceScheduleCoverage.None` = no published version. The three-argument signature is **replaced**, not overloaded, so no code path can withdraw without the coverage (and `Service_ExposesNoMutatorOtherThanWithdraw` stays exact). | R19; spec §0.12 check order ("the new check, last"); AGENTS.md rule 3 (rule in the domain) and rule 5 (a refused call must not leave a mutated tracked entity behind, which checking in the handler after `Withdraw` would). | Checking R19 in the handler after `Withdraw` (mutates, then refuses); an overload (a bypass, and fails the reflection test) |
| **P11** | **`PublishedTimeline`** (`YCR.Domain.Timetable`) is the one home of "in force" and "applies": built from the published versions' `(Id, EffectiveFrom)`; `InForceOn(DateOnly date)` → the id with the latest `EffectiveFrom ≤ date`, or none (R21); `AppliesOnOrAfter(Guid versionId, DateOnly date)` → true when the version has no successor or its successor's `EffectiveFrom` > `date` (R19's "applies on some date ≥ D"). The in-force read and the withdrawal guard both load the published list (O3, O4) and ask it. Duplicate start dates in the input throw (the index makes them impossible). | F-004 P13 precedent: the rule is tested without a database and has one home. Published versions are few (one per timetable change), so loading `(Id, EffectiveFrom)` for all of them is a covered scan of a small filtered index. | A `TOP(1)` seek in SQL for R21 plus a separate rule for R19 (two homes for "which version applies"; recorded under Risks as the fallback if the list ever grows large) |
| **P12** | **`runsOnDate`:** `ServiceRunningDay.RunsOn(DateOnly date, DateOnly effectiveFrom, DateOnly? effectiveTo, OperatingDays days)` = `effectiveFrom ≤ date ≤ (effectiveTo ?? max)` and `days.Includes(date.DayOfWeek)`; `OperatingDays` gains `Includes(DayOfWeek)`. A never-running service (`EffectiveTo < EffectiveFrom`) never runs. Holidays are not considered (R35, OQ47). | R18, R48. A static rather than a `Service` method: the reflection test pins `Service`'s methods, and the read projects rows, never the aggregate. | `Service.RunsOn` |
| **P13** | **Reads project and never materialise the aggregate** (`AsNoTracking`). Get: header by PK, then its listed services joined to `Services` (current code, names, direction, stop count, period, `neverRuns`). Service times: the version (404), the entry (`404 ScheduleServiceNotInVersion`), its stop times with `ServiceStops.StationId` by position, then `INetworkReader.GetStationsAsync` for station code and names (R43). List: `ORDER BY Number`, SQL `COUNT`, `OFFSET/FETCH`, `serviceCount` subquery, optional exact `status` (Q2). In force: P11 then the listed services with their seven `RunsOn…` bits and period, `runsOnDate` by P12. Listed services in every response are ordered by service `Code`, then `ServiceId`. DTOs are separate from API responses; status and times are strings in DTOs (`HH:mm`, `null`). | F-004 P17; R43; spec §6. `Number` is unique, so it is a complete order. | Materialising `ScheduleVersion` with 10,000 stop times for a header read |
| **P14** | **Request fields that are not plain strings travel as strings**, as F-004 P6: `effectiveFrom`, `serviceId`, `arrival`, `departure` — validated by the validator (`effectiveFrom` exactly `yyyy-MM-dd`, `serviceId` a `D` GUID) or by the domain (`arrival`/`departure` → `400 Timetable.InvalidTimetableTime`, R16, because the spec gives the time format its own code). `position` is `int?` (a JSON number). **Names are not validated by the validator**: a missing, blank or 101-character name is `400 Timetable.InvalidScheduleVersionName` from the domain (SV18 says so explicitly, unlike F-004, whose validator sends a blank code to `Common.ValidationFailed`). | SV8, SV11, SV18; R16. | Typed `DateOnly`/`Guid` properties (framework `400` without `errorCode`) |
| **P15** | **Caps and body limit** (REQUIRED CONTROL, R41; §API changes): at most **250 services** per version, **200 stop times per service** (F-004's stop cap), **10,000 stop times per version** (Q1); `POST /schedules/versions` body limit **2 MiB** (2,097,152 bytes) as `RequestSizeLimitAttribute` endpoint metadata. `publish`, `cancel` and `discard` take **no body**, are not body-carrying endpoints in R41's sense, and keep the server default (as F-003's and F-004's body-less endpoints; T-042). | R41 and SV47, from the measured sizes (O6). | 8 MiB (Q1 (b)) |
| **P16** | **The in-force endpoint** binds `?date=` as a string in a query record (`[AsParameters]`), validated to exactly `yyyy-MM-dd` → otherwise `400 Common.ValidationFailed` (SV48; V4). Its literal route `/schedules/versions/in-force` is mapped before `/{id:guid}`; `in-force` cannot match the GUID constraint either way (V5). | SV48; spec §6 route note. | `DateOnly? date` binding (framework `400` without `errorCode`) |
| **P17** | **List filter:** `status` optional, exactly one of the four names; anything else → `400 Common.ValidationFailed` (Q2). `page`/`pageSize` reuse `Paging` → `400 Timetable.InvalidPageRequest`. | SV3, SV48; F-004 list. | — |
| **P18** | **Audit** (§Audit): `TimetableAuditActions` + `ScheduleVersionCreated`, `…Published`, `…Discarded`, `…Cancelled`; `TimetableAuditSubjects.ScheduleVersion = "Timetable.ScheduleVersion"`; snapshot records `ScheduleVersionAuditSnapshot` (header) and `ScheduleVersionCreatedAuditSnapshot` (header + services + `stopTimesSha256`); `ScheduleStopTimesDigest` fixes the canonical form. | Spec §8, E13; ADR-0021 rule 3. | Full times in `AfterJson` (E13 rejected it) |
| **P19** | **Errors.** `TimetableErrors` gains **21** codes (§Error code → HTTP). `TimetableConstraints.ScheduleVersionEffectiveFromPublishedUniqueIndex = "UX_ScheduleVersions_EffectiveFrom_Published"` (new class, `NetworkConstraints` pattern), pinned to the model by `ScheduleModel_ConstraintNames_MatchTheModel`. | `docs/20` §2; R42. | Matching on a literal in the handler |
| **P20** | **Three migrations, kept separate:** `<ts>_Timetable_CreateScheduleVersions` → `<ts>_Identity_SeedSchedulePermissionGrants` → `<ts>_Security_TimetableScheduleGrants`. | Spec §7 names; F-002/F-003/F-004 separation of review concerns and `Down()`s. | One migration |
| **P21** | **Indexes declared explicitly:** `UX_ScheduleVersions_Number`, `UX_ScheduleVersions_EffectiveFrom_Published` (filtered), `IX_ScheduleVersionServices_ServiceId`, **`IX_ScheduleStopTimes_ServiceId_Position`** (the spec's "covering index for `(ServiceId, Position)`, decided at PLAN"), all with `HasDatabaseName`. A model test pins the exact set per table (O1). | The F-003/F-004 lesson: no index appears by accident. `IX_ScheduleStopTimes_ServiceId_Position` serves no F-005 query; it is kept because EF adds it for the FK to `PK_ServiceStops` unless another index leads with those columns, and a key-leading alternative (`PK` on `(ServiceId, Position, ScheduleVersionId)`) would lose the per-version clustering every read uses and add a convention index for the other FK instead. Its cost is one extra row per stop time on insert. | Reordering `PK_ScheduleStopTimes` |
| **P22** | **Grants exactly spec §7** (§DB changes 3). | E14; S51. | — |
| **P23** | **Seed exactly `docs/10` §Schedule permission grants** (ten rows; 34 → 44). | OQ59 ruling; E5. | — |
| **P24** | **Transactions.** One explicit transaction per create, publish, discard, cancel and withdrawal, holding the lock(s), the deciding reads, one `SaveChangesAsync` and the commit; default isolation; disposing without commit rolls back and releases every applock. | ADR-0026 item 4; F-004 P10. | — |
| **P25** | **No domain event**, as F-004 P21: `docs/04` proposes `SchedulePublished`, but nothing consumes one and F-005 dispatches nothing. | YAGNI; R43 (no consumer module). | `SchedulePublished` event |
| **P26** | **No new packages.** | Everything used is referenced (`System.Security.Cryptography.SHA256` is in the BCL). | — |

---

## Affected modules and files

Modules touched: **`Timetable`** (new aggregate, reads, lock, and the F-004 withdrawal change), **`Identity`** (a data-only seed migration), solution-wide `Common` (two permission constants). `Network` gains nothing (the existing `INetworkReader.GetStationsAsync` is called). `Audit` gets new rows, not new code.

### `src/YCR.Domain/Timetable/`

| File | New / Changed | Why |
|---|---|---|
| `ScheduleVersion.cs` | New | Aggregate root: `CreateDraft` (P3), `Publish`, `Discard`, `Cancel` (P7); no other public method (R27, R32) |
| `ScheduleVersionService.cs`, `ScheduleStopTime.cs` | New | Children (P1); internal factories; no navigation to `Service` (same module, FK only) |
| `ScheduleVersionStatus.cs` | New | `Draft`, `Published`, `Discarded`, `Cancelled`; stored as text (`CK_ScheduleVersions_Status`), never renamed |
| `TimetableTime.cs` | New | P2 (ADR-0027) |
| `ScheduleVersionInput.cs` | New | `ScheduleVersionInput`, `ScheduleServiceInput`, `ScheduleStopTimeInput`; `Parse` (P3, checks 2–5) |
| `ScheduleServiceFacts.cs` | New | What a version needs to know about a listed service (P3), given by the handler |
| `ScheduleVersionNumbering.cs` | New | P5 |
| `PublishedTimeline.cs` | New | P11 (R19, R21) |
| `ServiceScheduleCoverage.cs` | New | P10 |
| `ServiceRunningDay.cs` | New | P12 (R18, R48) |
| `OperatingDays.cs` | Changed | + `Includes(DayOfWeek)` (P12); nothing else |
| `Service.cs` | **Changed (F-004)** | `Withdraw` gains `ServiceScheduleCoverage coverage` and the R19 check after F-004's checks (P10); XML comment cites F-005 R19 and spec §0.12 |
| `TimetableErrors.cs` | Changed | + 21 codes (P19) |

### `src/YCR.Application/`

| File | New / Changed | Why |
|---|---|---|
| `Common/Authorization/Permissions.cs` | Changed | `SchedulesManage = "schedules.manage"`, `SchedulesRead = "schedules.read"`, with the OQ59 provisional-ruling comment |
| `Timetable/ITimetableDbContext.cs` | Changed | + `DbSet<ScheduleVersion> ScheduleVersions`; comment: the explicit transaction is also used by the schedule handlers (ADR-0026) |
| `Timetable/Abstractions/IScheduleVersionsLock.cs` | New | P8 |
| `Timetable/TimetableConstraints.cs` | New | P19 |
| `Timetable/TimetableAuditActions.cs`, `TimetableAuditSubjects.cs` | Changed | + four actions, + `ScheduleVersion` (P18) |
| `Timetable/ScheduleVersionAuditSnapshot.cs`, `Timetable/ScheduleStopTimesDigest.cs` | New | P18 |
| `Timetable/ScheduleReadMapping.cs` | New | Status names and `HH:mm` formatting for every read DTO, in one place |
| `Timetable/CreateScheduleVersion/CreateScheduleVersionCommand.cs`, `CreateScheduleVersionHandler.cs` | New | P3–P6 |
| `Timetable/PublishScheduleVersion/…Command.cs`, `…Handler.cs` | New | P7 |
| `Timetable/DiscardScheduleVersion/…Command.cs`, `…Handler.cs` | New | P7 |
| `Timetable/CancelScheduleVersion/…Command.cs`, `…Handler.cs` | New | P7 |
| `Timetable/GetScheduleVersion/GetScheduleVersionQuery.cs`, `…Handler.cs`, `ScheduleVersionDto.cs` | New | P13 |
| `Timetable/GetScheduleServiceTimes/…Query.cs`, `…Handler.cs`, `ScheduleServiceTimesDto.cs` | New | P13 |
| `Timetable/ListScheduleVersions/…Query.cs`, `…Handler.cs`, `ScheduleVersionSummaryDto.cs` | New | P13, P17 |
| `Timetable/GetScheduleVersionInForce/…Query.cs`, `…Handler.cs`, `ScheduleVersionInForceDto.cs` | New | P11–P13 |
| `Timetable/WithdrawService/WithdrawServiceHandler.cs` | **Changed (F-004)** | Takes `IScheduleVersionsLock` after the code lock; loads the coverage; passes it to `Withdraw` (§The F-004 withdrawal change) |
| `DependencyInjection.cs` | Unchanged | Handlers by scanning |

### `src/YCR.Infrastructure/`

| File | New / Changed | Why |
|---|---|---|
| `Persistence/Configurations/Timetable/ScheduleVersionConfiguration.cs` | New | Table, columns, the seven check constraints, both unique indexes, `Status` token, owned `BilingualName`, `HasMany` entries |
| `Persistence/Configurations/Timetable/ScheduleVersionServiceConfiguration.cs` | New | Composite PK, `IX_ScheduleVersionServices_ServiceId`, FK to `Services` (`NO ACTION`, no navigation), `HasMany` stop times on the composite FK |
| `Persistence/Configurations/Timetable/ScheduleStopTimeConfiguration.cs` | New | Composite PK, three checks, `IX_ScheduleStopTimes_ServiceId_Position`, FK to `PK_ServiceStops` (`NO ACTION`, no navigation), `TimetableTime?` converters (V7) |
| `Persistence/YcrDbContext.cs` | Changed | `DbSet<ScheduleVersion> ScheduleVersions` |
| `Persistence/Migrations/<ts>_Timetable_CreateScheduleVersions.cs` (+ Designer) | New | §DB changes 1 |
| `Persistence/Migrations/<ts>_Identity_SeedSchedulePermissionGrants.cs` (+ Designer) | New | §DB changes 2 |
| `Persistence/Migrations/<ts>_Security_TimetableScheduleGrants.cs` (+ Designer) | New | §DB changes 3 |
| `Persistence/Migrations/YcrDbContextModelSnapshot.cs` | Changed | Generated |
| `Timetable/SqlServerScheduleVersionsLock.cs` | New | P8 |
| `DependencyInjection.cs` | Changed | `IScheduleVersionsLock` → `SqlServerScheduleVersionsLock` (scoped) |

### `src/YCR.Api/`

| File | New / Changed | Why |
|---|---|---|
| `Contracts/Timetable/ScheduleVersionContracts.cs` | New | Requests (+ validators, caps), responses (§Contracts) |
| `Endpoints/Timetable/ScheduleVersionEndpoints.cs` | New | `MapScheduleVersionEndpoints`, tag `ScheduleVersions`, the body-limit constant |
| `Endpoints/Timetable/ServiceEndpoints.cs` | **Changed (F-004)** | XML "deliberately absent" no longer lists `/schedules/versions*`; the withdraw summary names the new `422` (its `.ProducesProblem(422)` already exists) |
| `Program.cs` | Changed | `api.MapScheduleVersionEndpoints();` |
| `YCR.Api.http` | Changed | "Schedule versions (F-005)" section; the header lists F-005 |

### Other

| File | New / Changed | Why |
|---|---|---|
| `.github/workflows/ci.yml` | Changed | `api-smoke` schedule checks (§OpenAPI, `.http`, smoke) |
| Tests | New / Changed | §Test plan |

**Not changed:** `CreateServiceHandler` (service creation does not take the Timetable-wide lock, R46), `GetService`/`ListServices`, every `Services`/`ServiceStops` column, constraint, index and grant, every `network` object, `INetworkReader`, the Worker.

---

## Domain changes

### `ScheduleVersion` (aggregate root)

State: `Id`, `Number` (`int`), `Name` (`BilingualName`), `EffectiveFrom` (`DateOnly`), `Status` (`ScheduleVersionStatus`), `CreatedAtUtc`, `PublishedAtUtc?`, `DiscardedAtUtc?`, `CancelledAtUtc?`, `Services` (`IReadOnlyList<ScheduleVersionService>`, each with `StopTimes` in position order).

| Method | Rules | Errors |
|---|---|---|
| `ScheduleVersionInput.Parse(nameEn, nameMy, effectiveFrom, today, services)` | R7 name via `BilingualName.Create(…, TimetableErrors.InvalidScheduleVersionName)`; R16 every time via `TimetableTime.Parse` in request order; R31 `effectiveFrom ≥ today`; R50 (Amendment 2) no services → `effectiveFrom > today` | `InvalidScheduleVersionName`, `InvalidTimetableTime` (Validation); `ScheduleVersionEffectiveFromInPast`, `EmptyScheduleVersionNotInFuture` (BusinessRule) |
| `ScheduleVersion.CreateDraft(id, number, input, facts, nowUtc)` | Non-UTC `nowUtc` throws; `number < 1` throws. Per service in request order, P4 checks 6–13 (R17, R10, R12, R15); on success `Draft`, instants null, entries in request order, stop times in position order | `ScheduleServiceNotFound`, `ScheduleServiceRepeated`, `ScheduleServiceNotEffective`, `ScheduleStopNotInService`, `ScheduleStopTimesIncomplete`, `ScheduleStopTimeUnexpected`, `ScheduleDwellNegative`, `ScheduleTimesNotIncreasing` (BusinessRule; each message names the service id and, where relevant, the position — only the caller's input) |
| `Publish(today, listedServices, nowUtc)` | P7 (R30, R31, **R50**, R17) | `ScheduleVersionNotDraft`, `ScheduleVersionEffectiveFromInPast`, `EmptyScheduleVersionNotInFuture`, `ScheduleServiceNotEffective` |
| `Discard(nowUtc)` | P7 (R28) | `ScheduleVersionNotDraft` |
| `Cancel(today, nowUtc)` | P7 (R34) | `ScheduleVersionNotPublished`, `ScheduleVersionAlreadyEffective` |

**R17's predicate**, one home (`ScheduleServiceFacts.IsEffectiveOn(DateOnly start)`): `EffectiveFrom ≤ start` and (`EffectiveTo` is null or `start ≤ EffectiveTo`). A never-running service (`EffectiveTo < EffectiveFrom`) fails it for every date. A withdrawn service fails it from its withdrawal date on (its `EffectiveTo` is the day before).

**R10 per stop** (k = the service's stop count, a full circuit's closing stop is position k, F-004 R14): stop 1 → departure required, arrival forbidden; stop k → arrival required, departure forbidden; 1 < i < k → both required. **R12:** at each stop with both, `arrival ≤ departure`; for i ≥ 2, `arrival(i) > departure(i−1)`. **R13** (no passing times) and **R14** (one set of times) are structural: a stop time addresses a *stop position* of the service, and there is one entry per service.

**Lifecycle (§5):**

```text
            CreateDraft
  (none) ──────────────▶ Draft ──Publish──▶ Published ──Cancel (EffectiveFrom > today)──▶ Cancelled
                           │
                           └──Discard──▶ Discarded
```

`Discarded` and `Cancelled` are final; `Published` → `Cancelled` is the only change to a published version (R32). **Immutability (R27, R32)** is structural: private setters, no public method other than the four above (`ScheduleVersion_ExposesNoMutatorOtherThanItsTransitions`), children read-only; the grants enforce it in the database (S51).

### `PublishedTimeline` and `ServiceScheduleCoverage` (R19, R21)

`PublishedTimeline.From(IEnumerable<(Guid Id, DateOnly EffectiveFrom)>)` sorts by start date. `InForceOn(d)` → the version with the greatest `EffectiveFrom ≤ d`, or none. `AppliesOnOrAfter(v, d)` → `successor(v)` is none or `successor(v).EffectiveFrom > d`. `ServiceScheduleCoverage(PublishedTimeline, IReadOnlySet<Guid> listingVersionIds).AppliesOnOrAfter(d)` → any listing version `v` with `AppliesOnOrAfter(v, d)`. Only published versions are ever given to it (the query filters `Status = N'Published'`), so drafts, discarded and cancelled versions never refuse a withdrawal (R19, SV38) and cancelled versions never apply (R34).

Worked through SV37: V1 from 2026-10-05 lists S1, V2 from 2027-01-01 does not. Withdraw from 2027-01-01: V1's successor V2 starts 2027-01-01, not after D → V1 does not apply on or after D; V2 does not list S1 → allowed. Withdraw from 2026-12-31: V2 starts after D → V1 applies on D → refused.

### Domain tests (`YCR.Domain.Tests/Timetable/`)

Listed in §Test plan. Every rule above has at least one, including the R45 precedence theory and the lifecycle reflection test.

---

## The F-004 withdrawal change (R19, spec §0.12)

`WithdrawServiceHandler`, new order (changes in **bold**):

1. Read the code by id without a lock → unknown → `404 Timetable.ServiceNotFound` (unchanged).
2. Begin the transaction; take `timetable.ServiceCode:<code>` (unchanged).
3. Reload the service tracked with its stops; read route and station codes through `INetworkReader`; take the `before` snapshot (unchanged).
4. **Take `timetable.ScheduleVersions`** (P9).
5. **Load the coverage:** the published versions' `(Id, EffectiveFrom, ListsService)` (O4) → `ServiceScheduleCoverage`.
6. `service.Withdraw(withdrawFrom, today, nowUtc, coverage)`: `422 WithdrawalDateInPast` → `422 WithdrawalDoesNotShorten` → **`422 ServiceInPublishedScheduleVersion`** (R19) → mutate.
7. Audit, one save (`409 ServiceChangedConcurrently` on the token, unchanged), commit.

The endpoint, its body, its limit and every other code are unchanged. A refused withdrawal writes nothing and records no event (the audit call is after the decision). **New error:** `TimetableErrors.ServiceInPublishedScheduleVersion` (`422`, BusinessRule, message names only the service id). **Deliberate non-change:** the Network reads stay inside the transaction under the code lock as F-004 wrote them (ADR-0026 item 4 would put them before; moving them is outside this feature's scope, AGENTS.md rule 11); the Timetable-wide lock is taken after them, so it is held only for step 5 and the save.

**Existing F-004 tests:** the seven `ServiceTests` withdrawal tests pass `ServiceScheduleCoverage.None` (call sites only, assertions unchanged; items 19–25). Every `WithdrawServiceHandlerTests` case stays valid unchanged: none publishes a version, so the coverage is empty; the forced S29/S37 tests gate the code lock, and the Timetable-wide lock is then free; `WithdrawServiceSql_…` still sees exactly two `UPDATE`s and an `sp_getapplock` (now two).

---

## The Timetable-wide lock (R46, ADR-0026)

### Resource and scope

```sql
DECLARE @result int;
EXEC @result = sp_getapplock
    @Resource    = @resource,          -- N'timetable.ScheduleVersions', passed as a parameter
    @LockMode    = N'Exclusive',
    @LockOwner   = N'Transaction',
    @LockTimeout = 30000;
IF @result < 0
    THROW 50036, N'Could not acquire the schedule-versions lock (R46).', 1;
```

**Scope:** the whole set of versions and the services they list — the smallest key covering R19 (withdrawal vs every version that lists the service), R22 re-checked with R17 at publish, and the number (R7) (ADR-0026 item 2: a whole-set lock, like `identity.SystemAdministrators`). It is a set invariant one unique index cannot enforce ("no published version listing S applies on a date ≥ D" compares a service's period with other rows' dates), so ADR-0026 item 1 allows it. R22 alone would be the index's (and is), which is why publish still maps the index violation rather than pre-reading.

### Who takes it

| Operation | Service-code lock | Timetable-wide lock | Taken before |
|---|---|---|---|
| `CreateScheduleVersion` | — | ✓ | the service-facts read and the number read (P6) |
| `PublishScheduleVersion` | — | ✓ | the header and listed-service reads (P7) |
| `DiscardScheduleVersion` | — | ✓ | the header read |
| `CancelScheduleVersion` | — | ✓ | the header read |
| `WithdrawService` (F-004) | ✓ first | ✓ second | the coverage read (step 5) |
| `CreateService` (F-004) | ✓ | — (R46: a new service is listed by no version) | — |
| Every read (get, list, times, in force) | — | — (§5: reads do not take it) | — |

### Ordering proof (deadlock freedom)

Define the resource order **every `timetable.ServiceCode:*` lock < `timetable.ScheduleVersions`**.

1. **Applocks are always requested in ascending order.** Five operations take one applock; `WithdrawService` takes a code lock, then the Timetable-wide lock (P9). No operation takes two code locks, and no operation holding the Timetable-wide lock ever requests a code lock (table above; asserted by `WithdrawService_TakesTheServiceCodeLockBeforeTheScheduleLock` and by the fact that no schedule handler depends on `IServiceCodeLock`, which `ScheduleHandlers_DoNotDependOnTheServiceCodeLock` checks by reflection over constructor parameters). A wait-for cycle among applocks would need some transaction to hold a higher resource while waiting for a lower one; none does.
2. **Row locks cannot close a cycle.** Every `X` row lock on `timetable` and `audit` rows is taken by a transaction's single `SaveChangesAsync`, which runs **after** it holds every applock it will ever take, and it then only commits. So a transaction that is waiting for an applock holds no `X` row lock on these tables, and a transaction that holds `X` row locks never waits for an applock. Under `READ COMMITTED` the shared locks of the deciding reads are released at the end of each statement (under `READ_COMMITTED_SNAPSHOT` reads take none), so a reader waiting on an `X` lock waits for a transaction that is past all its applock requests and will finish. The only cross-table case is a foreign-key check (a stop-time insert checks `ServiceStops`, an entry insert checks `Services`): `WithdrawService` updates only `EffectiveTo` and `WithdrawnAtUtc`, never a key, and its update runs while it holds the Timetable-wide lock, so no schedule writer can be inserting at the same moment; `CreateService`'s new rows are listed by no version.
3. **Therefore the wait-for graph is acyclic.** The only waits are (a) for a higher-or-equal applock held by a transaction that will not request a lower one, or (b) for a row lock held by a transaction that requests no further lock. A timeout (30 s) is an opaque `500` (ADR-0026 item 6), never a partial write.

What the order buys: publish never needs a code lock, so a withdrawal holding a code lock and waiting for the Timetable-wide lock simply waits for the publish to commit.

### Real-SQL tests (as `ycr_app`)

- **Blocking and scope:** `DatabasePrivilegeTests.ApplicationCredential_CanTakeTheScheduleVersionsApplock` — no grant needed; refuses without a transaction; a second transaction on `timetable.ScheduleVersions` waits (still waiting after 500 ms, proceeds after the first commits); a transaction holding `timetable.ServiceCode:SV1` does not block it and is not blocked by it (V6).
- **Forced orders, deterministic** (the F-004 `GatedServiceCodeLock` technique): `GatedScheduleVersionsLock`, a test decorator over the real `IScheduleVersionsLock`, signals "acquired" and waits on a gate. The test starts operation 1, waits for "acquired", starts operation 2, asserts after 500 ms that operation 2 has **not** acquired (it is blocked by the real SQL lock), then opens the gate. **Mutation check for stage 6:** remove `AcquireAsync` from either handler and the "still waiting" assertion fails.
  - **SV40**, both orders: `PublishAndWithdraw_PublishFirst_WithdrawReturns422InPublishedVersion`, `PublishAndWithdraw_WithdrawFirst_PublishReturns422NotEffective`; in each, afterwards no published version lists a service withdrawn while that version applies.
  - **SV41**, both orders: `CancelAndWithdraw_CancelFirst_WithdrawReturns422InPublishedVersion`, `CancelAndWithdraw_WithdrawFirst_BothSucceedAndServiceStaysWithdrawn` (R49).
  - **SV42**: `PublishAndDiscard_SameDraft_ForcedBothOrders_OneSucceedsOtherNotDraft` (theory over the order).
- **No deadlock, withdraw vs publish:** `PublishAndWithdraw_InParallelRepeatedly_NeverDeadlockAndEndInASerialOutcome` — 25 rounds, each with fresh data (a draft listing S from D, S withdrawable from D), publish and withdraw started together with `Task.WhenAll`, unforced; asserts no `SqlException` (1205 or 50036) and that each round's outcome is exactly one of SV40's two serial outcomes. Plus `Publish_WhileAWithdrawalHoldsTheServiceCodeLock_DoesNotWait` (withdrawal gated just after the **code** lock; publish of a version listing that service completes; then the withdrawal proceeds and is refused by R19) — the ordering argument, executed.
- **Parallel, unforced:** SV26 (two same-date publishes: one `204`, one `409`, one event), SV42 creates (five scopes; numbers contiguous, all distinct).
- **Non-interference:** `CreateService_WhileTheScheduleLockIsHeld_DoesNotWait`; `ScheduleReads_WhileTheScheduleLockIsHeld_DoNotWait`.
- **Backstops (R47):** `PublishScheduleVersion_WhenStatusChangesBehindTheLock_Returns409ChangedConcurrently` and the same for cancel and discard — a direct-SQL status change on a separate connection between the handler's load and its save (through `AuditRecordHook`), then `409`, no event, the direct value intact.

**What the application login needs:** nothing. `sp_getapplock` is executable by `public` (F-002 V5), so `Security_TimetableScheduleGrants` has no `EXECUTE`.

---

## The in-force read (R18, R21, R48)

`GetScheduleVersionInForceHandler(date)`:

1. **Timeline** (P11): `SELECT [s].[Id], [s].[EffectiveFrom] FROM [timetable].[ScheduleVersions] AS [s] WHERE [s].[Status] = N'Published'` → scan of `UX_ScheduleVersions_EffectiveFrom_Published` (filtered to published rows; covers `Id` as the clustering key; O3). `PublishedTimeline.InForceOn(date)` → none → `404 Timetable.ScheduleVersionNotInForce` (before the first published version; R21).
2. **Header:** `PK_ScheduleVersions` seek for number, names, start date, `PublishedAtUtc`.
3. **Listed services with their running facts:**

   ```sql
   SELECT [e].[ServiceId], [s].[Code], [s].[EffectiveFrom], [s].[EffectiveTo],
          [s].[RunsOnMonday], [s].[RunsOnTuesday], [s].[RunsOnWednesday], [s].[RunsOnThursday],
          [s].[RunsOnFriday], [s].[RunsOnSaturday], [s].[RunsOnSunday]
   FROM [timetable].[ScheduleVersionServices] AS [e]
   INNER JOIN [timetable].[Services] AS [s] ON [s].[Id] = [e].[ServiceId]
   WHERE [e].[ScheduleVersionId] = @id
   ORDER BY [s].[Code], [e].[ServiceId]
   ```

   `PK_ScheduleVersionServices` range seek on `ScheduleVersionId` (≤ 250 rows), nested-loop `PK_Services` seeks. `runsOnDate = ServiceRunningDay.RunsOn(date, EffectiveFrom, EffectiveTo, days)` (P12): the service's own period and weekday; the version-in-force condition is step 1. An empty version returns `200` with `services: []` (SV53).

It reads committed rows and takes no applock; a publish or cancel in flight is either seen whole or not at all (one save, one commit).

The same filtered index serves the withdrawal guard (O4) and the successor lookup (P11). **Why not a `TOP(1)` seek** (`WHERE Status = N'Published' AND EffectiveFrom <= @d ORDER BY EffectiveFrom DESC`): it is the cheaper plan, but it would give R21 a second home next to `PublishedTimeline`, which R19 needs anyway. The list of published versions stays small (one row per timetable change); if it ever grows large the seek is a local change (Risks R-7).

---

## DB changes

Three migrations, applied in order by the EF migration bundle under `ycr_migrator` (ADR-0022), each reviewed per `docs/workflows/04-database-change.md`, named `yyyyMMddHHmmss_<Module>_<Change>` (`docs/20` §6). **Upgrade path:** all three are additive on F-004's schema (last migration `20260925072603_Security_TimetableGrants`); `ScheduleMigrationTests` migrates a database already at that migration, with station, route, service and stop rows in it (`docs/21` §Data). No `Services`, `ServiceStops` or `network` object changes (O2).

### 1. `<ts>_Timetable_CreateScheduleVersions` (EF model-built)

Every object is declared in the EF model, including the check constraints (the F-001 R-4 ruling), so `has-pending-model-changes` sees drift (V3).

**`timetable.ScheduleVersions`** — `Id uniqueidentifier` PK (`ValueGeneratedNever`, R38) · `Number int` · `NameEn nvarchar(100)` · `NameMy nvarchar(100)` · `EffectiveFrom date` · `Status nvarchar(10)` (concurrency token, R47) · `CreatedAtUtc datetimeoffset(3)` · `PublishedAtUtc`, `DiscardedAtUtc`, `CancelledAtUtc datetimeoffset(3) NULL`. No `rowversion`.

| Object | Definition |
|---|---|
| `PK_ScheduleVersions` | clustered on `Id` |
| `UX_ScheduleVersions_Number` | unique on `Number` (R7 backstop) |
| `UX_ScheduleVersions_EffectiveFrom_Published` | unique on `EffectiveFrom` `WHERE [Status] = N'Published'` (R22 authority → `409`; the timeline scan, O3) |
| `CK_ScheduleVersions_Number` | `[Number] >= 1` |
| `CK_ScheduleVersions_Status` | `[Status] IN (N'Draft', N'Published', N'Discarded', N'Cancelled')` |
| `CK_ScheduleVersions_StatusInstants` | `([Status] = N'Draft' AND [PublishedAtUtc] IS NULL AND [DiscardedAtUtc] IS NULL AND [CancelledAtUtc] IS NULL) OR ([Status] = N'Published' AND [PublishedAtUtc] IS NOT NULL AND [DiscardedAtUtc] IS NULL AND [CancelledAtUtc] IS NULL) OR ([Status] = N'Discarded' AND [PublishedAtUtc] IS NULL AND [DiscardedAtUtc] IS NOT NULL AND [CancelledAtUtc] IS NULL) OR ([Status] = N'Cancelled' AND [PublishedAtUtc] IS NOT NULL AND [DiscardedAtUtc] IS NULL AND [CancelledAtUtc] IS NOT NULL)` |
| `CK_ScheduleVersions_CreatedAtUtc_Utc` | `DATEPART(TZOFFSET, [CreatedAtUtc]) = 0` |
| `CK_ScheduleVersions_PublishedAtUtc_Utc`, `…_DiscardedAtUtc_Utc`, `…_CancelledAtUtc_Utc` | `[<col>] IS NULL OR DATEPART(TZOFFSET, [<col>]) = 0` |

**`timetable.ScheduleVersionServices`** — `ScheduleVersionId uniqueidentifier` · `ServiceId uniqueidentifier`.

| Object | Definition |
|---|---|
| `PK_ScheduleVersionServices` | clustered on `(ScheduleVersionId, ServiceId)` — a service listed once (R17); covers the FK to `ScheduleVersions` |
| `IX_ScheduleVersionServices_ServiceId` | nonclustered on `ServiceId` — the FK to `Services` (O1) and "which versions list S" |
| `FK_ScheduleVersionServices_ScheduleVersions_ScheduleVersionId` | → `ScheduleVersions(Id)`, `NO ACTION` |
| `FK_ScheduleVersionServices_Services_ServiceId` | → `timetable.Services(Id)`, `NO ACTION`, no navigation |

**`timetable.ScheduleStopTimes`** — `ScheduleVersionId`, `ServiceId uniqueidentifier` · `Position int` · `ArrivalMinute`, `DepartureMinute smallint NULL`.

| Object | Definition |
|---|---|
| `PK_ScheduleStopTimes` | clustered on `(ScheduleVersionId, ServiceId, Position)`; covers the FK to `ScheduleVersionServices` |
| `IX_ScheduleStopTimes_ServiceId_Position` | nonclustered on `(ServiceId, Position)`, **not unique** (one row per version) — the FK to `PK_ServiceStops` (O1; P21) |
| `FK_ScheduleStopTimes_ScheduleVersionServices_ScheduleVersionId_ServiceId` | `(ScheduleVersionId, ServiceId)` → `ScheduleVersionServices`, `NO ACTION` |
| `FK_ScheduleStopTimes_ServiceStops_ServiceId_Position` | `(ServiceId, Position)` → `timetable.ServiceStops(ServiceId, Position)`, `NO ACTION`, no navigation (spec D16) |
| `CK_ScheduleStopTimes_Minutes` | `([ArrivalMinute] IS NULL OR [ArrivalMinute] BETWEEN 0 AND 1439) AND ([DepartureMinute] IS NULL OR [DepartureMinute] BETWEEN 0 AND 1439)` (R15, R16) |
| `CK_ScheduleStopTimes_AnyTime` | `[ArrivalMinute] IS NOT NULL OR [DepartureMinute] IS NOT NULL` (R10) |
| `CK_ScheduleStopTimes_Dwell` | `[ArrivalMinute] IS NULL OR [DepartureMinute] IS NULL OR [DepartureMinute] >= [ArrivalMinute]` (R12: arrival ≤ departure) |

First- and last-stop rules, contiguity and ordering across stops are the aggregate's (a check cannot see other rows). All keys are inside `timetable`, so ADR-0025's cross-module conditions do not apply.

**Index decision** — each query and write, with its access path:

| Named query / write | Access path |
|---|---|
| Timeline (in force; withdrawal coverage; O3, O4) | `UX_ScheduleVersions_EffectiveFrom_Published` scan (covering); coverage adds `PK_ScheduleVersionServices` seeks |
| Highest number (O5) | `UX_ScheduleVersions_Number` backward seek |
| Service facts for create/publish (`Services WHERE Id IN (…)`, stop count) | `PK_Services` seeks; `PK_ServiceStops` prefix for the count |
| Listed services of a version (publish, get, in force) | `PK_ScheduleVersionServices` range on `ScheduleVersionId` + `PK_Services` seeks |
| Stop times of one service in a version | `PK_ScheduleStopTimes` range on `(ScheduleVersionId, ServiceId)`; `PK_ServiceStops` range for station ids |
| List: `ORDER BY Number`, `COUNT`, page, `?status=` | `UX_ScheduleVersions_Number` ordered scan + key lookups (a page ≤ 200); the `status` filter is residual (versions are few) |
| Transition update | `PK_ScheduleVersions` |
| `INSERT` FK validation | `PK_ScheduleVersions`, `PK_ScheduleVersionServices`, `PK_Services`, `PK_ServiceStops` |
| Reverse FK checks on `DELETE` of `Services`/`ServiceStops` | **never run** (no `DELETE` grant; F-004 R33) |

Data impact: none (new, empty tables). **No seed version** (R37, OQ1).

**Rollback.** `Down()` drops `ScheduleStopTimes`, `ScheduleVersionServices`, then `ScheduleVersions` (O2). The `timetable` schema and F-004's tables stay. After real versions exist, a down-migration destroys timetable history (and the audit ledger then names versions whose rows are gone): production recovery is a restore, not `Down()` (as F-002–F-004). **Roll-forward:** any correction is a new migration; an applied migration is never edited.

### 2. `<ts>_Identity_SeedSchedulePermissionGrants`

Inserts exactly ten `identity.RolePermissions` rows (`docs/10` §Schedule permission grants): `schedules.manage` → `SystemAdministrator`, `RailwayAdministrator`; `schedules.read` → all eight roles. Role ids re-declared as constants (a migration never depends on another's code). Header comment: "BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-26; T-053, OQ59) — not a Myanma Railways answer." No `schedules.publish` row (OQ59), no role, no user. Total grants **34 → 44**.

**Rollback.** `Down()` deletes exactly those ten `(RoleId, Permission)` pairs; nothing references them; the schedule endpoints then return `403` to everyone. **Roll-forward:** a changed grant is a new reviewed migration, never an API call.

### 3. `<ts>_Security_TimetableScheduleGrants`

Exactly spec §7, raw SQL (like `Security_TimetableGrants`):

```sql
GRANT SELECT, INSERT ON [timetable].[ScheduleVersions] TO [ycr_app];
GRANT UPDATE ON [timetable].[ScheduleVersions]([Status], [PublishedAtUtc], [DiscardedAtUtc], [CancelledAtUtc]) TO [ycr_app];
GRANT SELECT, INSERT ON [timetable].[ScheduleVersionServices] TO [ycr_app];
GRANT SELECT, INSERT ON [timetable].[ScheduleStopTimes] TO [ycr_app];
```

**Deliberately absent**, asserted by `DatabasePrivilegeTests`: `DELETE` on all three tables (R25); `UPDATE` of `ScheduleVersions.Id`, `Number`, `NameEn`, `NameMy`, `EffectiveFrom`, `CreatedAtUtc`; table-level `UPDATE` on all three; any `UPDATE` on `ScheduleVersionServices` or `ScheduleStopTimes`; `ALTER`, `CONTROL`, any DDL; any `EXECUTE` (the applock needs none). The XML comment states R25, R27 and R32: these grants are the database statement that a version's content never changes, only its status moves forward, and nothing is deleted. F-004's grants on `Services` and `ServiceStops` are unchanged.

**Rollback.** `Down()` revokes exactly these, in reverse order; no data is lost. **Roll-forward:** widening needs a new migration, a spec change and a review, never a failing test.

**Order.** 1 before 3 (3 grants on 1's objects); 2 depends only on F-002's roles. 1 → 2 → 3, as F-002–F-004.

### Performance

- **Create at the caps** inserts 1 + 250 + 10,000 rows plus one audit row in one save under the Timetable-wide lock; EF batches the inserts (default `MaxBatchSize` 42 statements per batch, well under the 2,100-parameter limit). Measured at step 6 (V2). Only two administrator roles can create, and a timetable is created rarely.
- **Publish, discard, cancel** update one row; publish reads ≤ 250 service rows under the lock.
- **Withdrawal** adds one covered scan of the published versions and ≤ n PK seeks, under the global lock.
- **Reads** are index seeks and ranges; the largest (a version's detail) returns ≤ 250 service rows; one service's times ≤ 200 rows plus one `GetStationsAsync`.

---

## API changes

Base path `/api/v1`, JSON camelCase, GUID ids, dates `YYYY-MM-DD`, times `HH:mm` (ADR-0027), ProblemDetails with `errorCode` and `traceId` (ADR-0004). Every endpoint also returns `401 Auth.Unauthenticated` (with `WWW-Authenticate: Bearer`) and, during a must-change session, `403 Auth.PasswordChangeRequired` (`docs/08`). No `Idempotency-Key` (R40). No version token (R47).

### Endpoint inventory

| Method | Path | Permission | Body limit | Idempotency | Response / errors |
|---|---|---|---|---|---|
| POST | `/api/v1/schedules/versions` | `schedules.manage` | **2 MiB** | No (R40) | `201` + `{ id, number }`, `Location: /api/v1/schedules/versions/{id}` · `400 Common.ValidationFailed` · `400 Timetable.InvalidScheduleVersionName` · `400 Timetable.InvalidTimetableTime` · `400` malformed JSON · `401` · `403` · `413` · `422 Timetable.ScheduleVersionEffectiveFromInPast` · **`422 Timetable.EmptyScheduleVersionNotInFuture`** (Amendment 2) · `422 Timetable.ScheduleServiceNotFound` · `422 Timetable.ScheduleServiceRepeated` · `422 Timetable.ScheduleServiceNotEffective` · `422 Timetable.ScheduleStopNotInService` · `422 Timetable.ScheduleStopTimesIncomplete` · `422 Timetable.ScheduleStopTimeUnexpected` · `422 Timetable.ScheduleDwellNegative` · `422 Timetable.ScheduleTimesNotIncreasing` |
| GET | `/api/v1/schedules/versions/{id:guid}` | `schedules.read` | — | n/a | `200` + `ScheduleVersionResponse` · `401` · `403` · `404 Timetable.ScheduleVersionNotFound` |
| GET | `/api/v1/schedules/versions/{id:guid}/services/{serviceId:guid}` | `schedules.read` | — | n/a | `200` + `ScheduleServiceTimesResponse` · `401` · `403` · `404 Timetable.ScheduleVersionNotFound` · `404 Timetable.ScheduleServiceNotInVersion` |
| GET | `/api/v1/schedules/versions` | `schedules.read` | — | n/a | `?page=1&pageSize=50` (max 200) `&status=` (optional, exact name; Q2); ordered by `number` · `200` + `{ items, page, pageSize, totalCount }` · `400 Timetable.InvalidPageRequest` · `400 Common.ValidationFailed` (unknown `status`, Q2) · `401` · `403` |
| GET | `/api/v1/schedules/versions/in-force` | `schedules.read` | — | n/a | `?date=YYYY-MM-DD` (required) · `200` + `ScheduleVersionInForceResponse` · `400 Common.ValidationFailed` · `401` · `403` · `404 Timetable.ScheduleVersionNotInForce` |
| POST | `/api/v1/schedules/versions/{id:guid}/publish` | `schedules.manage` | no body (server default, P15) | No | `204` · `401` · `403` · `404 Timetable.ScheduleVersionNotFound` · `409 Timetable.ScheduleVersionEffectiveFromTaken` · `409 Timetable.ScheduleVersionChangedConcurrently` · `422 Timetable.ScheduleVersionNotDraft` · `422 Timetable.ScheduleVersionEffectiveFromInPast` · **`422 Timetable.EmptyScheduleVersionNotInFuture`** (Amendment 1) · `422 Timetable.ScheduleServiceNotEffective` |
| POST | `/api/v1/schedules/versions/{id:guid}/cancel` | `schedules.manage` | no body | No | `204` · `401` · `403` · `404 Timetable.ScheduleVersionNotFound` · `409 Timetable.ScheduleVersionChangedConcurrently` · `422 Timetable.ScheduleVersionNotPublished` · `422 Timetable.ScheduleVersionAlreadyEffective` |
| POST | `/api/v1/schedules/versions/{id:guid}/discard` | `schedules.manage` | no body | No | `204` · `401` · `403` · `404 Timetable.ScheduleVersionNotFound` · `409 Timetable.ScheduleVersionChangedConcurrently` · `422 Timetable.ScheduleVersionNotDraft` |
| POST | `/api/v1/services/{id:guid}/withdraw` (F-004, **changed**) | `services.manage` (unchanged) | 1 KiB (unchanged) | No | F-004's codes **plus `422 Timetable.ServiceInPublishedScheduleVersion`**, checked last (§The F-004 withdrawal change) |

**Deliberately absent** (spec §6): `PATCH`/`PUT`/`DELETE` on `/schedules/versions` and `/schedules/versions/{id}` (R25–R27, R32); adding or removing a draft's services (R27); withdrawing a version that has taken effect (R34); `schedules.publish` (R4); any anonymous read (R44). `AbsentScheduleEndpoints_AreNotRouted` pins them (SV44).

### Contracts

```text
CreateScheduleVersionRequest   { nameEn: string?, nameMy: string?, effectiveFrom: string?,
                                 services: ScheduleServiceRequest?[]? }                      // P14
ScheduleServiceRequest         { serviceId: string?, stopTimes: ScheduleStopTimeRequest?[]? }
ScheduleStopTimeRequest        { position: int?, arrival: string?, departure: string? }      // "HH:mm" | null
CreateScheduleVersionResponse  { id, number }
ScheduleVersionResponse        { id, number, nameEn, nameMy, effectiveFrom, status,
                                 createdAtUtc, publishedAtUtc?, discardedAtUtc?, cancelledAtUtc?,
                                 services: [ { serviceId, code, nameEn, nameMy, direction, stopCount,
                                               effectiveFrom, effectiveTo?, neverRuns } ] }
ScheduleServiceTimesResponse   { versionId, serviceId, code,
                                 stops: [ { position, stationId, stationCode, stationNameEn,
                                            stationNameMy, arrival?, departure? } ] }
ScheduleVersionSummaryResponse { id, number, nameEn, nameMy, effectiveFrom, status, serviceCount,
                                 createdAtUtc, publishedAtUtc?, discardedAtUtc?, cancelledAtUtc? }
ScheduleVersionInForceResponse { date, id, number, nameEn, nameMy, effectiveFrom, publishedAtUtc,
                                 services: [ { serviceId, code, runsOnDate } ] }
```

Exactly spec §6. No request has an actor field (SV49). Responses are mapped explicitly from DTOs; the Api allowlist already forbids `YCR.Domain.Timetable` in the Api.

**Validators** (shape only; every business rule stays in the domain):
- `CreateScheduleVersionRequestValidator`: `effectiveFrom` present and exactly `yyyy-MM-dd`; `services` present (an empty array is valid, R9), **≤ `MaxServicesPerVersion` = 250**, no `null` element; each `serviceId` present and a `D`-format GUID; each `stopTimes` present, **≤ `MaxStopTimesPerService` = 200**, no `null` element; each `position` present and ≥ 1 (SV8); **the total of all `stopTimes` ≤ `MaxStopTimesPerVersion` = 10,000** (Q1). `nameEn`, `nameMy`, `arrival`, `departure` are not checked here (P14). Each constant carries the REQUIRED CONTROL comment (R41).
- `ScheduleVersionInForceRequestValidator`: `date` present and exactly `yyyy-MM-dd` (SV48).
- `ListScheduleVersionsRequestValidator`: `status`, if present, exactly one of the four names (Q2).

### Error code → HTTP

| Code | `ErrorType` | HTTP |
|---|---|---|
| `Common.ValidationFailed` (validation filter) | — | 400 |
| `Timetable.InvalidScheduleVersionName`, `InvalidTimetableTime`, `InvalidPageRequest` (existing) | Validation | 400 |
| `Timetable.ScheduleVersionNotFound`, `ScheduleServiceNotInVersion`, `ScheduleVersionNotInForce` | NotFound | 404 |
| `Timetable.ScheduleVersionEffectiveFromTaken`, `ScheduleVersionChangedConcurrently` | Conflict | 409 |
| `Timetable.ScheduleVersionEffectiveFromInPast`, `ScheduleServiceNotFound` (a `422`: the addressed resource is the new version, F-003 E8), `ScheduleServiceRepeated`, `ScheduleServiceNotEffective`, `ScheduleStopNotInService`, `ScheduleStopTimesIncomplete`, `ScheduleStopTimeUnexpected`, `ScheduleDwellNegative`, `ScheduleTimesNotIncreasing`, `ScheduleVersionNotDraft`, `ScheduleVersionNotPublished`, `ScheduleVersionAlreadyEffective`, **`EmptyScheduleVersionNotInFuture`**, **`ServiceInPublishedScheduleVersion`** | BusinessRule | 422 |
| Oversized body; malformed JSON | framework (T-042) | 413; 400 |

21 new codes (2 Validation, 3 NotFound, 2 Conflict, 14 BusinessRule). Only `ResultExtensions` maps them. An exception from either lock (timeout, `50035`/`50036`) or from `UX_ScheduleVersions_Number` is not caught: the F-001 opaque `500`.

### Body limits and caps (P15, R41, SV47; Q1)

The realistic whole-network version (spec §0.10 E10) is **200 services × 40 stops** = 8,000 stop times. Proposed caps: **250 services**, **200 stop times per service** (F-004's stop cap: a service has at most 200 stops, and one time row per stop), **10,000 stop times per version** (= 250 × 40; Q1).

Largest valid `CreateScheduleVersionRequest`, as `System.Text.Json` web defaults write it (compact; every non-ASCII character escaped `\uXXXX`, 6 bytes), **measured** (O6):

| Body | Compact | Indented |
|---|---|---|
| Realistic: 200 services × 40 stops | 443,667 | 923,293 |
| **At the proposed caps: 250 services × 40 stops (10,000 stop times)** | **554,267** | **1,153,793** |
| At the caps with one 200-stop service (3-digit positions) and the rest 40, 10,000 in total | 554,160 | 1,153,534 |
| Without a total cap: 250 × 200 (both array caps full) | 2,739,517 | 5,699,043 |

Parts of the bound, for review: one stop time is at most `{"position":200,"arrival":"23:59","departure":"23:59"}` = **54 bytes** + 1 comma; a service adds `{"serviceId":"<36>","stopTimes":[…]}` = 68 bytes with its comma; the header, with both 100-character names fully escaped (600 + 600), is about 1,300 bytes. Upper bound: 10,000 × 55 + 250 × 68 + 1,300 = **568,300 bytes**.

**Limit: 2 MiB (2,097,152 bytes)** — 3.8 times the largest compact body and 1.8 times the largest indented one, the same margin policy as F-004's 32 KiB (3.5 times), **64 times F-004's create limit** because a timetable is the whole network in one request (OQ56 ruling). It stays far below Kestrel's default (≈ 28.6 MiB). A request that needs more (a network beyond the caps) is refused `400` by a cap before it could need more room, not silently truncated. Without Q1's total cap, SV47's "exactly the caps → reaches the handler" would need 8 MiB.

`RequestSizeLimitAttribute(2 * 1024 * 1024)` endpoint metadata (constant `ScheduleVersionEndpoints.CreateScheduleVersionMaxRequestBodyBytes`), copied by endpoint routing into Kestrel's `IHttpMaxRequestBodySizeFeature` before the body is read. `ScheduleVersionRequestLimitTests` proves it on real Kestrel (`UseKestrel(0)`): declared-length and chunked over the limit → `413` before binding with no stack trace, type or path; malformed JSON → bounded `400`; the body at exactly the caps (≤ 568,300 bytes) reaches the handler (`422 Timetable.ScheduleServiceNotFound` with unknown service ids — no data written); one over each cap → `400 Common.ValidationFailed`.

### OpenAPI, `.http`, smoke

- **OpenAPI/Scalar:** Development only (unchanged). Tag `ScheduleVersions`; `.WithName`, `.WithSummary`, `.Produces<…>`, `.ProducesProblem(...)` as `ServiceEndpoints` declares them. The create summary states the caps, `HH:mm`, and that stop 1 has only a departure and the last stop only an arrival.
- **`YCR.Api.http`**, a "Schedule versions (F-005)" section: create a draft listing a service → `201`; read it and one service's times → `200`; list → `200`; publish → `204`; in force on a date → `200` with `runsOnDate`; publish a second draft on the same date → `409 Timetable.ScheduleVersionEffectiveFromTaken`; cancel a future version → `204`; cancel one in force → `422 Timetable.ScheduleVersionAlreadyEffective`; discard a draft → `204`; an empty version starting today → create `422 Timetable.EmptyScheduleVersionNotInFuture` (Amendment 2); withdraw a listed service → `422 Timetable.ServiceInPublishedScheduleVersion`. Variables `@scheduleVersionId`, `@serviceId`; each comment names the permission and the errors.
- **`api-smoke` additions** (after the F-004 service checks, same administrator token; `today`, `tomorrow=$(TZ=Asia/Yangon date -d '+1 day' +%F)`), so the seed, the grants, both locks and the filtered index run under `ycr_app` in the deployed shape:
  1. Anonymous `GET /api/v1/schedules/versions` → `401`, `Auth.Unauthenticated`.
  2. `POST /api/v1/schedules/versions` from `$today` listing `SV1` (A dep 06:00, B arr 06:10) → `201`; `GET` it → `200`; `GET …/services/SV1` → `200`; `GET` the list → `200`.
  3. Publish it → `204`; `GET …/in-force?date=$today` → `200` naming it; cancel it → `422 Timetable.ScheduleVersionAlreadyEffective`.
  4. An empty version from `$today` → `422 Timetable.EmptyScheduleVersionNotInFuture` at creation (Amendment 2); nothing is created. (The publication-time refusal needs the clock to move, so it is tested in Application and Api tests only.)
  5. An empty version from `$tomorrow` → `201`; publish → `204`; cancel → `204`.
  6. A draft → discard → `204`.
  7. `POST /api/v1/services/SV1/withdraw` from `$tomorrow` → `422 Timetable.ServiceInPublishedScheduleVersion` (the version from step 2 applies open-ended).

  `CiWorkflowTests` inspects `env:` and provisioning, not the checks; confirmed at step 10.

---

## Audit

Through `IAuditWriter.Record`, inside the handler's single `SaveChangesAsync`, in its transaction (ADR-0017, ADR-0021). A refused or losing request writes no event (R39).

| Action | When | `SubjectType` | `SubjectId` | `BeforeJson` | `AfterJson` | `AuthorizedByPermission` |
|---|---|---|---|---|---|---|
| `Timetable.ScheduleVersionCreated` | SV1 | `Timetable.ScheduleVersion` | version id | null | full snapshot | `schedules.manage` |
| `Timetable.ScheduleVersionPublished` | SV23 | ″ | ″ | header (Draft) | header (Published) | ″ |
| `Timetable.ScheduleVersionDiscarded` | SV35 | ″ | ″ | header | header | ″ |
| `Timetable.ScheduleVersionCancelled` | SV31 | ″ | ″ | header | header | ″ |
| `Timetable.ServiceWithdrawn` (F-004) | unchanged | `Timetable.Service` | service id | snapshot | snapshot | `services.manage` |

`PayloadVersion = 1`.

```text
ScheduleVersionAuditSnapshot(int Number, string NameEn, string NameMy, DateOnly EffectiveFrom, string Status,
                             DateTimeOffset? PublishedAtUtc, DateTimeOffset? DiscardedAtUtc,
                             DateTimeOffset? CancelledAtUtc) : IAuditSnapshot                  // header
ScheduleVersionCreatedAuditSnapshot(int Number, string NameEn, string NameMy, DateOnly EffectiveFrom,
                             string Status, DateTimeOffset? PublishedAtUtc, DateTimeOffset? DiscardedAtUtc,
                             DateTimeOffset? CancelledAtUtc,
                             IReadOnlyList<ScheduleVersionServiceAuditSnapshot> Services,
                             string StopTimesSha256) : IAuditSnapshot                           // full (E13)
ScheduleVersionServiceAuditSnapshot(Guid ServiceId, string ServiceCode, int StopCount)
```

`Services` are ordered by `ServiceId`'s lower-case `D` string, ordinal (the digest's order). `ServiceCode` comes from the facts loaded under the lock; codes are stable per service (F-004 R20).

**`stopTimesSha256` canonical form (E13; `ScheduleStopTimesDigest`):** one line per stop time, `"{serviceId}|{position}|{arrivalMinute}|{departureMinute}\n"`, where `serviceId` is the lower-case `D` format, the numbers are invariant-culture decimal integers, and a missing time is the empty string; lines ordered by `serviceId` (ordinal) then `position` (ascending); UTF-8 without BOM; SHA-256; lower-case hex, 64 characters. An empty version hashes the empty input: `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`. A test pins that vector and one computed from fixed ids, so the form cannot drift; the form is written into the XML comment and `docs/07`. Anyone can recompute it from the insert-only rows (R25, R36).

Records in `YCR.Application.Timetable`, so `AuditSnapshots_WithSourceAssemblies_AreValid` covers them. XML comments carry the "changing this shape bumps `PayloadVersion`" warning and the ADR-0021 rule 4 note (no personal data; a timetable has none). **Logging:** message templates only (`docs/20` §7); nothing beyond what the F-004 handlers log. **Metrics:** none (`docs/17`; spec §8).

---

## Security impact

| Item | Treatment |
|---|---|
| Permissions | `schedules.manage` on create, publish, cancel, discard; `schedules.read` on the four GETs; `.RequireAuthorization(Permissions.…)` on each; no anonymous schedule endpoint (SV43). **No station, route or service permission grants a schedule right, and no schedule permission grants a station, route or service right** (R5): API tests with only `services.*`, `routes.*` or `stations.*` get `403` on schedule endpoints; a caller with only `schedules.*` gets `403` on `POST /services/{id}/withdraw` (SV45). `SchedulePermissionGrantTests` checks all eight roles with real tokens (SV46). |
| Grants | Data, seeded by the reviewed `Identity_SeedSchedulePermissionGrants` (OQ59 provisional ruling). No API edits them. `Seed_MatchesDocs10GrantTables` keeps the seed equal to `docs/10`. |
| Database least privilege | `Security_TimetableScheduleGrants`: insert-only rows except four status columns; no `DELETE`; no DDL; no `EXECUTE`. `DatabasePrivilegeTests` asserts presences **and** absences and executes the allowed and denied statements (SV51). Every Application and API test runs as `ycr_app`. |
| Data integrity | Stop times name real stops of real services by FK (SV52); minutes, dwell, status and instants are checked from any writer; the filtered unique index enforces R22 from any writer; the number is unique; `Status` is the concurrency token behind the lock (R47). |
| Concurrency integrity | R19, R7 and the publish/cancel/withdraw decisions serialised by the Timetable-wide lock; the order with the code lock proved deadlock-free (§The Timetable-wide lock). |
| Audit integrity | Actor fields from `ICurrentUser`, never the body (SV49); append-only ledger; explicit snapshot records; the times are summarised by a digest over insert-only rows. |
| Input handling | Dates and GUIDs by the validators (P14), names and times by value objects (the `[0-9]` regex refuses Unicode digits); **caps: 250 services, 200 stop times per service, 10,000 per version; 2 MiB body limit before binding (REQUIRED CONTROL, R41)**; `pageSize` ≤ 200; EF parameterises all SQL; the applock resource is a constant passed as a parameter. |
| Error disclosure | Messages name only the caller's input (a service id, a position); `413`/`400` framework bodies carry no internals (tested as F-003/F-004); a lock timeout is an opaque `500`. |
| Denial of service / large bodies | The 2 MiB limit refuses larger bodies before any allocation beyond Kestrel's buffers; caps refuse oversized arrays before the handler (no database work, asserted); a maximal valid create holds the global lock for one insert batch set (V2); only two administrator roles can call the write endpoints, every call is audited, and a waiter gives up after 30 s. **Abuse test:** `Post_LargeBodyAbuse_IsRefusedBeforeAnyDatabaseWork` (Kestrel) sends 3 MiB declared and chunked, and a sub-limit body with 10,001 stop times, and asserts `413`/`400` with no internals, no `ScheduleVersions` row and no audit event (the refusals happen in Kestrel and in the validation filter, before the handler opens a transaction). |

**`docs/18` threats this feature touches:** *Unauthorized configuration* (the timetable is railway operational configuration; `schedules.manage` held by two roles; every change audited). *Privilege escalation* (seeded grants only; no grant API; exact-grant tests). *Insider manipulation* (immutability of published content by the grants as well as the domain; cancellation only before effect; an empty-version suspension must start after today, R50, so it can always be cancelled; each change audited with before and after). *Data disclosure* (reads need `schedules.read`; no personal data). *API abuse* (body limit, three caps, page cap, lock timeout). *Audit tampering* (unchanged ledger controls). Cookie, CSP and XSS controls are not touched.

---

## Test plan

Names follow `docs/20` §2. "TH" = the F-001 test authentication handler; "Real" = real ES256 tokens; "Kestrel" = real Kestrel; "Unmodified" = the production composition. Application and API tests run as `ycr_app`. Unless stated, the test clock is **2026-10-01 03:00 UTC** (2026-10-01 09:30 Asia/Yangon, a Thursday), and the network and services are built through the F-003/F-004 handlers (`TimetableTestData`, extended with `ScheduleTestData`: route RC `[A, B, C, D, E]` closed, S1 `[A, C, E]` Mon–Fri from 2026-10-05, S2 `[E, C, A]` every day from 2026-10-05, "a valid create" as spec §4). Stage 5 (a different agent) owns completeness and checks itself against this section.

### `YCR.Domain.Tests`

| Test | Spec |
|---|---|
| `Timetable/TimetableTimeTests.Parse_WithValidTime_ReturnsMinutes` (theory: `00:00`→0, `06:00`→360, `23:59`→1439) | SV11, R16 |
| ″ `Parse_WithInvalidText_ReturnsInvalidTimetableTime` (theory: `24:00`, `24:15`, `6:00`, `06:60`, `06:00:30`, `0600`, `""`, `" 06:00"`, `null`, Myanmar digits `၀၆:၀၀`, Arabic-Indic digits) | SV11, R16 |
| ″ `ToString_WritesTwoDigitHoursAndMinutes` · `FromMinutes_OutsideTheDay_Throws` (−1, 1440) | R16, ADR-0027 |
| `Timetable/ScheduleVersionInputTests.Parse_WithBlankMissingOrLongName_ReturnsInvalidScheduleVersionName` (theory) | SV18, R7 |
| ″ `Parse_WithEffectiveFromBeforeToday_ReturnsEffectiveFromInPast` · `Parse_WithEffectiveFromToday_Succeeds` (with one service: an empty one is SV55, Amendment 2) | SV19, R31 |
| ″ `Parse_ReportsTheFirstBadTimeInRequestOrder` | R45 (3) |
| ″ **`Parse_WithNoServicesStartingToday_ReturnsEmptyNotInFuture`** · **`Parse_WithNoServicesStartingTomorrow_Succeeds`** · **`Parse_WithNoServicesStartingBeforeToday_ReturnsEffectiveFromInPastFirst`** | **SV55, R50 (Amendment 2)**, R45 (5) |
| `Timetable/ScheduleVersionTests.CreateDraft_WithValidInput_BuildsDraftWithEntriesAndMinutes` (number, Draft, null instants, 360/370/372/…) | SV1 |
| ″ `CreateDraft_WithNoServices_BuildsAnEmptyDraft` | SV22, R9 |
| ″ `CreateDraft_WithUnexpectedTime_ReturnsStopTimeUnexpected` (theory: arrival at stop 1; departure at last stop) | SV6, R10 |
| ″ `CreateDraft_WithMissingOrRepeatedTimes_ReturnsStopTimesIncomplete` (theory: departure at C, arrival at E, departure at A, position 2 omitted, position 2 twice) | SV7, R10 |
| ″ `CreateDraft_WithPositionBeyondTheStops_ReturnsStopNotInService` | SV8, R10 |
| ″ `CreateDraft_WithDepartureBeforeArrival_ReturnsDwellNegative` · `CreateDraft_WithZeroDwell_Succeeds` | SV9, R12 |
| ″ `CreateDraft_WithTimesNotIncreasing_ReturnsTimesNotIncreasing` (theory: equal, earlier, E after C equal) | SV10, R12 |
| ″ `CreateDraft_FromMidnightToLastMinute_Succeeds` | SV11, R16 |
| ″ `CreateDraft_CrossingMidnight_ReturnsTimesNotIncreasing` | SV12, R15 |
| ″ `CreateDraft_FullCircuit_TimesTheClosingStopLast` | SV13, R10 |
| ″ `CreateDraft_WithUnknownOrRepeatedService_ReturnsServiceNotFoundOrRepeated` (theory) | SV14, R17 |
| ″ `CreateDraft_WithServiceNotEffectiveOnStartDate_ReturnsServiceNotEffective` (theory: starts later; ended before; withdrawn from the start date; never runs) | SV15, R17 |
| ″ `CreateDraft_WithServiceEffectiveOnlyOnStartDateOrFromThePast_Succeeds` (theory) | SV16, R17 |
| ″ `CreateDraft_WithSeveralFailures_ReturnsTheFirstInR45Order` (theory, one row per adjacent pair of P4's steps 6–13, plus "second service fails, first valid") | R45 |
| ″ `CreateDraft_WithNonUtcTime_Throws` · `CreateDraft_WithNumberBelowOne_Throws` | ADR-0018, R7 |
| ″ `Publish_Draft_SetsPublishedAndInstant` | SV23, R30 |
| ″ `Publish_WhenNotDraft_ReturnsNotDraftAndChangesNothing` (theory: Published, Discarded, Cancelled) | SV25, R30 |
| ″ `Publish_WithStartBeforeToday_ReturnsEffectiveFromInPast` · `Publish_OnTheStartDate_Succeeds` | SV20, SV21, R31 |
| ″ **`Publish_EmptyVersionWhoseStartHasBecomeToday_ReturnsEmptyNotInFutureAndChangesNothing`** (draft parsed with today 2026-10-01 for 2026-10-02, published with today 2026-10-02) · **`Publish_EmptyVersionStartingTomorrow_Succeeds`** | **SV55, SV56, R50** |
| ″ `Publish_WithListedServiceNoLongerEffective_ReturnsServiceNotEffective` (reports the lowest `ServiceId`) | SV17, R17 |
| ″ `Discard_Draft_SetsDiscarded` · `Discard_WhenNotDraft_ReturnsNotDraft` (theory) | SV35, R28 |
| ″ `Cancel_PublishedBeforeItsStart_SetsCancelledAndKeepsPublishedAt` | SV31, R34 |
| ″ `Cancel_OnOrAfterItsStart_ReturnsAlreadyEffective` (theory: today; yesterday) | SV33, R34 |
| ″ `Cancel_WhenNotPublished_ReturnsNotPublished` (theory: Draft, Discarded, Cancelled) | SV34, R34 |
| ″ `ScheduleVersion_ExposesNoMutatorOtherThanItsTransitions` (public methods exactly `Cancel`, `CreateDraft`, `Discard`, `Publish`; no public setter; children read-only) | R27, R32 |
| `Timetable/ScheduleVersionNumberingTests.Next_StartsAtOneThenHighestPlusOne` (theory: null→1, 1→2, 7→8) | SV4, R7 |
| `Timetable/PublishedTimelineTests.InForceOn_ReturnsTheLatestStartOnOrBeforeTheDate` (theory over SV27–SV29's dates) · `InForceOn_BeforeTheFirstVersion_ReturnsNone` · `From_WithDuplicateStartDates_Throws` | R21, SV27–SV29 |
| ″ `AppliesOnOrAfter_UsesTheSuccessorsStartDate` (theory: no successor; successor after D; successor on D; successor before D) | R19 |
| `Timetable/ServiceScheduleCoverageTests.AppliesOnOrAfter_OnlyForListingVersions` · `None_NeverApplies` | R19, SV38 |
| `Timetable/ServiceTests.Withdraw_WhileAListingVersionAppliesOnOrAfterTheDate_ReturnsInPublishedVersionAndChangesNothing` (theory: open-ended V1; V1 superseded after D) | SV36, SV37, R19 |
| ″ `Withdraw_FromTheStartOfTheVersionThatDropsTheService_Succeeds` | SV37, R20 |
| ″ `Withdraw_WhenOnlyASupersededPastVersionListsIt_Succeeds` | SV39 |
| ″ `Withdraw_ChecksF004RulesBeforeTheScheduleGuard` (theory: past date → `WithdrawalDateInPast`; not shortening → `WithdrawalDoesNotShorten`; both with a listing version that applies) | SV36, SV54, §0.12 |
| ″ `Withdraw_WithNullCoverage_Throws` | P10 |
| `Timetable/ServiceRunningDayTests.RunsOn_ChecksThePeriodAndTheWeekday` (theory: Saturday → false, Monday → true, after `EffectiveTo` → false, before `EffectiveFrom` → false, never runs → false) | SV30, SV54, R18, R48 |
| `Timetable/OperatingDaysTests.Includes_ReturnsWhetherTheDayIsAnOperatingDay` | R18 |
| existing `ServiceTests` (seven withdrawal tests' call sites, items 19–25), `EffectivePeriodTests`, `OperatingDaysTests`, `ServiceCodeTests` | regression |

### `YCR.Application.Tests` (real SQL Server, `ycr_app`, `TestClock`)

Support: `Timetable/ScheduleTestData.cs`, `Timetable/GatedScheduleVersionsLock.cs`, `Timetable/RecordingLocks.cs` (records the order of `AcquireAsync` calls across both lock abstractions), the existing `AuditRecordHook`.

| Test | Spec |
|---|---|
| `Timetable/CreateScheduleVersionHandlerTests.CreateScheduleVersion_WithValidCommand_PersistsRowsAndOneAuditEvent` (row values, `Number = 1`, `Draft`, instants null, 1 entry, 3 stop times with minutes; event subject, full snapshot with the service code and stop count and the digest, `AuthorizedByPermission`) | SV1, R39 |
| ″ `CreateScheduleVersion_Numbers_AreContiguousAndNeverReused` (1, 2, discard 2, 3; a cancelled version keeps its number) | SV4, R7 |
| ″ `CreateScheduleVersion_WithMyanmarName_RoundTripsExactly` | SV5 |
| ″ `CreateScheduleVersion_WithInvalidTimes_ReturnsErrorAndWritesNothing` (theory: SV6 ×2, SV7 ×5, SV8, SV9, SV10 ×3, SV12) | SV6–SV10, SV12, SV50 |
| ″ `CreateScheduleVersion_WithBadTimeFormat_ReturnsInvalidTimetableTimeAndWritesNothing` (theory: SV11's seven strings) | SV11 |
| ″ `CreateScheduleVersion_WithEdgeTimes_Creates` (theory: dwell 0; `00:00` … `23:59`) · `CreateScheduleVersion_FullCircuit_Creates` | SV9, SV11, SV13 |
| ″ `CreateScheduleVersion_WithUnknownOrRepeatedService_ReturnsErrorAndWritesNothing` (theory) | SV14 |
| ″ `CreateScheduleVersion_WithServiceNotEffectiveOnStartDate_ReturnsNotEffectiveAndWritesNothing` (theory: from 2026-10-06; `effectiveTo` 2026-10-04; withdrawn from 2026-10-05; never runs) | SV15 |
| ″ `CreateScheduleVersion_WithServiceEffectiveOnlyOnStartDateOrFromThePast_Creates` (theory) | SV16 |
| ″ `CreateScheduleVersion_WithInvalidName_ReturnsInvalidScheduleVersionName` (theory: blank, 101 characters) | SV18 |
| ″ `CreateScheduleVersion_WithStartBeforeToday_ReturnsInPastAndWritesNothing` · `CreateScheduleVersion_WithStartToday_Creates` (listing a service effective on 2026-10-01, as SV16; not empty, Amendment 2) | SV19 |
| ″ `CreateScheduleVersion_AtYangonMidnight_UsesTheYangonDateAsToday` (clock 2026-09-30 17:29:59Z vs 17:30:00Z, start 2026-10-01) | R33 |
| ″ `CreateScheduleVersion_WithNoServices_CreatesHeaderOnlyAndSnapshotWithEmptyServices` (digest = the empty vector) | SV22, R9 |
| ″ **`CreateScheduleVersion_WithNoServicesStartingToday_ReturnsEmptyNotInFutureAndWritesNothing`** (no row, no event; the next valid create still gets number 1; from 2026-09-30 → `EffectiveFromInPast` first) | **SV55 (creation), R50 (Amendment 2)** |
| ″ `CreateScheduleVersion_WithSeveralFailures_ReturnsTheFirstInR45Order` (theory across P4's steps 2–13) | R45 |
| ″ `CreateScheduleVersion_AuditActor_ComesFromCurrentUserOnly` | SV49 |
| ″ `CreateScheduleVersion_WhenNumberIsTakenBehindTheLock_FailsAndWritesNothing` (a direct-SQL row with the next number before the save; exception; no row of ours, no event) | SV50, R7 backstop, P5 |
| ″ `CreateScheduleVersion_WholeNetworkAtTheCaps_PersistsEveryRow` (250 services × 40 stops; row counts; duration logged for V2) | R41, SV47, V2 |
| `Timetable/ScheduleStopTimesDigestTests.Compute_WithNoStopTimes_IsTheEmptyVector` · `Compute_WithFixedIds_MatchesThePinnedVector` · `Compute_IsIndependentOfInputOrder` | §8, E13 |
| `Timetable/PublishScheduleVersionHandlerTests.PublishScheduleVersion_Draft_UpdatesStatusAndInstantAndWritesOneEvent` (by the draft's author; child rows untouched) | SV23, R4 |
| ″ `PublishScheduleVersion_WithAPublishedStartDate_Returns409ThenSucceedsOnceThatIsCancelled` | SV24, R22 |
| ″ `PublishScheduleVersion_WhenNotDraftOrUnknown_ReturnsNotDraftOrNotFound` (theory: Published, Cancelled, Discarded, unknown) | SV25 |
| ″ `PublishScheduleVersion_TwoSameStartDateInParallel_OneSucceedsOneConflicts` (one `Published` row, one event) | SV26 |
| ″ `PublishScheduleVersion_AfterItsStartDatePassed_ReturnsInPastStaysDraftAndCanBeDiscarded` | SV20 |
| ″ `PublishScheduleVersion_OnItsStartDate_Succeeds` | SV21 |
| ″ `PublishScheduleVersion_WithServiceWithdrawnSinceCreation_ReturnsNotEffectiveAndWritesNothing` | SV17, R17 |
| ″ **`PublishScheduleVersion_EmptyWhoseStartHasBecomeToday_ReturnsEmptyNotInFutureAndWritesNothing`** (created on 2026-10-01 for 2026-10-02; clock moved to 2026-10-02; stays `Draft`, no event; in force unchanged; a non-empty draft published on its start date still publishes) | **SV55 (publication), R50** |
| ″ **`PublishScheduleVersion_EmptyStartingTomorrow_PublishesAndCanBeCancelled`** | **SV56, R50** |
| ″ `PublishScheduleVersion_Empty_IsInForceWithNoServices` (then cancel it; V1 back with `runsOnDate = true`) | SV53, R9 |
| ″ `PublishScheduleVersion_WhenIndexViolatedBehindTheLock_Returns409EffectiveFromTaken` (direct-SQL published row with the same start date before the save) | R22, V8 |
| ″ `PublishScheduleVersion_WhenStatusChangesBehindTheLock_Returns409ChangedConcurrentlyAndWritesNothing` | R47 |
| `Timetable/CancelScheduleVersionHandlerTests.CancelScheduleVersion_BeforeItsStart_SetsCancelledKeepsRowsAndWritesOneEvent` | SV31 |
| ″ `CancelScheduleVersion_OnOrAfterItsStart_ReturnsAlreadyEffectiveAndWritesNothing` (theory: on 2026-10-01; clock moved to 2026-10-02) | SV33 |
| ″ `CancelScheduleVersion_WhenNotPublishedOrUnknown_ReturnsNotPublishedOrNotFound` (theory) | SV34 |
| ″ `CancelScheduleVersion_WhenStatusChangesBehindTheLock_Returns409ChangedConcurrently` | R47 |
| `Timetable/DiscardScheduleVersionHandlerTests.DiscardScheduleVersion_Draft_SetsDiscardedKeepsRowsAndWritesOneEvent` · `…_WhenNotDraft_ReturnsNotDraft` (theory) · `…_WhenStatusChangesBehindTheLock_Returns409ChangedConcurrently` | SV35, R47 |
| `Timetable/ScheduleVersionTransitionSqlTests.Transitions_UpdateOnlyStatusAndOneInstantAndNoChildRows` (captures SQL for publish, discard, cancel) | V1, V9, SV51 |
| `Timetable/ScheduleVersionQueryHandlerTests.GetScheduleVersion_ReturnsHeaderAndListedServicesWithCurrentValues` | SV2 |
| ″ `GetScheduleServiceTimes_ReturnsStopsInOrderWithStationsAndTimes` · `GetScheduleServiceTimes_ForAServiceNotInTheVersion_ReturnsNotInVersion` · `GetScheduleVersion_WithUnknownId_ReturnsNotFound` | SV2 |
| ″ `ListScheduleVersions_OrderedByNumberAndFilteredByStatus` | SV3 |
| ″ `ListScheduleVersions_WithPageSizeAbove200_ReturnsInvalidPageRequest` | SV48 |
| ″ `ScheduleVersions_AfterDiscardOrCancel_StayReadableAndUnchanged` | SV31, SV35, R25 |
| ″ `GetInForce_BeforeTheFirstVersion_ReturnsNotInForce` | SV27 |
| ″ `GetInForce_AcrossASupersession_ReturnsTheLatestStartOnOrBeforeTheDate` (theory) | SV28 |
| ″ `GetInForce_WithAnInsertedVersion_ReturnsItForItsRangeOnly` | SV29, R24 |
| ″ `GetInForce_ReportsRunsOnDatePerService` (theory: Saturday, Monday, after S5's `effectiveTo`) | SV30, R48 |
| ″ `GetInForce_AfterACancellation_ReturnsTheEarlierVersion` | SV32 |
| `Timetable/WithdrawServiceHandlerTests.WithdrawService_WhileAPublishedVersionListsIt_Returns422AndWritesNothing` · `WithdrawService_WithPastDateAndAListingVersion_ReturnsDateInPastFirst` | SV36, R19 |
| ″ `WithdrawService_FromTheStartOfTheVersionThatDropsIt_Succeeds` · `WithdrawService_TheDayBefore_Returns422` | SV37, R20 |
| ″ `WithdrawService_ListedOnlyByDraftDiscardedOrCancelledVersions_Succeeds` | SV38 |
| ″ `WithdrawService_ListedOnlyByASupersededPastVersion_Succeeds` (clock at 2026-09-01 to publish V0) | SV39 |
| ″ `WithdrawThenCancel_TheWithdrawalStaysAndInForceReportsNotRunning` (and a later withdrawal → `WithdrawalDoesNotShorten`) | SV54, R49 |
| `Timetable/ScheduleLockTests.PublishAndWithdraw_PublishFirst_WithdrawReturns422InPublishedVersion` · `PublishAndWithdraw_WithdrawFirst_PublishReturns422NotEffective` (forced) | **SV40** |
| ″ `CancelAndWithdraw_CancelFirst_WithdrawReturns422InPublishedVersion` · `CancelAndWithdraw_WithdrawFirst_BothSucceedAndServiceStaysWithdrawn` (forced) | **SV41**, R49 |
| ″ `PublishAndDiscard_SameDraft_ForcedBothOrders_OneSucceedsOtherNotDraft` (theory) | **SV42** |
| ″ `CreateScheduleVersion_InParallel_GetDistinctConsecutiveNumbers` (five scopes) | **SV42**, R7 |
| ″ **`PublishAndWithdraw_InParallelRepeatedly_NeverDeadlockAndEndInASerialOutcome`** (25 rounds, unforced) | **R46**, no-deadlock |
| ″ `Publish_WhileAWithdrawalHoldsTheServiceCodeLock_DoesNotWait` | R46, P9 |
| ″ `WithdrawService_TakesTheServiceCodeLockBeforeTheScheduleLock` (`RecordingLocks`) · `ScheduleHandlers_DoNotDependOnTheServiceCodeLock` (constructor reflection) | P9 |
| ″ `CreateService_WhileTheScheduleLockIsHeld_DoesNotWait` · `ScheduleReads_WhileTheScheduleLockIsHeld_DoNotWait` | R46, §5 |
| `DependencyInjectionTests.HandlerTypes_IncludesEveryHandlerDefined` (**changed**: 30 → **38**, eight handlers named) | ADR-0004 |

### `YCR.Infrastructure.Tests`

| Test | Credential | Spec |
|---|---|---|
| `Persistence/ScheduleModelTests.ScheduleModel_MapsTablesColumnsTypesAndKeys` (`date`, `smallint NULL`, `nvarchar(10)`, `ValueGeneratedNever`, no `rowversion`, `Status` the only concurrency token) | — | §7, R38, R47, V7 |
| ″ `ScheduleModel_HasExactlyTheDeclaredIndexes` (per table, including the filter and uniqueness; and the migration's `CreateIndex` operations) | — | P21, O1 |
| ″ `ScheduleModel_ForeignKeys_AreNoActionWithinTimetableWithoutNavigationsToService` | — | §7, E2 |
| ″ `ScheduleModel_DeclaresEveryCheckConstraint` | — | §7 |
| ″ `ScheduleModel_ConstraintNames_MatchTheModel` (`TimetableConstraints` vs the mapped index name) | — | P19 |
| ″ `UpMigration_TimetableCreateScheduleVersions_CreatesOnlyTheThreeTablesAndFourIndexes` · `DownMigration_…_DropsTheThreeTablesAndKeepsTheSchema` | — | O2, rollback |
| `Persistence/ScheduleMigrationTests.Migrate_FromF004Schema_CreatesScheduleObjectsGrantsAndSeed` (from `20260925072603_Security_TimetableGrants` with a service and its stops; service rows intact; no version rows; exact tables, checks, indexes (with filter), FKs `NO_ACTION`; the ten grant rows; ten `schedules.%` rows; **44** in total) | migrator | `docs/21` §Data, R37 |
| ″ `Migrate_DownToF004_RemovesScheduleObjectsGrantsAndSeedRowsAndKeepsServices` (and forward again) | migrator | rollback |
| `Persistence/DatabasePrivilegeTests.ApplicationCredential_HasExactlyTheScheduleGrants`: **presence** `ScheduleVersions` `SELECT`, `INSERT`, `UPDATE(Status)`, `UPDATE(PublishedAtUtc)`, `UPDATE(DiscardedAtUtc)`, `UPDATE(CancelledAtUtc)`; `ScheduleVersionServices`, `ScheduleStopTimes` `SELECT`, `INSERT`. **Absence** `DELETE`, table-level `UPDATE`, `ALTER`, `CONTROL` on all three; `UPDATE` of `Id`, `Number`, `NameEn`, `NameMy`, `EffectiveFrom`, `CreatedAtUtc`; `UPDATE` of every child column | `ycr_app` | **SV51**, R25 |
| ″ `ApplicationCredential_CanMoveAVersionsStatusButNotRewriteIt` (executes: a status update succeeds; `UPDATE … EffectiveFrom`, `UPDATE … Number`, `UPDATE ScheduleStopTimes`, `DELETE` on each table are denied; rows unchanged) | `ycr_app` | **SV51**, R32 |
| ″ `ApplicationCredential_ScheduleBackstops_RejectInvalidRows` (theory, direct SQL: minute −1 and 1440; arrival > departure; both times null; `Number` 0; `Status` `Live`; each status with the wrong instants; each non-UTC instant; a duplicate `Number`; a second `Published` row with the same `EffectiveFrom`) · `ApplicationCredential_ScheduleBackstops_AllowSharedStartDatesOutsidePublished` (two drafts, a cancelled and a published) | `ycr_app` | **SV52**, R22 |
| ″ `ApplicationCredential_CannotInsertAnEntryOrStopTimeNamingNoServiceOrStop` (FK error 547) | `ycr_app` | **SV52** |
| ″ `ApplicationCredential_CanTakeTheScheduleVersionsApplock` (no grant; same resource waits; a service-code lock neither blocks nor is blocked) | `ycr_app` | **SV51**, R46, V6 |
| ″ `ApplicationCredential_AttemptingDdl_IsDenied` (**changed**: three rows) | `ycr_app` | SV51 |
| ″ `ApplicationCredential_CannotWriteRolesOrGrants` (**changed**: 34 → 44) | `ycr_app` | OQ59 |
| `Persistence/StationModelTests.Model_WithUtcColumn_DeclaresItsCheckConstraint` (**changed**: four rows) | — | ADR-0018 |
| `Persistence/TimetableMigrationTests` (**changed**, items 6–7) | migrator | — |
| `Persistence/RouteMigrationTests` (**changed**, items 4–5) | migrator | — |
| `Identity/IdentitySeedTests` (**changed**, items 1–2) | migrator | OQ59, `docs/10` |

### `YCR.Api.Tests`

| Test | Mode | Spec |
|---|---|---|
| `Timetable/ScheduleVersionEndpointsTests.Post_WithValidRequest_Returns201WithLocationIdAndNumber` | TH | SV1 |
| ″ `Get_ReturnsScheduleVersionResponseRecordNotEntity` (exact JSON property sets, nested `services`) · `GetServiceTimes_ReturnsStopsWithHHmmTimesAndNulls` · `GetServiceTimes_ForAServiceNotInTheVersion_Returns404NotInVersion` | TH | SV2 |
| ″ `List_ReturnsPagedEnvelopeOrderedByNumberAndFiltersByStatus` · `List_WithUnknownStatus_Returns400ValidationFailed` | TH | SV3, Q2 |
| ″ `Numbers_AreContiguousAcrossDiscardAndCancel` | TH | SV4 |
| ″ `Post_WithMyanmarName_RoundTripsThroughGet` | TH | SV5 |
| ″ `Post_WithInvalidTimes_Returns422WithErrorCode` (theory: SV6, SV7, SV8, SV9, SV10, SV12) | TH | SV6–SV10, SV12 |
| ″ `Post_WithBadTimeFormat_Returns400InvalidTimetableTime` (theory: SV11's seven) · `Post_WithEdgeTimesOrFullCircuit_Returns201` | TH | SV9, SV11, SV13 |
| ″ `Post_WithServiceErrors_Returns422` (theory: SV14 ×2, SV15 ×4) · `Post_WithServiceEffectiveOnlyOnStartDate_Returns201` | TH | SV14–SV16 |
| ″ `Post_WithInvalidBody_Returns400CommonValidationFailed` (theory: `effectiveFrom` missing, `"2026-13-01"`, `"05/10/2026"`; `services` missing; `null` service; `serviceId` null, `"abc"`; `stopTimes` missing; `null` stop time; `position` missing, 0, −1) | TH | SV18, SV8 |
| ″ `Post_WithInvalidName_Returns400InvalidScheduleVersionName` (theory: blank, 101 characters, missing) | TH | SV18 |
| ″ `Post_WithStartBeforeToday_Returns422` · `Post_WithStartToday_Returns201` (listing a service effective on 2026-10-01; not empty, Amendment 2) | TH | SV19 |
| ″ `Post_WithNoServices_Returns201` · **`Post_WithNoServicesStartingToday_Returns422EmptyNotInFuture`** | TH | SV22, **SV55 (creation)** |
| ″ `Publish_ReturnsExpectedStatuses` (theory: SV20, SV21, SV23, SV25 ×4, **SV55 publication: created for tomorrow, clock moved to its start date**) | TH | SV20, SV21, SV23, SV25, SV55 |
| ″ `Publish_WithAPublishedStartDate_Returns409ThenAfterCancel204` | TH | SV24 |
| ″ `Publish_AfterAListedServiceWasWithdrawn_Returns422NotEffective` | TH | SV17 |
| ″ `InForce_ReturnsTheVersionForEachDate` (theory: SV27, SV28, SV29) · `InForce_ReportsRunsOnDate` · `InForce_WithAnEmptyVersion_Returns200WithNoServices` | TH | SV27–SV30, SV53 |
| ″ `Cancel_ReturnsExpectedStatuses` (theory: SV31, SV33 ×2, SV34 ×4) · `Cancel_ThenInForceReturnsTheEarlierVersion` | TH | SV31–SV34 |
| ″ **`EmptyVersionStartingTomorrow_PublishesThenCancels`** | TH | **SV56** |
| ″ `Discard_ReturnsExpectedStatuses` (theory: SV35 ×4) | TH | SV35 |
| ″ `Withdraw_WhileAPublishedVersionListsTheService_Returns422InPublishedVersion` · `Withdraw_WithAPastDate_Returns422DateInPastFirst` | TH | SV36 |
| ″ `DroppingAService_PublishWithoutItThenWithdraw_Returns204AndTheDayBefore422` | TH | SV37 |
| ″ `Withdraw_ListedOnlyByDraftDiscardedOrCancelled_Returns204` · `Withdraw_ListedOnlyByASupersededPastVersion_Returns204` | TH | SV38, SV39 |
| ″ `WithdrawThenCancel_ServiceStaysWithdrawnAndInForceSaysNotRunning` | TH | SV54 |
| ″ `AnyScheduleEndpoint_Anonymous_Returns401` (theory: eight endpoints) | TH | SV43 |
| ″ `AbsentScheduleEndpoints_AreNotRouted` (theory: `PATCH`/`PUT`/`DELETE /schedules/versions/{id}`, `PUT`/`DELETE /schedules/versions`) | TH | SV44 |
| ″ `WriteEndpoints_WithOnlySchedulesRead_Return403` (theory: create, publish, cancel, discard) | TH | SV45 |
| ″ `ScheduleEndpoints_WithOnlyServiceRouteOrStationPermissions_Return403` (theory: each of the six permissions × a read and a write) · `Withdraw_WithOnlySchedulePermissions_Returns403` | TH | SV45, R5 |
| ″ `List_WithPageSizeAbove200_Returns400InvalidPageRequest` · `InForce_WithMissingOrMalformedDate_Returns400ValidationFailed` (theory) | TH | SV48 |
| ″ `Post_WhenRequestTriesToSupplyActorFields_RecordsTheAuthenticatedActor` · `RefusedRequests_WriteNoEvent` (theory: a `422` create, publish, cancel) | TH | SV49 |
| ″ `InForceRoute_IsNotShadowedByTheIdRoute` | TH | §6, V5 |
| `Timetable/ScheduleVersionRequestLimitTests.Limits_Are2MiBAndTheThreeCaps` (the constants: 2,097,152; 250; 200; 10,000) | — | R41 |
| ″ `Post_WithBodyOverTheLimit_Returns413WithoutInternals` (theory: declared, chunked) | Kestrel | **SV47** |
| ″ `Post_WithMalformedJson_Returns400WithoutInternals` (theory ×3) | Kestrel | SV47 |
| ″ `Post_OneOverACap_Returns400ValidationFailed` (theory: 251 services; 201 stop times in one service; 10,001 in total) | Kestrel | SV47, Q1 |
| ″ `Post_ExactlyAtTheCaps_IsUnderTheLimitAndReachesTheHandler` (250 services, one with 200 stop times, 10,000 in total; ≤ 568,300 bytes; `422 Timetable.ScheduleServiceNotFound`) | Kestrel | SV47 |
| ″ **`Post_LargeBodyAbuse_IsRefusedBeforeAnyDatabaseWork`** (3 MiB declared and chunked → `413`; 10,001 stop times → `400`; no version row, no event) | Kestrel | SV47, security |
| `Identity/SchedulePermissionGrantTests.EveryRole_HasExactlyTheSeededScheduleRights` (theory over the eight roles: `GET /schedules/versions` 200 for all; create `201` only for `SystemAdministrator`, `RailwayAdministrator`, else `403`; nothing written on `403`) | Real | **SV46**, OQ59 |
| ″ `RailwayAdministrator_CreatesPublishesAndCancels_AuditedAsSchedulesManage` | Real | SV46, R39 |
| `Common/DeployedShapeTests.ProtectedEndpoint_Anonymous_Returns401BearerChallengeWithProblemDetails` (**changed**: eight rows) | **Unmodified** | SV43, `docs/21` §Tests |
| `Identity/MustChangePasswordTests.MustChangeSession_MayCallOnlyMeRefreshLogoutAndPassword` (**changed**: every route placeholder replaced) | Real | R26 (F-002), SV43 |
| `Identity/UserAdministrationEndpointTests.ListRoles_ReturnsEightRolesWithPermissions` (**changed**: 34 → 44) · `Identity/PasswordEndpointTests.Me_ReturnsUserNameRolesAndPermissions` (**changed**: + `SchedulesManage`, `SchedulesRead`) | Real | OQ59 |
| `Timetable/ServiceEndpointsTests.AbsentEndpoints_AreNotRouted` (**changed**: the `POST /api/v1/schedules/versions` row removed; Q4) | TH | F-004 R22 superseded |
| existing `ServiceEndpointsTests` (other rows), `ServiceRequestLimitTests`, `ServicePermissionGrantTests`, every F-001–F-003 suite | TH / Real / Kestrel | regression, unchanged |

### `YCR.ArchitectureTests`

No new rule. The existing rules now cover real schedule code non-vacuously: `ApplicationAndDomainRules_ForEveryBoundedContext_HaveNoSourceViolations` (Timetable depends only on its own domain, Common and `Network.Contracts`), `ApiRules_WithForbiddenTypes_DetectViolations` (the Api does not use `YCR.Domain.Timetable`), `AuditSnapshots_WithSourceAssemblies_AreValid` (the three new snapshot records). Unchanged.

### `YCR.IntegrationTests`

None new; `BootstrapAdministratorCommandTests` and `WorkerStartupTests` unchanged (the Worker resolves no schedule handler).

### Scenario coverage

Live scenarios: **56** (SV1–SV54, and Amendment 1's SV55 (rewritten by Amendment 2: refused at creation, and at publication once its start date has become today) and SV56). Every one maps to at least one named test:

| Scenario | Tests (project) |
|---|---|
| SV1 | Domain, Application, Api (TH, Real) |
| SV2 | Application (×3), Api (×3) |
| SV3 | Application, Api |
| SV4 | Domain, Application, Api |
| SV5 | Application, Api |
| SV6–SV10 | Domain, Application, Api |
| SV11 | Domain (×2), Application (×2), Api (×2) |
| SV12, SV13 | Domain, Application, Api |
| SV14–SV16 | Domain, Application, Api |
| SV17 | Domain, Application, Api |
| SV18 | Domain, Application, Api (×2) |
| SV19 | Domain, Application, Api |
| SV20, SV21 | Domain, Application, Api |
| SV22 | Domain, Application, Api |
| SV23, SV25 | Domain, Application, Api |
| SV24 | Application (×2, incl. the index backstop), Api |
| **SV26** | Application (parallel) |
| SV27–SV29 | Domain (timeline), Application, Api |
| SV30 | Domain (running day), Application, Api |
| SV31–SV34 | Domain, Application, Api |
| SV35 | Domain, Application, Api |
| SV36–SV39 | Domain, Application, Api |
| **SV40, SV41** | Application (forced, both orders each) |
| **SV42** | Application (forced both orders; parallel creates) |
| SV43 | Api (TH, Unmodified) |
| SV44 | Api |
| SV45 | Api (×3) |
| SV46 | Api (Real ×2) |
| SV47 | Api (Kestrel ×5), Application (at the caps) |
| SV48 | Application, Api (×2) |
| SV49 | Application, Api (×2) |
| SV50 | Application (every "writes nothing" case, and the number backstop) |
| SV51 | Infrastructure (×5), Application (SQL capture) |
| SV52 | Infrastructure (×3) |
| SV53 | Application, Api |
| SV54 | Domain, Application, Api |
| **SV55, SV56** | Domain, Application, Api |

**Scenario → test counts:** 56 of 56 live scenarios mapped; 3 have forced-order race tests on real SQL Server in both orders (SV40, SV41, SV42), plus the unforced no-deadlock test; 5 are proved on real Kestrel (SV47); 1 on the unmodified composition (SV43); 2 with real tokens (SV46, and SV1's audit actor).

**Rules without a dedicated scenario:** R1 (FACT), R2 and R43 (architecture tests; `GetScheduleServiceTimes` reads stations through `INetworkReader` only), R13/R14 (structural: `ScheduleStopTime` addresses stop positions; one entry per service — `CreateDraft_WithUnknownOrRepeatedService_…`), R25 (SV44, SV51, the grant tests), R26/R27/R32 (`ScheduleVersion_ExposesNoMutatorOtherThanItsTransitions`, SV44, SV51), R33 (`CreateScheduleVersion_AtYangonMidnight_…`, the existing `LocalCalendarTests`), R35 (not implemented; no placeholder), R36 (analysis; the digest tests), R37 (`Migrate_FromF004Schema_…` asserts no version rows), R38 (`ScheduleModel_MapsTablesColumnsTypesAndKeys`), R39 (every "one event" and "writes nothing" assertion), R40 (nothing to test), R42 (the error table through `ResultExtensions`, exercised by every API error test), R44 (SV43), R45 (the two precedence theories), R46 (the lock tests), R47 (the three backstop tests), R49 (SV41, SV54), R50 (SV55, SV56).

### Existing tests whose counts or lists change (the complete list)

Found with `grep` over `tests/` for `34`, `ThirtyFour`, `30` (handler count), the permission names, `schedules`, `Withdraw(`, `{id}`, and every `InlineData` list that enumerates endpoints, DDL or UTC columns:

| # | Test (file:line) | Change | Why |
|---|---|---|---|
| 1 | `IdentitySeedTests.Seed_ProducesExactlyEightRolesAndThirtyFourGrants` → renamed **`…FortyFourGrants`** (`tests/YCR.Infrastructure.Tests/Identity/IdentitySeedTests.cs:30,36`) | 34 → **44**; the ten schedule rows added to the expected set; class summary | The seed |
| 2 | `IdentitySeedTests.Seed_MatchesDocs10GrantTables` (same file, `:113`) | The heading list gains `## Schedule permission grants` (which must yield grants) | `docs/10` §Schedule permission grants |
| 3 | `DatabasePrivilegeTests.ApplicationCredential_CannotWriteRolesOrGrants` (`tests/YCR.Infrastructure.Tests/Persistence/DatabasePrivilegeTests.cs:407`) | 34 → **44** | The seed |
| 4 | `RouteMigrationTests.Migrate_FromF002Schema_CreatesRouteTablesConstraintsIndexesAndGrants` (`…/Persistence/RouteMigrationTests.cs:187`) | total 34 → **44** | Migrates to the latest migration |
| 5 | `RouteMigrationTests.Migrate_DownToF002_RemovesRouteObjectsGrantsAndSeedRowsAndKeepsStations` (`:235`) | 34 → **44** after migrating forward again (the post-rollback 14 unchanged; the rollback now runs F-005's three `Down()`s first) | Same |
| 6 | `TimetableMigrationTests.Migrate_FromF003Schema_CreatesTimetableObjectsGrantsAndSeed` (`…/Persistence/TimetableMigrationTests.cs:163, 241, 297`) | `timetable` tables `["ServiceStops", "Services"]` → the five tables; `ExpectedTimetableGrants` (a schema-wide query) 6 → **16** rows (F-004's six + F-005's ten, as two named arrays); total 34 → **44**. Its check, index and FK queries are filtered to F-004's two tables and stay as they are | Migrates to the latest migration |
| 7 | `TimetableMigrationTests.Migrate_DownToF003_RemovesTimetableObjectsGrantsAndSeedRowsAndKeepsNetwork` (`:288`) | After forward again: `ExpectedTimetableGrants` 16 rows, 34 → **44**; comment: the rollback runs F-005's three `Down()`s first (post-rollback assertions unchanged) | Same |
| 8 | `UserAdministrationEndpointTests.ListRoles_ReturnsEightRolesWithPermissions` (`tests/YCR.Api.Tests/Identity/UserAdministrationEndpointTests.cs:377`) | 34 → **44** | The seed |
| 9 | `AdministrationHandlerTests.ListRoles_ReturnsEightRolesWithPermissions` (`tests/YCR.Application.Tests/Identity/AdministrationHandlerTests.cs:478, 480`) | `SystemAdministrator` list + `schedules.manage`, `schedules.read`; `ReportingUser` list + `schedules.read` | The seed |
| 10 | `SessionHandlerTests.Resolve_ActiveSession_ReturnsUserRolesAndPermissionUnion` (`tests/YCR.Application.Tests/Identity/SessionHandlerTests.cs:186`) | + `schedules.manage`, `schedules.read` | The seed |
| 11 | `SessionHandlerTests.GetCurrentUser_ReturnsUserNameRolesAndPermissions` (`:261`) | + `schedules.manage`, `schedules.read` (`RailwayAdministrator`) | The seed |
| 12 | `PasswordEndpointTests.Me_ReturnsUserNameRolesAndPermissions` (`tests/YCR.Api.Tests/Identity/PasswordEndpointTests.cs:127`) | + `SchedulesManage`, `SchedulesRead` | The seed |
| 13 | `DependencyInjectionTests.HandlerTypes_IncludesEveryHandlerDefined` (`tests/YCR.Application.Tests/DependencyInjectionTests.cs:69`) | 30 → **38**; eight handlers named | Eight new handlers |
| 14 | `DatabasePrivilegeTests.ApplicationCredential_AttemptingDdl_IsDenied` (`:34–47`) | + three rows: `ALTER TABLE [timetable].[ScheduleVersions] ADD …`, `DROP TABLE [timetable].[ScheduleStopTimes]`, `DROP INDEX [UX_ScheduleVersions_EffectiveFrom_Published] ON [timetable].[ScheduleVersions]` | New objects |
| 15 | `StationModelTests.Model_WithUtcColumn_DeclaresItsCheckConstraint` (`tests/YCR.Infrastructure.Tests/Persistence/StationModelTests.cs:58–86`) | + four rows (`CK_ScheduleVersions_{Created,Published,Discarded,Cancelled}AtUtc_Utc`) | New UTC columns |
| 16 | `DeployedShapeTests.ProtectedEndpoint_Anonymous_Returns401BearerChallengeWithProblemDetails` (`tests/YCR.Api.Tests/Common/DeployedShapeTests.cs:24–36`) | + eight rows (every schedule endpoint) | New endpoints |
| 17 | `ServiceEndpointsTests.AbsentEndpoints_AreNotRouted` (`tests/YCR.Api.Tests/Timetable/ServiceEndpointsTests.cs:711–717`) | The `POST /api/v1/schedules/versions` row removed (5 → 4 rows); summary no longer says "no schedule versions" (**Q4**) | F-005 routes it; F-004 R22 superseded; SV44 and item 16 take over |
| 18 | `MustChangePasswordTests.MustChangeSession_MayCallOnlyMeRefreshLogoutAndPassword` (`tests/YCR.Api.Tests/Identity/MustChangePasswordTests.cs:47`) | Replace **every** `{name}` route placeholder with a GUID (a regex), not only `{id}`; the assertion (`403 Auth.PasswordChangeRequired` on every protected route) is unchanged and now also covers the eight schedule routes | `{serviceId}` would otherwise stay literal and route to `404` |
| 19–25 | `ServiceTests` (`tests/YCR.Domain.Tests/Timetable/ServiceTests.cs:368–465`): `Withdraw_FromFutureDate_SetsEffectiveToDayBeforeAndWithdrawnAt`, `Withdraw_FromToday_EndsYesterday`, `Withdraw_FromPastDate_ReturnsWithdrawalDateInPastAndChangesNothing`, `Withdraw_ThatWouldExtendOrKeepTheEnd_ReturnsDoesNotShortenAndChangesNothing`, `Withdraw_SecondTime_ShortensFurtherButNeverLengthens`, `Withdraw_OnOrBeforeEffectiveFrom_NeverRuns`, `Withdraw_WithNonUtcTime_Throws` | Their nine `Withdraw(...)` calls gain `ServiceScheduleCoverage.None`; **no assertion changes** | P10 signature |

Items 1–13 change an exact count or list; 14–16 extend a theory; 17 removes one theory row (Q4); 18 widens a substitution without changing the assertion; 19–25 are call sites. **Each assertion stays a strict equality; none is loosened.** Checked and **not** affected: `TimetableModelTests` (per-entity for `Service`/`ServiceStop`; the migration-operation assertions read F-004's own migration class), `DatabasePrivilegeTests.ApplicationCredential_HasExactlyTheTimetableGrants` (its schema-wide query counts permissions other than `SELECT`/`INSERT`/`UPDATE`, which F-005 does not grant; the rest names F-004's columns), every `WithdrawServiceHandlerTests` case (no version is published in them; see §The F-004 withdrawal change), `CreateServiceHandlerTests` (create does not take the new lock), `ServiceEndpointsTests` other rows, `ServiceRequestLimitTests`, `ServicePermissionGrantTests`, `RoutePermissionGrantTests`, `WrongPermissionTests`, `IdentityMigrationTests` (identity objects only), `MigrationBundleTests` (compares with the assembly's own list), `LedgerMigrationTests`, `ModuleInterfacesTests`, `CiWorkflowTests` (confirmed at step 10).

### Counts

**About 185 named tests** (a theory counts once): **about 160 new** — Domain ≈ 50, Application ≈ 60, Infrastructure ≈ 15, Api ≈ 38 (five on Kestrel, two with real tokens); **25 existing tests changed** (the table above; 19–25 mechanical). The exact figures are fixed at stage 4 and reported in `progress.md`. No test is disabled, skipped or deleted; item 17 removes one theory row, for the reason given and with hein's approval (Q4 ruling, 2026-09-27).

### Whole suite

`dotnet build YCR.sln` and `dotnet test YCR.sln` green with **0 skipped**; CI green on `origin` for `feature/F-005`, including `has-pending-model-changes` and the extended `api-smoke`.

---

## New packages

**None** (P26). EF Core, FluentValidation, ASP.NET Core, ArchUnitNET, Testcontainers and xUnit v3 are pinned in `Directory.Packages.props`; `SHA256`, `DateOnly` and `TimeZoneInfo` are in the BCL.

---

## Risks

| # | Risk | Likelihood | Mitigation |
|---|---|---|---|
| R-1 | A global lock serialises every timetable write; a large create holds it for its whole insert | Certain, by design (R46) | Only two administrator roles; rare operation; V2 measures the hold time and stops for hein above 5 s; waiters time out after 30 s with an opaque `500`, never a partial write. |
| R-2 | Deadlock between the service-code lock and the Timetable-wide lock | Low | Fixed order (P9) with a written proof; `ScheduleHandlers_DoNotDependOnTheServiceCodeLock`, the recording-order test, the forced `Publish_WhileAWithdrawalHoldsTheServiceCodeLock_DoesNotWait` and the 25-round no-deadlock test. |
| R-3 | Changing an approved, merged feature (F-004 withdrawal) breaks an F-004 guarantee | Medium | The F-004 checks run first and unchanged (§0.12); every existing F-004 test stays valid, only seven domain call sites change; the stage-8 amendment note in the F-004 spec at R21. |
| R-4 | The seed's count change breaks tests pinned at 34 | Certain, planned | All listed (items 1–13), changed in step 3, each exact. |
| R-5 | The 2 MiB body is 64 times F-004's limit | Medium | Sized from measurements (O6); three caps refuse oversized arrays before the handler; the abuse test proves no database work for refused bodies; the total cap (Q1, ruled 2026-09-27) keeps the limit at 2 MiB instead of 8 MiB. |
| R-6 | EF writes more than the four granted status columns on a transition, or touches child rows | Medium | V1/V9 and the SQL capture test fail under `ycr_app` first; the fix is the mapping, never the grant. |
| R-7 | `PublishedTimeline` loads every published version for each in-force read and each withdrawal | Low (one row per timetable change) | A covered scan of a small filtered index; if the count ever matters, the in-force read becomes a `TOP(1)` seek and the guard a bounded query, a local change with the domain tests unchanged. |
| R-8 | Framework `413`/`400` (malformed JSON, a fractional `position`) carry no `errorCode` | Known | Same as F-003 R-6/F-004 R-5; T-042 owns it; V10 records the binder's behaviour. |
| R-9 | Forced-order tests are flaky | Low | The order is fixed by the gate, not by timing; the one timing assertion can fail only when the lock is missing; unforced tests assert outcome sets only (F-003 R-9). |
| R-10 | The provisional rulings (OQ51–OQ60, Amendments 1–2) change when Myanma Railways answers | Medium | Each lives in one place: `ScheduleVersionInput`/`ScheduleVersion.CreateDraft` (OQ51, OQ52, OQ53, OQ56, Amendment 2), `ScheduleServiceFacts.IsEffectiveOn` (OQ54), `PublishedTimeline`/`ServiceScheduleCoverage` (OQ54, OQ55), `ScheduleVersion.Publish`/`Cancel` (OQ57, OQ58, OQ60, Amendment 1), the seed (OQ59). |
| R-11 | Mixed deployment: old code (no R19 guard) withdrawing while new code publishes | Low (single deployment unit) | Migrations and code ship together; §Rollback. |
| R-12 | A `\d` slips into the time regex and admits Myanmar digits | Low | P2 and a dedicated test row with Myanmar and Arabic-Indic digits. |

---

## Rollback and forward compatibility

**Migrations.** All three have working `Down()`s (§DB changes): the grants revoked, the ten seed rows deleted, the three tables dropped; F-004's tables, grants and the `timetable` schema, and every `network` object, are untouched. Once real versions exist, rolling back the schema destroys timetable history (the audit ledger keeps events about versions whose rows are gone), so production recovery is a restore (as F-002–F-004). The ledger only gains rows.

**Code.** The F-004 change is behavioural (withdrawal gains a refusal) but does not change any stored value or contract field; rolling the code back removes the refusal only.

**API.** Additive: eight new endpoints; the withdraw endpoint gains one `422` code (its OpenAPI already declares `422`). No client exists yet (no SPA, OQ22).

**Partially deployed instances.** Migrations run first, as before. **New code on an old database:** every schedule endpoint **and `POST /services/{id}/withdraw`** fail with `500` (missing tables), because the withdrawal now reads versions; everything else works. **Old code on a new database:** unaffected by the new tables; withdrawals by old code skip R19, so a mixed fleet could withdraw a service that a newly published version lists. Phase 1 deploys one unit (migrations, then the new code), so no mixed fleet runs; a future multi-instance deployment must drain old instances before the first version is published.

**Forward compatibility.** `PayloadVersion` 1. Stable version ids, insert-only rows and the digest keep OQ4's departure-bound ticket reconstructible (R36). ADR-0027 item 4 (time → instant) is left for its first consumer. `IX_ScheduleStopTimes_ServiceId_Position` serves a later "times of service S across versions" read. Running past midnight (OQ53) needs only a widened `CK_ScheduleStopTimes_Minutes` and `TimetableTime` range (ADR-0027 item 2).

---

## Steps

Each step ends with `dotnet test YCR.sln` green, **0 skipped**. No step leaves the branch red.

| # | Step | Ends green with |
|---|---|---|
| 1 | **Domain (new types only):** `TimetableTime`, `ScheduleVersionStatus`, `ScheduleVersionInput`, `ScheduleServiceFacts`, `ScheduleVersion` + children, `ScheduleVersionNumbering`, `PublishedTimeline`, `ServiceScheduleCoverage`, `ServiceRunningDay`, `OperatingDays.Includes`, the 21 `TimetableErrors` | Every new `YCR.Domain.Tests` row except the `Service.Withdraw` ones (step 7); existing tests unchanged |
| 2 | **Persistence:** `Permissions.SchedulesManage`/`SchedulesRead`; `ITimetableDbContext` + `YcrDbContext`; the three configurations; `TimetableConstraints`; migration `Timetable_CreateScheduleVersions`; item 6's `timetable` table list (two → five tables, because that test migrates to the latest migration). **V3, V7** | `ScheduleModelTests`, the four UTC rows (item 15), `UpMigration_…`/`DownMigration_…`; `has-pending-model-changes` clean; all existing tests |
| 3 | **Seed:** migration `Identity_SeedSchedulePermissionGrants`; changed-tests items 1–5 and 8–12, and the `44`s of items 6–7 | The seed tests; every count/list test at its new exact value |
| 4 | **Grants:** migration `Security_TimetableScheduleGrants`; the new `DatabasePrivilegeTests` cases (except the applock one), item 14's DDL rows, `ScheduleMigrationTests` upgrade and down, items 6–7's 16 grant rows | `ApplicationCredential_HasExactlyTheScheduleGrants`, `…CanMoveAVersionsStatusButNotRewriteIt`, `…ScheduleBackstops_*`, `…CannotInsertAnEntryOrStopTimeNamingNoServiceOrStop`, `Migrate_FromF004Schema_…`, `Migrate_DownToF004_…` |
| 5 | **Lock:** `IScheduleVersionsLock`, `SqlServerScheduleVersionsLock`, DI; `GatedScheduleVersionsLock`, `RecordingLocks`. **V6** | `ApplicationCredential_CanTakeTheScheduleVersionsApplock` |
| 6 | **Application writes:** audit actions, subject, snapshots, `ScheduleStopTimesDigest`; `CreateScheduleVersion`, `PublishScheduleVersion`, `DiscardScheduleVersion`, `CancelScheduleVersion`; item 13 moves 30 → 34 here (four write handlers named) and 34 → 38 at step 8, exact at each step. **V1, V2, V8, V9** | Create/publish/cancel/discard handler tests, the digest tests, the transition SQL test, SV26, SV42; V2's timing in `progress.md` |
| 7 | **F-004 withdrawal change:** `Service.Withdraw(…, coverage)` (items 19–25); `WithdrawServiceHandler` (lock order, coverage); `ServiceInPublishedScheduleVersion` | The new `ServiceTests` and `WithdrawServiceHandlerTests` rows (SV36–SV39, SV54); `ScheduleLockTests` (SV40, SV41, no-deadlock, order, non-interference); every existing F-004 test |
| 8 | **Application reads:** `GetScheduleVersion`, `GetScheduleServiceTimes`, `ListScheduleVersions`, `GetScheduleVersionInForce`, DTOs, `ScheduleReadMapping`; DI count **38** | `ScheduleVersionQueryHandlerTests` (SV2, SV3, SV27–SV32, SV48, SV53) |
| 9 | **API:** `ScheduleVersionContracts` (+ validators, caps), `ScheduleVersionEndpoints` (+ 2 MiB limit), `Program.cs`, the `ServiceEndpoints` comment; items 16–18. **V4, V5, V10** | Every `YCR.Api.Tests` row, including `DeployedShapeTests` (Unmodified), `ScheduleVersionRequestLimitTests` (Kestrel), `SchedulePermissionGrantTests` (Real), the changed must-change test; all F-001–F-004 API suites |
| 10 | **`YCR.Api.http` and CI:** the schedule section; the `api-smoke` checks. Push `feature/F-005` and **prove a green GitHub Actions run** (build, tests, `has-pending-model-changes`, `api-smoke`, gitleaks; trunk-only filter untouched); record the run URL and the test counts in `progress.md` | **A green GitHub Actions run on `origin` for `feature/F-005`**, 0 skipped |

### Stage 8 documentation (not stage 4)

- **`docs/07`:** a new "F-005 timetable-version tables, constraints, indexes and grants" section — the three tables, every object in §DB changes, the two convention indexes and why they are declared, the index table, the three migration names, the grants table with its absences, the digest's canonical form, and the Timetable-wide lock. §F-004 "The service-code lock": withdrawal also takes `timetable.ScheduleVersions`, after the code lock; ADR-0026 now **Accepted** (the text still says Proposed). §Core tables: `ScheduleVersions` implemented.
- **`docs/08`:** replace the two proposed `/schedules/versions` lines in §Initial resources with "Implemented in F-005 — timetable versions" (endpoints, contracts, error codes, check orders, the 2 MiB limit and the three caps); §Implemented in F-004: the withdraw row gains `422 Timetable.ServiceInPublishedScheduleVersion` and the withdrawal check order.
- **`docs/20` §6:** the list of ADR-0026 uses gains `timetable.ScheduleVersions` (F-005 R46), with the rule that a handler taking two locks takes the keyed lock before the whole-set lock, and ADR-0026's status corrected to Accepted. **C4:** `docs/20` §4 cites ADR-0002 for timetable versioning; replace with the binding sources (`docs/07` §Rules, `docs/08`, F-005 R26/R32).
- **`docs/10`:** record the seed migration's name in §Schedule permission grants (no grant change).
- **`docs/glossary.md`:** `ScheduleVersion` (with "timetable version" as the same thing, C3), version number, stop time, timetable time (`HH:mm`, minutes after local midnight), in force, applies, publish, cancel, discard, network-wide suspension (an empty version); the `Withdrawal` entry gains the R19 refusal. Every Myanmar term stays **OPEN QUESTION**.
- **`docs/features/F-004-service-management/spec.md`:** a dated amendment note at R21 pointing to F-005 R19 and spec §0.12 (the Approved text itself is not rewritten), also noting that R22's "no `/schedules/versions`" is superseded by F-005 (Q4).
- **`docs/19`:** add the **Q2 ruling (spec R49)** to the resolution blocks of **OQ54** and **OQ58** (T-054 ruling 2), keeping the file's line endings. (OQ60's block already carries Amendments 1 and 2, written with the spec amendments at T-054.)
- **`docs/business/mr-questions-pack.md`:** add **OQ51–OQ60**, each marked as provisionally ruled (with Amendments 1–2 under OQ60), in the pack's routing format (Network Operations / Planning).
- **C5 — ADR-0024:** a Proposed-stage note refreshing its Context (`PATCH /routes`, `/trains`, `/services` were removed by OQ38, OQ42, OQ48; F-005 does not use it, OQ56); it stays Proposed.
- **ADR-0027:** a Follow-up line recording the name `TimetableTime` (P2) and that item 4's instant function has no consumer yet.
- **README:** no change (it lists no endpoints).

---

## Stop point

**Stage 3 ⛔ — passed.** hein ruled on Q1–Q5 and approved this plan on 2026-09-27 (revision 2). Stage 4 (IMPLEMENT) is a separate task, **T-055**, which starts from this revision. No production code is written in this stage.

---

## Review history

**Revision 1 — 2026-09-27, claude (T-054).** First draft, against the spec at `1469a92` plus Amendment 1 (hein, 2026-09-27, T-054 ruling 1), written in the same commit. Applies T-054 rulings 2 (stage-8 `docs/19` OQ54/OQ58 entries) and 3 (Asia/Yangon timestamps).

**Revision 2 — 2026-09-27 12:48 Asia/Yangon, claude (T-054). Approved (hein, 2026-09-27).** Applies hein's rulings on Q1–Q5 (§Rulings on Q1–Q5): Q1 the total cap of 10,000 stop times with the 2 MiB limit, 250 services and 200 stop times per service, all REQUIRED CONTROLs; Q2 an unknown `status` → `400 Common.ValidationFailed`; Q3 spec **Amendment 2** (committed just before this revision, with `docs/19` OQ60): an empty draft whose start date is not later than today is refused at creation with `422 Timetable.EmptyScheduleVersionNotInFuture` — P3, P4 (new check 5; the per-service checks renumbered 6–13), P6, §Domain changes, the create row of §Endpoint inventory, `.http`, `api-smoke` step 4, and the SV55 test rows (creation and publication-time cases) follow; Q4 the F-004 absent-endpoint row removed (item 17); Q5 `timetable.ScheduleVersions`. The Questions section is replaced by the rulings table; the stop point and status are updated. No decision P1–P26 changes.

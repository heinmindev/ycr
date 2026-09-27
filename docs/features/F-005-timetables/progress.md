# Progress: F-005 Timetables

Append-only. Newest entry at the bottom. Never edit or delete earlier entries.

---

## 2026-09-26 07:57 Asia/Yangon — claude — T-053

**Stage:** 1–2 (DISCOVER + SPECIFY) — stopped at the stage-2 ⛔.
**Commit:** spec commit on `feature/F-005` (SHA recorded in the `TASKS.md` T-053 row); this entry is in the same commit.

**Done this session:**
- Claimed T-053 (`claim/T-053`, ledger commit `60d624a` on `main`); worktree `D:\MR\ycr-F-005` on `feature/F-005` from `main` `60d624a`.
- Read: workflows 01/02, templates, `docs/00`–`docs/05`, `docs/07`, `docs/08`, `docs/10`–`docs/12`, `docs/18`, `docs/19` (all), `docs/20`, `docs/21`, glossary, ADR-0002/0004/0006/0012/0013/0017/0018/0019/0021/0024/0025/0026, `docs/business/mr-questions-pack.md` §OQ4, the F-004 spec, plan (headings, P11, Amendment 1, R35) and progress, and the `Timetable` code (`Service`, `ServiceStop`, `EffectivePeriod`, `OperatingDays`, `ITimetableDbContext`, `IServiceCodeLock`, `ILocalCalendar`, `INetworkReader`).
- Wrote `docs/features/F-005-timetables/spec.md` (Status: Draft): §0 discovery (D1–D26, gaps G1–G13, contradictions C1–C5, engineering proposals E1–E14, scope options SC1–SC3, worked example W1–W9, **§0.10 Rulings needed from hein**); §1–§9 against worked example W inside SC2, every business rule labelled OPEN QUESTION until ruled; Blocked behaviour.
- Added **OQ51–OQ59** to `docs/19-open-questions.md`, each with a **BLOCKS:** line.
- Added **ADR-0027 "Timetable times of day" (Proposed)** and its row in `docs/decisions/README.md`.
- Not edited (as instructed): `docs/business/mr-questions-pack.md`, `docs/glossary.md`, `docs/10`; no production code.

**New OPEN QUESTIONs for hein (to add to `docs/business/mr-questions-pack.md` later):**
- OQ51 — what a timetable version is: scope (network / route / service), identity, effective dates.
- OQ52 — stop times: arrival/departure per stop, first/last stop, precision, dwell, ordering, passing times, per-weekday times.
- OQ53 — may a service run past midnight; notation; maximum journey length.
- OQ54 — version dates vs service effective periods; withdrawn services; withdrawing a service listed in a published version; "withdraw + recreate" vs new version (C1).
- OQ55 — how versions follow one another: one in force, supersession, overlap, gaps, insertion, history.
- OQ56 — how drafts are prepared: created whole, built up, or edited; discard; several drafts.
- OQ57 — who publishes; second approver; lead time; past start date.
- OQ58 — cancelling or withdrawing a published version.
- OQ59 — role grants for `schedules.manage`, `schedules.read` (and `schedules.publish` if split).

**Evidence:** docs only; no build or test run (no code changed). `git diff --stat main` on the branch shows only the four doc files.

**Next step (exact):** hein rules on spec §0.10 (SC, OQ51–OQ59, H, ADR-0027, ADR-0024 if OQ56 (c), E1–E14). Then a revision session applies the rulings to the spec and adds `docs/19` resolution blocks (and `docs/10` §Schedule permission grants if OQ59 is ruled). Stage 3 (PLAN) does not start before the spec is Approved.

**Blockers / open questions:** `approval` (stage-2 ⛔); business: OQ51–OQ57 and OQ59 (OQ58 under SC1).

**State of the branch:** docs only; committed and pushed.

---

## 2026-09-26 08:22 Asia/Yangon — claude — T-053

**Stage:** 2 (SPECIFY) — rulings applied; stopped at the stage-2 ⛔ again.
**Commit:** rulings commit on `feature/F-005` (SHA recorded in the `TASKS.md` T-053 row); this entry is in the same commit.

**Done this session:**
- Applied hein's rulings of 2026-09-26 (T-053 row at `417c400`): SC → SC2 + cancel before effect; OQ51–OQ59; H; ADR-0027 Accepted (max 1439); E1–E14 accepted except E8 (one Timetable-wide ADR-0026 lock for publish, cancel and service withdrawal); E10 numbers at PLAN (≈200 services × 40 stops); C4/C5 to stage 8.
- `spec.md`: Status "Draft — all rulings applied, awaiting approval"; §0.10 rulings table (asked table kept); consequences; §0.11 raised items (OQ60, Q2); new §0.12 spelling out the F-004 withdrawal change; closure notes in §0.3/§0.4/§0.5/§0.8/§0.9; §1–§9 and Blocked behaviour rewritten for the ruled design (R1–R48, SV1–SV52).
- `docs/19`: resolution blocks under OQ51–OQ59 (F-004 wording); new **OQ60** (may a version list no services?).
- `docs/10`: `schedules.read` in the inventory; `## Schedule permission grants — resolved (OQ59)` in the service-section bullet format; intro and service-section cross-references updated.
- ADR-0027 Status Accepted (Decision items 1–3 now state minutes and 0–1439); README row Accepted.
- Not touched: glossary, MR questions pack, code, ADR-0024.

**For hein:**
- **OQ60** (blocking, R9/SV22): may a version list no services?
- **Q2** (confirmation, SV41): withdraw S from D, then cancel the version starting on D → both succeed under the rulings' letter, and the earlier version applies again listing S (which doesn't run). Confirm (a), or choose (b) refuse the cancel (and the analogous publish).
- Engineering choices made while applying (hein may amend): numbers contiguous via max+1 under the lock (R7); start ≥ today also checked at creation (R31); services re-checked at publish (R17); `runsOnDate` in the in-force read (R48); create and discard also take the lock (R46).
- Add OQ51–OQ60 to `docs/business/mr-questions-pack.md` (OQ51–OQ59 as provisionally ruled).

**Evidence:** docs only; no build or test run. `IdentitySeedTests` parses only the station/route/service headings, so the new `docs/10` section does not change today's test; stage 4 adds the heading (spec §7).

**Next step (exact):** hein rules on OQ60 and Q2 and approves the spec; then a revision (if needed) and T-053 → done; stage 3 (PLAN) is a separate task.

**Blockers / open questions:** `approval` (stage-2 ⛔); business: OQ60.

**State of the branch:** docs only; committed and pushed.

---

## 2026-09-27 05:00 Asia/Yangon — claude — T-053

**Stage:** 2 (SPECIFY) — complete; spec **Approved (hein, 2026-09-26)**.
**Commit:** approval commit on `feature/F-005` (SHA recorded in the `TASKS.md` T-053 row); this entry is in the same commit.

**Done this session:**
- Applied hein's final rulings (T-053 row at `97b5d7d`): **OQ60 → (b)**, a version may list no services (network-wide suspension; corrected by cancel-before-effect or a later version); **Q2 → (a)**, a withdrawal survives the cancellation of the version that made it legal; the five engineering choices confirmed (numbers highest+1 under the lock; past start date refused at creation; services re-checked at publish; create and discard take the Timetable-wide lock; `runsOnDate` in the in-force read).
- `spec.md`: Status "Approved (hein, 2026-09-26)"; §0.10 "Final rulings at approval" table and approval line; §0.10 consequence for an empty version; §0.11 outcome; R9 rewritten (PROVISIONAL RULING OQ60); new **R49** (Q2); the five choices relabelled ENGINEERING DECISION (tech lead, hein, 2026-09-26) in R7, R17, R31, R46, R48; SV22 now `201`; new **SV53** (publish an empty version, then in-force reads) and **SV54** (withdraw, then cancel; S stays withdrawn, `runsOnDate = false`); SV41 second order made definite; §0.12, §9, Blocked behaviour and Notes for the next stage updated. No blocking OPEN QUESTION left; OQ47 stays a known limitation.
- `docs/19`: resolution block under OQ60.
- Not touched: code, glossary, MR questions pack, `docs/10`, ADRs.

**Evidence:** docs only; no build or test run.

**Next step (exact):** T-054 — stage 3 (PLAN) against the Approved spec, in a separate session.

**Blockers / open questions:** none for F-005. Still for hein: add OQ51–OQ60 to `docs/business/mr-questions-pack.md`.

**State of the branch:** docs only; committed and pushed.

Correction (claude, T-054, 2026-09-27 12:32 Asia/Yangon): the entry stamped "2026-09-27 05:00 Asia/Yangon" above was written at **11:26 Asia/Yangon** (05:00 was UTC from a Windows time-zone command; commit `f2dda58`, +0630).

---

## 2026-09-27 12:32 Asia/Yangon — claude — T-054

**Stage:** 3 (PLAN) — plan written; stopped at the stage-3 ⛔ for hein's approval.
**Commit:** the plan commit on `feature/F-005` (SHA recorded in the `TASKS.md` T-054 row); this entry is in the same commit.

**Done this session:**
- **Spec Amendment 1** (hein, 2026-09-27; T-054 ruling 1; provisional tech-lead ruling, not a Myanma Railways answer): a version that lists no services may only be published with a start date later than today (`422 Timetable.EmptyScheduleVersionNotInFuture`). Spec: Status "Approved (hein, 2026-09-26), amended by Amendment 1"; §0.10 "Amendment 1 after approval" table; R9 pointer; new **R50**; new **SV55** (empty version starting today refused at publication) and **SV56** (starting tomorrow published and cancelled); §5 publish guard; §6 publish error list; §9 known limitation; Blocked behaviour row. `docs/19` OQ60: the Amendment 1 paragraph under the resolution block (LF kept).
- **`plan.md` revision 1**, from `docs/templates/plan.md` on the F-004 model: P1–P26; the `ScheduleVersion` aggregate, `TimetableTime` (ADR-0027), the R45 check order; `PublishedTimeline` for R19/R21; the F-004 withdrawal change (`Service.Withdraw` gains the coverage; F-004 checks first); the Timetable-wide lock `timetable.ScheduleVersions` with the order "service-code lock, then the Timetable-wide lock" and its deadlock-freedom proof; three migrations (tables and checks, the filtered unique index, grants, seed 34 → 44) with rollback; the in-force read's SQL; eight endpoints with a 2 MiB create limit and caps 250 / 200 / 10,000 from measured sizes; audit and the digest's canonical form; the test plan mapping all 56 live scenarios; the 25 existing tests that change; ten steps ending in a green GitHub Actions run; the stage-8 docs list.
- Verified at PLAN in a scratch app (outside the repo) against the real F-004 model: EF convention indexes (exactly `IX_ScheduleVersionServices_ServiceId`, `IX_ScheduleStopTimes_ServiceId_Position`), the migration diff and its reverse, the filtered-index DDL, the query SQL (literal `N'Published'`), and body sizes (554,267 bytes compact at the caps; 443,667 for 200 × 40).
- Found two existing tests the spec did not name: `MustChangePasswordTests.MustChangeSession_MayCallOnlyMeRefreshLogoutAndPassword` (substitutes only `{id}`) and `ServiceEndpointsTests.AbsentEndpoints_AreNotRouted` (asserts `POST /schedules/versions` is absent).

**Evidence:** docs only; no build or test run of the repository. Scratch check: `dotnet run` of a console app referencing `YCR.Infrastructure` (net10.0, EF Core 10.0.12). Files: `spec.md`, `plan.md`, `progress.md`, `docs/19-open-questions.md`.

**Next step (exact):** hein answers plan Q1–Q5 and approves the plan (or asks for a revision); then T-054 → done and stage 4 is a separate task.

**Blockers / open questions:** `approval` (stage-3 ⛔). Plan questions Q1 (total stop-time cap 10,000), Q2 (unknown `status` filter), Q3 (Amendment 1 at creation), Q4 (remove the F-004 `AbsentEndpoints` row), Q5 (lock name). No new business OQ.

**State of the branch:** docs only; committed and pushed.

---

## 2026-09-27 12:52 Asia/Yangon — claude — T-054

**Stage:** 3 (PLAN) — **plan Approved (hein, 2026-09-27)**; stage 3 complete.
**Commits:** spec Amendment 2 at `e2cd3c5`; approved plan (revision 2) at `5901d53`; this entry is the next commit on `feature/F-005`.

**Done this session:**
- **hein's rulings on plan Q1–Q5** (2026-09-27, recorded in the `TASKS.md` T-054 row) applied. Plan §Questions for hein replaced by a dated §Rulings on Q1–Q5 table; Status "Approved (hein, 2026-09-27)", revision 2; stop point and review history updated.
- **Q1:** total cap 10,000 stop times per version, 2 MiB body limit, 250 services, 200 stop times per service — REQUIRED CONTROLs; the size calculation stays in the plan. **Q2:** unknown `status` → `400 Common.ValidationFailed`. **Q4:** the F-004 `AbsentEndpoints_AreNotRouted` row for `POST /api/v1/schedules/versions` is removed (item 17); the other rows stay. **Q5:** lock resource `timetable.ScheduleVersions`. These were already the plan's proposals; only wording changed.
- **Q3 → spec Amendment 2** (provisional tech-lead ruling, not a Myanma Railways answer): an empty draft whose `EffectiveFrom` is not later than today is refused at creation with `422 Timetable.EmptyScheduleVersionNotInFuture`; nothing written. Spec: Status "amended by Amendments 1–2"; §0.10 Amendment 2 table; R9 pointer; R45 (the empty check after the start-date check); R50 (creation and publication); SV55 rewritten (creation refusal; publication case for a draft created for tomorrow and published once that date is today); §5 create guard; §6 create errors; §9; Blocked behaviour. `docs/19` OQ60: Amendment 2 paragraph (LF kept).
- Plan follow-ups for Amendment 2: P3, P4 (new check 5; per-service checks renumbered 6–13), P6, §Domain changes, create row of §Endpoint inventory, `.http`, `api-smoke` step 4 (now a creation refusal), new test rows (`Parse_WithNoServicesStartingToday_…`, `CreateScheduleVersion_WithNoServicesStartingToday_…`, `Post_WithNoServicesStartingToday_…`), the publication-time SV55 rows renamed to "start has become today", and the SV19 "start today" rows noted as non-empty.

**Evidence:** docs only; no build or test run. Files: `spec.md`, `plan.md`, `progress.md`, `docs/19-open-questions.md`.

**Next step (exact):** T-055 (stage 4, implement) from plan revision 2 at `5901d53`, step 1. Not started in this session.

**Blockers / open questions:** none blocking. Reported to hein: spec §6 still lists only `400 Timetable.InvalidPageRequest` for the list endpoint (Q2 adds `400 Common.ValidationFailed`), and spec R41/SV47 name only the two array caps (Q1 adds the per-version total cap); the plan carries both, the spec text was not changed without a ruling.

**State of the branch:** docs only; committed and pushed.

---

## 2026-09-27 13:36 Asia/Yangon — claude — T-055

**Stage:** 4 (IMPLEMENT) — plan step 1 done.
**Commits:** spec Amendment 3 at `aa10448`; step 1 in the commit carrying this entry.

**Done this session:**
- Claimed T-055 (`claim/T-055`; ledger commit `1cc86c5` on `main`). Worktree at `b257fb2`; spec Approved with Amendments 1–2, plan revision 2 Approved (`5901d53`) — confirmed.
- **Spec Amendment 3** (hein, 2026-09-27, T-055; ENGINEERING DECISIONS, no business rule change) at `aa10448`: §6 list endpoint `400 Common.ValidationFailed` for an unknown `status`; R41/SV47 the per-version total of 10,000 stop times; SV19/SV21 "a version listing a service effective on its start date". Recorded in §0.10; Status "amended by Amendments 1–3".
- **Environment (not a plan deviation):** the machine's SDK had been updated to 10.0.401 at 12:23 today, so `global.json`'s 10.0.302 (`latestPatch`) no longer resolved. SDK 10.0.302 installed side by side into `C:\dn302` with Microsoft's `dotnet-install.ps1` (no machine-wide change, `global.json` untouched); every command runs with `DOTNET_ROOT=C:\dn302` and that `dotnet` first on `PATH`. Docker Desktop was stopped and was started. Baseline at `aa10448`: `dotnet test YCR.sln` **1161/1161, 0 skipped**.
- **Step 1 — domain (new types only):** `TimetableTime` (P2; `\A…\z` and `[0-9]`), `ScheduleVersionStatus`, `ScheduleVersionInput` (P3 checks 2–5), `ScheduleServiceFacts` (`IsEffectiveOn`, R17), `ScheduleVersion` + `ScheduleVersionService` + `ScheduleStopTime` (P1, P4 checks 6–13, P7 transitions), `ScheduleVersionNumbering` (P5), `PublishedTimeline` (P11), `ServiceScheduleCoverage` (P10), `ServiceRunningDay` (P12), `OperatingDays.Includes`, the 21 `TimetableErrors` (2 Validation, 3 NotFound, 2 Conflict, 14 BusinessRule).
- Tests: `TimetableTimeTests` (5), `ScheduleVersionInputTests` (8), `ScheduleVersionTests` (35), `ScheduleVersionNumberingTests` (1), `PublishedTimelineTests` (5), `ServiceScheduleCoverageTests` (2), `ServiceRunningDayTests` (1), `OperatingDaysTests.Includes_ReturnsWhetherTheDayIsAnOperatingDay` (1) — 139 cases. No existing test changed.
- **RED confirmed:** (1) with the eleven new files moved aside and `OperatingDays`/`TimetableErrors` stashed, the tests did not compile (62 × CS0246, 18 × CS0103); (2) with the real types but stubbed logic (empty-version and past checks, R17 effective check, stop-time checks 9–13, publish checks, cancel `≤`, successor `>`, weekday check, numbering) → **77 of 353 failed**; (3) a targeted mutation of the time pattern to `^([01]\d|2[0-3]):[0-5]\d$` → 4 failed (Myanmar and Arabic-Indic digits in a `\d` position, and a trailing newline). The mixed-digit rows were added for that purpose (the all-Myanmar rows could not catch it: the first hour digit is `[01]`). All stubs restored byte-for-byte from a copy (`cmp`).

**Deviations:**
- **V1 (mechanical):** `ScheduleVersionInput.Parse` takes the services as two small raw records, `ScheduleServiceText(ServiceId, StopTimes)` and `ScheduleStopTimeText(Position, Arrival, Departure)` (times still text), in the same file as the plan's three input records. The plan names the method's parameters but not a type for unparsed services; the Domain cannot take Application's command type.
- **V2 (mechanical):** the time pattern is anchored with `\A…\z`, not `^…$` as written in P2: in .NET `$` also matches before a final `\n`, so `"06:00\n"` would be accepted. Same accepted language otherwise; a test row pins it.
- **V3 (mechanical):** `FromMinutes_OutsideTheDay_Throws` reaches the `internal` method by reflection (the Domain grants `InternalsVisibleTo` only to Infrastructure); no assembly attribute was added.

**Evidence:** `dotnet test tests/YCR.Domain.Tests` 356/356; `dotnet test YCR.sln` **1300 total, 1300 passed, 0 failed, 0 skipped**; build 0 warnings.

**Next step (exact):** plan step 2 — persistence (permissions, context, three configurations, `TimetableConstraints`, migration `Timetable_CreateScheduleVersions`, `ScheduleModelTests`, item 15, item 6's table list; V3, V7).

**Blockers / open questions:** none.

**State of the branch:** step 1 committed and pushed; step 2 files in the working tree, uncommitted.

---

## 2026-09-27 17:02 Asia/Yangon — claude — T-055

**Stage:** 4 (IMPLEMENT) — plan step 2 done.
**Commit:** step 2 at `a564192` (pushed); this entry is the next commit.

**Done:**
- **Step 2 — persistence:** `Permissions.SchedulesManage`/`SchedulesRead` (OQ59 comment); `ITimetableDbContext` and `YcrDbContext` gain `DbSet<ScheduleVersion>`; `ScheduleVersionConfiguration`, `ScheduleVersionServiceConfiguration`, `ScheduleStopTimeConfiguration`; `TimetableConstraints`; migration **`20260927070804_Timetable_CreateScheduleVersions`**, EF-generated and reviewed by hand against plan §DB changes 1: three tables, seven checks, four `NO ACTION` keys (all inside `timetable`), exactly four indexes (`UX_ScheduleVersions_Number`, the filtered `UX_ScheduleVersions_EffectiveFrom_Published` `WHERE [Status] = N'Published'`, `IX_ScheduleVersionServices_ServiceId`, `IX_ScheduleStopTimes_ServiceId_Position`); no `EnsureSchema`, no raw SQL; `Down()` drops the three tables children first. The snapshot diff is additions only. `has-pending-model-changes`: clean (**V3**). **V7 confirmed:** `TimetableTime?` maps to `smallint NULL` through a converter; nulls stay `NULL`.
- Tests: `ScheduleModelTests` (7: columns and types, the exact index set with filter and uniqueness in the model and in the migration's `CreateIndex` operations, the four FKs in model and migration, every check, the constraint name, up and down operations). Changed tests: **item 15** (`StationModelTests` + four UTC rows) and **item 6's table list** (two → five tables).
- **RED confirmed:** (1) the tests did not compile without the migration (2 × CS0246 `Timetable_CreateScheduleVersions`); (2) with the three configurations moved aside, the targeted classes (`StationModelTests`, `TimetableMigrationTests`) → 22 of 24 failed. Two test-side fixes on the way to green, neither loosening an intent: a `rowversion` absence scoped to the three schedule tables (it had caught `StaffUser`'s legitimate `RowVersion`), and V7's provider type read from the converter.

**Deviations:**
- **V4 — killed and contaminated runs.** The first full run for step 2 was contaminated: the test fixture builds the migration bundle from the source tree when the tests start, and by then I had restored uncommitted step 3–4 files into the tree, so the bundle contained the seed and grants migrations (10 failures, all "44 grants where 34 expected" or the migration list). A clean re-run in a separate worktree (`D:\MR\ycr-F-005-verify`) was then **killed by Claude Code because the machine was critically low on memory** (two checkouts building and testing at once). With hein's go-ahead (2026-09-27): the verify worktree was removed, the steps 3–6 work stashed (`T-055 WIP steps 3-6 (not yet verified)`), and the suite run once on the clean `a564192` tree with `--max-parallel-test-modules 1` (the MTP switch; `-m:1` is not an MSBuild switch under `dotnet test` with Microsoft.Testing.Platform and ran zero tests).
- **V5 — worked ahead of the per-step rule.** Steps 3–6 code and tests were written while step 2's full run was still pending. No code was committed out of order: step 2 was committed alone, and the later work is stashed and will be verified one step at a time (RED, then GREEN, then the full suite, then commit and push).
- **V6 — side-by-side SDK.** The machine's default SDK is now 10.0.401 (updated at 12:23 today); `global.json` pins 10.0.302 with `latestPatch`, which 10.0.401 does not satisfy. SDK 10.0.302 is installed side by side in `C:\dn302` (Microsoft's `dotnet-install.ps1`, no machine-wide change); every command runs with `DOTNET_ROOT=C:\dn302` and that `dotnet` first on `PATH`. `global.json` is untouched. CI uses its own pinned SDK.

**Evidence:** `dotnet test YCR.sln --max-parallel-test-modules 1` on `a564192` (clean tree) → **1311 total, 1311 passed, 0 failed, 0 skipped** (step 1 was 1300; +11); `dotnet ef migrations has-pending-model-changes` clean.

**Next step (exact):** `git stash pop`; plan step 3 — seed migration `20260927071025_Identity_SeedSchedulePermissionGrants` and changed-tests items 1–5, 8–12 and the 44s of items 6–7; RED with the migration absent, then GREEN, then the full suite.

**Blockers / open questions:** none.

**State of the branch:** step 2 pushed; steps 3–6 work stashed, unverified.

---

## 2026-09-27 17:36 Asia/Yangon — claude — T-055

**Stage:** 4 (IMPLEMENT) — plan step 3 done.
**Commit:** the step 3 commit carrying this entry.

**Done:**
- **Step 3 — seed:** migration **`20260927071025_Identity_SeedSchedulePermissionGrants`** (EF-generated shell, empty model diff, snapshot unchanged; body hand-written as raw SQL like `Identity_SeedServicePermissionGrants`): exactly the ten `docs/10` §Schedule permission grants — `schedules.manage` → `SystemAdministrator`, `RailwayAdministrator`; `schedules.read` → all eight roles; no `schedules.publish`; role ids re-declared; `Down()` deletes exactly the ten pairs. OQ59 provisional-ruling comment. Total grants 34 → 44.
- Changed tests (plan items, each still a strict equality): **1** `IdentitySeedTests.Seed_ProducesExactlyEightRolesAndThirtyFourGrants` → renamed `…FortyFourGrants`, 34 → 44, the ten schedule rows in the expected set, class summary; **2** `Seed_MatchesDocs10GrantTables` reads `## Schedule permission grants` too; **3** `DatabasePrivilegeTests.ApplicationCredential_CannotWriteRolesOrGrants` 34 → 44 (and its comment); **4, 5** `RouteMigrationTests` 34 → 44 (both); **6, 7** `TimetableMigrationTests` 34 → 44 (both; their grant arrays move at step 4); **8** `UserAdministrationEndpointTests.ListRoles_…` 34 → 44; **9** `AdministrationHandlerTests.ListRoles_…` + `schedules.manage`, `schedules.read` / + `schedules.read`; **10, 11** `SessionHandlerTests` + the two schedule permissions; **12** `PasswordEndpointTests.Me_…` + `SchedulesManage`, `SchedulesRead`.
- **RED confirmed:** with the seed migration moved aside, the twelve changed tests run by name → **12 failed, 0 passed** (7 Infrastructure, 3 Application, 2 Api). With it back → 12/12 passed.

**Evidence:** `has-pending-model-changes` clean; `dotnet test YCR.sln --max-parallel-test-modules 1` → **1311 total, 1311 passed, 0 failed, 0 skipped** (no new test in this step).

**Next step (exact):** plan step 4 — migration `20260927072142_Security_TimetableScheduleGrants`, the new `DatabasePrivilegeTests` cases (except the applock one), item 14's DDL rows, `ScheduleMigrationTests`, items 6–7's sixteen grant rows (from the stash).

**Blockers / open questions:** none.

**State of the branch:** step 3 committed and pushed; steps 4–6 work stashed, unverified.

---

## 2026-09-27 18:17 Asia/Yangon — claude — T-055

**Stage:** 4 (IMPLEMENT) — plan step 4 done.
**Commit:** the step 4 commit carrying this entry.

**Done:**
- **Step 4 — grants:** migration **`20260927072142_Security_TimetableScheduleGrants`** (EF shell, empty model diff; raw SQL like `Security_TimetableGrants`): exactly spec §7 — `ScheduleVersions` `SELECT`, `INSERT`, `UPDATE(Status, PublishedAtUtc, DiscardedAtUtc, CancelledAtUtc)`; `ScheduleVersionServices` and `ScheduleStopTimes` `SELECT`, `INSERT`. No `DELETE`, no other `UPDATE`, no DDL, no `EXECUTE`. `Down()` revokes exactly these in reverse order.
- New tests: `DatabasePrivilegeTests.ApplicationCredential_HasExactlyTheScheduleGrants` (presences and absences), `…CanMoveAVersionsStatusButNotRewriteIt` (executes the allowed status update and nine denied statements; rows unchanged), `…ScheduleBackstops_RejectInvalidRows` (theory, 18 rows: minutes −1/1440, dwell, no time, number 0, status `Live`, five wrong-instant combinations, four non-UTC instants, duplicate number, second published start date), `…ScheduleBackstops_AllowSharedStartDatesOutsidePublished`, `…CannotInsertAnEntryOrStopTimeNamingNoServiceOrStop` (the four FKs); `ScheduleMigrationTests.Migrate_FromF004Schema_CreatesScheduleObjectsGrantsAndSeed` (from `20260925072603_Security_TimetableGrants` with a service and stops: rows intact, no version rows, the five tables, ten checks, seven indexes with the filter `([Status]=N'Published')`, four FKs `NO_ACTION`, the ten grants, ten `schedules.%` rows, 44 in all) and `…Migrate_DownToF004_…` (and forward again). Shared SQL helpers live in `ScheduleMigrationTests`.
- Changed tests: **item 14** (`ApplicationCredential_AttemptingDdl_IsDenied` + three rows) and **items 6–7's grant rows** (`TimetableMigrationTests`: `ExpectedTimetableGrants` = F-004's six `ExpectedServiceGrants` + F-005's ten `ExpectedScheduleGrants`, sixteen, exact).
- **RED confirmed:** with the grants migration moved aside, the step 4 tests run by name → 26 failed, 13 passed; the 13 are item 14's DDL rows, which assert an absence the migration keeps absent and so cannot show a RED (the same as F-004). With it back → 39/39.
- One test-data fix on the way to green: three backstop rows inserted a second *published* version on the setup version's start date, so R22's filtered unique index refused them before their check could; they now start on 2026-12-01. No assertion changed.

**Evidence:** `has-pending-model-changes` clean; `dotnet test YCR.sln --max-parallel-test-modules 1` → **1338 total, 1338 passed, 0 failed, 0 skipped** (+27).

**Next step (exact):** plan step 5 — `IScheduleVersionsLock`, `SqlServerScheduleVersionsLock`, DI, `GatedScheduleVersionsLock`, `RecordingLocks`, `ApplicationCredential_CanTakeTheScheduleVersionsApplock` (V6), from the stash.

**Blockers / open questions:** none.

**State of the branch:** step 4 committed and pushed; steps 5–6 work stashed, unverified.

---

## 2026-09-27 19:14 Asia/Yangon — claude — T-055

**Stage:** 4 (IMPLEMENT) — plan step 5 done.
**Commit:** the step 5 commit carrying this entry.

**Done:**
- **Step 5 — the Timetable-wide lock (P8, Q5):** `IScheduleVersionsLock` (`YCR.Application/Timetable/Abstractions`); `SqlServerScheduleVersionsLock` (`sp_getapplock` on `timetable.ScheduleVersions`, exclusive, transaction-owned, 30 s, the resource passed as a parameter, `THROW 50036` on a negative result, refuses to run without a transaction); scoped DI registration beside the service-code lock. Test support `GatedScheduleVersionsLock` (the F-004 gate technique) and `RecordingLocks` (records the order of lock requests across both abstractions; used from step 7).
- New test: `DatabasePrivilegeTests.ApplicationCredential_CanTakeTheScheduleVersionsApplock` (**V6 confirmed** under `ycr_app`): no grant needed; refuses without a transaction; a transaction holding `timetable.ServiceCode:SV1` does not block it; a second transaction on it still waits after 750 ms; the code-lock holder then asking for it (the P9 order) waits for the first holder only; a new service-code lock is not blocked by the schedule-lock holder; both waiters proceed once the first commits.
- **RED confirmed:** (1) without the lock types the test does not compile (6 × CS0246); (2) **mutation check:** `AcquireAsync` returning before `sp_getapplock` → the test fails on "The second transaction acquired the schedule lock while the first still held it."; restored byte-for-byte (`cmp`), no `MUTATION` marker left. With the real code: the new test and the F-004 applock test 2/2.

**Deviations:**
- **V7 — intermittent test-host start failure, test unchanged.** The first full run for step 5 had one failure in an unchanged F-004 test, `ServiceRequestLimitTests.Post_WithMalformedJson_Returns400WithoutInternals(withdraw, "not json")`: `InvalidOperationException: IServerAddressesFeature.Addresses cannot be modified after the server has started`, thrown from `WebApplicationFactory.StartServer()` inside the test's `StartKestrel()`, before any request or assertion. Step 5 changes no Api code (one extra scoped DI registration). The class passed 3 of 3 runs in isolation (12/12 each), and the full suite then passed. Logged, not changed; it is the Kestrel-mode `WebApplicationFactory` start path, a test-harness race rather than an F-005 behaviour.

**Evidence:** `dotnet test YCR.sln --max-parallel-test-modules 1` → first run 1339 total, 1 failed (V7); second run **1339 total, 1339 passed, 0 failed, 0 skipped** (+1).

**Next step (exact):** plan step 6 — audit actions and subject, snapshots, `ScheduleStopTimesDigest`, the four write handlers, their tests, the digest tests, the transition SQL test, SV26, SV42, item 13 (30 → 34), from the stash.

**Blockers / open questions:** none.

**State of the branch:** step 5 committed and pushed; step 6 work stashed, unverified.

# Progress: F-004 Service management

Append-only. Newest entry at the bottom. Never edit or delete earlier entries.

---

## 2026-09-25 11:26 Asia/Yangon — claude — T-044

**Stage:** 1–2 (DISCOVER + SPECIFY, `docs/workflows/02-feature-development.md`) — stopped at the stage-2 ⛔.
**Commit:** spec at `7328117` on `feature/F-004`; this entry is the commit after it.

**Done this session:**
- Claimed T-044 in the coordination checkout: `git branch claim/T-044 main`, row set to doing/claude (`3cf0f01`, pushed). Created the worktree `D:\MR\ycr-F-004` on `feature/F-004` from `main`.
- Read AGENTS.md, `TASKS.md` incl. §Protocol and the T-044 row, workflows 01/02, templates (feature-spec, progress, adr), `docs/00`, `docs/01`, `03`, `04`, `05`, `07`, `08`, `10`, `11`, `12`, `19`, `20`, `21`, the glossary, `docs/business/mr-questions-pack.md` §OQ1/§OQ4/§OQ19 (read only), ADR-0002/0003/0004/0006/0012/0013/0014 (payload)/0017/0018/0019/0021/0024, the F-003 spec in full and its progress/plan/review headings, and the code: `src/YCR.Domain/Network/Route.cs`, `RouteStation.cs`, `src/YCR.Application/Network/` (`INetworkDbContext`, `NetworkConstraints`, `NetworkAuditSubjects`, `RouteAuditSnapshot`, `GetRouteHandler`), `Permissions.cs`, `src/YCR.Infrastructure/DependencyInjection.cs`, `tests/YCR.ArchitectureTests/ArchitectureRules.cs`.
- Wrote `docs/features/F-004-service-management/spec.md` (Status: Draft): §0 discovery (D1–D24, gaps G1–G12, contradictions C1–C7, engineering proposals E1–E13, boundary options B1–B4 in §0.7, **§0.9 Rulings needed from hein**), rules R1–R34, scenarios S1–S28 (tagged with the OQs that fix them), state changes, API and data proposals written against B1, audit, out of scope, Blocked behaviour.
- Added **OQ42–OQ49** to `docs/19-open-questions.md`, each with a **BLOCKS:** line.
- Drafted **ADR-0025 (Proposed)**, cross-module references: contract shape and foreign keys, and added it to `docs/decisions/README.md`.

**New OQs to add to `docs/business/mr-questions-pack.md`** (not edited in this stage, per instructions):
- **OQ42** — What is a train (rolling stock, a numbered working, or not needed in Phase 1)? Suggested owner: Network Operations / Planning.
- **OQ43** — What identifies a service (number/code, names, format, uniqueness scope, reuse, type/class)? Network Operations / Planning, with Commercial/Fares for "service type".
- **OQ44** — How does a service express direction and extent on closed and open routes (clockwise/anticlockwise, start/end, full or multiple circuits, part route)? Network Operations / Planning.
- **OQ45** — Stopping-pattern rules (stops on route, in order, skipping, first/last, minimum, multi-route, repeats, stop attributes)? Network Operations / Planning.
- **OQ46** — Services versus inactive routes and stations (creation; effect of deactivation)? Network Operations / Planning.
- **OQ47** — Operating days (days of week, holiday calendar and its owner, per-date exceptions, which date is the operating day)? Network Operations / Planning.
- **OQ48** — Effective period, change after definition, withdrawal, reactivation, overlap, relation to timetable-version effective dates? Network Operations / Planning.
- **OQ49** — Who holds `services.manage`/`services.read` (and `trains.*`, `schedules.*` where in scope)? Same routing as OQ28/OQ40.

**Evidence:** documentation only; no build or test was run because no code changed. Commands: `git pull`, `git branch claim/T-044 main`, `git commit`/`git show --stat HEAD`/`git push origin main` (claim), `git worktree add -b feature/F-004 ../ycr-F-004 main`, `git diff`/`git status`/`file` checks, `git commit` (`7328117`). Files changed: `docs/features/F-004-service-management/spec.md` (new), `docs/features/F-004-service-management/progress.md` (new), `docs/19-open-questions.md` (OQ42–OQ49 inserted before §Engineering decisions), `docs/decisions/ADR-0025-cross-module-references.md` (new, Proposed), `docs/decisions/README.md` (one row).

**Next step (exact):** hein, at the ⛔ stop: rule on the spec §0.9 table — B (boundary), OQ42–OQ49 (Myanma Railways answers, or provisional tech-lead rulings labelled "not a Myanma Railways answer"), ADR-0025 (accept/amend/reject), E6, E11 and the remaining E-proposals, and the S28 race. Then a stage-2 revision applies the rulings to the spec, `docs/19` and `docs/10`, and hein sets the spec Approved. Stage 3 (PLAN) must not start before that. If B3 is chosen, raise the timing OQs first.

**Blockers / open questions:** `approval` (⛔ stage 2). Business: OQ42–OQ49 — all blocking. Scope: ruling B. Engineering: ADR-0025 Proposed; ADR-0024 still Proposed (needed only under OQ48 (b) / B2 / B3). Unchanged background: OQ1 (no seed data), OQ4 and OQ19 (not blocking; see spec G11), OQ17 (out of scope).

**State of the branch:** documentation only; nothing to build or test. All work committed on `feature/F-004` and pushed.

---

## 2026-09-25 11:43 Asia/Yangon — claude — T-044

**Stage:** 2 (SPECIFY), revision applying hein's rulings — stopped at the stage-2 ⛔ again.
**Commit:** spec at `6cf674d` on `feature/F-004`; this entry is the commit after it.

**Done this session:**
- Applied hein's rulings of 2026-09-25 (B → B1; OQ42–OQ49 as provisional tech-lead rulings, not a Myanma Railways answer; ADR-0025 Accepted; E1–E13) to `spec.md`: Status "Draft — all rulings applied, awaiting approval"; §0.3 closure notes (C1–C5); §0.7 outcome B1; §0.9 rulings table; new §0.10 (OQ50, Q2, Q3, consequences); §1–§9 and Blocked behaviour rewritten for B1 — rules R1–R42, scenarios S1–S52.
- `docs/19`: resolution block under each of OQ42–OQ49 (OQ47 partly resolved: holidays and per-date exceptions stay open); **new OQ50** (past `EffectiveFrom`/`EffectiveTo` at creation), raised because OQ48 rules withdrawals but not creation.
- `docs/10`: preamble exception list, §Service permission grants — resolved (OQ49) in the route-section bullet format, `services.read` in the inventory, `trains.manage` marked not used in Phase 1.
- ADR-0025: Status Accepted (hein, 2026-09-25); README row Accepted; OQ46 note added to Follow-up only (Decision untouched).

**New OQ to add to `docs/business/mr-questions-pack.md`:** **OQ50** — may a service be created with an effective period starting (or ending) before today, e.g. the timetable in force at go-live? Network Operations / Planning.

**Evidence:** documentation only; no build or test run because no code changed. `IdentitySeedTests` reads only the station, route and identity sections of `docs/10`, so the new section does not affect it (PLAN extends it). Commands: `git pull` (both checkouts), `git diff --stat`, `git ls-files --eol`, `git commit`, `git push`.

**Next step (exact):** hein rules on spec §0.10 — OQ50 (a/b/c), Q2 (stop cap 200 or 201), Q3 (accept the create-vs-deactivation race or serialise) — and sets the spec Approved. Stage 3 (PLAN) must not start before that.

**Blockers / open questions:** `approval` (⛔ stage 2). Business: OQ50 (blocking). Engineering: Q2, Q3. OQ42–OQ49 still open with Myanma Railways (not blocking); OQ47 holiday/exception part open (not blocking, known limitation). OQ1, OQ4, OQ17, OQ19 unchanged.

**State of the branch:** documentation only; all work committed on `feature/F-004` and pushed.

---

## 2026-09-25 11:48 Asia/Yangon — claude — T-044

**Stage:** 2 (SPECIFY) — complete. Spec **Approved (hein, 2026-09-25)**.
**Commit:** approved spec at `c2ea4f0` on `feature/F-004`; this entry is the commit after it.

**Done this session:**
- Applied hein's final rulings (2026-09-25): **OQ50** (past `EffectiveFrom` allowed; a given `EffectiveTo` not before today, `422 Timetable.ServiceEffectiveToInPast`; overlap unchanged; `CreatedAtUtc`/audit record entry time) — R37, R39, §6 error list, S40 rewritten, S40a added, `docs/19` OQ50 resolution block; **Q2** (keep 200 stops; a full circuit of a 200-station route is a known limitation) — R31, §9; **Q3** (create vs deactivation race accepted) — §5 paragraph, S51; readings 1 and 2 confirmed; the periods-only overlap consequence recorded in §9 known limitations.
- §0.10 records all of the above with the date; blocking markers removed ("Blocking open questions: none"); Blocked behaviour keeps OQ47's holiday/exception part, FR-004, OQ1, OQ9/OQ17 and the Myanma Railways follow-up only. Status set to "Approved (hein, 2026-09-25)".

**Evidence:** documentation only; no build or test run because no code changed. Commands: `git pull` (both checkouts), `git diff --stat`, `git commit`, `git push`.

**Next step (exact):** T-045 (stage 3, PLAN) against the approved spec `c2ea4f0` — a separate task; not started here.

**Blockers / open questions:** none blocking. OQ42–OQ50 still open with Myanma Railways (provisional rulings apply); OQ47 holiday/exception part open (known limitation). OQ1, OQ4, OQ17, OQ19 unchanged.

**State of the branch:** documentation only; all work committed on `feature/F-004` and pushed.

---

## 2026-09-25 12:19 Asia/Yangon — claude — T-045

**Stage:** 3 (PLAN) — plan written; ⛔ awaiting approval.
**Commit:** `plan.md` is in the commit before this entry on `feature/F-004`. The plan SHA is recorded in `TASKS.md` T-045 Notes.

**Done this session:**
- Wrote `docs/features/F-004-service-management/plan.md` (revision 1) against the approved spec `c2ea4f0`: P1–P23; the ADR-0025 contract (`INetworkReader` + four records, `internal NetworkReader`, DI in `AddApplication`, the new `ContractsMustNotDependOnModuleDomainOrContext` rule with violating fixtures, Timetable boundary fixtures); the stop-order algorithm with pseudo-code and pinning cases; the R35 lock (`sp_getapplock` on `timetable.ServiceCode:<CODE>`, transaction-owned, no grant); three migrations with rollback/roll-forward; endpoint inventory with body limits (32 KiB create, 1 KiB withdraw; largest valid bodies 9,280 and 29 bytes); a test plan mapping all 53 live scenarios; the 16 existing tests whose counts or lists change; stage-8 documentation list; nine steps.
- Verified at PLAN in a scratch console (outside the repo, EF Core 10.0.12): cross-schema FKs from `HasOne<Route>().WithMany()` / `HasOne<Station>().WithMany()` are `NO ACTION` to `network.*` with no navigation; EF adds `IX_Services_RouteId` and `IX_ServiceStops_StationId` by convention (both wanted by spec §7, now declared explicitly); the Timetable diff emits nothing against `network`.

**Evidence:** documentation only; no build or test run, because no repository code changed. Commands: `git pull` (both checkouts), `git branch claim/T-045 main`, `grep` over `src/` and `tests/` for pinned counts and permission lists, `dotnet run` of the scratch EF console, `git commit`, `git push`.

**Next step (exact):** hein rules on plan Q1 (shared kernel: move `BilingualName` and the code format to `YCR.Domain.Common`), Q2 (explicit transaction for the code lock as the second F-002 P14 exception) and Q3 (`Time:LocalTimeZone` + `ILocalCalendar`), and approves the plan. Stage 4 (implementation) is a separate task and must not start before that.

**Blockers / open questions:** `approval` (⛔ stage 3). Engineering: plan Q1–Q3. No new business OQ. OQ42–OQ50 still open with Myanma Railways (provisional rulings apply); OQ47 holiday/exception part open (known limitation). OQ1, OQ4, OQ17, OQ19 unchanged.

**State of the branch:** documentation only; all work committed on `feature/F-004` and pushed.

---

## 2026-09-25 12:30 Asia/Yangon — claude — T-045

**Stage:** 3 (PLAN) — complete. Plan **Approved (hein, 2026-09-25)**, revision 2.
**Commit:** approved plan at `e81b7f3` on `feature/F-004`; this entry is the commit after it.

**Done this session:**
- Applied hein's rulings on plan Q1–Q3 (2026-09-25, ENGINEERING DECISIONS). §Questions for hein replaced by §Rulings on the plan questions.
  - **Q1:** `BilingualName` and `CodeFormat` move to `YCR.Domain.Common`; callers pass their module's error. P2, the file tables, the test tables (the rewritten `BilingualName` test keeps exactly its assertions) and step 1 updated. The existing architecture tests that cover the move are named.
  - **Q2:** the explicit transaction for `sp_getapplock` is accepted as the second use of the F-002 P14 exception. P10 updated. Stage 8 now includes a `docs/20` rule on when a handler may open its own transaction, and ADR-0026 "Application locks for set invariants" (Proposed at stage 8, accepted by hein at stage 9).
  - **Q3:** `Time:LocalTimeZone` = `Asia/Yangon` and `ILocalCalendar.Today()`. Both the Api and the Worker validate the zone at startup; the Worker's `Program.cs` and `appsettings.json` are added. New tests: `AsiaYangon_ResolvesInThisEnvironment`, `ResolveZone_WithMissingOrUnknownZone_ThrowsWithClearMessage`, the 17:29:59Z/17:30:00Z midnight theory, the Api startup tests and `WorkerStartupTests` (Integration). R-4, steps 7–9 and the `docs/15` stage-8 item updated.
- Recorded tzdata facts (checked with `docker` on 2026-09-25). CI runs directly on `ubuntu-latest`, not in a .NET image. `mcr.microsoft.com/dotnet/runtime-deps:10.0` (Ubuntu 24.04.5) has `tzdata` and `Asia/Yangon`. The plain `…-noble-chiseled` image has no `Asia/Yangon`; `…-noble-chiseled-extra` has it. The pulled images were removed afterwards.
- Status set to Approved, revision 2. The test count is now about 179 (163 new).

**Evidence:** documentation only; no build or test run, because no repository code changed. Commands: `git pull` (both checkouts), `docker run`/`docker create`/`docker export` against the three `runtime-deps` tags, `git diff --stat`, `git commit`, `git push`.

**Next step (exact):** T-046 (stage 4) against the approved plan `e81b7f3`, starting with plan step 1. It is a separate task and was not started here.

**Blockers / open questions:** none blocking. OQ42–OQ50 are still open with Myanma Railways (the provisional rulings apply). OQ47's holiday/exception part is still open (a known limitation). OQ1, OQ4, OQ17 and OQ19 are unchanged.

**State of the branch:** documentation only; all work committed on `feature/F-004` and pushed.

---

## 2026-09-25 13:25 Asia/Yangon — claude — T-046

**Stage:** 4 (IMPLEMENT) — plan step 1 of 9 done.
**Commit:** step 1 is the commit that adds this entry on `feature/F-004`.

**Done this session:**
- Claimed T-046 (`claim/T-046`; ledger `3886e07` on `main`). Preconditions checked: branch head `f4026fc`, spec Approved (hein, 2026-09-25), plan Approved revision 2 (`e81b7f3`).
- **Step 1 — shared kernel (P2, ruling Q1).** `git mv` of `Network/NetworkCodeFormat.cs` → `Common/CodeFormat.cs` (class renamed, regex unchanged, comment adds service codes, OQ43) and `Network/BilingualName.cs` → `Common/BilingualName.cs` (namespace `YCR.Domain.Common`; only `Create(en, my, Error whenInvalid)`, the two-argument overload removed). `StationCode`/`RouteCode` call `CodeFormat`; `CreateStationHandler` passes `NetworkErrors.InvalidStationName`.
- Changed-tests items 15–16 exactly: `BilingualNameTests` moved to `tests/YCR.Domain.Tests/Common/` (namespace `YCR.Domain.Tests.Common`); `…WithoutErrorArgument_StillReturnsInvalidStationName` → `…WithStationError_WhenInvalid_ReturnsInvalidStationName`, its two assertions unchanged (`IsFailure`; `NetworkErrors.InvalidStationName` for `("   ", "Yangon Myanmar")`); `Create_WithValidNames_ReturnsTrimmedNames` and the `AssertValidationFailure` helper only gained the explicit station error. Call sites (no assertion change): `StationTests` ×3, `LedgerMigrationTests` ×2, `MigrationBundleTests`, `ModuleInterfacesTests`, `UniqueConstraintTranslationTests` (those four Infrastructure test files also gained `using YCR.Domain.Common;`).
- **V1 passed:** `dotnet ef migrations has-pending-model-changes` → "No changes have been made to the model since the last migration." The snapshot's `"YCR.Domain.Network.BilingualName"` owned-type string is left as generated; it is rewritten by `Timetable_CreateServices` in step 4.
- No new test in step 1 (the plan names none); the ruling-Q1 architecture tests (`CommonKernel_WithModuleDependency_DetectsViolation`, `ApplicationAndDomainRules_ForEveryBoundedContext_HaveNoSourceViolations`, `DomainModuleBoundary_…`, `DomainLayerRules_…`, `ApiRules_…`) ran unchanged and passed.

**Evidence:** baseline before any change: `dotnet test YCR.sln` 798 total (one failure in that run was caused by my step-1 edits landing while the lazy migration bundle was being built; not a pre-existing failure). After step 1: `dotnet build YCR.sln` 0 warnings, 0 errors; `dotnet test YCR.sln` **798 total, 798 passed, 0 failed, 0 skipped**.

**Next step (exact):** plan step 2 — Timetable domain (`ServiceCode`, `Direction`, `OperatingDays`, `EffectivePeriod`, `ServiceRouteFacts`, `ServiceStop`, `Service`, `TimetableErrors`) with every `YCR.Domain.Tests` row of the plan.

**Blockers / open questions:** none.

**State of the branch:** builds; tests green; committed and pushed.

---

## 2026-09-25 13:50 Asia/Yangon — claude — T-046

**Stage:** 4 (IMPLEMENT) — plan step 2 of 9 done.
**Commit:** step 2 is the commit that adds this entry.

**Done:**
- **Step 2 — Timetable domain** (`src/YCR.Domain/Timetable/`): `TimetableErrors` (all 17 codes; the per-stop and per-route codes take the offending id, `ServiceCodePeriodOverlap` the code), `ServiceCode` (P3), `Direction` (P8), `OperatingDays` (P7; throws on empty/repeated; `Days` Monday first), `EffectivePeriod` (`ForNewService` R19→R39; `Overlaps` inclusive, null unbounded, an empty period overlaps nothing, R42), `ServiceRouteFacts`/`ServiceRouteStationFacts` (P4), `ServiceStop` (P1), `Service` (`Create` with the plan's stop algorithm steps 1–7 verbatim, `Withdraw` per P12, `NeverRuns`; no events, P21).
- Tests (44 named, as the plan counts): `Common/BilingualNameTests.BilingualName_Create_WithServiceError_WhenInvalid_ReturnsInvalidServiceName`; `Timetable/ServiceCodeTests` (2), `OperatingDaysTests` (3), `EffectivePeriodTests` (8), `ServiceTests` (30), including the walking-simulation oracle over every stop list of length ≤ n+1 on open and closed routes of 3–6 stations in both directions (1,427,744 lists; the count is asserted).
- **RED confirmed:** (1) the tests alone did not compile (CS0246/CS0234: `YCR.Domain.Timetable` types missing); (2) with the real types but `Service.CheckStops`, `Service.Withdraw`, `EffectivePeriod.ForNewService` and `Overlaps` short-circuited by temporary stubs, `dotnet test tests/YCR.Domain.Tests` → **43 failed** (every refused-pattern, precedence, oracle, withdrawal and overlap case). Stubs removed (the staged files are byte-identical to what is committed); then 214/214.

**Evidence:** `dotnet test YCR.sln` **886 total, 886 passed, 0 failed, 0 skipped** (798 + 88 new cases).

**Next step (exact):** plan step 3 — the Network contract (`INetworkReader` + records, `internal NetworkReader`, DI), `ContractsMustNotDependOnModuleDomainOrContext`, Contracts and Timetable fixtures, three architecture tests, `NetworkReaderTests` (4), `AddApplication_RegistersNetworkReaderAsScoped`.

**Blockers / open questions:** none.

**State of the branch:** builds; tests green; committed and pushed.

---

## 2026-09-25 14:10 Asia/Yangon — claude — T-046

**Stage:** 4 (IMPLEMENT) — plan step 3 of 9 done.
**Commit:** step 3 is the commit that adds this entry.

**Done:**
- **Step 3 — the Network contract (ADR-0025, P14, P15).** `YCR.Application.Network.Contracts.INetworkReader` (three read methods) and `RouteReference`, `RouteStationReference`, `RouteSummaryReference`, `StationReference` (exactly the plan's records); `internal sealed class NetworkReader` in `YCR.Application.Network` (`AsNoTracking`, whole value objects projected and unwrapped after materialisation, `Contains` for id lists, the `GetRouteHandler` two-statement shape); `AddApplication` registers it scoped. `ArchitectureRules.ContractsMustNotDependOnModuleDomainOrContext` exactly as the plan's snippet. Fixtures: `Violations/ContractViolations.cs` (`ContractExposingNetworkDomain`, `…NetworkContext`, `…EntityFramework`) and `Violations/TimetableBoundaryViolations.cs` (`TimetableUsingNetworkDomain`, `…NetworkContext`, the permitted `…NetworkContracts`).
- Tests: `ContractsRule_WithDomainContextOrEfFixtures_DetectsViolations`, `TimetableApplication_DependingOnNetworkDomainOrContext_IsDetected`, `TimetableApplication_DependingOnNetworkContracts_IsAllowed`; `NetworkReaderTests` (4); `DependencyInjectionTests.AddApplication_RegistersNetworkReaderAsScoped`. Existing architecture tests unchanged; `HandlerTypes_IncludesEveryHandlerDefined` still 26 (NetworkReader is not a `*Handler`).
- **RED confirmed:** the architecture tests did not compile without the rule (CS0117 `ContractsMustNotDependOnModuleDomainOrContext`); the two Timetable boundary tests confirm the *existing* allowlist rule, so their RED was the missing fixtures (they cannot fail once the fixtures compile, which is what they prove). With the contract types present but no `NetworkReader`/registration: 5 failed (`No service for type 'YCR.Application.Network.Contracts.INetworkReader' has been registered`; the DI test on its `Assert.Single`).

**Evidence:** targeted runs Architecture 20/20, Application (reader + DI) 12/12; `dotnet test YCR.sln` **894 total, 894 passed, 0 failed, 0 skipped**; build 0 warnings.

**Next step (exact):** plan step 4 — permissions constants, `ITimetableDbContext`, `ServiceConfiguration`/`ServiceStopConfiguration`, `YcrDbContext`, DI, migration `Timetable_CreateServices` (+ hand-added `DropSchema`), V4; `TimetableModelTests`, the two UTC rows, `TimetableConstraints_*`, `ForeignKeys_*`, `ModuleInterfaces_TimetableContext_…`.

**Blockers / open questions:** none.

**State of the branch:** builds; tests green; committed and pushed.

---

## 2026-09-25 14:45 Asia/Yangon — claude — T-046

**Stage:** 4 (IMPLEMENT) — plan step 4 of 9 done.
**Commit:** step 4 is the commit that adds this entry.

**Done:**
- **Step 4 — persistence.** `Permissions.ServicesManage`/`ServicesRead` (OQ49 comment); `YCR.Application.Timetable.ITimetableDbContext` (`Services`, `Database`, `SaveChangesAsync`; no `DbSet<ServiceStop>`); `Configurations/Timetable/ServiceConfiguration.cs` and `ServiceStopConfiguration.cs` (spec §7: columns and types, the six check constraints in the model, `EffectiveTo` the only concurrency token, owned `BilingualName` and `OperatingDays` (`Days` ignored), `NeverRuns`/`DomainEvents` ignored, the three indexes declared and named (P16), `HasOne<Route>()`/`HasOne<Station>().WithMany()` `NO ACTION` with no navigation, `HasMany(Stops)` `NO ACTION`); `YcrDbContext` implements `ITimetableDbContext`; `AddInfrastructure` registers it.
- **Migration `20260925065623_Timetable_CreateServices`** — EF-generated, reviewed by hand: `EnsureSchema timetable`, `CreateTable Services` (17 columns exactly as spec §7, five checks, `FK_Services_Routes_RouteId` → `network.Routes` with no `onDelete` = NO ACTION), `CreateTable ServiceStops` (`PK (ServiceId, Position)`, `CK_ServiceStops_Position`, `FK_…_Services_ServiceId`, `FK_…_Stations_StationId` → `network.Stations`), three `CreateIndex` — nothing against `network`. **V8:** `DropSchema("timetable")` added by hand to `Down()` before first apply. The snapshot diff is additive plus the two `"YCR.Domain.Network.BilingualName"` owned-type strings becoming `"YCR.Domain.Common.BilingualName"` (the V1 follow-up). **V4:** `has-pending-model-changes` → "No changes have been made to the model since the last migration."
- Tests: `TimetableModelTests` (6: columns/types/keys, exact index set in model and migration, both cross-schema FKs NO ACTION without navigations in model and migration, every check constraint, Up touches only `timetable`, Down drops tables then schema); `StationModelTests.Model_WithUtcColumn_DeclaresItsCheckConstraint` + 2 rows (changed-tests item 13); `TimetableMigrationTests.TimetableConstraints_RejectInvalidRows` (6 rows), `…AllowAnEmptyPeriodOnlyAfterWithdrawal`, `ForeignKeys_RejectAServiceOrStopNamingNoNetworkRow` (migrator); `ModuleInterfacesTests.ModuleInterfaces_TimetableContext_ResolvesToSameInstance` (same instance and same transaction; no query, because `ycr_app` has no timetable grant until step 6).
- **RED confirmed:** `TimetableModelTests` did not compile (CS0246 `Timetable_CreateServices`). With `ITimetableDbContext` present but no configuration, registration or migration: `TimetableMigrationTests` 8/8 failed, the two new UTC rows failed, `ModuleInterfaces_TimetableContext_…` failed.
- **V-note (not a deviation):** one assertion in my new `TimetableModelTests` first used `GetValueConverter()!.ProviderClrType`, which is null on the design-time model for `HasConversion<string>()`; it asserts `GetProviderClrType() == typeof(string)` instead — same claim. One `Assert.Empty(… .Where(…))` became `Assert.DoesNotContain` (xUnit2029, warnings are errors).

**Evidence:** `dotnet test YCR.sln` **911 total, 911 passed, 0 failed, 0 skipped**; build 0 warnings.

**Next step (exact):** plan step 5 — migration `Identity_SeedServicePermissionGrants`; changed-tests items 1–10 (already edited in the working tree, uncommitted, to be run RED against the unseeded database first).

**Blockers / open questions:** none.

**State of the branch:** builds; tests green; step 4 committed and pushed. The step-5 test edits (items 1–10) are in the working tree, deliberately not in this commit.

---

## 2026-09-25 15:20 Asia/Yangon — claude — T-046

**Stage:** 4 (IMPLEMENT) — plan step 5 of 9 done.
**Commit:** step 5 is the commit that adds this entry.

**Done:**
- **Step 5 — seed.** Migration `20260925071216_Identity_SeedServicePermissionGrants` (EF-generated shell, empty model diff, snapshot unchanged; body hand-written as raw SQL like `Identity_SeedRoutePermissionGrants`): exactly the ten `docs/10` §Service permission grants rows (`services.manage` → SystemAdministrator, RailwayAdministrator; `services.read` → all eight), role ids re-declared; `Down()` deletes exactly those ten pairs. Header comment carries the OQ49 provisional-ruling label. No role, user, `trains.*` or `schedules.*` row. V4 still clean.
- **Changed-tests items 1–10, exactly as the plan lists, each still a strict equality:** (1) `IdentitySeedTests.Seed_ProducesExactlyEightRolesAndTwentyFourGrants` → renamed `…ThirtyFourGrants`, 24 → 34, the ten service rows added to the expected set; (2) `Seed_MatchesDocs10GrantTables` heading list + `## Service permission grants` (the section parses with the existing bullet parser and must yield grants); (3) `DatabasePrivilegeTests.ApplicationCredential_CannotWriteRolesOrGrants` 24 → 34; (4) `RouteMigrationTests.Migrate_FromF002Schema_…` total 24 → 34 (`routes.%` still 10); (5) `…Migrate_DownToF002_…` forward-again total 24 → 34 (the 14 after rollback unchanged); (6) `UserAdministrationEndpointTests.ListRoles_…` 24 → 34; (7) `AdministrationHandlerTests.ListRoles_…` SystemAdministrator list + `services.manage`, `services.read`, ReportingUser list + `services.read`; (8) `SessionHandlerTests.Resolve_ActiveSession_…` + both; (9) `SessionHandlerTests.GetCurrentUser_…` + both (RailwayAdministrator); (10) `PasswordEndpointTests.Me_…` + `ServicesManage`, `ServicesRead`. Only comments beside them changed otherwise. No other existing test broke.
- **RED confirmed:** with items 1–10 edited and no seed migration, the ten tests were run by name → **10 failed, 0 passed**. After the migration, all ten pass in the full run.

**Evidence:** `dotnet test YCR.sln` **911 total, 911 passed, 0 failed, 0 skipped** (no new test cases in this step; ten existing ones changed); build 0 warnings; `has-pending-model-changes` clean.

**Next step (exact):** plan step 6 — migration `Security_TimetableGrants`; `DatabasePrivilegeTests` timetable cases (except the applock one) + the three DDL rows (item 12); `TimetableMigrationTests.Migrate_FromF003Schema_…` and `Migrate_DownToF003_…`.

**Blockers / open questions:** none.

**State of the branch:** builds; tests green; committed and pushed.

---

## 2026-09-25 15:55 Asia/Yangon — claude — T-046

**Stage:** 4 (IMPLEMENT) — plan step 6 of 9 done.
**Commit:** step 6 is the commit that adds this entry.

**Done:**
- **Step 6 — grants.** Migration `20260925072603_Security_TimetableGrants` (EF shell, empty model diff; raw SQL exactly spec §7 / S47: `GRANT SELECT, INSERT` on `Services`; `GRANT UPDATE ([EffectiveTo], [WithdrawnAtUtc])` on `Services`; `GRANT SELECT, INSERT` on `ServiceStops`; `Down()` revokes in reverse order; no `DELETE`, no other `UPDATE`, no DDL, no `EXECUTE`). V4 clean.
- Tests: `DatabasePrivilegeTests.ApplicationCredential_HasExactlyTheTimetableGrants` (presences; absences of `DELETE`/`ALTER`/`CONTROL`/table-level `UPDATE` on both tables, `UPDATE` on each of the 15 fixed `Services` columns and the 3 `ServiceStops` columns, and no schema- or object-level permission other than SELECT/INSERT/UPDATE in `timetable`), `…CanWithdrawAServiceButNotRewriteIt` (executed: the two-column update succeeds; nine rewrites/deletes denied; rows unchanged), `…CannotInsertAServiceOrStopNamingNoNetworkRow` (S48, error 547 as `ycr_app`); `ApplicationCredential_AttemptingDdl_IsDenied` + the three timetable rows (changed-tests item 12); `TimetableMigrationTests.Migrate_FromF003Schema_CreatesTimetableObjectsGrantsAndSeed` (from `20260924152836_Security_NetworkRouteGrants` with a station, a route and its sequence row; network rows survive; no service rows; exact tables, six checks, five indexes with uniqueness and key columns, three FKs with referenced schema and NO_ACTION/NO_ACTION, the exact grant set, the ten `services.%` rows by role, 34 total) and `Migrate_DownToF003_RemovesTimetableObjectsGrantsAndSeedRowsAndKeepsNetwork` (with a service and stop in place; schema and tables gone, network rows kept, grants and seed gone, 24 total, the F-003 route grant untouched; forward again restores the grants and 34).
- Note on counting: the grant set is six `sys.database_permissions` rows (two column-level `UPDATE`s); the plan's "five grant rows" counts the column-level `UPDATE` once. The test asserts the exact six-row set.
- **RED confirmed:** before the migration the five new tests failed (5/5); the 10 `…AttemptingDdl_IsDenied` rows passed before and after, as expected: they assert an absence the migration keeps absent, so the three new rows cannot show a RED. After the migration: 15/15.

**Evidence:** `dotnet test YCR.sln` **919 total, 919 passed, 0 failed, 0 skipped**; build 0 warnings; `has-pending-model-changes` clean.

**Next step (exact):** plan step 7 — `ILocalCalendar` + `LocalCalendar` + `LocalTimeOptions.ResolveZone`; `IServiceCodeLock` + `SqlServerServiceCodeLock`; audit subjects/actions/snapshot; the four handlers with DTOs; DI test 26 → 30; every Application test row; `ApplicationCredential_CanTakeTheServiceCodeApplock`; `LocalCalendarTests`.

**Blockers / open questions:** none.

**State of the branch:** builds; tests green; committed and pushed.

---

## 2026-09-25 17:05 Asia/Yangon — claude — T-046

**Stage:** 4 (IMPLEMENT) — plan step 7 of 9 done.
**Commit:** step 7 is the commit that adds this entry.

**Done:**
- **Step 7 — Application.** `ILocalCalendar` (Application/Common/Abstractions); `Infrastructure/Time/LocalTimeOptions` (`Time`, `Time:LocalTimeZone`, the one `ResolveZone` with the two plan messages) and `internal LocalCalendar` (TimeProvider instant → configured zone → `DateOnly`); `IServiceCodeLock` + `internal SqlServerServiceCodeLock` (`sp_getapplock` on `timetable.ServiceCode:<CODE>`, Exclusive, Transaction-owned, 30 s, resource passed as a parameter through `ExecuteSqlAsync`, `THROW 50035` on a negative result, `InvalidOperationException` without a transaction); `TimetableAuditSubjects`, `TimetableAuditActions`, `ServiceAuditSnapshot` (+ `ServiceStopAuditSnapshot`, spec §8 shape); `ServiceReadMapping`; `CreateService`, `WithdrawService`, `GetService` (+ `ServiceDto`, `ServiceRouteDto`, `ServiceStopDto`, `ServiceProjection`), `ListServices` (+ `ServiceSummaryDto`) exactly as P5/P12/P17. DI: `ILocalCalendar` singleton, `IServiceCodeLock` scoped, `AddOptions<LocalTimeOptions>()` (each host binds and validates it, step 8).
- Tests (51 named): `Timetable/CreateServiceHandlerTests` (23), `WithdrawServiceHandlerTests` (15, incl. forced S29 ×2, unforced S29, forced S37 theory, R36 backstop, V2 SQL), `ServiceQueryHandlerTests` (8), `ListServicesSqlTests` (1, V3); support `TimetableHandlerTestBase`, `TimetableTestData`, `GatedServiceCodeLock`, `AuditRecordHook`; `DatabasePrivilegeTests.ApplicationCredential_CanTakeTheServiceCodeApplock` (V5); `Time/LocalCalendarTests` (3, V6 on Windows). Changed-tests item 11: `DependencyInjectionTests.HandlerTypes_IncludesEveryHandlerDefined` names the four handlers, 26 → 30.
- **V2 confirmed:** withdrawal is one `UPDATE [timetable].[Services] SET [EffectiveTo], [WithdrawnAtUtc] … WHERE [Id] = @p AND [EffectiveTo] IS NULL` the first time and `… AND [EffectiveTo] = @p` afterwards; nothing against `ServiceStops`. (EF writes only changed columns: in my first draft of the test the second withdrawal ran at the same clock instant, so `WithdrawnAtUtc` did not change and was correctly left out; the test now advances the clock between the two, as real withdrawals are.) **V3 confirmed:** three round trips — `COUNT`, the page (`ORDER BY [Code], [EffectiveFrom], [Id]`, `OFFSET/FETCH`, correlated `COUNT(*)` over `ServiceStops`), one Network summary read. **V5 confirmed** under `ycr_app`. **V6** resolves on this Windows machine; CI proof is step 9.
- **RED confirmed:** (1) the tests did not compile before the production code (CS0246/CS0234 on the commands, DTOs, `IServiceCodeLock`, `YCR.Infrastructure.Time`); (2) mutation check with the real code in place: `SqlServerServiceCodeLock.AcquireAsync` returning before `sp_getapplock`, and `LocalCalendar.Today()` returning the UTC date → **8 failed**: both forced S29 tests, both S37 rows, S27 (five parallel creates), `CreateService_AtYangonMidnight_…(17:30:00Z)`, `Today_AroundYangonMidnight_…(17:30:00Z)`, `ApplicationCredential_CanTakeTheServiceCodeApplock`. The unforced S29 test still passed under the mutation, as an unforced test may (it asserts outcomes, not an interleaving; the forced tests carry the proof). Mutations reverted (files restored from the staged originals; no `MUTATION` marker left).
- Two test-side fixes while going green, neither touching an assertion's intent: `ServiceQueryHandlerTests`' row snapshot used an aggregate inside `STRING_AGG` (SQL error) → `CROSS APPLY`; the V2 clock advance above.

**Evidence:** targeted: Application Timetable 88/88, LocalCalendarTests 9/9, applock 1/1; `dotnet test YCR.sln` **1017 total, 1017 passed, 0 failed, 0 skipped**; build 0 warnings.

**Next step (exact):** plan step 8 — `ServiceContracts` (+ validators, 200 cap), `ServiceEndpoints` (+ 32 KiB / 1 KiB), `Program.cs` (mapping; `LocalTimeOptions` bound and validated on start), Api `appsettings.json`; Worker `Program.cs` zone check before both start paths + `appsettings.json`; every Api test row, `DeployedShapeTests` (+4 rows, item 14), `ServiceRequestLimitTests`, `ServicePermissionGrantTests`, `LocalTimeZoneStartupTests`, `WorkerStartupTests`; V7.

**Blockers / open questions:** none.

**State of the branch:** builds; tests green; committed and pushed.

---

## 2026-09-25 18:15 Asia/Yangon — claude — T-046

**Stage:** 4 (IMPLEMENT) — plan step 8 of 9 done (locally green).
**Commit:** step 8 is the commit that adds this entry.

**Done:**
- **Step 8 — API and Worker.** `Api/Contracts/Timetable/ServiceContracts.cs` (`CreateServiceRequest` + validator per P6, `MaxStopStationIds = 200` REQUIRED CONTROL; `WithdrawServiceRequest` + validator; the responses mapped from DTOs; exact day names ordinal, dates `yyyy-MM-dd` invariant, GUIDs `D`); `Api/Endpoints/Timetable/ServiceEndpoints.cs` (four endpoints, tag `Services`, `services.manage`/`services.read`, `RequestSizeLimitAttribute` 32 KiB create / 1 KiB withdraw as `CreateServiceMaxRequestBodyBytes` / `WithdrawServiceMaxRequestBodyBytes`); `Program.cs` maps them and binds `LocalTimeOptions` from `Time`, validated through `LocalTimeOptions.ResolveZone`, `ValidateOnStart()`; Api `appsettings.json` `"Time": { "LocalTimeZone": "Asia/Yangon" }`. **Worker:** `Program.Main` builds its configuration once (as the bootstrap path did), resolves `Time:LocalTimeZone` through the same resolver before either start path, and on failure writes the message to stderr and returns 1; Worker `appsettings.json` gains the same `Time` section.
- Tests (44 named): `Timetable/ServiceEndpointsTests` (34), `Timetable/ServiceRequestLimitTests` (5, real Kestrel), `Identity/ServicePermissionGrantTests` (2, real tokens), `Common/LocalTimeZoneStartupTests` (2, unmodified host), `IntegrationTests/Worker/WorkerStartupTests` (1 theory, 4 rows, the real process); changed-tests item 14: `DeployedShapeTests.ProtectedEndpoint_Anonymous_…` + the four service rows.
- **RED confirmed:** the Timetable API tests did not compile (CS0234 `YCR.Api.Contracts.Timetable`, `YCR.Api.Endpoints.Timetable`). With that folder set aside and no production code: `LocalTimeZoneStartupTests` 3 failed (the host started), `ServicePermissionGrantTests` 9/9 failed, the four new `DeployedShapeTests` rows failed, `WorkerStartupTests` 4/4 failed. `Startup_WithAsiaYangon_Starts` passed before and after (a regression guard).
- One test-side correction: `AnyServiceEndpoint_Anonymous_Returns401` (test-handler mode, as the plan maps it) first also asserted the `Auth.Unauthenticated` body, which the test handler's challenge does not write (F-003's `Get_Anonymous_Returns401` asserts the status only for the same reason). The body is asserted for the same four endpoints on the unmodified host by the four new `DeployedShapeTests` rows.
- **V7 recorded:** `GET /api/v1/services?routeId=abc` → framework `400`, `application/problem+json`, `{type, title, status, instance, traceId}`, no `errorCode` — exactly as a malformed `page` today; T-042's question (R-5).
- The empty `Time__LocalTimeZone` environment value reaches configuration on Windows (the Worker case with `""` exits 1 with the "not configured" message).

**Evidence:** targeted: Api Timetable 120/120, startup+grants+deployed 30/30, Integration 24/24; `dotnet test YCR.sln` **1158 total, 1158 passed, 0 failed, 0 skipped**; build 0 warnings.

**Found while preparing step 9 — a plan gap (see the next entry).**

**State of the branch:** builds; tests green locally; committed and pushed.

---

## 2026-09-25 18:45 Asia/Yangon — claude — T-046

**Stage:** 4 (IMPLEMENT) — **stopped before plan step 9. Blocked: `engineering` (two plan gaps; tech-lead ruling needed, no business OQ).**
**Commit:** code at `045be9f` (step 8); this entry is the commit after it.

**CI evidence.** Run [36114002714](https://github.com/heinmindev/ycr/actions/runs/36114002714) at `045be9f`: Build and test **failure** (1158 total, 1157 passed, **1 failed**, 0 skipped), API smoke test **failure**, Secret scan success, Trunk-only skipped (no PR). The step-7 run 36110923723 at `9237d1e` already showed gap G1 (Build and test failure; smoke success, because the startup check arrived in step 8). Steps 1–6 runs were cancelled by later pushes; `7f5ae96` (step 6) was green.

**G1 — `LocalCalendarTests.AsiaYangon_ResolvesInThisEnvironment` cannot be written as the plan names it.** The plan (§Test plan, Infrastructure; ruling Q3; V6) says: "its base offset is +06:30 with **no adjustment rules, i.e. no DST**". On Windows (ICU / "Myanmar Standard Time") that holds. On Linux tzdata it does not. Checked in `mcr.microsoft.com/dotnet/sdk:10.0` (Ubuntu 24.04.5, the runner's family) on 2026-09-25: `Asia/Yangon` resolves, `BaseUtcOffset = 06:30`, but `GetAdjustmentRules()` returns **9 historical rules** (LMT −00:05 base deltas to 1920; +02:30 base delta 1942-05-01..1945-05-02, the wartime +09:00), every one with `DaylightDelta = 00:00`, and `SupportsDaylightSavingTime` is **true** (a .NET/tzdata quirk, not real DST). `GetUtcOffset` is 06:30 and `IsDaylightSavingTime` false for 2026, 2030, 2040. In CI the test failed at its `SupportsDaylightSavingTime` assertion. Everything else about the zone passes on the runner, including both midnight-crossing theories (`17:29:59Z` / `17:30:00Z`).
  - *Proposed (not applied):* keep "resolves" and "+06:30", replace "no adjustment rules / no DST flag" by what the ruling means for F-004 dates: `GetUtcOffset(d) == +06:30` and `IsDaylightSavingTime(d) == false` for dates across the service horizon (e.g. 2026–2040), and every adjustment rule has `DaylightDelta == 0`. This changes a test assertion the approved plan spells out, so it is not a mechanical deviation.

**G2 — `api-smoke` does not start the API with its shipped `appsettings.json`.** Plan step 9 says the job "starts the built Worker (bootstrap) and the API with their shipped `appsettings.json`, so it proves the zone check passes on the Linux runner". True for the Worker (`Program.Main` reads `AppContext.BaseDirectory`). **False for the API:** `ci.yml` "Run the API" runs `dotnet src/YCR.Api/bin/Release/net10.0/YCR.Api.dll` from the repository root, so the content root is the repo root and `appsettings.json` is never loaded (it never was; `Auth:Issuer`/`Audience`/limits have code defaults, which hid it). With step 8's fail-closed check the API now refuses to start in CI: `System.InvalidOperationException: Time:LocalTimeZone is not configured…` (run 36114002714, "Run the API"). Reproduced locally the same way; the same binary started from its own directory loads the file and serves `/health/live` 200. The fail-closed behaviour itself is correct (ruling Q3); the question is how the deployed-shape API gets the setting.
  - *Options:* (a) add `Time__LocalTimeZone: Asia/Yangon` to the "Run the API" `env:` block, as every other deployment setting reaches it there — then the smoke proves the zone resolves on the runner but not that the shipped file is read; `CiWorkflowTests` inspects `env:` and must be re-checked; (b) run the API with its output directory as working directory (content root), so the shipped `appsettings.json` is loaded as the plan assumed — this also starts loading the file's `Auth` values (equal to the code defaults today) in the smoke, a wider change to the deployed-shape job; (c) something else hein prefers (e.g. `--contentRoot`). Also a stage-8 fact for `docs/15`: a deployment must run from the app directory or supply `Time__LocalTimeZone`.
  - *Recommendation:* (b) — it is the only one that makes the plan's step-9 claim true and exercises the shipped setting; (a) if hein prefers not to change the job's start shape.

**Not done (waiting for the rulings):** plan step 9 entirely — the `YCR.Api.http` "Services (F-004)" section and the `api-smoke` service checks (drafted, not applied); the green CI run; the ledger move to `review`.

**Next step (exact):** hein rules on G1 (the proposed assertion or another) and G2 (a/b/c). Then: apply G1 in `LocalCalendarTests` (record as a V-number, RED on Linux already observed in CI), apply the G2 choice in `ci.yml`, do step 9 (`.http`, the smoke checks), push, get CI green, move T-046 to `review`.

**Blockers / open questions:** `engineering` — G1, G2 above. No business OQ.

**State of the branch:** builds; `dotnet test YCR.sln` green locally (1158/1158, 0 skipped, Windows); CI red for G1 and G2 only; all work committed and pushed.

---

## 2026-09-25 21:49 Asia/Yangon — claude — T-046

**Stage:** 4 (IMPLEMENT) — **plan step 9 of 9 done; all steps done.** Resumed 20:57 after hein's rulings on G1 and G2 (ENGINEERING DECISIONS, 2026-09-25, recorded in the T-046 row).
**Commit:** code at `67797d6`; this entry is the commit after it.

**Done:**
- **Plan Amendment 1 (hein, 2026-09-25, T-046 G1)** recorded in plan §Amendments during implementation, §Test plan and §Review history. `LocalCalendarTests.AsiaYangon_ResolvesInThisEnvironment` now: (1) binds `LocalTimeOptions` from an in-memory `Time:LocalTimeZone = Asia/Yangon` and resolves it with `LocalTimeOptions.ResolveZone`; (2) asserts `GetUtcOffset` is exactly +06:30 at 1 Jan 00:00 and 1 Jul 00:00 local (as local `DateTime`) of every year 2026–2040, and at the instants one second before and at each of those local midnights; (3) asserts every adjustment rule whose `DateStart..DateEnd` overlaps 2026-01-01..2040-12-31 has `DaylightDelta` zero. `SupportsDaylightSavingTime` is no longer asserted. The midnight-crossing theory is unchanged.
  - Windows: the class 9/9. Linux: the same assertions in `mcr.microsoft.com/dotnet/sdk:10.0` (Ubuntu 24.04.5): 9 adjustment rules, **none** overlapping 2026–2040 (so assertion (3) holds vacuously there, as on Windows), `SupportsDaylightSavingTime` true, 0 failures; then green in CI on `ubuntu-latest`.
- **Plan Amendment 2 (hein, 2026-09-25, T-046 G2)** recorded in plan §Amendments, §OpenAPI/.http/smoke, step 9 and §Stage 8 (`docs/15`, `docs/local-development.md`: the API and the Worker run with their output directory as content root). `ci.yml` "Run the API" has `working-directory: src/YCR.Api/bin/Release/net10.0` and runs `dotnet YCR.Api.dll`; no `Time__` variable anywhere in the job. New smoke checks after the health checks: the API log's `Content root path:` is `$GITHUB_WORKSPACE/src/YCR.Api/bin/Release/net10.0`; `/proc/<api pid>/environ` has no `Time__`, `ASPNETCORE_Time__` or `DOTNET_Time__` variable.
  - **Auth values:** the shipped `appsettings.json` `Auth` section (`Issuer`/`Audience` `YCR.Api`, `PrincipalCacheSeconds` 15, `AllowedOrigins` `[]`, rate limits 5/20/30) equals the `AuthOptions` code defaults the job relied on before, and the job's `Auth__*` environment values still override; nothing changed, so no stop was needed. The file also brings `Logging` (Microsoft.AspNetCore at Warning) and `AllowedHosts: *`.
  - **Failure experiment:** throwaway branch `feature/F-004-g2-experiment` = `67797d6` + only the "Run the API" start reverted to the repository root (`7f1a16f`). Run [36150209178](https://github.com/heinmindev/ycr/actions/runs/36150209178): **API smoke test failure** at "Run the API" ("API did not become healthy"; log `System.InvalidOperationException: Time:LocalTimeZone is not configured…`), smoke step skipped; build and test and secret scan success. The branch was deleted afterwards. So the check that really fails when the file is not read is the API's own fail-closed startup; the content-root assertion names the cause.
  - **Worker:** unchanged in the job. Its `Program.Main` builds configuration from `AppContext.BaseDirectory`, not the working directory, so the bootstrap reads its shipped `appsettings.json` wherever it is started; the smoke's `bootstrap-administrator exits 0` (no `Time__` variable given) proves its zone check passes on the runner. Its no-command host path (`Host.CreateApplicationBuilder`) still takes the working directory as content root, hence the stage-8 docs line.
- **Step 9:** `YCR.Api.http` "Services (F-004)" section (header, `@serviceId`; on a closed route: Forward create 201, a wrap 201, a full circuit 201, out of order 422 `Timetable.ServiceStopsOutOfOrder`, same code overlapping 409 `Timetable.ServiceCodePeriodOverlap`, list, list by `routeId`, read, withdraw 204, withdraw later 422 `Timetable.WithdrawalDoesNotShorten`, the timetable-change pair; each comment names the permission and the errors). `api-smoke` service checks as plan step 9 lists (anonymous 401 + `Auth.Unauthenticated`; route `SMOKE2`; `SV1` from Yangon today 201 — the request also carries `nameEn`/`nameMy`, which the plan's sketch left out and the contract requires; read 200 with two stops; list 200; withdraw from today+30 204; again 422 `Timetable.WithdrawalDoesNotShorten`). `CiWorkflowTests` unchanged and green (confirmed).

**Evidence:** `dotnet test YCR.sln` locally **1158 total, 1158 passed, 0 failed, 0 skipped**; build 0 warnings. CI run [36150203657](https://github.com/heinmindev/ycr/actions/runs/36150203657) at `67797d6` on `feature/F-004`: **Build and test success** (1158/1158, 0 skipped), **API smoke test success** (every check PASS, 0 FAIL, the new content-root, `Time__` and service checks included), **Secret scan success**, Trunk-only skipped (runs only on a PR or `main`).

**Deviations:** none beyond the two amendments. Observation: assertion (3) of Amendment 1 is vacuous on both hosts today (no rule overlaps the horizon); it would bite if tzdata ever gained a rule for 2026–2040.

**Next step (exact):** T-046 to `review`. Stage 5 (scenario tests), code review and security review go to a different agent (AGENTS.md); not started by T-046.

**Blockers / open questions:** none.

**State of the branch:** builds; tests green locally and in CI; committed and pushed.

---

## 2026-09-26 02:55 Asia/Yangon — codex — T-051

**Stage:** Re-review of T-050 documentation and ADR-0026 against implementation SHA `c5d8516`.

**Done:** confirmed `c5d8516` is an ancestor of branch head `1cac48a`; the `71f0bab..1cac48a`
delta changes only `docs/`; reviewed each changed document, the three timetable migrations and EF
configurations, service handlers and lock implementations, API and Worker startup code, and the
architecture tests. Added the re-review to `review-codex.md`.

**Finding:** one Low documentation accuracy finding (L-1): `docs/15` and
`docs/local-development.md` abbreviate the missing/unknown local-time-zone startup messages, while
the API and Worker emit the full resolver messages. No Critical, High or Medium finding.

**Evidence:** isolated `dotnet test YCR.sln` — **1161 passed, 0 failed, 0 skipped**.

**Next step (exact):** T-052 human approval and merge review.

---

## 2026-09-26 00:01 Asia/Yangon — claude — T-050

**Stage:** 8 (DOCUMENT) — done. No review findings to remediate (codex stages 5–7 at `1bae50f`, `8c94408`, `71f0bab`: READY, no findings).
**Commit:** docs at `c5d8516`; this entry is the commit after it.

**Done:** the plan's stage-8 list, written against the code at `67797d6` (plus stage-5 tests), not only the plan:
- `docs/07`: §Core tables (`Trains` not used, OQ42; `TrainServices` → `Services`); §Module schemas ADR-0025 paragraph (cross-schema keys only to a never-deleted table's primary key, `NO ACTION`, no navigation, created by the referencing migration; why; one-way Timetable → Network); new §F-004 section: both tables, every check, index and key, the two cross-schema `NO ACTION` keys, why both convention indexes are declared, the access-path table, the three migration names, the code lock, the `ycr_app` grants and what is withheld.
- `docs/08`: §Initial resources marks `/trains` not provided (OQ42) and `PATCH /services` not provided (OQ48); new "Implemented in F-004 — services": four endpoints, permissions, contracts, day names, 200-stop cap, error codes in check order (R37) and the withdrawal order, 32 KiB / 1 KiB limits, framework `400`/`413`, lock timeout `500`, absent endpoints.
- `docs/20`: §1 "Cross-module reads" row (ADR-0025); §6 the own-transaction rule (ADR-0026: set invariant one unique index cannot enforce, lock scoped to the smallest key, transaction-owned, 30 s, timeout = `500`); §8 local dates only through `ILocalCalendar.Today()`.
- `docs/15`: new §F-004: `Time:LocalTimeZone`, both hosts refuse to start (messages), ICU/tzdata, the 2026-09-25 image facts (no plain `-chiseled`), content root = output directory (API), Worker reads its zone next to its DLL.
- `docs/local-development.md`: §2.3 migration count 6 → 12 (it was stale since F-003); §5 content root and time zone; two troubleshooting rows.
- ADR-0026 "Application locks for set invariants", **Proposed**, and its row in `docs/decisions/README.md`.
- `docs/glossary.md`: `Service` rewritten (C7); Service code, Direction (Forward/Reverse, wrap, full circuit), ServiceStop / stop, Operating days, Effective period, Withdrawal; Asia/Yangon local date extended; Myanmar terms all OPEN QUESTION.
- Not changed: spec, plan, `docs/10` (hein's T-050 instruction overrides the plan's "record the seed migration name in docs/10"; the name is in `docs/07`), `docs/19`, README (lists no endpoints), no code.

**Facts found while writing (no behaviour mismatch with the spec):** (1) the unhandled-exception `500` carries `errorCode` `Common.UnexpectedError` (`ProblemDetailsSetup.cs`), so ADR-0026 and `docs/08` say that rather than "no errorCode"; (2) `dotnet run --project` runs both hosts with the project directory as working directory (`RunWorkingDirectory`, checked with `dotnet msbuild -getProperty`), which holds the same `appsettings.json`, so the local-dev commands work from the repo root; only a built DLL must be started from its output directory.

**Evidence:** local `dotnet test YCR.sln` **1161 total, 1161 passed, 0 failed, 0 skipped**. CI at `c5d8516`: push run [36164377902](https://github.com/heinmindev/ycr/actions/runs/36164377902) — Build and test success (1161/1161, 0 skipped), API smoke test success, Secret scan success, Trunk-only skipped (push); pull_request run [36164382540](https://github.com/heinmindev/ycr/actions/runs/36164382540) — all four jobs success, Trunk-only included.

**Next step (exact):** T-051 — codex re-reviews the stage-8 docs and ADR-0026 against the code at `c5d8516` (not claude, AGENTS.md rule 11).

**Blockers / open questions:** none.

**State of the branch:** builds; tests green locally and in CI; committed and pushed.

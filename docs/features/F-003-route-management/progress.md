# Progress: F-003 Route management

Append-only. Newest entry at the bottom. Never edit or delete earlier entries.

---

## 2026-09-24 19:59 Asia/Yangon — claude — T-032

**Stage:** 1–2 (DISCOVER + SPECIFY, `docs/workflows/02-feature-development.md`) — stopped at the stage-2 ⛔.
**Commit:** spec at `483ddfa` on `feature/F-003`; this entry is the commit after it.

**Done this session:**
- Claimed T-032 in the coordination checkout: `git branch claim/T-032 main`, row set to doing/claude (`afa1e80`, pushed). Created the worktree `D:\MR\ycr-F-003` on `feature/F-003` from `main`.
- Read AGENTS.md, `TASKS.md` incl. §Protocol, workflows 01/02/04, templates (feature-spec, progress, adr), `docs/01` FR-001–003, `docs/03`, `04`, `05`, `07`, `08`, `10`, `12`, `19`, `20`, `21`, the glossary, `docs/business/mr-questions-pack.md` §OQ1, ADR-0002/0003/0004/0006/0012/0014/0017/0018/0021, the F-001 spec (whole) and F-002 spec §0.5, §0.8, R11, R20, and the Network code in `src/YCR.Domain/Network`, `src/YCR.Application/Network`, `src/YCR.Infrastructure/Persistence/Configurations/Network`, `src/YCR.Api/Endpoints/Network`, plus the `Security_AppDatabaseRole` migration.
- Wrote `docs/features/F-003-route-management/spec.md` (Status: Draft): §0 discovery (D1–D16, gaps G1–G9, contradictions C1–C7, engineering proposals E1–E8, **§0.8 Rulings needed from hein**), 24 labelled rules R1–R24, scenarios S1–S27, state changes, API, data/grants proposal, audit, out of scope, Blocked behaviour.
- Added **OQ36–OQ41** to `docs/19-open-questions.md`, each with a **BLOCKS:** line.
- Drafted **ADR-0024 (Proposed)**, client-held version for edits composed from an earlier read, and added it to `docs/decisions/README.md`.

**New OQs to add to `docs/business/mr-questions-pack.md`** (not edited in this stage, per instructions):
- **OQ36** — Which routes exist (one loop, one per direction, short workings/branches/other lines)? Is "line" distinct from "route"? Suggested owner: Network Operations / Planning.
- **OQ37** — Is a sequence closed or open; may a station repeat; minimum length? Network Operations / Planning.
- **OQ38** — Can a sequence change, and must earlier sequences be kept with effective dates? Network Operations / Planning, with Commercial/Fares for the ADR-0002 interaction.
- **OQ39** — May an inactive station be in a route; what does deactivating a station in a route do? Network Operations / Planning.
- **OQ40** — Who holds `routes.manage` and `routes.read`? Same routing as OQ28.
- **OQ41** — Route code/names (format, uniqueness, reuse) and lifecycle (withdraw, reactivate, delete)? Network Operations / Planning.

**Evidence:** documentation only; no build or test was run because no code changed. Commands: `git pull`, `git branch claim/T-032 main`, `git commit`/`git show --stat HEAD`/`git push origin main` (claim), `git worktree add -b feature/F-003 ../ycr-F-003 main`, `git diff`/`git status` checks, `git commit` (`483ddfa`). Files changed: `docs/features/F-003-route-management/spec.md` (new), `docs/features/F-003-route-management/progress.md` (new), `docs/19-open-questions.md` (OQ36–OQ41 appended), `docs/decisions/ADR-0024-client-held-version-for-edits.md` (new, Proposed), `docs/decisions/README.md` (one row).

**Next step (exact):** hein, at the ⛔ stop: rule on the spec §0.8 table — OQ36–OQ41 (Myanma Railways answers, or provisional tech-lead rulings labelled "not a Myanma Railways answer"), E1 (accept/amend ADR-0024), E2 (`routes.read` name), E3–E8. Then a stage-2 revision applies the rulings to the spec, `docs/19` and `docs/10`, and hein sets the spec Approved. Stage 3 (PLAN) must not start before that.

**Blockers / open questions:** `approval` (⛔ stage 2). Business: OQ36, OQ37, OQ38, OQ39, OQ40, OQ41 — all blocking. Engineering: ADR-0024 Proposed; E2–E8. Unchanged background: OQ1 (no seed data), OQ17 (out of scope).

**State of the branch:** documentation only; nothing to build or test. All work committed on `feature/F-003` and pushed.

---

## 2026-09-24 20:12 Asia/Yangon — claude — T-032

**Stage:** 2 (SPECIFY), revision applying hein's rulings — stopped at the stage-2 ⛔ again, for approval.
**Commit:** spec at `bee71fe` on `feature/F-003`; this entry is the commit after it.

**Done this session:**
- Applied hein's rulings of 2026-09-24 (OQ36–OQ41 as provisional tech-lead rulings, not a Myanma Railways answer; E1–E8) to `spec.md`: §0.8 rulings table; §3 relabelled (R2, R3, R5–R17 rewritten, R22/R23 adjusted, R25 added); §4 scenarios rewritten (S4, S16, S18, S19, S20, S27 removed with their numbers kept; S7–S12, S15, S17, S23, S25 made definite; S28–S30 added); §5–§9 and Blocked behaviour rewritten. Status: "Draft — all rulings applied, awaiting approval".
- `docs/19`: resolution block under each of OQ36–OQ41 (CRLF kept).
- `docs/10`: `routes.read` in the inventory; §Route permission grants — resolved (OQ40).
- ADR-0024: Proposed-stage review note in Follow-up only; status unchanged, README row unchanged.

**For hein at approval:** R25 (`isClosed` required, no default) is an engineering choice added in this revision; ADR-0024's Context/Follow-up still name F-003 as its first user (left as instructed); `IX_RouteStations_StationId` has lost its original reason (PLAN confirms or drops); the route-creation vs station-deactivation race is noted in §5 for PLAN.

**Evidence:** documentation only; no build or test run because no code changed. Commands: `git pull` (both checkouts), `git diff --stat`, `file` (line-ending checks), `git commit`, `git push`.

**Next step (exact):** hein reviews the spec and sets it Approved (or amends). Stage 3 (PLAN) must not start before that.

**Blockers / open questions:** `approval` (⛔ stage 2). OQ36–OQ41 still open with Myanma Railways (not blocking). OQ1, OQ17 unchanged.

**State of the branch:** documentation only; all work committed on `feature/F-003` and pushed.

---

## 2026-09-24 20:31 Asia/Yangon — claude — T-032

**Stage:** 2 (SPECIFY) complete — spec **Approved (hein, 2026-09-24)**.
**Commit:** approved spec at `15d3709` on `feature/F-003`; this entry is the commit after it.

**Done this session:**
- Applied hein's approval-time decisions: **A1** `Routes.DeactivatedAtUtc datetimeoffset(3) NULL` + `CK_Routes_DeactivatedAtUtc_Utc`, set with `IsActive = 0` in one `UPDATE`, exposed as `deactivatedAtUtc` in `RouteResponse`, `RouteSummaryResponse` and `RouteAuditSnapshot`, grant `UPDATE(IsActive, DeactivatedAtUtc)` (R10, R15, R17, S23, S25, §5–§8); R25 relabelled as accepted; `IX_RouteStations_StationId` left to PLAN (keep only if a named query uses it); the creation/station-deactivation race stated as accepted without serialisation (§5). All recorded in §0.8 "Decisions at approval".
- ADR-0024 (still Proposed): Follow-up line now says F-003 does not use it.
- Spec Status set to "Approved (hein, 2026-09-24)".

**Evidence:** documentation only; no build or test run because no code changed. Commands: `git pull` (both checkouts), `grep` stale-phrase checks, `git commit`, `git push`.

**Next step (exact):** stage 3 (PLAN) for F-003, as a new task in `TASKS.md`. Not started by T-032.

**Blockers / open questions:** none for F-003. OQ36–OQ41 still open with Myanma Railways (provisional rulings apply). OQ1, OQ17 unchanged.

**State of the branch:** documentation only; all work committed on `feature/F-003` and pushed.

---

## 2026-09-24 20:52 Asia/Yangon — claude — T-034

**Stage:** 3 (PLAN, `docs/workflows/02-feature-development.md`) — stopped at the stage-3 ⛔.
**Commit:** plan at `00ce225` on `feature/F-003`; this entry is the commit after it.

**Done this session:**
- Claimed T-034 in the coordination checkout: `git branch claim/T-034 main`, row set to doing/claude (`39b3153`, pushed).
- Read AGENTS.md, `TASKS.md` (Protocol, T-032, T-034), the Approved spec (`15d3709`) and this file, `docs/templates/plan.md`, workflows 02 and 04, `docs/07`, `08`, `10`, `18`, `20`, `21`, the glossary's Network section, the F-001 and F-002 plans, and the existing code: the Network domain/application/infrastructure/API slice, `Permissions`, `YcrDbContext`, the unique-constraint translator, the `Security_AppDatabaseRole`, `Security_IdentityGrants` and `Identity_SeedRolesAndPermissionGrants` migrations, `DatabasePrivilegeTests`, `IdentitySeedTests`, `StationModelTests`, `YCR.Api.http` and the `api-smoke` job.
- Verified five EF Core 10.0.12 behaviours in a scratch console app outside the repository (plan §Verifications done at PLAN, O1–O5). The key finding: EF's FK-index convention adds `IX_RouteStations_StationId` and adds it back when it is removed; reversing the unique index to `(StationId, RouteId)` is the only local way to drop it → **Q1**.
- Wrote `docs/features/F-003-route-management/plan.md` (Status: Draft): P1–P12 decisions, Q1–Q3 questions for hein, three migrations (`Network_CreateRoutes`, `Identity_SeedRoutePermissionGrants`, `Security_NetworkRouteGrants`) with rollback and roll-forward, the index decision (dropped; no named query uses it), the endpoint inventory, audit, security, a test plan mapping all 24 live scenarios to 93 named tests (86 new, 7 existing tests changed), and 7 steps ending with a green GitHub Actions run.

**Evidence:** documentation only; no build or test was run because no repository code changed. Scratch verification: `dotnet run` of a throwaway project in the session scratchpad (EF Core SqlServer/Design 10.0.12, SDK 10.0.302). Commands: `git pull` (both checkouts), `git branch claim/T-034 main`, `git commit`/`git show --stat HEAD`/`git push origin main` (claim), `grep`/`sed`/`cat` reads, `git commit` (`00ce225`). Files changed: `docs/features/F-003-route-management/plan.md` (new), this file.

**Next step (exact):** hein reviews `plan.md`: confirms or overturns P1–P12 and rules on **Q1** (index name/columns; blocks step 2), **Q2** (`stationIds` upper limit), **Q3** (stage-8 glossary edit vs spec §9). If Q1 is ruled (a), the spec's index name gets a recorded stage-2 amendment first. Then stage 4 (IMPLEMENT) starts as a new task, following the plan's steps.

**Blockers / open questions:** `approval` (⛔ stage 3). Q1–Q3 for hein (not Myanma Railways questions; no new OQ in `docs/19`). OQ36–OQ41 still open with Myanma Railways (provisional rulings apply). OQ1, OQ17 unchanged.

**State of the branch:** documentation only; all work committed on `feature/F-003` and pushed.

---

## 2026-09-24 21:00 Asia/Yangon — claude — T-034

**Stage:** 3 (PLAN) complete — plan **Approved (hein, 2026-09-24)**.
**Commit:** spec amendments at `bcbba09`, approved plan at `84f8ad8`, both on `feature/F-003`; this entry is the commit after them.

**Done this session:**
- Applied hein's rulings of 2026-09-24 on the plan questions to the spec, as §0.9 **Amendment 1** (Q1 (a): `UX_RouteStations_StationId_RouteId` on `(StationId, RouteId)` replaces `UX_RouteStations_RouteId_StationId`; `IX_RouteStations_StationId` dropped; R7, §7, the OQ37 and IX rows in §0.8 and Notes updated) and **Amendment 2** (Q2 (b): new R26, `stationIds` at most 200, a REQUIRED CONTROL; S5 and §6 updated. Q3 (a): §9 now allows glossary edits at stage 8; the MR questions pack stays out of scope). The Status line stays "Approved (hein, 2026-09-24)" and adds "amended by Amendments 1–2".
- Plan revision 2: the questions table replaced by "Rulings on the plan questions (hein, 2026-09-24)"; every Q-conditional row resolved (P6 validator cap, P7, `NetworkConstraints`, `RouteStationConfiguration`, §DB changes, security, S5 tests, R-1 retired, R-7, steps, stage-8 list, stop point); Status "Approved (hein, 2026-09-24)". Test plan now 94 named tests (87 new, 7 existing changed) for the 24 live scenarios.

**Evidence:** documentation only; no build or test was run because no code changed. Commands: `git pull` (both checkouts), `grep`/`sed` checks, `git commit`, `git push`.

**Next step (exact):** T-035, stage 4 (IMPLEMENT), following `plan.md` §Steps 1–7 from step 1. Not started by T-034.

**Blockers / open questions:** none for F-003. OQ36–OQ41 are still open with Myanma Railways (the provisional rulings apply). OQ1 and OQ17 are unchanged.

**State of the branch:** documentation only; all work committed on `feature/F-003` and pushed.

---

## 2026-09-24 21:24 Asia/Yangon — claude — T-035

**Stage:** 4 (IMPLEMENT), plan step 1 of 7 (Domain) — done.
**Commit:** step 1 code and this entry are one commit on `feature/F-003` (see `git log`).

**Setup:** claimed T-035 (`git branch claim/T-035 main`; ledger commit "chore(tasks): claim T-035", pushed). Worktree at `999c392`; the spec (Approved, Amendments 1–2) and plan (Approved, revision 2 at `84f8ad8`) are the ones read. Baseline before any change: `dotnet test YCR.sln` **643 passed, 0 failed, 0 skipped**.

**Naming used in this log:** the plan's own verification items are written **plan-V1…plan-V5**. Deviations from the plan are **V1, V2, …** (the F-002 convention).

**Done:**
- `NetworkCodeFormat` (the shared `^[A-Z0-9]{2,10}$`, P2); `StationCode` now calls it (behaviour unchanged); `RouteCode` (`Network.InvalidRouteCode`); `BilingualName.Create(en, my, whenInvalid)` with the two-argument overload delegating with `InvalidStationName` (P3); route errors in `NetworkErrors`; `RouteStation`; `Route` (`Create` with the P4 precedence R7 → R8 → R16 → R11, `Deactivate(nowUtc)`); `RouteDeactivated`.
- Tests written in this step (RED seen first: the build failed on the missing `Route`/`RouteCode` types): `RouteCodeTests` (2 theories), `RouteTests` (16), `BilingualNameTests` +3. Existing `StationCodeTests`, `StationTests` and the existing `BilingualNameTests` cases are unmodified and pass.

**Deviations:**
- **V1 — one extra domain test.** `BilingualName_Create_WithRouteError_WhenValid_ReturnsTrimmedNames` is added beside the two the plan names, so the overload's success path is covered too. Domain count: 21 new, not 20. Nothing is removed or weakened.

**Evidence:** `dotnet test tests/YCR.Domain.Tests` 126/126. `dotnet build YCR.sln` 0 warnings, 0 errors. `dotnet test YCR.sln` **681 passed, 0 failed, 0 skipped**.

**Next step (exact):** plan step 2 (persistence: permissions, `INetworkDbContext.Routes`, constraints, audit subject, the two EF configurations, migration `Network_CreateRoutes`, plan-V4).

**Blockers / open questions:** none.

**State of the branch:** builds; tests green; committed and pushed.

---

## 2026-09-24 21:35 Asia/Yangon — claude — T-035

**Stage:** 4 (IMPLEMENT), plan step 2 of 7 (Persistence) — done.
**Commit:** step 2 code and this entry are one commit on `feature/F-003`.

**Done:**
- `Permissions.RoutesManage`/`RoutesRead`; `INetworkDbContext.Routes` (no `DbSet<RouteStation>`); `NetworkConstraints.RouteCodeUniqueIndex`/`RouteStationUniqueIndex`; `NetworkAuditSubjects.Route`; `RouteConfiguration`, `RouteStationConfiguration`; `YcrDbContext.Routes`.
- Migration **`20260924145627_Network_CreateRoutes`**, generated by `dotnet ef migrations add` and reviewed by hand: both tables as plan §DB changes; `PK_Routes`, `UX_Routes_Code`, `CK_Routes_CreatedAtUtc_Utc`, `CK_Routes_DeactivatedAtUtc_Utc`; `PK_RouteStations (RouteId, Position)`, `CK_RouteStations_Position`, `UX_RouteStations_StationId_RouteId (StationId, RouteId)`, both FKs `NO ACTION`; **no `IX_RouteStations_StationId`**; `Down()` drops the two tables only. The only hand edit is the XML summary/remarks header.
- **plan-V4 passed:** `dotnet ef migrations has-pending-model-changes` → "No changes have been made to the model since the last migration."
- Tests written in this step (RED seen first: build failed on the missing `Network_CreateRoutes`): `RouteModelTests` (5: `RouteModel_MapsTablesKeysAndConstraintNames`, `RouteModel_RouteStationsHasNoStationIdOnlyIndex` — asserts the model **and** the migration's `Up` operations, `RouteModel_ForeignKeys_AreNoActionWithoutStationNavigation`, `RouteModel_DeclaresPositionCheck`, `DownMigration_NetworkCreateRoutes_DropsTablesButNotNetworkSchema`); `RouteMigrationTests` (3: `RouteConstraints_RejectNonUtcOffsetsAndNonPositivePositions`, `RouteStations_SameStationTwiceInOneRoute_RejectedByUniqueIndex`, `RouteStations_UnknownStation_RejectedByForeignKey`, migrator); `Model_WithUtcColumn_DeclaresItsCheckConstraint` +2 rows.

**Deviations:**
- **V2 — the down-migration test lives in `RouteModelTests`.** The plan lists `DownMigration_NetworkCreateRoutes_…` in the Infrastructure table without a file. It needs no database, so it sits with the other model-level tests, as `StationModelTests` holds F-001's.

**Evidence:** `dotnet build YCR.sln` 0 warnings, 0 errors. Targeted: 16/16. `dotnet test YCR.sln` **691 passed, 0 failed, 0 skipped**.

**Next step (exact):** plan step 3 (seed migration `Identity_SeedRoutePermissionGrants`; the 14 → 24 test updates).

**Blockers / open questions:** none.

**State of the branch:** builds; tests green; committed and pushed.

---

## 2026-09-24 21:55 Asia/Yangon — claude — T-035

**Stage:** 4 (IMPLEMENT), plan step 3 of 7 (Seed) — done.
**Commit:** step 3 code and this entry are one commit on `feature/F-003`.

**Done:**
- Migration **`20260924150657_Identity_SeedRoutePermissionGrants`**, scaffolded by `dotnet ef migrations add` (the model snapshot did not change; the Designer is EF's), with the SQL written by hand: exactly ten `identity.RolePermissions` rows, `routes.manage` → `SystemAdministrator`, `RailwayAdministrator`; `routes.read` → all eight roles. Role ids re-declared as constants. Header "BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-24; T-032, OQ40) — not a Myanma Railways answer". `Down()` deletes exactly those ten pairs. Checked against spec R2/R3 and `docs/10` §Route permission grants: identical.
- Tests, updated in this step (RED seen first: 3 failed, 14 ≠ 24 and the docs/10 route rows missing from the seed): `Seed_ProducesExactlyEightRolesAndFourteenGrants` renamed `…TwentyFourGrants`, with the ten route rows in its exact expected set; `Seed_MatchesDocs10GrantTables` now parses `## Route permission grants` with the station bullet parser, and asserts each bullet section yields grants; `ApplicationCredential_CannotWriteRolesOrGrants` asserts 24. `Seed_EveryPermissionIsAPermissionsConstant` passes unchanged.
- plan-V4 re-checked: no pending model changes.

**Deviations:**
- **V3 — five more F-002 tests pinned the seeded grants, and the plan did not name them.** The plan's FACT list names two tests that assert 14 grants; the first full run after the seed found five more that assert a role's exact permission set or the total. Each was updated to the **exact new set** (an equality, still exact, not loosened), with a one-line comment citing `docs/10` §Route permission grants. This is the plan's own R-3 mitigation ("F-002 tests that pinned 14 … updated to the exact new set in step 3 … extended, not loosened"), applied to tests its list missed. **hein to confirm.** The five:
  - `YCR.Api.Tests` `UserAdministrationEndpointTests.ListRoles_ReturnsEightRolesWithPermissions`: total 14 → 24.
  - `YCR.Application.Tests` `AdministrationHandlerTests.ListRoles_ReturnsEightRolesWithPermissions`: `SystemAdministrator` set + `routes.manage`, `routes.read`; `ReportingUser` `[stations.read]` → `[routes.read, stations.read]`.
  - `YCR.Application.Tests` `SessionHandlerTests.Resolve_ActiveSession_ReturnsUserRolesAndPermissionUnion`: union + `routes.manage`, `routes.read`.
  - `YCR.Application.Tests` `SessionHandlerTests.GetCurrentUser_ReturnsUserNameRolesAndPermissions`: `RailwayAdministrator` set + the two route permissions.
  - `YCR.Api.Tests` `PasswordEndpointTests.Me_ReturnsUserNameRolesAndPermissions`: same, with the `Permissions` constants.

**Evidence:** targeted seed tests RED (3 failed of 5) then 5/5; the five V3 tests 5/5 after the update. `dotnet build YCR.sln` 0 warnings, 0 errors. `dotnet test YCR.sln` **691 passed, 0 failed, 0 skipped** (the first full run after the seed had 4 failures, the V3 tests not yet updated; the fifth, the API total, was updated beforehand).

**Next step (exact):** plan step 4 (migration `Security_NetworkRouteGrants`; the new `DatabasePrivilegeTests` cases and the two DDL rows; the `RouteMigrationTests` upgrade and down tests).

**Blockers / open questions:** none. V3 is for hein's review.

**State of the branch:** builds; tests green; committed and pushed.

---

## 2026-09-24 22:08 Asia/Yangon — claude — T-035

**Stage:** 4 (IMPLEMENT), plan step 4 of 7 (Grants) — done.
**Commit:** step 4 code and this entry are one commit on `feature/F-003`.

**Done:**
- Migration **`20260924152836_Security_NetworkRouteGrants`**, scaffolded by `dotnet ef migrations add` (snapshot unchanged), SQL by hand, exactly spec R10 / S25: `GRANT SELECT, INSERT ON [network].[Routes]`; `GRANT UPDATE ON [network].[Routes]([IsActive], [DeactivatedAtUtc])`; `GRANT SELECT, INSERT ON [network].[RouteStations]`, all to `[ycr_app]`. `Down()` revokes exactly these, in reverse order. The XML comment states R9 and lists the absences.
- Tests written in this step (RED seen first: the grant tests returned 0 for the presences, and the migration tests found no grants): `DatabasePrivilegeTests.ApplicationCredential_HasExactlyTheRouteGrants` (every presence and absence the plan lists, plus table-level `UPDATE` on `Routes` = 0), `ApplicationCredential_CanDeactivateARouteButNotRewriteIt` (executed as `ycr_app`: the two-column deactivation succeeds; seven rewrites and deletes are denied; rows unchanged afterwards), `ApplicationCredential_AttemptingDdl_IsDenied` +2 rows (`ALTER TABLE [network].[Routes] ADD …`, `DROP TABLE [network].[RouteStations]`); `RouteMigrationTests.Migrate_FromF002Schema_CreatesRouteTablesConstraintsIndexesAndGrants` (from `20260923133743_Security_IdentityGrants` with a station row: tables, the three checks, exactly four indexes incl. `UX_RouteStations_StationId_RouteId` key order `StationId, RouteId` and **no `IX_RouteStations_StationId`**, both FKs `NO_ACTION`, the role's six grants, 10 route + 24 total grant rows) and `Migrate_DownToF002_RemovesRouteObjectsGrantsAndSeedRowsAndKeepsStations` (with a route in it: tables and grants gone, 14 grant rows, `network` schema, the station and its `UPDATE(IsActive)` grant kept, then forward again).
- plan-V4 re-checked: no pending model changes.

**Deviations:** none new. (Two SQL mistakes in my own new test queries — a catalog collation clash and an `ORDER BY` inside `IdentitySql.StringsAsync`'s derived table — were fixed before the tests were run green; they were test-authoring errors, not plan issues.)

**Evidence:** targeted 11/11 (the 7 DDL cases passed before the migration too, as absences should). `dotnet build YCR.sln` 0 warnings, 0 errors. `dotnet test YCR.sln` **697 passed, 0 failed, 0 skipped**.

**Next step (exact):** plan step 5 (Application: `RouteAuditSnapshot`, `CreateRoute`, `DeactivateRoute`, `GetRoute`, `ListRoutes`; plan-V1, plan-V2, plan-V3).

**Blockers / open questions:** none.

**State of the branch:** builds; tests green; committed and pushed.

---

## 2026-09-24 22:24 Asia/Yangon — claude — T-035

**Stage:** 4 (IMPLEMENT), plan step 5 of 7 (Application) — done.
**Commit:** step 5 code and this entry are one commit on `feature/F-003`.

**Done:**
- `RouteAuditSnapshot` + `RouteStationAuditSnapshot` (records; stations in position order with their codes); `CreateRoute` (command, handler: P5 order, one station query, `Route.Create`, `409` pre-check, one save; `UX_Routes_Code` → `409`, `UX_RouteStations_StationId_RouteId` → `RouteStationRepeated`); `DeactivateRoute` (loads the route with its stations and the station codes, `Route.Deactivate(clock.GetUtcNow())`, audit before/after, one save, `DbUpdateConcurrencyException` → `RouteAlreadyInactive`); `GetRoute` (`RouteDto`, `RouteStationDto`, `RouteProjection`; two `AsNoTracking` projections); `ListRoutes` (`RouteSummaryDto`; `COUNT`, then a page with a correlated `stationCount`).
- **plan-V1 passed:** deactivation emits exactly one `UPDATE [network].[Routes] SET [DeactivatedAtUtc] = @p, [IsActive] = @p … WHERE [Id] = @p AND [IsActive] = @p`, and no `INSERT`/`UPDATE`/`DELETE` on `RouteStations` (only the audit `INSERT` beside it). No fallback needed.
- **plan-V2 passed:** a route read is two statements; the stations are one `SELECT … JOIN [network].[Stations] … ORDER BY`.
- **plan-V3 passed:** the list is two round trips: a `COUNT(*)` with no `RouteStations`, then `ORDER BY … OFFSET … FETCH NEXT` with `stationCount` as a `COUNT(*)` subquery over `RouteStations`.
- Tests written in this step (RED seen first: the build failed on the missing `CreateRoute`/`DeactivateRoute`/`GetRoute`/`ListRoutes` namespaces): `CreateRouteHandlerTests` (13, incl. parallel S21 and the S24 loser test), `DeactivateRouteHandlerTests` (5, incl. parallel S23), `RouteQueryHandlerTests` (7), `ListRoutesSqlTests.ListRoutesSql_PagesCountsAndStationCountsInSql`, `DeactivateStationHandlerTests.DeactivateStation_WhenStationIsInActiveRoute_DeactivatesAndLeavesRouteStationsUnchanged` (S17), the DI test with the four route handlers (22 → 26 handlers).

**Deviations:**
- **V4 — the DI test the plan names does not exist under that name.** The plan says `DependencyInjection_ResolvesEveryHandler`; the existing test that names each handler is `DependencyInjectionTests.HandlerTypes_IncludesEveryHandlerDefined`. The four route handlers were added there and its exact count went 22 → 26. `AddApplication_RegistersEveryHandlerInTheAssembly` covers resolution unchanged.
- **V5 — two extra SQL tests make plan-V1 and plan-V2 assertions, not notes.** `ListRoutesSqlTests` also holds `GetRouteSql_ReadsTheRouteAndItsStationsInTwoStatements` (plan-V2) and `DeactivateRouteSql_UpdatesOnlyIsActiveAndDeactivatedAtAndNoRouteStations` (plan-V1). They capture SQL through `services.ConfigureDbContext<YcrDbContext>(… LogTo …)` on the real composition. The plan asked for these verifications, not for tests; tests keep them from regressing.
- **V6 — `RouteTestData.cs` holds a base class.** The plan describes it as "a helper that inserts stations through `CreateStationHandler`". It is `RouteHandlerTestBase : NetworkHandlerTestBase`: the same helpers, plus a `TestClock` registered before `AddInfrastructure` (which uses `TryAddSingleton(TimeProvider.System)`), so the S23 tests can assert "the clock's UTC now". `NetworkHandlerTestBase` itself is unchanged.
- Note (inside the plan): the station-index catch in `CreateRouteHandler` is unreachable through `Route.Create`, which refuses repeats first. It returns `RouteStationRepeated` naming the first repeated id in the command, or the first id if there is none. No test reaches it.

**Evidence:** targeted Application run 50/50. `dotnet build YCR.sln` 0 warnings, 0 errors. `dotnet test YCR.sln` **736 passed, 0 failed, 0 skipped**.

**Next step (exact):** plan step 6 (API: `RouteContracts` + validator with the 200 cap, `RouteEndpoints`, `Program.cs`; `RouteEndpointsTests`, `RoutePermissionGrantTests`, `DeployedShapeTests` rows; plan-V5).

**Blockers / open questions:** none.

**State of the branch:** builds; tests green; committed and pushed.

---

## 2026-09-24 22:41 Asia/Yangon — claude — T-035

**Stage:** 4 (IMPLEMENT), plan step 6 of 7 (API) — done.
**Commit:** step 6 code and this entry are one commit on `feature/F-003`.

**Done:**
- `Contracts/Network/RouteContracts.cs`: `CreateRouteRequest` (`isClosed` `bool?`, `stationIds` `IReadOnlyList<string?>?`, P6) with `CreateRouteRequestValidator` (`NotEmpty` code/names, `NotNull` `isClosed` (R25), `stationIds` present, non-empty and **at most `MaxStationIds = 200`**, REQUIRED CONTROL R26, each element a `D`-format GUID); `CreateRouteResponse`, `RouteResponse`, `RouteStationResponse`, `RouteSummaryResponse`, each mapped explicitly from its DTO; `PagedResponse<T>` reused.
- `Endpoints/Network/RouteEndpoints.cs`: the four endpoints, tag `Routes`, `routes.manage` on both POSTs and `routes.read` on both GETs, `.WithName`/`.WithSummary`/`.Produces`/`.ProducesProblem` as `StationEndpoints`. `Program.cs`: `api.MapRouteEndpoints();`. The station endpoints are not changed.
- **plan-V5 recorded (probe, not committed):** a JSON type mismatch (`"isClosed": "yes"`, `"stationIds": "x"`) or a malformed body gives `400 application/problem+json` with `traceId` and **no `errorCode`**, from the framework's binding failure. As plan R-6 says, this predates F-003, applies to every endpoint, and is not in S5, so F-003 does not change it. If hein wants an `errorCode` there, it is a cross-cutting follow-up task.
- Tests written in this step (RED seen first: the build failed on the missing response contracts): `RouteEndpointsTests` (26 test methods as the plan lists them), `RoutePermissionGrantTests` (2, real ES256 tokens: `TicketOperator_ReadsRoutesButCannotCreateOrDeactivate`, `RailwayAdministrator_CreatesAndDeactivatesRoute`), `DeployedShapeTests.ProtectedEndpoint_Anonymous_Returns401BearerChallengeWithProblemDetails` +4 route rows. Every F-001/F-002 API suite passes unchanged (apart from the V3 updates at step 3).

**Deviations:**
- **V7 — a whitespace-only name at the endpoint is `400 Common.ValidationFailed`, not `400 Network.InvalidRouteName`.** Spec S11 lists "a blank, whitespace-only … name → `400 Network.InvalidRouteName`". Plan P6 (approved) makes the validator `NotEmpty()` "as in `CreateStationRequestValidator`", and FluentValidation's `NotEmpty()` treats a whitespace-only string as empty. So at the API, `"   "` is refused by the filter, exactly as F-001's station endpoint does (see `StationEndpointsTests.Post_WithMissingField_Returns400FromTheValidationFilter` and `Post_WithOverlongMyanmarName_Returns400FromTheDomainRule`). Both are `400`. The domain rule (`BilingualName` → `Network.InvalidRouteName` for whitespace) is proven at the handler by `CreateRoute_WithInvalidCodeOrName_ReturnsValidationErrorAndWritesNothing`. At the endpoint, `Post_WithInvalidName_Returns400InvalidRouteName` uses the two overlong cases, and `Post_WithInvalidBody_Returns400CommonValidationFailed` gains a "whitespace nameEn" row that pins the filter's answer. **hein to confirm**; if S11's code must hold at the endpoint too, the validator would use `NotNull()` for the names (a one-line change plus the row).
- **V8 — one extra row in the S5 theory**, the "whitespace nameEn" row above, beyond the nine the plan lists.

**Evidence:** targeted API run 62/62 (`RouteEndpointsTests`, `RoutePermissionGrantTests`, `DeployedShapeTests`). `dotnet build YCR.sln` 0 warnings, 0 errors. `dotnet test YCR.sln` **789 passed, 0 failed, 0 skipped**.

**Next step (exact):** plan step 7 (`YCR.Api.http` Routes section; `api-smoke` route checks in `.github/workflows/ci.yml`; push; a green GitHub Actions run on `feature/F-003`; record the run URL here).

**Blockers / open questions:** none. V7 is for hein's review.

**State of the branch:** builds; tests green; committed and pushed.

---

## 2026-09-24 23:16 Asia/Yangon — claude — T-035

**Stage:** 4 (IMPLEMENT), plan step 7 of 7 (`.http` and CI) — done. **Stage 4 complete; T-035 → `review`.**
**Commit:** **code SHA `910724e`** (step 7). This entry is a docs-only commit after it (CI skips `docs/features/**/progress.md`).

**Done:**
- `YCR.Api.http`: a "Routes (F-003)" section with the seven requests plan §API changes lists (open route with 3 stations → `201`; closed route with 2 → `422 Network.RouteTooFewStations`; repeated station → `422 Network.RouteStationRepeated`; list; read; deactivate → `204`; again → `422 Network.RouteAlreadyInactive`), each commented with its permission and errors, plus the `@stationId2`, `@stationId3` and `@routeId` variables.
- `.github/workflows/ci.yml` `api-smoke`: the six route checks the plan lists (as 11 `check` lines), after the existing real-token calls, with the same administrator token. `CiWorkflowTests` passes **unchanged**, which confirms the plan's statement that it needs no change.

**GitHub Actions — run https://github.com/heinmindev/ycr/actions/runs/36027185726 on `910724e207dd2223c8d16088547b6ed1763617c0` (push): success.**
- Build and test: success. Log: `has-pending-model-changes` "No changes have been made to the model since the last migration."; **789 passed, 0 failed, 0 skipped**.
- API smoke test: success, **37/37 checks PASS** (26 from F-001/F-002 plus the 11 route checks).
- Secret scan: success.
- Trunk-only tests: **skipped**. The job runs only on `pull_request` and on `main` (`if:` in `ci.yml`), so a push to `feature/F-003` never runs it. That is the design, and F-002 recorded the same (run 35955113706). No PR was opened, since that was not asked for. As evidence instead, the trunk-only suite was run locally in trunk-only mode (`YCR_RUN_TRUNK_ONLY_TESTS=1 dotnet test YCR.sln`): **4 passed, 0 failed, 0 skipped**. With the branch run's 789, all 793 tests ran. The CI job itself will run when a PR into `main` is opened.

**Stage 4 summary (per step: commit, tests added, `dotnet test YCR.sln` total):**
1. `8278dcb` Domain — 21 new domain tests (V1) — 681/681, 0 skipped.
2. `57eb024` Persistence + `Network_CreateRoutes` — 8 new tests + 2 UTC-theory rows — 691/691.
3. `f0911f4` Seed — 0 new; 3 planned test updates + 5 unplanned F-002 updates (V3) — 691/691.
4. `d365d50` Grants — 4 new tests + 2 DDL-theory rows — 697/697.
5. `ad72396` Application — 27 planned new tests + 2 SQL tests (V5) — 736/736.
6. `9dde7a3` API — 28 planned new tests (26 TH + 2 real-token) + 4 `DeployedShapeTests` rows + 1 extra theory row (V8) — 789/789.
7. `910724e` `.http` + CI — no tests — 789/789; CI green (above).
Baseline before step 1: 643/643.

**Migrations:** `20260924145627_Network_CreateRoutes`, `20260924150657_Identity_SeedRoutePermissionGrants`, `20260924152836_Security_NetworkRouteGrants`.

**Deviations (all recorded in their step's entry above):** V1 extra domain test; V2 down-migration test file; **V3 five unplanned F-002 tests updated to the new exact grant sets (hein to confirm)**; V4 DI test name; V5 two extra SQL tests for plan-V1/V2; V6 `RouteTestData.cs` holds a base class; **V7 whitespace-only name at the endpoint is `Common.ValidationFailed` (the F-001 filter behaviour), not `Network.InvalidRouteName` (hein to confirm)**; V8 one extra S5 theory row. Plan verifications: plan-V1 to plan-V4 passed with no fallback; plan-V5 recorded (framework `400` without `errorCode` on a JSON type mismatch, pre-existing, unchanged).

**Not done here, on purpose:** stage-8 documentation (`docs/07`, `docs/08`, glossary, README) is left for stage 8. Stages 5–7 go to codex (AGENTS.md rule 11); no review was run by the implementer.

**Next step (exact):** codex — stage 5 (scenario tests), 6 (code review), 7 (security review) against code SHA `910724e`.

**Blockers / open questions:** none blocking. For hein: V3 and V7 above; whether to open a PR now so the trunk-only CI job runs on this branch.

**State of the branch:** builds; tests green; everything committed and pushed.

## 2026-09-25 08:20 Asia/Yangon — agent: codex — T-036

**Stage:** 5 (TEST) — complete.
**Commit:** pending stage-5 commit.
**Done this session:**
- Traced every live scenario S1–S3, S5–S15, S17, S21–S26 and S28–S30, including R26's 200-id boundary, in `review-codex.md` §Stage 5.
- Traced R10 grants, R18 positions, R24 current station projections and A1 `DeactivatedAtUtc` despite having no dedicated scenario numbers.
- No additional test code was needed; existing tests assert each listed behavior.

**Evidence:** `dotnet test YCR.sln` — 789 passed, 0 failed, 0 skipped. Report added at `docs/features/F-003-route-management/review-codex.md`.

**Next step (exact):** commit and push the stage-5 report, then claim T-037 from the coordination checkout.

**Blockers / open questions:** none.

**State of the branch:** tests green; report and progress are uncommitted.

## 2026-09-25 09:05 Asia/Yangon — agent: codex — T-038

**Stage:** 7 (SECURITY REVIEW) — complete.
**Commit:** pending stage-7 commit.
**Done this session:**
- Reviewed SHA `910724e` against `docs/18`, `docs/09`, `docs/10`, ADR-0017/0021 and the route implementation/tests.
- Verified all endpoint 401/403 paths, route/station permission separation, exact seeded grants, least-privilege `ycr_app` route grants and no DDL, Unicode handling, server-derived audit actors, payload contents and CI smoke secret handling.
- Added Stage 7 report with S-1 Medium: no explicit route request-size/JSON boundary before model binding and no huge/malformed-hosting test. No Critical or High findings.

**Evidence:** `dotnet test YCR.sln` — 789 passed, 0 failed, 0 skipped. Route API, grant, privilege, audit and CI workflow tests are included in that run.

**Next step (exact):** commit and push the Stage 7 report, then close T-038 in the coordination checkout and delete its claim.

**Blockers / open questions:** S-1 Medium is open for stage-8 remediation; stage 7 exit criterion is met because no Critical/High finding is open.

**State of the branch:** tests green; report and progress are uncommitted.

## 2026-09-25 08:45 Asia/Yangon — agent: codex — T-037

**Stage:** 6 (REVIEW) — complete.
**Commit:** pending stage-6 commit.
**Done this session:**
- Reviewed implementation SHA `910724e` against the approved spec, plan revision 2, ADRs, `docs/20` and `docs/21`.
- Checked F-001 StationCode/BilingualName behavior, validation order, named unique-violation mapping, concurrency/update shape, audit snapshots and atomicity, API DTO boundaries, migrations/Down methods and test quality.
- Added Stage 6 report with verdict READY and low finding C-1 (unreachable route-station constraint catch lacks a focused handler test; retain as defense in depth and add a test seam or document it).

**Evidence:** `dotnet test YCR.sln` — 789 passed, 0 failed, 0 skipped. `dotnet ef migrations has-pending-model-changes --project src/YCR.Infrastructure --startup-project src/YCR.Infrastructure` — no pending changes.

**Next step (exact):** commit and push the Stage 6 report, then claim T-038 from the coordination checkout.

**Blockers / open questions:** no blocking findings.

**State of the branch:** tests green; report and progress are uncommitted.

## 2026-09-25 09:20 Asia/Yangon — agent: claude — T-039

**Stage:** 8 (remediation and documentation) — complete; waiting on re-review (T-040, codex).
**Commit:** fix at `fa3642c`; this entry and the `review-codex.md` Status lines are the commit after it. The CI run for the branch head is recorded in `TASKS.md` T-039 Notes.

**Done this session:**
- Claimed T-039 (`claim/T-039`, ledger commit on `main`).
- **S-1 (REQUIRED CONTROL, hein, 2026-09-25):** `POST /api/v1/routes` carries `RequestSizeLimitAttribute(32 KB)` as endpoint metadata (`RouteEndpoints.CreateRouteMaxRequestBodyBytes`). Endpoint routing copies `IRequestSizeLimitMetadata` into the server's `IHttpMaxRequestBodySizeFeature` before the body is read, so Kestrel refuses a larger body before JSON binding. No other endpoint's limit changed (T-042). **Why tests run on Kestrel:** `TestServer` has no `IHttpMaxRequestBodySizeFeature` (checked in `Microsoft.AspNetCore.TestHost` 10.0.12), so the metadata is inert there; `RouteRequestLimitTests` starts the API with `WebApplicationFactory.UseKestrel(0)` and talks to it over real HTTP.
- New `tests/YCR.Api.Tests/Network/RouteRequestLimitTests.cs` (7 tests): `Limit_Is32KiB`; `Post_WithBodyOver32KiB_Returns413WithoutInternals` (declared length and chunked); `Post_WithMalformedJson_Returns400WithoutInternals` (3 bodies); `Post_WithLargestValidRequest_IsUnderTheLimitAndReachesTheHandler` (200 ids, 10-character code, two 100-character names → `422 Network.RouteStationNotFound`). Each refusal is pinned to `application/problem+json` with `status` and `traceId`, under 1 KB, with no exception type, stack frame, framework/parser detail, server path or database name, and nothing written. **Red first:** with the metadata removed, both oversize cases returned `400` (body parsed whole, then refused by validation); with it, `413`.
- **C-1:** kept the `UX_RouteStations_StationId_RouteId` catch in `CreateRouteHandler`; the comment now says it is defence in depth, unreachable while `Route.Create` refuses repeats, and that the translation is covered by `UniqueConstraintTranslationTests` (index name pinned by `RouteModelTests`). No test seam.
- **Spec Amendment 3** (§0.9): V7 (blank/whitespace code or name at the endpoint is `400 Common.ValidationFailed`; `InvalidRouteCode`/`InvalidRouteName` from the domain for format and length) and **R27** (32 KB, `POST /routes`). S5, S11 and §6 updated. The S5 API theory gains two rows, whitespace-only `code` and `nameMy` (only `nameEn` was covered), so the amended S11 is traced end to end.
- **Docs:** `docs/07` "F-003 route tables, constraints, indexes and grants" (columns, objects, both `NO ACTION` FKs, grants and absences, why there is no `IX_RouteStations_StationId`, the three migration names); `docs/08` "Implemented in F-003 — routes" (endpoints, contracts, error codes and their sources, error precedence, the 32 KB limit, not-provided operations) and Initial resources (`PATCH /routes` not provided); glossary rows *Route code*, *IsClosed*, *RouteStation / position* (Myanmar term OPEN QUESTION). README lists no endpoints: unchanged. `docs/10` and `docs/19` not edited (instructed); the plan's "record the migration name in `docs/10`" is therefore done in `docs/07` instead.
- `review-codex.md`: `**Status:**` lines added under S-1 and C-1 (the findings are headings, so there was no Status column to fill); Codex's text unchanged.

**Evidence:** `dotnet test YCR.sln` on the `fa3642c` code — **798 passed, 0 failed, 0 skipped** (789 + 7 limit tests + 2 S5 rows). Targeted `RouteRequestLimitTests` 7/7, and 5/7 with the limit removed (the two oversize cases fail as expected).

**Next step (exact):** codex — T-040 re-review of `fa3642c` (S-1, C-1, Amendment 3, docs against behaviour).

**Blockers / open questions:** none blocking. For T-042/hein: the framework's `400` (malformed JSON) and `413` ProblemDetails carry `traceId` but no `errorCode`, on every endpoint (the plan-V5 behaviour); `docs/08` states this.

**State of the branch:** builds; tests green; everything committed and pushed.

# Progress: F-001 Walking skeleton

Append-only. Newest entry at the bottom. Never edit or delete earlier entries.

---

## 2026-09-20 02:35 Asia/Yangon — claude — T-002

**Stage:** 1–2 (DISCOVER + SPECIFY, `docs/workflows/02-feature-development.md`)
**Commit:** see the `docs(F-001): T-002 discovery + spec` commit on `feature/F-001` (SHA recorded in the ledger row Notes)

**Done this session:**
- Claimed T-002: created ref `claim/T-002` from the trunk in the coordination checkout, then committed the row update (`chore(tasks): claim T-002`, `84962d9`).
- Read AGENTS.md, TASKS.md §Protocol, `docs/workflows/02-feature-development.md`, `docs/prompts/discovery-agent.md`, `docs/00`–`docs/02`, `docs/05`–`docs/10`, `docs/15`–`docs/21`, all Accepted ADRs (0004, 0005, 0006, 0012, 0016, 0017, 0018, 0019) and `docs/reviews/2026-09-19-starter-kit-review.md` + `docs/reviews/2026-09-19-claude-review-of-codex-apply.md`.
- Wrote `docs/features/F-001-walking-skeleton/spec.md`: discovery notes (§0) with five repository facts D1–D5 and six documentation contradictions C1–C6, goal, actors, twelve labelled business rules R1–R12, 23 scenarios S1–S23, state-change table, API table, data/DDL section, audit-logging-metrics section, out-of-scope, a Blocked-behaviour section per `docs/21`, and eight engineering decisions E1–E8 with a recommendation for each.
- Registered three new business OPEN QUESTIONs in `docs/19-open-questions.md`: **OQ26** station code format, **OQ27** station name rules, **OQ28** who may manage and read stations.

**Evidence:** no code was written and no build exists yet, so no build or test command was available to run (`src/` does not exist — spec §0.2 D2; AGENTS.md §Commands are not yet valid). Files changed: `docs/features/F-001-walking-skeleton/spec.md` (new), `docs/features/F-001-walking-skeleton/progress.md` (new), `docs/19-open-questions.md` (three questions appended). Discovery evidence is cited inline in spec §0 by file and section.

**Next step (exact):** a human must, at the stage-2 ⛔ stop:
1. Answer **OQ26**, **OQ27**, **OQ28** in `docs/19-open-questions.md` (Myanma Railways decisions), or explicitly reduce F-001's scope so the station slice no longer depends on them.
2. Decide **E1–E8** in spec §"Engineering decisions required before stage 3"; **E1** (authentication scope), **E7** (two database credentials) and **E8** (`audit.AuditEvents` column shape) need ADRs because they change the project and migration layout.
3. Rule on **C6** — whether F-001 may close the `docs/21` glossary item while T-010 is undecided.
4. Then set T-002 to `done`, and T-003 (PLAN) may be claimed. **T-003 must not start before steps 1 and 2**, because E1, E7 and E8 determine the project and migration inventory a plan would enumerate.

**Blockers / open questions:** blocked at the ⛔ approval stop. Business: OQ26, OQ27, OQ28. Engineering: E1–E8 in the spec. Process: spec §0.2 D4 — `TASKS.md` refers to branch `main`, but the repository trunk is `master`; the `claim/T-002` ref was created from `master`. A human should correct the protocol text or rename the branch.

**State of the branch:** documentation only, no `src/`, nothing to build or test. All work committed on `feature/F-001`.

---

## 2026-09-20 03:05 Asia/Yangon — claude — T-002

**Stage:** 2 (SPECIFY) — approval decisions applied, stage complete
**Commit:** see the `docs(F-001): T-002 approved` commit on `feature/F-001` (SHA in the ledger row Notes)

**Done this session:**
- Applied hein's 2026-09-20 approval decisions and set the spec Status to **Approved (hein, 2026-09-20)**.
- Station rules are now provisional skeleton-only ASSUMPTIONs (spec R3, R4, R8), each labelled `ASSUMPTION (provisional, approved by hein 2026-09-20; replace when OQ26/OQ27/OQ28 answered)`: code 2-10 chars `A-Z`/`0-9`, unique across all stations including inactive, never reused; `NameEn` and `NameMy` both required, 1-100 chars after trim, not unique; permissions `stations.manage` and `stations.read` with no role grants seeded.
- Spec §Blocked behaviour rewritten as an explicit five-term waiver of the `docs/21` open-question rule, scoped to the two value objects and the permission constants, stating that F-001 station rules must not reach production until replaced, and that the waiver is not a precedent.
- Scenarios renumbered S1-S26 (was S1-S23). New: **S6** code reuse after deactivation rejected, **S21** the test auth handler is fenced to the `Testing` environment and startup throws elsewhere, **S22** the application login has no DDL rights and no startup migration. Every `[B]` blocked marker removed. **S9** now enumerates the nine validation cases.
- `docs/10-authorization-matrix.md`: added `stations.read` to the inventory, and recorded that the role table is a proposal and the inventory governs (resolves C1 and C2).
- `docs/19-open-questions.md`: OQ26, OQ27 and OQ28 remain **open with Myanma Railways**, annotated with the provisional values, the waiver reference and the T-014 replacement task. OQ28 notes the `stations.read` part is resolved.
- New ADRs, both **Proposed**, added to `docs/decisions/README.md`: **ADR-0020** F-001 authentication scope and the test authentication handler (E1, including the `Testing`-only fence, startup throw and proving test); **ADR-0021** audit event record shape (E8, including `ClientIp`, nullable `ReasonCode`, `PayloadVersion`).
- Engineering decisions E1-E8 recorded as accepted in the spec, with E3 = GitHub Actions (confirmed by hein after the written approval left `<FILL IN>`), E7 extended with no-startup-migrations and no DDL rights for the application login, and Station having no `rowversion` accepted.
- Contradictions C1, C2, C5 and C6 and repository facts D4 and D5 updated with their resolutions: the trunk is being renamed `master` -> `main`, and T-010 decided yes so the glossary is written in parallel.
- Added **T-014** to `TASKS.md` (status `blocked`, blocker type `business`, owner `—`) to replace the provisional station rules once OQ26-OQ28 are answered. It is a release gate, not an F-001 blocker.

**Evidence:** documentation only; `src/` still does not exist, so no build or test command is available to run. Files changed: `docs/features/F-001-walking-skeleton/spec.md`, `docs/features/F-001-walking-skeleton/progress.md`, `docs/19-open-questions.md`, `docs/10-authorization-matrix.md`, `docs/decisions/ADR-0020-f001-authentication-scope-and-test-auth-handler.md` (new), `docs/decisions/ADR-0021-audit-event-record-shape.md` (new), `docs/decisions/README.md`, and `TASKS.md` in the coordination checkout.

**Next step (exact):** T-002 is done and the `claim/T-002` ref is deleted. **T-003 (PLAN) must not be started until hein says so** — this is an explicit instruction, not a dependency. When it is released, the stage-3 owner reads this spec plus ADR-0020 and ADR-0021, and writes `plan.md` from `docs/templates/plan.md`.

**Blockers / open questions:** none blocking F-001. Still open: OQ26, OQ27, OQ28 with Myanma Railways, tracked for replacement by T-014 as a release gate. ADR-0020 and ADR-0021 are Proposed and await hein's acceptance; the plan should cite them as Proposed until then.

**State of the branch:** documentation only, no `src/`, nothing to build or test. All work committed on `feature/F-001`.

---

## 2026-09-20 03:40 Asia/Yangon — claude — T-002 follow-up

**Stage:** 2 (SPECIFY) — post-approval refinements; T-002 was already `done`
**Commit:** the `docs(F-001): accept ADR-0020/0021` commit on `feature/F-001` (SHA in the T-002 ledger Notes)

**Done this session:**
- **ADR-0020, now Accepted (hein, 2026-09-20).** The test authentication handler class lives only in `tests/YCR.Api.Tests` and is registered only through `WebApplicationFactory.ConfigureTestServices`; no authentication handler of any kind exists in `src/`. New decision item 4 makes the architecture test — no `AuthenticationHandler<>` subtype anywhere in `src/` — the **primary** control, with the startup environment guard retained as **defence in depth** (decision item 5), reformulated as an allowlist check of registered scheme handler types because the test handler's type is no longer visible to `src/`.
- **Spec S21 split** into S21a (architecture test, primary) and S21b (startup guard, defence in depth). S16 gains case (f) for the same architecture rule.
- **ADR-0021, now Accepted (hein, 2026-09-20).** `SubjectId` nullable; `ActorRole` holds a JSON array of roles held at event time; `AuthorizedByPermission nvarchar(100) null` added; `ISJSON` check constraints on `BeforeJson`, `AfterJson` and `ActorRole`; nonclustered indexes on `(SubjectType, SubjectId)` and `OccurredAtUtc`, all created in the same migration as the table.
- **Spec §7** now carries the full `audit.AuditEvents` column table plus its constraints and indexes, naming ADR-0021 as authoritative.
- **ADR-0001 and ADR-0002 set to Accepted (hein, 2026-09-20)** per T-011; `docs/decisions/README.md` updated for all four. No ADR is Proposed any more.

**Judgment calls made, both flagged in the documents:**
- `ActorRole` widened from `nvarchar(100)` to `nvarchar(1000)`. A JSON array of several role names does not reliably fit 100 characters, and ADR-0017 §6's additive-only rule makes widening a ledger column later expensive. Recorded as ADR-0021 decision item 6.
- ADR-0020 §Consequences records that the blanket "no `AuthenticationHandler<>` in `src/`" rule holds only while YCR writes no handler of its own. ADR-0016 uses framework-provided handler types, so it holds today; if the follow-up authentication feature needs a custom handler, the rule needs an allowlist formulation through a superseding ADR, not an edit to the test.

**Evidence:** documentation only; `src/` still does not exist, so no build or test command is available to run. Files changed: `docs/features/F-001-walking-skeleton/spec.md`, `docs/features/F-001-walking-skeleton/progress.md`, `docs/decisions/ADR-0020-*.md`, `docs/decisions/ADR-0021-*.md`, `docs/decisions/ADR-0001-modular-monolith.md`, `docs/decisions/ADR-0002-versioned-fare-rules.md`, `docs/decisions/README.md`. Ledger changes (T-010 done, T-011 done, T-015 added) are committed separately on `main` in the coordination checkout.

**Next step (exact):** **T-003 (PLAN) must not be started until hein says so** — an explicit instruction, not a dependency. When released, the stage-3 owner reads this spec plus ADR-0020 and ADR-0021 (both now binding) and writes `plan.md` from `docs/templates/plan.md`.

**Blockers / open questions:** none blocking F-001. Still open with Myanma Railways: OQ26, OQ27, OQ28, tracked for replacement by T-014 as a release gate.

**State of the branch:** documentation only, no `src/`, nothing to build or test. All work committed on `feature/F-001`.

---

## 2026-09-20 03:15 Asia/Yangon — claude — T-003

**Stage:** 3 (PLAN, `docs/workflows/02-feature-development.md`)
**Commit:** the `docs(F-001): T-003 implementation plan` commit on `feature/F-001` (SHA in the T-003 ledger Notes)

**Done this session:**
- Claimed T-003 (`claim/T-003` ref, ledger commit `3ab1109` on `main`).
- Wrote `docs/features/F-001-walking-skeleton/plan.md` from `docs/templates/plan.md`, against the Approved spec and ADR-0004/0005/0006/0012/0016/0017/0018/0019/0020/0021.
- File inventory: 12 projects (`docs/06` five `src/` + six `tests/`, plus `YCR.DbMigrator` and `YCR.TestSupport` pending P1/P2), ~60 files, each with a reason and a citation.
- Three migrations: `20260920_Network_CreateStations`, `20260920_Audit_CreateAuditEventsLedger` (guard first, then ledger DDL, then the three ISJSON constraints and two indexes), `20260920_Security_AppDatabaseRole`.
- Endpoint inventory, security impact mapped to `docs/18` threats, and a test plan naming every test per project with its spec scenario, so stage 5 has a checklist rather than prose.
- Nine risks with mitigations, rollback/forward-compatibility section, and 13 implementation steps each ending green.
- **Ten decisions P1-P10** collected for the ⛔ stop, each with a recommendation.

**Evidence:** documentation only; `src/` still does not exist, so no build or test command was available to run. Files changed: `docs/features/F-001-walking-skeleton/plan.md` (new), `docs/features/F-001-walking-skeleton/progress.md`.

**Design judgments worth a reviewer's attention:**
- `Down()` on the audit ledger migration **throws** rather than dropping the table (P7), because ADR-0017 item 6 forbids a migration that drops a ledger table and rollback would destroy tamper-evident history.
- Architecture tests run each rule **twice** — over `src/` asserting zero violations, and over a `Violations` namespace inside the test assembly asserting the planted violation is caught. Without the second half a rule matching nothing would pass silently, and `docs/21` §Code explicitly requires negative cases.
- The `ycr_app` database **role** is created by a migration; the **login and user** by environment provisioning, so no credential enters a migration file (P9).
- Risk R-2 names a concrete fallback if EF cannot insert into the ledger table: parameterised INSERT on the same connection and transaction, preserving ADR-0017's single-transaction guarantee.

**Next step (exact):** a human must answer **P1-P10** in `plan.md` §Decisions needed and approve the plan at the ⛔ stop. P1, P2, P5 and P6 change the file inventory, so **T-004 must not start before they are answered**. When approved, set T-003 to `done`; T-004 is then claimable by any agent, and its owner must not be the owner of T-005, T-006 or T-007 (`TASKS.md` §Protocol item 11).

**Blockers / open questions:** blocked at the ⛔ approval stop, blocker type `approval`. P1-P10 pending. Still open with Myanma Railways: OQ26, OQ27, OQ28, tracked by T-014 as a release gate. Known and accepted: the repository has no git remote, so the CI workflow in step 13 cannot be proven green (plan risk R-6, spec E3).

**State of the branch:** documentation only, no `src/`, nothing to build or test. All work committed on `feature/F-001`.

---

## 2026-09-20 03:35 Asia/Yangon — claude — T-003

**Stage:** 3 (PLAN) — plan revised after review, plus the remote change
**Commit:** the `docs(F-001): plan revision 2+3` commit on `feature/F-001` (SHA in the T-003 ledger Notes)

**Done this session — nine review items applied (plan revision 2):**
1. Test fixture: migrations run under the migrator credential; `YCR.Application.Tests` and `YCR.Api.Tests` connect as `ycr_app`, so every application-path test exercises least privilege. New plan §Test fixture gives the provisioning order and a per-project credential table. **One documented exception:** `SequentialGuidFragmentationTests` stays on the migrator credential because it reads `sys.dm_db_index_physical_stats`, which needs `VIEW DATABASE STATE`; granting that to `ycr_app` would weaken the control E7 exists to enforce.
2. Migration identifiers changed to EF's `yyyyMMddHHmmss_<Module>_<Change>`. `docs/20` §6's date-only `YYYYMMDD_` form cannot be produced by EF and collides on any day with two migrations — flagged as a stage-8 correction.
3. S16d and S16e replaced by one rule: `YCR.Api` must not depend on `YCR.Domain.<Module>`. It subsumes "endpoint returns an EF entity" (an endpoint that cannot reference `Station` cannot return it). Recorded that it only partly covers AGENTS.md rule 3, which stays a stage-6 concern.
4. Architecture tests and their `Violations` fixtures moved to step 6, right after Infrastructure persistence.
5. P1 changed to `dotnet ef migrations bundle` under the migrator credential; `src/YCR.DbMigrator` removed from the inventory (twelve projects now); no ADR-0022 unless **V6** fails.
6. P4 became **V5** (NetArchTest maintenance for .NET 10, `ArchUnitNET` fallback), resolved in step 1. P3 pinned to **xUnit v3** (`xunit.v3`) in `Directory.Packages.props`.
7. S19 test method specified: a digest-pinned **SQL Server 2019** image in a trunk-only category, asserting the failure is our `THROW` naming the version and not an incidental ledger syntax error. Cost and the untestable edition branch both stated.
8. **V3** added: ledger `CREATE TABLE` inside EF's migration transaction, with a `suppressTransaction: true` fallback that keeps the guard first.
9. `DeactivateStationHandler` specified as load → `Station.Deactivate()` → explicit transaction → conditional `UPDATE ... WHERE IsActive = 1` → zero rows affected means a concurrent writer won, mapped to the same 422. Exactly one 204, one 422, one audit event.

**Plan revision 3 (same day, hein):** a git remote `origin` (`https://github.com/heinmindev/ycr.git`) now exists. Risk R-9 struck; step 13 must now prove the CI workflow **green on `origin` for `feature/F-001`** and record the run URL here. `TASKS.md` §Protocol gained two lines: push `main` after each ledger commit, never push `claim/*` refs.

**Two conflicts surfaced by the review, raised rather than silently resolved:**
- **P11.** "`YCR.Api` must not depend on `YCR.Domain`" cannot hold literally: `ResultExtensions` must reference `Result`/`Error`/`ErrorType`, which ADR-0004 §Errors places in `YCR.Domain.Common`. Written as "no dependency on any `YCR.Domain.<Module>` namespace, `YCR.Domain.Common` permitted", with **no** composition-root exception because `Program.cs` needs none — tighter than asked in one respect, looser in another, so it needs an explicit yes.
- **P12.** The new concurrency test has no scenario in the Approved spec (§4 ends at S26), and workflow 02 says a stage finding a spec gap returns to stage 2 rather than patching around it. Proposed one-sentence **S27** for a stage-2 amendment; the test is listed as "S27 (proposed)" until then.

**Evidence:** documentation only; `src/` still does not exist, so no build or test command was available to run. Files changed: `docs/features/F-001-walking-skeleton/plan.md`, `docs/features/F-001-walking-skeleton/progress.md`, and `TASKS.md` in the coordination checkout.

**Next step (exact):** a human answers **P11** and **P12** and approves the plan at the ⛔ stop. P12 also needs a stage-2 amendment to the Approved spec, which stage 3 may not apply itself. Then set T-003 to `done`; T-004 becomes claimable, and its owner must not own T-005, T-006 or T-007 (`TASKS.md` §Protocol item 11).

**Blockers / open questions:** blocked at the ⛔ approval stop, blocker type `approval`. P11 and P12 pending. Still open with Myanma Railways: OQ26, OQ27, OQ28, tracked by T-014. Spec E3's "no remote yet" wording is now stale and is corrected at stage 8.

**State of the branch:** documentation only, no `src/`, nothing to build or test. All work committed on `feature/F-001`. Not yet pushed to `origin`.

---

## 2026-09-20 04:55 Asia/Yangon — claude — T-004

**Stage:** 4 (IMPLEMENT) — **step 1 of 13 done**
**Commit:** `1028cff` on `feature/F-001`

**Done this session:**
- Applied hein's T-003 approval to `spec.md` and `plan.md`, set both to Approved, and marked T-003 `done` (ledger `bbe8dda` / plan revision 4). Claimed T-004.
- **Step 1 complete.** `global.json` (SDK 10.0.302, `rollForward` latestPatch), `Directory.Build.props` (net10.0, nullable, implicit usings, `TreatWarningsAsErrors`, deterministic), `Directory.Packages.props` (central package management, every version pinned), `.gitignore`, `.editorconfig` (UTF-8), `YCR.sln` with all twelve projects wired, and `BuildConfigurationTests`.

**Evidence:**
- `dotnet build YCR.sln` from a cleaned tree: **Build succeeded, 0 Warning(s), 0 Error(s)**. Warnings-as-errors has no runtime trace, so a clean build with it enabled is the evidence, and `BuildConfigurationTests` says so in a comment rather than faking an assertion.
- `dotnet test YCR.sln`: **total 2, failed 0, succeeded 2, skipped 0**, exit code 0. Both in `YCR.Domain.Tests.BuildConfigurationTests` — `RuntimeVersion_UnderPinnedSdk_IsNet10` and `TestAssembly_TargetsNet10`.
- Twelve projects: `YCR.Domain`, `YCR.Application`, `YCR.Infrastructure`, `YCR.Api`, `YCR.Worker`; `YCR.TestSupport`, `YCR.Domain.Tests`, `YCR.Application.Tests`, `YCR.Infrastructure.Tests`, `YCR.Api.Tests`, `YCR.IntegrationTests`, `YCR.ArchitectureTests`.

**V5 resolved (plan P4):** `NetArchTest.Rules` last shipped **1.3.2 on 2021-05-23** — over five years old, no .NET 10 signal — so the fallback applies. Architecture tests will use **`TngTech.ArchUnitNET` 0.13.4** (published 2026-08-20) with `TngTech.ArchUnitNET.xUnitV3`, which pairs with the xUnit v3 choice in P3. Both pinned in `Directory.Packages.props`; the plan's §New packages row records the reasoning.

**Four things the plan did not foresee, and how each was handled:**
1. **`dotnet new sln` defaults to the new `.slnx` format on .NET 10.** AGENTS.md §Commands names `YCR.sln`, so the solution was created with `--format sln`. The constitution's documented commands work as written.
2. **xUnit v3 needs an opt-in to the Microsoft.Testing.Platform runner.** On .NET 10 the VSTest target is gone, and the opt-in lives in `global.json` under `"test": { "runner": "Microsoft.Testing.Platform" }` — not in `dotnet.config`, which was tried first and had no effect.
3. **`Microsoft.NET.Sdk.Worker` does not reference `Microsoft.Extensions.Hosting` implicitly on .NET 10**, so its generated global usings did not compile. Added as an explicit pinned reference and recorded in the plan's §New packages, because `docs/20` §8 requires every package to be stated there.
4. **Five test projects have no tests yet**, and Microsoft.Testing.Platform exits 8 on a run that discovers none. Rather than disabling that signal solution-wide, each carries a scoped `--ignore-exit-code 8` waiver naming the step that must delete it — steps 6, 7, 10 and 11 respectively. `YCR.IntegrationTests` keeps its waiver for all of F-001 with plan C4 as the reason. An empty suite therefore cannot hide once it should have tests.

**Next step (exact):** plan step 2 — `YCR.Domain.Common`: `Result`, `Error`, `ErrorType`, `Entity`, `AggregateRoot`, with `YCR.Domain.Tests` covering Result and Error semantics.

**Blockers / open questions:** none. Still open with Myanma Railways: OQ26, OQ27, OQ28, tracked by T-014 as a release gate.

**State of the branch:** builds clean, tests green, all work committed and pushed to `origin/feature/F-001`.

---

## 2026-09-20 05:10 Asia/Yangon — claude — T-004 — HANDOFF TO CODEX

**Stage:** 4 (IMPLEMENT). Steps 1/13 complete. **Implementation passes to Codex from step 2** (decided by hein, 2026-09-20).
**Commit Codex starts from:** `20e3d56` on `feature/F-001`, which is also `origin/feature/F-001`. This handoff entry is its only child.

### State of the branch

Clean tree, nothing uncommitted, local and `origin` in sync. `dotnet build YCR.sln` → **0 warnings, 0 errors**. `dotnet test YCR.sln` → **2 passed, 0 failed, 0 skipped**, exit 0. Twelve projects exist and are wired; only `YCR.Domain.Tests` has tests.

Toolchain confirmed present on this machine: .NET SDK **10.0.302**, Docker **29.7.2**.

### Exact next step

**Plan step 2 — `YCR.Domain.Common`:** `Result`, `Result<T>`, `Error`, `ErrorType`, `Entity`, `AggregateRoot` in `src/YCR.Domain/Common/`, ending green with `YCR.Domain.Tests` covering Result and Error semantics.

Constraints on step 2 specifically:

- `ErrorType` must carry exactly ADR-0004's cases, because `ResultExtensions` maps them to status codes at step 11: `Validation` 400, `Unauthorized` 401, `Forbidden` 403, `NotFound` 404, `Conflict` 409, `BusinessRule` 422.
- Error codes are the stable strings `<Module>.<Reason>` (`docs/20` §2).
- `AggregateRoot` needs `Raise(...)` and a domain-event collection, because `docs/20` §3's reference slice calls it. **No dispatcher** — plan P6.
- These four types are the ones `YCR.Api` is later allowed to reference; see "must not change" below.

Then steps 3–13 in the plan's §Steps table, in order. Steps 1–6 need no Docker; 7 onward do.

### The four step-1 discoveries Codex needs

1. **`dotnet new sln` defaults to `.slnx` on .NET 10.** `YCR.sln` was created with `--format sln` because AGENTS.md §Commands names `YCR.sln`. **Do not regenerate the solution without `--format sln`** — a `.slnx` would silently break both documented commands.

2. **xUnit v3 needs the Microsoft.Testing.Platform opt-in, and it lives in `global.json`**, not `dotnet.config`. The key is `test.runner` set to `Microsoft.Testing.Platform`. On .NET 10 the VSTest target is gone; without this, every test project fails with "Testing with VSTest target is no longer supported". `dotnet.config` was tried first and had no effect.

3. **`Microsoft.NET.Sdk.Worker` does not reference `Microsoft.Extensions.Hosting` implicitly on .NET 10.** Without the explicit pin, `YCR.Worker`'s generated global usings do not compile. It is pinned at 10.0.12 and recorded in the plan's §New packages, because `docs/20` §8 requires that for every package.

4. **Exit-8 waivers.** Microsoft.Testing.Platform exits 8 when a run discovers no tests. Rather than disabling that signal solution-wide, five projects carry a scoped `--ignore-exit-code 8` in their own csproj. **Delete each one in the step that gives that project its first test:**

   | Project | Waiver removed at |
   |---|---|
   | `YCR.ArchitectureTests` | **step 6** |
   | `YCR.Infrastructure.Tests` | **step 7** |
   | `YCR.Application.Tests` | **step 10** |
   | `YCR.Api.Tests` | **step 11** |
   | `YCR.IntegrationTests` | **not in F-001** — intentionally empty for the whole feature (plan C4). The waiver stays, with that reason in the csproj comment |

   Leaving a waiver in place after its step would let a suite silently stop running. Removing them early turns the build red for no reason.

### What Codex must not change

**Binding, supersede rather than edit:**

- Accepted ADRs 0004, 0005, 0006, 0012, 0016, 0017, 0018, 0019, **0020**, **0021**. `docs/decisions/README.md` §Rules: never edit the Decision section of an Accepted ADR; propose a superseding one.
- The **Approved** `spec.md` and `plan.md`. A spec gap goes **back to stage 2** for a human amendment (workflow 02), as happened for S27 — it does not get patched in code.
- Spec scenario numbering **S1–S27**. The plan's test plan references these by number; renumbering silently breaks that mapping.

**Plan decisions already made — changing one needs hein, not a judgement call:**

- **P1** migrations are applied by `dotnet ef migrations bundle` under the migrator credential. **No `YCR.DbMigrator` project** unless **V6** fails, and then only with the ADR that would need.
- **P3** xUnit v3, plain xUnit assertions. **No FluentAssertions** — its v8 licence change is the concern that made ADR-0004 reject MediatR.
- **P5** keep both `StationDto` (Application) and `StationResponse` (Api).
- **P6** domain events are collected, never dispatched, in F-001.
- **P7** the audit ledger migration's `Down()` **throws**. Do not make it drop the table; ADR-0017 item 6 forbids it.
- **P9** the `ycr_app` **role** is created by a migration; its **login and user** by provisioning. No credential in any migration file.
- **P11** `YCR.Api` may reference **no** `YCR.Domain.<Module>` type, and from `YCR.Domain.Common` exactly four: `Result`, `Result<T>`, `Error`, `ErrorType`. The step-6 architecture test enforces the **allowlist by type**, not the namespace. Adding a fifth shared type is meant to fail the build.

**The provisional-rules waiver (spec §Blocked behaviour), the easiest thing to break by accident:**

- Rules R3 (station code), R4 (names) and R8 (permissions) are **ASSUMPTION (provisional, approved by hein 2026-09-20; replace when OQ26/OQ27/OQ28 answered)**. They live **only** in `StationCode`, `BilingualName` and `Permissions`, and that label belongs in an XML doc comment on each. Nothing else may encode or branch on them.
- **Seed no role-to-permission grants** (OQ28). Tests mint permissions directly on the test principal.
- OQ26, OQ27 and OQ28 stay open in `docs/19`; T-014 is the release gate.

**Verifications that must not be quietly worked around:**

- **V1** (ledger supported by the image's edition) and **V2** (`CHECK` constraints and nonclustered indexes on an append-only ledger table) run at step 8. If either fails, **stop and return to stage 2** — do not substitute a normal table. AGENTS.md rule 8.
- **V3** (ledger DDL inside EF's migration transaction) and **V4** (EF `INSERT` into the ledger) have fallbacks already written in the plan; use those rather than inventing one.
- **V6** (`dotnet ef migrations bundle` handles the raw-SQL ledger migration) runs at step 7.

**Build and repository hygiene:**

- Do not remove the SDK pin or the MTP opt-in from `global.json`.
- Do not remove `TreatWarningsAsErrors`, and do not silence a warning with a pragma or a suppression to get a green build (`docs/20` §8).
- Keep central package management: `PackageReference` with no `Version`, the version pinned in `Directory.Packages.props`. **Every new package needs a row in the plan's §New packages with its reason** (`docs/20` §8).
- `tests/Directory.Build.props` excludes `YCR.TestSupport` by name from the test-runner setup; it is a fixture library, not a suite.
- **Never disable or delete a test to make the build pass** (AGENTS.md rule 7).

**Process:**

- `TASKS.md` inside this worktree is **stale**. Read and edit the ledger only in the coordination checkout (`D:\MR\yangon-circular-railway`).
- Push `main` after every ledger commit; **never push a `claim/*` ref** (`TASKS.md` §Protocol, §Checkouts and branches).
- Push `feature/F-001` after every checkpoint (hein, 2026-09-20).
- Log every finished step here with test counts and the commit SHA.
- **Cross-review (`TASKS.md` §Protocol item 11):** T-005 scenario tests, T-006 code review and T-007 security review must be done by an agent **other than the T-004 implementer**. With implementation moving to Codex, Codex must not take T-005, T-006 or T-007; it does own T-008 remediation.
- Step 13 must prove the CI workflow **green on `origin` for `feature/F-001`** and record the run URL here.

### Ledger note

`TASKS.md` was deliberately not touched in this handoff. The T-004 row still reads Owner `claude`, Status `doing`, and the `claim/T-004` ref still exists in the coordination checkout. Per §Protocol item 12 only a human may release a claimed task, so **hein needs to reassign the row and the claim ref to Codex** before Codex starts step 2.

**Blockers / open questions:** none technical. Still open with Myanma Railways: OQ26, OQ27, OQ28 (T-014, release gate).

---

## 2026-09-20 12:08 Asia/Yangon — codex — T-004

**Stage:** 4 (IMPLEMENT). Step 2 of 13 complete.
**Commit:** `80b741951f887bf9f4830d2611ab4e388c03eddf` (`feat(F-001): implement domain common primitives`), pushed to `origin/feature/F-001`.

**Done this session:**
- Added `YCR.Domain.Common` primitives: `Result`, `Result<T>`, `Error`, `ErrorType`, `Entity`, `AggregateRoot`, and `IDomainEvent`.
- Added domain tests for success/failure semantics, all ADR-0004 error categories and value equality, implicit conversions used by the reference slice, and collected-but-undispatched aggregate events.
- No new package was required; `plan.md` §New packages is unchanged.

**Evidence:** `dotnet build D:\MR\ycr-F-001\YCR.sln` succeeded with 0 warnings and 0 errors. `dotnet test D:\MR\ycr-F-001\YCR.sln` passed with total 11, failed 0, succeeded 11, skipped 0. Files changed: `src/YCR.Domain/Common/*.cs` and `tests/YCR.Domain.Tests/Common/*.cs`.

**Next step (exact):** plan step 3 — implement `YCR.Domain.Network`: `StationCode`, `BilingualName`, `Station`, `NetworkErrors`, and `StationDeactivated`, with the listed `YCR.Domain.Tests` cases for S1, S2, S7 and S9.

**Blockers / open questions:** none technical. OQ26, OQ27 and OQ28 remain open with Myanma Railways and are confined by the approved provisional-rules waiver.

**State of the branch:** build and tests green; committed and pushed; no uncommitted work.

---

## 2026-09-20 12:16 Asia/Yangon — codex — T-004

**Stage:** 4 (IMPLEMENT). Step 3 of 13 complete.
**Commit:** `21d236b6f8f64df66265e8c80d2e83e65cbcbe85` (`feat(F-001): implement station domain slice`), pushed to `origin/feature/F-001`.

**Done this session:**
- Added `StationCode`, `BilingualName`, `Station`, `NetworkErrors`, and `StationDeactivated` in `YCR.Domain.Network`.
- Added all planned domain tests for S1, S2, S7 and S9, including trimming, length, character-set, required-name, active-state, and event-collection behavior.
- Kept the approved provisional R3/R4 rules confined to the two value objects and labeled them in XML documentation.

**Evidence:** `dotnet build D:\MR\ycr-F-001\YCR.sln` succeeded with 0 warnings and 0 errors. `dotnet test D:\MR\ycr-F-001\YCR.sln` passed with total 30, failed 0, succeeded 30, skipped 0. Files changed: `src/YCR.Domain/Network/*.cs` and `tests/YCR.Domain.Tests/Network/*.cs`.

**Next step (exact):** plan step 4 — add Application abstractions: `IIdGenerator`, `IAuditWriter`, `ICurrentUser`, `Permissions`, `PagedResult`, and `INetworkDbContext`; compile with no behavior yet.

**Blockers / open questions:** none technical. OQ26, OQ27 and OQ28 remain open with Myanma Railways and are confined by the approved provisional-rules waiver.

**State of the branch:** build and tests green; committed and pushed; no uncommitted work.

---

## 2026-09-20 12:21 Asia/Yangon — codex — T-004

**Stage:** 4 (IMPLEMENT). Step 4 blocked before code changes.
**Commit:** no implementation commit; the last completed implementation checkpoint is `21d236b6f8f64df66265e8c80d2e83e65cbcbe85`.

**Done this session:**
- Began plan step 4 by resolving the approved abstraction signatures against ADR-0004 and ADR-0012.
- Found a plan/package mismatch and made no step-4 code change or workaround.

**Evidence:** ADR-0012 §Decision item 2 requires module context interfaces to expose their module `DbSet`s, and `plan.md` step 4 requires `INetworkDbContext`. `src/YCR.Application/YCR.Application.csproj` references only `YCR.Domain`; `Directory.Packages.props` has no `Microsoft.EntityFrameworkCore` pin; and `plan.md` §New packages lists the SQL Server and Design packages only for Infrastructure. The step-5 `Microsoft.EntityFrameworkCore.SqlServer` dependency cannot flow backward from Infrastructure to the Application project that Infrastructure references.

**Next step (exact):** tech lead decides whether to add a centrally pinned `Microsoft.EntityFrameworkCore` package reference to `YCR.Application` and records it in `plan.md` §New packages, or amends the approved `INetworkDbContext` shape. Then resume plan step 4 from `IIdGenerator`, `IAuditWriter`, `ICurrentUser`, `Permissions`, `PagedResult`, and `INetworkDbContext`.

**Blockers / open questions:** ENGINEERING BLOCKER — the approved plan is missing the direct EF Core package required for `INetworkDbContext` in the Application project. No business OQ is added. OQ26, OQ27 and OQ28 remain unchanged.

**State of the branch:** last completed checkpoint builds and tests green; this progress entry is the only uncommitted change.

---

## 2026-09-20 12:45 Asia/Yangon — hein ruling recorded by codex — T-004

**Stage:** 4 (IMPLEMENT). Step 4 unblocked by tech-lead ruling.
**Commit:** pending checkpoint commit; ruling and plan correction are included in the next feature push.

**Ruling applied:**
- Added provider-neutral `Microsoft.EntityFrameworkCore` 10.0.12 to `YCR.Application` and recorded it in `plan.md` §New packages with ADR-0004 §Request flow as the source.
- Recorded the step-6 architecture control and `Violations` fixture requirement forbidding `Microsoft.EntityFrameworkCore.SqlServer` and `Microsoft.Data.SqlClient` dependencies from Application.
- Recorded the step-9 `UniqueConstraintViolationException` boundary and SQL Server 2601/2627 translation rule. `CreateStationHandler` will map only the named `UX_Stations_Code` constraint; other violations remain unexpected.
- T-004 is returned to `doing` in the coordination ledger; no other ledger row was changed.

**Next step (exact):** continue plan step 4 by implementing `IIdGenerator`, `IAuditWriter`, `ICurrentUser`, `Permissions`, `PagedResult`, and `INetworkDbContext` using the newly approved provider-neutral EF reference.

**Blockers / open questions:** none technical after the ruling. OQ26, OQ27 and OQ28 remain open with Myanma Railways and are confined by the approved provisional-rules waiver.

---

## 2026-09-20 12:56 Asia/Yangon — codex — T-004

**Stage:** 4 (IMPLEMENT). Step 4 of 13 complete.
**Commit:** `704d872fe310b5b2a90f02af6b691a3201dc4b1f` (`feat(F-001): add application contracts`), pushed to `origin/feature/F-001`.

**Done this session:**
- Added `IIdGenerator`, `IAuditWriter`, `ICurrentUser`, `Permissions`, `PagedResult<T>`, and the EF-backed `INetworkDbContext` abstraction.
- Applied the ruling's provider-neutral `Microsoft.EntityFrameworkCore` 10.0.12 reference to `YCR.Application`; the package and rationale are recorded in `plan.md` §New packages.
- Recorded the step-6 provider-boundary architecture rule and step-9 unique-constraint translation design in the approved plan.

**Evidence:** `dotnet build D:\MR\ycr-F-001\YCR.sln` succeeded with 0 warnings and 0 errors. `dotnet test D:\MR\ycr-F-001\YCR.sln` passed with total 30, failed 0, succeeded 30, skipped 0. Files changed: `src/YCR.Application/Common/*.cs`, `src/YCR.Application/Network/INetworkDbContext.cs`, `Directory.Packages.props`, `src/YCR.Application/YCR.Application.csproj`, and the feature plan/progress records.

**Next step (exact):** plan step 5 — implement `YcrDbContext`, `StationConfiguration`, `SqlServerSequentialGuidIdGenerator`, Infrastructure DI registration, and the `Network_CreateStations` migration. The SQL-backed scope test remains scheduled for step 7.

**Blockers / open questions:** none technical. OQ26, OQ27 and OQ28 remain open with Myanma Railways and are confined by the approved provisional-rules waiver.

**State of the branch:** build and tests green; implementation committed and pushed; this progress entry is the only uncommitted change.

---

## 2026-09-20 18:59 Asia/Yangon — codex — T-004

**Stage:** 4 (IMPLEMENT). Step-5 stop review remediated before step 7.
**Commit:** `9a09b36a18394e81f162bca0637d280d3c575a1b` (`test(F-001): harden architecture rules`), pushed to `origin/feature/F-001`.

**Claude review and fixes:**
- Replaced pair-specific application/domain boundary checks with module-parameterized rules over the nine `docs/05` modules, including the allowed application dependency shape and per-module negative fixtures.
- Added domain/application layer rules, the Reporting read-context-only rule (including the `INetworkDbContext` fixture), and the assembly-level SQL Server provider reference check.
- Added the S17 scoped-context identity test; it proves the module interface and concrete context are the same within one scope and differ across scopes.
- Replaced the direct-base authentication check with an `IAuthenticationHandler` assignability scan across all source assemblies and removed the tautological ArchUnit rule.
- Made `StationCode.From` internal and granted `YCR.Infrastructure` friend access through `InternalsVisibleTo`.

**Evidence:** `dotnet build D:\MR\ycr-F-001\YCR.sln` succeeded with 0 warnings and 0 errors. `dotnet test D:\MR\ycr-F-001\YCR.sln --no-build` passed with total 43, failed 0, succeeded 43, skipped 0. Focused architecture tests passed 9; Infrastructure tests passed 3. Files changed: architecture rules/tests and fixtures, `StationCode` visibility, Domain assembly friend declaration, and the Infrastructure module-scope test.

**Next step (exact):** plan step 7 — add the digest-pinned Testcontainers SQL Server fixture, `docker-compose.yml`, `init-principals.sql`, migration bundle, and run the migrate-and-query smoke test plus S17 under the real container.

**Blockers / open questions:** none technical. OQ26, OQ27 and OQ28 remain open with Myanma Railways and are confined by the approved provisional-rules waiver.

**State of the branch:** builds and tests green; review commit pushed; this progress entry is the only uncommitted change.

---

## 2026-09-20 19:03 Asia/Yangon — codex — T-004

**Stage:** 4 (IMPLEMENT). Step 7 started; blocked before implementation by missing container prerequisites.
**Commit:** pending documentation checkpoint.

**Blocker:** Step 7 requires the approved digest-pinned SQL Server 2022 Testcontainers fixture and V6 migration-bundle verification. This environment has no `docker` executable or Docker service, and `Testcontainers.MsSql` is not available in the local NuGet cache. The plan does not specify an image digest, so using a floating tag or replacing the real container with another database would violate E4, ADR-0017, and the explicit no-workaround instruction.

**Evidence:** `Get-Command docker` and `Get-Service com.docker.service` returned no result; `C:\Users\ASUS ZENBOOK\.nuget\packages` contains no `testcontainers*` package. No step-7 code or package was added.

**Next step (exact):** provide a working Docker runtime and the approved SQL Server 2022 image digest (or make the pinned package/image available), then implement `YCR.TestSupport` and V6 exactly as plan step 7 specifies and remove the Infrastructure exit-code-8 waiver in that checkpoint.

**Blockers / open questions:** ENGINEERING/ENVIRONMENT BLOCKER — Docker/Testcontainers prerequisite unavailable. No business OQ added.

**State of the branch:** prior review commit remains pushed; only this blocker entry is uncommitted.

---

## 2026-09-20 13:18 Asia/Yangon — codex — T-004

**Stage:** 4 (IMPLEMENT). Step 5 of 13 complete.
**Commit:** `5e901f734720382cc33c55d2f85cfbf3f496ac4a` (`feat(F-001): add station persistence`), pushed to `origin/feature/F-001`.

**Done this session:**
- Added `YcrDbContext`, `StationConfiguration`, the named `UX_Stations_Code` unique index, `IsActive` concurrency-token mapping, and `SqlServerSequentialGuidIdGenerator`.
- Added scoped Infrastructure DI registration so `INetworkDbContext` resolves to the same `YcrDbContext` instance, plus the design-time factory required by EF tooling.
- Generated `20260920064342_Network_CreateStations` and its model snapshot through `dotnet-ef` 10.0.12.
- Added two Infrastructure model/generator tests. The Infrastructure exit-code-8 waiver remains until its named step 7, as required by the handoff.

**Evidence:** `dotnet build D:\MR\ycr-F-001\YCR.sln --no-restore` succeeded with 0 warnings and 0 errors. `dotnet test D:\MR\ycr-F-001\YCR.sln` passed with total 32, failed 0, succeeded 32, skipped 0. Files changed: `src/YCR.Infrastructure/**`, `tests/YCR.Infrastructure.Tests/Persistence/StationModelTests.cs`, and the central EF package pins.

**Next step (exact):** plan step 6 — add architecture tests and `Violations` fixtures, including the new control that flags `YCR.Application` dependencies on `Microsoft.EntityFrameworkCore.SqlServer` or `Microsoft.Data.SqlClient`; remove the `YCR.ArchitectureTests` exit-code-8 waiver at this step.

**Blockers / open questions:** none technical. OQ26, OQ27 and OQ28 remain open with Myanma Railways and are confined by the approved provisional-rules waiver.

**State of the branch:** build and tests green; implementation committed and pushed; this progress entry is the only uncommitted change.

---

## 2026-09-20 14:03 Asia/Yangon — codex — T-004

**Stage:** 4 (IMPLEMENT). Step 6 of 13 complete; stop point reached.
**Commit:** `27c9922b947688a81fe62c2a8e8101b532a8f348` (`test(F-001): enforce architecture boundaries`), pushed to `origin/feature/F-001`.

**Done this session:**
- Added ArchUnitNET rules and planted `Violations` fixtures for Network-to-Ticketing context/domain boundaries, Reporting write-context access, API module-domain and Common-type allowlist boundaries, Application provider dependencies, and deployable authentication handlers.
- The provider-boundary rule rejects dependencies on both `Microsoft.EntityFrameworkCore.SqlServer` and `Microsoft.Data.SqlClient`; Application may retain provider-neutral `Microsoft.EntityFrameworkCore` per the tech-lead ruling.
- Removed the `YCR.ArchitectureTests` exit-code-8 waiver at its named step.
- Module-boundary pattern: one `YcrDbContext` is exposed to each Application module through module-scoped `I<Module>DbContext` interfaces; architecture rules scan source assemblies for forbidden cross-module context/domain/provider dependencies and scan the test assembly's `Violations` namespace to prove each rule has teeth.

**Evidence:** `dotnet build D:\MR\ycr-F-001\YCR.sln` succeeded with 0 warnings and 0 errors. `dotnet test D:\MR\ycr-F-001\YCR.sln` passed with total 39, failed 0, succeeded 39, skipped 0. Architecture-focused tests: 7 passed, 0 failed, 0 skipped. Files changed: `tests/YCR.ArchitectureTests/YCR.ArchitectureTests.csproj`, `ArchitectureRuleTests.cs`, `ArchitectureRules.cs`, and `Violations/*.cs`.

**Next step (exact):** stop for the mandated T-004 report. After human acknowledgment, plan step 7 starts with the Testcontainers fixture, pinned SQL Server 2022 image, `docker-compose.yml`, `init-principals.sql`, migration bundle, and V6 verification.

**Blockers / open questions:** none technical. OQ26, OQ27 and OQ28 remain open with Myanma Railways and are confined by the approved provisional-rules waiver.

**State of the branch:** build and tests green; implementation committed and pushed; this progress entry is the only uncommitted change.

---

## 2026-09-20 19:30 Asia/Yangon — claude — T-004

**Stage:** 4 (IMPLEMENT). Step 7 of 13 complete. **Step-7 environment blocker cleared.**
**Commit:** `0375cce` (`feat(F-001): add pinned container fixture and migration bundle`), pushed to `origin/feature/F-001`.
**Implemented by:** claude. Reviews are split by author: steps 1 and 7 are claude's; steps 2–6 and the step-5 review fixes are Codex's.

### Environment blocker resolved

Codex's 19:03 entry blocked step 7 on a missing Docker runtime and an unavailable `Testcontainers.MsSql`. Both are gone:

- `docker version` reports client **29.8.0** and **Docker Desktop 4.91.0 (239619)**, engine 29.8.0 on WSL2. Hein installed it (ruling, 2026-09-20).
- `Testcontainers.MsSql` **4.15.0** restored from nuget.org and pinned centrally; `plan.md` §New packages carries the row with its reason.

### E4 — the image pin (hein's ruling applied)

| | |
|---|---|
| **Tag** | `mcr.microsoft.com/mssql/server:2022-CU27-ubuntu-22.04` |
| **Digest** | `sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090` |
| **Pinned on** | 2026-09-20 |
| **Why this tag** | Newest SQL Server 2022 cumulative update published on `mcr.microsoft.com` as of that date. The tag list ends at CU27; CU26 is the newest with a GDR variant, and CU27 supersedes it |
| **Verified as** | `ProductVersion` = **16.0.4295.3**, `ProductMajorVersion` = **16**, `Edition` = **Developer Edition (64-bit)** |

The digest was read from the MCR manifest and then confirmed against the pulled image's `RepoDigests` — they match, so the pin names the bits that actually ran.

**Single shared constant:** `tests/YCR.TestSupport/SqlServerImage.cs` holds `Tag`, `Digest` and `Reference`. `docker-compose.yml` names the same reference on both services. A YAML file cannot import a C# constant, so `SqlServerImagePinTests.ComposeFile_PinsExactlyTheFixtureImage` reads the compose file and asserts every `image:` line equals `SqlServerImage.Reference`. That test, not convention, is what makes the constant shared — without it the two pins would drift the first time one was bumped alone. No floating tag exists anywhere in the repository.

`MSSQL_PID=Developer` is set explicitly in both compose and the fixture rather than inherited from the image default, so an image change cannot quietly downgrade the edition underneath V1.

### V6 — RESOLVED, PASS

`dotnet ef migrations bundle` builds on EF 10 and produces a working self-contained migrator. Plan decision **P1 stands**: no `YCR.DbMigrator` project and no superseding ADR are needed.

The fixture applies schema **through the bundle**, not through in-process `Migrate()`. That was deliberate: in-process migration would pass while leaving the artifact that actually ships untested, which is the exact failure V6 exists to retire. `Migrate_AgainstPinnedImage_RecordsEveryMigrationAsApplied` compares `__EFMigrationsHistory` against `Database.GetMigrations()`, so a migration the assembly carries but the bundle omits fails the build.

**`dotnet-ef` 10.0.12 is now pinned as a local tool** in `.config/dotnet-tools.json`, and the fixture runs `dotnet tool restore` before building the bundle. Without it the bundle would be built by whatever version happened to be installed globally, and V6 would be a statement about one laptop rather than about the repository. Recorded in `plan.md` §New packages.

### Files added or changed

| File | What |
|---|---|
| `tests/YCR.TestSupport/SqlServerImage.cs` | The E4 constant: tag, digest, combined reference, and how to move it |
| `tests/YCR.TestSupport/SqlServerTestContainer.cs` | One pinned container per collection; a fresh migrated database per test class; per-container random `sa` password with a redactor |
| `tests/YCR.TestSupport/MigrationBundle.cs` | Builds the bundle once per test process, applies it per database, redacts passwords from tooling output |
| `tests/YCR.Infrastructure.Tests/SqlServerFixture.cs` | The xUnit collection-fixture adapter |
| `tests/YCR.Infrastructure.Tests/Persistence/MigrationBundleTests.cs` | V6 plus the migrate-and-query smoke test |
| `tests/YCR.Infrastructure.Tests/Persistence/SqlServerImagePinTests.cs` | Compose-vs-constant pin equality; tag-and-digest shape |
| `tests/YCR.Infrastructure.Tests/Persistence/ModuleInterfacesTests.cs` | S17 moved onto the real container |
| `docker-compose.yml` | Local dev server plus a one-shot init service, both on the pinned image |
| `docker/sqlserver/init-principals.sql` | Database, `ycr_migrator` and `ycr_app` logins and users. Idempotent |
| `.env.example` | The three required passwords, no defaults |
| `.config/dotnet-tools.json` | `dotnet-ef` 10.0.12 |
| `Directory.Packages.props`, `tests/YCR.TestSupport/YCR.TestSupport.csproj` | `Testcontainers.MsSql` 4.15.0 |
| `tests/YCR.Infrastructure.Tests/YCR.Infrastructure.Tests.csproj` | **Exit-code-8 waiver removed at its named step** |

### Evidence

- `dotnet build YCR.sln` — **0 warnings, 0 errors**.
- `dotnet test YCR.sln --no-build` — **total 48, failed 0, succeeded 48, skipped 0**. Up from 43; the five new tests are three in `MigrationBundleTests` and two in `SqlServerImagePinTests`, with `ModuleInterfacesTests` rewritten rather than added to.
- `YCR.Infrastructure.Tests` alone: **8 passed, 0 failed, 0 skipped** in 28s, all against the real container.
- `docker compose config` validates. `docker compose up -d` brings the server to healthy and the init service exits **0**; a second `up -d` is clean, so the script is idempotent. `sys.database_principals` then contains `ycr_app` and `ycr_migrator`, both `SQL_USER`. The stack was torn down with `down -v` afterwards.

**`docker compose up -d --wait` is not the documented command and does not work here**, because `--wait` treats a one-shot init service that exits as a failure even on exit 0. AGENTS.md §Commands documents plain `docker compose up -d`, which is what was verified.

### Design notes worth a reviewer's attention

- **`YCR.TestSupport` still references no test framework.** `tests/Directory.Build.props` excludes it from the runner, so the container class is a plain `IAsyncDisposable` and each consuming test project supplies its own thin collection-fixture adapter. The alternative — adding `xunit.v3.extensibility.core` to TestSupport — would have been a new package and would have coupled the fixture library to the runner.
- **No `Microsoft.Data.SqlClient` pin was added.** `SqlConnection` arrives with the EF Core SQL Server provider through `YCR.Infrastructure`, and central transitive pinning fixes its version; a second explicit pin could only drift from the provider's own requirement. Recorded in `plan.md` §New packages under "Deliberately not added".
- **Credentials.** The `sa` password is generated per container from `RandomNumberGenerator` and never written to source. Connection strings are passed to the bundle on the command line, so `MigrationBundle.Redact` strips `Password=...` from any tooling output before it can reach an exception message or a log. Compose defaults no password at all — a missing variable stops compose rather than creating a known credential.
- **S17 now proves something.** It previously asserted over a connection string that was never opened. It now runs against the real database and shows that work tracked through `INetworkDbContext` is saved by the concrete `YcrDbContext` in one `SaveChangesAsync`, which is the property ADR-0012's one-context-many-interfaces design actually depends on.
- **Credential caveat, deliberate.** S17 and the smoke tests run under the migrator credential, not `ycr_app`, because the `ycr_app` **role** does not exist until the `Security_AppDatabaseRole` migration at **step 9**. The plan's §Test fixture table assigns S17 to `ycr_app`; that move belongs to step 9 and is called out in the test's own XML comment so it cannot be forgotten.
- **`init-principals.sql` creates no role and no grant.** Logins and users only. Every privilege stays in the migration, so a grant cannot enter the system through a provisioning script nobody diffs (plan P9, ADR-0017 item 3).

**Next step (exact):** plan step 8 — run **V1–V4** against the pinned image first, then implement the audit ledger: `AuditEvent` mapping with `ExcludeFromMigrations()`, `AuditWriter`, and the `Audit_CreateAuditEventsLedger` raw-SQL migration with its version and edition guard, three `ISJSON` check constraints and two nonclustered indexes. **If V1 or V2 fails, stop and return to stage 2 — do not substitute a normal table** (AGENTS.md rule 8).

**Blockers / open questions:** none technical. OQ26, OQ27 and OQ28 remain open with Myanma Railways and are confined by the approved provisional-rules waiver; T-014 is the release gate.

**State of the branch:** build and tests green; implementation committed and pushed; this progress entry is the only uncommitted change.

---

## 2026-09-20 20:05 Asia/Yangon — claude — T-004

**Stage:** 4 (IMPLEMENT). Step 8 of 13 complete. **V1–V4 all PASS.**
**Commit:** `aa0ae50` (`feat(F-001): add append-only audit ledger`), pushed to `origin/feature/F-001`.
**Implemented by:** claude.

Hein's instruction for this session listed the E6 ledger VERIFY and the audit ledger alongside the step-7 items, so step 8 was carried out in the same session and is reported here. Work stops after this step.

### Verifications — all run against the pinned image *before* any migration was written

The verification ran as a throwaway experiment on a container started from the E4 digest, so the VERIFY results are not a by-product of code that assumed them.

| # | VERIFY | Result | Evidence |
|---|---|---|---|
| **V1** | The pinned image's edition supports ledger tables | **PASS** | `CREATE TABLE ... WITH (LEDGER = ON (APPEND_ONLY = ON))` succeeded on **Developer Edition (64-bit)**, `ProductMajorVersion` 16, `EngineEdition` 3. `sys.tables.ledger_type_desc` = **`APPEND_ONLY_LEDGER_TABLE`** |
| **V2** | `CHECK` constraints and nonclustered indexes are permitted on an append-only ledger table | **PASS** | All three `ISJSON` constraints were created inline with the table and all are listed in `sys.check_constraints`; both nonclustered indexes were created and are listed in `sys.indexes`. A malformed `AfterJson` insert was rejected by `CK_AuditEvents_AfterJson` |
| **V3** | Ledger `CREATE TABLE` runs inside EF's migration transaction | **PASS** | Ledger DDL plus a nonclustered index committed inside an explicit `BEGIN TRANSACTION`/`COMMIT`. A second probe created a ledger table and rolled back: `OBJECT_ID` was `NULL` afterwards, so the DDL is genuinely transactional. **No `suppressTransaction: true` is needed** and the plan's V3 fallback is not used |
| **V4** | EF Core can `INSERT` into a ledger table whose generated columns it does not map | **PASS** | `AuditWriter_WithEfCore_InsertsIntoTheLedgerInTheCallersUnitOfWork` writes a station and its audit row in one `SaveChangesAsync` and reads every column back. The generated columns are `is_hidden = 1`, so EF never sees them. **Risk R-2's fallback is not needed** |

**AGENTS.md rule 8 was not engaged.** No normal table was substituted for anything, and no VERIFY failed.

Also observed and worth recording: the engine rejects both `UPDATE` and `DELETE` on the table with **Msg 37359**, and reports both as "Updates are not allowed for the append only Ledger table" — the message does not distinguish the two. Tests assert the rejection, not the wording of the delete case.

### What was implemented

| File | What |
|---|---|
| `src/YCR.Infrastructure/Persistence/Migrations/20260920130536_Audit_CreateAuditEventsLedger.cs` | Raw-SQL ledger DDL: guard, schema, table with ADR-0021's fourteen columns and three `ISJSON` constraints, two nonclustered indexes. `Down()` throws |
| `src/YCR.Infrastructure/Audit/AuditEvent.cs` | The row shape. A persistence record, not a domain type |
| `src/YCR.Infrastructure/Persistence/Configurations/Audit/AuditEventConfiguration.cs` | Maps it with `ExcludeFromMigrations()` |
| `src/YCR.Infrastructure/Audit/AuditWriter.cs` | `IAuditWriter` implementation |
| `src/YCR.Infrastructure/Persistence/YcrDbContext.cs` | `internal DbSet<AuditEvent>` |
| `src/YCR.Infrastructure/DependencyInjection.cs` | Registers `IAuditWriter` scoped and `TimeProvider.System` |
| `tests/YCR.Infrastructure.Tests/Persistence/LedgerMigrationTests.cs` | S18 plus V1, V2 and V4 |
| `tests/YCR.Infrastructure.Tests/Persistence/MigrationBundleTests.cs` | Smoke test renamed to the plan's `Migrate_AgainstPinnedImage_CreatesStationsAndLedgerTable` and extended to assert the ledger table exists |

### The guard, and what it does and does not prove

The guard is the migration's first statement, so a rejected server leaves nothing half-created — and V3 shows a failure rolls back the whole migration anyway.

It has two branches:

1. **Version.** `ProductMajorVersion >= 16`, else `THROW 50017` naming the detected version *and* edition. Ledger tables do not exist before SQL Server 2022. Plan step 12 proves this branch fires against a pinned 2019 image (S19).
2. **Ledger surface.** `OBJECT_ID('sys.database_ledger_transactions')` and the `ledger_type` column on `sys.tables` must both exist, else the same `THROW`.

**Deliberately not an edition allowlist.** Writing `EngineEdition IN (2,3,4,...)` would have meant asserting a list of editions I had not verified, on a server where every reachable edition passes. Probing the engine's actual ledger surface is a fact this migration can check for itself. What it proves is that the engine exposes ledger; what it cannot prove is that a licence permits ledger's use. The plan already states this limit — "the **edition** branch is not testable this way... it is covered by V1 in step 8 and by code review at stage 6" — and V1 has now covered it for the pinned image. **This is the one place in step 8 worth a reviewer's deliberate attention.**

### Design notes worth a reviewer's attention

- **`AuditEvent` lives in Infrastructure, not Domain.** The plan's `src/` inventory lists an `AuditEventConfiguration` and an `AuditWriter` but no domain or application `AuditEvent` type, and the Application layer's whole contract with the audit trail is `IAuditWriter`. Putting the fourteen-column row shape in Infrastructure keeps the ledger's shape out of every layer that has no business knowing it. `YcrDbContext.AuditEvents` is `internal` for the same reason: no module context interface exposes it, so the only route into the table from above Infrastructure is `IAuditWriter`, which derives the actor fields server-side.
- **`ExcludeFromMigrations()` makes ADR-0017 item 6 mechanical.** EF emits no DDL for the table at all, so no future model change can produce a migration that converts or drops the ledger. The scaffolded migration came out empty, which confirmed it.
- **`IAuditWriter.Record` takes `object subject`, and step 8 had to decide what that means.** The mapping implemented is: `SubjectType` is the runtime type name, or the `Type`'s name when a caller passes a `Type` for an event with no entity to hand; `SubjectId` is `Entity.Id` when the subject is an `Entity`, and null otherwise — which ADR-0021 explicitly allows. This is an engineering mapping, not a business rule, and it is tested both ways. **Step 10's handlers are the first real callers**; if hein would rather the contract named `subjectType` and `subjectId` explicitly instead of inferring them, that is a small change to make at step 10 and a cheaper one than after more callers exist.
- **`ActorRole` array shape is the writer's job.** ADR-0021's own §Consequences says `ISJSON` proves only that the text is JSON, not that it is an array, and puts the array obligation on `IAuditWriter` with a test. The writer serialises `ICurrentUser.Roles` as a JSON array and the V4 test asserts the exact text `["Admin","StationManager"]`.
- **Actor fields cannot be spoofed by construction.** `Record` has no parameter through which a caller could supply `ActorUserId`, `ActorRole`, `ClientIp` or `CorrelationId`; all four come from `ICurrentUser` (ADR-0017 item 2). S20 at step 10 tests this end to end from a request that tries.
- **`TimeProvider.System` is now registered** (`TryAddSingleton`) because ADR-0021 stamps `OccurredAtUtc` from `TimeProvider` (ADR-0018), and an append-only row's timestamp needs to be controllable in a test.
- **`Down()` is tested, not just written.** `DownMigration_ThrowsInsteadOfDroppingTheLedgerTable` generates the actual down script through `IMigrator` and asserts it contains the `THROW` and does not contain a `DROP TABLE` for the ledger. A comment saying "this throws" would not have caught a later edit; the generated script does.

### Evidence

- `dotnet build YCR.sln` — **0 warnings, 0 errors**.
- `dotnet test YCR.sln --no-build` — **total 57, failed 0, succeeded 57, skipped 0**. Up from 48; the nine new tests are eight in `LedgerMigrationTests` and one extra assertion path, with `MigrationBundleTests` extended rather than added to.
- `YCR.Infrastructure.Tests` alone: **17 passed, 0 failed, 0 skipped** in 39s, all against the real pinned container.
- The scaffolded `Audit_CreateAuditEventsLedger` migration was generated **empty** before the raw SQL was written, which is the direct evidence that `ExcludeFromMigrations()` holds.
- `YcrDbContextModelSnapshot.cs` carries `AuditEvent` with `ToTable("AuditEvents", "audit", t => t.ExcludeFromMigrations())`.

### Not done, and deliberately so

- **S22 (`DatabasePrivilegeTests`) and the `ycr_app` credential** belong to plan step 9. The `Security_AppDatabaseRole` migration does not exist yet, so every container-backed test still runs as the migrator. `init-principals.sql` already creates the `ycr_app` login and user awaiting that role.
- **S19 (`LedgerGuardTests`, the pinned SQL Server 2019 image)** belongs to plan step 12 as a trunk-only category. The version branch of the guard is therefore written and reviewed but not yet executed against an unsupported server.
- **`AuditWriter` has no caller yet.** Step 10's handlers are the first.

**Next step (exact):** plan step 9 — least privilege: the `Security_AppDatabaseRole` migration granting the `ycr_app` role `SELECT, INSERT, UPDATE` on `network.Stations` and `INSERT, SELECT` on `audit.AuditEvents` and nothing else; two connection strings from the fixture; `ALTER ROLE ycr_app ADD MEMBER` after migrations; no startup migration. Ends green with S22, and moves S17 and the Application/API suites onto the `ycr_app` credential.

**Blockers / open questions:** none technical. OQ26, OQ27 and OQ28 remain open with Myanma Railways and are confined by the approved provisional-rules waiver; T-014 is the release gate.

**State of the branch:** build and tests green; implementation committed and pushed; this progress entry is the only uncommitted change.

**Review split so far:** steps 1, 7 and 8 implemented by claude; steps 2–6 and the step-5 review fixes implemented by Codex. Per `TASKS.md` §Protocol item 11, T-005, T-006 and T-007 over claude's steps must be done by an agent other than claude, and over Codex's steps by an agent other than Codex.

---

## 2026-09-20 21:10 Asia/Yangon — claude — T-004

**Stage:** 4 (IMPLEMENT). Step 9 of 13 complete. **Hein's three rulings of 2026-09-20 applied.**
**Commit:** `b6bf336` (`feat(F-001): add least-privilege role and audit contract rulings`), pushed to `origin/feature/F-001`.
**Implemented by:** claude.

### Rulings applied

**Ruling 1 — the guard's edition probe: accepted as is.** No change; the step-8 implementation stands.

**Ruling 2 — `IAuditWriter` changed before any handler depends on it.** The new contract is

```csharp
void Record(string action, string subjectType, Guid? subjectId,
            object? before, object? after, string? reasonCode = null);
```

- **`subjectType` is a module-declared constant.** `YCR.Application.Network.NetworkAuditSubjects.Station` = `"Network.Station"`. Nothing derives it from a CLR type name any more. The reason is recorded on the type: ledger rows outlive the code that wrote them, so a class rename must not be able to change what a historical row appears to be about.
- **`before`/`after` are explicit snapshot records.** `StationAuditSnapshot(Code, NameEn, NameMy, IsActive)`, with `From(Station)`. Its XML doc states that changing its shape requires bumping `PayloadVersion` (ADR-0021 rule 3).
- **`authorizedByPermission` is gone from the signature.** `ICurrentUser` gained `AuthorizedByPermission`, and `AuditWriter` reads it from there. A handler can no longer name its own authority — the claim would be unfalsifiable once written to an append-only row.

**Ruling 3 — the `UPDATE` grant narrowed to the column.** `GRANT UPDATE ON [network].[Stations]([IsActive])`, with `SELECT, INSERT` still at table level. `DeactivateStation` is the only update F-001 performs; a table-wide grant would also have let the application rewrite a station's code or names, which no endpoint offers. A future rename feature now needs its own grant and its own review.

### Entity rejection — what was done and the choice behind it

Hein asked for an entity passed as a snapshot to be "rejected or impossible by type". The signature in the ruling types `before`/`after` as `object?`, so **rejection** is what is implemented: `AuditWriter` throws `ArgumentException` naming the parameter and the entity type. `AuditWriter_WhenHandedAnEntityAsASnapshot_Refuses` proves it throws and that nothing was tracked, so a later save cannot carry it through.

**The stronger option was not taken because it would change the ruled signature.** Typing the parameters as a marker interface — `IAuditSnapshot?` — would make the mistake fail to compile rather than fail at runtime, since `Station` would not implement it. That is a small change if hein wants it; it is noted rather than done because the signature was given explicitly.

### What was implemented

| File | What |
|---|---|
| `src/YCR.Infrastructure/Persistence/Migrations/20260920135245_Security_AppDatabaseRole.cs` | The `ycr_app` role and its grants. `Down()` revokes, drops members, drops the role |
| `src/YCR.Application/Common/Abstractions/IAuditWriter.cs` | The ruled signature, with the reasoning for each of its three changes |
| `src/YCR.Application/Common/Abstractions/ICurrentUser.cs` | `AuthorizedByPermission` |
| `src/YCR.Application/Network/NetworkAuditSubjects.cs` | `Network.Station` |
| `src/YCR.Application/Network/StationAuditSnapshot.cs` | The snapshot record and `From(Station)` |
| `src/YCR.Infrastructure/Audit/AuditWriter.cs` | New contract, entity rejection, widened JSON encoder |
| `src/YCR.Application/Common/UniqueConstraintViolationException.cs` | The provider-neutral boundary (tech-lead ruling, step 4) |
| `src/YCR.Infrastructure/Persistence/SqlServerUniqueConstraintTranslator.cs` | SQL Server 2601/2627 → that exception |
| `src/YCR.Infrastructure/Persistence/YcrDbContext.cs` | `SaveChanges`/`SaveChangesAsync` overrides that apply the translation |
| `tests/YCR.TestSupport/SqlServerTestContainer.cs` | Per-container `ycr_app` login, per-database user, role membership after the bundle, two connection strings, and `CreateEmptyDatabaseAsync` |
| `tests/YCR.Infrastructure.Tests/Persistence/DatabasePrivilegeTests.cs` | S22 |
| `tests/YCR.Infrastructure.Tests/Persistence/UniqueConstraintTranslationTests.cs` | Both error numbers, against real SQL Server |
| `docker/sqlserver/init-principals.sql` | **Step-7 defect fixed** — see below |

### A step-7 defect this step exposed

`init-principals.sql` created a database **user** named `ycr_app`. The migration creates a **role** of that name, and SQL Server will not allow a role and a user to share a name — the local dev stack would have failed the moment anyone ran the bundle against it. The plan's §Test fixture already specified `ycr_app_user`; step 7 simply did not follow it. Fixed: the **login** is `ycr_app`, the **user** is `ycr_app_user`, the **role** is `ycr_app`.

The script also now joins the user to the role when the role exists, and prints what to do when it does not, because on a first `docker compose up -d` the migration has not run yet.

### Things a reviewer should look at closely

- **The unique-constraint translator parses SQL Server's message text.** There is no API that exposes the offending constraint name, so it has to be read out of the message. That is a real weakness and it is handled rather than hidden: when the name cannot be extracted — a localised server, or a changed message format — the translator returns null and the **original exception propagates untranslated**. A write then fails as an unexpected infrastructure error, which is correct, rather than being reported as whatever conflict the nearest handler happens to know about. Both error numbers are provoked from real DDL in tests rather than from hand-built exceptions, because a test over a fabricated message would only prove the regex matches a string the test wrote.
- **`SaveChangesAsync(bool, CancellationToken)` is the overridden overload**, not the one-argument one. Every other overload delegates to it; overriding the shorter one would have left a direct two-argument call untranslated.
- **The JSON encoder was widened to all Unicode ranges, and this was a real defect, not a preference.** `System.Text.Json`'s default escapes every non-ASCII character, so the first run stored `ရန်ကုန်ဘူတာကြီး` as a run of six-character escape sequences — unreadable to an investigator and several times larger, in a table that can never be rewritten. Myanmar names are the norm on this network. The change widens the character range only; HTML- and script-sensitive characters are still escaped. It changes a payload's encoding, not its shape, so **no `PayloadVersion` bump is required** — the JSON value is identical either way. The test that caught it asserts the literal Myanmar text, and it was the test that was right.
- **S22 asserts absences.** `HAS_PERMS_BY_NAME` is used rather than an attempted `UPDATE` on the ledger, because the ledger engine also refuses updates — a failed statement would pass even if the grant were wrong. The permission itself is checked directly, and a separate test shows both controls hold.
- **My first version of `ApplicationCredential_AttemptingDdl_IsDenied` asserted one exact sentence and failed four of its own five cases.** SQL Server words denial differently per statement: "CREATE TABLE permission denied", "User does not have permission to perform this action", and — where the credential cannot even see the object — "Cannot find the object ... or you do not have permission". The test now asserts the stable element and, more usefully, that the schema is genuinely unchanged afterwards.

### Evidence

- `dotnet build YCR.sln` — **0 warnings, 0 errors**.
- `dotnet test YCR.sln --no-build` — **total 73, failed 0, succeeded 73, skipped 0**. Up from 57.
- `YCR.Infrastructure.Tests` alone: **33 passed, 0 failed, 0 skipped**, all against the real pinned container.
- **The local dev flow was run end to end, not assumed.** `docker compose up -d` → init reports the role does not exist yet → `dotnet ef migrations bundle` built and applied all three migrations under the real `ycr_migrator` credential (not `sa`) → `docker compose up -d sqlserver-init` → "ycr_app_user added to the ycr_app role". Connecting as `ycr_app` then returns `CURRENT_USER` = `ycr_app_user`, `UPDATE` on `IsActive` = 1, `UPDATE` on `Code` = 0, `INSERT` on the ledger = 1, `UPDATE` on the ledger = 0 — the same shape the fixture produces. Torn down with `down -v`.

### Documentation debt recorded

`plan.md` §Steps now carries the **stage-8 correction to `docs/20` §3**. Its line 104 reads `audit.Record("Network.StationCreated", station, before: null, after: station);`, which passes the aggregate as both subject and payload — exactly what ruling 2 forbids. The corrected call is recorded there so the edit is traceable to the ruling.

**Next step (exact):** plan step 10 — Application handlers: `CreateStation`, `DeactivateStation` (`IsActive` concurrency token), `GetStation`, `ListStations`, and DI scanning. Ends green with all `YCR.Application.Tests` rows (S1, S3, S5–S8, S13, S14, S20, S27) under the `ycr_app` credential, and removes that project's exit-code-8 waiver at its named step.

**Blockers / open questions:** none technical. OQ26, OQ27 and OQ28 remain open with Myanma Railways and are confined by the approved provisional-rules waiver; T-014 is the release gate.

**State of the branch:** build and tests green; implementation committed and pushed; this progress entry is the only uncommitted change.

**Review split so far:** steps 1, 7, 8 and 9 implemented by claude; steps 2–6 and the step-5 review fixes by Codex.

---

## 2026-09-20 21:45 Asia/Yangon — claude — T-004

**Stage:** 4 (IMPLEMENT). Step 9 amended by two further rulings; step 10 not yet started.
**Commit:** `6dfb67a` (`feat(F-001): make audit snapshots type-safe and pin session language`), pushed to `origin/feature/F-001`.
**Implemented by:** claude.

### Ruling 1 — audit snapshots are impossible-by-type

`IAuditSnapshot` is a member-less marker interface in `YCR.Application.Common.Abstractions`. `IAuditWriter.Record` now takes `IAuditSnapshot? before, IAuditSnapshot? after`, `StationAuditSnapshot` implements it, and the runtime entity check is **removed**.

**The compiler proved the ruling immediately.** The first build after the change failed with `CS1503: cannot convert from 'YCR.Domain.Network.Station' to 'IAuditSnapshot?'` — the old runtime-rejection test could no longer compile, which is exactly the outcome the ruling wanted. That test is replaced by `Station_IsNotAnAuditSnapshot_SoPassingItCannotCompile`, which asserts the aggregate does not implement the marker and the snapshot does. The barrier disappears silently if an entity ever implements the interface, and every call site would keep compiling, so it is worth one assertion.

**Architecture test added** — `AuditSnapshots_AreRecordsInAnApplicationModuleNamespace`. Every `IAuditSnapshot` implementation must be a record and must live in `YCR.Application.<Module>`. Three planted fixtures prove both halves bite: `NonRecordAuditSnapshot` (a class), `MisplacedAuditSnapshot` (right kind, wrong namespace) and `YCR.Domain.Network.Violations.DomainAuditSnapshot` (a record that drifted into Domain). A second test, `AuditSnapshots_ExistAtAllSoTheRuleIsNotVacuous`, fails if `src/` ever contains no snapshot at all — without it the rule would pass trivially the day someone deleted the last one.

Written with reflection rather than as an ArchUnit rule because "is a record" is not a dependency fact: it is detected by the compiler-generated `<Clone>$` member, which ArchUnit's fluent API does not express.

### A trap the marker interface introduced, and what it would have cost

`JsonSerializer.Serialize(snapshot, PayloadOptions)` serialises against the **static** type. With the parameter now typed as `IAuditSnapshot` — which declares no members — that overload would have written **`{}` into every `BeforeJson` and `AfterJson`**: a silent, total loss of audit state, in a table that can never be corrected, with no error anywhere. `SerializeSnapshot` therefore passes the runtime type explicitly, and the comment says why so nobody "simplifies" it back.

This was caught by the existing tests that assert exact payload JSON. It is the second time in two steps that an exact-value assertion caught something an existence assertion would have missed.

### Ruling 2 — `us_english` pinned, and VERIFIED

| Where | How |
|---|---|
| Test fixture | `SqlServerImage.SessionLanguage = "us_english"`, applied via `SqlConnectionStringBuilder.CurrentLanguage` to **both** connection strings |
| Compose | Both logins in `init-principals.sql` now set `DEFAULT_LANGUAGE = us_english` |
| Connection strings | `.env.example` documents both, each carrying `Current Language=us_english`, with the reason and "do not drop this setting" |
| CI | Recorded as a step-13 obligation in `plan.md` §Steps |

**VERIFY — SqlClient accepts it: PASS.** Two levels of evidence. `SqlConnectionStringBuilder.CurrentLanguage` is a strongly typed property, so the build itself proves the keyword exists; and `BothCredentials_ConnectOnAUsEnglishSession` opens both connections and asserts `SELECT @@LANGUAGE` returns `us_english`. Against the local compose stack, connecting as `ycr_app` likewise returns `us_english` and database `YCR`.

**The translator's own input is asserted, not just some connection.** `SaveChanges_WithDuplicateStationCode_...` checks `@@LANGUAGE` on the very connection whose exception it then parses. Asserting the language on a different connection would not have proven anything about the message that was actually read.

**Why login `DEFAULT_LANGUAGE` as well as the connection string:** the connection string covers the application; the login default covers any tool that connects without naming a language — sqlcmd, a migration bundle run by hand, a DBA session. Belt and braces, because the failure mode is silent.

The dependency is documented on `SqlServerUniqueConstraintTranslator` itself as a REQUIRED CONTROL, naming every place the language is pinned, so the next person to touch the regex finds the constraint next to the code rather than in a progress file.

### Evidence

- `dotnet build YCR.sln` — **0 warnings, 0 errors**.
- `dotnet test YCR.sln --no-build` — **total 76, failed 0, succeeded 76, skipped 0**. Up from 73.
- Local compose stack re-verified after editing `init-principals.sql`: init runs clean, and `ycr_app` reports `@@LANGUAGE` = `us_english`, `DB_NAME()` = `YCR`. Torn down with `down -v`.

### One thing I broke and fixed within this session

A `perl -0pi` edit to `init-principals.sql` consumed the `$(YcrDatabase)` sqlcmd variable, leaving `DEFAULT_DATABASE = []`. Caught by re-reading the file, fixed with a plain edit, and confirmed by the compose run above reporting `DB_NAME()` = `YCR`. Recorded because the failure would only ever have surfaced at runtime in someone's local environment.

**Next step (exact):** plan step 10 — Application handlers: `CreateStation`, `DeactivateStation` (`IsActive` concurrency token), `GetStation`, `ListStations`, and DI scanning. They are the first real callers of the new audit contract. Ends green with all `YCR.Application.Tests` rows (S1, S3, S5–S8, S13, S14, S20, S27) under the `ycr_app` credential, and removes that project's exit-code-8 waiver at its named step.

**Blockers / open questions:** none technical. OQ26, OQ27 and OQ28 remain open with Myanma Railways; T-014 is the release gate.

**State of the branch:** build and tests green; committed and pushed; this progress entry is the only uncommitted change.

---

## 2026-09-20 22:40 Asia/Yangon — claude — T-004

**Stage:** 4 (IMPLEMENT). Step 10 of 13 complete.
**Commit:** `63adc65` (`feat(F-001): add Network application handlers`), pushed to `origin/feature/F-001`.
**Implemented by:** claude.

### What was implemented

Four handlers in `YCR.Application.Network`, registered by assembly scanning (ADR-0004 §Consequences), each ending in one `SaveChangesAsync`.

| File | What |
|---|---|
| `Network/CreateStation/CreateStationCommand.cs`, `CreateStationHandler.cs` | S1, S5, S6, S13, S14 |
| `Network/DeactivateStation/DeactivateStationCommand.cs`, `DeactivateStationHandler.cs` | S2, S7, S8, S27 |
| `Network/GetStation/GetStationQuery.cs`, `GetStationHandler.cs`, `StationDto.cs` | S4, S8 |
| `Network/ListStations/ListStationsQuery.cs`, `ListStationsHandler.cs` | S3, S10 |
| `Network/NetworkConstraints.cs` | `UX_Stations_Code`, so the handler can match the constraint by name |
| `Common/Pagination/Paging.cs` | `docs/20` §4's `max 200`, stated once |
| `DependencyInjection.cs` | `AddApplication()` |
| `YCR.Domain/Network/NetworkErrors.cs` | `Network.InvalidPageRequest` |
| `tests/YCR.Application.Tests/**` | 30 tests, all under `ycr_app` |

**Exit-code-8 waiver removed** from `YCR.Application.Tests` at its named step.

### Decisions a reviewer should check

- **Queries materialise the entity and then map, instead of projecting to a DTO in SQL.** The plan says "queries project straight to DTOs with `AsNoTracking()`", and that is not achievable here: `Station.Code` is a value object behind an EF value converter, and EF cannot translate member access through one — `station.Code.Value` inside a `Select` does not compile to SQL. `AsNoTracking()` is kept, the mapping lives in one place (`StationDto.From`), and only one page is ever materialised, bounded by `Paging.MaxPageSize`. **This is a deviation from the plan's wording and is called out rather than quietly done.**
- **Listing is ordered by `Code`.** Without a deterministic order SQL Server may return rows differently between pages, so a caller walking the pages would silently see duplicates and omissions. `Code` is unique, so it is a total order by itself. Tested across three pages.
- **`Network.InvalidPageRequest` lives in `NetworkErrors`, in Domain.** The *limits* are platform-wide and live in `Application.Common.Pagination`; only the error **code** is per module, because `docs/20` §2 requires `<Module>.<Reason>`. Keeping every `Network.*` code discoverable in one class was judged worth more than the layering purity of moving one validation error into Application. Flagging it as a judgement call.
- **`NetworkConstraints.StationCodeUniqueIndex` is a duplicated literal, and the duplication is closed by a test.** The index is declared in Infrastructure, which Application may not reference, so the name has to be restated. `StationModelTests` now asserts the mapped index name equals the Application constant. Without that, a rename in Infrastructure would turn a `409` into an unhandled `500`, and nothing would fail until a duplicate code was posted in production.
- **`CreateStationHandler` keeps both guards, deliberately.** The pre-check gives an ordinary caller a clean `409` with a useful message; the unique index is the authority that settles the race the pre-check cannot (S13, R7). The catch matches **one named constraint**, so an unrelated uniqueness failure propagates as an unexpected error instead of being reported as a duplicate station code.
- **The S13 test does not assert which guard fired.** Depending on scheduling either the pre-check or the index may reject the loser, and both are correct; asserting one would make the test flaky. The index path specifically is proven by `UniqueConstraintTranslationTests`, which provokes the violation directly rather than hoping the scheduler cooperates. My first version of that comment claimed the test forced the index path — it does not, and the comment was corrected before commit.

### A step-7 defect this step exposed

`dotnet test YCR.sln` failed with `Handle_WhenRequestSuppliesActorFields_IgnoresThem` reporting `'dotnet' exited with 1 ... Build failed`, while `YCR.Application.Tests` alone passed. The cause was mine, from step 7: `dotnet ef migrations bundle` publishes through the Infrastructure project's own `obj/` directory, and `MigrationBundle`'s build gate was a **`SemaphoreSlim` — in-process only**. With two container-backed suites, `dotnet test` runs two test *processes* in parallel and both built the bundle into the same intermediate output.

It was invisible until now because step 9 had only one container-backed suite.

Fixed with `CrossProcessLock`, an exclusively opened lock file: a named `Mutex` would be simpler but is not shared between processes on Linux, where CI runs, and the OS releases a file handle even if a test process is killed, so a crash cannot leave the lock held. The bundle is now cached per checkout (keyed by a hash of the repository path, so worktrees do not share one) and reused when it is newer than the Infrastructure assembly it was built from.

**Verified both paths, not just the fixed one:** with the cache deleted, `dotnet test YCR.sln` is green in 1m50s (one process builds, the other waits); run again warm, green in 56s (the bundle is reused rather than rebuilt).

### Evidence

- `dotnet build YCR.sln` — **0 warnings, 0 errors**.
- `dotnet test YCR.sln --no-build` — **total 106, failed 0, succeeded 106, skipped 0**. Up from 76.
- `YCR.Application.Tests` alone: **30 passed, 0 failed, 0 skipped**, all against the real container under `ycr_app`.
- Two xUnit analyser errors (`xUnit2031`) failed the build on first attempt and were fixed rather than suppressed; `TreatWarningsAsErrors` did its job.

### Scenario coverage added at this step

S1, S3, S4, S5, S6, S7, S8, S10, S13, S14, S20, S27, plus boundary cases for page size and every invalid-code shape from S9. Stage 5 (T-005) owns completeness against the full spec list.

**Not done, deliberately:** the API-level halves of these scenarios — status codes, ProblemDetails, authorization — belong to step 11. A handler returning `ErrorType.BusinessRule` is asserted here; that it becomes a `422` is asserted there.

**Next step (exact):** plan step 11 — API: `Program`, ProblemDetails, `ResultExtensions`, `ValidationFilter`, the permission policy provider, the authentication scheme guard, contracts and `StationEndpoints`, plus health endpoints. Ends green with all `YCR.Api.Tests` rows (S2, S4, S9–S12, S21b, S24, S25) and removes that project's exit-code-8 waiver at its named step.

**Blockers / open questions:** none technical. OQ26, OQ27 and OQ28 remain open with Myanma Railways; T-014 is the release gate.

**State of the branch:** build and tests green; implementation committed and pushed; this progress entry is the only uncommitted change.

**Review split so far:** steps 1, 7, 8, 9 and 10 implemented by claude; steps 2–6 and the step-5 review fixes by Codex.

---

## 2026-09-21 01:20 Asia/Yangon — claude — T-004

**Stage:** 4 (IMPLEMENT). Pre-step-11 rulings applied (`00d1341`) and **step 11 of 13 complete** (`a57b491`), both pushed to `origin/feature/F-001`.
**Implemented by:** claude.

### Hein's rulings of 2026-09-21

**Ruling 1 — confirm ListStations pages in SQL.** It already did: `Skip`, `Take` and `CountAsync` were all on `IQueryable`, so the count was a `SELECT COUNT(*)` and the page was `OFFSET`/`FETCH NEXT`. What was wrong was the **projection** — the handler materialised whole entities. Both query handlers now project through `StationProjection`, taking **whole value-object properties** (`station.Code`, never `station.Code.Value`), which EF can translate because the converter applies to the column; reaching inside it is what fails. `docs/20`'s "queries project" now holds, and the pattern is recorded on `StationProjection` for the stage-8 `docs/20` §4 update.

`NameEn`/`NameMy` are projected as scalars rather than as the owned `BilingualName`, because EF refuses to project an owned entity type into a non-entity result.

**`ListStationsSqlTests` asserts against the generated SQL**, captured with `LogTo`: exactly two round trips, a `COUNT(*)` with no `OFFSET`, and a page query carrying `ORDER BY`, `OFFSET` and `FETCH NEXT`, selecting named columns. This is the only kind of test that can catch the regression — a query that fetched the whole table and paged in memory returns identical rows, so every behavioural test would still pass.

**Ruling 2 — the `{}` guard.** `Handle_WithValidCommand_WritesTheStationStateIntoAfterJsonVerbatim` reads `AfterJson` straight out of `audit.AuditEvents` and asserts the code and the Myanmar name appear verbatim, that the payload is not `{}`, and that no `\u` escape appears. One assertion covers both the runtime-type serialisation and the widened encoder.

**Ruling 3 — CI bundle handoff.** `MigrationBundle` now uses `YCR_MIGRATION_BUNDLE` when set, **failing loudly if the path does not exist** rather than silently building a second bundle, and falls back to the locked local build otherwise. The CI obligation is recorded in `plan.md` §Steps step 13.

### Step 11 — the API

| File | What |
|---|---|
| `Program.cs` | Composition root; no migration at startup; OpenAPI in Development only |
| `Common/ResultExtensions.cs` | The ADR-0004 status mapping, in one place. The only file depending on `YCR.Domain`, and only on P11's four types |
| `Common/ProblemDetailsSetup.cs` | RFC 9457 with `errorCode` and `traceId`; the 500-with-no-internals handler |
| `Common/ValidationFilter.cs` | FluentValidation behind an endpoint filter |
| `Common/Authorization/*` | Permission requirement, handler, policy provider, **and `AuthorizationResultHandler` — see below** |
| `Common/Authentication/AuthenticationSchemeGuard.cs` | S21b |
| `Common/HttpContextCurrentUser.cs` | **Plan addition** — see below |
| `Contracts/Network/*`, `Endpoints/Network/StationEndpoints.cs`, `Endpoints/Health/HealthEndpoints.cs` | The surface |
| `YCR.Api.http` | Every endpoint, with the 401s explained |
| `tests/YCR.Api.Tests/**` | 36 tests |

**Exit-code-8 waiver removed** from `YCR.Api.Tests` at its named step. Only `YCR.IntegrationTests` still carries one, intentionally, for all of F-001 (plan C4).

### The defect that only running the API could find

**Symptom:** against the local compose database, every `/api/v1/stations` request returned **`500`**, not `401`.

**Cause:** ADR-0020 ships F-001 with the authorization pipeline and **no authentication handler**. `RequireAuthenticatedUser()` then challenges, and ASP.NET Core throws — *"No authenticationScheme was specified, and there was no DefaultChallengeScheme found."* An ordinary unauthenticated request to a deployed instance would have looked like a server fault.

**Why no test caught it:** every API test registers the test handler through `ConfigureTestServices`, so a challenge scheme always existed and the throw never fired. The suite was structurally blind to the shape that actually ships. I found it only because I ran the API to check the `.http` file's claims were true rather than asserting them.

**Fix:** `AuthorizationResultHandler`, an `IAuthorizationMiddlewareResultHandler` that answers a clean `401 Common.Unauthenticated` **only when there is genuinely no default challenge scheme**. It adds no `AuthenticationHandler` to `src/`, which ADR-0020 item 4 forbids, and it steps aside entirely once the ADR-0016 token feature registers a real scheme. `MissingSchemeTests` hosts the application with authentication left exactly as `src/` configures it — the deployed shape — and covers all four station routes plus both probes.

The exception handler did behave correctly throughout: the 500 leaked no internals. It was the status code that was wrong.

### Other things a reviewer should look at

- **`HttpContextCurrentUser` is a plan addition.** The plan's `src/YCR.Api` inventory lists no `ICurrentUser` implementation, but `AuditWriter` requires one and only the composition root has an `HttpContext`. Every field is derived server-side; `AuthorizedByPermission` comes from the matched endpoint's own policy name, which works because `PermissionPolicyProvider` names each policy after the permission it enforces.
- **`ClientIp` is the connection's peer address, never `X-Forwarded-For`.** A forwarded header is client-supplied and therefore forgeable, which ADR-0017 item 2 rules out for an audit field. Behind a proxy the correct fix is `ForwardedHeadersOptions` with an explicit trusted-proxy list — a deployment decision, not something to assume. `Post_WhenRequestTriesToSupplyActorFields_RecordsTheAuthenticatedActor` sends a forged header and asserts it is ignored.
- **`PermissionPolicyProvider` only treats a policy name as a permission when it matches `docs/20` §2's `<resource>.<action>` shape.** Anything else falls through to the default provider, so a genuinely unknown policy name still fails loudly instead of silently getting a policy no claim can satisfy.
- **`ResultExtensions` throws on an unmapped `ErrorType`** rather than defaulting. A new error type must be mapped deliberately; saying so loudly beats returning 500, or worse 200.
- **No role-to-permission mapping is seeded anywhere** (OQ28). The permission handler reads permission claims; tests mint them directly on the test principal.
- **My own test expectation was wrong once and I fixed the test, not the code.** `Post_WithBlankMyanmarName_Returns400FromTheDomainRule` expected `Network.InvalidStationName`, but FluentValidation's `NotEmpty()` already treats a whitespace-only string as empty, so the filter answers first with `Common.ValidationFailed`. The whitespace case moved into the filter theory where it belongs, and the domain-rule test now uses an overlong name, which genuinely reaches `BilingualName`.

### OpenAPI

**Document URL: `http://localhost:5080/openapi/v1.json`** (path `/openapi/v1.json`; the port is whatever the host binds).

**Development environment only.** The document maps every route and the permission each one requires, which is a map of the system a deployed instance has no reason to hand out. Verified: `200` under `ASPNETCORE_ENVIRONMENT=Development`.

Served as **OpenAPI 3.1.1**, `YCR.Api | v1`, with all four station operations. The first run described every operation as returning `200`, including the `POST` that returns `201` — a document that is wrong is worse than none, so the endpoints now carry `.Produces`/`.ProducesProblem` metadata and the document reports 200, 201, 204, 400, 401, 403, 404, 409 and 422 on the operations that produce them.

The health probes are absent from the document: `MapHealthChecks` emits no OpenAPI metadata. They are documented in `YCR.Api.http` instead.

### `YCR.Api.http`, and no dev-auth

Covers all four station endpoints plus both probes and the OpenAPI document, including invalid-code, missing-field and over-cap variants. Its header explains why most requests return `401` and states plainly that **no dev-auth mechanism is provided, deliberately** — a "just for local development" bypass is exactly the kind of thing that survives into a deployment.

### Evidence

- `dotnet build YCR.sln` — **0 warnings, 0 errors**.
- `dotnet test YCR.sln --no-build` — **total 146, failed 0, succeeded 146, skipped 0**. Up from 106.
- **Verified against a really running API**, not just tests: `docker compose up -d` → migration bundle applied as `ycr_migrator` → init re-run to join the role → API run under the `ycr_app` credential. Results: `/health/live` 200, `/health/ready` 200, `/openapi/v1.json` 200, and all four station routes **401** with `Common.Unauthenticated` and a `traceId`. Before the fix those four were `500`. Stack torn down with `down -v`.

**Next step (exact):** plan step 12 — trunk-only categories: the ADR-0006 sequential-GUID fragmentation control (spec S23, E5) and the SQL Server 2019 guard test (spec S19), the latter against a second digest-pinned image.

**Blockers / open questions:** none technical. OQ26, OQ27 and OQ28 remain open with Myanma Railways; T-014 is the release gate.

**State of the branch:** build and tests green; committed and pushed; this progress entry is the only uncommitted change.

**Review split so far:** steps 1, 7, 8, 9, 10 and 11 implemented by claude; steps 2–6 and the step-5 review fixes by Codex.

---

## 2026-09-21 02:30 Asia/Yangon — claude — T-004

**Stage:** 4 (IMPLEMENT). Step 12 of 13 complete.
**Commit:** `f63c754` (`feat(F-001): add trunk-only fragmentation and ledger guard controls`), pushed to `origin/feature/F-001`.
**Implemented by:** claude.

### Hein's ruling 1 — the production-composition requirement

`docs/21` §Tests gains: **"At least one test runs the unmodified production composition"** — no `ConfigureTestServices` overrides, no substituted services — with F-001's `MissingSchemeTests` named as the reference and the step-11 defect given as the reason.

`docs/templates/review-report.md` gains a **Production-composition check** section, a table for the reviewer to name the qualifying test, and the statement that finding none is a **blocking finding, not a note**.

**ADR-0020 §Follow-up work** now records that the ADR-0016 feature must prove `AuthorizationResultHandler` steps aside once a real scheme is registered — asserting the framework's own `401` with its `WWW-Authenticate` header, and `Common.Unauthenticated` no longer appearing — and must delete the interception if it turns out to be unnecessary rather than leaving a dormant branch in the authorization path. Recorded in the ADR rather than in this feature's notes because the ADR-0016 feature is what has to act on it. Only the Consequences/Follow-up section was touched; the Decision section is untouched, as `docs/decisions/README.md` §Rules requires.

### Ruling 2 — the 2019 image pin

| | |
|---|---|
| **Tag** | `mcr.microsoft.com/mssql/server:2019-CU32-GDR1-ubuntu-20.04` |
| **Digest** | `sha256:cb917712eb2c8a1a497f71a0287ef9aaccd5ce549515c2ace0ae5e734f293d3e` |
| **Pinned on** | 2026-09-21 |
| **Why** | Newest SQL Server 2019 cumulative update on `mcr.microsoft.com` at that date. Reports `ProductMajorVersion` 15, below the 16 the guard requires |

Digest read from the MCR manifest, then confirmed against the pulled image's `RepoDigests`. It lives in `SqlServerImage.Unsupported` beside the 2022 pin, and `SqlServerImagePinTests.UnsupportedVersionImage_CarriesBothATagAndADigest` covers it on the same terms — tag shape, digest shape, composed reference, and that it is genuinely a different image from the supported one. A floating tag here would let the version S19 asserts drift underneath the test.

### Ruling 2 — the fragmentation test's credential exception, stated in code

`SequentialGuidFragmentationTests`' XML documentation now carries the exception in full: it runs under the **migrator** credential, not `ycr_app`, because it creates measurement tables and reads `sys.dm_db_index_physical_stats`, which needs `VIEW DATABASE STATE`. Granting that to `ycr_app` would widen the very role spec E7 and ADR-0017 item 3 exist to keep narrow, and `DatabasePrivilegeTests` asserts that role holds nothing beyond its four grants. It is an infrastructure characterisation test, not an application-path test, and it is the single documented exception in the plan's §Test fixture table.

### Ruling 2 — the measured fragmentation

Measured on the pinned 2022 image, 2026-09-21:

| | |
|---|---|
| **Rows** | 10,000 |
| **Database compatibility level** | **160** (SQL Server 2022) |
| **Our generator** — index `PK_GeneratedIds` | **1.79 %** |
| **`NEWSEQUENTIALID()` baseline** — index `PK_BaselineIds` | **1.79 %** |
| **Delta** | **0.00 points** |
| **ADR-0006 budget** | 10.00 points |

`SqlServerSequentialGuidIdGenerator` matches SQL Server's own sequential generator exactly, to two decimal places.

### The measurement was suspicious, so it was checked

Two numbers identical to two decimals is the right answer here — and it is also precisely what a measurement that always returned the same value would look like. A control that cannot fail is not a control.

So the same instrument was fed a known-bad input: `Guid.NewGuid()`, through the same table shape, insert loop and DMV query.

| | |
|---|---|
| **Random GUIDs** — index `PK_RandomIds` | **97.33 %** |
| **Baseline in the same run** — `PK_RandomBaselineIds` | **1.79 %** |
| **Delta** | **95.55 points**, far outside the 10-point budget |

`Insert10000RandomGuids_FragmentsFarWorseThanBaseline_ProvingTheMeasurementWorks` asserts that, and its failure message says what a pass would mean: the control proves nothing. This is the negative case `docs/21` requires for architecture rules, applied to a performance control for the same reason.

### S19 — the guard against a real 2019 server

`LedgerGuardTests` runs the **migration bundle** against the pinned 2019 container and asserts the failure is ours: it contains `YCR requires SQL Server 2022`, `ADR-0017`, `Detected version 15.` and the edition, and **not** a SQL Server parse error about `LEDGER`, which would mean the guard ran too late to matter.

A second test asserts the rejected server is left with **no `audit` schema and no `AuditEvents` table** — the guard runs before any DDL, so nothing partial survives.

**What S19 still does not cover, stated in the test itself:** the guard's **edition** branch. No readily available container runs a 2022 edition without ledger support. That branch is covered by V1 at step 8, which showed the pinned image's Developer edition does create a ledger table, and by code review.

### Trunk-only: excluded, not skipped

Both controls are `[Trait("Category", "TrunkOnly")]`. `tests/Directory.Build.props` excludes that trait from the default run, so a branch build still reports **`skipped: 0`** — S15 requires no skipped tests, and marking them `Skip` would have broken it while looking like compliance.

- Branch run: `dotnet test YCR.sln` → **147 passed, 0 failed, 0 skipped**.
- Trunk run: the filter-trait option with `Category=TrunkOnly` → **4 passed, 0 failed, 0 skipped**.
- Locally: `YCR_RUN_TRUNK_ONLY_TESTS=1` lifts the exclusion.

151 tests exist; the two runs are complements, so every one of them runs somewhere.

### Ruling 3 — the step-13 CI smoke job, recorded

`plan.md` §Steps step 13 now requires a smoke job **against the built API and a compose database**, asserting `/health/live` 200, `/health/ready` 200, `/openapi/v1.json` **not served** outside Development, and `GET /api/v1/stations` 401 carrying an `errorCode`. The plan states why it runs against the real artifact rather than the test host: it is the check that would have caught the step-11 defect. Step 13 also now records that a trunk job must run the `TrunkOnly` complement.

### Evidence

- `dotnet build YCR.sln` — **0 warnings, 0 errors**.
- `dotnet test YCR.sln --no-build` — **total 147, failed 0, succeeded 147, skipped 0**.
- Trunk-only run — **total 4, failed 0, succeeded 4, skipped 0**, in 2m31s.
- Measurements above captured from the run's own CTRF report, not retyped from memory.
- An XML comment containing the `--filter` option text broke every test project's build (`MSB4024`: an XML comment cannot contain `--`). Caught by the build, reworded, not worked around.

**Next step (exact):** plan step 13 — `.github/workflows/ci.yml`: restore, build, test against the pinned images, gitleaks, the bundle built once and passed in `YCR_MIGRATION_BUNDLE`, `Current Language=us_english` on every connection string, the API smoke job above, and a trunk job for the `TrunkOnly` complement. Push `feature/F-001` and **prove the run green on `origin`**, recording the run URL here.

**Blockers / open questions:** none technical. OQ26, OQ27 and OQ28 remain open with Myanma Railways; T-014 is the release gate.

**State of the branch:** build and tests green; committed and pushed; this progress entry is the only uncommitted change.

**Review split so far:** steps 1, 7, 8, 9, 10, 11 and 12 implemented by claude; steps 2–6 and the step-5 review fixes by Codex.

---

## 2026-09-21 05:30 Asia/Yangon — claude — T-004

**Stage:** 4 (IMPLEMENT). **Step 13 of 13 complete — stage 4 finished.**
**Commit:** `cfd6cc5` (`ci(F-001): add the CI workflow`), pushed to `origin/feature/F-001`.
**Implemented by:** claude.

### The workflow

`.github/workflows/ci.yml`, four jobs:

| Job | What it does |
|---|---|
| **build-and-test** | Restore, build, build the migration bundle **once**, run the whole suite under `YCR_MIGRATION_BUNDLE` |
| **api-smoke** | The **built API** against a compose database, in `Production` |
| **trunk-only-tests** | The `Category=TrunkOnly` complement, on `pull_request` into `main` **and** on push to `main` |
| **secret-scan** | gitleaks over full history (S26) |

### Hein's additions of 2026-09-21, each applied

**1. Trunk-only on PR *and* push to main.** The job's condition is
`github.event_name == 'pull_request' || github.ref == 'refs/heads/main'`. Running them only after merge would mean the first anyone learns the ledger guard or the fragmentation control broke is when `main` is already red. The feature-branch push run confirms the other half: the job reports **skipped** there, which is what keeps every branch build off a second multi-gigabyte image pull.

**2. Actions pinned by commit SHA, permissions minimal.** All four third-party actions are pinned to a commit, with the tag kept only as a trailing comment:

| Action | Commit |
|---|---|
| `actions/checkout` | `11d5960a326750d5838078e36cf38b85af677262` |
| `actions/setup-dotnet` | `67a3573c9a986a3f9c594539f4ab511d57bb3ce9` |
| `actions/upload-artifact` | `ea165f8d65b6e75b540449e92b4886f43607fa02` |
| `gitleaks/gitleaks-action` | `ff98106e4c7b2bc287b24eaf42907196329070c7` |

`gitleaks-action@v2` resolved to an **annotated tag**, so the tag object had to be dereferenced to the commit it points at; pinning the tag object's own SHA would not have been a commit pin at all. Workflow permissions are `contents: read`, with nothing raised per job.

**3. Credentials generated per run.** The `sa`, migrator and application passwords are generated with `openssl rand -hex 16` inside the job and written only to `$GITHUB_ENV`. **None is a repository secret and none is written to a file**, so there is nothing to leak between runs and nothing for a fork to exfiltrate.

**4. `.gitattributes`.** `*.sh` and `*.sql` are forced to LF in the working tree as well as the repository — a shebang ending in CR makes the Linux kernel look for an interpreter named `/bin/bash\r`, and a stray CR inside a sqlcmd variable travels silently into the database. `docker-compose*.yml` and the workflow files are covered for the same reason.

**Verified on Windows after renormalising**, which is the half that could have broken quietly: `git ls-files --eol` reports `i/lf w/lf` for `init-principals.sql` and `docker-compose.yml` even though `core.autocrlf=true`, and a fresh `docker compose up -d` then ran the init script successfully — *"Changed database context to 'YCR' … YCR principals ready."*

**5. Artifacts on failure.** TRX results and container logs are uploaded for both test jobs, and API plus compose logs for the smoke job. All guarded by `if: failure()`, so a green run uploads nothing.

### The smoke job, and why it runs against the built artifact

It hosts `YCR.Api.dll` directly — not `dotnet run`, which would background a launcher whose PID is not the API's — against a compose database, under `ASPNETCORE_ENVIRONMENT=Production`. Four assertions:

| Assertion | Result |
|---|---|
| `/health/live` → `200` | **PASS** |
| `/health/ready` → `200` | **PASS** |
| `/openapi/v1.json` **not served** outside Development | **PASS** |
| `GET /api/v1/stations` → `401` carrying `errorCode` `Common.Unauthenticated` | **PASS** |

This is the check that would have caught the step-11 defect. Every API test registers the test authentication handler through `ConfigureTestServices`, so the suite is structurally blind to the shape that actually ships; an unauthenticated request returned `500` instead of `401` for exactly that reason. The smoke job has no such blind spot because it runs the real artifact with the real configuration.

All four were verified locally in Production mode **before** the push, including the exact `sed` extraction the job uses — the workflow was not written hoping it would pass.

### Evidence — branch run

**Run URL: https://github.com/heinmindev/ycr/actions/runs/35562606107** — `push` on `feature/F-001` at `cfd6cc5`, **success**.

| Job | Result |
|---|---|
| Build and test | **success** — `total: 147, failed: 0, succeeded: 147, skipped: 0`, identical to the local run |
| API smoke test | **success** — all four assertions PASS, on the first attempt |
| Secret scan | **success** |
| Trunk-only tests | **skipped**, correctly: a feature-branch push is neither a PR into `main` nor a push to `main` |

CI reporting `skipped: 0` while four trunk-only tests exist is the point of excluding by trait rather than by `Skip` — S15 holds and nothing is quietly not running.

### Getting the pull_request event to fire — two separate problems

The PR ([#1](https://github.com/heinmindev/ycr/pull/1)) produced **no `pull_request` run at all** for some time, so the trunk-only job could not be proved green. Two distinct causes, found in this order:

**1. A real merge conflict.** `gh pr view` reported `mergeable=UNKNOWN`. `main` carries `9a25d48 docs: sync approved shared docs from feature/F-001`, which copied ADR-0020 onto `main` from an earlier point on this branch; step 12 then added the `AuthorizationResultHandler` follow-up to the branch's copy. `git merge-tree` confirmed an add/add conflict on that file.

Resolved by merging `origin/main` into `feature/F-001` (`6691708`). `mergeable` became `MERGEABLE` immediately afterwards.

**Which side won, checked rather than assumed** (hein's ruling, 2026-09-21 — "keep main's" applies to `TASKS.md` only):

| File | Resolution | Verification |
|---|---|---|
| `ADR-0020` | **`feature/F-001`'s version kept**, including the step-12 Follow-up bullet | Byte-identical to `f63c754`'s copy; differs from `main` by **+1 line, 0 removals**, and that one line is the `AuthorizationResultHandler` follow-up |
| `TASKS.md` | **main's content kept** — it is the live ledger | Whitespace-insensitive diff against `origin/main` is empty; only line endings differ, from the new `.gitattributes` |

The ADR check matters because the naive reading of "keep main's version" would have silently dropped the step-12 follow-up — the branch's copy is the newer one, and `main`'s is an older synced snapshot.

**2. Cause of the earlier missing runs: UNDETERMINED.** After the merge the runs appeared, at the same moment the PR was marked ready for review. Two explanations fit the evidence equally well — the draft state suppressed the event, or GitHub simply does not dispatch a `pull_request` run until it has computed the merge ref, which the conflict had prevented. The run's head SHA is the merge commit under either reading, so the timing does not separate them.

**Deliberately not investigated further** (hein, 2026-09-21): settling it would cost CI runs to answer a question that no longer blocks anything. Recorded as undetermined rather than guessed, because an earlier draft of this entry asserted the draft state *was* the cause, and that was not something the evidence supported.

The PR is left **ready for review** on hein's instruction. It is not merged by an agent under any circumstances: stages 5 to 8 have not run, and **T-009 is the human merge gate**.

**Worth a reviewer's attention:** the shared-doc conflict is a standing hazard, not a one-off. `main` receives synced copies of shared docs while feature branches keep editing them, so any shared doc touched on both sides conflicts the same way — and the symptom is a *missing* CI run rather than a failing one, which is far easier to miss than a red check.

### gitleaks needed one more read scope on pull_request events

The secret-scan job passed on `push` and **failed on `pull_request`** with `403 Resource not accessible by integration`. The log named the call: `GET /repos/heinmindev/ycr/pulls/1/commits` — on a PR event the action enumerates the PR's commits, which `contents: read` does not cover.

Fixed by granting that **one job** `pull-requests: read`, and nothing else. Deliberately `read`: the action would also post PR comments, which needs `write`, so `GITLEAKS_ENABLE_COMMENTS: 'false'` turns that off instead of widening the token. The job's own failure is the signal that matters, and a leaked secret should not be echoed into a PR comment in any case. The workflow default stays `contents: read`.

This is the kind of thing only a real PR run surfaces — the push run had been green throughout.

### The trunk-only job failed the first time it actually ran, and the fault was mine

Its first real execution reported `error: 4`, `failed: 0`, exit code **8**. No test failed; four test *assemblies* errored.

**Cause.** Most assemblies contain no `TrunkOnly` test, so under `--filter-trait "Category=TrunkOnly"` they legitimately match nothing, and Microsoft.Testing.Platform exits 8 for "no tests ran" — four times over, failing the job. **I had only ever run the filter against `YCR.Infrastructure.Tests`, the one project that has such tests, so the solution-wide behaviour was never exercised locally.** The lesson is the same one this feature keeps relearning: run the command the pipeline runs, not a convenient subset of it.

**The first fix was wrong too, and the local re-run caught it.** Adding `--ignore-exit-code 8` to the command line made `YCR.IntegrationTests` exit **5** instead — that project already carries the same option in its own csproj (plan C4), and Microsoft.Testing.Platform rejects the duplicate rather than merging it. Confirmed directly: the same project passes without the command-line copy and exits 5 with it.

**Final shape.** Both filters now live in `tests/Directory.Build.props`, selected by `YCR_RUN_TRUNK_ONLY_TESTS`, and `YCR.IntegrationTests`' own waiver is skipped in trunk-only mode so it can never double. The CI job sets the variable and passes no filter at all, which makes the collision impossible to reintroduce from the pipeline side.

**The waiver is not a hole.** `--ignore-exit-code 8` also covers "the filter matched nothing *anywhere*", which would let the job pass having run nothing. A following step sums `executed` across the TRX reports and fails when it is zero. Verified against the real reports: it reads **4**.

Both modes were then re-run locally with the exact CI commands — trunk-only: **4 passed, exit 0, no errored assemblies**; default: **147 passed, 0 skipped**.

### Run URLs — all jobs green

Final SHA **`8d28f40`**. Both events were proved separately, because they run different job sets.

| Run | Event | SHA | Result |
|---|---|---|---|
| [35564512468](https://github.com/heinmindev/ycr/actions/runs/35564512468) | `push` on `feature/F-001` | `8d28f40` | **success** — build-and-test, api-smoke, secret-scan green; trunk-only correctly **skipped** |
| [35564888120](https://github.com/heinmindev/ycr/actions/runs/35564888120) | `pull_request` → `main` ([PR #1](https://github.com/heinmindev/ycr/pull/1)) | `8d28f40` | **success** — **all four jobs green, trunk-only included** |
| [35562606107](https://github.com/heinmindev/ycr/actions/runs/35562606107) | `push` on `feature/F-001` | `cfd6cc5` | success — the first green branch run, kept for the record |

**Trunk-only job evidence** (run 35564888120): `total: 4, failed: 0, succeeded: 4, skipped: 0`, and the guard step reported `Trunk-only tests executed: 4`. The S19 ledger guard and the S23 fragmentation control, including its negative case, therefore ran against real containers in CI rather than only on this machine.

**Branch-run evidence** (run 35564512468): `total: 147, failed: 0, succeeded: 147, skipped: 0` — identical to the local run, with the four trunk-only tests excluded by trait rather than skipped, so S15's "no skipped tests" holds.

The two runs are complements: 147 + 4 = every test in the solution, each executed in exactly one of them.

**Note on dispatch timing.** The `pull_request` run for `8d28f40` appeared several minutes after the `push` run, while `gh pr view` reported `mergeStateStatus=UNKNOWN`; it dispatched once that resolved to `CLEAN`. The same lag preceded the earlier "missing" runs, which is consistent with GitHub simply not dispatching a `pull_request` run until it has computed the merge ref — but, per hein's ruling, the earlier case stays recorded as **cause undetermined** rather than being asserted from this later observation.

### Stage 4 is complete

All thirteen plan steps are done, every VERIFY is resolved, and both CI events are green on `8d28f40`. T-004 moves to `review` with that SHA, per `TASKS.md` §Protocol item 10. It becomes `done` only when T-009 — the human approval and merge — is done.

**PR #1 is left open and ready for review, and is not merged by an agent.** Stages 5 to 8 have not run: T-005 scenario tests, T-006 code review and T-007 security review must each be taken by an agent **other than claude** (§Protocol item 11), since claude implemented steps 1 and 7 to 13. Codex implemented steps 2 to 6 and the step-5 review fixes, so neither implementer can review their own work — the reviews need to be split by author, or taken by a third party.

---

## 2026-09-21 16:05 Asia/Rangoon — codex — T-005

**Stage:** 5 (SCENARIO TEST REVIEW). **Commit reviewed:** `9106d532ee4a10729748e6040c1290f03dd6ec7c`.

The full S1-S27 matrix was checked against the tests and the recorded CI runs. The solution build
passed locally; Domain (30/30) and Architecture (12/12) test projects passed locally. The complete
container-backed suite could not run on this workstation because Docker is unavailable, so the review
uses the recorded green CI evidence (147 default tests plus 4 trunk-only tests, zero skipped, runs
35564512468 and 35564888120).

One coverage finding is recorded in `docs/features/F-001-walking-skeleton/review-codex.md`: S9 does not
have separate cases for an exact 11-character code, an embedded-space code, and the API blank-code
case. No production code was changed.

---

## 2026-09-21 16:35 Asia/Rangoon — codex — T-006a

**Stage:** 6a (CODE REVIEW, claude scope). **Commit reviewed:** `9106d532ee4a10729748e6040c1290f03dd6ec7c`.

Reviewed step 1 and steps 7-13 only, excluding Codex's steps 2-6 and step-5 review fixes. The report
records two findings: a High finding that the Testcontainers fixture labels the SA connection as the
migrator connection and therefore does not test the required credential separation, and a Low CI path
ignore mismatch for feature progress files. No production code was changed. The report remains Not ready
pending T-008 remediation and the dedicated T-007a security pass.

---

## 2026-09-21 09:40 Asia/Yangon — claude — T-006b and T-007b

**Stage:** 6b and 7b (REVIEW and SECURITY REVIEW) — the **codex half** of stages 6 and 7, split by author under `TASKS.md` §Protocol item 11.
**Commit:** this entry and `docs/features/F-001-walking-skeleton/review-claude.md`, on `feature/F-001`.

**Done this session:**
- Added ledger rows **T-006b** and **T-007b** in the coordination checkout (`e622cd6`, `chore(tasks): claim T-006b`), locked with `claim/T-006b` / `claim/T-007b`, and pushed `main`. T-006 and T-007 are untouched and stay open for claude's steps 1 and 7–13.
- Reviewed **only codex's commits** — plan steps 2–6 and the step-5 review fixes, `0572ff3..89551cb` — as they stand **at `9106d53`**, with claude's later edits to the same files excluded file-section by file-section.
- Wrote `review-claude.md` from `docs/templates/review-report.md`: scope and commit table, Definition-of-Done check, production-composition check, twelve findings, security review, verdict.
- **No code was changed.** Remediation is T-008's, and belongs to the implementer.

**Evidence:**
- `dotnet build YCR.sln` — **succeeded, 0 warnings, 0 errors** (12.06 s), under `TreatWarningsAsErrors`.
- `dotnet test tests/YCR.Domain.Tests` — **30 passed, 0 failed, 0 skipped**.
- `dotnet test tests/YCR.ArchitectureTests` — **12 passed, 0 failed, 0 skipped**.
- `dotnet test tests/YCR.Infrastructure.Tests --filter-class …StationModelTests --filter-class …ModuleInterfacesTests` — **3 passed, 0 failed, 0 skipped** (34.2 s, against the pinned SQL Server 2022 container).
- `grep` over `src/` and `tests/` (excluding `obj/`) found no `[Skip]`, no `#pragma warning disable` and no credential in any file in scope.
- Whole-suite evidence is CI's at `9106d53`, cited from T-004 Notes rather than re-run.

**Verdict: Ready. No blocking finding, and no open Critical or High security finding.** Twelve non-blocking findings, C-1 to C-12. C-9 (the original `IAuditWriter` shape) and C-10 (the S17 test that never opened its connection) were **already fixed** by later commits and need no action. The remaining ten are T-008's; **C-1 — nothing enforces that `CreatedAtUtc` is actually UTC** — is the one worth doing before the reference slice is copied, and **C-11 — R4's "never Zawgyi" half has no control** — needs an OPEN QUESTION in `docs/19`, not code.

**Next step (exact):** T-008 remediates C-1 to C-8, C-11 and C-12 after **T-006 and T-007** have also run over claude's steps 1 and 7–13. Those two still need an agent that is neither claude nor codex, or a further split by author. **T-005 (scenario tests) has not run at all** and this review deliberately does not stand in for it: nothing here asserts that spec §4's S1–S27 are covered.

**Blockers / open questions:** none technical. OQ26, OQ27 and OQ28 remain open with Myanma Railways; C-11 proposes one more open question about Zawgyi input handling, for T-008 to raise and T-014 to absorb.

**State of the branch:** builds and tests green; review and this entry committed and pushed; no uncommitted work. PR #1 remains open and unmerged — T-009 is the human gate.

---

## 2026-09-21 17:05 Asia/Rangoon — codex — T-007a

**Stage:** 7a (SECURITY REVIEW, claude scope). **Commit reviewed:** `9106d532ee4a10729748e6040c1290f03dd6ec7c`.

Reviewed the threat categories in `docs/18-threat-model.md` and `docs/prompts/security-agent.md` against
claude's step 1 and steps 7-13 only. The security section of `review-codex.md` records the High
credential-separation finding F-006A-1/S-007A-1 and a Medium CI/test process-argument secret-handling
finding S-007A-2. No code was changed; the verdict is Not ready pending T-008 remediation and reruns.


---

## 2026-09-21 19:40 Asia/Yangon — codex — T-008b

**Stage:** 8b (DOCUMENT / REMEDIATION). **Baseline checkpoint before fixes.**
**Commit:** `0f66e06` (feature/F-001 starting SHA).

**Done this session:**
- Pulled `main`, claimed T-008b at ledger commit `fb76ff8`, and pushed the claim to `origin/main`.
- Verified the linked worktree `D:\\MR\\ycr-F-001` is clean on `feature/F-001`.
- Read the approved feature plan, review findings, and hein's T-008b rulings.
- Confirmed Docker is unavailable locally; container-backed verification is deferred to CI.

**Evidence:** `dotnet build YCR.sln --no-restore` passed with 0 warnings and 0 errors; `dotnet test tests/YCR.Domain.Tests/YCR.Domain.Tests.csproj --no-build` passed 30/30 with 0 skipped; `dotnet test tests/YCR.ArchitectureTests/YCR.ArchitectureTests.csproj --no-build` passed 12/12 with 0 skipped.

**Next step (exact):** Add the C-1 failing domain test and implement the ruled UTC rejection plus database constraint.

**Blockers / open questions:** Docker-backed integration tests are deferred to CI; OQ29 will be added for C-11 exactly as instructed.

**State of the branch:** build and unit/architecture tests green; progress entry is uncommitted.


---

## 2026-09-21 19:55 Asia/Yangon — codex — T-008b

**Stage:** 8b (C-1 checkpoint).
**Commit:** `700500a` — UTC station timestamp enforcement.

**Done this session:**
- Added the ruled `Station.Create` non-zero-offset guard and a `+06:30` domain regression test.
- Added `CK_Stations_CreatedAtUtc_Utc` to the unapplied station migration and a CI-only SQL Server insert-rejection test.
- Documented the UTC invariant in `docs/20`, ADR-0018 Consequences, and `docs/07`.

**Evidence:** `dotnet build YCR.sln --no-restore` passed 0 warnings / 0 errors; Domain tests passed 31/31; `StationModelTests` passed 2/2. The SQL Server constraint test is deferred to CI because Docker is unavailable locally.

**Next step (exact):** Add the shared-kernel architecture rule and violating fixture for C-2.

**Blockers / open questions:** none beyond the deferred Docker-backed C-1 integration assertion.

**State of the branch:** committed at `700500a`; review status for C-1 now names the fixing SHA.

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

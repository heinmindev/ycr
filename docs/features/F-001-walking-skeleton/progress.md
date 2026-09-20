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

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

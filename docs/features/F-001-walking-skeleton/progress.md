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

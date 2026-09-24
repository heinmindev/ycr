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

## 2026-09-24 20:40 Asia/Yangon — claude — T-032

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

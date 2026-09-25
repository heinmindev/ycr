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

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

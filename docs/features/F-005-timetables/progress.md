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

---

## 2026-09-26 08:22 Asia/Yangon — claude — T-053

**Stage:** 2 (SPECIFY) — rulings applied; stopped at the stage-2 ⛔ again.
**Commit:** rulings commit on `feature/F-005` (SHA recorded in the `TASKS.md` T-053 row); this entry is in the same commit.

**Done this session:**
- Applied hein's rulings of 2026-09-26 (T-053 row at `417c400`): SC → SC2 + cancel before effect; OQ51–OQ59; H; ADR-0027 Accepted (max 1439); E1–E14 accepted except E8 (one Timetable-wide ADR-0026 lock for publish, cancel and service withdrawal); E10 numbers at PLAN (≈200 services × 40 stops); C4/C5 to stage 8.
- `spec.md`: Status "Draft — all rulings applied, awaiting approval"; §0.10 rulings table (asked table kept); consequences; §0.11 raised items (OQ60, Q2); new §0.12 spelling out the F-004 withdrawal change; closure notes in §0.3/§0.4/§0.5/§0.8/§0.9; §1–§9 and Blocked behaviour rewritten for the ruled design (R1–R48, SV1–SV52).
- `docs/19`: resolution blocks under OQ51–OQ59 (F-004 wording); new **OQ60** (may a version list no services?).
- `docs/10`: `schedules.read` in the inventory; `## Schedule permission grants — resolved (OQ59)` in the service-section bullet format; intro and service-section cross-references updated.
- ADR-0027 Status Accepted (Decision items 1–3 now state minutes and 0–1439); README row Accepted.
- Not touched: glossary, MR questions pack, code, ADR-0024.

**For hein:**
- **OQ60** (blocking, R9/SV22): may a version list no services?
- **Q2** (confirmation, SV41): withdraw S from D, then cancel the version starting on D → both succeed under the rulings' letter, and the earlier version applies again listing S (which doesn't run). Confirm (a), or choose (b) refuse the cancel (and the analogous publish).
- Engineering choices made while applying (hein may amend): numbers contiguous via max+1 under the lock (R7); start ≥ today also checked at creation (R31); services re-checked at publish (R17); `runsOnDate` in the in-force read (R48); create and discard also take the lock (R46).
- Add OQ51–OQ60 to `docs/business/mr-questions-pack.md` (OQ51–OQ59 as provisionally ruled).

**Evidence:** docs only; no build or test run. `IdentitySeedTests` parses only the station/route/service headings, so the new `docs/10` section does not change today's test; stage 4 adds the heading (spec §7).

**Next step (exact):** hein rules on OQ60 and Q2 and approves the spec; then a revision (if needed) and T-053 → done; stage 3 (PLAN) is a separate task.

**Blockers / open questions:** `approval` (stage-2 ⛔); business: OQ60.

**State of the branch:** docs only; committed and pushed.

---

## 2026-09-27 05:00 Asia/Yangon — claude — T-053

**Stage:** 2 (SPECIFY) — complete; spec **Approved (hein, 2026-09-26)**.
**Commit:** approval commit on `feature/F-005` (SHA recorded in the `TASKS.md` T-053 row); this entry is in the same commit.

**Done this session:**
- Applied hein's final rulings (T-053 row at `97b5d7d`): **OQ60 → (b)**, a version may list no services (network-wide suspension; corrected by cancel-before-effect or a later version); **Q2 → (a)**, a withdrawal survives the cancellation of the version that made it legal; the five engineering choices confirmed (numbers highest+1 under the lock; past start date refused at creation; services re-checked at publish; create and discard take the Timetable-wide lock; `runsOnDate` in the in-force read).
- `spec.md`: Status "Approved (hein, 2026-09-26)"; §0.10 "Final rulings at approval" table and approval line; §0.10 consequence for an empty version; §0.11 outcome; R9 rewritten (PROVISIONAL RULING OQ60); new **R49** (Q2); the five choices relabelled ENGINEERING DECISION (tech lead, hein, 2026-09-26) in R7, R17, R31, R46, R48; SV22 now `201`; new **SV53** (publish an empty version, then in-force reads) and **SV54** (withdraw, then cancel; S stays withdrawn, `runsOnDate = false`); SV41 second order made definite; §0.12, §9, Blocked behaviour and Notes for the next stage updated. No blocking OPEN QUESTION left; OQ47 stays a known limitation.
- `docs/19`: resolution block under OQ60.
- Not touched: code, glossary, MR questions pack, `docs/10`, ADRs.

**Evidence:** docs only; no build or test run.

**Next step (exact):** T-054 — stage 3 (PLAN) against the Approved spec, in a separate session.

**Blockers / open questions:** none for F-005. Still for hein: add OQ51–OQ60 to `docs/business/mr-questions-pack.md`.

**State of the branch:** docs only; committed and pushed.

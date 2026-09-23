# Progress: F-002 Staff authentication

Append-only. Newest entry at the bottom. Never edit or delete earlier entries.

---

## 2026-09-23 15:20 Asia/Yangon — claude — T-023

**Stage:** 1–2 (DISCOVER + SPECIFY), stopped at the stage-2 ⛔
**Commit:** the commit that adds this file on `feature/F-002` (SHA recorded in `TASKS.md` T-023 Notes)
**Done this session:**
- Claimed T-023: `claim/T-023` lock, ledger claim `effe312` on `main`, pushed. Created worktree `D:\MR\ycr-F-002` on `feature/F-002` from `main` at `effe312`.
- Read AGENTS.md, TASKS.md (incl. Protocol), workflows 01/02, the discovery-agent prompt, the spec and progress templates, ADR-0005/0006/0009/0012/0016/0017/0020/0021, `docs/01`, `02`, `03`, `04`, `05`, `06`, `07`, `08`, `09`, `10`, `15`, `17`, `18`, `19`, `20`, `21`, the glossary, the MR questions pack (OQ12/OQ28), and F-001's spec as the worked example.
- Read the current auth code: `Program.cs`, `AuthenticationSchemeGuard`, `AuthorizationResultHandler`, `PermissionAuthorizationHandler`, `PermissionPolicyProvider`, `HttpContextCurrentUser`, `ICurrentUser`, `IAuditWriter`, `AuditWriter`, `Permissions`, the test handler, `AuthenticationSchemeGuardTests`, `MissingSchemeTests`, and the S21a architecture test.
- Wrote `spec.md`: §0 discovery (repository facts O1–O9, contradictions C1–C16, the ADR-0009 carry-over table, decisions D1–D18, new OQs), the rules table R1–R22, scenarios S1–S33, state changes, the API proposal (auth + an administration section blocked in full), the `identity` schema proposal, audit/logging/metrics, out of scope, the security-controls mapping (ADR-0016 required tests, `docs/18` required controls, `docs/09`, `docs/21`), and §Blocked behaviour.
- `docs/19`: added **OQ34** (staff-account governance, the first administrator's holder) and **OQ35** (mandated authentication policy); added a **BLOCKS** clause to OQ12 naming F-002. No other OQ changed.

**Evidence:** docs only. No production code, tests or migrations were written, per T-023's scope. No build or test run was needed, since nothing under `src/` or `tests/` changed. Files changed: `docs/features/F-002-staff-authentication/spec.md` (new), `docs/features/F-002-staff-authentication/progress.md` (new), `docs/19-open-questions.md`.

**Next step (exact):** wait for hein's rulings on spec §0.5 D1–D18 and answers or provisional rulings on OQ12, OQ34, OQ35. Then a stage-2 session (same branch and worktree) folds the rulings into `spec.md`, relabels the affected rules, clears the [BLOCKED] marks that were ruled on, and writes any ADR the rulings require (at least one, restating ADR-0009's account-security items; see the spec's §Notes for the next stage). Only after hein approves the spec may T-00x (PLAN) start.

**Blockers / open questions:** approval (stage-2 ⛔). Business: OQ12, OQ34, OQ35. Engineering: D1–D17 (D18 is plan-level).

**State of the branch:** committed and pushed; builds and tests unaffected (docs-only change).

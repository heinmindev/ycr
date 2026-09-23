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

---

## 2026-09-23 15:36 Asia/Yangon — claude — T-023

**Stage:** 2 (SPECIFY), rulings applied; stopped at the stage-2 ⛔
**Commit:** the commit that adds this entry on `feature/F-002` (SHA recorded in `TASKS.md` T-023 Notes)
**Done this session:**
- Pulled `main` in the coordination checkout and read hein's rulings (2026-09-23) in the T-023 row: all of the tech lead's recommendations approved; business items are provisional tech-lead rulings, not Myanma Railways answers.
- `spec.md`: Status → "Draft — rulings applied, awaiting hein's approval". §0.3 "Resolved by" and §0.4 "Decision" filled from the rulings; §0.5 gains a Ruling column (D1–D18); new §0.8 lists six points no ruling covers (U1–U6), left open rather than chosen. R1–R22 relabelled with their rulings and source (hein, 2026-09-23); new R23 (uniform 401), R24 (security headers, no CORS), R25 (MFA and SPA release gates). Every [BLOCKED Dn] mark cleared and the scenarios given exact expectations (S2–S5 uniform 401/lockout/429, S6 Origin on login, S13 lifetimes, S15 logout by `sid`, S22 ProblemDetails challenge, S30 the fourteen seeded grants, S32 bootstrap); new S19a/S19b, S26a–c, S32a. §2 actors, §5, §6 (auth + administration API with permissions), §7 data, §8 audit list, §9, §10 mapping and §Blocked behaviour updated (remaining: signing-key production storage, SPA controls under OQ22, the D4 MFA release gate, D18, and the §0.8 points).
- `docs/19`: OQ12, OQ34, OQ35 gain "Resolved by tech-lead ruling (hein, 2026-09-23; T-023) — not a Myanma Railways answer" blocks; each stays open with Myanma Railways.
- `docs/10`: canonical role names in the grant prose (C3); `stations.read` → all eight roles including `ReportingUser`; new §Identity permission grants (`users.read`, `users.manage`, `users.roles.manage`, `auth-sessions.revoke` → `SystemAdministrator` only); inventory updated. All labelled provisional tech-lead rulings.
- `docs/20`: §4 third case, `.RequireAuthorization()` with no permission only with a reason comment (D17); §2 records the `Auth.*` / `Identity.*` error-code split (D14).
- New **ADR-0023** (Accepted 2026-09-23, hein): D1–D6, D11, D12, D14, and the C5/C6/C7 clarifications of ADR-0016 (ADR-0016 not edited). Indexed in `docs/decisions/README.md`.
- **ADR-0020** amended inline for D13 (allowlist = `JwtBearerHandler`; test handler stays for non-auth tests), D14 (customised challenge, `AuthorizationResultHandler` deleted, `MissingSchemeTests` rewritten) and D15 (no-handler rule survives), T-014 pattern.

**Evidence:** docs only. No production code, tests or migrations, per T-023's scope; no build or test run needed. Files changed: `docs/features/F-002-staff-authentication/spec.md`, `docs/features/F-002-staff-authentication/progress.md`, `docs/19-open-questions.md`, `docs/10-authorization-matrix.md`, `docs/20-coding-conventions.md`, `docs/decisions/ADR-0023-staff-account-security-and-access-tokens.md` (new), `docs/decisions/ADR-0020-f001-authentication-scope-and-test-auth-handler.md`, `docs/decisions/README.md`.

**Next step (exact):** hein reviews the spec and rules on §0.8 U1–U6 (or defers named ones to PLAN), then approves at the stage-2 ⛔. Only then may a PLAN task start.

**Blockers / open questions:** approval (stage-2 ⛔). Unruled: §0.8 U1–U6. Left outside T-023's file list for hein: `docs/glossary.md` §Actor and role labels still says the role model is unapproved; `docs/business/mr-questions-pack.md` (C15, and whether OQ34/OQ35 go into it).

**State of the branch:** committed and pushed; builds and tests unaffected (docs-only change).

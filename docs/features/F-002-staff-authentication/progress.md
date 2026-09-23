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

---

## 2026-09-23 15:46 Asia/Yangon — claude — T-023

**Stage:** 2 (SPECIFY), §0.8 rulings applied; stopped at the stage-2 ⛔
**Commit:** the commit that adds this entry on `feature/F-002` (SHA recorded in `TASKS.md` T-023 Notes)
**Done this session:**
- Pulled `main` in the coordination checkout and read "Rulings on spec §0.8 U1–U6" (hein, 2026-09-23) in the T-023 row; everything else at `43ed652` accepted as written.
- `spec.md`: Status → "Draft — all rulings applied, awaiting hein's approval". §0.8 gains a Ruling column. R12 (U4 actors), R16 (U6 defaults), R18 (U3 unlock), R20 (U5 self-target, U2), R21 (U1 path-based prefixes), R22 (thirteen events, `Identity.UserUnlocked`) updated; new R26 (must-change restriction, `403 Auth.PasswordChangeRequired`) and R27 (last `SystemAdministrator`, `422 Identity.LastAdministrator`, provisional tech-lead ruling — not a Myanma Railways answer). Scenarios S4, S5, S12, S16, S17, S19a, S27 updated; new S19c (unlock), S19d (must-change), S19e (last administrator, incl. concurrency). §2, §5, §6 (unlock endpoint, must-change 403, path-based prefixes, new 422 codes), §7, §8 audit list, §10, Blocked behaviour (the §0.8 row removed) and the notes updated.
- ADR-0023 items 3 (U3), 5 (U6), 6 (U3, U4) and 9 (U1, U2) amended inline, marked "Amended 2026-09-23 (hein; T-023, U-n)"; still Accepted.
- `docs/20` §2: U1's path-based prefix rule recorded next to the D14 split.

**Evidence:** docs only; no code, tests or migrations; no build or test run needed. Files changed: `spec.md`, `progress.md`, `docs/decisions/ADR-0023-staff-account-security-and-access-tokens.md`, `docs/20-coding-conventions.md`.

**Next step (exact):** hein reviews and approves the spec at the stage-2 ⛔. Only then may a PLAN task start.

**Blockers / open questions:** approval (stage-2 ⛔). Implementer choices for hein to confirm: `Identity.CannotDisableOwnAccount` and `Identity.CannotResetOwnPassword` (U5 names no code for the self-target refusals); `/auth-sessions/*` and `/roles` use `Identity.*` under D14 (U1 names only `/auth/*` and `/users/*`); unlocking a user who is not locked is a `204` no-op with no audit row. Not edited (outside this task's file list): `docs/10` `users.manage` row (unlock), `docs/19` OQ34 block (U5).

**State of the branch:** committed and pushed; builds and tests unaffected (docs-only change).

---

## 2026-09-23 16:10 Asia/Yangon — claude — T-023

**Stage:** 2 (SPECIFY) closed — spec Approved
**Commit:** the commit that adds this entry on `feature/F-002` (SHA recorded in `TASKS.md` T-023 Notes)
**Done this session:**
- Pulled `main` in the coordination checkout and read hein's approval in the T-023 row (spec Approved at `b28e0fe`, subject to three doc syncs; the four implementer gap-fills accepted as written).
- ADR-0021: inline "Amended 2026-09-23 (hein; T-023, U4)" notes on `ActorUserId` and `ActorRole` — also null when no user is signed in (failed sign-in, lockout, refresh-family revocation), consistent with ADR-0017 §2. Nothing else changed.
- `docs/10` §Identity permission grants: `users.manage` also allows unlock.
- `docs/19` OQ34 ruling block: U5 added (no administrator may disable or administrator-reset their own account; at least one active `SystemAdministrator` must remain).
- `spec.md` Status → "Approved (hein, 2026-09-23)".

**Evidence:** docs only; no code, tests or migrations; no build or test run needed. Files changed: `spec.md`, `progress.md`, `docs/decisions/ADR-0021-audit-event-record-shape.md`, `docs/10-authorization-matrix.md`, `docs/19-open-questions.md`.

**Next step (exact):** T-024 — stage 3 PLAN (architect-agent) on the approved spec.

**Blockers / open questions:** none for T-023. Still outside any task's file list: `docs/glossary.md` §Actor and role labels; `docs/business/mr-questions-pack.md` (C15; OQ34/OQ35).

**State of the branch:** committed and pushed; builds and tests unaffected (docs-only change).

---

## 2026-09-23 17:30 Asia/Yangon — claude — T-024

**Stage:** 3 (PLAN), stopped at the stage-3 ⛔
**Commit:** the commit that adds `plan.md` on `feature/F-002` (SHA recorded in `TASKS.md` T-024 Notes)
**Done this session:**
- Claimed T-024 (`claim/T-024`, ledger claim on `main`, pushed).
- Read the architect-agent prompt, the plan template, the approved spec, ADR-0016, ADR-0020 (as amended), ADR-0021 (as amended), ADR-0022, ADR-0023, ADR-0012/0017 decisions, `docs/20`, `docs/21`, F-001's plan, and the current auth, audit, persistence, Worker, test-fixture and CI code.
- Verified spec O6 in a scratch app outside the repository (packages at 10.0.12; installed runtime 10.0.10): `AddIdentityCore` registers no scheme; JwtBearer 10.0.12 → IdentityModel 8.19.2; `UserOnlyStore` over-reports capabilities; a hand-written store over a POCO works; PasswordHasher V3 = PBKDF2-SHA512 100k; ES256 token shape and rejections as expected.
- Wrote `plan.md`: P1–P15, verifications V1–V8, files, three migrations with grants, endpoint inventory, test plan mapped to S1–S33/S19a–e and ADR-0016's required tests, two new packages, risks, twelve steps.
- Found at PLAN: `PermissionPolicyProvider` rejects `users.roles.manage` and `auth-sessions.revoke` (fixed in plan step 1).

**Evidence:** docs only; no code, tests or migrations; no build or test run needed. Files changed: `plan.md` (new), `progress.md`.

**Next step (exact):** hein reviews the plan, rules on spec gaps **G1** (subject of `Identity.RefreshFamilyRevoked`) and **G2** (repeat disable/enable), confirms or overturns P1–P15 and N1, then approves at the stage-3 ⛔. Only then may the implementation task start.

**Blockers / open questions:** approval (stage-3 ⛔); spec gaps G1, G2; confirmation N1.

**State of the branch:** committed and pushed; builds and tests unaffected (docs-only change).

---

## 2026-09-23 17:10 Asia/Yangon — claude — T-025 step 1

**Stage:** 4 (IMPLEMENT), plan step 1 of 12
**Commit:** `8a11a8c` on `feature/F-002` (after merging `main` at `e2c5e44`, which claims T-025)
**Done this session:**
- Setup: claimed T-025 on `main` (`claim/T-025`, `e2c5e44`); merged `origin/main` into `feature/F-002` (`4af917b`, TASKS.md only).
- Baseline before step 1: `dotnet test YCR.sln` 178/178.
- Pinned `Microsoft.AspNetCore.Authentication.JwtBearer` and `Microsoft.Extensions.Identity.Core` 10.0.12; referenced from `YCR.Api` and `YCR.Infrastructure`.
- `Permissions`: `UsersRead`, `UsersManage`, `UsersRolesManage`, `AuthSessionsRevoke`; stale "no grant is seeded" comment corrected.
- `PermissionPolicyProvider`: two or more `.`-separated `[a-z]+(-[a-z]+)*` segments (RED first: `users.roles.manage` and `auth-sessions.revoke` failed, then green).
- `YCR.TestSupport`: `TestClock`, `TestSigningKey` (with their own tests in `YCR.Infrastructure.Tests/TestSupport`).
- **V1 passed:** `AddInfrastructure_RegistersNoAuthenticationScheme` — `AddAuthentication` + `AddInfrastructure` + `AddIdentityCore` → no scheme, no default scheme. Until step 4 puts `AddIdentityCore<StaffUser>` inside `AddInfrastructure`, the test calls `AddIdentityCore<ProbeUser>` itself.
- **V2 failed → plan fallback applied.** In a project that references the ASP.NET Core shared framework (as `YCR.Api` does), the SDK resolves `Microsoft.Extensions.Identity.Core` to the framework's copy: **10.0.10+f7d90799** on this machine; `YCR.Api`'s `bin` has no app-local Identity dll. `YCR.Worker` (no shared framework) does get the app-local 10.0.12 package. Risk moves to R-9 as the plan says.

**Deviations from the plan:**
- V2 test renamed `IdentityCore_LoadedAssembly_Is10_0_12` → `IdentityCore_LoadedAssembly_ComesFromTheAspNetCoreRuntime` (plan V2 fallback: record the version). It asserts what does hold: Identity is 10.0.x and the same runtime build and directory as `Microsoft.AspNetCore.Authentication`, so the API never mixes versions. P13's reason "stops the Identity version following the installed runtime" holds for the Worker only; comments in the csproj and `Directory.Packages.props` say so. Stage 8 should note this in the plan's P13/R-9 history.
- `YCR.Infrastructure.Tests` gains `<FrameworkReference Include="Microsoft.AspNetCore.App" />` (test project only) so V1 can call `AddAuthentication` and V2 runs in the API's shape.

**Evidence:** `dotnet test YCR.sln` → **202/202 passed, 0 skipped** (was 178; +17 `PermissionPolicyProviderTests`, +2 `IdentityCoreRegistrationTests`, +5 TestClock/TestSigningKey). Docker Desktop 29.8.0.

**Next step (exact):** plan step 2 — `YCR.Domain.Identity` (`UserName`, `StaffUser` with G2 = no-op, `AuthSession`, `RefreshToken`, `RoleNames`, `AccountLockoutPolicy`, `IdentityErrors`, `RevocationReason`, `RefreshOutcome`) with the Domain test rows of plan §Test plan.

**Blockers / open questions:** none.

**State of the branch:** committed and pushed; build green; tests green.

---

## 2026-09-23 19:55 Asia/Yangon — claude — T-025 step 2

**Stage:** 4 (IMPLEMENT), plan step 2 of 12
**Commit:** `176eec5` on `feature/F-002`
**Done this session:**
- `src/YCR.Domain/Identity/`: `UserName`, `StaffUser`, `UserRole`, `Role`, `RolePermission`, `RoleNames`, `AccountLockoutPolicy`, `AuthSession`, `RefreshToken`, `RefreshOutcome`, `RevocationReason`, `IdentityErrors`.
- Domain tests (RED as compile failure first, then green): `UserNameTests`, `StaffUserTests` (incl. G2 repeat cases `Disable_WhenAlreadyDisabled_ReportsNoChange`, `Enable_WhenActive_ReportsNoChange`), `AuthSessionTests`, `RoleNamesTests`.
- **G2 applied** (hein, T-024): `Disable` → `Result<bool>` / `Enable` → `bool`, where `false` = no change (handler returns `204`, no audit, no session change).

**Deviations from the plan (all small; stated so a reviewer can check them):**
- `IdentityErrors` holds the codes the Application returns. The four pipeline codes (`Auth.Unauthenticated`, `Auth.PasswordChangeRequired`, `Auth.OriginRejected`, `Auth.TooManyRequests`) go in `YCR.Api` at steps 7–8, because the architecture rule forbids the Api to reference `YCR.Domain.Identity`. The plan said "All `Auth.*` and `Identity.*` codes".
- Added `Identity.InvalidUserName` (Validation → `400`) for `UserName.Create`; the API's validator still answers `400 Common.ValidationFailed` first (spec §6). The CLI uses this code.
- `Unlock(nowUtc)` takes the clock (the plan wrote `Unlock()`); "not locked" means `IsLockedOut(now)` is false, so an expired lockout is a no-op too.
- `ReplaceRoles(roleIds, Guid? callerId)`: null caller = the bootstrap (D10, no actor). An unchanged role set still succeeds; R22 audits every set-roles operation once.
- `StaffUser.Create` sets `NormalizedUserName` = upper-invariant (what Identity's default normalizer gives; step 4 checks the store agrees) and `PasswordChangedAtUtc` = creation time. `PasswordHash` / `SecurityStamp` are applied by the Infrastructure store through `internal` methods (`InternalsVisibleTo("YCR.Infrastructure")` already exists).
- Username is neither trimmed nor case-folded: `" hein"` and `"Hein"` are refused (S32a).

**Evidence:** `dotnet test YCR.sln` → **258/258 passed, 0 skipped** (+56 Domain tests). One earlier run failed 106 tests only because Docker Desktop had stopped (the fixture could not reach `npipe://./pipe/docker_engine`); after Docker Desktop was restarted, the same code gave 258/258.

**Next step (exact):** plan step 3 — `IIdentityDbContext`, six EF configurations, migrations `Identity_CreateIdentitySchema`, `Identity_SeedRolesAndPermissionGrants`, `Security_IdentityGrants`; V4, V7; Infrastructure migration/seed/constraint/privilege tests.

**Blockers / open questions:** none.

**State of the branch:** committed and pushed; build green; tests green.

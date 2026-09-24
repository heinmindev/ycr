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

---

## 2026-09-23 20:40 Asia/Yangon — claude — T-025 step 3

**Stage:** 4 (IMPLEMENT), plan step 3 of 12
**Commit:** `97ad32f` on `feature/F-002`
**Done this session:**
- `YCR.Application/Identity/IIdentityDbContext.cs` (`Users`, `Roles`, `AuthSessions`, `Database`, `SaveChangesAsync`) and `IdentityConstraints.cs` (index names handlers will match on). `YcrDbContext` implements it; `AddInfrastructure` registers it (scoped, same instance).
- Six configurations in `Persistence/Configurations/Identity/` matching plan §DB changes; every check constraint declared in the EF model (R-4); `has-pending-model-changes` → no changes.
- Migrations: `20260923133457_Identity_CreateIdentitySchema` (EF-built; `Down()` also drops the schema), `20260923133735_Identity_SeedRolesAndPermissionGrants` (8 roles with fixed ids `0199b3a0-0000-7000-8000-00000000000{1..8}`, 14 grants, no user; `Down()` deletes exactly those rows), `20260923133743_Security_IdentityGrants` (plan P7 grant table; `Down()` revokes).
- Tests: `IdentityMigrationTests` (upgrade from F-001's last migration with a station row; tables, 18 check constraints, 7 indexes incl. the filtered successor index, 6 FKs all NO ACTION, every `*Utc` is `datetimeoffset(3)`; model constants), `IdentitySeedTests` (8/14, docs/10 parse, `Permissions` constants, no user), `IdentityConstraintTests` (username format, permission format, UTC checks, half-set rows, S33 raw SQL, V4, V7), `DatabasePrivilegeTests` (+4 identity grant tests), `ModuleInterfacesTests` (+1).
- **V4 passed:** EF emits the `ReplacedByTokenId IS NULL` guard; the losing parallel rotation throws `DbUpdateConcurrencyException` and its successor INSERT rolls back (one successor remains). Same for racing revocations on `RevokedAtUtc`.
- **V7 passed:** lockout, disable/enable, unlock, own password change and role replacement saved through EF as `ycr_app` succeed under the column-scoped grants.

**Deviations from the plan:**
- `UserRoles`→`Users` FK uses EF `DeleteBehavior.ClientCascade`: the database FK stays `NO ACTION` (asserted), and EF deletes the orphaned `UserRole` rows it tracks when `ReplaceRoles` drops one. Plain `NoAction` makes EF throw on a severed required relationship.
- `CK_RolePermissions_Permission_Format` is `NOT LIKE N'%[^-a-z.]%'` (hyphen first). The plan's `[^a-z.-]` does not treat a trailing `-` as a literal in T-SQL and rejected `auth-sessions.revoke`; found by the seed migration failing, fixed before commit, with a new test for the constraint.
- `IIdentityDbContext` registration moved from step 4 to step 3, because step 3's `ModuleInterfaces_IdentityContext_…` test needs it.
- Added `IdentityConstraints` (Application) for the unique-index names, following `NetworkConstraints`.

**Evidence:** `dotnet test YCR.sln` → **304/304 passed, 0 skipped** (+46). `dotnet ef migrations has-pending-model-changes` → "No changes have been made to the model since the last migration."

**Next step (exact):** plan step 4 — `StaffUserStore`, `PasswordService` (dummy hash for unknown users), `PasswordPolicyValidator` (12–128 scalar values, blocklist), `CommonPasswordBlocklist` + `common-passwords.txt` + `THIRD-PARTY-NOTICES.md`, `RefreshTokenGenerator`, `SqlServerIdentityAdministratorLock`, `IAuditWriter.RecordWithoutActor`/`RecordSignIn`, DI (`AddIdentityCore<StaffUser>`); V5 (applock as `ycr_app`). Then stop for hein's first checkpoint.

**Blockers / open questions:** none.

**State of the branch:** committed and pushed; build green; tests green.

---

## 2026-09-23 21:45 Asia/Yangon — claude — T-025 step 4 (tech-lead checkpoint 1)

**Stage:** 4 (IMPLEMENT), plan step 4 of 12 — **stopped for hein's first checkpoint (after steps 1–4)**
**Commit:** `01cd7e9` on `feature/F-002`
**Done this session:**
- `YCR.Application/Identity/Abstractions/`: `IPasswordService` (`VerifyAsync`, `TrySetPasswordAsync`), `IRefreshTokenGenerator` + `RefreshTokenMaterial` (redacted `ToString`), `IIdentityAdministratorLock`.
- `IAuditWriter.RecordWithoutActor` and `RecordSignIn`; `AuditWriter` implements both (actor fields forced null / the verified user; client address and correlation id still from `ICurrentUser`). `Record` is unchanged in behaviour.
- `YCR.Infrastructure/Identity/`: `StaffUserStore` (P1: `IUserPasswordStore` + `IUserSecurityStampStore` only; no lockout store (P2); never saves; create/delete/rename unsupported), `PasswordService` (UserManager `CheckPasswordAsync` with rehash for real users; dummy V3 hash verified with the registered hasher for unknown users), `PasswordPolicyValidator` (12–128 scalar values, blocklist), `CommonPasswordBlocklist` + `common-passwords.txt`, `RefreshTokenGenerator`, `SqlServerIdentityAdministratorLock`.
- `AddInfrastructure` now calls `AddIdentityCore<StaffUser>` (allowed username characters `a-z0-9.`), removes Identity's default `PasswordValidator`, registers the services above.
- `THIRD-PARTY-NOTICES.md` (new) with the SecLists source, commit, filter program, both SHA-256s and the MIT licence; `.gitattributes` fixes the blocklist to LF. The committed blob hashes to the recorded `e1e5d002…716fb`.
- Tests: `PasswordPolicyValidatorTests` (8+), `CommonPasswordBlocklistTests` (3), `PasswordServiceTests` (counting hasher; unknown, disabled, locked and wrong-password paths each perform exactly one verification), `RefreshTokenGeneratorTests`, `AuditWriterTests` (4, ambient principal naming another user reaches no actor field), `DatabasePrivilegeTests.ApplicationCredential_CanTakeTheAdministratorApplock` (V5), `IdentityCoreRegistrationTests` updated (V1 with the real registration; UserManager reports lockout, email, claims and 2FA unsupported).
- **V1 re-proved** with `AddIdentityCore<StaffUser>` inside `AddInfrastructure`: zero schemes.
- **V5 passed:** `ycr_app` acquires `sp_getapplock` with no grant; a second transaction waits until the first commits. Without a transaction the lock throws instead of running unowned.

**Deviations from the plan:**
- **`IAuditWriter.RecordSignIn(Guid verifiedUserId, …)`**, not `RecordSignIn(StaffUser verifiedUser, …)`. With the aggregate in the signature the existing architecture rule "Common must not depend on any module" (ADR-0012; `CommonKernel_WithModuleDependency_DetectsViolation`) failed. Behaviour is unchanged; the step-5 architecture test still restricts callers to `LoginHandler`.
- **Blocklist source file:** the plan named SecLists `10-million-password-list-top-1000000.txt`; SecLists deleted it at `4ff3ff8` as a duplicate of `xato-net-10-million-passwords-1000000.txt`, which was used at commit `7ee9b278` (46,146 entries after filtering). Recorded in the notice.
- `IPasswordService.TrySetPasswordAsync` returns `bool` (the plan said "policy result"): the caller maps `false` to `Auth.PasswordRejected` or `Identity.PasswordRejected` by path (U1).
- Refresh-token hash is SHA-256 over the UTF-8 of the base64url raw token (not over the decoded bytes), so every presented value — including a malformed cookie — has a hash that simply matches no row. The test was renamed in spirit only (`Generate_Returns32RandomBytesAndTheirSha256` checks 32 decoded bytes and the SHA-256 of the raw text).
- `THIRD-PARTY-NOTICES.md` landed at step 4 (plan §Affected files put it with step 11's docs; step 4's row lists "blocklist + notice", and the hash test needs it).
- The step-4 tests were written together with the implementation, not strictly before it; the failures seen during the step were a test-helper DI validation issue and the architecture rule above, both fixed.

**Evidence:** `dotnet test YCR.sln` → **332/332 passed, 0 skipped** (+28).

**Next step (exact):** wait for hein's review of steps 1–4. On approval: plan step 5 — Application sign-in slice (`Login`, `RefreshSession`, `Logout`, `ChangeOwnPassword`, `GetCurrentUser`, `ResolveSessionPrincipal`, snapshots, `IdentityAuditSubjects` with G1 = user subject for both revocation events, telemetry), `IAccessTokenIssuer` abstraction, architecture rules for P5 and module boundaries (V6).

**Blockers / open questions:** approval (tech-lead checkpoint after step 4).

**State of the branch:** committed and pushed; build green; tests green.

---

## 2026-09-23 22:10 Asia/Yangon — hein (recorded by claude) — T-025 checkpoint 1 review

**Stage:** 4 (IMPLEMENT), tech-lead checkpoint after step 4 — **passed**
**Commit reviewed:** `01cd7e9` (checkpoint entry `5bd0672`)
**Rulings (hein, 2026-09-23):**
- Every deviation recorded for steps 1–4 is **accepted as written**: `RecordSignIn` takes the user id; `CK_RolePermissions_Permission_Format` uses `[^-a-z.]`; the xato-net blocklist source at the pinned commit; the pipeline error codes live in `YCR.Api`; `Identity.InvalidUserName`; time-aware `Unlock`; `ClientCascade` on `UserRoles`; `bool TrySetPasswordAsync`; hashing the raw token text; the Identity context registration moved to step 3. **V2's fallback is accepted.**
- **New requirement for step 5 (ADR-0023 item 3, D3):** a disabled or a locked account gets the identical `401` **and** equalised timing. `PasswordService` runs the dummy hash only for an unknown user, so `LoginHandler` must still run the full password verification for disabled and locked users before rejecting them — no early return. One test per case in the unknown-user style (proving the full hash check runs, not timing). **Neither case counts a failure towards lockout, nor unlocks, nor resets the count.**
- **G1 confirmed:** the subject of both `Identity.SessionRevoked` and `Identity.RefreshFamilyRevoked` is the user; the session id and the reason go in `AfterJson`.
- Continue with steps 5, 6 and 7 under the same rules; stop after step 7 for the next checkpoint.

---

## 2026-09-23 23:20 Asia/Yangon — claude — T-025 step 5

**Stage:** 4 (IMPLEMENT), plan step 5 of 12
**Commit:** `55bd042` on `feature/F-002`
**Done this session:**
- `YCR.Application/Identity/`: `IdentityAuditActions` (13), `IdentityAuditSubjects` (`Identity.User` only — G1), `UserAuditSnapshot`, `AuthSessionAuditSnapshot` (session id, user id, reason), `AuthSettings` (12 h / 20 s defaults, registered by `AddApplication` with `TryAdd`), `IdentityTelemetry` (meter `YCR.Identity`, the four `Auth*` counters, the cache-age histogram, `LoggerMessage` events with no usernames), `IdentityQueries` (role names in catalogue order + permission union), `IdentityRetry` (optimistic-concurrency retry, clears the change tracker incl. pending audit rows), `Abstractions/IAccessTokenIssuer`.
- Handlers: `LoginHandler`, `RefreshSessionHandler`, `LogoutHandler`, `ChangeOwnPasswordHandler`, `GetCurrentUserHandler`, `ResolveSessionPrincipalHandler`. `IIdentityDbContext` gains `ChangeTracker`.
- **hein's step-5 requirement:** `LoginHandler` runs `IPasswordService.VerifyAsync` once, before looking at the account's state, on every path (null → dummy hash; disabled/locked → real hash). Disabled or locked: `LoginFailed` (subject the user), uniform `Auth.InvalidCredentials`, count and lockout untouched. Tests `Handle_WithDisabledUserAndCorrectPassword_…`, `Handle_WithLockedUserAndCorrectPassword_…` and the two wrong-password variants assert exactly one hasher verification and an unchanged `(AccessFailedCount, LockoutEndUtc)`. **Mutation check:** with the verification skipped for disabled/locked users, all four fail; restored.
- **G1 applied:** `RefreshFamilyRevoked` (and, in step 6, `SessionRevoked`) has subject `Identity.User` / the user id; `AfterJson` = `{"sessionId":…,"userId":…,"revocationReason":"FamilyReuse"}`.
- Tests (`YCR.Application.Tests/Identity/`): `LoginHandlerTests` (15), `RefreshSessionHandlerTests` (8, incl. parallel same-token → one 200 + one 409 and S29's exactly-two-rows ledger), `SessionHandlerTests` (logout ×3, own password ×5, resolve ×5, me, S31 secrets at rest); `DependencyInjectionTests` now names the 10 handlers.
- Architecture (`YCR.ArchitectureTests`): `RecordSignIn_CalledOutsideLoginHandler_IsDetected`, `IdentityApplication_DependingOnAnotherModuleContext_IsDetected`, `NetworkApplication_DependingOnIdentityContext_IsDetected`, `Application_DependingOnAspNetIdentity_IsDetected`, with fixtures `RecordSignInOutsideLoginHandler`, `IdentityModuleBoundaryViolations` (two), `ApplicationUsingAspNetIdentity`.
- **V6 passed:** ArchUnitNET 0.13.4 `NotCallAny(MethodMembers().That().HaveNameStartingWith("RecordSignIn("))` sees the call inside `LoginHandler`'s async state machine — the test asserts the unexempted rule flags `LoginHandler` on the source, then that the exempted rule passes on the source and flags the fixture. No fallback needed.

**Deviations from the plan / decisions made inside the plan's latitude:**
- Username lookup at sign-in matches the upper-invariant normalized name (ASP.NET Core Identity's behaviour, D1), so `Hein.Min` signs in `hein.min`. Values over 50 characters are not looked up (they cannot match); the dummy verification still runs. Test `Handle_UserNameInOtherCase_MatchesTheNormalizedName`.
- `IdentityRetry.MaxAttempts` = 10 with 1–10·n ms jitter: with 5 attempts, 6 parallel wrong passwords exhausted the retries (seen once); stable over 3 repeated runs after the change.
- `ChangeOwnPassword`: the current password is checked before the new one's policy; a wrong current password does not count towards lockout (spec S16 "nothing changed").
- `LockedOut` carries `AfterJson` = `UserAuditSnapshot` (username, roles, locked-until); `LoginFailed` has no payload. `LoginSucceeded` carries the new session's `AuthSessionAuditSnapshot`; `PasswordChanged` before/after `UserAuditSnapshot`.
- Logout of a session that is not the caller's, or already revoked, is a silent no-op (unreachable through the pipeline, which only admits the caller's active `sid`).
- `ResolveSessionPrincipalHandler` uses two queries (session⋈user, then role grants), not one. A locked user's existing sessions stay valid (lockout guards password guessing; spec does not revoke on lockout).
- `IdentityAuditSubjects.AuthSession` (plan) not created: G1 made it unused.
- `GetCurrentUserHandler` returns `null` (endpoint → 401) instead of an `Identity.UserNotFound` error, so no `Identity.*` code can leak onto the `/auth/*` path (U1).

**Evidence:** `dotnet test YCR.sln` → **375/375 passed, 0 skipped** (+43).

**Next step (exact):** plan step 6 — administration handlers (`ListUsers`, `GetUser`, `CreateUser`, `DisableUser`, `EnableUser` (G2 no-op), `UnlockUser`, `ReplaceUserRoles`, `ResetUserPassword`, `ListUserSessions`, `RevokeSession` (G1 subject), `ListRoles`, `BootstrapAdministrator`) with P14's lock for R27.

**Blockers / open questions:** none.

**State of the branch:** committed and pushed; build green; tests green.

---

## 2026-09-24 00:20 Asia/Yangon — claude — T-025 step 6

**Stage:** 4 (IMPLEMENT), plan step 6 of 12
**Commit:** `8b93f1f` on `feature/F-002`
**Done this session:**
- `YCR.Application/Identity/`: `ListUsers`, `GetUser` (`UserDto`: id, username, roles, disabled, locked-until only while a lockout is in force, created), `CreateUser`, `DisableUser`, `EnableUser`, `UnlockUser`, `ReplaceUserRoles`, `ResetUserPassword`, `ListUserSessions` (`AuthSessionDto`, newest first), `RevokeSession`, `ListRoles`, `BootstrapAdministrator`; shared `AdministratorGuard` (R27 + role-name resolution), `UserReads`, `UserRolesAuditSnapshot`.
- **G2 applied:** a repeat disable or enable → success (`204`), nothing changes (`DisabledAtUtc` kept), no audit, no session change.
- **G1 applied:** `SessionRevoked` subject = the session's user; `AfterJson` = `{"sessionId":…,"userId":…,"revocationReason":"AdministratorRevoked"}`. An already-revoked session → `204`, no second row.
- **R27 / P14:** `DisableUser` and `ReplaceUserRoles` take the `sp_getapplock` inside a transaction only when the target is an active `SystemAdministrator` (checked first, read-only), then reload the target under the lock, count other active administrators, change, audit, save, commit. The bootstrap always takes it. **Mutation check:** with the lock removed from `DisableUser`, `DisableUser_TwoAdministratorsDisablingEachOtherConcurrently_ExactlyOneSucceeds` failed 3/3; restored.
- Check order implemented (from spec §6.2 and the S-scenarios): disable — 404, own account, G2 no-op, last administrator; roles — 404, own account (with any value, S19b), unknown role, last administrator; reset — 404, own account, policy; create — username format, unknown role, policy, duplicate (pre-check + unique index).
- Tests: `AdministrationHandlerTests` (37 cases incl. both S19e concurrency tests, parallel duplicate create, bootstrap ×5 incl. two concurrent runs); `DependencyInjectionTests` names all 22 handlers.

**Deviations from the plan / decisions inside its latitude:**
- New domain errors: `Identity.UnknownRole` (400; the API validator answers `Common.ValidationFailed` first, spec §6), `Identity.InvalidPageRequest` (400; the `Network.InvalidPageRequest` pattern), `Identity.AdministratorAlreadyExists` (422; bootstrap refusal — the CLI maps any failure to a non-zero exit in step 10).
- The bootstrap refuses when **any** user holds `SystemAdministrator`, disabled or not (S32: "when any SystemAdministrator exists").
- One `UserDto` for list and view (plan named `UserDto` and `UserSummaryDto`).
- `RolesChanged` is audited for every accepted request, including one that sets the same roles (R22: one row per operation).
- `ResetUserPassword` leaves the lockout state as it is (not specified; the reset does not claim to unlock).

**Evidence:** `dotnet test YCR.sln` → **412/412 passed, 0 skipped** (+37).

**Next step (exact):** plan step 7 — API pipeline swap: `JwtBearerSetup` (P3, P11, ProblemDetails challenge), `SessionPrincipalCache` (P4), signing-key provider + `JwtAccessTokenIssuer`, `AuthOptions` validation, guard allowlist = `JwtBearerHandler`, `PasswordChangeRequiredMiddleware` (N1), `SecurityHeadersMiddleware` (P10), delete `AuthorizationResultHandler`, `YcrApiFactory` modes, `MissingSchemeTests` → `DeployedShapeTests`; V3. Then stop for the second checkpoint.

**Blockers / open questions:** none.

**State of the branch:** committed and pushed; build green; tests green.

---

## 2026-09-24 01:30 Asia/Yangon — claude — T-025 step 7 (tech-lead checkpoint 2)

**Stage:** 4 (IMPLEMENT), plan step 7 of 12 — **stopped for hein's second checkpoint (after steps 5–7)**
**Commit:** `8c4d449` on `feature/F-002`
**Done this session:**
- `YCR.Api/Common/Authentication/`: `AuthOptions` (+ `SigningOptions`, `SigningKeyEntry`) and `AuthOptionsValidator` (`ValidateOnStart`: cache TTL 1–30 s, issuer/audience set, positive lifetimes, ≥1 absolute `https` origin outside Development/Testing); `ISigningKeyProvider` / `ConfigurationSigningKeyProvider` (P-256 PKCS#8 PEM only; active `kid` must exist; outside Development/Testing refuses `DevelopmentOnly` and `dev-`/`test-` kids; messages name the kid, never key material); `JwtAccessTokenIssuer` (`sub`, `sid`, `jti`, `iss`, `aud`, `iat`, `nbf`, `exp` from `TimeProvider`); `JwtBearerSetup` (P3 principal rebuild, P11 validation, D14 challenge); `SessionPrincipalCache` (P4); `PasswordChangeRequiredMiddleware` + `AllowedWhilePasswordChangeRequiredAttribute` (R26, N1); `AuthClaims`, `AuthErrorCodes` (the four pipeline `Auth.*` codes), `AuthProblem`; `AuthenticationSetup.AddYcrAuthentication` / `ValidateSigningKeys`.
- `SecurityHeadersMiddleware` (P10; via `OnStarting`, first in the pipeline; `/scalar` and `/openapi` exempt from the CSP in Development only; `no-store` on `/api/v1/auth/*`).
- `Program.cs`: pipeline = security headers → exception handler → status pages → authentication → must-change gate → authorization; no `AddCors`. `AuthenticationSchemeGuard` allowlist = `{ JwtBearerHandler }`. **`AuthorizationResultHandler` deleted.** Doc comments of `PermissionAuthorizationHandler` / `HttpContextCurrentUser` updated.
- Tests: `YcrApiFactory` modes `TestHandler` (default; every F-001 test unchanged and green) / `RealTokens` / `Unmodified`; `StaffUserSeeder`; `DeployedShapeTests` (replaces `MissingSchemeTests`: the four station routes → `401` + `WWW-Authenticate: Bearer` + ProblemDetails `Auth.Unauthenticated` + `traceId` + security headers; garbage bearer → bare `Bearer` challenge; health stays anonymous; `Common.Unauthenticated` nowhere in `src/`; no `IAuthorizationMiddlewareResultHandler` type left); `AuthenticationSchemeGuardTests` + `Startup_WithOnlyJwtBearerOutsideTesting_Succeeds`; `SigningKeyStartupTests` (dev-only key, `dev-`/`test-` kids, no active key, P-384/RSA/garbage PEM, cache TTL 31 and 0, http origin in Production); `AccessTokenTests` (S20 exact claims, valid token grants exactly the role's permissions, S21 ten invalid-token cases, S13 expiry on the fake clock, S13 session past lifetime, R-2 forged claims grant nothing, R26 must-change → `403 Auth.PasswordChangeRequired` while `/health/live` stays `200`).
- **V3 passed:** the `TimeProvider`-driven `LifetimeValidator` is honoured by `JwtBearerHandler` (JsonWebTokenHandler path). Evidence beyond `Token_PastExp_Returns401`: the test clock sits at 2026-09-23 03:00Z while the machine clock is a day later, so with wall-clock validation every fake-clock token would already be expired and every `200` assertion would fail.

**Deviations from the plan:**
- `TestSigningKey` gained a `developmentOnly` constructor parameter (the key, not its `kid`, carries the flag); `ToConfiguration()` lost its parameter.
- The "principal cache TTL above 30 s fails startup" test moved from step 9's `RevocationLatencyTests` into step 7's `SigningKeyStartupTests` (as `PrincipalCacheTtlOutsideOneToThirtySeconds_FailsStartup`), because the validation it tests landed here. Step 9 keeps the latency tests.
- The must-change gate got a first test here (`MustChangeSession_ProtectedEndpoint_Returns403PasswordChangeRequired`); step 8 adds the full theory over every endpoint.
- `AuthOptions` carries the non-secret defaults in code (issuer/audience `YCR.Api`, 15 min, 12 h, 20 s, cache 15 s); `appsettings.json` repeats them at step 11.
- `JwtBearerOptions.RequireHttpsMetadata = true` (no metadata endpoint is used; set for clarity).

**Known consequence (as planned):** from this commit until step 12 the **CI `api-smoke` job fails**: it starts the API in `Production` with no signing key and no allowed origin (startup refuses, S26a/R9) and still expects `Common.Unauthenticated`. Plan step 12 rewrites that job (ephemeral key, origin, `Auth.Unauthenticated`, headers). `dotnet test YCR.sln` is green at every step. Recorded here so the red smoke job on steps 7–11 is not mistaken for a regression.

**Evidence:** `dotnet test YCR.sln` → **442/442 passed, 0 skipped** (+30). GitHub Actions on `feature/F-002` green for every checkpoint through step 6 (`0d58fab`).

**Next step (exact):** wait for hein's review of steps 5–7. On approval: plan step 8 — auth endpoints (`/auth/login`, `/auth/refresh`, `/auth/logout`, `/auth/me`, `/auth/password`), contracts and validators, `OriginCheckFilter`, `AuthRateLimitFilter` / `AuthRateLimiters` (U6 defaults), `RefreshCookie`; V8.

**Blockers / open questions:** approval (tech-lead checkpoint after step 7).

**State of the branch:** committed and pushed; build green; tests green.

---

## 2026-09-24 09:00 Asia/Yangon — hein (recorded by claude) — T-025 checkpoint 2 review

**Stage:** 4 (IMPLEMENT), tech-lead checkpoint after step 7 — **passed**
**Commit reviewed:** `8c4d449` (checkpoint entry `dea0816`)
**Rulings (hein, 2026-09-24):**
- The decisions and deviations recorded for steps 5–7 are **accepted as written**, including: case-insensitive username matching at sign-in; `IdentityRetry` limit 10 with jitter; the current password checked before the new password's policy on own password change, with no lockout count; a locked account's existing sessions stay valid; `/auth/me` answering `401` rather than an `Identity.*` code; the new codes `Identity.UnknownRole`, `Identity.InvalidPageRequest`, `Identity.AdministratorAlreadyExists`; the bootstrap refusing when any user (even disabled) holds `SystemAdministrator`; auditing every accepted role change (same set included); an administrator reset leaving the lockout state unchanged.
- The CI `api-smoke` job staying red from step 7 until step 12 is accepted. **No PR before step 12 is green on GitHub Actions.**
- Continue with steps 8–12 under the same rules (green `dotnet test` after each step; commit, push and a `progress.md` checkpoint after each). V8 at step 8. Stop after step 12 with the final SHA, the green Actions run URL, the final test totals and every deviation; then set T-025 to `review` as the plan says.

---

## 2026-09-24 10:30 Asia/Yangon — claude — T-025 step 8

**Stage:** 4 (IMPLEMENT), plan step 8 of 12
**Commit:** `3626cfb` on `feature/F-002`
**Done this session:**
- `YCR.Api/Endpoints/Identity/AuthEndpoints.cs`: `POST /auth/login` (anonymous), `POST /auth/refresh` (anonymous, cookie read inside the endpoint), `POST /auth/logout`, `GET /auth/me`, `POST /auth/password` (self-service `.RequireAuthorization()` with reason comments, all three marked `AllowedWhilePasswordChangeRequired`). Filter order on the cookie endpoints: Origin → rate limit → validation → handler (P6).
- `OriginCheckFilter` (R9; exact scheme/host/port against `Auth:AllowedOrigins`, host case-insensitive; missing, repeated, `null`, other port/scheme, trailing path, look-alike host → `403 Auth.OriginRejected`).
- `AuthRateLimiters` + `AuthRateLimitFilter` (`LoginRateLimitFilter`: per client address, then per normalized username; `RefreshRateLimitFilter`: per client address). Sliding window 1 min / 6 segments, no queue; `Auth:RateLimits:*` with U6 defaults 5 / 20 / 30, validated positive at start → `429 Auth.TooManyRequests`.
- `RefreshCookie` (`ycr_refresh`; `HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth/refresh`; `Expires` = session expiry; logout expires it).
- Contracts `LoginRequest` (+validator), `AccessTokenResponse`, `CurrentUserResponse`, `ChangePasswordRequest` (+validator); `AuthProblem.Result` / `AuthProblem.Unauthenticated` helpers.
- Tests (`YCR.Api.Tests`): `RealAuthApiTestBase`; `LoginEndpointTests` (7), `LockoutEndpointTests` (3), `OriginTests` (22), `RefreshEndpointTests` (11), `LogoutEndpointTests` (3), `PasswordEndpointTests` (8), `RateLimitTests` (6), `MustChangePasswordTests` (3), `AuditActorTests` (2, sign-in half), `SecretLeakTests` (1), `BrowserControlsTests` (3). `YcrApiFactory` gains a `configureServices` hook and a test-only startup filter that sets the client address from `X-Test-Client-Address` (S5); both are absent in `Unmodified` mode.
- **V8 passed:** a `WebApplicationFactory` cookie-container client on `https://localhost` stores the `Secure; SameSite=Strict; Path=/api/v1/auth/refresh` cookie and sends it back to `/auth/refresh` twice in a row (`Login_CookieContainerClient_SendsRefreshCookieBackToRefresh`). The container checks `Expires` against wall-clock time, so `RealAuthApiTestBase` starts its `TestClock` at the current second; tests that move the clock send the cookie explicitly.
- **Mutation check:** with the login `OriginCheckFilter`, the `LoginRateLimitFilter` and the password-change cache eviction removed, exactly the 12 expected tests failed (7 origin cases, 4 login-limit tests, the must-change release test); restored.

**Deviations from the plan / decisions inside its latitude:**
- **Cache eviction on this instance:** a successful logout or own password change calls `SessionPrincipalCache.Evict(sid)` for the caller's session. Logout then answers `401` at once on the serving instance (spec §6.1 "a second call on a revoked session gets `401`"), and a must-change session is released at once after `POST /auth/password` (S19d). Other instances still converge within the TTL (R3); other sessions revoked by a password change are not evicted (their ids are not in hand) and follow the TTL.
- Login limit order: the per-address partition is tried first; a request refused per address consumes no per-username permit, one refused per username has consumed a per-address permit. Usernames longer than 50 characters share one partition (R-10).
- Validators check presence only. The login's username format is not validated, so a malformed name gets the same `401` as an unknown one (R23). An empty `newPassword` on `/auth/password` is `400 Common.ValidationFailed`; a present but non-conforming one is `400 Auth.PasswordRejected` (one home for the policy).
- The four contracts are in one file, `Contracts/Identity/AuthContracts.cs` (plan listed one file each); `AuthRateLimitFilter` is an abstract base with `LoginRateLimitFilter` and `RefreshRateLimitFilter`.
- `MustChangeSession_MayCallOnlyMeRefreshLogoutAndPassword` is a single test that enumerates every authorization-requiring route from the running endpoint table rather than a hand-written theory, so step 9's endpoints are covered without editing it. `AdministratorCreatedAndResetPasswords_AreMustChange`, the unlock half of `LockoutEndpointTests` and the administration half of `AuditActorTests` need the step-9 endpoints and land there.
- Added beyond the plan's rows: `Login_AllowedOriginInOtherCase_IsAccepted`, `Login_NineFailuresThenSuccess_ResetsTheCount`, `Login_UnknownUserTenTimes_LocksNothingAndStoresNoTypedValue`, `ChangePassword_WithInvalidBody_Returns400`, `MustChangeSession_AnonymousEndpoints_AreUnaffected`.

**Evidence:** `dotnet test YCR.sln` → **511/511 passed, 0 skipped** (+69). `dotnet build YCR.sln` 0 warnings.

**Next step (exact):** plan step 9 — administration endpoints and contracts (`/users`, `/users/{id}`, disable, enable, unlock, roles, password-reset, auth-sessions, `/auth-sessions/{id}/revoke`, `/roles`) with the administration, revocation-latency, last-administrator, wrong-permission and remaining audit-actor rows.

**Blockers / open questions:** none.

**State of the branch:** committed and pushed; build green; tests green (CI `api-smoke` red until step 12, as accepted).

---

## 2026-09-24 12:00 Asia/Yangon — claude — T-025 step 9

**Stage:** 4 (IMPLEMENT), plan step 9 of 12
**Commit:** `0f82c68` on `feature/F-002`
**Done this session:**
- `YCR.Api/Endpoints/Identity/UserEndpoints.cs`: `GET /users`, `GET /users/{id}` (`users.read`), `POST /users` (`users.manage`), `POST /users/{id}/disable|enable|unlock` (`users.manage`), `PUT /users/{id}/roles` (`users.roles.manage`), `POST /users/{id}/password-reset` (`users.manage`), `GET /users/{id}/auth-sessions` (`users.read`), `POST /auth-sessions/{id}/revoke` (`auth-sessions.revoke`), `GET /roles` (`users.read`). Thin translations; every rule stays in the handlers and the domain.
- Contracts (`Contracts/Identity/UserContracts.cs`): `CreateUserRequest`, `ReplaceUserRolesRequest`, `ResetUserPasswordRequest` (+ validators), `CreateUserResponse`, `UserResponse`, `AuthSessionResponse`, `RoleResponse`; the lists reuse the `PagedResponse<T>` envelope.
- `YCR.Application/Identity/RoleCatalogue`: the R19 identifiers for API validation, because the Api may not reference `YCR.Domain.Identity` (plan P11 allowlist).
- Tests: `UserAdministrationEndpointTests` (43 incl. theories: create 201/must-change/audit, six invalid usernames, policy, duplicate, five invalid bodies; disable/enable with both sessions revoked and G2 repeats; self-disable 422; unknown id ×7 → 404; roles with before/after audit; self roles ×3 → 422; roles invalid ×3; reset; self reset; reset policy; list paging and `Identity.InvalidPageRequest`; revoke + already-revoked; unknown session; roles catalogue; every endpoint anonymous → 401 ×11, without permission → 403 ×11, invalid body → 400 ×3), `RevocationLatencyTests` (4; the revoked-session case uses a second host over the same database as "another instance", so only the TTL bounds it), `LastAdministratorEndpointTests` (2), `WrongPermissionTests` (12), `AuditActorTests` + administration events (1), `LockoutEndpointTests` + unlock (3), `MustChangePasswordTests` + `AdministratorCreatedAndResetPasswords_AreMustChange` (1). The step-8 route-table test now covers the eleven new routes without edits.
- **Mutation check:** unlock's permission weakened to `stations.read`, and the cache's age test made inclusive (`<=`): 7 tests failed (3 unlock-permission cases, all 4 latency tests); restored.

**Deviations from the plan / decisions inside its latitude:**
- Unknown role names are refused by the request validator (`400 Common.ValidationFailed`, as the plan says) **before** the handler's self-target check, so an administrator PUTting their own roles with an unknown role name gets `400`, not `422`. S19b's "any value" is tested with valid values (`[]`, the held set, a superset). Ruling: validation precedes business rules on every endpoint (docs/20 §3); cost if wrong: one validator rule moved into the handler.
- Role names are matched ordinally (`systemadministrator` is refused) — they are identifiers, not free text.
- The username format is not checked by the validator (presence only); an invalid name gets `400 Identity.InvalidUserName` from the domain, the `Station` pattern (one home for R20).
- One endpoint file (`UserEndpoints.cs`) holds `/users`, `/auth-sessions` and `/roles`; the plan listed `UserEndpoints`, `AuthSessionEndpoints`, `RoleEndpoints`. One contracts file likewise.
- `POST /auth-sessions/{id}/revoke` evicts that session from this instance's principal cache, as logout does (step 8).
- `RealAuthApiTestBase.SignedInAsync` helper added.

**Evidence:** `dotnet test YCR.sln` → **596/596 passed, 0 skipped** (+85). `dotnet build YCR.sln` 0 warnings.

**Next step (exact):** plan step 10 — `YCR.Worker` `bootstrap-administrator` subcommand (stdin password, exit codes, no host start), `SystemCurrentUser`, `YCR.IntegrationTests` wired to TestSupport with the bootstrap CLI rows, and `ProductionCompositionTests` (S25).

**Blockers / open questions:** none.

**State of the branch:** committed and pushed; build green; tests green (CI `api-smoke` red until step 12, as accepted).

---

## 2026-09-24 13:10 Asia/Yangon — claude — T-025 step 10

**Stage:** 4 (IMPLEMENT), plan step 10 of 12
**Commit:** `f456319` on `feature/F-002`
**Done this session:**
- `YCR.Worker`: `BootstrapAdministratorCli` — `dotnet YCR.Worker.dll bootstrap-administrator --username <name>`; the password is the first line of standard input (read without echo when stdin is a terminal); any `--password…` or `-p` argument is refused; exit `0` created, `1` refused by a rule (nothing written), `2` usage error; stdout/stderr carry the account id and error codes, never the password. `AddBootstrapAdministrator(configuration)` composes `AddInfrastructure` + `AddApplication` over `ConnectionStrings:Application` with `SystemCurrentUser` (all actor fields null, one correlation id per run). `Program` dispatches the command before any host is built; with no command it is the F-001 bare host.
- `YCR.IntegrationTests`: first suite. References the Worker; the F-001 exit-code-8 waiver is removed (its note said it would be). `BootstrapAdministratorCommandTests` (16): first run (one must-change `SystemAdministrator`, one `UserCreated` row with null actor, role, permission and client address), second run exits 1 and writes nothing, 4 invalid usernames, 2 policy violations, the password in no output / Trace log / ledger row, 3 command-line password forms refused, 3 usage errors plus empty stdin, and **one real `dotnet YCR.Worker.dll` process** fed its password on a pipe (exit 0, then 1 on a second run).
- `YCR.Api.Tests/Identity/ProductionCompositionTests` (S25, 2): `Unmodified` host in `Production` with a non-test key; bootstrap via the Worker command against the same database → sign in → `403` until `/auth/password` → sign in → `GET /stations` `200`; anonymous → `401 Auth.Unauthenticated`. `YCR.Api.Tests` references the Worker for this.
- `YCR.ArchitectureTests`: the Worker assembly joins the S21a no-authentication-handler scan (plan: "now also scans `YCR.Worker`").
- **Mutation check:** removing the `--password` refusal failed 2 tests (the `-p` case is a separate check and still passed). Making `SystemCurrentUser.UserId` non-null failed nothing — an equivalent mutation: the bootstrap audits through `RecordWithoutActor`, which forces every actor field to null whatever `ICurrentUser` says (P5), so `SystemCurrentUser`'s values are defence in depth.

**Deviations from the plan / decisions inside its latitude:**
- The Worker's `Program` is an explicit `internal static class YCR.Worker.Program` instead of top-level statements, so it is not a second global `Program` beside the API's in test projects that reference both (`WebApplicationFactory<Program>` would otherwise be ambiguous).
- The command class is `BootstrapAdministratorCli` (plan: `BootstrapAdministratorCommand`), because `YCR.Application.Identity.BootstrapAdministrator.BootstrapAdministratorCommand` is the handler's command record. Its entry point is `RunAsync(args, stdin, stdout, stderr, services)` as planned.
- The CLI builds a plain `ServiceCollection` (configuration from `appsettings.json`, `appsettings.{DOTNET_ENVIRONMENT}.json`, environment variables) rather than `Host.CreateApplicationBuilder`: the host's Development-time `ValidateOnBuild` would reject `AddApplication`'s sign-in handlers, which need the API's token issuer the Worker does not have.
- Exit code `2` (usage) is added beside the plan's `0`/non-zero.
- `SqlServerFixture.cs` is at the project root (used by any future suite), not under `Bootstrap/`.

**Evidence:** `dotnet test YCR.sln` → **614/614 passed, 0 skipped** (+18). `dotnet build YCR.sln` 0 warnings.

**Next step (exact):** plan step 11 — README (dev key generation with user-secrets, bootstrap usage, Origin note), `.env.example` (`Auth__AllowedOrigins__0`, key never in `.env`), `src/YCR.Api/appsettings.json` `Auth` defaults, `src/YCR.Worker/appsettings.json` (logging), `YCR.Api.http` (login/refresh/me), `UserSecretsId` in `YCR.Api.csproj`; `THIRD-PARTY-NOTICES.md` already landed at step 4.

**Blockers / open questions:** none.

**State of the branch:** committed and pushed; build green; tests green (CI `api-smoke` red until step 12, as accepted).

---

## 2026-09-24 13:40 Asia/Yangon — claude — T-025 step 11

**Stage:** 4 (IMPLEMENT), plan step 11 of 12
**Commit:** `1b31cd1` on `feature/F-002`
**Done this session:**
- `src/YCR.Api/appsettings.json`: the non-secret `Auth` defaults — issuer/audience `YCR.Api`, 15 min / 12 h / 20 s, principal cache 15 s, rate limits 5 / 20 / 30, `AllowedOrigins: []`. No key material. (`RateLimitTests`' default-limit tests now read these values through the host's configuration.)
- `src/YCR.Worker/appsettings.json` (new): logging only; EF Core at `Warning` so the bootstrap command's console output is not SQL.
- `YCR.Api.csproj`: `UserSecretsId` for per-developer signing keys (D11), loaded in Development only.
- `.env.example`: `Auth__AllowedOrigins__0` and the rule that the signing key never goes in `.env`. `.gitignore`: `*.pem`, `*.key`.
- `README.md`: "Developer setup" — dev ES256 key with `openssl genpkey … P-256` into user-secrets (`dev-` kid, `DevelopmentOnly: true`), the allowed origin, the Origin requirement for scripts, and the bootstrap command with its exit codes.
- `YCR.Api.http`: rewritten for real tokens — sign-in, refresh (explicit cookie), me, password, logout, every administration endpoint, and the station calls with a bearer token.

**Deviations from the plan:**
- The README's key command is `openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256` (emits PKCS#8 directly) instead of the plan's `openssl ecparam … | openssl pkcs8 -topk8 -nocrypt` pipe; same key format.
- `.gitignore` gains `*.pem` / `*.key` (not in the plan's file list): a locally generated dev key cannot be committed by accident. Step 12's gitleaks rule is the second line.
- `THIRD-PARTY-NOTICES.md` needed no change (landed at step 4, accepted at checkpoint 1).

**Evidence:** `dotnet test YCR.sln` → **614/614 passed, 0 skipped** (no new tests, as planned). `dotnet build YCR.sln` 0 warnings.

**Next step (exact):** plan step 12 — CI `api-smoke` job (ephemeral ES256 key per run, masked; `Auth__AllowedOrigins__0`; expect `401` `Auth.Unauthenticated` with `WWW-Authenticate: Bearer`; security-header checks), gitleaks PEM rule if the default ruleset does not flag one; push and prove the GitHub Actions run green.

**Blockers / open questions:** none.

**State of the branch:** committed and pushed; build green; tests green (CI `api-smoke` red until step 12, as accepted).

---

## 2026-09-24 14:10 Asia/Yangon — claude — T-025 step 12 (tech-lead checkpoint 3; implementation complete)

**Stage:** 4 (IMPLEMENT), plan step 12 of 12 — **stopped for hein's third checkpoint; T-025 → `review`**
**Commit:** `b3464b6` on `feature/F-002` (CI proven green on this SHA; this entry is docs-only on top)
**Done this session:**
- `.github/workflows/ci.yml` `api-smoke`: a per-run ES256 P-256 key (`openssl genpkey`), every base64 line `::add-mask::`ed in the step that generates it, passed to the API only through the environment (`Auth__Signing__*`, kid `ci-<run id>`, `DevelopmentOnly: false`); `Auth__AllowedOrigins__0`; the Worker is built too. Checks: `401` with `errorCode` `Auth.Unauthenticated`, `traceId` and `WWW-Authenticate: Bearer`; the four security headers on the challenge and on `/health/live`; no `Access-Control-*` on a foreign preflight; login without `Origin` → `Auth.OriginRejected`; unknown user → `Auth.InvalidCredentials`; **deployed-shape bootstrap journey** — the built Worker's `bootstrap-administrator` (password on stdin) exits 0, sign-in sets the hardened cookie with `no-store`, the must-change session gets `Auth.PasswordChangeRequired`, `/auth/password` → 204, sign-in again → `GET /stations` and `GET /users` 200, a second bootstrap exits 1. The F-001 comment about the API suite being blind to the shipped shape is updated.
- **gitleaks:** no custom rule added. Verified with gitleaks **v8.30.1** (the version `gitleaks-action` v2 pulls) over a throwaway directory: the default `private-key` rule flags both a raw PKCS#8 PEM file and the same key embedded in a JSON settings file. `.gitleaks.toml` unchanged, as the plan's condition allows.
- `actionlint` 1.7.12: clean.

**Evidence:**
- GitHub Actions run **https://github.com/heinmindev/ycr/actions/runs/35955113706** on `b3464b6`: **Build and test ✅, API smoke test ✅ (26/26 checks PASS), Secret scan ✅**; Trunk-only tests skipped (runs on PRs and `main` only, by design). The key's body lines appear as `***` wherever the runner echoes the step environment.
- `dotnet test YCR.sln` (local, `b3464b6`'s code) → **614/614 passed, 0 skipped**; `dotnet build YCR.sln` 0 warnings.

**Deviations from the plan (step 12):**
- The smoke job goes beyond the plan's list (key, origin, `Auth.Unauthenticated`, headers) with the bootstrap → must-change → password change → sign-in → `200` journey on the built artifacts, the Origin and uniform-401 checks, and the no-CORS preflight. Cost: about a minute of CI.
- Cosmetic: the runner's masking rewrites the `PASS … WWW-Authenticate` line's "(got" as `***`; the check itself passed (compared value `Bearer`). Not investigated further.

**Deviations across steps 8–12, for review** (details in each step's entry): principal-cache eviction on this instance after logout, own password change and administrator session revocation (step 8, 9); login limit order address-then-username and one shared partition for overlong usernames (8); validators check presence only — username format and password policy stay in the domain/validator, empty `newPassword` is `Common.ValidationFailed` (8); unknown role names refused by the validator before the self-target check, so self + unknown role is `400` not `422` (9, ruling); contracts and endpoints consolidated into fewer files than the plan listed (8, 9); `RoleCatalogue` in Application for API validation (9); must-change coverage by a route-table test instead of a hand-written theory (8); Worker `Program` as an explicit namespaced class, CLI class `BootstrapAdministratorCli`, plain `ServiceCollection` composition, exit code 2 for usage (10); `YCR.Api.Tests` references the Worker for S25 (10); README key command `openssl genpkey`, `*.pem`/`*.key` git-ignored (11); extended smoke job (12).

**Next step (exact):** hein's third checkpoint (after step 12). Then stage 5 (scenario tests), stage 6 (code review) and stage 7 (security review) by **a different agent** than this one (AGENTS.md; workflow 02); stage 8 documentation (`docs/07`, `08`, `09`, `15`, `17`, `20`, plan P13/R-9 history). No PR has been opened.

**Blockers / open questions:** approval (tech-lead checkpoint after step 12).

**State of the branch:** committed and pushed; build green; tests green; CI green on `b3464b6`.

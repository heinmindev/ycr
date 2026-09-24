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

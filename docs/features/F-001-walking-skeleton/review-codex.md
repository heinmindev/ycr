# Review: F-001 Walking skeleton

Reviewer: codex
Commit / PR: `9106d532ee4a10729748e6040c1290f03dd6ec7c` / PR #1

## Definition of Done check

Evidence below is for the feature at the reviewed commit. `[x]` means verified, `[ ]` means
not verified or blocked by a finding.

### Specification

- [x] Spec exists and is human-approved: `docs/features/F-001-walking-skeleton/spec.md` header.
- [x] Rules are labelled and sourced; deferred station rules are explicitly waived and listed in
  `spec.md` `Blocked behaviour` (OQ26-OQ28, T-014 release gate).

### Code

- [x] Layer and module boundaries are exercised by `YCR.ArchitectureTests` (12/12 passed locally;
  `tests/YCR.ArchitectureTests/ArchitectureRuleTests.cs:43-155`).
- [x] API responses use contracts/DTO projections: `StationEndpointsTests.Get_ReturnsResponseRecordNotEntity`
  (`tests/YCR.Api.Tests/Network/StationEndpointsTests.cs:32-54`) and the architecture negative cases.
- [x] Architecture negative cases cover foreign contexts/domains, Reporting write context, API
  domain/EF entity leakage, and source authentication handlers (`ArchitectureRuleTests.cs:43-155`).

### Tests

- [x] Domain invariants and transitions are covered; `dotnet test tests/YCR.Domain.Tests/YCR.Domain.Tests.csproj
  --no-build --configuration Debug` passed 30/30.
- [x] Handler/API happy paths, error codes, authorization, duplicate/concurrency, audit, and
  SQL-shape cases are present across `tests/YCR.Application.Tests`, `tests/YCR.Api.Tests`, and
  `tests/YCR.Infrastructure.Tests`.
- [x] Unmodified production composition is covered below.
- [x] CI evidence records 147 default tests plus 4 trunk-only tests, zero skipped, and both push
  and pull-request jobs green (`progress.md:1119-1133`, runs 35564512468 and 35564888120).
- [ ] S9's required validation matrix is incomplete; see finding F-005-1.

### Security

- [x] Station endpoint permissions and the authorization-matrix update are present; S11/S12 API
  tests cover 401/403 (`StationEndpointsTests.cs:189-247`).
- [ ] Security review is pending T-007a; this report's security section is not yet final.
- [x] No SPA/cookie surface is in scope for F-001; CI runs gitleaks and the recorded CI runs passed.
- [x] Actor/audit fields are server-derived and tested end to end (`StationEndpointsTests.cs:249-275`).

### Data

- [x] SQL Server 2022 ledger DDL, migration guards, privilege boundaries, constraints, and indexes
  are covered by the infrastructure tests and the green trunk-only CI run (`spec.md:S18-S23`).
- [x] Upgrade/migration evidence is recorded in `progress.md` for the pinned image and migration
  bundle; no application-startup migration is used (S22 tests).

### Observability and audit

- [x] Create/deactivate audit events and before/after snapshots are asserted by the handler tests;
  health/readiness tests cover the defined probes.
- [x] No additional `docs/17` metric is required by this reference slice.

### Documentation and approval

- [x] Feature progress and ADR-linked implementation evidence are updated through the reviewed SHA.
- [ ] The feature review is not ready for a final Ready verdict while F-005-1 remains open.
- [ ] Human approval/merge remains the T-009 gate.

## Production-composition check

Name the test(s) that run the **unmodified production composition** — no `ConfigureTestServices`
overrides, no substituted services (`docs/21` §Tests):

| Test | What it hosts unmodified | Evidence |
|---|---|---|
| `MissingSchemeTests.ProtectedEndpoint_WithNoAuthenticationScheme_Returns401NotServerError` | `WebApplicationFactory<Program>` with `registerTestAuthentication: false`; production authentication wiring is left untouched while the real SQL Server application connection is used | `tests/YCR.Api.Tests/Common/MissingSchemeTests.cs:21-46`; factory early-returns before `ConfigureTestServices` at `tests/YCR.Api.Tests/Authentication/YcrApiFactory.cs:32-47` |
| `MissingSchemeTests.HealthEndpoint_WithNoAuthenticationScheme_StaysAnonymous` | Same unmodified production composition for the anonymous probes | `tests/YCR.Api.Tests/Common/MissingSchemeTests.cs:48-61` |

## Findings

| # | Severity | Finding | Evidence (file:line, test) | Recommended action | Status |
|---|---|---|---|---|---|
| F-005-1 | Medium | The S9 validation scenario is not covered for every required input shape. The endpoint theory covers 1-character, overlong, lower-case, and hyphen forms, while the handler theory covers whitespace-only code; neither asserts an exact 11-character code nor a code containing an embedded space. The endpoint suite also does not separately assert the blank-code case. | Requirement `spec.md:115`; endpoint cases `tests/YCR.Api.Tests/Network/StationEndpointsTests.cs:125-162`; handler cases `tests/YCR.Application.Tests/Network/CreateStationHandlerTests.cs:236-252` (`TOOLONGCODE1` is 12 characters, not the specified 11). | Add separate API/handler cases for `""` (blank), an exact 11-character code, and an embedded-space code, asserting 400 plus the stable `errorCode`/`traceId`; keep the existing hyphen and whitespace-only cases. Re-run the full CI matrix. | Fixed at `c75bb2f` |

## Security review

Security reviewer: codex
Scope: claude-owned step 1 and steps 7-13 at `9106d532ee4a10729748e6040c1290f03dd6ec7c`.
Threat categories reviewed from `docs/18-threat-model.md` and `docs/prompts/security-agent.md`:
authentication, authorization, injection, replay/idempotency, sensitive-data exposure, auditability,
secrets, rate limiting/API abuse, privilege escalation, and insider manipulation.

Controls verified: no authentication handler is shipped in `src/`; production startup rejects unexpected
schemes; endpoint permissions are explicit and tested; actor/audit fields are server-derived; station and
ledger privileges are constrained in the SQL migration; ledger writes share the business transaction;
the API does not expose EF entities; and CI runs gitleaks with read-only workflow permissions.

| # | Severity | Finding | Evidence (file:line, test) | Recommended action | Status |
|---|---|---|---|---|---|
| S-007A-1 | High | The container-backed security tests do not exercise the required separate migrator identity: the fixture's alleged migrator connection is the SA connection. This makes the migration/admin credential boundary unverified and can hide excessive rights or accidental use of SA in the deployment path. | `tests/YCR.TestSupport/SqlServerTestContainer.cs:66-83`, `:94-104`, `:126-156`; the S22 tests consume `database.MigratorConnectionString` at `tests/YCR.Infrastructure.Tests/Persistence/DatabasePrivilegeTests.cs:158-186`. This is the same underlying defect as F-006A-1. | Create and use a dedicated `ycr_migrator` login/user in the fixture, reserve SA for provisioning, and assert the migration connection's login identity and intended rights. | Fixed at `23ce780` |
| S-007A-2 | Medium | CI and test migration bundles receive SQL passwords as process command-line arguments. The workflow generates credentials into `GITHUB_ENV` without explicit masking and invokes `efbundle --connection "...Password=..."`; a failing tool or process inspection can expose them outside the intended connection. | `.github/workflows/ci.yml:141-147`, `:160-167`; `tests/YCR.TestSupport/MigrationBundle.cs:117-126`, `:219-229` (only output redaction is provided, not process-argument protection). | Prefer a secret-safe connection mechanism supported by the migrator; at minimum call `::add-mask::` for each generated password before use, avoid diagnostic process listings, and keep redaction tests for all failure paths. | Fixed at `f802526` |

Open Critical/High findings: S-007A-1 / F-006A-1. The security verdict is therefore Not ready.

## T-006a code review: claude scope

Scope reviewed at `9106d532ee4a10729748e6040c1290f03dd6ec7c`: step 1 (`1028cff`) and the implementation
commits for steps 7-13 (`0375cce`, `aa0ae50`, `b6bf336`, `6dfb67a`, `63adc65`, `00d1341`, `a57b491`,
`f63c754`, `cfd6cc5`, `34531f4`, `8d28f40`). Codex's steps 2-6 and step-5 review fixes are excluded.

| # | Severity | Finding | Evidence (file:line, test) | Recommended action | Status |
|---|---|---|---|---|---|
| F-006A-1 | High | The Testcontainers fixture does not create or use a dedicated migrator credential. `StartAsync` creates only `ycr_app`; `MigratorConnectionString` is built from `_container.GetConnectionString()`, which is the Testcontainers SA connection, so migration and grant setup run as SA. The S22/privilege tests therefore do not prove the separate migrator boundary required by E7/ADR-0017. | `tests/YCR.TestSupport/SqlServerTestContainer.cs:66-83` creates only `ycr_app`; `:94-104` applies migrations and grants using `database.MigratorConnectionString`; `:126-138` builds that value from `ConnectionStringFor(database)`; `:150-156` derives it directly from `_container.GetConnectionString()` (SA). `tests/YCR.Infrastructure.Tests/Persistence/DatabasePrivilegeTests.cs:158-186` labels this path migrator but never verifies its identity. | Generate a second password, create `ycr_migrator` at server scope and its database user, and construct `MigratorConnectionString` with that login. Reserve the SA connection for fixture provisioning only, then rerun all container-backed tests and assert the migration credential identity/rights explicitly. | Fixed at `23ce780` |
| F-006A-2 | Low | The CI workflow's `paths-ignore` does not match the feature progress files it claims to skip, so every progress-only checkpoint under `docs/features/**/progress.md` still runs the full container-backed workflow. | `.github/workflows/ci.yml:23-28` ignores `docs/progress/**`; the actual progress file is `docs/features/F-001-walking-skeleton/progress.md`. | Either change the ignore pattern to the actual progress-file scope (while retaining required checks for code/spec/review changes) or correct the comment and accept the extra runs as an explicit engineering decision. | Fixed at `350a21d` |

## Verdict

Not ready. F-005-1 and the High security boundary finding F-006A-1/S-007A-1 must be addressed.
S-007A-2 is an additional Medium secret-handling finding. T-008 must remediate and rerun the affected
scenario/security evidence before human approval.

---

## T-006a re-review at 52326b3

Reviewer: **claude**. Task **T-006a**, re-run under `TASKS.md` §Protocol item 10 after T-008a's
remediation.

**Commit re-reviewed: `52326b3`, tip of `feature/F-001`.** Delta scope `git diff d98bf12..52326b3`
— twenty commits, all **claude's own T-008a work**. §Protocol item 11 normally bars the
implementer from reviewing; hein ruled a one-time exception at `f6e2fb1` on `main` because codex
is over its usage limit, with T-009 personally checking the findings and codex still re-reviewing
before production release. This report is written to that standard: every claim below is checked
against the source or against a command run here, **not** against `progress.md`, and the section
"New findings" is what I would raise if the work were someone else's.

### Evidence

| Command / source | Result |
|---|---|
| `dotnet build YCR.sln --configuration Debug` | **Succeeded, 0 warnings, 0 errors** (19.4 s), under `TreatWarningsAsErrors` |
| `dotnet test YCR.sln --no-build`, **both `YCR_DESIGN_TIME_CONNECTION` and `YCR_MIGRATION_BUNDLE` unset** (`env -u`), Docker running | **total 174, failed 0, succeeded 174, skipped 0** (2 m 20 s). This is the exact command R-1 said was broken at `d98bf12`; it is green here. |
| `dotnet ef migrations has-pending-model-changes` with the placeholder set | exit **0**, "No changes have been made to the model since the last migration." — the hand-edited snapshot and Designers really do match the model |
| the same command with the variable **unset** | fails with `YCR_DESIGN_TIME_CONNECTION must be set for EF design-time operations.` — confirms the new `docs/07` sentence is load-bearing, not decorative |
| CI at `52326b3`, read from the run via `gh`, not from `progress.md` | pull_request [35700061128](https://github.com/heinmindev/ycr/actions/runs/35700061128), `headSha` **`52326b3c1521b9c4c4ed3f9c31c47b0cc7e0955c`**, conclusion **success**; all four jobs success — Build and test, Trunk-only tests, API smoke test, Secret scan. Build-and-test step 6 **"Verify the model matches the migrations" success**, step 8 "Test" success. |
| mutation probe: delete `ci.yml:62-63` (build-and-test job `env`) | `CiWorkflowTests` **fails** — the guard bites |
| mutation probe: delete `ci.yml:146-147` (api-smoke job `env`) | `CiWorkflowTests` **passes** — the guard does **not** bite. See **N-1**. `ci.yml` restored; worktree verified clean. |

### Per-finding verdict

| # | Verdict | Fix and evidence |
|---|---|---|
| **F-006A-1** | **Verified** | The fixture now has a real migrator. `SqlServerTestContainer.cs:47` declares `MigratorLogin = "ycr_migrator"`; `:93-100` creates both logins on `master` under SA; `:142` runs `CREATE DATABASE` under SA; `:147-156` gives the migrator its per-database user and `db_owner` under SA — the two things only SA can do — and from `:160-167` the migrator credential does the rest, including creating the application user, so a right it lacks fails there rather than passing under sysadmin. `MigratorConnectionString` is built at `:160` from `ConnectionStringFor(database, MigratorLogin, _migratorPassword)`, never from `_container.GetConnectionString()`; the SA string is now `private SaConnectionStringFor` (`:187`). The boundary is asserted by `DatabasePrivilegeTests.MigratorCredential_ConnectedToATestDatabase_IsYcrMigratorAndNotSysadmin` (`:215-237`): `SUSER_NAME() = 'ycr_migrator'`, `IS_SRVROLEMEMBER('sysadmin') = 0`, `IS_ROLEMEMBER('db_owner') = 1`, **and** that the application connection is a different login — which is what makes every other comparison in that class mean something. This matches `docker/sqlserver/init-principals.sql:32-39, 61-65`. Two deviations from that script: `CHECK_POLICY = OFF`, explained at `:89-92`, and no `DEFAULT_LANGUAGE` — see **N-8**. See **N-9** for one side effect nobody recorded. |
| **R-1** | **Verified** | `MigrationBundle.CreateStartInfo` (`MigrationBundle.cs:255-287`) writes `YCR_DESIGN_TIME_CONNECTION` on the child's `ProcessStartInfo.Environment` **only** when `designTimeConnectionOverride` is supplied (`:275-279`) or the inherited value is blank (`:280-284`); a real inherited value falls through untouched. Proven both ways by `MigrationBundleStartInfoTests` (`tests/YCR.Infrastructure.Tests/TestSupport/`): `CreateStartInfo_WithNoInheritedConnection_SuppliesThePlaceholder` for `null`/`""`/`"   "`, and `CreateStartInfo_WithAnInheritedConnection_DoesNotOverrideIt`. Deliberately CI-independent — the inherited value is a parameter, so no test mutates a process-global variable. `docs/07:100-107` carries the required developer sentence. Independently reproduced here: the full suite is **174/174 with both variables unset**, the exact condition that failed 4 tests at `d98bf12`. The fix is real. **But see N-2**: the contract is asserted on `CreateStartInfo` only, and the one production caller that feeds it is untested. |
| **R-1 own-test fix at `3d87e6a`** | **Verified, and the self-catch was correct** | The original assertion was `Assert.DoesNotContain(VAR, startInfo.Environment.Keys)`, which assumed `ProcessStartInfo.Environment` starts empty. It starts as a copy of the current process's environment, so on any machine with the variable set — every CI job, since it is job-level — the key is present either way and the assertion failed for a reason unrelated to the behaviour under test. The replacement (`:52-56`) asserts the child's entry equals this process's entry and is never the placeholder. That is the right contract. **It is still weaker than its name on a machine where the variable is unset**, where it degenerates to `Assert.Equal(null, null)`: see **N-2**. I looked for others of the same shape across the delta and found **none** — `MigrationBundleStartInfoTests` is the only new test that reads ambient process state; every other new assertion (`StationModelTests`' design-time model, `LedgerMigrationTests`' `SqlException`, `DatabasePrivilegeTests`' `SUSER_NAME()`, the six S9 rows) is a function of its own inputs. |
| **R-4 + ledger UTC** | **Verified** | Both constraints are in the EF model: `StationConfiguration.cs:11-20` and `AuditEventConfiguration.cs:26-36`, the latter alongside `ExcludeFromMigrations()` — which stops EF *emitting* DDL, not *knowing* the shape, and knowing is what `has-pending-model-changes` diffs. All four generated artefacts carry both: `YcrDbContextModelSnapshot.cs:48-51, 113-115` and the three Designers (`Network_CreateStations`, `Audit_CreateAuditEventsLedger`, `Security_AppDatabaseRole`). The ledger constraint is created **with the table** in the unapplied migration at `20260920130536_Audit_CreateAuditEventsLedger.cs:96-102`; the Stations one was already at `20260920064342_Network_CreateStations.cs:34-38` from C-1. Model side asserted by `StationModelTests.Model_WithUtcColumn_DeclaresItsCheckConstraint` (`:47-89`), a theory over both entity types reading `IDesignTimeModel` — correct, because EF strips check constraints out of the runtime model. Database side by `LedgerMigrationTests.LedgerTable_WithNonUtcOccurredAtUtc_IsRejectedByTheCheckConstraint` (`:298-315`), a raw-SQL `+06:30` insert asserting `SqlException.Number == 547`, the constraint name, and that nothing landed; `:51-61` now expects all four ledger constraints. The CI step is verified running and green (evidence table). I re-ran `has-pending-model-changes` myself: **exit 0** — the hand-edited snapshot is genuinely consistent, which was the real risk in this implementation. Two collateral edits are necessary and honest: `LedgerMigrationTests.cs:279, 348` and `DatabasePrivilegeTests.cs:140` move from `SYSDATETIMEOFFSET()` to `SYSUTCDATETIME() AT TIME ZONE 'UTC'`, because the new constraint would otherwise reject their own fixtures. **N-6** notes the half of this control that nothing guards. |
| **F-006A-2 / R-2 / R-3** | **Verified, with a regression — N-1** | **F-006A-2:** `ci.yml:26-32` adds `docs/features/**/progress.md` to the push `paths-ignore`, with a comment explaining why `docs/progress/**` never matched; the pull_request event still has none, deliberately (`:35-40`). Working as designed — the push run at `52326b3` was correctly skipped and the pull_request run covered it. **R-2:** the three step-level `env:` blocks are gone; the variable is declared at job level only, at `ci.yml:62-63`, `:146-147`, `:302-303`. **R-3:** `CiWorkflowTests` moved from `Persistence/` to `Ci/`, cut to one `[Fact]` with one claim, no timeout and no key-order coupling, plus an `Assert.NotEmpty` guard against the rule passing vacuously. All three as ruled — **but the R-3 simplification lost the guard's bite for one of the three jobs; see N-1, which I demonstrated by mutation rather than by reading.** |
| **F-005-1** | **Verified** | All three missing S9 shapes added at both levels. Endpoint (`StationEndpointsTests.cs:136-154`): `"ABCDEFGHIJK"` — **exactly 11 characters, the boundary spec S9 names**, where the pre-existing `TOOLONGCODE1` is 12 and only ever proved that something clearly too long is refused — and `"IN S"`, an embedded space that trimming cannot remove, which is a genuinely different case from the whitespace-only one. Blank code gets its own `[Fact]` at `:157-179` and correctly asserts `Common.ValidationFailed`, not `Network.InvalidStationCode`, because the validation filter answers first — a caller-visible distinction the old theory row hid. Handler (`CreateStationHandlerTests.cs:236-256`): the same three plus `""`, which reaches the domain rule directly with no filter in front. All six new rows assert `400`, the `errorCode` **and** `traceId`: `ErrorCodeOf` asserts `traceId` at `StationEndpointsTests.cs:335`, so the claim in the doc comment is true rather than assumed. |
| **The six test renames** | **Verified** | `132662c`. `List_→List_WithTwoStations_`, `Get_→Get_WithKnownId_`, `GetStation_→GetStation_WithSeededStation_`, `Handle_→Handle_WithFiveStations_`, `Handle_→Handle_WithOneStation_`, and `Deactivate_WhenActive_Returns204_AndWhenInactive_Returns422` → `Deactivate_WhenActiveThenRepeated_Returns204Then422`, which was the one packing two state/result pairs into a single name. This matches `docs/20` §2 as amended by T-008b (`Method_State_ExpectedResult`, or `Method_ExpectedResult` where there is no meaningful state). `plan.md` updated; `review-codex.md` correctly left alone, because it records what existed at `9106d53`. I spot-checked the remaining two-segment names and agree they have no meaningful setup to name. |
| **Stage-8 docs** | **Verified, with one factual error — N-4** | `docs/07:43-91` documents both tables, every column, index and check constraint with a reason per row, and states that both UTC constraints live in the model as well as the migrations and that CI runs `has-pending-model-changes`; `:100-107` adds the R-1 developer instruction. `docs/08:37-60` documents the four station endpoints and the two probes with request shape, success shape, every error code and the required permission, plus an explicit "Not implemented" and the OQ26–OQ28 provisionality. I checked these against the code rather than trusting them: `/health/live` and `/health/ready` exist at `HealthEndpoints.cs:20, 28`; `/openapi/v1.json` is Development-only at `Program.cs:60-65`; `Network.InvalidStationName` is real (`BilingualName.cs:31`). `docs/20` §6's migration-naming correction matches the actual file names (`20260920064342_…`), and §3's audit example now matches `IAuditWriter.Record`'s real six-parameter signature (`IAuditWriter.cs:35-41`) and `CreateStationHandler.cs:55-60` **exactly**. The one defect is `docs/07:86`: see **N-4**. |

### Scope

Nothing outside T-008a's remit changed. `src/` moved in three places only — two EF configurations
(check constraints) and `YcrDbContextFactory.DesignTimeConnectionVariable` widened from
`private const` to `public const` so the fixture stops repeating the literal. No handler, endpoint,
domain rule, authorization policy or authentication path was touched. `docs/glossary.md` was
deliberately **not** edited, with the F-001 terms listed in `progress.md` for T-015 to absorb — a
defensible reading of "coordinate with T-015", though the scope item's wording ("glossary
contribution") would also have permitted a direct contribution; T-009 should confirm T-015 actually
picks them up. No `[Skip]`, `#pragma warning disable` or `Assert.True(true)` was introduced, and no
test was deleted or renamed away. The count moved 158 → 174: +6 S9 rows, +5
`MigrationBundleStartInfoTests`, +2 `Model_WithUtcColumn_…` rows, +1 ledger UTC, +1 migrator
identity, and −3 +1 as `CiWorkflowTests`' theory collapsed to a fact. That reconciles exactly.

### New findings

| # | Severity | Finding | Evidence | Recommended action | Status |
|---|---|---|---|---|---|
| **N-1** | **Medium** | **The R-3 simplification silently removed `CiWorkflowTests`' protection for the `api-smoke` job — the one job where the S-007A-2 change added a second occurrence of the variable's name.** The new assertion is a whole-job substring match, `Assert.Contains(DesignTimeConnectionVariable, job.Body)`. In `api-smoke`, the S-007A-2 step at `ci.yml:202` writes the literal `YCR_DESIGN_TIME_CONNECTION=` inline inside a `run:` block, so the string is in the job body whether or not the job declares the variable. That job's `dotnet ef migrations bundle --force` at `:194-197` genuinely depends on the job-level `env:`. The pre-R-3 version, which matched `timeout-minutes: N\n    env:\n      YCR_DESIGN_TIME_CONNECTION:`, would have caught this. R-3 was right that the old form was over-coupled; the replacement went one step too far. | **Demonstrated, not inferred.** Deleting `ci.yml:146-147` (api-smoke `env:`) leaves `CiWorkflowTests` **passing** 1/1. Deleting `ci.yml:62-63` (build-and-test `env:`) makes it **fail**. `ci.yml` was restored and the worktree verified clean afterwards. Assertion at `tests/YCR.Infrastructure.Tests/Ci/CiWorkflowTests.cs:35-38`. | Match the variable as a YAML key at the job's `env:` scope rather than anywhere in the job body — or strip `run:` blocks before searching. Keep R-3's other gains: no timeouts, no key order, no indentation. The failure mode is a red CI run rather than a silent hole, which is why this is Medium and not High; but the guard exists precisely because this wiring mistake already happened once and cost a failed push. | Open |
| **N-2** | Low | **The non-override contract is asserted on `CreateStartInfo` but not on the only thing that calls it, so the exact regression R-1 forbids could be reintroduced with every test still green.** `MigrationBundle.RunAsync` supplies `inheritedDesignTimeConnection` by reading `Environment.GetEnvironmentVariable(...)` at `MigrationBundle.cs:300`. Change that one argument to `null` and a developer who had deliberately pointed EF somewhere gets the placeholder written over it — and nothing fails, because `RunAsync` is private and untested. Relatedly, `CreateStartInfo_WithAnInheritedConnection_DoesNotOverrideIt` decouples its `inherited` argument from the ambient value it then asserts on, so on a machine with the variable unset it reduces to `Assert.Equal(null, null)` plus one `NotEqual`; the `inherited` argument it was handed is never observed. | `MigrationBundle.cs:296-301`; `MigrationBundleStartInfoTests.cs:39-57`. | Either make the inherited-value lookup injectable and assert the wiring, or have the test assert the positive half directly — that a non-blank inherited value is what the child ends up with. Record honestly that for the **build** path the blast radius is nil (building a bundle never opens the connection), so this is about keeping a stated contract true, not about a live defect. | Open |
| **N-3** | Low | **Redaction is the last defence on the one remaining path where a password could reach a log, and it has no test at all; a second redactor is dead code that the class documentation presents as a live control.** S-007A-2's recommended action says "keep redaction tests for all failure paths". `MigrationBundle.Redact` (`:324`) runs on every non-zero exit (`:316`) and is untested — nothing pins that `Password=[^;"']*` matches what `SqlConnectionStringBuilder` and `ci.yml` actually emit, and nothing notices that `Pwd=` is unmatched. `SqlServerTestContainer.Redact` (`:196-199`) is `public` and has **zero call sites anywhere in the repository**, yet `:16-18` tells the reader it "strips them from anything this class reports". `23ce780` extended that dead method with `_migratorPassword`. | `grep -rn "\.Redact(" --include=*.cs src/ tests/` returns nothing. `MigrationBundle.cs:316, 320-327`; `SqlServerTestContainer.cs:16-18, 196-199`. | Add a test for `MigrationBundle.Redact` over a realistic failure string. Either wire `SqlServerTestContainer.Redact` into whatever it was meant to protect, or delete it together with the sentence at `:16-18` — a documented control that does not run is worse than no control, because a reviewer stops looking. | Open |
| **N-4** | Low | **New documentation states a number that is wrong on either reading.** `docs/07:86`: "All five check constraints are created **with the table**". `audit.AuditEvents` has **four** (`20260920130536_Audit_CreateAuditEventsLedger.cs:94-102`: `BeforeJson`, `AfterJson`, `ActorRole`, `OccurredAtUtc_Utc`) — and the table immediately above in the same section lists exactly those four. Read as five across both tables it is still wrong: `CK_Stations_CreatedAtUtc_Utc` is created by a separate `AddCheckConstraint` **after** `CreateTable` (`20260920064342_Network_CreateStations.cs:17-38`), not with the table. `LedgerMigrationTests.cs:51-61` asserts four. AGENTS.md rule 12 makes documentation part of the change. | `docs/07:86`; the constraint list at `docs/07:74-82`. | "All four check constraints on this table are created with it." | Open |
| **N-5** | Low | Stale comment created by the S-007A-2 fix itself. `MigrationBundle.cs:321-323` still says redaction exists because "Connection strings are passed on the command line, so tooling echoes them back on error" — which is exactly what this commit stopped being true. The accurate reason is already written correctly at `:127-130`: the bundle prints the connection in some failure paths regardless of how it was given. | `MigrationBundle.cs:320-324` vs `:127-130`. | Reword `:321-323`. | Open |
| **N-6** | Low | **Half of R-4's control is guarded and half is not.** The ruling pairs "constraints live in the EF model" with "CI runs `has-pending-model-changes`". The model half is asserted by `Model_WithUtcColumn_DeclaresItsCheckConstraint`; the CI half is a workflow step with nothing watching it — although this repository already has both the pattern for that (`CiWorkflowTests`) and a precedent for guarding a step against passing vacuously ("Assert trunk-only tests actually ran", `ci.yml:347-357`). Delete `ci.yml:86-91` and the drift detector is gone with no test failing. | `.github/workflows/ci.yml:86-91`; `tests/YCR.Infrastructure.Tests/Ci/CiWorkflowTests.cs`. | Add one assertion to `CiWorkflowTests` that a job runs `has-pending-model-changes`. Naturally fixed together with N-1, since both are about that file asserting less than the control it stands for. | Open |
| **N-7** | Informational | `Model_WithUtcColumn_DeclaresItsCheckConstraint` builds its options from a hardcoded `Server=(localdb)\MSSQLLocalDB` string (`StationModelTests.cs:76`) — the literal C-8 removed from production code. It is inert (the design-time model never opens a connection) and it follows the file's pre-existing pattern at `:21` and `:97`, so this is **not** a defect introduced here. Recorded only because `MigrationBundle.DesignTimeConnectionPlaceholder` now exists as the house placeholder and is the obvious thing for all three to use next time this file is touched. | `StationModelTests.cs:21, 76, 97`. | No action required within T-008a's scope. | Noted |
| **N-8** | Informational | The fixture's logins deviate from `init-principals.sql` in two ways. `CHECK_POLICY = OFF` is deviated deliberately and explained (`SqlServerTestContainer.cs:89-92`). `DEFAULT_LANGUAGE = us_english` (`init-principals.sql:37, 53`) is **not** set on the fixture's logins and the omission is not noted; it is covered in practice because `SaConnectionStringFor` pins `Current Language` on every connection string the fixture hands out (`:192`), which is the belt that login default is braces to. | `SqlServerTestContainer.cs:93-100, 187-193`; `docker/sqlserver/init-principals.sql:34-55`. | One line in the `MigratorLogin` remark noting both deviations, so the next reader does not have to re-derive that the language dependency is still honoured. | Noted |
| **N-9** | Informational | **Two tests quietly changed what they prove, and nothing records it.** `LedgerMigrationTests.LedgerTable_Update_IsRejectedByTheEngine` and `…_Delete_IsRejectedByTheEngine` (`:250-270`) execute through `MigratorConnectionString`. Before `23ce780` that was the container's **sa**, so they proved "not even a sysadmin can amend the ledger". They now run as `ycr_migrator`/`db_owner`, so they prove "`db_owner` cannot". That is the more deployment-representative claim and arguably the better test — but it is strictly weaker than what the names implied before, it fell out of the F-006A-1 fix rather than being decided, and no comment, document or commit message mentions it. | `LedgerMigrationTests.cs:250-270, 351-357`; `SqlServerTestContainer.cs:160`. | Record the intent in the two tests' remarks. If the stronger claim is wanted back, the fixture would have to expose an SA path for this one purpose, which cuts against the point of F-006A-1 — so recording it is probably the right answer, not restoring it. | Noted |

### Verdict

**Ready, with findings. No open Critical or High.**

Every finding routed to T-008a — **F-006A-1, F-006A-2, F-005-1, S-007A-2, R-1, R-2, R-3, R-4 and the
ledger UTC counterpart** — is **verified fixed**, each against hein's ruling where the ruling and the
original recommendation differed, and each with a test that asserts behaviour rather than text. The
two claims most worth checking independently both held: the full suite is **174/174, 0 skipped, with
both environment variables unset**, which is exactly the condition that failed four tests at
`d98bf12`; and `has-pending-model-changes` returns **exit 0** against the hand-edited snapshot and
Designers, which was the real risk in R-4's implementation. CI at `52326b3` is green on all four jobs
with the new model-drift step running.

**N-1 is the one I would not merge without fixing.** It is a Medium, not a High — its failure mode is
a red CI run rather than a silent hole — but it is a **regression this delta introduced**: R-3's
simplification and S-007A-2's inline assignment interact so that `CiWorkflowTests` no longer bites for
`api-smoke`, and I proved that by mutation rather than by reading. The guard exists because that exact
wiring mistake already happened once. N-6 belongs with it: both are `CiWorkflowTests` asserting less
than the control it stands for.

**N-2 and N-3 are the two places where a test proves less than its name or its documentation claims**
— the non-override contract's production wiring is untested, and redaction is untested with a second
redactor that is dead code presented as a live control. Neither is a live defect; both are stated
guarantees that nothing currently holds to. **N-4** is a plain factual error in new documentation.

On the specific question of whether anything else in this delta resembles the test I had to fix at
`3d87e6a`: I checked every new assertion, and **no**. That test was the only new one reading ambient
process state; the rest are functions of their own inputs. Its replacement is correct but, as N-2
records, still degenerates to a vacuous comparison on a machine where the variable is unset — which is
the honest version of the "verified both ways" claim in `3d87e6a`'s message.

Remaining risk for T-009: this review was written by the implementer under a declared exception. N-1
and N-9 are the two items a reviewer with no authorship stake would most likely have found first, and
they are the two I would most want codex to re-check before production release.

---

## T-007a re-review at 52326b3

Security reviewer: **claude** (security-agent). Task **T-007a**, re-run under `TASKS.md` §Protocol
item 10 after T-008a's remediation, under the same one-time §Protocol item 11 exception recorded at
`f6e2fb1` on `main`.

**Commit: `52326b3`.** Scope is the security surface of `git diff d98bf12..52326b3` — claude's own
T-008a work: the `ycr_migrator` identity and its rights, the S-007A-2 mask/environment change, and
whether anything new in the delta touches authentication, secrets or privilege. Threat categories
from `docs/18` with a surface here: **privilege escalation**, **insider manipulation**, **secrets**,
**data disclosure**, **unauthorized configuration**.

### 1. The `ycr_migrator` identity and its rights

**Does it really lack sysadmin? Yes — in the fixture and in the deployment script, by different
evidence.**

- *Fixture.* `DatabasePrivilegeTests.MigratorCredential_ConnectedToATestDatabase_IsYcrMigratorAndNotSysadmin`
  (`:215-237`) asserts `SUSER_NAME() = 'ycr_migrator'` and `IS_SRVROLEMEMBER('sysadmin') = 0` on the
  connection the S22 tests actually use, plus the positive half `IS_ROLEMEMBER('db_owner') = 1` and
  that the application connection is a different login. That closes S-007A-1 properly: before it,
  every privilege test compared `ycr_app` against a sysadmin and proved only that `ycr_app` is not
  one, which was never in doubt.
- *Deployment.* `docker/sqlserver/init-principals.sql:32-39` creates the login with `CHECK_POLICY = ON`,
  a default language and a default database, and **no server-role grant at all**, so it holds `public`
  only. Nothing in the repository adds it to a server role.
- *SA containment.* SA now does only what none of the other principals can: `CREATE DATABASE`
  (`SqlServerTestContainer.cs:142`), login creation (`:93-100`) and the migrator's own per-database
  user and `db_owner` (`:147-156`). From `:160` on, the migrator credential does the work, including
  creating the application user — so a right it turns out not to have fails in the fixture rather than
  passing under sysadmin and surfacing in a deployment. The SA connection string is now `private`
  (`:187`); no test can reach it.

**Does `db_owner` grant only what migrations need? No — it is broader, and that is the one thing here
worth a ruling.** See **S-1**. Spec E7/S22 and ADR-0017 item 3 require the two-credential split and
that the *application* login has no DDL; both are met and tested. Neither document says anything about
how much the *migrator* should hold, and no ADR records why `db_owner` was chosen.

### 2. The S-007A-2 mask and environment-variable change

**Masking — correct and complete.** `ci.yml:166-177`: all three passwords are generated into shell
locals, `::add-mask::` is emitted for each **in the same step, before any of them reaches
`$GITHUB_ENV`**, and only then are they written. Masking after first use would have been too late;
this ordering is right. Only one job generates credentials, so there is no second unmasked site.

**The migrator password no longer reaches a process argument list.** `ci.yml:202` replaces
`efbundle --connection "…Password=…"` with a shell environment-prefix assignment, so `efbundle`'s
argv is just its own path. The password is not in the `run:` script on disk either — that file holds
`${YCR_MIGRATOR_PASSWORD}`, expanded by bash at run time, not the value. In the fixture,
`MigrationBundle.ApplyAsync` (`:132-142`) passes the target through
`CreateStartInfo(..., designTimeConnectionOverride:)` with an **empty argument list**, asserted by
`CreateStartInfo_WithAnOverride_UsesItWhateverTheParentHas` (`MigrationBundleStartInfoTests.cs:63-82`),
which checks both `Assert.Empty(startInfo.ArgumentList)` and that the fake secret is absent from
`startInfo.Arguments`. The mechanism was verified to work rather than assumed: CI run 35700061128's
API smoke job is green at `52326b3` with no `--connection` anywhere.

**But "no password reaches a command line or log anywhere" is not yet true.** See **S-2**: three
passwords still reach `sqlcmd`'s argv inside the `sqlserver-init` container, on every CI run and every
local `docker compose up`. That path is pre-existing and outside the delta, and outside the evidence
S-007A-2 cited — but it is squarely inside the claim, so it is stated here rather than left implied.

**Logs.** The runner masks every generated value from the point of generation, so `docker logs
ycr-sqlserver-init` (`ci.yml:204`) and any tool that echoes a connection string print `***`. In the
fixture, `MigrationBundle.Redact` runs over child-process output on every non-zero exit
(`MigrationBundle.cs:316`). See **S-3** for what is missing around it.

### 3. Does any new code in this delta touch auth, secrets or privilege?

**Authentication and authorization: no.** `src/` changed in exactly three places —
`StationConfiguration.cs` and `AuditEventConfiguration.cs` (check constraints only) and
`YcrDbContextFactory.cs`. No endpoint, policy, permission constant, authentication handler or
authorization filter is in the diff. `Permissions.cs`, the station endpoints and `Program.cs` are
untouched, so S11/S12 and the actor-derivation controls are exactly as T-007a verified them at
`9106d53`.

**Secrets: one visibility change, no value exposed.** `YcrDbContextFactory.DesignTimeConnectionVariable`
went `private const` → `public const`. It exposes the **name** `"YCR_DESIGN_TIME_CONNECTION"`, which is
already in the workflow, the documentation and this report. No default connection string was
reintroduced — the factory still throws when the variable is unset, which I confirmed by running
`dotnet ef migrations has-pending-model-changes` with it unset and getting
`YCR_DESIGN_TIME_CONNECTION must be set for EF design-time operations.` **Fails closed, unchanged.**
No credential, key or token appears anywhere in the delta; the CI Secret scan job is green at
`52326b3`.

**Privilege: test-fixture only, and in the safe direction.** The fixture moved *down* from sysadmin to
`db_owner` for everything except provisioning. One consequence of that is recorded as N-9 in the code
review: `LedgerTable_Update_IsRejectedByTheEngine` and `…_Delete_…` used to run as sa and now run as
`ycr_migrator`, so they prove "`db_owner` cannot amend the ledger" rather than "not even a sysadmin
can". More representative, slightly weaker, and unrecorded — noted, not a defect.

**Data integrity, as a control rather than a correctness matter.** `CK_AuditEvents_OccurredAtUtc_Utc`
is created **with** the append-only ledger table (`20260920130536_Audit_CreateAuditEventsLedger.cs:96-102`)
and binds every writer at the database, not just the `IAuditWriter` path — proven by a raw-SQL `+06:30`
insert that fails with `SqlException 547` (`LedgerMigrationTests.cs:298-315`). On a table whose rows can
never be corrected, and whose `OccurredAtUtc` is what orders tamper-evident history, enforcing this
where no caller can route around it is the right place for it. This strengthens the audit control.

### Findings

| # | Severity | Finding | Evidence | Recommended action | Status |
|---|---|---|---|---|---|
| **S-1** | Low | **`db_owner` is wider than the migrations need, and the choice is recorded nowhere.** It carries `SELECT` on `audit.AuditEvents` (data disclosure), DDL over every object, and the ability to `DROP` the ledger table — which the migration's own `Down()` refuses to do (plan P7) but a raw statement under this credential would not. What the migrations actually need is DDL plus `CREATE ROLE`/`GRANT` plus write access to `__EFMigrationsHistory`, which `db_ddladmin` + `db_securityadmin` plus a history-table grant would plausibly cover. The compensating control is real and now proven under this exact credential — the ledger engine rejects `UPDATE`/`DELETE` regardless of privilege (`LedgerMigrationTests.cs:250-270`) — and SQL Server retains a dropped ledger table in ledger history. But nothing bounds the credential **from above** beyond `sysadmin = 0`, and spec E7, S22 and ADR-0017 item 3 are silent on the migrator's rights. | `docker/sqlserver/init-principals.sql:61-65`; `SqlServerTestContainer.cs:147-156`; `DatabasePrivilegeTests.cs:215-237`; spec `:137`; ADR-0017 §Decision item 3. | Not a change to make inside T-008a's scope. Record it as an **ENGINEERING DECISION** — either "`db_owner` is accepted for the migrator because the ledger engine, not the grant, is what protects the audit trail", or an evaluation of `db_ddladmin` + `db_securityadmin`. A tech-lead call, since it is a REQUIRED-CONTROL-adjacent choice currently living only in a provisioning script. | Open |
| **S-2** | Low | **Passwords still reach a command line — in `sqlserver-init`, not in the paths S-007A-2 named.** `docker-compose.yml:62-67` invokes `sqlcmd -S sqlserver -U sa -P "$$MSSQL_SA_PASSWORD" -v YcrAppPassword="$$YCR_APP_PASSWORD" -v YcrMigratorPassword="$$YCR_MIGRATOR_PASSWORD" -i …`. The `$$` defers expansion past compose, but bash **inside the container** expands before `exec`, so all three cleartext values are in `sqlcmd`'s `argv` and readable through `/proc/<pid>/cmdline` by root on the host and by anything sharing that container's PID namespace. CI reaches this at `ci.yml:203`. Exploitability is low — a throwaway container, a short-lived process, and the values are masked in the runner log — but the claim "no password reaches a command line" is not yet true, and this is the same defect class S-007A-2 raised, one layer down. **Pre-existing; `docker-compose.yml` is unchanged in this delta and is therefore outside T-008a's remit.** | `docker-compose.yml:58-67`; `.github/workflows/ci.yml:203`; the parallel case S-007A-2 closed at `ci.yml:202`. | `sqlcmd` resolves `$(Name)` from environment variables when no `-v` is given, and honours `SQLCMDPASSWORD` in place of `-P`. Renaming the three container variables to `YcrDatabase`, `YcrAppPassword`, `YcrMigratorPassword` and dropping both `-v` and `-P` removes all four from `argv` with no change to `init-principals.sql`. Worth a new task row rather than scope creep here. | Open |
| **S-3** | Low | **Redaction — the last defence on the one path where a connection string can still reach a log — is untested, and a second redactor is dead code that the class documentation presents as a live control.** S-007A-2's recommended action explicitly says "keep redaction tests for all failure paths"; there are none. `MigrationBundle.Redact` (`:324`) runs on every non-zero child exit and nothing pins that `Password=[^;"']*` matches what `SqlConnectionStringBuilder` and `ci.yml` actually emit, nor that `Pwd=` is unmatched. `SqlServerTestContainer.Redact` (`:196-199`) is `public`, has **zero call sites in the repository**, and `23ce780` extended it with `_migratorPassword` — while `:16-18` tells the reader it "strips them from anything this class reports". | `grep -rn "\.Redact(" --include=*.cs src/ tests/` returns nothing. `MigrationBundle.cs:316, 324`; `SqlServerTestContainer.cs:16-18, 196-199`. Duplicated as N-3 in the code review. | Test `MigrationBundle.Redact` against a realistic failure string. Wire or delete `SqlServerTestContainer.Redact`; a documented control that never runs is worse than none, because a reviewer stops looking. | Open |
| **S-4** | Low | **A hardening note from the T-007b re-review is closed in one place and still open in the other.** `MigrationBundle.DesignTimeConnectionPlaceholder` correctly drops `TrustServerCertificate`, with a test pinning that it carries no credential and no disabled certificate check (`MigrationBundleStartInfoTests.DesignTimeConnectionPlaceholder_IsInertAndCarriesNoCredential`). CI's job-level placeholder still carries `TrustServerCertificate=True` at `ci.yml:63, 147, 303` on a string that is never opened, and `ci.yml:200` calls it "the inert placeholder" while it names `localhost`, which in the `api-smoke` job is a reachable SQL Server. No exploit path — building a bundle opens no connection — but the two placeholders now disagree, and the CI one models a disabled certificate check for anyone who copies it. | `ci.yml:63, 147, 200, 303` vs `MigrationBundle.cs:222-223`. Raised as a Low in T-007b's re-review at `d98bf12` for `docs/07`; unchanged. | Use `MigrationBundle.DesignTimeConnectionPlaceholder`'s exact value in the three CI declarations, so there is one inert placeholder in the repository and a test guards its shape. | Open |

### Open Critical / High

**None.** Stated explicitly: there are **no open Critical findings and no open High findings** in this
delta. S-007A-1 / F-006A-1, the only High T-007a carried, is **closed and verified** — the migrator is
a real `ycr_migrator` login that is provably not a sysadmin, SA is reserved for provisioning, and the
identity is asserted on both sides of every privilege comparison. S-007A-2 is **closed for the paths it
cited**: masks are emitted before first use, and no password appears in `efbundle`'s arguments in CI or
in the fixture. The four findings above are all **Low**, and **S-1 and S-2 are pre-existing surfaces
outside T-008a's remit**, recorded because the questions this re-review was asked to answer reach them.

Nothing in this delta touches authentication or authorization. The one secrets-adjacent change — widening
a constant that names an environment variable — exposes no value, and the design-time factory still
**fails closed** when that variable is unset, which I verified by running it.

**Security verdict: ready. No open Critical or High.** T-009 should note that S-1 wants a tech-lead
ruling and S-2 wants a task row of its own, and that this review, like the code review above it, was
written by the implementer under the declared `f6e2fb1` exception.

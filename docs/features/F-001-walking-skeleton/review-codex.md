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
| F-005-1 | Medium | The S9 validation scenario is not covered for every required input shape. The endpoint theory covers 1-character, overlong, lower-case, and hyphen forms, while the handler theory covers whitespace-only code; neither asserts an exact 11-character code nor a code containing an embedded space. The endpoint suite also does not separately assert the blank-code case. | Requirement `spec.md:115`; endpoint cases `tests/YCR.Api.Tests/Network/StationEndpointsTests.cs:125-162`; handler cases `tests/YCR.Application.Tests/Network/CreateStationHandlerTests.cs:236-252` (`TOOLONGCODE1` is 12 characters, not the specified 11). | Add separate API/handler cases for `""` (blank), an exact 11-character code, and an embedded-space code, asserting 400 plus the stable `errorCode`/`traceId`; keep the existing hyphen and whitespace-only cases. Re-run the full CI matrix. | Open |

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
| S-007A-1 | High | The container-backed security tests do not exercise the required separate migrator identity: the fixture's alleged migrator connection is the SA connection. This makes the migration/admin credential boundary unverified and can hide excessive rights or accidental use of SA in the deployment path. | `tests/YCR.TestSupport/SqlServerTestContainer.cs:66-83`, `:94-104`, `:126-156`; the S22 tests consume `database.MigratorConnectionString` at `tests/YCR.Infrastructure.Tests/Persistence/DatabasePrivilegeTests.cs:158-186`. This is the same underlying defect as F-006A-1. | Create and use a dedicated `ycr_migrator` login/user in the fixture, reserve SA for provisioning, and assert the migration connection's login identity and intended rights. | Open |
| S-007A-2 | Medium | CI and test migration bundles receive SQL passwords as process command-line arguments. The workflow generates credentials into `GITHUB_ENV` without explicit masking and invokes `efbundle --connection "...Password=..."`; a failing tool or process inspection can expose them outside the intended connection. | `.github/workflows/ci.yml:141-147`, `:160-167`; `tests/YCR.TestSupport/MigrationBundle.cs:117-126`, `:219-229` (only output redaction is provided, not process-argument protection). | Prefer a secret-safe connection mechanism supported by the migrator; at minimum call `::add-mask::` for each generated password before use, avoid diagnostic process listings, and keep redaction tests for all failure paths. | Open |

Open Critical/High findings: S-007A-1 / F-006A-1. The security verdict is therefore Not ready.

## T-006a code review: claude scope

Scope reviewed at `9106d532ee4a10729748e6040c1290f03dd6ec7c`: step 1 (`1028cff`) and the implementation
commits for steps 7-13 (`0375cce`, `aa0ae50`, `b6bf336`, `6dfb67a`, `63adc65`, `00d1341`, `a57b491`,
`f63c754`, `cfd6cc5`, `34531f4`, `8d28f40`). Codex's steps 2-6 and step-5 review fixes are excluded.

| # | Severity | Finding | Evidence (file:line, test) | Recommended action | Status |
|---|---|---|---|---|---|
| F-006A-1 | High | The Testcontainers fixture does not create or use a dedicated migrator credential. `StartAsync` creates only `ycr_app`; `MigratorConnectionString` is built from `_container.GetConnectionString()`, which is the Testcontainers SA connection, so migration and grant setup run as SA. The S22/privilege tests therefore do not prove the separate migrator boundary required by E7/ADR-0017. | `tests/YCR.TestSupport/SqlServerTestContainer.cs:66-83` creates only `ycr_app`; `:94-104` applies migrations and grants using `database.MigratorConnectionString`; `:126-138` builds that value from `ConnectionStringFor(database)`; `:150-156` derives it directly from `_container.GetConnectionString()` (SA). `tests/YCR.Infrastructure.Tests/Persistence/DatabasePrivilegeTests.cs:158-186` labels this path migrator but never verifies its identity. | Generate a second password, create `ycr_migrator` at server scope and its database user, and construct `MigratorConnectionString` with that login. Reserve the SA connection for fixture provisioning only, then rerun all container-backed tests and assert the migration credential identity/rights explicitly. | Open |
| F-006A-2 | Low | The CI workflow's `paths-ignore` does not match the feature progress files it claims to skip, so every progress-only checkpoint under `docs/features/**/progress.md` still runs the full container-backed workflow. | `.github/workflows/ci.yml:23-28` ignores `docs/progress/**`; the actual progress file is `docs/features/F-001-walking-skeleton/progress.md`. | Either change the ignore pattern to the actual progress-file scope (while retaining required checks for code/spec/review changes) or correct the comment and accept the extra runs as an explicit engineering decision. | Open |

## Verdict

Not ready. F-005-1 and the High security boundary finding F-006A-1/S-007A-1 must be addressed.
S-007A-2 is an additional Medium secret-handling finding. T-008 must remediate and rerun the affected
scenario/security evidence before human approval.

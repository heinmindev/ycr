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

Security reviewer: pending T-007a
Threat IDs reviewed: pending security pass against `docs/prompts/security-agent.md` and `docs/18`
Evidence: authentication/authorization, actor provenance, least-privilege SQL role, ledger immutability,
gitleaks CI, and production-composition tests are listed above; the dedicated security review is still pending.
Open Critical/High findings: pending T-007a

## Verdict

Not ready. F-005-1 must be addressed before the scenario-test stage can claim complete S1-S27 coverage;
T-006a and T-007a must append their code/security findings before the final verdict is revisited.

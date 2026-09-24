# Review: F-002 Staff Authentication

Reviewer: codex
Reviewed implementation commit: `b3464b6` (feature branch tip includes the documentation checkpoint `d6c176b`)

## Stage 5 — scenario and required-test coverage (T-027)

### Evidence

| Command / source | Result |
|---|---|
| `dotnet test YCR.sln --no-restore` | **Passed: 614, failed: 0, skipped: 0**. Docker-backed SQL Server was running. |
| Approved scenario source | `docs/features/F-002-staff-authentication/spec.md:192-355`, including S1-S33 and S19a-S19e. |
| ADR-0016 required-test source | `docs/features/F-002-staff-authentication/spec.md:360-370` and ADR-0016. |

### Scenario traceability

Every scenario has an executable test at the reviewed commit `b3464b6`:

| Scenario | Real test(s) |
|---|---|
| S1 | `LoginEndpointTests.Login_WithValidCredentials_Returns200TokenAndHardenedCookie`; `LoginHandlerTests.Handle_WithValidCredentials_CreatesSessionWithHashedTokenAndAuditsLoginSucceeded` |
| S2 | `LoginEndpointTests.Login_UniformRejection_FourCasesIdenticalApartFromTraceId`; `PasswordServiceTests` verification cases |
| S3 | `LoginHandlerTests.Handle_WithDisabledUserAndCorrectPassword_ReturnsInvalidCredentials`; `LoginEndpointTests` uniform rejection |
| S4 | `LockoutEndpointTests.Login_NineFailuresThenSuccess_ResetsTheCount`, `Login_TenFailures_LocksForFifteenMinutesThenSucceeds`, `Login_UnknownUserTenTimes_LocksNothingAndStoresNoTypedValue`, `Login_TenFailures_LocksThenAdministratorUnlockRestoresAccess`; `Unlock_WithoutUsersManage_Returns403`; `Unlock_UnknownId_Returns404` |
| S5 | `RateLimitTests.Login_OverPerUserNameLimitFromVariedAddresses_Returns429`, `Login_OverPerAddressLimitForVariedUserNames_Returns429`, `Login_RejectedByLimit_EvaluatesNoCredentialAndWritesNoAudit`, `Login_DefaultLimits_AreFivePerUserNameAndTwentyPerAddress` |
| S6 | `OriginTests.Login_MissingOrForeignOrigin_Returns403WithNoCookieNoAuditNoCount` |
| S7 | `RefreshEndpointTests.Refresh_WithValidCookie_RotatesAndKeepsSessionExpiry` |
| S8 | `RefreshEndpointTests.Refresh_TwoTabRace_OneRotatesOther409ThenSuccessorWorks`; `IdentityConstraintTests` successor uniqueness |
| S9 | `RefreshEndpointTests.Refresh_PredecessorInsideGrace_Returns409NoCookieNoAudit`; application equivalent in `RefreshSessionHandlerTests` |
| S10 | `RefreshEndpointTests.Refresh_PredecessorOutsideGrace_RevokesFamilyAndAccessTokenFailsWithin30s` |
| S11 | `RefreshEndpointTests.Refresh_Ancestor_RevokesFamily` |
| S12 | `OriginTests.Refresh_MissingOrForeignOrigin_Returns403WithoutRotation`; `RateLimitTests.Refresh_OverPerAddressLimit_Returns429`, `Refresh_DefaultLimit_IsThirtyPerAddress` |
| S13 | `RefreshEndpointTests.Refresh_AfterTwelveHours_Returns401`; `AccessTokenTests.Token_PastExp_Returns401`, `Token_UnexpiredButSessionPastLifetime_Returns401` |
| S14 | `RefreshEndpointTests.Refresh_MissingRevokedOrUnknownCookie_Returns401` |
| S15 | `LogoutEndpointTests.Logout_RevokesSessionExpiresCookieAndAccessTokenFailsWithin30s`, `Logout_LeavesOtherSessionsActive`, `Logout_WithoutBearer_Returns401`; `OriginTests.Logout_MissingOrigin_Returns403AndRevokesNothing` |
| S16 | `PasswordEndpointTests.ChangePassword_RevokesOtherSessionsWithin30sAndKeepsCurrent`, `ChangePassword_PolicyViolation_Returns400AuthPasswordRejected`, `ChangePassword_WrongCurrent_Returns422`; `SessionHandlerTests` equivalents |
| S17 | `UserAdministrationEndpointTests.Disable_RevokesBothSessionsThenEnableRestoresSignIn`, `Disable_Self_Returns422AndChangesNothing`; `RevocationLatencyTests.DisabledUser_AccessTokenRejectedWithin30s` |
| S18 | `RevocationLatencyTests.RemovedRole_WithinThirtySeconds_Returns403WithoutReLogin`, `RemovedRolePermissionRow_WithinThirtySeconds_Returns403` |
| S19 | `UserAdministrationEndpointTests.RevokeSession_RevokesNamedSessionOnlyAndAudits`; `RevocationLatencyTests.RevokedSession_AccessTokenRejectedWithin30s`; unauthorized matrix test |
| S19a | `UserAdministrationEndpointTests.PasswordReset_RevokesAllSessionsSetsMustChangeAndAudits`, `PasswordReset_Self_Returns422`, `PasswordReset_PolicyViolation_Returns400IdentityPasswordRejected` |
| S19b | `UserAdministrationEndpointTests.ReplaceRoles_Self_Returns422AndChangesNothing` |
| S19c | `LockoutEndpointTests` administrator unlock and no-op cases; `UserAdministrationEndpointTests` endpoint matrix |
| S19d | `MustChangePasswordTests.MustChangeSession_MayCallOnlyMeRefreshLogoutAndPassword`, `AfterPasswordChange_SameSessionSucceedsWithoutSignIn`, `AdministratorCreatedAndResetPasswords_AreMustChange`, `MustChangeSession_AnonymousEndpoints_AreUnaffected` |
| S19e | `LastAdministratorEndpointTests.DisableAndReEnableSecondAdministrator_AllowedWhileOneRemains`, `MutualDisableAtOnce_ExactlyOneSucceeds`; `AdministrationHandlerTests` last-administrator and concurrent demotion cases |
| S20 | `AccessTokenTests.Token_HasEs256KidAndExactlyEightClaims`; `AuditActorTests` and station audit injection test |
| S21 | `AccessTokenTests.Token_Invalid_Returns401`, `Token_PastExp_Returns401`, `Token_UnexpiredButSessionPastLifetime_Returns401`; forged-permission case |
| S22 | `DeployedShapeTests.ProtectedEndpoint_Anonymous_Returns401BearerChallengeWithProblemDetails`, `ProtectedEndpoint_WithGarbageBearer_Returns401WithoutValidationDetail`, `CommonUnauthenticated_AppearsNowhereInSrc`, `AuthorizationResultHandler_TypeNoLongerExists` |
| S23 | `AuthenticationSchemeGuardTests.Startup_WithOnlyJwtBearerOutsideTesting_Succeeds`, `Startup_WithTestHandlerOutsideTesting_Throws` |
| S24 | `ArchitectureRuleTests` positive and violating-fixture tests; `HealthEndpointsTests` anonymous probes |
| S25 | `ProductionCompositionTests.Bootstrap_ChangePassword_Login_ListStations_WithNoTestOverrides`, `ListStations_Anonymous_Returns401AuthUnauthenticated` |
| S26 | `WrongPermissionTests.TicketOperator_PostStations_Returns403`, `TicketOperator_AnyAdministrationEndpoint_Returns403` |
| S26a | `SigningKeyStartupTests.Production_WithDevelopmentOnlyKey_FailsStartup`, `Production_WithDevOrTestKid_FailsStartup`, `Startup_WithNoActiveKey_FailsStartup`, `Startup_WithNonP256Key_FailsStartup` |
| S26b | `BrowserControlsTests.Preflight_FromForeignOrigin_HasNoCorsHeaders` |
| S26c | `BrowserControlsTests.EveryResponseKind_CarriesSecurityHeaders`, `AuthResponses_AreNoStore` |
| S27 | `AuditActorTests.LoginEvents_ActorFieldsPerU4_BearerOfAnotherUserIgnored`, `FamilyRevoked_ActorNull`, `AdministrationEvents_ActorAndPermissionFromServerContext_BodyAndHeadersIgnored`; login and refresh handler audit tests |
| S28 | `SecretLeakTests.AfterAuthScenarios_NoSecretInLogsAuditOrProblemBodies_NoUserNameInLogs` |
| S29 | `RefreshEndpointTests.Ledger_AfterRefreshScenarios_HasExactlyTwoFamilyRevokedRows`; application equivalent in `RefreshSessionHandlerTests` |
| S30 | `IdentitySeedTests.Seed_ProducesExactlyEightRolesAndFourteenGrants`, `Seed_MatchesDocs10GrantTables`, `Seed_EveryPermissionIsAPermissionsConstant`, `Seed_CreatesNoUser`; `RoleNamesTests` |
| S31 | `RefreshTokenGeneratorTests`; `LoginEndpointTests` storage assertions; `SessionHandlerTests.IdentityTables_AfterSignInAndRefresh_ContainNoRawTokenOrPassword` |
| S32 | `BootstrapAdministratorCommandTests.Bootstrap_FirstRun_CreatesOneMustChangeAdministratorAndExitsZero`, `Bootstrap_SecondRun_ExitsNonZeroAndWritesNothing`, `Bootstrap_PasswordAppearsInNoOutputOrLog`, process/stdin coverage; `AdministrationHandlerTests` bootstrap cases |
| S32a | `UserAdministrationEndpointTests.Create_WithInvalidUserName_Returns400AndCreatesNothing`; `BootstrapAdministratorCommandTests` invalid username; `UserNameTests`, `IdentityConstraintTests` |
| S33 | `RefreshEndpointTests.Refresh_TwoTabRace_OneRotatesOther409ThenSuccessorWorks`; `IdentityConstraintTests` SQL uniqueness tests |

### ADR-0016 required tests

All ten required tests are represented by the following executable cases at `b3464b6`: refresh race (S8), predecessor inside grace (S9), predecessor outside grace (S10), ancestor reuse (S11), logout (S15), password change (S16), disabled user (S17), removed permission within 30 seconds (S18), missing Origin (S6/S12/S15), and session revocation (S19). The named tests are listed in the scenario table above and cover both application behavior and the real API pipeline where the requirement is endpoint-visible.

### Findings

No missing scenario or ADR-0016 required test was identified. No test was added, weakened, skipped, or deleted. T-027 therefore has no defect-exposing test to record.

### Stage 5 verdict

**READY for stage 6.** Scenario and required-test coverage is complete for reviewed commit `b3464b6`; the full solution test run is green with zero skipped tests.

## Stage 6 — code review (T-028)

Reviewer: codex  
Reviewed implementation commit: `b3464b6`

### Evidence

| Check | Result |
|---|---|
| Architecture and module boundaries | `dotnet test YCR.sln --no-restore` passed the architecture assembly. The Identity application references its own `IIdentityDbContext`, API responses use contracts/DTOs, and the negative boundary fixtures for EF entities, foreign contexts, ASP.NET Identity in Application, and authentication handlers pass. |
| Error contracts | Auth pipeline and `/auth/*` use `Auth.*`; administration paths use `Identity.*`; endpoint validation and ProblemDetails tests pass. `DeployedShapeTests` covers the bearer challenge and removal of `AuthorizationResultHandler`. |
| Migrations and grants | `IdentityMigrationTests` covers the F-001 upgrade path, identity constraints/indexes and no-cascade FKs. `DatabasePrivilegeTests` covers the `ycr_app` role, column-scoped writes, no role/grant writes, no session/token deletes, and the separate migrator identity. |
| Test quality and composition | The stage-5 matrix above names the real tests; `ProductionCompositionTests` exercises the API without test-service overrides. The full suite was 614/614 passed, 0 skipped. |
| Definition of Done | Code, architecture, tests, security controls and data checks have executable evidence. The documentation items listed in findings C-2 and the pending stage-7 security review keep the feature from a final ready verdict. |

### Findings

| # | Severity | Finding | Evidence (file:line, test) | Recommended action | Status |
|---|---|---|---|---|---|
| C-1 | Medium | The implementation treats the token, session and refresh-grace lifetimes as arbitrary positive configuration, even though the approved F-002 rules fix them at 15 minutes, 12 hours and approximately 20 seconds. A deployment can currently set `Auth:AccessTokenLifetime`, `Auth:SessionLifetime` or `Auth:RefreshGraceWindow` to another positive value and still start successfully, changing token exposure and refresh replay behavior without an ADR or documented business decision. | `src/YCR.Api/Common/Authentication/AuthOptionsValidator.cs:32-36` validates only `> 0`/non-negative; the fixed values are stated in `docs/features/F-002-staff-authentication/spec.md:160-163` and `plan.md:89, P11`, while `src/YCR.Api/appsettings.json:12-14` exposes them as settings. No startup test rejects a value that differs from the approved lifetime. | Keep these values fixed in code, or add an explicit accepted configuration decision and startup bounds/tests that preserve the approved security limits. Do not let an operator silently change the approved token/session policy. | Fixed at `18f1e35` |
| C-2 | Medium | The feature changes the API surface, identity schema/grants, account-security controls and observability, but the required durable docs are still F-001-only. The Definition of Done requires affected docs in the same change; the implementation branch has no F-002 update to the API specification, database design, security architecture, deployment/observability docs, or glossary. The stage-4 progress file explicitly defers these to stage 8, so the feature is not ready for the final DoD gate at `b3464b6`. | `docs/08-api-specification.md:1-49` lists only the proposal and F-001 station endpoints; `docs/07-database-design.md:35-91` documents only Stations/Audit and even retains the stale “five check constraints” sentence; `docs/09-security-architecture.md:1-26` remains a generic checklist; `docs/features/F-002-staff-authentication/plan.md:235` assigns these docs to stage 8. | Complete stage 8 before approval: document `/auth/*`, `/users/*`, `/auth-sessions/*` and `/roles`, the `identity` tables and grants, implemented security controls/blocked production gates, metrics/log events, signing-key/origin/bootstrap deployment requirements, and any canonical role vocabulary. | Fixed at `18f1e35` |

### Definition of Done review

- [x] Approved spec and plan exist; rules and accepted decisions are labelled and sourced.
- [x] No domain logic is in endpoints; API responses are DTO/contract projections.
- [x] Architecture tests and negative boundary cases pass.
- [x] Domain, handler, API, concurrency, production-composition and SQL privilege tests pass.
- [x] Migration upgrade path, constraints, indexes and grants are tested.
- [ ] Security review is pending T-029.
- [ ] Affected durable documentation is pending stage 8 (C-2).
- [ ] Human approval and merge remain outside this review.

### Stage 6 verdict

**NOT READY.** Reviewed commit `b3464b6` has no identified architecture or migration correctness blocker, but C-1 and C-2 are open Medium findings. C-1 requires a security-policy decision/fix; C-2 is the required stage-8 documentation work. T-029 must complete the independent security review before any final readiness claim.

## Stage 7 — security review (T-029)

Reviewer: codex  
Reviewed implementation commit: `b3464b6`

### Threat/control coverage

- **Token issuance and validation:** ES256 signing, configured issuer/audience, required signature and expiry, algorithm allow-list, 30-second clock skew, and server-side `sub`/`sid` principal reconstruction are implemented in `JwtAccessTokenIssuer` and `JwtBearerSetup`. Forged permission claims are discarded by the replacement principal. `AccessTokenTests` covers exact claims and invalid/tampered/expired/session-invalid tokens.
- **Refresh rotation and replay:** the database unique successor index plus EF concurrency token enforce one successor; immediate predecessor grace returns `Auth.RefreshSuperseded` without revocation; predecessor-after-grace and ancestor reuse revoke the family and write `Identity.RefreshFamilyRevoked`. `RefreshEndpointTests` and `IdentityConstraintTests` cover the race and replay paths.
- **Principal resolution and the 30-second bound:** `SessionPrincipalCache` rechecks session expiry on each use, bounds stale role/permission/session state by validated TTL, and supports local eviction on password change/logout/admin session revocation. `RevocationLatencyTests` covers session, disablement and permission changes.
- **Lockout, anti-enumeration and limits:** unknown, wrong, disabled and locked credentials converge on the same `401`; the unknown path performs a dummy full hash verification. Login and refresh limits run before credential handling, and Origin rejection runs before both. `LoginEndpointTests`, `PasswordServiceTests`, `LockoutEndpointTests`, `RateLimitTests` and `OriginTests` cover these controls.
- **Must-change and privilege boundaries:** the middleware gates authorized routes while allowing only the approved self-service routes; permissions are server-resolved, role changes cannot target the caller, and the last-administrator checks use the SQL application lock. `MustChangePasswordTests`, `WrongPermissionTests` and `LastAdministratorEndpointTests` cover these paths.
- **Audit integrity and secrets:** actor fields are derived from the server principal or explicit no-actor/sign-in paths; snapshots exclude credentials/tokens; DTOs and command `ToString` implementations redact secrets. `AuditActorTests`, `SecretLeakTests`, bootstrap tests and the architecture rule restricting `RecordSignIn` cover this surface.

Threat categories reviewed from `docs/18-threat-model.md`: account takeover, API abuse, privilege escalation, data disclosure, insider manipulation, refresh-token predecessor replay/family reuse, and unauthorized configuration. SPA XSS and frontend dependency controls remain the documented pre-production gate because no SPA is in F-002.

### Findings

| # | Severity | Finding | Evidence (file:line, test) | Recommended action | Status |
|---|---|---|---|---|---|
| S-1 | Medium | The approved MFA release gate is procedural only: the API can create accounts with `SystemAdministrator`, `RailwayAdministrator` or `FinanceOfficer` roles before MFA exists, and the bootstrap CLI creates a `SystemAdministrator` without an environment guard. Password-only privileged accounts in production remain a direct account-takeover/privilege-escalation risk. | `src/YCR.Api/Endpoints/Identity/UserEndpoints.cs:69-80` accepts the caller-supplied role set; `src/YCR.Application/Identity/CreateUser/CreateUserHandler.cs:43-50` applies it; `src/YCR.Application/Identity/BootstrapAdministrator/BootstrapAdministratorHandler.cs:49-78` creates the first administrator. The release gate is only documented in ADR-0023 D4 and `TASKS.md` T-026 item 1. | Keep privileged roles blocked from production provisioning until the MFA feature ships, either through a deployment control that is tested or an explicit production configuration gate. Treat T-026 item 1 as a release blocker. | Fixed at `18f1e35` |
| S-2 | Medium | Login and refresh rate limits are in-process and partition client traffic by `RemoteIpAddress`. Behind a reverse proxy, all users can share one partition; across N API instances, the effective limit is multiplied by N. This weakens API-abuse protection and can create a denial-of-service path even though the current behavior is an accepted provisional decision. | `src/YCR.Api/Common/Authentication/AuthRateLimitFilter.cs:92-97,108-123` uses the TCP peer and singleton in-process limiters. ADR-0023 D5 explicitly accepts this until hosting is decided; `TASKS.md` T-026 item 4 tracks trusted-proxy configuration. | Configure trusted `ForwardedHeaders` and a deployment-appropriate shared limiter before production, with tests proving the partition key and effective limits through the proxy. | Open release gate |
| S-3 | Medium | Production signing-key storage is still the configuration-backed provider. The code refuses missing, malformed, development-marked and non-P256 keys, but it does not provide a production secret-store implementation; compromise of the configuration channel exposes the long-lived ES256 private key and all newly issued tokens. | `src/YCR.Api/Common/Authentication/SigningKeyProvider.cs:24-86` loads private PEM material from `Auth:Signing`; ADR-0023 D11 and `TASKS.md` T-026 item 3 explicitly leave the production provider blocked on hosting. | Choose and implement the production key provider/rotation procedure before deployment; keep the current provider for development/testing only or document the approved secret mechanism and operational rotation controls. | Open release gate |
| S-4 | Low | Validation enforces ES256 and configured signing keys, but it does not require a `kid` header during bearer validation. Issuance always emits a `kid`, yet a signed token without one may still be accepted when IdentityModel can match one of the configured validation keys, making key rotation behavior less explicit and potentially increasing verification work. | `src/YCR.Api/Common/Authentication/JwtBearerSetup.cs:47-70` sets `IssuerSigningKeys`/`ValidAlgorithms` but no `kid` requirement or key-id resolver; `JwtAccessTokenIssuer` supplies the `kid` through `SigningCredentials` at `src/YCR.Api/Common/Authentication/JwtAccessTokenIssuer.cs:31-49`. | Reject tokens without a non-empty `kid` and resolve the validation key by the configured key id; add a negative API test for a signed token with the header omitted. | Fixed at `18f1e35` |

### Security verdict

**READY for the security-stage exit criterion: no open Critical or High findings.** Security review of `b3464b6` records Medium release gates S-1 through S-3 and Low hardening item S-4. These do not override the stage-6 NOT READY verdict: C-1/C-2 and the T-026 production gates must be resolved before production approval.

## Re-review of 18f1e35 (T-031)

Reviewer: codex
Reviewed stage-8 fix commit: `18f1e35` (`18f1e35018760ba11d98eedcf41c13023c71a0d7`). The branch head is `7fb4856`; that later commit only records progress and finding statuses. Scope: `73f5892..18f1e35`, the stage-8 changes for C-1, S-1, S-4 and C-2.

### Evidence

| Check | Result |
|---|---|
| `dotnet test YCR.sln` | **643 passed, 0 failed, 0 skipped.** Docker-backed integration tests passed. |
| CI smoke environment | The only behavior changes to `.github/workflows/ci.yml` set `DOTNET_ENVIRONMENT=Testing` for both Worker bootstrap invocations. The API still runs with `ASPNETCORE_ENVIRONMENT=Production`; the existing smoke checks and deployed-shape journey remain in the job. |
| Documentation spot-check | The 15 documented Identity routes match the five `AuthEndpoints` routes and ten `UserEndpoints` routes. The privileged-role 422 codes map to the domain business-rule error through `ResultExtensions`. Identity grants match `Security_IdentityGrants`; seeded role/grant documentation remains checked against the migration by `IdentitySeedTests.Seed_MatchesDocs10GrantTables`. The `YCR.Identity` meter, four counters, cache-age histogram, EventIds 2001–2004 and event names in `docs/17` match `IdentityTelemetry`. |

### Finding status

| # | Status | Re-review evidence |
|---|---|---|
| C-1 | **Closed** | `AuthLifetimes` fixes the values at 15 minutes, 12 hours and 20 seconds (`src/YCR.Application/Identity/AuthLifetimes.cs:17-23`). Issuance/session rotation consume those values; `AuthOptionsValidator` rejects any configured difference during startup (`src/YCR.Api/Common/Authentication/AuthOptionsValidator.cs:34-36,68`). `SigningKeyStartupTests` rejects each differing setting and accepts an explicitly configured matching value (lines 89-119). The three settings are removed from `appsettings.json`. |
| S-1 | **Closed** | `PrivilegedRoleGate.ForEnvironment` blocks only the case-insensitive `Production` environment, and `Check` rejects only the three named roles (`src/YCR.Application/Identity/PrivilegedRoleGate.cs:24-47`). Create and role replacement call the gate before writes; role replacement checks `(command.Roles ?? []).Except(before, StringComparer.Ordinal)` (`src/YCR.Application/Identity/ReplaceUserRoles/ReplaceUserRolesHandler.cs:72`), so retaining an already-held role is not treated as a grant. In Production, endpoint tests cover each privileged role returning `422 Identity.PrivilegedRoleRequiresMfa` without writes; ordinary roles succeed. Development and Testing tests allow each privileged role. Bootstrap tests cover Production refusal with no writes, Production-by-default when `DOTNET_ENVIRONMENT` is unset, and successful Development/Testing use. The fallback registration fails closed. This matches hein’s rulings: Production bootstrap remains blocked until MFA ships, and only newly granted roles are checked. |
| S-4 | **Closed** | `JwtBearerSetup` resolves signing keys only by a non-empty `kid` and sets `TryAllIssuerSigningKeys = false` (`src/YCR.Api/Common/Authentication/JwtBearerSetup.cs:58-61,83-89`). `AccessTokenTests.Token_CorrectlySignedWithoutKid_Returns401` proves both an omitted and empty `kid` fail with 401 while the same correctly signed token with the configured `kid` succeeds (`tests/YCR.Api.Tests/Identity/AccessTokenTests.cs:95-116`). |
| C-2 | **Closed** | `docs/07` now describes the six Identity tables, constraints, indexes and `ycr_app` grants; its grant list matches the `Security_IdentityGrants` migration. `docs/08` lists every mapped Identity endpoint and its request, success, errors and permission, including the new 422 code on create/role replacement. `docs/09` describes implemented controls and production gates; `docs/15` describes runtime, signing-key/origin configuration and bootstrap environment behavior; `docs/17` matches telemetry declarations; the glossary records the implemented role identifiers and keeps OQ12 explicitly open for an official answer. The smoke job continues to prove its prior journey with only the bootstrap environment changed to Testing; the API remains Production. |

S-2 (trusted-proxy/shared rate limiting) and S-3 (production signing-key storage) remain **open release gates**, as tracked in T-026. No stage-8 finding was reopened.

### Re-review verdict

**READY FOR HUMAN APPROVAL; NOT READY FOR PRODUCTION.** C-1, C-2, S-1 and S-4 are closed at `18f1e35`. The requested full solution test run passed. S-2 and S-3 remain explicit pre-production gates and are not represented as resolved by this re-review.

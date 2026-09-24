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
| C-1 | Medium | The implementation treats the token, session and refresh-grace lifetimes as arbitrary positive configuration, even though the approved F-002 rules fix them at 15 minutes, 12 hours and approximately 20 seconds. A deployment can currently set `Auth:AccessTokenLifetime`, `Auth:SessionLifetime` or `Auth:RefreshGraceWindow` to another positive value and still start successfully, changing token exposure and refresh replay behavior without an ADR or documented business decision. | `src/YCR.Api/Common/Authentication/AuthOptionsValidator.cs:32-36` validates only `> 0`/non-negative; the fixed values are stated in `docs/features/F-002-staff-authentication/spec.md:160-163` and `plan.md:89, P11`, while `src/YCR.Api/appsettings.json:12-14` exposes them as settings. No startup test rejects a value that differs from the approved lifetime. | Keep these values fixed in code, or add an explicit accepted configuration decision and startup bounds/tests that preserve the approved security limits. Do not let an operator silently change the approved token/session policy. | Open |
| C-2 | Medium | The feature changes the API surface, identity schema/grants, account-security controls and observability, but the required durable docs are still F-001-only. The Definition of Done requires affected docs in the same change; the implementation branch has no F-002 update to the API specification, database design, security architecture, deployment/observability docs, or glossary. The stage-4 progress file explicitly defers these to stage 8, so the feature is not ready for the final DoD gate at `b3464b6`. | `docs/08-api-specification.md:1-49` lists only the proposal and F-001 station endpoints; `docs/07-database-design.md:35-91` documents only Stations/Audit and even retains the stale “five check constraints” sentence; `docs/09-security-architecture.md:1-26` remains a generic checklist; `docs/features/F-002-staff-authentication/plan.md:235` assigns these docs to stage 8. | Complete stage 8 before approval: document `/auth/*`, `/users/*`, `/auth-sessions/*` and `/roles`, the `identity` tables and grants, implemented security controls/blocked production gates, metrics/log events, signing-key/origin/bootstrap deployment requirements, and any canonical role vocabulary. | Open |

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

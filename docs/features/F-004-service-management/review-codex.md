# F-004 Service Management Review

## Stage 5 — Scenario tests

**Reviewed implementation:** `67797d6` (feature/F-004; branch head `16d290b` contains only the
stage-4 progress checkpoint).

**Result:** Every live scenario in spec §4 is traced below. S51 is explicitly excluded by the
approved spec because it is an accepted, untested interleaving. Three focused domain tests were
added for the requested stop-order boundaries: a two-station route, a three-station full circuit,
and a 200-station full circuit of exactly `n + 1` stops. The stage-5 additions are in
`tests/YCR.Domain.Tests/Timetable/ServiceTests.cs`.

| Scenario | Test(s) proving it |
|---|---|
| S1 | `Post_WithValidRequest_Returns201WithLocationAndId` — `tests/YCR.Api.Tests/Timetable/ServiceEndpointsTests.cs`; `CreateService_WithValidCommand_PersistsServiceStopsAndOneAuditEvent` — `tests/YCR.Application.Tests/Timetable/CreateServiceHandlerTests.cs`; `Create_ForwardOnClosedRoute_HasContiguousPositionsInRequestOrder` — `tests/YCR.Domain.Tests/Timetable/ServiceTests.cs` |
| S2 | `Get_WithKnownId_ReturnsServiceResponseRecordNotEntity` — `tests/YCR.Api.Tests/Timetable/ServiceEndpointsTests.cs`; `GetService_WithKnownId_ReturnsStopsInOrderWithCurrentRouteAndStationValues` — `tests/YCR.Application.Tests/Timetable/ServiceQueryHandlerTests.cs` |
| S3 | `List_ReturnsPagedEnvelopeOrderedByCodeThenEffectiveFrom` and `List_FilteredByRouteId_ReturnsOnlyThatRoute` — `tests/YCR.Api.Tests/Timetable/ServiceEndpointsTests.cs`; `ListServices_ReturnsPagedEnvelopeOrderedByCodeThenEffectiveFromWithWithdrawn` and `ListServices_FilteredByRoute_ReturnsOnlyThatRoutesServices` — `tests/YCR.Application.Tests/Timetable/ServiceQueryHandlerTests.cs` |
| S4 | `Post_WithValidPatterns_Returns201(Forward, DEAB)` — `tests/YCR.Api.Tests/Timetable/ServiceEndpointsTests.cs`; `CreateService_WithValidPatterns_Creates(Forward, DEAB)` — `tests/YCR.Application.Tests/Timetable/CreateServiceHandlerTests.cs`; `Create_WrapOnClosedRoute_Succeeds(Forward, DEAB)` — `tests/YCR.Domain.Tests/Timetable/ServiceTests.cs` |
| S5 | The same three parameterized tests with `Reverse, BAED`. |
| S6 | The same three parameterized tests with `Forward, DAC` and `Forward, CB`. |
| S7 | `Post_WithValidPatterns_Returns201` and `GetService_FullCircuit_KeepsTheClosingStopAtTheLastPosition` — `tests/YCR.Api.Tests/Timetable/ServiceEndpointsTests.cs`; `CreateService_WithValidPatterns_Creates` — `tests/YCR.Application.Tests/Timetable/CreateServiceHandlerTests.cs`; `Create_FullCircuit_SucceedsWithClosingStopLast` — `tests/YCR.Domain.Tests/Timetable/ServiceTests.cs`; stage-5 `Create_OnThreeStationRoute_FullCircuitUsesExactlyFourStops` and `Create_OnTwoHundredStationRoute_FullCircuitUsesExactlyTwoHundredOneStops` — same domain file. |
| S8 | `Post_WithInvalidPatterns_Returns422WithErrorCode(CEC)` — `tests/YCR.Api.Tests/Timetable/ServiceEndpointsTests.cs`; `CreateService_WithInvalidPatterns_ReturnsErrorAndWritesNothing(CEC)` and `Create_FullCircuitWithTwoDistinctStations_ReturnsTooFewStops` — application/domain timetable tests. |
| S9 | `Post_WithInvalidPatterns_Returns422WithErrorCode(CDEABCD, DACE)` and `Create_MoreThanOneCircuit_IsRefused` — corresponding API, application and domain timetable tests. |
| S10 | `Post_WithValidPatterns_Returns201(QR, SRP)` and `Create_OpenRoutePartAndReverse_Succeeds` — API/application/domain timetable tests. |
| S11 | `Post_WithInvalidPatterns_Returns422WithErrorCode(RSP, QPS)` and `Create_WrapOnOpenRoute_ReturnsOutOfOrder` — API/application/domain timetable tests. |
| S12 | `Post_WithInvalidPatterns_Returns422WithErrorCode(PQRP)` and `Create_ClosureOnOpenRoute_ReturnsRepeated` — API/application/domain timetable tests. |
| S13 | `Post_WithInvalidPatterns_Returns422WithErrorCode(ABBC, ABAC)` and `Create_RepeatedStop_ReturnsRepeated` — API/application/domain timetable tests. |
| S14 | `Post_WithInvalidPatterns_Returns422WithErrorCode(ACB, PRQ)` and `Create_StopsOutOfOrder_ReturnsOutOfOrder` — API/application/domain timetable tests. |
| S15 | `Post_WithInvalidPatterns_Returns422WithErrorCode(A)` and `Create_WithFewerThanTwoStops_ReturnsTooFewStops` — API/application/domain timetable tests. |
| S16 | `Post_WithInvalidPatterns_Returns422WithErrorCode(AXC, AZC)` and `Create_StopNotOnRoute_ReturnsStopNotOnRoute` — API/application/domain timetable tests. |
| S17 | `Post_OnInactiveRoute_Returns422RouteInactive` and `CreateService_OnInactiveRoute_ReturnsRouteInactiveAndWritesNothing` — API/application tests; `Create_OnInactiveRoute_ReturnsRouteInactive` — domain test. |
| S18 | `Post_WithInactiveStopStation_Returns422` and `CreateService_WithInactiveStopStation_ReturnsStopStationInactiveAndWritesNothing`; `CreateService_PassingInactiveStation_Creates` and the API inactive-station test's pass-through case. |
| S19 | `Post_WithUnknownRoute_Returns422RouteNotFound` and `CreateService_WithUnknownRoute_ReturnsRouteNotFoundAndWritesNothing`. |
| S20 | `DeactivateRouteOrStation_UsedByAService_Returns204AndServiceShowsInactive` — `tests/YCR.Api.Tests/Timetable/ServiceEndpointsTests.cs`; `GetService_AfterRouteOrStationDeactivated_ShowsInactiveFlagsAndRowsUnchanged` — `tests/YCR.Application.Tests/Timetable/ServiceQueryHandlerTests.cs`. |
| S21 | `Post_WithInvalidBody_Returns400CommonValidationFailed` and `Post_WithExactly200StopIds_PassesValidationAndReachesTheHandler` — `tests/YCR.Api.Tests/Timetable/ServiceEndpointsTests.cs`; shape/format rows in `CreateServiceHandlerTests`; `ServiceCodeTests`, `OperatingDaysTests`, and `EffectivePeriodTests` — `tests/YCR.Domain.Tests/Timetable/`. |
| S22 | `Post_WithInvalidCode_Returns400InvalidServiceCode`, `Post_WithInvalidName_Returns400InvalidServiceName`; `CreateService_WithInvalidCodeOrName_ReturnsValidationErrorAndWritesNothing`; `ServiceCodeTests`. |
| S23 | `Post_WithEffectiveToBeforeFrom_Returns400`, `Post_WithOneDayPeriod_Returns201`; `CreateService_WithEffectiveToBeforeEffectiveFrom_ReturnsInvalidPeriod`, `CreateService_WithOneDayPeriod_Creates`; `EffectivePeriodTests`. |
| S24 | `Post_WithOverlappingPeriod_Returns409` (open-ended row); `CreateService_WithOverlappingPeriodForSameCode_ReturnsConflictAndWritesNothing`; `EffectivePeriodTests`. |
| S25 | The same overlap theory's inclusive edge and route/direction/day variants, plus `Post_WithAdjacentPeriod_Returns201` and `CreateService_WithAdjacentPeriodForSameCode_Creates`; `EffectivePeriodTests`. |
| S26 | `TimetableChange_WithdrawThenCreateSameCode_Returns204Then201AndListsBoth` — API; `WithdrawService_ThenCreateSameCodeFromSameDate_BothSucceed` — application. |
| S27 | `CreateService_WithParallelSameCodeOverlappingPeriods_PersistsExactlyOneServiceAndOneAuditEvent` — `tests/YCR.Application.Tests/Timetable/CreateServiceHandlerTests.cs`; `ApplicationCredential_CanTakeTheServiceCodeApplock` — `tests/YCR.Infrastructure.Tests/Persistence/DatabasePrivilegeTests.cs`. |
| S28 | `CreateService_WithParallelSameCodeAdjacentPeriods_CreatesBoth` — `tests/YCR.Application.Tests/Timetable/CreateServiceHandlerTests.cs`; adjacent-period rows in `EffectivePeriodTests`. |
| S29 | `WithdrawAndCreate_SameCode_WithdrawFirst_Returns204Then201`, `WithdrawAndCreate_SameCode_CreateFirst_Returns409Then204`, and `WithdrawAndCreate_SameCodeInParallel_NeverCommitOverlappingServices` — `tests/YCR.Application.Tests/Timetable/WithdrawServiceHandlerTests.cs`. |
| S30 | `Withdraw_ReturnsExpectedStatusAndReadsBack` (future withdrawal); `WithdrawService_FromFutureDate_UpdatesBothColumnsAndWritesOneEventWithBeforeAndAfter`; `WithdrawServiceSql_UpdatesOnlyEffectiveToAndWithdrawnAtAndNoServiceStops`. |
| S31 | `Withdraw_ReturnsExpectedStatusAndReadsBack` and `WithdrawService_FromToday_EndsYesterday`. |
| S32 | `Withdraw_ReturnsExpectedStatusAndReadsBack` and `WithdrawService_FromPastDate_ReturnsDateInPastAndWritesNothing`. |
| S33 | `Withdraw_ReturnsExpectedStatusAndReadsBack` and `WithdrawService_ThatDoesNotShorten_ReturnsDoesNotShortenAndWritesNothing` (both extending and equal-end cases). |
| S34 | The same withdrawal theory's already-ended case. |
| S35 | `Withdraw_Twice_Returns204Then204Then422` — API; `WithdrawService_Twice_ShortensAgainThenRefusesLonger` — application/domain timetable tests. |
| S36 | `Withdraw_ReturnsExpectedStatusAndReadsBack` and `WithdrawService_BeforeEffectiveFrom_NeverRunsAndFreesTheCode`; `GetService_ThatNeverRuns_ReturnsStoredDatesAndNeverRunsTrue`. |
| S37 | `WithdrawService_TwoWithdrawalsForced_EndOnTheShorterDateInEitherOrder` — application, both forced orders. |
| S38 | `UnknownIdOrInvalidWithdrawBody_Returns404Or400` — API; `WithdrawService_WithUnknownId_ReturnsNotFound` — application. |
| S39 | `Withdraw_AfterRouteDeactivated_Returns204`; `WithdrawService_AfterRouteOrStopStationDeactivated_Succeeds`. |
| S40 | `Post_WithPastEffectiveFrom_Returns201`; `CreateService_WithPastEffectiveFrom_CreatesAndStampsCreatedAtWithClockNow`; `Create_AtYangonMidnight_UsesTheYangonDateAsToday` and `Today_AroundYangonMidnight_ReturnsTheYangonDate`. |
| S40a | `Post_WithEffectiveToInPast_Returns422` and `Post_WithEffectiveToToday_Returns201`; `CreateService_WithEffectiveToBeforeToday_ReturnsEffectiveToInPastAndWritesNothing`, `CreateService_WithEffectiveToToday_Creates`. |
| S41 | `AnyServiceEndpoint_Anonymous_Returns401` — `tests/YCR.Api.Tests/Timetable/ServiceEndpointsTests.cs`; deployed-shape protected-endpoint checks. |
| S42 | `WriteEndpoints_WithOnlyServicesRead_Return403` and `ServiceEndpoints_WithOnlyStationOrRoutePermissions_Return403`. |
| S43 | `Post_WithBodyOverLimit_Returns413WithoutInternals` (declared and chunked create/withdraw), `Post_WithMalformedJson_Returns400WithoutInternals`, `Post_WithLargestValidCreate_IsUnderTheLimitAndReachesTheHandler`, `Withdraw_WithLargestValidBody_ReachesTheHandler` — `tests/YCR.Api.Tests/Timetable/ServiceRequestLimitTests.cs`. |
| S44 | `List_WithPageSizeAbove200_Returns400InvalidPageRequest` — API; `ListServices_WithPageSizeAbove200_ReturnsInvalidPageRequest` — application. |
| S45 | `Post_WhenRequestTriesToSupplyActorFields_RecordsTheAuthenticatedActor`; `CreateService_AuditActor_ComesFromCurrentUserOnly`. |
| S46 | `CreateService_WithValidCommand_PersistsServiceStopsAndOneAuditEvent`, all failure tests' `WritesNothing` assertions, and `Create_ForwardOnClosedRoute_HasContiguousPositionsInRequestOrder`. |
| S47 | `ApplicationCredential_HasExactlyTheTimetableGrants` and `ApplicationCredential_CanWithdrawAServiceButNotRewriteIt` — `tests/YCR.Infrastructure.Tests/Persistence/DatabasePrivilegeTests.cs`; migration grant assertions in `TimetableMigrationTests`. |
| S48 | `ForeignKeys_RejectAServiceOrStopNamingNoNetworkRow` — `tests/YCR.Infrastructure.Tests/Persistence/TimetableMigrationTests.cs`; `ApplicationCredential_CannotInsertAServiceOrStopNamingNoNetworkRow` — `tests/YCR.Infrastructure.Tests/Persistence/DatabasePrivilegeTests.cs`. |
| S49 | `Post_WithMyanmarName_RoundTripsThroughGet`; `CreateService_WithMyanmarName_RoundTripsExactly`. |
| S50 | `ContractsRule_WithDomainContextOrEfFixtures_DetectsViolations`, `TimetableApplication_DependingOnNetworkDomainOrContext_IsDetected`, `TimetableApplication_DependingOnNetworkContracts_IsAllowed`, `CommonKernel_WithModuleDependency_DetectsViolation` — `tests/YCR.ArchitectureTests/ArchitectureRuleTests.cs`. |
| S51 | Explicitly untested by spec §5 (accepted create/deactivation race); no coverage is expected. |
| S52 | `Post_WithSundayAndMonday_ReadsBackMondayFirst`; `CreateService_OperatingDays_StoredAsBitsAndReadMondayFirst`; `OperatingDaysTests`. |

### Rules without a dedicated scenario

| Rule | Evidence |
|---|---|
| R3/R4 permission names and exact role grants | `Seed_ProducesExactlyEightRolesAndThirtyFourGrants`, `Seed_MatchesDocs10GrantTables`, `ApplicationCredential_HasExactlyTheTimetableGrants`, plus the endpoint 401/403 tests. |
| R8/R29 identifiers only, cross-schema NO ACTION FKs, no Network navigation | `TimetableModel_ForeignKeys_AreNoActionAcrossSchemasWithoutNavigations`, `ForeignKeys_RejectAServiceOrStopNamingNoNetworkRow`, and the S50 architecture fixtures. |
| R23 generated GUID ids | `CreateService_WithValidCommand_PersistsServiceStopsAndOneAuditEvent` asserts the generated id is used for the service and every stop; API asserts the same id in `Location` and body. |
| R24/R26 audit atomicity and UTC instants | Create/withdraw application audit assertions, `WithdrawServiceSql_UpdatesOnlyEffectiveToAndWithdrawnAtAndNoServiceStops`, and timetable UTC check-constraint tests. |
| R25 no idempotency key | `AbsentEndpoints_AreNotRouted` and the API contract tests send no idempotency field; the endpoint inventory has no such parameter. |
| R27 no real service seed data | `Migrate_FromF003Schema_CreatesTimetableObjectsGrantsAndSeed` asserts the timetable tables are empty after migration. |
| R28 contract implementation boundary | `NetworkReaderTests`, `ContractsRule_WithDomainContextOrEfFixtures_DetectsViolations`, and the Timetable dependency fixtures. |
| R32 error mapping | API scenario assertions check the `Timetable.*` and `Common.ValidationFailed` codes, while `ResultExtensions` is the sole status mapper. |
| R33 no delete/reactivation/edit | `AbsentEndpoints_AreNotRouted`, the timetable grants absence assertions, and `Service_ExposesNoMutatorOtherThanWithdraw`. |
| R41/R42 empty-period semantics | `WithdrawService_BeforeEffectiveFrom_NeverRunsAndFreesTheCode`, `GetService_ThatNeverRuns_ReturnsStoredDatesAndNeverRunsTrue`, and `EffectivePeriodTests`' empty-period overlap cases. |

**Test additions:** three focused domain tests were added for the requested 2/3/200 station stop-order
boundaries. The implementation was mutated locally by changing the cyclic guard from `travelled >
limit` to `travelled >= limit`; `dotnet test tests/YCR.Domain.Tests/YCR.Domain.Tests.csproj --no-restore`
then failed **23 tests**, including the new 3- and 200-station full-circuit tests and existing wrap,
full-circuit, and simulation cases. The mutation was reverted and the domain suite returned to
**217 passed, 0 failed, 0 skipped**.

The code lock was then mutated locally to a no-op while retaining parameter use. The application
suite failed the forced same-code create/withdraw and withdrawal-order tests (`The second operation
acquired the code lock while the first still held it`) and the SQL assertion that `sp_getapplock` was
executed; the run was terminated after those deterministic failures rather than waiting through a
database-lock timeout. The production lock was restored exactly.

## Stage 6 — Code review

**Reviewed SHA:** `67797d6` (plus the stage-5 test/report commit `1bae50f`).

**Verdict:** **READY**. No Critical, High, Medium, or Low findings were identified in the
reviewed implementation.

**Checks performed:**

- The shared-kernel move contains only the common bilingual-name and code-format rules. Existing
  station and route behavior remains covered by the F-001/F-003 suites; the Common architecture
  rule rejects a dependency on any module. The Network contract exposes only primitive record
  values, `NetworkReader` is `internal`, and the contract/domain/context fixture tests fail on the
  prohibited shapes. Timetable application code has no Network domain or context dependency.
- `Service.Create` keeps the approved R37 order: code, names, period/today, route, then route/stops
  and stop-state checks; overlap is checked last under the code lock. The cyclic algorithm was
  checked at 2, 3, and 200 stations and at exactly `n + 1` full-circuit stops; the stage-5 mutation
  check made the order tests fail when the boundary changed.
- `sp_getapplock` is exclusive, transaction-owned, parameterized, scoped to the validated service
  code, and configured with the planned 30-second timeout. Both create and withdraw begin the
  explicit transaction before acquiring it, and the forced same-code tests cover both operation
  orders plus different-code non-blocking behavior. Timeout/negative return values throw instead
  of allowing an unlocked write.
- Withdrawal changes only `EffectiveTo` and `WithdrawnAtUtc` in one guarded update. The nullable
  `EffectiveTo` concurrency token maps a stale writer to
  `Timetable.ServiceChangedConcurrently`; the audit row rolls back with the transaction. The SQL
  test proves no stop-row update and the database grant tests prove the application role has no
  other timetable update or delete privilege.
- `Timetable_CreateServices`, the permission-seed migration, and `Security_TimetableGrants` match
  the EF model. Cross-schema route/station keys are `NO ACTION`, have no Network navigations, and
  the migration Down operations remove only their owned objects/grants. The required command
  `dotnet ef migrations has-pending-model-changes --project src/YCR.Infrastructure
  --startup-project src/YCR.Infrastructure` passed with no pending changes (using the required
  design-time connection environment variable).
- `ILocalCalendar` resolves the configured IANA zone, and both API and Worker fail closed when it is
  missing or unresolvable. The endpoint metadata and real-Kestrel tests enforce the 32 KiB create
  and 1 KiB withdraw body limits before binding. The API smoke job starts from the output directory,
  checks the shipped content root, and exercises the service create/read/list/withdraw flow.
- DTOs and explicit snapshots keep EF entities and personal/security data out of API responses and
  audit payloads. No API endpoint evaluates domain rules or accepts actor fields.

**Verification:** `dotnet test YCR.sln --no-restore` passed with **1,161 passed, 0 failed, 0
skipped**. `dotnet build src/YCR.Infrastructure/YCR.Infrastructure.csproj --no-restore` passed
with 0 warnings and 0 errors. `git diff --check` is clean for the review changes.

### Findings

None.

# F-003 Route Management Review

## Stage 5 — Scenario tests

**Reviewed implementation:** `910724e207dd2223c8d16088547b6ed1763617c0` (feature/F-003)

**Result:** Every live scenario in spec §4 is traced below. The existing stage-4 test suite already
proves the required behavior; no test-code additions were required in this stage. The suite was run
unmodified with `dotnet test YCR.sln`: **789 passed, 0 failed, 0 skipped**.

| Scenario | Test(s) proving it |
|---|---|
| S1 | `Post_WithValidRequest_Returns201WithLocationAndId` — `tests/YCR.Api.Tests/Network/RouteEndpointsTests.cs`; `CreateRoute_WithValidCommand_PersistsRoutePositionsAndOneAuditEvent` — `tests/YCR.Application.Tests/Network/CreateRouteHandlerTests.cs` |
| S2 | `Get_WithKnownId_ReturnsRouteResponseRecordNotEntity` — `tests/YCR.Api.Tests/Network/RouteEndpointsTests.cs`; `GetRoute_WithKnownId_ReturnsStationsInPositionOrderWithCurrentStationData` — `tests/YCR.Application.Tests/Network/RouteQueryHandlerTests.cs` |
| S3 | `List_WithThreeRoutesOneInactive_ReturnsPagedEnvelopeOrderedByCode` — `tests/YCR.Api.Tests/Network/RouteEndpointsTests.cs`; `ListRoutes_WithThreeRoutesOneInactive_ReturnsPagedEnvelopeOrderedByCode` — `tests/YCR.Application.Tests/Network/RouteQueryHandlerTests.cs` |
| S5 (including R26) | `Post_WithInvalidBody_Returns400CommonValidationFailed` (missing/null/empty/non-GUID fields, missing `isClosed`, 201 ids, whitespace name) and `Post_WithExactly200StationIds_PassesValidationAndReachesTheHandler` — `tests/YCR.Api.Tests/Network/RouteEndpointsTests.cs` |
| S6 | `Post_WithUnknownStation_Returns422RouteStationNotFound` — `tests/YCR.Api.Tests/Network/RouteEndpointsTests.cs`; `CreateRoute_WithUnknownStation_ReturnsRouteStationNotFoundAndWritesNothing` — `tests/YCR.Application.Tests/Network/CreateRouteHandlerTests.cs` |
| S7 | `Post_WithInactiveStation_Returns422RouteStationInactive` — `tests/YCR.Api.Tests/Network/RouteEndpointsTests.cs`; `CreateRoute_WithInactiveStation_ReturnsRouteStationInactiveAndWritesNothing` — `tests/YCR.Application.Tests/Network/CreateRouteHandlerTests.cs` |
| S8 | `Post_WithRepeatedStation_Returns422RouteStationRepeated` — `tests/YCR.Api.Tests/Network/RouteEndpointsTests.cs`; `CreateRoute_WithRepeatedStation_ReturnsRouteStationRepeated` — `tests/YCR.Application.Tests/Network/CreateRouteHandlerTests.cs`; `RouteStations_SameStationTwiceInOneRoute_RejectedByUniqueIndex` — `tests/YCR.Infrastructure.Tests/Persistence/RouteMigrationTests.cs` |
| S9 | `Post_AtAndBelowMinimumLength_Returns201Or422` — `tests/YCR.Api.Tests/Network/RouteEndpointsTests.cs`; `CreateRoute_AtAndBelowMinimumLength_CreatesOrReturnsTooFew` — `tests/YCR.Application.Tests/Network/CreateRouteHandlerTests.cs` |
| S10 | `Post_WithDuplicateCode_Returns409WithErrorCode` — `tests/YCR.Api.Tests/Network/RouteEndpointsTests.cs`; `CreateRoute_WithCodeOfActiveRoute_ReturnsConflictAndWritesNothing` — `tests/YCR.Application.Tests/Network/CreateRouteHandlerTests.cs` |
| S11 | `Post_WithInvalidCode_Returns400InvalidRouteCode` and `Post_WithInvalidName_Returns400InvalidRouteName` — `tests/YCR.Api.Tests/Network/RouteEndpointsTests.cs`; `CreateRoute_WithInvalidCodeOrName_ReturnsValidationErrorAndWritesNothing` — `tests/YCR.Application.Tests/Network/CreateRouteHandlerTests.cs` |
| S12 | `GetAndDeactivate_WithUnknownId_Return404RouteNotFound` — `tests/YCR.Api.Tests/Network/RouteEndpointsTests.cs`; `DeactivateRoute_WithUnknownId_ReturnsNotFound` and `GetRoute_WithUnknownId_ReturnsNotFound` — `tests/YCR.Application.Tests/Network/DeactivateRouteHandlerTests.cs`, `RouteQueryHandlerTests.cs` |
| S13 | `List_WithPageSizeAbove200_Returns400InvalidPageRequest` — `tests/YCR.Api.Tests/Network/RouteEndpointsTests.cs`; `ListRoutes_WithPageSizeAbove200_ReturnsInvalidPageRequest` — `tests/YCR.Application.Tests/Network/RouteQueryHandlerTests.cs` |
| S14 | `AnyRouteEndpoint_Anonymous_Returns401` — `tests/YCR.Api.Tests/Network/RouteEndpointsTests.cs`; route rows are also in `ProtectedEndpoint_Anonymous_Returns401BearerChallengeWithProblemDetails` — `tests/YCR.Api.Tests/Common/DeployedShapeTests.cs` |
| S15 | `Post_WithOnlyRoutesRead_Returns403`, `Deactivate_WithOnlyRoutesRead_Returns403`, `Post_WithOnlyStationsManage_Returns403`, `List_WithOnlyStationsRead_Returns403` — `tests/YCR.Api.Tests/Network/RouteEndpointsTests.cs`; `TicketOperator_ReadsRoutesButCannotCreateOrDeactivate` — `tests/YCR.Api.Tests/Identity/RoutePermissionGrantTests.cs` |
| S17 | `DeactivateStation_InActiveRoute_Returns204AndRouteShowsStationInactive` — `tests/YCR.Api.Tests/Network/RouteEndpointsTests.cs`; `DeactivateStation_WhenStationIsInActiveRoute_DeactivatesAndLeavesRouteStationsUnchanged` — `tests/YCR.Application.Tests/Network/DeactivateStationHandlerTests.cs`; `GetRoute_AfterStationDeactivated_ShowsStationInactiveAtSamePosition` — `tests/YCR.Application.Tests/Network/RouteQueryHandlerTests.cs` |
| S21 | `CreateRoute_WithParallelDuplicateCodes_PersistsExactlyOneRouteAndOneAuditEvent` and `CreateRoute_ParallelDuplicateLoser_LeavesNoRouteStationsRows` — `tests/YCR.Application.Tests/Network/CreateRouteHandlerTests.cs`; `UniqueConstraintTranslationTests` — `tests/YCR.Infrastructure.Tests/Persistence/UniqueConstraintTranslationTests.cs` proves real SQL Server constraint-name translation. |
| S22 | `Post_WhenRequestTriesToSupplyActorFields_RecordsTheAuthenticatedActor` — `tests/YCR.Api.Tests/Network/RouteEndpointsTests.cs`; `CreateRoute_AuditActor_ComesFromCurrentUserOnly` — `tests/YCR.Application.Tests/Network/CreateRouteHandlerTests.cs` |
| S23 | `Deactivate_WhenActiveThenRepeated_Returns204Then422AndReadsBackInactive` — `tests/YCR.Api.Tests/Network/RouteEndpointsTests.cs`; `DeactivateRoute_WhenActive_SetsInactiveAndClockTimestampAndWritesOneAuditEvent`, `DeactivateRoute_WhenInactive_ReturnsAlreadyInactiveWritesNoAuditAndKeepsTimestamp`, `DeactivateRoute_WithParallelRequests_OneSucceedsOneAlreadyInactiveOneAuditEvent`, `DeactivateRoute_KeepsRouteAndRouteStationsRows` — `tests/YCR.Application.Tests/Network/DeactivateRouteHandlerTests.cs`; `DeactivateRouteSql_UpdatesOnlyIsActiveAndDeactivatedAtAndNoRouteStations` — `tests/YCR.Application.Tests/Network/ListRoutesSqlTests.cs` |
| S24 | `CreateRoute_WithValidCommand_PersistsRoutePositionsAndOneAuditEvent`, failure cases with `WritesNothing`, and `CreateRoute_ParallelDuplicateLoser_LeavesNoRouteStationsRows` — `tests/YCR.Application.Tests/Network/CreateRouteHandlerTests.cs`; API failures call `AssertNoRouteWrittenAsync` — `tests/YCR.Api.Tests/Network/RouteEndpointsTests.cs` |
| S25 | `ApplicationCredential_HasExactlyTheRouteGrants` and `ApplicationCredential_CanDeactivateARouteButNotRewriteIt` — `tests/YCR.Infrastructure.Tests/Persistence/DatabasePrivilegeTests.cs`; `Migrate_FromF002Schema_CreatesRouteTablesConstraintsIndexesAndGrants` — `tests/YCR.Infrastructure.Tests/Persistence/RouteMigrationTests.cs` |
| S26 | `Post_WithMyanmarName_RoundTripsThroughGet` — `tests/YCR.Api.Tests/Network/RouteEndpointsTests.cs`; `CreateRoute_WithMyanmarName_RoundTripsExactly` — `tests/YCR.Application.Tests/Network/CreateRouteHandlerTests.cs` |
| S28 | `Get_ClosedRoute_ReturnsIsClosedAndThreeStations` — `tests/YCR.Api.Tests/Network/RouteEndpointsTests.cs`; `GetRoute_ClosedRoute_ReturnsIsClosedAndThreeStationsFirstOnlyAtPositionOne` — `tests/YCR.Application.Tests/Network/RouteQueryHandlerTests.cs`; `Create_ClosedRoute_HasFirstStationOnlyAtPositionOne` — `tests/YCR.Domain.Tests/Network/RouteTests.cs` |
| S29 | `Post_SecondRouteSharingStations_Returns201` — `tests/YCR.Api.Tests/Network/RouteEndpointsTests.cs`; `CreateRoute_SharingStationsWithAnotherRoute_CreatesBothWithOwnPositions` — `tests/YCR.Application.Tests/Network/CreateRouteHandlerTests.cs` |
| S30 | `Post_WithCodeOfDeactivatedRoute_Returns409` — `tests/YCR.Api.Tests/Network/RouteEndpointsTests.cs`; `CreateRoute_WithCodeOfDeactivatedRoute_ReturnsConflictAndWritesNothing` — `tests/YCR.Application.Tests/Network/CreateRouteHandlerTests.cs` |

### Rules without a dedicated scenario

| Rule | Evidence |
|---|---|
| R10 grants | `ApplicationCredential_HasExactlyTheRouteGrants` asserts positive grants and absence of table-level UPDATE, forbidden column UPDATE, DELETE, ALTER and CONTROL; `Migrate_FromF002Schema_CreatesRouteTablesConstraintsIndexesAndGrants` asserts the six route grant rows. |
| R18 position | `CreateRoute_WithValidCommand_PersistsRoutePositionsAndOneAuditEvent`, `Get_WithKnownId_ReturnsRouteResponseRecordNotEntity`, and `RouteModel_MapsTablesKeysAndConstraintNames` assert contiguous/order-preserving positions and the composite key. |
| R24 current station names | `Get_WithKnownId_ReturnsRouteResponseRecordNotEntity` and `GetRoute_WithKnownId_ReturnsStationsInPositionOrderWithCurrentStationData` read station projections, including current code/name/activity fields; `GetRoute_AfterStationDeactivated_ShowsStationInactiveAtSamePosition` proves the projection is current after a station change. |
| A1 `DeactivatedAtUtc` | `DeactivateRoute_WhenActive_SetsInactiveAndClockTimestampAndWritesOneAuditEvent`, `Deactivate_WhenActiveThenRepeated_Returns204Then422AndReadsBackInactive`, `DeactivateRouteSql_UpdatesOnlyIsActiveAndDeactivatedAtAndNoRouteStations`, and `RouteConstraints_RejectNonUtcOffsetsAndNonPositivePositions` cover null-while-active, UTC write, same UPDATE, concurrency/retention, and the database check. |

**Test additions:** none. No live scenario or listed rule was found without an asserting test, and
the existing tests assert observable outcomes rather than only status codes for the data-integrity
rules. Removed S4, S16, S18–S20 and S27 remain intentionally unimplemented per the approved spec.

## Stage 6 — Code review

**Reviewed SHA:** `910724e207dd2223c8d16088547b6ed1763617c0`

**Verdict:** **READY**. No Critical or High findings. The implementation matches the approved
specification and plan in the reviewed scope.

**Checks performed:**

- `StationCode` now delegates only the existing pattern to `NetworkCodeFormat`; the existing
  station tests still pin trimming and all accepted/rejected forms. `BilingualName.Create` keeps
  its original station overload and adds an explicit error overload for routes; station behavior is
  unchanged.
- `Route.Create` validates in the planned order: repeated station, minimum length, missing station,
  then inactive station. The handler validates code and names first, performs station validation,
  then checks duplicate route code, matching the plan's error precedence.
- Duplicate-code races map `UX_Routes_Code` by constraint name. The route-station unique-index catch
  is retained as defense in depth and does not broaden unrelated database errors.
- `IsActive` is the EF concurrency token. Deactivation sets `IsActive` and `DeactivatedAtUtc`
  before one `SaveChangesAsync`; the SQL test proves the single guarded `UPDATE` contains exactly
  those two columns and no `RouteStations` update. Concurrency losers map to
  `Network.RouteAlreadyInactive`, with the audit insert rolled back.
- Route endpoints require the intended permissions and map application DTOs to response records;
  no EF entity is returned. Route projections read current station fields and list queries page and
  count in SQL.
- Audit snapshots are explicit records containing the approved route fields and stable station
  codes. The audit writer derives actor, permission, address and correlation fields from server
  context, and create/deactivate changes plus audit rows share one unit of work.
- The three migrations match the EF model and their `Down()` methods reverse only their own objects
  and grants. The exact CI command `dotnet ef migrations has-pending-model-changes --project
  src/YCR.Infrastructure --startup-project src/YCR.Infrastructure` passed with no pending changes.
- `dotnet test YCR.sln` passed with **789/789, 0 skipped**.

### Findings

#### C-1 — Low — Unreachable route-station constraint mapping has no focused test

**Evidence:** `src/YCR.Application/Network/CreateRoute/CreateRouteHandler.cs:104-110` catches
`UX_RouteStations_StationId_RouteId`, while `Route.Create` rejects every repeated station before
`SaveChangesAsync` (`src/YCR.Domain/Network/Route.cs:81-88`). The stage-5 race and duplicate tests
therefore cannot reach this catch; the existing infrastructure test proves translation generally,
not this handler mapping.

**Recommendation:** Keep the catch as defense in depth because the named database constraint is the
integrity authority, and add a focused application test seam that supplies that named violation (or
explicitly document the catch as untestable infrastructure defense). Do not remove the mapping or
turn unrelated database errors into a route conflict. This is a testability/maintenance item only;
it does not block the review verdict.

## Stage 7 — Security review

Pending T-038.

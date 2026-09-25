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

Pending T-037.

## Stage 7 — Security review

Pending T-038.

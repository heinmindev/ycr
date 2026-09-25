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

**Status:** Addressed as ruled, at `fa3642c` (T-039).

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

**Reviewed SHA:** `910724e207dd2223c8d16088547b6ed1763617c0`

**Threat coverage:** `docs/18` API abuse, unauthorized configuration, privilege escalation and
data disclosure categories; ADR-0017/0021 audit controls; `docs/09` authentication and
`docs/10` route grants.

**Verdict:** **READY — no open Critical or High findings.**

**Controls verified:**

- All four endpoints require authorization: `routes.manage` on create/deactivate and `routes.read`
  on get/list. API tests cover anonymous `401`, wrong-permission `403`, and station permissions
  granting no route right. Seed migration tests assert the exact ten role grants from `docs/10`.
- `ycr_app` has route `SELECT`/`INSERT`, column-scoped `UPDATE(IsActive, DeactivatedAtUtc)`, and
  RouteStations `SELECT`/`INSERT` only. Database tests assert absent DELETE, table-level UPDATE,
  forbidden column UPDATE, ALTER/CONTROL, and DDL. No migration grants DDL.
- `stationIds` is required, non-empty, GUID-D validated and capped at 200 before the handler;
  `pageSize` is capped at 200 in the application paging guard. Malformed IDs and the 201-id case
  return the validation ProblemDetails shape. Route/domain bounds enforce 2–10 code characters
  and 1–100 trimmed name characters.
- Myanmar Unicode round-trips through the API and application tests. Route snapshots contain only
  route configuration and stable station identifiers/codes, with no personal data, secrets, tokens,
  cookies or keys. Explicit response DTOs prevent EF entities from leaving the API.
- Audit actor, role, permission, client IP and correlation values are read from `ICurrentUser` by
  the infrastructure writer. The actor-field injection test proves request body and forwarded-header
  values do not replace server context.
- CI route smoke checks use masked/generated credentials and tokens; route responses are checked by
  status and selected JSON fields without echoing secrets. The existing CI secret scan remains in the
  workflow, and the smoke/API logs are only uploaded on failure.

### Findings

#### S-1 — Medium — Route body size is bounded only after JSON model binding

**Status:** Fixed at `fa3642c` (T-039).

**Threat:** API abuse / resource exhaustion (`docs/18`).

**Evidence:** `CreateRouteRequestValidator` limits `StationIds.Count` to 200 and the domain limits
field lengths, but both run after ASP.NET has deserialized the request (`src/YCR.Api/Contracts/Network/RouteContracts.cs:52-76`).
`src/YCR.Api/Program.cs` and `RouteEndpoints` do not set an explicit route request-size limit or
JSON depth/field-size limit. A caller can therefore send a large JSON body containing far more than
200 array elements or very large strings; the server allocates and parses it before returning the
validation error. The tests cover malformed IDs and 201 ids, but not a huge body or malformed JSON
at the hosting boundary.

**Recommended fix:** Add an explicit request-size limit for `POST /routes` (and, if the hosting
policy requires it, a bounded JSON depth/string policy) below the platform default, and add API tests
for an oversized body and malformed JSON that verify a bounded, non-sensitive 4xx response. Keep the
200-id validation as the business/API contract after binding. This finding is Medium because the
endpoint is authenticated and the platform has a default body limit, but the feature does not state
or test a deliberate abuse bound.

**Stage 7 result:** S-1 is Medium; there are **zero open Critical/High findings**.

## Re-review of fa3642c

**Reviewed range:** `d40d756..2f381bc` on `feature/F-003`; implementation fix `fa3642c`, with
`2f381bc` containing only review-status lines and the progress checkpoint. `fa3642c` is an ancestor
of the branch head.

### S-1 — Closed

- `POST /api/v1/routes` alone carries `RequestSizeLimitAttribute(32 * 1024)` through endpoint
  metadata (`src/YCR.Api/Endpoints/Network/RouteEndpoints.cs:56`). A repository search found no
  request-size metadata on any other endpoint; those limits remain T-042's scope.
- `RouteRequestLimitTests` runs the API on real Kestrel via `WebApplicationFactory.UseKestrel(0)`.
  Declared-length and chunked bodies over 32 KiB both return `413`; three malformed JSON bodies
  return `400`; all responses are framework ProblemDetails under 1 KiB with status and trace ID,
  no stack, exception, framework/parser detail, server path or database name. The 200-id request
  with 10-character code and 100-character names returns `422 Network.RouteStationNotFound`,
  proving the largest valid request reaches the handler.
- The required mutation was run locally: after removing the metadata, the seven-test class ran
  **5 passed / 2 failed / 0 skipped**; both oversize cases changed to `400 BadRequest` instead of
  the expected `413 RequestEntityTooLarge`. The metadata was restored exactly, rebuilt, and the
  final class ran **7 passed / 0 failed / 0 skipped**.
- The mechanism matches the deployed hosting path: `RequestSizeLimitAttribute` implements
  `IRequestSizeLimitMetadata`, endpoint routing applies it to Kestrel's
  `IHttpMaxRequestBodySizeFeature` before the body is read. The real-Kestrel tests exercise that
  feature; `TestServer` is intentionally not used because it has no body-size feature.

### C-1 — Closed

The named `UX_RouteStations_StationId_RouteId` catch remains in
`src/YCR.Application/Network/CreateRoute/CreateRouteHandler.cs:104-115`; only its comment changed.
The comment matches hein's ruling: this is defense in depth, unreachable while `Route.Create`
rejects repeats, with no test seam added. `UniqueConstraintTranslationTests` covers the named SQL
Server translation and `RouteModelTests` pins the constraint name. No behavior changed.

### Amendment 3 and documentation

Amendment 3 is consistent with the validator, domain rules and tests: V7 is `400
Common.ValidationFailed` for whitespace-only `code`, `nameEn` and `nameMy`; format/length failures
remain the domain's `Network.InvalidRouteCode`/`Network.InvalidRouteName`; R27 is the route-only
32 KiB limit. S5 gained the two missing whitespace rows, and S11/R27/§6 describe the same behavior.

The stage-8 docs were checked against the migrations and endpoints:

- `docs/07` matches route columns, UTC and position checks, primary/unique indexes, both `NO ACTION`
  foreign keys, the absence of `IX_RouteStations_StationId`, the three migration names, and the
  exact `ycr_app` grants/absences.
- `docs/08` matches all four route endpoints, request/response contracts, permissions, error codes
  and sources, precedence, the 32 KiB Kestrel limit, and the framework 400/413 shape.
- `docs/glossary.md` adds exactly three route entries; each Myanmar term remains `OPEN QUESTION`.
- The only changed paths under `docs/` are `07`, `08`, the F-003 spec/progress/review files, and
  the glossary. `README.md`, `docs/10`, `docs/19`, and the MR question pack are unchanged.

### Verification and verdict

`dotnet test YCR.sln --no-restore` on the restored branch: **798 passed, 0 failed, 0 skipped**.
`git diff --check` is clean. No new findings were identified.

**Final verdict: READY FOR HUMAN APPROVAL.** S-1 and C-1 are closed, Amendment 3 and the stage-8
documentation match the implemented behavior, and there are no new Critical, High, or Medium
findings in this re-review.

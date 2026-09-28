# Review: F-005 Timetables

Reviewer: codex (hein, 2026-09-28)
Commit / PR: `2bc591e` (stage 4 code SHA; branch head at review start `b1bbcc9`)

## Stage 5 — Scenario coverage

### Live scenario map

| Scenario | Test(s) and file |
|---|---|
| SV1 | `Post_WithValidRequest_Returns201WithLocationIdAndNumber`, `ScheduleVersionEndpointsTests.cs`; `CreateScheduleVersion_WithValidCommand_PersistsRowsAndOneAuditEvent`, `CreateScheduleVersionHandlerTests.cs` |
| SV2 | `Get_ReturnsScheduleVersionResponseRecordNotEntity`, `GetServiceTimes_ReturnsStopsWithHHmmTimesAndNulls`, `GetServiceTimes_ForAServiceNotInTheVersion_Returns404NotInVersion`, `ScheduleVersionEndpointsTests.cs`; `GetScheduleVersion_ReturnsHeaderAndListedServicesWithCurrentValues`, `GetScheduleServiceTimes_ReturnsStopsInOrderWithStationsAndTimes`, `GetScheduleServiceTimes_ForAServiceNotInTheVersion_ReturnsNotInVersion`, `ScheduleVersionQueryHandlerTests.cs` |
| SV3 | `List_ReturnsPagedEnvelopeOrderedByNumberAndFiltersByStatus`, `ScheduleVersionEndpointsTests.cs`; `ListScheduleVersions_OrderedByNumberAndFilteredByStatus`, `ScheduleVersionQueryHandlerTests.cs` |
| SV4 | `Numbers_AreContiguousAcrossDiscardAndCancel`, `ScheduleVersionEndpointsTests.cs`; `CreateScheduleVersion_Numbers_AreContiguousAndNeverReused`, `CreateScheduleVersionHandlerTests.cs` |
| SV5 | `Post_WithMyanmarName_RoundTripsThroughGet`, `ScheduleVersionEndpointsTests.cs`; `CreateScheduleVersion_WithMyanmarName_RoundTripsExactly`, `CreateScheduleVersionHandlerTests.cs` |
| SV6 | `Post_WithInvalidTimes_Returns422WithErrorCode`, `ScheduleVersionEndpointsTests.cs`; `CreateDraft_WithUnexpectedTime_ReturnsStopTimeUnexpected`, `ScheduleVersionTests.cs` |
| SV7 | `Post_WithInvalidTimes_Returns422WithErrorCode`, `ScheduleVersionEndpointsTests.cs`; `CreateDraft_WithMissingOrRepeatedTimes_ReturnsStopTimesIncomplete`, `ScheduleVersionTests.cs` |
| SV8 | `Post_WithInvalidTimes_Returns422WithErrorCode`, `Post_WithInvalidBody_Returns400CommonValidationFailed`, `ScheduleVersionEndpointsTests.cs`; `CreateDraft_WithPositionBeyondTheStops_ReturnsStopNotInService`, `ScheduleVersionTests.cs` |
| SV9 | `Post_WithInvalidTimes_Returns422WithErrorCode`, `Post_WithEdgeTimesOrFullCircuit_Returns201`, `ScheduleVersionEndpointsTests.cs`; `CreateDraft_WithDepartureBeforeArrival_ReturnsDwellNegative`, `CreateDraft_WithZeroDwell_Succeeds`, `ScheduleVersionTests.cs` |
| SV10 | `Post_WithInvalidTimes_Returns422WithErrorCode`, `ScheduleVersionEndpointsTests.cs`; `CreateDraft_WithTimesNotIncreasing_ReturnsTimesNotIncreasing`, `ScheduleVersionTests.cs` |
| SV11 | `Post_WithBadTimeFormat_Returns400InvalidTimetableTime`, `ScheduleVersionEndpointsTests.cs`; `Parse_WithInvalidText_ReturnsInvalidTimetableTime`, `Parse_WithValidTime_ReturnsMinutes`, `TimetableTimeTests.cs` |
| SV12 | `Post_WithInvalidTimes_Returns422WithErrorCode`, `ScheduleVersionEndpointsTests.cs`; `CreateDraft_CrossingMidnight_ReturnsTimesNotIncreasing`, `ScheduleVersionTests.cs` |
| SV13 | `Post_WithEdgeTimesOrFullCircuit_Returns201`, `ScheduleVersionEndpointsTests.cs`; `CreateDraft_FullCircuit_TimesTheClosingStopLast`, `ScheduleVersionTests.cs` |
| SV14 | `Post_WithServiceErrors_Returns422`, `ScheduleVersionEndpointsTests.cs`; `CreateScheduleVersion_WithUnknownOrRepeatedService_ReturnsErrorAndWritesNothing`, `CreateScheduleVersionHandlerTests.cs` |
| SV15 | `Post_WithServiceErrors_Returns422`, `ScheduleVersionEndpointsTests.cs`; `CreateScheduleVersion_WithServiceNotEffectiveOnStartDate_ReturnsNotEffectiveAndWritesNothing`, `CreateScheduleVersionHandlerTests.cs` |
| SV16 | `Post_WithServiceEffectiveOnlyOnStartDate_Returns201`, `ScheduleVersionEndpointsTests.cs`; `CreateScheduleVersion_WithServiceEffectiveOnlyOnStartDateOrFromThePast_Creates`, `CreateScheduleVersionHandlerTests.cs` |
| SV17 | `Publish_AfterAListedServiceWasWithdrawn_Returns422NotEffective`, `ScheduleVersionEndpointsTests.cs`; `PublishScheduleVersion_WithServiceWithdrawnSinceCreation_ReturnsNotEffectiveAndWritesNothing`, `PublishScheduleVersionHandlerTests.cs` |
| SV18 | `Post_WithInvalidBody_Returns400CommonValidationFailed`, `Post_WithInvalidName_Returns400InvalidScheduleVersionName`, `ScheduleVersionEndpointsTests.cs`; `CreateScheduleVersion_WithInvalidName_ReturnsInvalidScheduleVersionName`, `CreateScheduleVersionHandlerTests.cs` |
| SV19 | `Post_WithStartBeforeToday_Returns422`, `Post_WithStartToday_Returns201`, `ScheduleVersionEndpointsTests.cs`; `CreateScheduleVersion_WithStartBeforeToday_ReturnsInPastAndWritesNothing`, `CreateScheduleVersion_WithStartToday_Creates`, `CreateScheduleVersionHandlerTests.cs` |
| SV20 | `Publish_ReturnsExpectedStatuses`, `ScheduleVersionEndpointsTests.cs`; `PublishScheduleVersion_AfterItsStartDatePassed_ReturnsInPastStaysDraftAndCanBeDiscarded`, `PublishScheduleVersionHandlerTests.cs` |
| SV21 | `Publish_ReturnsExpectedStatuses`, `ScheduleVersionEndpointsTests.cs`; `PublishScheduleVersion_OnItsStartDate_Succeeds`, `PublishScheduleVersionHandlerTests.cs` |
| SV22 | `Post_WithNoServices_Returns201`, `ScheduleVersionEndpointsTests.cs`; `CreateScheduleVersion_WithNoServices_CreatesHeaderOnlyAndSnapshotWithEmptyServices`, `CreateScheduleVersionHandlerTests.cs` |
| SV23 | `Publish_ReturnsExpectedStatuses`, `ScheduleVersionEndpointsTests.cs`; `PublishScheduleVersion_Draft_UpdatesStatusAndInstantAndWritesOneEvent`, `PublishScheduleVersionHandlerTests.cs` |
| SV24 | `Publish_WithAPublishedStartDate_Returns409ThenAfterCancel204`, `ScheduleVersionEndpointsTests.cs`; `PublishScheduleVersion_WithAPublishedStartDate_Returns409ThenSucceedsOnceThatIsCancelled`, `PublishScheduleVersionHandlerTests.cs` |
| SV25 | `Publish_ReturnsExpectedStatuses`, `ScheduleVersionEndpointsTests.cs`; `PublishScheduleVersion_WhenNotDraftOrUnknown_ReturnsNotDraftOrNotFound`, `PublishScheduleVersionHandlerTests.cs` |
| SV26 | `PublishScheduleVersion_TwoSameStartDateInParallel_OneSucceedsOneConflicts`, `PublishScheduleVersionHandlerTests.cs` |
| SV27 | `InForce_ReturnsTheVersionForEachDate`, `ScheduleVersionEndpointsTests.cs`; `GetInForce_BeforeTheFirstVersion_ReturnsNotInForce`, `ScheduleVersionQueryHandlerTests.cs` |
| SV28 | `InForce_ReturnsTheVersionForEachDate`, `ScheduleVersionEndpointsTests.cs`; `GetInForce_AcrossASupersession_ReturnsTheLatestStartOnOrBeforeTheDate`, `ScheduleVersionQueryHandlerTests.cs`; `InForceOn_ReturnsTheLatestStartOnOrBeforeTheDate`, `PublishedTimelineTests.cs` |
| SV29 | `InForce_ReturnsTheVersionForEachDate`, `ScheduleVersionEndpointsTests.cs`; `GetInForce_WithAnInsertedVersion_ReturnsItForItsRangeOnly`, `ScheduleVersionQueryHandlerTests.cs` |
| SV30 | `InForce_ReportsRunsOnDate`, `ScheduleVersionEndpointsTests.cs`; `GetInForce_ReportsRunsOnDatePerService`, `ScheduleVersionQueryHandlerTests.cs`; `RunsOn_ChecksThePeriodAndTheWeekday`, `ServiceRunningDayTests.cs` |
| SV31 | `Cancel_ReturnsExpectedStatuses`, `ScheduleVersionEndpointsTests.cs`; `CancelScheduleVersion_BeforeItsStart_SetsCancelledKeepsRowsAndWritesOneEvent`, `CancelScheduleVersionHandlerTests.cs` |
| SV32 | `Cancel_ThenInForceReturnsTheEarlierVersion`, `ScheduleVersionEndpointsTests.cs`; `GetInForce_AfterACancellation_ReturnsTheEarlierVersion`, `ScheduleVersionQueryHandlerTests.cs` |
| SV33 | `Cancel_ReturnsExpectedStatuses`, `ScheduleVersionEndpointsTests.cs`; `CancelScheduleVersion_OnOrAfterItsStart_ReturnsAlreadyEffectiveAndWritesNothing`, `CancelScheduleVersionHandlerTests.cs` |
| SV34 | `Cancel_ReturnsExpectedStatuses`, `ScheduleVersionEndpointsTests.cs`; `CancelScheduleVersion_WhenNotPublishedOrUnknown_ReturnsNotPublishedOrNotFound`, `CancelScheduleVersionHandlerTests.cs` |
| SV35 | `Discard_ReturnsExpectedStatuses`, `ScheduleVersionEndpointsTests.cs`; `DiscardScheduleVersion_Draft_SetsDiscardedKeepsRowsAndWritesOneEvent`, `DiscardScheduleVersionHandlerTests.cs` |
| SV36 | `Withdraw_WhileAPublishedVersionListsTheService_Returns422InPublishedVersion`, `Withdraw_WithAPastDate_Returns422DateInPastFirst`, `ScheduleVersionEndpointsTests.cs`; `WithdrawService_WhileAPublishedVersionListsIt_Returns422AndWritesNothing`, `WithdrawService_WithPastDateAndAListingVersion_ReturnsDateInPastFirst`, `WithdrawServiceScheduleGuardTests.cs` |
| SV37 | `DroppingAService_PublishWithoutItThenWithdraw_Returns204AndTheDayBefore422`, `ScheduleVersionEndpointsTests.cs`; `WithdrawService_FromTheStartOfTheVersionThatDropsIt_Succeeds`, `WithdrawService_TheDayBefore_Returns422`, `WithdrawServiceScheduleGuardTests.cs`; `AppliesOnOrAfter_UsesTheSuccessorsStartDate`, `PublishedTimelineTests.cs` |
| SV38 | `Withdraw_ListedOnlyByDraftDiscardedOrCancelled_Returns204`, `ScheduleVersionEndpointsTests.cs`; `WithdrawService_ListedOnlyByDraftDiscardedOrCancelledVersions_Succeeds`, `WithdrawServiceScheduleGuardTests.cs` |
| SV39 | `Withdraw_ListedOnlyByASupersededPastVersion_Returns204`, `ScheduleVersionEndpointsTests.cs`; `WithdrawService_ListedOnlyByASupersededPastVersion_Succeeds`, `WithdrawServiceScheduleGuardTests.cs` |
| SV40 | `PublishAndWithdraw_PublishFirst_WithdrawReturns422InPublishedVersion`, `PublishAndWithdraw_WithdrawFirst_PublishReturns422NotEffective`, `ScheduleLockTests.cs` |
| SV41 | `CancelAndWithdraw_CancelFirst_WithdrawReturns422InPublishedVersion`, `CancelAndWithdraw_WithdrawFirst_BothSucceedAndServiceStaysWithdrawn`, `ScheduleLockTests.cs` |
| SV42 | `PublishAndDiscard_SameDraft_ForcedBothOrders_OneSucceedsOtherNotDraft`, `CreateScheduleVersion_InParallel_GetDistinctConsecutiveNumbers`, `ScheduleLockTests.cs`; `CreateScheduleVersion_Numbers_AreContiguousAndNeverReused`, `CreateScheduleVersionHandlerTests.cs` |
| SV43 | `AnyScheduleEndpoint_Anonymous_Returns401`, `ScheduleVersionEndpointsTests.cs` |
| SV44 | `AbsentScheduleEndpoints_AreNotRouted`, `ScheduleVersionEndpointsTests.cs` |
| SV45 | `WriteEndpoints_WithOnlySchedulesRead_Return403`, `ScheduleEndpoints_WithOnlyServiceRouteOrStationPermissions_Return403`, `Withdraw_WithOnlySchedulePermissions_Returns403`, `ScheduleVersionEndpointsTests.cs` |
| SV46 | `EveryRole_HasExactlyTheSeededScheduleRights`, `RailwayAdministrator_CreatesPublishesAndCancels_AuditedAsSchedulesManage`, `SchedulePermissionGrantTests.cs` |
| SV47 | `Post_WithBodyOverTheLimit_Returns413WithoutInternals`, `Post_OneOverACap_Returns400ValidationFailed`, `Post_ExactlyAtTheCaps_IsUnderTheLimitAndReachesTheHandler`, `Post_LargeBodyAbuse_IsRefusedBeforeAnyDatabaseWork`, `ScheduleVersionRequestLimitTests.cs` |
| SV48 | `List_WithPageSizeAbove200_Returns400InvalidPageRequest`, `InForce_WithMissingOrMalformedDate_Returns400ValidationFailed`, `ScheduleVersionEndpointsTests.cs` |
| SV49 | `CreateScheduleVersion_AuditActor_ComesFromCurrentUserOnly`, `CreateScheduleVersionHandlerTests.cs`; `Post_WhenRequestTriesToSupplyActorFields_RecordsTheAuthenticatedActor`, `RefusedRequests_WriteNoEvent`, `ScheduleVersionEndpointsTests.cs` |
| SV50 | `CreateScheduleVersion_WithInvalidTimes_ReturnsErrorAndWritesNothing`, `CreateScheduleVersion_WhenNumberIsTakenBehindTheLock_FailsAndWritesNothing`, `CreateScheduleVersionHandlerTests.cs`; endpoint create-refusal rows call `AssertNoVersionWrittenAsync`, `ScheduleVersionEndpointsTests.cs` |
| SV51 | `ApplicationCredential_HasExactlyTheScheduleGrants`, `ApplicationCredential_CanMoveAVersionsStatusButNotRewriteIt`, `DatabasePrivilegeTests.cs`; `Migrate_FromF004Schema_CreatesScheduleObjectsGrantsAndSeed`, `ScheduleMigrationTests.cs` |
| SV52 | `ApplicationCredential_ScheduleBackstops_RejectInvalidRows`, `ApplicationCredential_ScheduleBackstops_AllowSharedStartDatesOutsidePublished`, `ApplicationCredential_CannotInsertAnEntryOrStopTimeNamingNoServiceOrStop`, `DatabasePrivilegeTests.cs` |
| SV53 | `InForce_WithAnEmptyVersion_Returns200WithNoServices`, `ScheduleVersionEndpointsTests.cs`; `GetInForce_WithAnEmptyVersion_ReturnsItWithNoServices`, `ScheduleVersionQueryHandlerTests.cs`; `PublishScheduleVersion_Empty_IsInForceWithNoServices`, `PublishScheduleVersionHandlerTests.cs` |
| SV54 | `WithdrawThenCancel_ServiceStaysWithdrawnAndInForceSaysNotRunning`, `ScheduleVersionEndpointsTests.cs`; `WithdrawThenCancel_TheWithdrawalStaysAndInForceReportsNotRunning`, `WithdrawServiceScheduleGuardTests.cs`; `CancelAndWithdraw_WithdrawFirst_BothSucceedAndServiceStaysWithdrawn`, `ScheduleLockTests.cs` |
| SV55 | `Post_WithNoServicesStartingToday_Returns422EmptyNotInFuture`, `ScheduleVersionEndpointsTests.cs`; `CreateScheduleVersion_WithNoServicesStartingToday_ReturnsEmptyNotInFutureAndWritesNothing`, `CreateScheduleVersionHandlerTests.cs`; `Publish_EmptyVersionWhoseStartHasBecomeToday_ReturnsEmptyNotInFutureAndWritesNothing`, `PublishScheduleVersionHandlerTests.cs`; matching `ScheduleVersionInputTests` and `ScheduleVersionTests` cases |
| SV56 | `EmptyVersionStartingTomorrow_PublishesThenCancels`, `ScheduleVersionEndpointsTests.cs`; `PublishScheduleVersion_EmptyStartingTomorrow_PublishesAndCanBeCancelled`, `PublishScheduleVersionHandlerTests.cs`; matching `ScheduleVersionTests` case |

### Rules without a single standalone scenario

| Rule | Coverage |
|---|---|
| R10–R14, stop-time shape, whole-minute precision, dwell, strict ordering, no passing times, one set per service | `ScheduleVersionTests.CreateDraft_WithUnexpectedTime_ReturnsStopTimeUnexpected`, `CreateDraft_WithMissingOrRepeatedTimes_ReturnsStopTimesIncomplete`, `CreateDraft_WithDepartureBeforeArrival_ReturnsDwellNegative`, `CreateDraft_WithZeroDwell_Succeeds`, `CreateDraft_WithTimesNotIncreasing_ReturnsTimesNotIncreasing`; API rows SV6–SV13 in `ScheduleVersionEndpointsTests.cs` |
| R15–R16 and ADR-0027, 0–1439, no midnight crossing, exact `HH:mm` | `TimetableTimeTests.Parse_WithValidTime_ReturnsMinutes`, `Parse_WithInvalidText_ReturnsInvalidTimetableTime`, `ScheduleVersionTests.CreateDraft_FromMidnightToLastMinute_Succeeds`, `CreateDraft_CrossingMidnight_ReturnsTimesNotIncreasing`; API SV11–SV12 in `ScheduleVersionEndpointsTests.cs` |
| R19 published-only withdrawal guard and successor boundary | SV36–SV39 above; `WithdrawServiceScheduleGuardTests.WithdrawService_ListedOnlyByDraftDiscardedOrCancelledVersions_Succeeds`; query filter is in `PublishedTimelineReader.LoadCoverageAsync` |
| R33 Asia/Yangon calendar boundary | `CreateScheduleVersion_AtYangonMidnight_UsesTheYangonDateAsToday`, `CreateScheduleVersionHandlerTests.cs` |
| R38 generated identifiers | `SequentialGuidGenerator_ReturnsDistinctNonEmptyIds`, `StationModelTests.cs`; endpoint create cases assert the returned id and persisted key |
| R39 same-save audit and no event on refusal | create/publish/cancel/discard handler success tests assert one event; SV36, SV49 and SV50 refusal/no-write assertions; `ScheduleVersionTransitionSqlTests` checks the status/timestamp update shape |
| R41 required controls: 2 MiB, 250 services, 200 per service, 10,000 total | SV47; `ScheduleVersionRequestLimitTests.Limits_Are2MiBAndTheThreeCaps`, exact-cap, one-over-each-cap, and large-body abuse cases |
| R46 Timetable-wide lock and lock order | SV26, SV40–SV42; `ScheduleLockTests` forced orders, 25-round no-deadlock race, resource-order assertions; `ApplicationCredential_CanTakeTheScheduleVersionsApplock` |
| R47 concurrency-token backstop | `PublishScheduleVersion_WhenStatusChangesBehindTheLock_Returns409ChangedConcurrentlyAndWritesNothing`, `CancelScheduleVersion_WhenStatusChangesBehindTheLock_Returns409ChangedConcurrently`, `DiscardScheduleVersion_WhenStatusChangesBehindTheLock_Returns409ChangedConcurrently` |
| R48 `runsOnDate` | SV30, SV53–SV54; `ServiceRunningDayTests.RunsOn_ChecksThePeriodAndTheWeekday` |
| R50 empty-version future-only rule (Amendments 1–2) | SV55–SV56; `ScheduleVersionInputTests.Parse_WithNoServicesStartingToday_ReturnsEmptyNotInFuture`, `ScheduleVersionTests.Publish_EmptyVersionWhoseStartHasBecomeToday_ReturnsEmptyNotInFutureAndChangesNothing`, API create and publish rows |

### Stage 5 result

Static inspection found a named test for every live SV1–SV56 scenario and for the additional rules listed above. One test was strengthened during this review: `ScheduleLockTests.PublishAndWithdraw_InParallelRepeatedly_NeverDeadlockAndEndInASerialOutcome` now first holds the real transaction-owned schedule applock and asserts a publisher cannot pass it. The prior test's unforced 25 rounds could pass with the lock removed, so this was a real coverage gap.

Evidence run in this review:

- Pinned SDK: `C:\dn302\dotnet.exe` (10.0.302).
- `dotnet test tests/YCR.Domain.Tests/YCR.Domain.Tests.csproj --no-restore`: 363 passed, 0 failed, 0 skipped.
- Time-order mutation (`<=` to `<` in `ScheduleVersion.cs`): the full domain project failed 2 equality cases in `ScheduleVersionTests.CreateDraft_WithTimesNotIncreasing_ReturnsTimesNotIncreasing` (2 failed, 361 passed, 0 skipped). Predicate restored; rerun passed 363/363, 0 skipped.
- Lock mutation: `SqlServerScheduleVersionsLock.AcquireAsync` was changed to a transaction check plus no-op. `ScheduleLockTests` then failed 7/13, including both forced orders and parallel numbering. The strengthened no-deadlock method also failed 1/1 at `Publishing passed a held Timetable-wide lock.` The real applock was restored before continuing.
- Withdrawal mutation: the R19 `coverage.AppliesOnOrAfter` guard was removed. `WithdrawServiceScheduleGuardTests` failed 2/7 (`WhileAPublishedVersionListsIt` and `TheDayBefore`); the guard was restored.
- Total-cap mutation: `MaxStopTimesPerVersion` was changed to 10,001. The 250-service/10,001-stop-time request (with no per-service cap violation) returned 422 instead of the expected 400, so the cap test failed 1/3. Restored to 10,000; the cap theory then passed 3/3.
- Full suite: `dotnet test YCR.sln --no-restore --max-parallel-test-modules 1` passed **1,624/1,624, 0 skipped** in 41m 09s. Application 12m 32s, architecture 15s, domain 9s, integration 1m 05s, infrastructure 4m 17s, API 22m 45s.
- The only permanent test addition is the deterministic held-lock assertion above. No production changes remain.

Verdict: **Ready**. Every live scenario and listed rule has named coverage; the one weak lock test was strengthened and mutation-proven; all requested mutation checks were caught after restoring each rule; the full suite is green with 0 skipped.

## Stage 6 — Code review

Reviewer: codex
Reviewed SHA: `2bc591e` (stage-4 implementation; stage-5 test/report commit is `ae32ba1`)

### Review scope and evidence

- **Lock order:** every schedule writer starts a transaction, then acquires `timetable.ScheduleVersions`, then performs deciding reads and one save: `CreateScheduleVersionHandler.cs:51-87`, `PublishScheduleVersionHandler.cs:37-69`, `CancelScheduleVersionHandler.cs:32-58`, and `DiscardScheduleVersionHandler.cs:29-55`. `WithdrawServiceHandler.cs:63-97` takes the service-code lock first, then the schedule lock at line 79, and reads coverage only after it. No other handler that takes the schedule lock depends on `IServiceCodeLock`; the stage-5 `ScheduleHandlers_DoNotDependOnTheServiceCodeLock` test asserts this. No row lock is requested before either applock.
- **Lock implementation and errors:** `SqlServerScheduleVersionsLock.cs:26-50` requires an open transaction, uses an exclusive transaction-owned `sp_getapplock` with a 30-second timeout, and throws on any negative return. The handlers do not translate timeout/deadlock SQL errors to business errors, so the API's opaque 500 path applies. The stage-5 no-op mutation failed the forced races and the strengthened held-lock assertion, proving the lock calls are live.
- **Transaction ownership and one-save pattern:** each create/publish/cancel/discard/withdraw path has one explicit transaction and one `SaveChangesAsync`; transition SQL is asserted by `ScheduleVersionTransitionSqlTests.Transitions_UpdateOnlyStatusAndOneInstantAndNoChildRows`. Refused outcomes return before save and transaction disposal rolls them back.
- **F-004 withdrawal:** the old date/past/shortening checks precede the new R19 guard in `Service.cs`; the published-only filter is in `PublishedTimelineReader.LoadCoverageAsync`. SV36–SV39 and SV54, plus the SQL-backed guard mutation (2/7 failures with the guard removed), cover published, boundary, draft/discarded/cancelled, superseded, and cancellation-revival cases. F-004 tests remained strict and the full suite passed.
- **Lifecycle and concurrency:** `ScheduleVersion` has only create, publish, discard and cancel transitions; database status/timestamp checks are declared in both the EF model and `20260927070804_Timetable_CreateScheduleVersions`. The `Status` concurrency token maps bypassing writers to the documented 409 tests. Published start-date uniqueness is the filtered index `UX_ScheduleVersions_EffectiveFrom_Published`; numbering is protected by `UX_ScheduleVersions_Number` plus the lock.
- **In-force query and indexes:** `PublishedTimelineReader` filters `Status = Published` for both timeline and withdrawal coverage. `ScheduleVersionConfiguration` declares the filtered unique index and `IX_ScheduleVersionServices_ServiceId`; model tests pin the exact index set. Supersession, insertion, cancellation and `runsOnDate` are covered by SV27–SV32 and SV53–SV54.
- **Migrations:** the three migrations match the model and plan: timetable tables/checks/indexes/FKs with child-first `Down`, ten schedule permission seed rows with exact `Down`, and the `ycr_app` grants with reverse `REVOKE`. `ScheduleModelTests`, `ScheduleMigrationTests`, `DatabasePrivilegeTests`, and the full suite passed; the progress log records `has-pending-model-changes` clean during implementation.
- **Body limits and caps:** `RequestSizeLimitAttribute` sets 2 MiB before binding; the validator enforces 250 services, 200 stop times per service, and 10,000 total. `ScheduleVersionRequestLimitTests` runs on real Kestrel for declared-length and chunked over-limit bodies, exact caps, malformed JSON, and each cap. The full suite passed; V12's chunked case remains a real-Kestrel test. The 10,001-total mutation failed its 250-service case and passed after restoring 10,000.
- **Test-host stability:** the progress log's known V7 intermittent start failure did not recur in the full run; API tests passed in 22m 45s.

### Findings

None. No Critical, High, Medium or Low code-review finding was identified against `2bc591e`. The only stage-5 change was a test-strengthening assertion committed separately at `ae32ba1`; it does not alter the reviewed production SHA.

### Verdict

**Ready.** The reviewed implementation satisfies the requested lock, transaction, lifecycle, query, migration, limit and test-quality checks. Full solution evidence: `dotnet test YCR.sln --no-restore --max-parallel-test-modules 1` — 1,624 passed, 0 failed, 0 skipped.

## Stage 7 — Security review

Not started. T-056 is blocked and the task ledger requires the stages in order.

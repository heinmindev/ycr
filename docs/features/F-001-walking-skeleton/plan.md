# Plan: F-001 Walking skeleton — solution, infrastructure and the `Station` reference slice

Spec: `spec.md` — **Approved (hein, 2026-09-20)**, subject to the waiver in its §Blocked behaviour.
Stage: 3 (PLAN), `docs/workflows/02-feature-development.md`. Task T-003.
Binding inputs: ADR-0004, ADR-0005, ADR-0006, ADR-0012, ADR-0016, ADR-0017, ADR-0018, ADR-0019, ADR-0020, ADR-0021; `docs/20-coding-conventions.md`; `docs/21-definition-of-done.md`.

**Ten decisions (P1–P10) need a human answer at the ⛔ stop before stage 4 starts.** They are collected in §Decisions needed. Four of them (P1, P2, P5, P6) change the file inventory below, so the inventory is written against the recommended option and marked where it would change.

---

## Understanding

F-001 turns `docs/20` §3's illustrative `CreateStation` slice into executable, tested code, and builds the infrastructure that slice needs: twelve projects, module-scoped persistence, application-generated sequential GUIDs, Result-to-ProblemDetails mapping, permission-based authorization with a test-only authentication handler, a SQL Server 2022 audit ledger, architecture tests with teeth, docker-compose, and a GitHub Actions pipeline.

The station business rules it encodes are provisional (spec R3, R4, R8) under an explicit tech-lead waiver, confined to two value objects and the permission constants. Everything else in the feature is engineering.

What makes this feature unusual: the *pattern* is the deliverable. A later agent will copy `CreateStation` far more faithfully than it will read ADRs, so an error here propagates. That argues for spending the extra step on the architecture tests and against leaving anything "to be tidied later".

---

## Facts / Assumptions / Open questions

**FACT — `README.md` §Technology:** C# / ASP.NET Core 10, EF Core, SQL Server, Docker.
**FACT — spec §0.2 D2:** the repository has no `src/`, `tests/`, solution, compose file, CI workflow or build configuration. Everything in §Affected modules and files is new.
**FACT — ADR-0017 §5:** CI must execute the ledger DDL and a smoke test against a pinned SQL Server 2022 container image, and the migration must fail loudly on unsupported versions or editions.
**FACT — ADR-0012 §Enforcement:** architecture tests must fail the build on forbidden cross-module context/domain references.
**FACT — ADR-0020 §Decision items 4–5:** no `AuthenticationHandler<>` subtype in `src/`, enforced by an architecture test, plus a startup scheme allowlist guard as defence in depth.
**FACT — ADR-0021:** the audit column shape, its three `ISJSON` check constraints and its two nonclustered indexes.
**FACT — `docs/20` §8:** new NuGet packages require a stated reason in the plan. See §New packages.

**ASSUMPTION (provisional, approved by hein 2026-09-20; replace when OQ26/OQ27/OQ28 answered)** — spec R3, R4, R8. This plan implements them only in `StationCode`, `BilingualName` and `Permissions`, per the waiver.

**ASSUMPTION (plan-level, needs confirmation at the ⛔):** the ten items in §Decisions needed. Each has a recommendation; none is implemented before it is answered.

**VERIFY (ADR-0017 §Blocked behaviour, spec E6), during step 7:** that the pinned image's edition supports ledger tables; that `CHECK` constraints and nonclustered indexes are permitted on an append-only ledger table; and that EF Core can `INSERT` into a ledger table whose generated columns it does not map. If any of these fails, stop and return to stage 2 rather than substituting a normal table (AGENTS.md rule 8).

**OPEN QUESTION — OQ26, OQ27, OQ28:** still open with Myanma Railways; T-014 is the release gate. They do not block stage 4.

**OPEN QUESTION — spec E3:** the repository has no git remote, so the GitHub Actions workflow is committed but cannot run until one exists. Step 13 delivers the file; proving it green needs the remote.

---

## Affected modules and files

Modules touched: `Network` (the slice), `Audit` (ledger write path), plus solution-wide `Common`. No other module gets more than an empty folder.

### Solution and build configuration

| File | New / Changed | Why |
|---|---|---|
| `YCR.sln` | New | AGENTS.md §Commands requires `dotnet build YCR.sln` to work |
| `global.json` | New | Pin the SDK so CI and local builds agree (E2) |
| `Directory.Build.props` | New | `net10.0`, nullable enabled, `TreatWarningsAsErrors`, deterministic builds, one place per E2 |
| `Directory.Packages.props` | New | Central package management; one version per package across twelve projects |
| `.gitignore` | New | None exists (D2) |
| `.editorconfig` | New | E2; also fixes source encoding to UTF-8 so Myanmar test fixtures survive |
| `docker-compose.yml` | New | AGENTS.md §Commands; SQL Server 2022 pinned by digest (E4) |
| `docker/sqlserver/init-principals.sql` | New | Creates the `ycr_app` login/user and adds it to the role the migration defines (E7, P9) |
| `.github/workflows/ci.yml` | New | ADR-0017 §5; E3 |

### `src/`

| File | New / Changed | Why |
|---|---|---|
| `src/YCR.Domain/Common/Result.cs`, `Error.cs`, `ErrorType.cs` | New | ADR-0004 §Errors; in-house and small, in `YCR.Domain.Common` |
| `src/YCR.Domain/Common/AggregateRoot.cs`, `Entity.cs`, `IDomainEvent.cs` | New | `docs/20` §3 reference slice calls `Raise(...)` |
| `src/YCR.Domain/Network/Station.cs` | New | The aggregate, exactly as `docs/20` §3 defines it |
| `src/YCR.Domain/Network/StationCode.cs` | New | Value object. **Sole home of provisional rule R3** |
| `src/YCR.Domain/Network/BilingualName.cs` | New | Value object. **Sole home of provisional rule R4** |
| `src/YCR.Domain/Network/NetworkErrors.cs` | New | `docs/20` §3; adds `StationNotFound` for spec S8 |
| `src/YCR.Domain/Network/StationDeactivated.cs` | New | Domain event raised by `Station.Deactivate()` |
| `src/YCR.Application/Common/Abstractions/IIdGenerator.cs` | New | ADR-0006 §4 amendment |
| `src/YCR.Application/Common/Abstractions/IAuditWriter.cs` | New | ADR-0017, ADR-0021 |
| `src/YCR.Application/Common/Abstractions/ICurrentUser.cs` | New | Supplies server-side actor fields; ADR-0017 §2 forbids taking them from the request |
| `src/YCR.Application/Common/Authorization/Permissions.cs` | New | `docs/20` §1 fixes this exact path. **Sole home of provisional rule R8** |
| `src/YCR.Application/Common/Pagination/PagedResult.cs` | New | `docs/20` §4 pagination envelope |
| `src/YCR.Application/Network/INetworkDbContext.cs` | New | ADR-0012 §Decision item 2. **Path not covered by `docs/20` §1 — see P10** |
| `src/YCR.Application/Network/CreateStation/CreateStationCommand.cs`, `CreateStationHandler.cs` | New | ADR-0004 handler per use case |
| `src/YCR.Application/Network/DeactivateStation/DeactivateStationCommand.cs`, `DeactivateStationHandler.cs` | New | ″ |
| `src/YCR.Application/Network/GetStation/GetStationQuery.cs`, `GetStationHandler.cs`, `StationDto.cs` | New | ADR-0004: queries project straight to DTOs with `AsNoTracking()`. **See P5** |
| `src/YCR.Application/Network/ListStations/ListStationsQuery.cs`, `ListStationsHandler.cs` | New | ″ |
| `src/YCR.Application/DependencyInjection.cs` | New | Handler registration by assembly scanning (ADR-0004 §Consequences) |
| `src/YCR.Infrastructure/Persistence/YcrDbContext.cs` | New | One concrete context implementing every module interface (ADR-0012 item 3) |
| `src/YCR.Infrastructure/Persistence/Configurations/Network/StationConfiguration.cs` | New | `docs/20` §1 fixes this path |
| `src/YCR.Infrastructure/Persistence/Configurations/Audit/AuditEventConfiguration.cs` | New | Maps the ledger table with `ExcludeFromMigrations()` so EF inserts but never creates or alters it (ADR-0017 §6) |
| `src/YCR.Infrastructure/Persistence/Migrations/20260920_Network_CreateStations.cs` | New | `docs/20` §6 naming |
| `src/YCR.Infrastructure/Persistence/Migrations/20260920_Audit_CreateAuditEventsLedger.cs` | New | Raw SQL ledger DDL + version/edition guard + constraints + indexes (ADR-0017, ADR-0021) |
| `src/YCR.Infrastructure/Persistence/Migrations/20260920_Security_AppDatabaseRole.cs` | New | Least-privilege role (E7, P9) |
| `src/YCR.Infrastructure/Identifiers/SqlServerSequentialGuidIdGenerator.cs` | New | Named by the ADR-0006 amendment |
| `src/YCR.Infrastructure/Audit/AuditWriter.cs` | New | Writes `audit.AuditEvents` in the caller's `SaveChangesAsync` |
| `src/YCR.Infrastructure/DependencyInjection.cs` | New | Registers `YcrDbContext` once, then every module interface to that same scoped instance (ADR-0012 item 3) |
| `src/YCR.Api/Program.cs` | New | Composition root; `public partial class Program` so `WebApplicationFactory` can reach it |
| `src/YCR.Api/Common/ResultExtensions.cs` | New | `Result` → `IResult`, the ADR-0004 status mapping in one place |
| `src/YCR.Api/Common/ProblemDetailsSetup.cs` | New | RFC 9457 with `errorCode` and `traceId`; global handler yielding 500 with no internals (spec S25) |
| `src/YCR.Api/Common/ValidationFilter.cs` | New | `docs/20` §3 endpoint filter |
| `src/YCR.Api/Common/Authorization/PermissionRequirement.cs`, `PermissionAuthorizationHandler.cs`, `PermissionPolicyProvider.cs` | New | Turns `stations.manage` into a policy without a registration per permission |
| `src/YCR.Api/Common/Authentication/AuthenticationSchemeGuard.cs` | New | ADR-0020 item 5, the defence-in-depth allowlist check |
| `src/YCR.Api/Contracts/Network/CreateStationRequest.cs`, `CreateStationRequestValidator.cs`, `StationResponse.cs`, `CreateStationResponse.cs` | New | `docs/20` §2 naming |
| `src/YCR.Api/Endpoints/Network/StationEndpoints.cs` | New | `docs/20` §1 fixes this path |
| `src/YCR.Api/Endpoints/Health/HealthEndpoints.cs` | New | `docs/02` §Reliability; the only `.AllowAnonymous()` in the feature |
| `src/YCR.Worker/Program.cs` | New | Empty host. `docs/06` lists the project; nothing in F-001 uses it (spec §9) |
| `src/YCR.DbMigrator/Program.cs` | New | Separate migration step under the migrator credential (E7). **Extends `docs/06` — see P1** |

Empty module folders (`Identity`, `Timetable`, `Fare`, `Ticketing`, `Payments`, `Operations`, `Reporting`, `Audit`) are **not** created speculatively in Domain and Application. They appear when their first slice does; an empty folder tree teaches nothing and git does not track it.

### `tests/`

| File | New / Changed | Why |
|---|---|---|
| `tests/YCR.TestSupport/SqlServerContainerFixture.cs`, `PinnedImage.cs` | New | One pinned digest (E4) shared by four test projects. **Extends `docs/06` — see P2** |
| `tests/YCR.Domain.Tests/Network/StationTests.cs`, `StationCodeTests.cs`, `BilingualNameTests.cs` | New | Invariants and transitions (`docs/21` §Tests) |
| `tests/YCR.Application.Tests/Network/CreateStationHandlerTests.cs`, `DeactivateStationHandlerTests.cs`, `GetStationHandlerTests.cs`, `ListStationsHandlerTests.cs` | New | Handler behaviour against real SQL Server (`docs/20` §3) |
| `tests/YCR.Infrastructure.Tests/Persistence/LedgerMigrationTests.cs`, `ModuleContextScopeTests.cs`, `DatabasePrivilegeTests.cs` | New | Spec S17–S19, S22 |
| `tests/YCR.Infrastructure.Tests/Identifiers/SequentialGuidFragmentationTests.cs` | New | ADR-0006 REQUIRED CONTROL (spec S23) |
| `tests/YCR.Api.Tests/Authentication/TestAuthHandler.cs`, `YcrApiFactory.cs` | New | ADR-0020 item 2; registered only via `ConfigureTestServices` |
| `tests/YCR.Api.Tests/Network/StationEndpointsTests.cs` | New | Spec S1–S12 |
| `tests/YCR.Api.Tests/Common/ProblemDetailsTests.cs`, `HealthEndpointsTests.cs`, `AuthenticationSchemeGuardTests.cs` | New | Spec S24, S25, S21b |
| `tests/YCR.ArchitectureTests/ModuleBoundaryTests.cs`, `AuthenticationHandlerTests.cs`, `ApiSurfaceTests.cs` | New | ADR-0012 §Enforcement; ADR-0020 item 4; spec S16 |
| `tests/YCR.ArchitectureTests/Violations/*.cs` | New | Deliberate violations in the test assembly, used to prove the rules have teeth (`docs/21` §Code "negative cases") |
| `tests/YCR.IntegrationTests/` | New, empty | `docs/06` lists it; F-001 has no end-to-end journey to test yet (C4) |

---

## Domain changes

One aggregate, two value objects, one domain event, one error class — all in `YCR.Domain.Network`, all as `docs/20` §3 specifies.

- `Station` — `Create(id, code, name, nowUtc)` returns an active station; `Deactivate()` returns `NetworkErrors.StationAlreadyInactive` when already inactive, otherwise flips `IsActive` and raises `StationDeactivated`. No other behaviour.
- `StationCode` — `Create(string)` returns `Result<StationCode>`, trimming then validating `^[A-Z0-9]{2,10}$` (R3). `From(string)` is the unvalidated EF materialisation path. The provisional label goes in an XML doc comment on the type, per the waiver's term 2.
- `BilingualName` — `Create(en, my)` returns `Result<BilingualName>`, trimming then requiring both to be 1–100 characters (R4). Same XML doc comment obligation.
- `StationDeactivated(Guid StationId)` — raised, collected on the aggregate, **not dispatched** in F-001 (P6).

`AggregateRoot` holds a domain-event list and `Raise(...)`. Nothing consumes events yet.

---

## DB changes

Three migrations, applied in order, all run by `YCR.DbMigrator` under the **migrator** credential. `docs/workflows/04-database-change.md` review applies to each.

### `20260920_Network_CreateStations`

Creates schema `network` and table `network.Stations` exactly as spec §7 defines it: `Id uniqueidentifier` PK (clustered, application-assigned), `Code nvarchar(10)` with a unique index, `NameEn`/`NameMy nvarchar(100)` not null, `IsActive bit` not null, `CreatedAtUtc datetimeoffset(3)` not null.

The unique index on `Code` is the concurrency authority (R7) and is also what enforces "codes are never reused" (R3), because deactivated rows are retained rather than deleted. Data impact: none, the table is new. `Down()` drops the table and schema.

### `20260920_Audit_CreateAuditEventsLedger`

Raw SQL, per ADR-0017 item 1. In order:

1. **Guard first.** Check `SERVERPROPERTY('ProductMajorVersion') >= 16` and that ledger objects are creatable in this edition; `THROW` with a message naming the detected version and edition otherwise (ADR-0017 item 5, spec S19). The guard runs before any DDL so a failure leaves nothing half-created.
2. Create schema `audit`.
3. Create `audit.AuditEvents` as an **append-only ledger table**, with the fourteen columns of ADR-0021 §Columns, including nullable `SubjectId`, `ActorRole nvarchar(1000)` and `AuthorizedByPermission nvarchar(100)`.
4. Create the three check constraints `CK_AuditEvents_BeforeJson`, `CK_AuditEvents_AfterJson`, `CK_AuditEvents_ActorRole`, each of the form `<col> IS NULL OR ISJSON(<col>) = 1`.
5. Create the two nonclustered indexes `IX_AuditEvents_Subject` on `(SubjectType, SubjectId)` and `IX_AuditEvents_OccurredAtUtc`.

Steps 3–5 are one migration because ADR-0021 decision item 5 requires the constraints to exist from the table's creation.

**`Down()` throws.** Dropping an append-only ledger table would destroy tamper-evident history, and ADR-0017 item 6 forbids migrations that silently convert or drop a ledger table. Rolling this back is an operational decision with a deliberate manual procedure, not something a `dotnet ef` command should offer. See P7.

### `20260920_Security_AppDatabaseRole`

Creates database role `ycr_app` and grants it exactly: `SELECT, INSERT, UPDATE` on `network.Stations`; `INSERT, SELECT` on `audit.AuditEvents` (ADR-0017 item 3); nothing else, and no DDL anywhere. `UPDATE` on `Stations` is needed by `DeactivateStation`; the audit schema deliberately gets no `UPDATE` or `DELETE`.

The role is in the migration because it is schema-shaped and belongs with the objects it grants on. The **login and user**, which need a credential, are created by environment provisioning — `docker/sqlserver/init-principals.sql` locally and a CI step — not by a migration, so no secret ever enters a migration file (P9).

---

## API changes

### Endpoint inventory

| Method | Path | Permission | Idempotency | Response / errors |
|---|---|---|---|---|
| POST | `/api/v1/stations` | `stations.manage` | No (R11) | `201` + `CreateStationResponse { id }`, `Location` header · `400` validation · `401` · `403` · `409 Network.StationCodeAlreadyExists` |
| POST | `/api/v1/stations/{id}/deactivate` | `stations.manage` | No (R11) | `204` · `401` · `403` · `404 Network.StationNotFound` · `422 Network.StationAlreadyInactive` |
| GET | `/api/v1/stations/{id}` | `stations.read` | n/a | `200` + `StationResponse` · `401` · `403` · `404 Network.StationNotFound` |
| GET | `/api/v1/stations` | `stations.read` | n/a | `200` + `{ items, page, pageSize, totalCount }` · `400` (`pageSize` > 200) · `401` · `403` |
| GET | `/health/live` | `.AllowAnonymous()` | n/a | `200` |
| GET | `/health/ready` | `.AllowAnonymous()` | n/a | `200` / `503` |

Health endpoints sit outside `/api/v1` because they are infrastructure probes, not versioned API surface; each carries the comment `docs/20` §4 requires.

Errors are RFC 9457 ProblemDetails with `errorCode` and `traceId`, produced in exactly one place (`ResultExtensions` + `ProblemDetailsSetup`) so no endpoint hand-rolls a status code. The `ErrorType` → status mapping is ADR-0004's: `Validation` 400, `Unauthorized` 401, `Forbidden` 403, `NotFound` 404, `Conflict` 409, `BusinessRule` 422.

`CreateStationHandler` pre-checks the code for a friendly `409`, and also catches the unique-index violation and maps it to the same `409` (R7, ADR-0004 §Errors). The pre-check is a courtesy; the index is the authority.

---

## Security impact

| Item | Treatment |
|---|---|
| Permissions | `stations.manage` and `stations.read` (R8), each on its endpoint via `.RequireAuthorization(...)`. `docs/10` already carries both in its inventory. **No role grants are seeded** (OQ28), so `docs/10`'s role table is not touched. |
| Authentication | ADR-0020: no handler in `src/`; test handler only in `tests/YCR.Api.Tests` via `ConfigureTestServices`; architecture test (S21a) plus startup allowlist guard (S21b). |
| Database least privilege | E7: the application credential holds the `ycr_app` role and has no DDL rights; migrations run separately under the migrator credential; no migration at startup (S22). |
| Audit integrity | ADR-0017 item 2: `ActorUserId`, `ActorRole`, `AuthorizedByPermission` and `ClientIp` come from `ICurrentUser` and the connection, never from the request body. Tested by S20 with a request that tries to supply them. |
| Secrets | No credential in any migration, appsettings or test fixture. Local credentials come from compose environment variables; `.gitleaks.toml` runs in CI (S26). |
| Logging | `docs/20` §7: message templates, no interpolation, and none of the forbidden fields. Reviewed at stage 7. |

Threats from `docs/18` that this feature touches: **unauthorized configuration** (mitigated by `stations.manage` on both write endpoints), **privilege escalation** (mitigated by no seeded grants and the permission policy provider), **insider manipulation** and **audit tampering** (mitigated by the ledger, least privilege and server-derived actor fields), **data disclosure** (mitigated by the 500-with-no-internals handler, S25).

Not touched, because no SPA or cookie-bearing endpoint exists yet: CSP, Origin checks and frontend dependency audit (ADR-0016 §Browser security; spec §9).

---

## Test plan

Test names follow `docs/20` §2's `Method_State_ExpectedResult`. Every spec scenario maps to at least one test; stage 5 owns completeness, and this table is what stage 5 checks itself against.

### `YCR.Domain.Tests`

| Test | Spec |
|---|---|
| `Create_WithValidCodeAndNames_ReturnsActiveStation` | S1 |
| `Deactivate_WhenActive_SetsInactiveAndRaisesEvent` | S2 |
| `Deactivate_WhenInactive_ReturnsError` | S7 |
| `StationCode_Create_WithValidCode_ReturnsCode` · `_WithTooShortCode_` · `_WithTooLongCode_` · `_WithLowerCase_` · `_WithPunctuation_` · `_WithBlank_` → `ReturnsValidationError` | S9 |
| `BilingualName_Create_WithMissingMyanmarName_` · `_WithWhitespaceOnlyName_` · `_WithOverlongName_` → `ReturnsValidationError` | S9 |

### `YCR.Application.Tests` (real SQL Server, `docs/20` §3)

| Test | Spec |
|---|---|
| `Handle_WithValidCommand_PersistsStationAndWritesAuditEvent` | S1 |
| `Handle_WithDuplicateCode_ReturnsConflict` | S5 |
| `Handle_WithCodeOfDeactivatedStation_ReturnsConflict` | S6 |
| `Handle_WithParallelDuplicateRequests_PersistsExactlyOneStation` | S13 |
| `Handle_WithMyanmarName_RoundTripsExactly` | S14 |
| `Handle_WhenStationInactive_ReturnsBusinessRuleError` | S7 |
| `Handle_WithUnknownId_ReturnsNotFound` | S8 |
| `Handle_WithThreeStations_ReturnsPagedEnvelope` | S3 |
| `Handle_WhenRequestSuppliesActorFields_IgnoresThem` | S20 |

### `YCR.Api.Tests` (`WebApplicationFactory`)

| Test | Spec |
|---|---|
| `Post_WithValidRequest_Returns201WithLocation` | S1 |
| `Post_WithDuplicateCode_Returns409WithErrorCode` | S5 |
| `Post_Anonymous_Returns401` | S11 |
| `Post_WithoutStationsManage_Returns403` (including a caller holding only `stations.read`) | S12 |
| `Post_WithInvalidBody_Returns400ProblemDetails` — one case per S9 item | S9 |
| `Deactivate_WhenActive_Returns204` · `_WhenInactive_Returns422` | S2, S7 |
| `Get_WithUnknownId_Returns404` · `Get_ReturnsResponseRecordNotEntity` | S8, S4 |
| `List_WithPageSizeAbove200_Returns400` | S10 |
| `Health_Live_Anonymous_Returns200` · `Health_Ready_ReportsDatabase` | S24 |
| `UnhandledException_Returns500WithoutInternalDetail` | S25 |
| `Startup_WithTestHandlerOutsideTesting_Throws` · `Startup_UnderTesting_Succeeds` | S21b |

### `YCR.Infrastructure.Tests`

| Test | Spec |
|---|---|
| `Migrate_AgainstPinnedImage_CreatesStationsAndLedgerTable` | S18 |
| `Migrate_OnUnsupportedServerVersion_ThrowsWithVersionInMessage` | S19 |
| `ModuleInterfaces_WithinOneScope_ResolveToSameContextInstance` | S17 |
| `ApplicationCredential_AttemptingDdl_IsDenied` · `_HasNoUpdateOnAuditEvents_` | S22 |
| `Insert10000Rows_FragmentationWithinTenPointsOfBaseline_Passes` | S23 |

### `YCR.ArchitectureTests`

Each rule runs twice: once over the `src/` assemblies asserting **zero** violations, and once over the `Violations` namespace in the test assembly asserting it flags **exactly** the planted type. The second half is the `docs/21` "negative cases" requirement, and without it a rule that matches nothing would pass silently.

| Test | Spec |
|---|---|
| `NetworkApplication_DependingOnTicketingContext_IsDetected` | S16a |
| `NetworkApplication_DependingOnTicketingDomain_IsDetected` | S16b |
| `Reporting_DependingOnWriteContext_IsDetected` | S16c |
| `Api_ContainingDomainLogic_IsDetected` | S16d |
| `Endpoint_ReturningEntityType_IsDetected` | S16e |
| `Src_ContainingAuthenticationHandler_IsDetected` | S21a |

### Whole-suite

`dotnet build YCR.sln` and `dotnet test YCR.sln` green from a clean clone with no skipped tests (S15); CI runs build, tests and gitleaks (S26).

---

## New packages

| Package | Where | Reason |
|---|---|---|
| `Microsoft.EntityFrameworkCore.SqlServer` | Infrastructure | Persistence provider (docs/06) |
| `Microsoft.EntityFrameworkCore.Design` | Infrastructure, DbMigrator | Migration tooling |
| `FluentValidation` + `FluentValidation.DependencyInjectionExtensions` | Api | `docs/20` §3 names FluentValidation behind an endpoint filter. The bundled `FluentValidation.AspNetCore` package is deprecated, so the filter is wired by hand |
| `Microsoft.AspNetCore.Mvc.Testing` | Api.Tests | `WebApplicationFactory`, required by `docs/20` §3 |
| `Testcontainers.MsSql` | TestSupport | `docs/20` §3 requires Testcontainers against `mssql/server:2022` |
| `NetArchTest.Rules` | ArchitectureTests | Type-dependency rules for ADR-0012 §Enforcement and ADR-0020 item 4 (P4) |
| `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` | all test projects | Test framework (P3) |
| `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` | Api | `/health/ready` must report database connectivity (`docs/02` §Reliability) |

**Deliberately not added.** `FluentAssertions` — its v8 licence change makes it commercial for some uses, the same class of concern that made ADR-0004 reject MediatR; plain xUnit assertions are used instead. `AutoMapper` — forbidden by `docs/20` §4. Any OpenTelemetry exporter — spec §8 asks only for baseline instrumentation, and ASP.NET Core's built-in metrics cover it; an exporter without a collector to send to is speculative (P8). `Respawn` — each test class gets its own database on the shared container, so no reset library is needed.

---

## Risks

| # | Risk | Likelihood | Mitigation |
|---|---|---|---|
| R-1 | The pinned image's edition does not support ledger tables, or `CHECK` constraints / nonclustered indexes are not permitted on an append-only ledger table | Medium | The E6 VERIFY runs in step 7, before anything depends on it. If it fails, stop and return to stage 2 — do not substitute a normal table (AGENTS.md rule 8) |
| R-2 | EF Core cannot insert into the ledger table because of its generated columns | Medium | Same VERIFY. Fallback within the same design: `AuditWriter` issues parameterised `INSERT` SQL on the same connection and transaction instead of through the EF change tracker. This keeps ADR-0017's one-transaction guarantee |
| R-3 | The ADR-0006 fragmentation control fails its 10-point threshold | Medium | It is a REQUIRED CONTROL and a genuine finding, not a test to loosen. If it fails, the generator is wrong; fix the generator or supersede ADR-0006 |
| R-4 | .NET 10 / EF Core 10 API differences from what `docs/20` §3's sample assumes | Medium | Step 1 pins the SDK and gets a trivial test green before any real code, so surprises surface immediately |
| R-5 | Docker unavailable or slow in CI, making every SQL-backed test flaky | Medium | One container per test collection, not per test; pin by digest to avoid surprise pulls; the fragmentation test is categorised so it need not run on every PR (E5) |
| R-6 | No git remote, so CI never actually runs and step 13 is unverified | High | Known and accepted (E3). Step 13 delivers the workflow; the DoD item stays open until a remote exists. Flagged now rather than discovered at stage 9 |
| R-7 | Myanmar Unicode mangled by source encoding or console output rather than by the database, producing a false S14 failure | Low | `.editorconfig` fixes UTF-8; the test compares against a `\u`-escaped literal so it does not depend on file encoding |
| R-8 | The provisional station rules leak beyond the two value objects, quietly becoming "the rules" | Medium | Waiver term 1 confines them; the stage-6 reviewer checks for leakage; T-014 is the release gate |
| R-9 | The reference slice ships with a flaw and every later feature copies it | Medium | This is the feature's central risk. Stages 5–7 are done by a different agent, and `docs/20` §3 is updated at stage 8 to match what was actually built |

---

## Rollback and forward compatibility

**Migrations.** `20260920_Network_CreateStations` and `20260920_Security_AppDatabaseRole` have working `Down()` methods. `20260920_Audit_CreateAuditEventsLedger` deliberately **throws** on `Down()` (P7): rolling back an append-only ledger destroys the tamper-evident history the ledger exists to provide, and ADR-0017 item 6 forbids a migration that drops one. Recovery from a bad audit migration is a documented manual operation against a restored backup, not an automated down-migration.

Because F-001 creates the database from nothing, there is no existing schema to upgrade and `docs/21`'s "upgrade tested on a copy of the current schema" is satisfied by proving a clean create against the pinned image. Stage 8 should record that explicitly rather than leaving the item ambiguously checked.

**API.** No deployed client exists, so there is no compatibility surface. `/api/v1` is versioned from the first endpoint, which is what protects later changes.

**Partially deployed clients.** Not applicable in F-001. The health endpoints exist so that a reverse proxy can withhold traffic from an instance whose database is unreachable, which is the only partial-deployment behaviour this feature has.

**Forward compatibility deliberately built in.** `PayloadVersion` on audit rows (ADR-0021 item 3) and `/api/v1` are the two places where F-001 pays a small cost now to avoid an additive-only migration later.

---

## Steps

Each step ends with `dotnet test YCR.sln` green. No step leaves the branch red.

| # | Step | Ends green with |
|---|---|---|
| 1 | Build configuration and empty solution: `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.gitignore`, `.editorconfig`, `YCR.sln`, all twelve projects with correct references and nothing in them | One trivial test, proving the SDK pin and `TreatWarningsAsErrors` hold |
| 2 | `YCR.Domain.Common`: `Result`, `Error`, `ErrorType`, `Entity`, `AggregateRoot` | `YCR.Domain.Tests` over Result and Error semantics |
| 3 | `YCR.Domain.Network`: `StationCode`, `BilingualName`, `Station`, `NetworkErrors`, `StationDeactivated` | All `YCR.Domain.Tests` rows above (S1, S2, S7, S9) |
| 4 | Application abstractions: `IIdGenerator`, `IAuditWriter`, `ICurrentUser`, `Permissions`, `PagedResult`, `INetworkDbContext` | Compiles; no behaviour yet |
| 5 | Infrastructure persistence: `YcrDbContext`, `StationConfiguration`, `SqlServerSequentialGuidIdGenerator`, DI registration, migration `20260920_Network_CreateStations` | `ModuleInterfaces_WithinOneScope_ResolveToSameContextInstance` (S17) |
| 6 | `YCR.TestSupport` container fixture with the digest-pinned image, `YCR.DbMigrator`, `docker-compose.yml`, `init-principals.sql` | A migrate-and-query smoke test against the real container |
| 7 | **E6 VERIFY, then** the audit ledger: `AuditEvent` mapping with `ExcludeFromMigrations()`, `AuditWriter`, migration `20260920_Audit_CreateAuditEventsLedger` with guard, constraints and indexes | S18, S19. **If the VERIFY fails, stop and return to stage 2** |
| 8 | Least-privilege: migration `20260920_Security_AppDatabaseRole`, two connection strings, no startup migration | S22 |
| 9 | Application handlers: `CreateStation`, `DeactivateStation`, `GetStation`, `ListStations`, DI scanning | All `YCR.Application.Tests` rows (S1, S3, S5, S6, S7, S8, S13, S14, S20) |
| 10 | API: `Program`, ProblemDetails, `ResultExtensions`, `ValidationFilter`, permission policy provider, scheme guard, contracts, `StationEndpoints`, health | All `YCR.Api.Tests` rows (S2, S4, S9–S12, S21b, S24, S25) |
| 11 | ADR-0006 fragmentation control, categorised per E5 | S23 |
| 12 | Architecture tests and their `Violations` fixtures | S16a–e, S21a |
| 13 | `.github/workflows/ci.yml`: restore, build, test against the pinned image, gitleaks | S26 as far as it can be proven without a remote (R-6) |

Steps 1–5 need no Docker. Steps 6 onwards do.

Documentation updates (`docs/07`, `docs/08`, `docs/20` §3, the glossary contribution) belong to **stage 8**, not here, and are listed in §Decisions needed P10 where a convention gap must be closed rather than merely recorded.

---

## Decisions needed at the ⛔ stop

Each has a recommendation. Nothing below is implemented before it is answered.

| # | Decision | Recommendation |
|---|---|---|
| **P1** | `src/YCR.DbMigrator` is a sixth `src/` project; `docs/06` §Projects lists five. E7 requires a separate migration step, and `dotnet ef database update` is tooling rather than a deployable artifact. | Add the project and record it in a short ADR-0022 superseding `docs/06`'s project list, since the architect prompt requires material changes to be ADRs. Alternative: use `dotnet ef database update` in CI and compose only, and defer the deployable migrator — cheaper now, but the production migration story stays unwritten. |
| **P2** | `tests/YCR.TestSupport` is a seventh `tests/` project not in `docs/06`. | Add it. Four test projects need the same pinned-digest fixture and duplicating it would defeat E4's single pinned reference. It is a test-support library, not a test project, so `docs/06` gets a note rather than an ADR. |
| **P3** | Test framework and assertion library are specified nowhere. | xUnit, with plain xUnit assertions. Explicitly **not** FluentAssertions, whose v8 licence change is the same concern that made ADR-0004 reject MediatR. |
| **P4** | Architecture-test library is specified nowhere. | `NetArchTest.Rules`: it works on type dependencies, which is exactly what ADR-0012 §Enforcement asks for, and it is small. |
| **P5** | Queries returning `StationDto` (Application) which the endpoint maps to `StationResponse` (Api) means two near-identical records per resource. | Keep both. ADR-0004 lets queries project to DTOs, and keeping the wire contract in the API layer is what lets `docs/20` §2's naming hold. The cost is one extra record per resource, and it is the pattern every later slice copies — so it should be a conscious choice, not a default. |
| **P6** | `Station.Deactivate()` raises `StationDeactivated`, but nothing consumes it and F-001 ships no dispatcher. | Collect events on the aggregate, ship no dispatcher. An unused dispatcher is speculative infrastructure; the first real subscriber should drive its design. |
| **P7** | `Down()` on the audit ledger migration throws instead of dropping the table. | Throw. Silently dropping a ledger table contradicts ADR-0017 item 6 and destroys the history the ledger exists for. Rollback becomes a documented manual procedure. |
| **P8** | No OpenTelemetry exporter in F-001, despite `README.md` saying "OpenTelemetry-ready". | Ship built-in ASP.NET Core metrics and structured logging only. "Ready" is satisfied by not blocking it; an exporter with no collector is dead configuration. |
| **P9** | The `ycr_app` **role** is created by a migration, but the **login and user** by environment provisioning. | Keep them separate, so no credential ever lands in a migration file. |
| **P10** | `docs/20` §1 has no row for `I<Module>DbContext`, though ADR-0012 requires one per module. This plan places it at `src/YCR.Application/<Module>/I<Module>DbContext.cs`. | Confirm the path, and add the row to `docs/20` §1 at **stage 8**. Leaving it unwritten guarantees the next module puts it somewhere else. |

---

## Stop point

⛔ **Plan needs human approval before implementation** (`docs/workflows/02-feature-development.md` stage 3). T-004 must not start until P1–P10 are answered and the plan is approved, because P1, P2, P5 and P6 change the file inventory that T-004 would build from.

# Plan: F-001 Walking skeleton — solution, infrastructure and the `Station` reference slice

Spec: `spec.md` — **Approved (hein, 2026-09-20)**, subject to the waiver in its §Blocked behaviour.
Stage: 3 (PLAN), `docs/workflows/02-feature-development.md`. Task T-003.
Binding inputs: ADR-0004, ADR-0005, ADR-0006, ADR-0012, ADR-0016, ADR-0017, ADR-0018, ADR-0019, ADR-0020, ADR-0021; `docs/20-coding-conventions.md`; `docs/21-definition-of-done.md`.

**Status: Approved (hein, 2026-09-20).** Revision 4. All twelve decisions P1-P12 are resolved; see §Decisions and §Review history. T-004 may proceed.

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
**FACT — ADR-0004 §Errors:** `Result<T>` and `Error` live in `YCR.Domain.Common`. This is what forces P11.
**FACT — `docs/20` §8:** new NuGet packages require a stated reason in the plan. See §New packages.

**ASSUMPTION (provisional, approved by hein 2026-09-20; replace when OQ26/OQ27/OQ28 answered)** — spec R3, R4, R8. This plan implements them only in `StationCode`, `BilingualName` and `Permissions`, per the waiver.

### Verifications, each with a named step and a fallback

| # | VERIFY | Step | If it fails |
|---|---|---|---|
| V1 | The pinned image's edition supports ledger tables | 8 | Stop, return to stage 2. Do not substitute a normal table (AGENTS.md rule 8) |
| V2 | `CHECK` constraints and nonclustered indexes are permitted on an append-only ledger table | 8 | Stop, return to stage 2 — ADR-0021 requires them from table creation |
| V3 | **Ledger `CREATE TABLE` runs inside EF's migration transaction** | 8 | Mark the migration `suppressTransaction: true` and split it so the guard still runs first and nothing partial survives a failure. Record the reason in `progress.md` |
| V4 | EF Core can `INSERT` into a ledger table whose generated columns it does not map | 8 | Risk R-2's fallback: `AuditWriter` issues parameterised `INSERT` on the same connection and transaction |
| V5 | `NetArchTest.Rules` is maintained for .NET 10 | 1 | Use `ArchUnitNET` instead. Decided before any rule is written, so the choice costs nothing later (P4) |
| V6 | `dotnet ef migrations bundle` produces a working self-contained migrator on EF 10, **including the raw-SQL ledger migration** | 7 | Only then revisit a `YCR.DbMigrator` project, with the ADR that would need (P1) |

**OPEN QUESTION — OQ26, OQ27, OQ28:** still open with Myanma Railways; T-014 is the release gate. They do not block stage 4.

**RESOLVED 2026-09-20 — `origin` now exists** (`https://github.com/heinmindev/ycr.git`). Spec E3 noted that no remote existed; that caveat is satisfied, and **spec E3's wording is now stale — stage 8 corrects it**. Step 13 must prove the CI workflow **green on `origin` for `feature/F-001`** and record the run URL in `progress.md`.

---

## Affected modules and files

Modules touched: `Network` (the slice), `Audit` (ledger write path), plus solution-wide `Common`. No other module gets more than an empty folder.

Twelve projects: five `src/` exactly as `docs/06` §Projects lists them, six `tests/` as `docs/06` lists them, plus `tests/YCR.TestSupport` (P2). **No `src/YCR.DbMigrator`** — migrations are applied by an EF migration bundle (P1).

### Solution and build configuration

| File | New / Changed | Why |
|---|---|---|
| `YCR.sln` | New | AGENTS.md §Commands requires `dotnet build YCR.sln` to work |
| `global.json` | New | Pin the SDK so CI and local builds agree (E2) |
| `Directory.Build.props` | New | `net10.0`, nullable enabled, `TreatWarningsAsErrors`, deterministic builds (E2) |
| `Directory.Packages.props` | New | Central package management, one pinned version per package across twelve projects (P3) |
| `.gitignore` | New | None exists (D2) |
| `.editorconfig` | New | E2; also fixes source encoding to UTF-8 so Myanmar test fixtures survive |
| `docker-compose.yml` | New | AGENTS.md §Commands; SQL Server 2022 pinned by digest (E4) |
| `docker/sqlserver/init-principals.sql` | New | Creates the `ycr_app` login and user. Role membership is granted after migrations, because the role is created by one (E7, P9) |
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
| `src/YCR.Domain/Properties/AssemblyInfo.cs` | New | Grants `YCR.Infrastructure` access to the internal `StationCode.From` EF materialisation path |
| `src/YCR.Application/Common/Abstractions/IIdGenerator.cs` | New | ADR-0006 §4 amendment |
| `src/YCR.Application/Common/Abstractions/IAuditWriter.cs` | New | ADR-0017, ADR-0021 |
| `src/YCR.Application/Common/Abstractions/ICurrentUser.cs` | New | Supplies server-side actor fields; ADR-0017 §2 forbids taking them from the request |
| `src/YCR.Application/Common/UniqueConstraintViolationException.cs` | New (step 9) | Application-owned exception boundary for provider-neutral unique-constraint translation; the infrastructure translator supplies the constraint name |
| `src/YCR.Application/Common/Authorization/Permissions.cs` | New | `docs/20` §1 fixes this exact path. **Sole home of provisional rule R8** |
| `src/YCR.Application/Common/Pagination/PagedResult.cs` | New | `docs/20` §4 pagination envelope |
| `src/YCR.Application/Network/INetworkDbContext.cs` | New | ADR-0012 §Decision item 2. Path confirmed by P10; `docs/20` §1 gains the row at stage 8 |
| `src/YCR.Application/Network/CreateStation/CreateStationCommand.cs`, `CreateStationHandler.cs` | New | ADR-0004 handler per use case |
| `src/YCR.Application/Network/DeactivateStation/DeactivateStationCommand.cs`, `DeactivateStationHandler.cs` | New | ″. Maps `DbUpdateConcurrencyException` to 422, see §Domain changes |
| `src/YCR.Application/Network/GetStation/GetStationQuery.cs`, `GetStationHandler.cs`, `StationDto.cs` | New | ADR-0004: queries project straight to DTOs with `AsNoTracking()` (P5) |
| `src/YCR.Application/Network/ListStations/ListStationsQuery.cs`, `ListStationsHandler.cs` | New | ″ |
| `src/YCR.Application/DependencyInjection.cs` | New | Handler registration by assembly scanning (ADR-0004 §Consequences) |
| `src/YCR.Infrastructure/Persistence/YcrDbContext.cs` | New | One concrete context implementing every module interface (ADR-0012 item 3) |
| `src/YCR.Infrastructure/Persistence/YcrDbContextFactory.cs` | New | EF design-time factory for migrations; reads required `YCR_DESIGN_TIME_CONNECTION` with no fallback target |
| `src/YCR.Infrastructure/Persistence/Configurations/Network/StationConfiguration.cs` | New | `docs/20` §1 fixes this path |
| `src/YCR.Infrastructure/Persistence/Configurations/Audit/AuditEventConfiguration.cs` | New | Maps the ledger table with `ExcludeFromMigrations()` so EF inserts but never creates or alters it (ADR-0017 §6) |
| `src/YCR.Infrastructure/Persistence/Migrations/<ts>_Network_CreateStations.cs` | New | EF timestamp naming, see §DB changes |
| `src/YCR.Infrastructure/Persistence/Migrations/<ts>_Audit_CreateAuditEventsLedger.cs` | New | Raw SQL ledger DDL + version/edition guard + constraints + indexes (ADR-0017, ADR-0021) |
| `src/YCR.Infrastructure/Persistence/Migrations/<ts>_Security_AppDatabaseRole.cs` | New | Least-privilege role (E7, P9) |
| `src/YCR.Infrastructure/Identifiers/SqlServerSequentialGuidIdGenerator.cs` | New | Named by the ADR-0006 amendment |
| `src/YCR.Infrastructure/Audit/AuditWriter.cs` | New | Writes `audit.AuditEvents` in the caller's `SaveChangesAsync` |
| `src/YCR.Infrastructure/DependencyInjection.cs` | New | Registers `YcrDbContext` once, then every module interface to that same scoped instance (ADR-0012 item 3) |
| `src/YCR.Api/Program.cs` | New | Composition root; `public partial class Program` so `WebApplicationFactory` can reach it |
| `src/YCR.Api/Common/ResultExtensions.cs` | New | `Result` → `IResult`, the ADR-0004 status mapping in one place. **The only file that depends on `YCR.Domain.Common` — see P11** |
| `src/YCR.Api/Common/ProblemDetailsSetup.cs` | New | RFC 9457 with `errorCode` and `traceId`; global handler yielding 500 with no internals (spec S25) |
| `src/YCR.Api/Common/ValidationFilter.cs` | New | `docs/20` §3 endpoint filter |
| `src/YCR.Api/Common/Authorization/PermissionRequirement.cs`, `PermissionAuthorizationHandler.cs`, `PermissionPolicyProvider.cs` | New | Turns `stations.manage` into a policy without a registration per permission |
| `src/YCR.Api/Common/Authentication/AuthenticationSchemeGuard.cs` | New | ADR-0020 item 5, the defence-in-depth allowlist check |
| `src/YCR.Api/Contracts/Network/CreateStationRequest.cs`, `CreateStationRequestValidator.cs`, `StationResponse.cs`, `CreateStationResponse.cs` | New | `docs/20` §2 naming |
| `src/YCR.Api/Endpoints/Network/StationEndpoints.cs` | New | `docs/20` §1 fixes this path |
| `src/YCR.Api/Endpoints/Health/HealthEndpoints.cs` | New | `docs/02` §Reliability; the only `.AllowAnonymous()` in the feature |
| `src/YCR.Worker/Program.cs` | New | Empty host. `docs/06` lists the project; nothing in F-001 uses it (spec §9) |

Empty module folders (`Identity`, `Timetable`, `Fare`, `Ticketing`, `Payments`, `Operations`, `Reporting`, `Audit`) are **not** created speculatively in Domain and Application. They appear when their first slice does; an empty folder tree teaches nothing and git does not track it.

### `tests/`

| File | New / Changed | Why |
|---|---|---|
| `tests/YCR.TestSupport/SqlServerContainerFixture.cs`, `PinnedImage.cs`, `DatabaseCredentials.cs` | New | One pinned digest (E4) and the two-credential setup (§Test fixture) shared by four test projects (P2) |
| `tests/YCR.Domain.Tests/Network/StationTests.cs`, `StationCodeTests.cs`, `BilingualNameTests.cs` | New | Invariants and transitions (`docs/21` §Tests) |
| `tests/YCR.Application.Tests/Network/CreateStationHandlerTests.cs`, `DeactivateStationHandlerTests.cs`, `GetStationHandlerTests.cs`, `ListStationsHandlerTests.cs` | New | Handler behaviour against real SQL Server (`docs/20` §3), under the `ycr_app` credential |
| `tests/YCR.Infrastructure.Tests/Persistence/LedgerMigrationTests.cs`, `LedgerGuardTests.cs`, `ModuleContextScopeTests.cs`, `DatabasePrivilegeTests.cs` | New | Spec S17–S19, S22 |
| `tests/YCR.Infrastructure.Tests/Identifiers/SequentialGuidFragmentationTests.cs` | New | ADR-0006 REQUIRED CONTROL (spec S23) |
| `tests/YCR.Api.Tests/Authentication/TestAuthHandler.cs`, `YcrApiFactory.cs` | New | ADR-0020 item 2; registered only via `ConfigureTestServices` |
| `tests/YCR.Api.Tests/Network/StationEndpointsTests.cs` | New | Spec S1–S12 |
| `tests/YCR.Api.Tests/Common/ProblemDetailsTests.cs`, `HealthEndpointsTests.cs`, `AuthenticationSchemeGuardTests.cs` | New | Spec S24, S25, S21b |
| `tests/YCR.ArchitectureTests/ModuleBoundaryTests.cs`, `LayerDependencyTests.cs`, `AuthenticationHandlerTests.cs` | New | ADR-0012 §Enforcement; ADR-0020 item 4; spec S16 |
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

### Deactivation concurrency (review item 9, revised at approval)

Spec §7 deliberately gives `Station` no `rowversion`, so two concurrent deactivations could otherwise both succeed and write two audit events. **`IsActive` is configured as an EF concurrency token** (`IsConcurrencyToken()` in `StationConfiguration`). `DeactivateStationHandler` then stays completely ordinary:

1. Loads the station. Not found → `Network.StationNotFound` (404).
2. Calls `Station.Deactivate()`. Already inactive → `Network.StationAlreadyInactive` (422). The domain method is the authority for the business rule, so AGENTS.md rule 3 holds and the domain tests keep their meaning.
3. Writes the audit event and calls `SaveChangesAsync` **once**. EF issues `UPDATE network.Stations SET IsActive = 0 WHERE Id = @id AND IsActive = @original`, and the aggregate change and the audit row commit in that one save.
4. A concurrent winner makes the `WHERE` match zero rows, EF throws `DbUpdateConcurrencyException`, and the handler maps it to the same `Network.StationAlreadyInactive` (422) as step 2. The caller cannot tell the two apart, which is correct — the outcome is the same.

**Why the audit event cannot leak on the losing request:** the aggregate update and the audit insert are in one `SaveChangesAsync`, so EF's implicit transaction rolls both back together. Exactly one `204`, one `422`, and one audit event per concurrent pair, with no explicit transaction management to get wrong.

This replaces the revision-2 design of an explicit transaction plus a hand-written `ExecuteUpdateAsync`. It is strictly better: the handler now matches ADR-0004's default ("Transactions come from one `SaveChangesAsync` per handler") instead of needing its exception, nothing bypasses the change tracker, and there is no pattern for a later slice to copy incorrectly.

One consequence worth recording: the token applies to **every** update of a `Station`, not just deactivation. F-001 has no other update, and when rename or reactivation arrives the same check will guard them too — which is wanted, not merely tolerated. It does become a fact the `PATCH /stations` feature must know about.

No column is added, so spec §7's no-`rowversion` decision is untouched.

### Unique-constraint translation (tech-lead ruling, 2026-09-20)

`YCR.Application.Common.UniqueConstraintViolationException` carries the database constraint name without exposing a provider-specific exception to Application. In step 9, Infrastructure translates SQL Server error numbers **2601** and **2627** to this exception in a `SaveChanges` interceptor or translator. `CreateStationHandler` catches only this application exception and maps it to `Network.StationCodeAlreadyExists` when the named constraint is the station `Code` unique index (`UX_Stations_Code`); any other unique-constraint violation is not caught by that handler and propagates as an unexpected infrastructure failure. This preserves ADR-0004's provider-neutral Application boundary and prevents unrelated uniqueness failures from being misreported as station-code conflicts.

---

## DB changes

Three migrations, applied in order by an **EF migration bundle** run under the **migrator** credential (P1). `docs/workflows/04-database-change.md` review applies to each.

### Migration identifiers (review item 2)

EF Core generates `yyyyMMddHHmmss_<Name>`, so the on-disk identifiers are, for example, `20260920103000_Network_CreateStations`. Exact timestamps come from `dotnet ef migrations add` and are not predictable from this plan; the `_<Module>_<Change>` part is what we control and what `docs/20` §6's intent requires.

**`docs/20` §6 currently specifies `YYYYMMDD_<Module>_<Change>`, which EF cannot produce** — a date-only prefix collides on any day with two migrations, and EF's own ordering depends on the full timestamp. **Stage 8 must correct `docs/20` §6 to `yyyyMMddHHmmss_<Module>_<Change>`.** Recorded here so the correction is traceable to this review rather than appearing as an unexplained edit.

### `<ts>_Network_CreateStations`

Creates schema `network` and table `network.Stations` exactly as spec §7 defines it: `Id uniqueidentifier` PK (clustered, application-assigned), `Code nvarchar(10)` with a unique index, `NameEn`/`NameMy nvarchar(100)` not null, `IsActive bit` not null, `CreatedAtUtc datetimeoffset(3)` not null.

The unique index on `Code` is the concurrency authority (R7) and is also what enforces "codes are never reused" (R3), because deactivated rows are retained rather than deleted. Data impact: none, the table is new. `Down()` drops the table and schema.

### `<ts>_Audit_CreateAuditEventsLedger`

Raw SQL, per ADR-0017 item 1. In order:

1. **Guard first.** Check `SERVERPROPERTY('ProductMajorVersion') >= 16` and that ledger objects are creatable in this edition; `THROW` with a message naming the detected version and edition otherwise (ADR-0017 item 5, spec S19). The guard runs before any DDL so a failure leaves nothing half-created.
2. Create schema `audit`.
3. Create `audit.AuditEvents` as an **append-only ledger table**, with the fourteen columns of ADR-0021 §Columns, including nullable `SubjectId`, `ActorRole nvarchar(1000)` and `AuthorizedByPermission nvarchar(100)`.
4. Create the three check constraints `CK_AuditEvents_BeforeJson`, `CK_AuditEvents_AfterJson`, `CK_AuditEvents_ActorRole`, each of the form `<col> IS NULL OR ISJSON(<col>) = 1`.
5. Create the two nonclustered indexes `IX_AuditEvents_Subject` on `(SubjectType, SubjectId)` and `IX_AuditEvents_OccurredAtUtc`.

Steps 3–5 are one migration because ADR-0021 decision item 5 requires the constraints to exist from the table's creation. **V3 checks that all of this runs inside EF's migration transaction**; if ledger DDL cannot, the migration is marked `suppressTransaction: true` and split so the guard still runs first and nothing partial survives a failure.

**`Down()` throws** (P7). Dropping an append-only ledger table would destroy tamper-evident history, and ADR-0017 item 6 forbids migrations that silently convert or drop a ledger table. Recovery is a documented manual operation against a restored backup.

### `<ts>_Security_AppDatabaseRole`

Creates database role `ycr_app` and grants it exactly: `SELECT, INSERT, UPDATE` on `network.Stations`; `INSERT, SELECT` on `audit.AuditEvents` (ADR-0017 item 3); nothing else, and no DDL anywhere. `UPDATE` on `Stations` is needed by `DeactivateStation`; the audit schema deliberately gets no `UPDATE` or `DELETE`.

It runs last because it grants on objects the first two migrations create.

The role is in a migration because it is schema-shaped and belongs with the objects it grants on. The **login and user**, which need a credential, are created by environment provisioning — `docker/sqlserver/init-principals.sql` locally, the test fixture in tests, a CI step in CI — so no secret ever enters a migration file (P9).

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
| Database least privilege | E7: the application credential holds the `ycr_app` role and has no DDL rights; migrations run separately under the migrator credential; no migration at startup (S22). **Every Application and API test now runs under `ycr_app`** (§Test fixture), so a missing grant fails a test rather than surfacing in production. |
| Audit integrity | ADR-0017 item 2: `ActorUserId`, `ActorRole`, `AuthorizedByPermission` and `ClientIp` come from `ICurrentUser` and the connection, never from the request body. Tested by S20 with a request that tries to supply them. |
| Secrets | No credential in any migration, appsettings or test fixture source. Local credentials come from compose environment variables; the test fixture generates a random password per container; `.gitleaks.toml` runs in CI (S26). |
| Logging | `docs/20` §7: message templates, no interpolation, and none of the forbidden fields. Reviewed at stage 7. |

Threats from `docs/18` that this feature touches: **unauthorized configuration** (mitigated by `stations.manage` on both write endpoints), **privilege escalation** (no seeded grants, permission policy provider, and now least privilege exercised by every test), **insider manipulation** and **audit tampering** (ledger, least privilege, server-derived actor fields), **data disclosure** (the 500-with-no-internals handler, S25).

Not touched, because no SPA or cookie-bearing endpoint exists yet: CSP, Origin checks and frontend dependency audit (ADR-0016 §Browser security; spec §9).

---

## Test fixture and credentials (review item 1)

`YCR.TestSupport` starts one digest-pinned SQL Server 2022 container per test collection and, for each test class, provisions a fresh database in this order:

1. Create the database and the `ycr_app` **login and user** with a randomly generated per-container password (no credential in source).
2. **Run the migration bundle under the migrator credential.** This creates `network.Stations`, the audit ledger and the `ycr_app` role.
3. `ALTER ROLE ycr_app ADD MEMBER ycr_app_user` — after migrations, because migration three is what creates the role.
4. Hand out two connection strings.

Which credential each project uses, and why:

| Project | Credential | Reason |
|---|---|---|
| `YCR.Application.Tests` | **`ycr_app`** | Handler tests exercise the real runtime identity, so a missing grant fails here rather than in production |
| `YCR.Api.Tests` | **`ycr_app`** | Same, end to end through the API |
| `YCR.Infrastructure.Tests` — migration and ledger tests | migrator | They are testing migrations, which only the migrator may run |
| `YCR.Infrastructure.Tests` — `DatabasePrivilegeTests` | `ycr_app` | Asserts DDL is denied and that `audit.AuditEvents` has no `UPDATE`/`DELETE` |
| `YCR.Infrastructure.Tests` — `SequentialGuidFragmentationTests` | migrator | It creates a test table and reads `sys.dm_db_index_physical_stats`, which needs `VIEW DATABASE STATE`. Granting that to `ycr_app` to satisfy a test would weaken the very control E7 exists to enforce |

That last row is the one trade-off in this change: the fragmentation control cannot run as `ycr_app` without loosening `ycr_app`. Running it as migrator is correct — it is an infrastructure characterisation test, not an application-path test.

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

### `YCR.Application.Tests` (real SQL Server, `ycr_app` credential)

| Test | Spec |
|---|---|
| `Handle_WithValidCommand_PersistsStationAndWritesAuditEvent` | S1 |
| `Handle_WithDuplicateCode_ReturnsConflict` | S5 |
| `Handle_WithCodeOfDeactivatedStation_ReturnsConflict` | S6 |
| `Handle_WithParallelDuplicateRequests_PersistsExactlyOneStation` | S13 |
| `Handle_WithMyanmarName_RoundTripsExactly` | S14 |
| `Handle_WhenStationInactive_ReturnsBusinessRuleError` | S7 |
| `Handle_WithParallelDeactivations_ReturnsOneSuccessOneConflictAndWritesOneAuditEvent` | S27 |
| `Handle_WithUnknownId_ReturnsNotFound` | S8 |
| `Handle_WithThreeStations_ReturnsPagedEnvelope` | S3 |
| `Handle_WhenRequestSuppliesActorFields_IgnoresThem` | S20 |

### `YCR.Api.Tests` (`WebApplicationFactory`, `ycr_app` credential)

| Test | Spec |
|---|---|
| `Post_WithValidRequest_Returns201WithLocation` | S1 |
| `Post_WithDuplicateCode_Returns409WithErrorCode` | S5 |
| `Post_Anonymous_Returns401` | S11 |
| `Post_WithoutStationsManage_Returns403` (including a caller holding only `stations.read`) | S12 |
| `Post_WithInvalidBody_Returns400ProblemDetails` — one case per S9 item | S9 |
| `Deactivate_WhenActive_Returns204` · `_WhenInactive_Returns422` | S2, S7 |
| `Get_WithUnknownId_Returns404` · `Get_WithKnownId_ReturnsResponseRecordNotEntity` | S8, S4 |
| `List_WithPageSizeAbove200_Returns400` | S10 |
| `Health_Live_Anonymous_Returns200` · `Health_Ready_ReportsDatabase` | S24 |
| `UnhandledException_Returns500WithoutInternalDetail` | S25 |
| `Startup_WithTestHandlerOutsideTesting_Throws` · `Startup_UnderTesting_Succeeds` | S21b |

### `YCR.Infrastructure.Tests`

| Test | Credential | Spec |
|---|---|---|
| `Migrate_AgainstPinnedImage_CreatesStationsAndLedgerTable` | migrator | S18 |
| `Migrate_OnSqlServer2019_ThrowsWithVersionInMessage` | migrator | S19, see below |
| `ModuleInterfaces_WithinOneScope_ResolveToSameContextInstance` | `ycr_app` | S17 |
| `ApplicationCredential_AttemptingDdl_IsDenied` · `_HasNoUpdateOnAuditEvents_` | `ycr_app` | S22 |
| `Insert10000Rows_FragmentationWithinTenPointsOfBaseline_Passes` | migrator | S23 |

**How S19 is actually tested (review item 7).** A second image, SQL Server **2019**, pinned by digest, in a **trunk-only** test category — the same treatment E5 gives the fragmentation test. The migration bundle is run against it and the test asserts the failure is our `THROW`, naming the detected version, and not an incidental SQL error from the ledger syntax. Testing the guard against a genuinely unsupported server is the only way to know it fires before the DDL rather than after; a unit test over a script string would prove nothing about ordering.

Cost and limit, stated plainly: this adds a second large image pull to the trunk build, which is why it is trunk-only. It exercises the **version** branch of the guard. The **edition** branch is not testable this way, because no readily available container runs a 2022 edition without ledger; it is covered by V1 in step 8 and by code review at stage 6.

### `YCR.ArchitectureTests`

Each rule runs twice: once over the `src/` assemblies asserting **zero** violations, and once over the `Violations` namespace in the test assembly asserting it flags **exactly** the planted type. The second half is the `docs/21` "negative cases" requirement, and without it a rule that matches nothing would pass silently.

| Test | Spec |
|---|---|
| `NetworkApplication_DependingOnTicketingContext_IsDetected` | S16a |
| `NetworkApplication_DependingOnTicketingDomain_IsDetected` | S16b |
| `Reporting_DependingOnWriteContext_IsDetected` | S16c |
| `Api_DependingOnModuleDomainNamespace_IsDetected` · `Api_DependingOnNonAllowlistedCommonType_IsDetected` | **S16d (replaced — see below and P11)** |
| `Application_DependentOnSqlServerProvider_IsDetected` | S16e (tech-lead ruling; provider boundary) |
| `Src_ContainingAuthenticationHandler_IsDetected` | S21a |

**Review item 3 applied.** The old S16d ("`YCR.Api` contains domain logic") and S16e ("an endpoint returns an EF entity type") are replaced by one stronger rule: **`YCR.Api` must not depend on `YCR.Domain`**, except the composition root.

Why this is an improvement: "contains domain logic" is not mechanically decidable and the old rule would have been a weak proxy; "returns an EF entity" is *subsumed*, because an endpoint that cannot reference `Station` cannot return it. One decidable rule replaces two, and it enforces AGENTS.md rule 4 more completely than the rule it replaces.

What it does **not** cover, stated so nobody assumes otherwise: AGENTS.md rule 3, "domain logic must not live in controllers", is only partly enforced. Business rules written inline over DTOs would still compile. That remains a stage-6 code-review concern, and the `docs/21` §Code checklist item should be read that way.

**P11 — resolved by hein, 2026-09-20: a type allowlist, not a namespace allowance.** `ResultExtensions` maps `Result`/`Error`/`ErrorType` to `IResult`, and ADR-0004 §Errors puts those types in `YCR.Domain.Common`, so a blanket "`YCR.Api` must not depend on `YCR.Domain`" fails on the one file implementing ADR-0004's error contract. The approved rule is:

- `YCR.Api` may depend on **no** `YCR.Domain.<Module>` type at all.
- From `YCR.Domain.Common` it may depend on exactly four types: **`Result`, `Result<T>`, `Error`, `ErrorType`**. Anything else in `YCR.Domain.Common` — `AggregateRoot`, `Entity`, `IDomainEvent` — is forbidden.
- No composition-root exception is granted: `Program.cs` wires `AddApplication()` and `AddInfrastructure()` and touches no domain type.

The architecture test enforces the **allowlist**, not merely the namespace, so adding a fifth shared type later is a deliberate act that fails the build until the allowlist is changed. `Violations/ApiUsingAggregateRoot.cs` plants a type referencing `AggregateRoot` to prove the allowlist half has teeth, alongside the fixture that plants a `YCR.Domain.Network` reference.

### Whole-suite

`dotnet build YCR.sln` and `dotnet test YCR.sln` green from a clean clone with no skipped tests (S15); CI runs build, tests and gitleaks (S26).

---

## New packages

| Package | Where | Reason |
|---|---|---|
| `Microsoft.EntityFrameworkCore` 10.0.12 | Application | Provider-neutral `DbSet` and `SaveChangesAsync` abstractions are accepted in Application by ADR-0004 §Request flow; the same EF Core version is used by the Infrastructure provider packages |
| `Microsoft.EntityFrameworkCore.SqlServer` | Infrastructure | Persistence provider (docs/06), pinned to the same EF Core version as the provider-neutral Application package |
| `Microsoft.EntityFrameworkCore.Design` | Infrastructure | Migration tooling and `dotnet ef migrations bundle` (P1) |
| `FluentValidation` + `FluentValidation.DependencyInjectionExtensions` | Api | `docs/20` §3 names FluentValidation behind an endpoint filter. The bundled `FluentValidation.AspNetCore` package is deprecated, so the filter is wired by hand |
| `Microsoft.AspNetCore.Mvc.Testing` | Api.Tests | `WebApplicationFactory`, required by `docs/20` §3 |
| `Testcontainers.MsSql` 4.15.0 | TestSupport | `docs/20` §3 requires Testcontainers against `mssql/server:2022`. Pinned at step 7. In 4.15 the image is a required constructor argument, so the E4 reference cannot be defaulted away |
| `TngTech.ArchUnitNET` + `TngTech.ArchUnitNET.xUnitV3` 0.13.4 | ArchitectureTests | Type-dependency rules for ADR-0012 §Enforcement and ADR-0020 item 4. **V5 resolved in step 1:** `NetArchTest.Rules` last shipped 1.3.2 in May 2021 with no .NET 10 signal, so the P4 fallback applies. ArchUnitNET last shipped 2026-08-20 and has an xUnit v3 package matching P3 |
| `xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` | all test projects | Test framework. **xUnit v3**, pinned in `Directory.Packages.props` (P3) |
| `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` | Api | `/health/ready` must report database connectivity (`docs/02` §Reliability) |
| `Microsoft.Extensions.Hosting` 10.0.12 | Worker | Added in step 1. `Microsoft.NET.Sdk.Worker` does not reference it implicitly on .NET 10, so the generated global usings do not compile without it |

**Not a package, recorded here because step 7 depends on it.** `.config/dotnet-tools.json` pins **`dotnet-ef` 10.0.12** as a local tool. The fixture shells out to `dotnet ef migrations bundle` (P1), so without the manifest the bundle would be built by whatever version happens to be installed globally on the machine or the CI runner — which would make V6 a statement about one laptop rather than about the repository.

**Deliberately not added.** `Microsoft.Data.SqlClient` — `YCR.TestSupport` uses `SqlConnection` for database provisioning, but the type arrives with the EF Core SQL Server provider through `YCR.Infrastructure` and central transitive pinning fixes its version; a second explicit pin could only drift from the provider's own requirement. `FluentAssertions` — its v8 licence change makes it commercial for some uses, the same class of concern that made ADR-0004 reject MediatR; plain xUnit assertions are used instead (P3). `AutoMapper` — forbidden by `docs/20` §4. Any OpenTelemetry exporter — spec §8 asks only for baseline instrumentation, and ASP.NET Core's built-in metrics cover it (P8). `Respawn` — each test class gets its own database on the shared container, so no reset library is needed.

---

## Risks

| # | Risk | Likelihood | Mitigation |
|---|---|---|---|
| R-1 | The pinned image's edition does not support ledger tables, or `CHECK` constraints / nonclustered indexes are not permitted on an append-only ledger table | Medium | V1 and V2 run in step 8, before anything depends on them. If either fails, stop and return to stage 2 — do not substitute a normal table (AGENTS.md rule 8) |
| R-2 | EF Core cannot insert into the ledger table because of its generated columns | Medium | V4. Fallback within the same design: `AuditWriter` issues parameterised `INSERT` SQL on the same connection and transaction instead of through the EF change tracker, preserving ADR-0017's one-transaction guarantee |
| R-3 | Ledger DDL cannot run inside EF's migration transaction | Medium | V3. Fallback: `suppressTransaction: true` with the guard still first, so a failure leaves nothing partial |
| R-4 | The ADR-0006 fragmentation control fails its 10-point threshold | Medium | It is a REQUIRED CONTROL and a genuine finding, not a test to loosen. If it fails, the generator is wrong; fix it or supersede ADR-0006 |
| R-5 | .NET 10 / EF Core 10 API differences from what `docs/20` §3's sample assumes; `NetArchTest.Rules` unmaintained for .NET 10 | Medium | Step 1 pins the SDK, resolves V5 and gets a trivial test green before any real code, so surprises surface immediately |
| R-6 | `dotnet ef migrations bundle` cannot handle the raw-SQL ledger migration, or is awkward under EF 10 | Medium | V6 in step 7, before the bundle is wired into CI or the test fixture. Only then is a `YCR.DbMigrator` project reconsidered (P1) |
| R-7 | Two SQL Server images (2022 and 2019) make the trunk build slow or flaky | Medium | The 2019 image is trunk-only, one container for the one test; both pinned by digest so no surprise pulls |
| R-8 | Running Application and API tests as `ycr_app` surfaces missing grants as confusing test failures | Medium | This is the point of the change — the failure is real. `DatabasePrivilegeTests` asserts the grant set explicitly, so a missing grant is diagnosed there rather than guessed at from a handler test |
| R-9 | ~~No git remote, so CI never runs and step 13 is unverified~~ | — | **Resolved 2026-09-20:** `origin` exists. Step 13 now has to prove the workflow green rather than merely deliver the file, so this stopped being a risk and became an exit criterion |
| R-10 | Myanmar Unicode mangled by source encoding rather than by the database, producing a false S14 failure | Low | `.editorconfig` fixes UTF-8; the test compares against a `\u`-escaped literal so it does not depend on file encoding |
| R-11 | The provisional station rules leak beyond the two value objects, quietly becoming "the rules" | Medium | Waiver term 1 confines them; the stage-6 reviewer checks for leakage; T-014 is the release gate |
| R-12 | The reference slice ships with a flaw and every later feature copies it | Medium | This is the feature's central risk. Stages 5–7 are done by a different agent, and `docs/20` §3 is updated at stage 8 to match what was actually built |

---

## Rollback and forward compatibility

**Migrations.** `Network_CreateStations` and `Security_AppDatabaseRole` have working `Down()` methods. `Audit_CreateAuditEventsLedger` deliberately **throws** on `Down()` (P7): rolling back an append-only ledger destroys the tamper-evident history the ledger exists to provide, and ADR-0017 item 6 forbids a migration that drops one. Recovery from a bad audit migration is a documented manual operation against a restored backup, not an automated down-migration.

Because F-001 creates the database from nothing, there is no existing schema to upgrade, and `docs/21`'s "upgrade tested on a copy of the current schema" is satisfied by proving a clean create against the pinned image. Stage 8 should record that explicitly rather than leaving the item ambiguously checked.

**API.** No deployed client exists, so there is no compatibility surface. `/api/v1` is versioned from the first endpoint, which is what protects later changes.

**Partially deployed clients.** Not applicable in F-001. The health endpoints exist so a reverse proxy can withhold traffic from an instance whose database is unreachable, which is the only partial-deployment behaviour this feature has.

**Forward compatibility deliberately built in.** `PayloadVersion` on audit rows (ADR-0021 item 3) and `/api/v1` are the two places where F-001 pays a small cost now to avoid an additive-only migration later.

---

## Steps

Each step ends with `dotnet test YCR.sln` green. No step leaves the branch red. **Steps 1–6 need no Docker; 7 onward do.**

| # | Step | Ends green with |
|---|---|---|
| 1 | Build configuration and empty solution: `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.gitignore`, `.editorconfig`, `YCR.sln`, all twelve projects with correct references and nothing in them. **Resolve V5** (architecture-test library) here | One trivial test, proving the SDK pin and `TreatWarningsAsErrors` hold |
| 2 | `YCR.Domain.Common`: `Result`, `Error`, `ErrorType`, `Entity`, `AggregateRoot` | `YCR.Domain.Tests` over Result and Error semantics |
| 3 | `YCR.Domain.Network`: `StationCode`, `BilingualName`, `Station`, `NetworkErrors`, `StationDeactivated` | All `YCR.Domain.Tests` rows above (S1, S2, S7, S9) |
| 4 | Application abstractions: `IIdGenerator`, `IAuditWriter`, `ICurrentUser`, `Permissions`, `PagedResult`, `INetworkDbContext` | Compiles; no behaviour yet |
| 5 | Infrastructure persistence: `YcrDbContext`, `StationConfiguration`, `SqlServerSequentialGuidIdGenerator`, DI registration, migration `Network_CreateStations` | Builds; the SQL-backed scope test arrives in step 7 |
| **6** | **Architecture tests and their `Violations` fixtures** (moved here by review item 4) | S16a–d, S21a |
| 7 | `YCR.TestSupport` container fixture with the digest-pinned image, `docker-compose.yml`, `init-principals.sql`, migration bundle. **Resolve V6** | A migrate-and-query smoke test against the real container; `ModuleInterfaces_WithinOneScope_...` (S17) |
| 8 | **V1–V4 first, then** the audit ledger: `AuditEvent` mapping with `ExcludeFromMigrations()`, `AuditWriter`, migration `Audit_CreateAuditEventsLedger` with guard, constraints and indexes | S18. **If V1 or V2 fails, stop and return to stage 2** |
| 9 | Least-privilege: migration `Security_AppDatabaseRole`, two connection strings, fixture role membership, no startup migration | S22; the fixture now hands Application/API tests the `ycr_app` credential |
| 10 | Application handlers: `CreateStation`, `DeactivateStation` (`IsActive` concurrency token), `GetStation`, `ListStations`, DI scanning | All `YCR.Application.Tests` rows (S1, S3, S5–S8, S13, S14, S20, S27) |
| 11 | API: `Program`, ProblemDetails, `ResultExtensions`, `ValidationFilter`, permission policy provider, scheme guard, contracts, `StationEndpoints`, health | All `YCR.Api.Tests` rows (S2, S4, S9–S12, S21b, S24, S25) |
| 12 | Trunk-only categories: ADR-0006 fragmentation control (E5) and the SQL Server 2019 guard test | S23, S19 |
| 13 | `.github/workflows/ci.yml`: restore, build, test against the pinned images, gitleaks. **Every connection string CI creates must carry `Current Language=us_english`** (hein's ruling, 2026-09-20 — see step 9). **CI builds the migration bundle once as its own step and passes its path in `YCR_MIGRATION_BUNDLE`** (hein's ruling, 2026-09-21); the fixture already consumes it and falls back to the locked local build when unset. Push `feature/F-001` to `origin` and **prove the run green**, recording the run URL in `progress.md`.<br><br>**Plus a smoke job against the built API and a compose database** (hein's ruling, 2026-09-21), asserting: `/health/live` → `200`; `/health/ready` → `200`; `/openapi/v1.json` **not served** outside Development; `GET /api/v1/stations` → `401` carrying an `errorCode`. This is the check that would have caught the 500-instead-of-401 defect found at step 11, so it runs against the real artifact, not the test host.<br><br>**Trunk-only tests** (`Category=TrunkOnly`) are excluded from the branch run and must be run by a trunk job passing the filter-trait option with `Category=TrunkOnly` | S26, evidenced by a green GitHub Actions run URL on `origin` for `feature/F-001` |

**Why step 6 moved.** The boundary rules now exist before the code that could break them, so every later step is guarded as it lands rather than audited afterwards. At step 6 the rules over `src/` pass vacuously — `YCR.Api` is still empty — but the `Violations` fixtures prove the rules have teeth from that moment, which is precisely what makes the vacuous pass trustworthy.

Documentation updates (`docs/07`, `docs/08`, `docs/20` §3, **`docs/20` §6's migration-naming correction**, the glossary contribution) belong to **stage 8**, not here.

**`docs/20` §3's audit example must be corrected at stage 8** (hein's ruling, 2026-09-20, applied in step 9). Line 104 currently reads `audit.Record("Network.StationCreated", station, before: null, after: station);`, which passes the aggregate as both the subject and the state payload — exactly what the ruling forbids. The corrected call is `audit.Record("Network.StationCreated", NetworkAuditSubjects.Station, station.Id, before: null, after: StationAuditSnapshot.From(station));`. Recorded here so the edit is traceable to the ruling rather than appearing unexplained.

---

## Decisions

### Resolved at plan review, 2026-09-20

| # | Decision | Outcome |
|---|---|---|
| **P1** | How migrations are applied | **Changed.** Use `dotnet ef migrations bundle`, run under the migrator credential. No `YCR.DbMigrator` project and no ADR-0022 — the bundle is already a self-contained deployable, which was the objection to EF tooling. Reconsider only if **V6** fails |
| **P2** | `tests/YCR.TestSupport` | **Accepted.** `docs/06` gets a note at stage 8 |
| **P3** | Test framework | **Accepted, pinned: xUnit v3** (`xunit.v3`), version fixed in `Directory.Packages.props`. Plain xUnit assertions; no FluentAssertions |
| **P4** | Architecture-test library | **Changed to a verification.** `NetArchTest.Rules` if **V5** shows it is maintained for .NET 10, otherwise `ArchUnitNET`. Decided in step 1 |
| **P5** | `StationDto` and `StationResponse` as separate records | **Accepted.** Keep both |
| **P6** | Domain events collected, no dispatcher | **Accepted** |
| **P7** | Audit ledger `Down()` throws | **Accepted** |
| **P8** | No OpenTelemetry exporter | **Accepted** |
| **P9** | Role in a migration, login and user in provisioning | **Accepted** |
| **P10** | `I<Module>DbContext` path | **Accepted.** `src/YCR.Application/<Module>/I<Module>DbContext.cs`; `docs/20` §1 gains the row at stage 8 |

### Resolved at approval, 2026-09-20

| # | Decision | Outcome |
|---|---|---|
| **P11** | `YCR.Api`'s permitted dependency on `YCR.Domain` | **Resolved as a type allowlist.** No `YCR.Domain.<Module>` type at all; from `YCR.Domain.Common` exactly `Result`, `Result<T>`, `Error`, `ErrorType`. The architecture test enforces the allowlist itself, so a fifth shared type fails the build until the allowlist is deliberately changed. No composition-root exception |
| **P12** | The concurrency scenario missing from the Approved spec | **Approved as a stage-2 amendment (hein, 2026-09-20).** Spec §4 now carries **S27**, and spec.md records it as Amendment 1 |

**No open decisions remain.** The plan is approved and T-004 may proceed.

---

## Review history

**Revision 4 — 2026-09-20, hein: approved.** P11 resolved as a four-type allowlist (`Result`, `Result<T>`, `Error`, `ErrorType`) that the architecture test enforces by type, not by namespace. P12 approved as a stage-2 amendment; spec §4 now carries S27 and spec.md records it as Amendment 1. Review item 9's mechanism replaced: `IsActive` becomes an EF concurrency token, so the explicit transaction and the hand-written `ExecuteUpdateAsync` are gone and the handler is one ordinary `SaveChangesAsync` — which is ADR-0004's default rather than its exception. The item-1 fragmentation-test credential exception and the Protocol-bullets approach were both accepted. `feature/F-001` is pushed after every checkpoint from now on.

**Revision 3 — 2026-09-20, hein.** A git remote (`origin`, `https://github.com/heinmindev/ycr.git`) now exists. Risk R-9 is struck: step 13 no longer merely delivers `ci.yml`, it must **prove the workflow green on `origin` for `feature/F-001`** and record the run URL in `progress.md`, which turns the old risk into an exit criterion. Spec E3's "no remote yet" wording is now stale and is corrected at stage 8. `TASKS.md` §Protocol gains two lines: push `main` after each ledger commit, and never push `claim/*` refs.

**Revision 2 — 2026-09-20, reviewer: Claude.** Nine items applied:

1. Test fixture now runs migrations under the migrator credential and Application/API tests under `ycr_app`, so every test exercises least privilege (§Test fixture). One exception documented with its reason: the fragmentation test stays on the migrator credential because it needs `VIEW DATABASE STATE`.
2. Migration identifiers changed to EF's `yyyyMMddHHmmss_<Module>_<Change>`; `docs/20` §6's date-only form is noted as a stage-8 correction.
3. S16d and S16e replaced by the single dependency rule, with its subsumption argument, its gap against AGENTS.md rule 3, and the `YCR.Domain.Common` conflict raised as P11.
4. Architecture tests moved to step 6, immediately after Infrastructure persistence.
5. P1 changed to `dotnet ef migrations bundle`; `YCR.DbMigrator` removed from the file inventory; V6 guards the change.
6. P4 became verification V5 with an `ArchUnitNET` fallback; P3 pinned to xUnit v3.
7. S19's test method specified: a digest-pinned SQL Server 2019 image in a trunk-only category, with its cost and its edition-branch limitation stated.
8. V3 added: ledger `CREATE TABLE` inside EF's migration transaction, with a `suppressTransaction` fallback.
9. `DeactivateStationHandler` specified as a conditional update with a rows-affected check (§Domain changes), keeping `Station.Deactivate()` as the business-rule authority; new test, and P12 raised because the scenario is missing from the Approved spec.

---

## Stop point

**Passed 2026-09-20.** The plan is **Approved (hein, 2026-09-20)** and the stage-2 amendment adding S27 was approved with it. T-004 may proceed.

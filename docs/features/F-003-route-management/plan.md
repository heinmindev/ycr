# Plan: F-003 Route management — the `Route` aggregate, its immutable station sequence, and route permissions

Spec: `spec.md` — **Approved (hein, 2026-09-24)** at `15d3709`; every ruling is in spec §0.8.
Stage: 3 (PLAN), `docs/workflows/02-feature-development.md`. Task T-034.
Binding inputs: ADR-0004, ADR-0006, ADR-0012, ADR-0014, ADR-0017, ADR-0018, ADR-0021, ADR-0022; `docs/07`, `docs/08`, `docs/10` §Route permission grants; `docs/20-coding-conventions.md`; `docs/21-definition-of-done.md`. Worked examples: the F-001 and F-002 plans, and the existing `Network` slice (`CreateStation`, `DeactivateStation`, `GetStation`, `ListStations`).

**Status: Draft — awaiting hein's approval.** Three questions for hein are in §Questions for hein (Q1–Q3). Q1 decides how `IX_RouteStations_StationId` is dropped, and it touches one index name in the Approved spec, so it is asked, not patched. Twelve plan decisions (P1–P12) are listed in §Decisions for hein to confirm or overturn. Stop at the ⛔: nothing is implemented until hein approves.

---

## Understanding

F-003 adds routes to the `Network` module. A route is an ordered, **immutable** sequence of existing, active stations with a code, a bilingual name and an open/closed flag. It is created whole by one `POST /routes`, read by id or as a page, and can only be deactivated. The feature has four parts:

1. **Domain.** A `Route` aggregate that owns its `RouteStation` rows. `Route.Create` enforces the sequence rules (R7, R8, R11, R16) and `Route.Deactivate` the lifecycle rule (R15). A `RouteCode` value object shares the station-code format, and `BilingualName` is reused as it is.
2. **Persistence.** Two new tables in `network`, least-privilege grants to `ycr_app` that make the sequence insert-only in the database (R10), and a seed of the ten route permission grants (`docs/10` §Route permission grants).
3. **API.** Four endpoints under `/api/v1/routes`. `POST /stations/{id}/deactivate` does not change (R12).
4. **Tests** for every live scenario (S1–S30 minus the six removed ones), including the parallel S21 and S23 tests and the privilege test's presence and absence lists (S25).

This feature is small next to F-001 and F-002, because it mostly reuses their patterns. The risk is in the details they did not need: a child table, a two-column grant, and a seed that changes counts other tests pin. So the plan names each existing test that must change, and says why. None is weakened.

---

## Facts / Assumptions / Open questions

**FACT — spec §0.8, R9, R10:** sequences, `IsClosed`, code and names never change. `RouteStations` is insert-only, and `Routes` accepts only `UPDATE(IsActive, DeactivatedAtUtc)`.
**FACT — `src/YCR.Domain/Network/BilingualName.cs`:** `BilingualName.Create` returns `NetworkErrors.InvalidStationName` on failure. A route needs `Network.InvalidRouteName` (R13), so the value object cannot be reused unchanged (P3).
**FACT — `src/YCR.Domain/Network/StationCode.cs`:** the `^[A-Z0-9]{2,10}$` rule lives in `StationCode` and returns `Network.InvalidStationCode`. The route code follows the same rule and needs its own error (R13), which leads to P2.
**FACT — `IdentitySeedTests.Seed_ProducesExactlyEightRolesAndFourteenGrants` and `DatabasePrivilegeTests.ApplicationCredential_CannotWriteRolesOrGrants`** both assert **14** grant rows. The route seed adds ten more, so both must assert **24**. `Seed_MatchesDocs10GrantTables` parses only the station and identity sections of `docs/10`, so it must also parse §Route permission grants. These tests are **extended, not weakened**: each keeps asserting the exact set.
**FACT — `StationModelTests.DownMigration_WithStationsTable_DropsNetworkSchema`:** `Network_CreateStations.Down()` drops the `network` schema. `Network_CreateRoutes.Down()` must drop only its two tables, and never the schema.
**FACT — `.github/workflows/ci.yml` `api-smoke`:** the job already bootstraps an administrator, changes the must-change password and calls `/api/v1/stations` with a real token. Route checks fit after that call (§API changes).
**FACT — `README.md`** lists no endpoints (checked with `grep`), so stage 8 does not change it.

### Verifications done at PLAN

Run in a scratch console app outside the repository (`net10.0`, SDK 10.0.302, `Microsoft.EntityFrameworkCore.SqlServer` 10.0.12, the repository's pins). The model was inspected through `IDesignTimeModel` and `IMigrationsModelDiffer`, which is what `has-pending-model-changes` compares.

| # | Question | Result |
|---|---|---|
| O1 | With `PK (RouteId, Position)`, a unique index on `(RouteId, StationId)` and an FK on `StationId`, does EF add an index for the FK? | **Yes.** EF's foreign-key index convention adds `IX_RouteStations_StationId`, because no index *leads* with `StationId`. This happens with both an owned collection and a child entity. |
| O2 | Can the configuration remove that index (`Metadata.RemoveIndex(...)`)? | **No.** The convention adds it back: the model and the migration diff still contain `CreateIndex IX_RouteStations_StationId`. |
| O3 | If the unique index is declared on `(StationId, RouteId)` instead, is the FK covered? | **Yes.** The model then has `PK_RouteStations` and the unique index only, and the diff creates only the unique index. It enforces the same rule (a station at most once per route). |
| O4 | Can both FKs be `NO ACTION`? | **Yes**, for a child entity (`OnDelete(DeleteBehavior.NoAction)` on both) and for an owned collection (by setting the ownership's `DeleteBehavior`). EF's default for the route FK would be `Cascade`, so it must be set explicitly. |
| O5 | How does EF 10 translate `stationIds.Contains(s.Id)` with 3 000 ids, the handler's station lookup? | As **one** `nvarchar(max)` JSON parameter (`OPENJSON`), not one parameter per id. The SQL Server 2 100-parameter limit is therefore not a failure mode (see Q2). |

O1–O3 are why **Q1** exists.

### Verifications left to implementation, each with a step and a fallback

| # | VERIFY | Step | If it fails |
|---|---|---|---|
| V1 | Deactivation emits **one** `UPDATE [network].[Routes] SET [IsActive], [DeactivatedAtUtc] … WHERE [Id] = @id AND [IsActive] = @original` and **no** statement against `RouteStations` (the children are loaded but unchanged) | 5 | Fix the mapping (for example, do not load the children for deactivation and build the snapshot from a projection). **Never widen the grant** (AGENTS.md rule 8). `DatabasePrivilegeTests` and the handler test run as `ycr_app`, so a stray `UPDATE` fails a test. |
| V2 | The `GetRoute` station query (`Routes → SelectMany(Stations) → join Stations`, ordered by `Position`) translates to one SQL statement | 5 | A nested collection projection inside the route projection. The response shape does not change. |
| V3 | `ListRoutes` computes `stationCount` with a SQL `COUNT` subquery and pages with `OFFSET/FETCH` | 5 | `ListRoutesSqlTests` asserts the generated SQL, as `ListStationsSqlTests` does. If the count evaluates on the client, compute it with a grouped subquery. |
| V4 | `dotnet ef migrations has-pending-model-changes` is clean after each migration (the CI step does this) | 2–4 | The model and the migration disagree. Fix the model. Never hand-edit the migration to match. |
| V5 | What the existing pipeline returns for a JSON **type** mismatch in a body (for example `"isClosed": "yes"`) | 6 | Record what happens in `progress.md`. This behaviour is shared by every endpoint and predates F-003, and S5 does not list it, so F-003 does not change it (§Risks R-6). |

**ASSUMPTION:** none. Every business rule used is a spec rule (R1–R25) or one of hein's rulings in spec §0.8.

**OPEN QUESTION:** none new in `docs/19`. Q1–Q3 are questions to hein about the spec and the task. They are not Myanma Railways questions.

---

## Questions for hein

Found while planning. None is patched. The plan rows that depend on one are marked **Q1**, **Q2** or **Q3**.

| # | Question | Why it is asked | Options | Blocks |
|---|---|---|---|---|
| **Q1** | **How is `IX_RouteStations_StationId` dropped?** | This plan drops the index: no query in F-003 filters `RouteStations` by `StationId` alone (§DB changes, "Index decision"). But EF adds it by convention and puts it back if removed (O1, O2). The only local way to drop it is to reverse the unique index's columns (O3). That changes the index name spec §7 and R7 give: `UX_RouteStations_RouteId_StationId`, "unique on `(RouteId, StationId)`". | **(a) Recommended:** the unique index becomes `UX_RouteStations_StationId_RouteId` on `(StationId, RouteId)`. It enforces the same rule, EF adds no extra index, and a later "routes through station X" query gets its index for free. This needs a stage-2 amendment to the index name in spec §7 and R7. (b) Keep the spec's name and column order, and keep `IX_RouteStations_StationId` as EF's convention index. This overturns the approval decision, "kept only if a named query uses it". (c) Remove EF's FK-index convention for the whole model. **Not recommended:** it changes how every module is modelled, to save one index. | Step 2 (EF configuration, `Network_CreateRoutes`, `NetworkConstraints`). Steps 5–6 only use the constant's name. |
| **Q2** | **Is there an upper limit on `stationIds`?** | The spec sets none. O5 shows there is no hard technical failure below the request-size limit (Kestrel's 30 MB default, about 750 000 ids). A limit would still be the first defence against API abuse (`docs/18`). But the number is also a business fact (the longest possible route), and AGENTS.md rule 1 forbids inventing it. | **(a) Recommended for F-003:** no limit. Only `routes.manage` holders (two administrator roles) can send the request, and every row is audited. Record the question for when real route data arrives (OQ1). (b) An engineering cap, which hein names, enforced by the validator as `400 Common.ValidationFailed`. That needs a new scenario, so it is a spec amendment. | Nothing, if (a). With (b), step 6 and one API test. |
| **Q3** | **May stage 8 edit `docs/glossary.md`?** | T-034 asks for glossary entries at stage 8 (Route code, `IsClosed`, `RouteStation`/position). The Approved spec §9 lists "Editing … `docs/glossary.md`" as **out of scope**. | (a) Stage 8 adds the three entries, and spec §9's line is read as a stage-2 restriction only. (b) No glossary edit in F-003. The entries go to a separate task. | Stage 8 only. Does not block stage 4. |

**Spec housekeeping, not edited (the spec is Approved):** spec §7 still describes `IX_RouteStations_StationId` as "left to PLAN". Stage 8 records the outcome in `docs/07`, and a Q1 (a) amendment would update §7 itself.

---

## Decisions (P1–P12) — made by this plan, for hein to confirm

ENGINEERING DECISION (plan, T-034) unless stated. Each one cites the ruling it implements.

| # | Decision | Why | Rejected alternative |
|---|---|---|---|
| **P1** | **`RouteStation` is a child entity of `Route`, not an owned type.** It is a plain `sealed class` (`RouteId`, `Position`, `StationId`) with an internal constructor, like `UserRole`. `Route` exposes it as `IReadOnlyList<RouteStation> Stations` over a private `List<>` field. It is configured by `RouteConfiguration` (`HasMany(...).WithOne().HasForeignKey(RouteId).OnDelete(NoAction)`, field access mode) and `RouteStationConfiguration`. `INetworkDbContext` gains `DbSet<Route> Routes` only. There is **no** `DbSet<RouteStation>`, so rows are reached through their aggregate. `RouteStation` has **no** property of type `Station` (E5). | Follows F-002's `AuthSession`/`RefreshToken` mapping (`AuthSessionConfiguration`), with explicit FK names and `NO ACTION` (O4). An owned collection would also work (O4), but EF always loads it with the owner and translates queries into it differently from the rest of the code base. | `OwnsMany` |
| **P2** | **The code format is shared.** A new `internal static class NetworkCodeFormat` in `YCR.Domain.Network` holds the one regex `^[A-Z0-9]{2,10}$` and `IsValid(string)`. `StationCode.Create` calls it, with unchanged behaviour. The new `RouteCode.Create` trims, calls it, and returns `Network.InvalidRouteCode`. `RouteCode.From` is the internal EF path, as in `StationCode`. | OQ41's ruling is "exactly the station rules", so one rule gets one home. Each value object keeps its own error code (R13). If Myanma Railways later gives the two codes different answers, splitting the helper is a local change. The XML comments cite both rulings and their different standing: OQ26 is final, OQ41 is provisional. | `RouteCode` calling `StationCode.Create` and translating the error (couples a route to a station type); a second copy of the regex (two places to change) |
| **P3** | **`BilingualName` gains `Create(string? en, string? my, Error whenInvalid)`.** The existing two-argument `Create` delegates to it with `NetworkErrors.InvalidStationName`, so its behaviour is unchanged. `CreateRouteHandler` passes `NetworkErrors.InvalidRouteName`. The XML comment is extended to route names (OQ41). | Reuses the value object and its 1–100 rule (R13) without mapping error codes in a handler. | A handler mapping `InvalidStationName` to `InvalidRouteName` |
| **P4** | **All four sequence rules live in `Route.Create`.** Signature: `Result<Route> Create(Guid id, RouteCode code, BilingualName name, bool isClosed, IReadOnlyList<Guid> stationIds, IReadOnlyDictionary<Guid, bool> knownStationIsActive, DateTimeOffset nowUtc)`. The handler only loads the map (id → `IsActive` for each requested id that exists). **Fixed precedence** when several rules fail: R7 repeated → R8 too few → R16 not found (the first unknown id in sequence order) → R11 inactive (the first inactive id in sequence order). Checking R7 before R8 means R8 counts distinct stations. `Position` is assigned `1..n` inside the factory, so contiguity (R18, S24) holds by construction. | AGENTS.md rule 3: every rule can be tested without a database. Station existence and activity are facts the aggregate is *given*, and deciding what they mean stays with the aggregate. A fixed precedence makes the error a caller sees deterministic, and a domain test pins it. | Checks in the handler; a separate domain service (for one call site) |
| **P5** | **Handler order in `CreateRouteHandler`:** `400` (validation filter) → `400 Network.InvalidRouteCode` → `400 Network.InvalidRouteName` → load station states → `Route.Create` (the four `422`s) → `409` code pre-check → one `SaveChangesAsync` (a `409` from `UX_Routes_Code`). | Checks run from cheapest and most local to most global: a request is judged valid in itself before it is checked against other routes. The `409` is always last, whether it comes from the pre-check or the index, so one rule describes both. | `409` first (the F-001 order, which had no `422`s) |
| **P6** | **`stationIds` is `IReadOnlyList<string?>?` in `CreateRouteRequest`, not `Guid[]`.** The validator requires the list to be present and non-empty, and every element to be a `D`-format GUID (`Guid.TryParseExact(value, "D", …)`), giving `400 Common.ValidationFailed`. `isClosed` is `bool?` with `NotNull()` (R25). `code`, `nameEn` and `nameMy` are `NotEmpty()`, as in `CreateStationRequestValidator`. `ToCommand()` parses the ids after validation. | S5 requires a non-GUID id to return **`Common.ValidationFailed`**. A `Guid[]` would fail JSON binding before the validation filter runs, and the response would lack that `errorCode` (V5). Changing the global binding-failure response would alter every existing endpoint, which is out of scope (AGENTS.md rule 11). `CreateUserRequest.Roles` (`IReadOnlyList<string>?`) is the precedent. The OpenAPI document shows the items as strings, which is what the wire carries. The endpoint summary says they are station ids in GUID `D` format. | `Guid[]` with a global binding-error customisation |
| **P7** | **Index: drop `IX_RouteStations_StationId` (Q1 (a)).** See §DB changes, "Index decision". | No F-003 query uses it (named-query table). | — |
| **P8** | **Three migrations, kept separate** as in F-002: `Network_CreateRoutes` → `Identity_SeedRoutePermissionGrants` → `Security_NetworkRouteGrants`. | F-002 kept schema, seed and grants apart (`Identity_CreateIdentitySchema`, `Identity_SeedRolesAndPermissionGrants`, `Security_IdentityGrants`). Each has its own reviewer concern and its own `Down()`. The seed writes `identity.RolePermissions`, so it is an `Identity_` migration. The grant name is spec §7's. | One migration (mixes three review concerns and three rollbacks) |
| **P9** | **Audit.** The actions are the literals `"Network.RouteCreated"` and `"Network.RouteDeactivated"`, as the Network slice writes `"Network.StationCreated"`. The subject is `NetworkAuditSubjects.Route = "Network.Route"`. `RouteAuditSnapshot.From(Route route, IReadOnlyDictionary<Guid, string> stationCodes)` is built in the handler, which already has the codes: from the station lookup on create, and from one `Stations` query on deactivate. `AuthorizedByPermission` comes from the endpoint through `ICurrentUser`, as today, so it is `routes.manage` without any handler code. | Follows the surrounding Network code (`docs/20` §1 puts snapshots in Application). Snapshots never carry the aggregate (ADR-0021). | A `NetworkAuditActions` class (a refactor of the station slice, out of scope) |
| **P10** | **Deactivation** follows F-001's pattern. `Route.Deactivate(DateTimeOffset nowUtc)` refuses a second call (`RouteAlreadyInactive`) and a non-UTC time (throws, as `Station.Create` does), sets `IsActive = false` and `DeactivatedAtUtc = nowUtc`, and raises `RouteDeactivated(Guid RouteId)`. The event is collected and not dispatched (F-001 P6). The handler takes `TimeProvider`, writes the audit row, and calls `SaveChangesAsync` **once**. `DbUpdateConcurrencyException` becomes `RouteAlreadyInactive`. Only `IsActive` is a concurrency token (R17). | The loser's `UPDATE … WHERE IsActive = 1` matches no row, so its `DeactivatedAtUtc` and audit row roll back with it. The winner's timestamp stands (S23). | A `rowversion` (ruled out by E1/E8) |
| **P11** | **Reads project, and never materialise the aggregate** (`StationProjection` pattern). `GetRouteHandler` runs two `AsNoTracking` queries: (1) the route row, (2) its stations joined to `network.Stations` for the current code, names and `IsActive`, ordered by `Position` (R24). `ListRoutesHandler` orders by `Code` (unique, so a total order), counts with SQL `COUNT`, pages with `OFFSET/FETCH`, and projects `stationCount` as a subquery. It reuses `Paging` and `NetworkErrors.InvalidPageRequest`. The DTOs (`RouteDto`, `RouteStationDto`, `RouteSummaryDto`) are separate from the API responses (F-001 P5). | ADR-0004 and `docs/20` §4. The whole-value-object projection explained in `StationProjection` applies to `RouteCode` too. | One query with a nested collection (kept as V2's fallback) |
| **P12** | **No new packages.** | Everything used is already referenced. | — |

---

## Affected modules and files

Modules touched: **`Network`** (routes), **`Identity`** (a data-only seed migration, no code), solution-wide `Common` (two permission constants). `Audit` gets new rows, not new code. No other module changes. Nothing crosses a module boundary: `Route` and `Station` are both `Network` (ADR-0012 item 6).

### `src/YCR.Domain/Network/`

| File | New / Changed | Why |
|---|---|---|
| `Route.cs` | New | Aggregate: `Create` (R7, R8, R11, R16, R18, UTC), `Deactivate` (R15, R17); exposes no other mutator (R9) |
| `RouteStation.cs` | New | Child row: `RouteId`, `Position`, `StationId`; internal constructor; no `Station` navigation (E5) |
| `RouteCode.cs` | New | Value object: `Create` → `Result<RouteCode>` (`Network.InvalidRouteCode`); internal `From` for EF (P2) |
| `NetworkCodeFormat.cs` | New | The shared `^[A-Z0-9]{2,10}$` rule (P2) |
| `RouteDeactivated.cs` | New | Domain event, collected and not dispatched (P10) |
| `StationCode.cs` | Changed | Calls `NetworkCodeFormat.IsValid`; behaviour unchanged (existing `StationCodeTests` prove it) |
| `BilingualName.cs` | Changed | Adds the `whenInvalid` overload; the XML comment covers route names (P3) |
| `NetworkErrors.cs` | Changed | `InvalidRouteCode`, `InvalidRouteName` (Validation); `RouteCodeAlreadyExists(code)` (Conflict); `RouteNotFound` (NotFound); `RouteStationNotFound(stationId)`, `RouteStationInactive(stationId)`, `RouteStationRepeated(stationId)`, `RouteTooFewStations` and `RouteAlreadyInactive` (BusinessRule, so `422`; `RouteStationNotFound` is deliberately **not** `NotFound`, E8) |
| `Properties/AssemblyInfo.cs` | Unchanged | Already grants `YCR.Infrastructure` access to internals, which is how it reaches `RouteCode.From` |

### `src/YCR.Application/`

| File | New / Changed | Why |
|---|---|---|
| `Common/Authorization/Permissions.cs` | Changed | `RoutesManage = "routes.manage"`, `RoutesRead = "routes.read"`, with the OQ40 provisional-ruling comment |
| `Network/INetworkDbContext.cs` | Changed | `DbSet<Route> Routes` |
| `Network/NetworkConstraints.cs` | Changed | `RouteCodeUniqueIndex = "UX_Routes_Code"`; `RouteStationUniqueIndex` = the Q1 name |
| `Network/NetworkAuditSubjects.cs` | Changed | `Route = "Network.Route"` |
| `Network/RouteAuditSnapshot.cs` | New | `RouteAuditSnapshot(Code, NameEn, NameMy, IsClosed, IsActive, DeactivatedAtUtc, Stations)` plus `RouteStationAuditSnapshot(Position, StationId, StationCode)`, both records (§Audit) |
| `Network/CreateRoute/CreateRouteCommand.cs`, `CreateRouteHandler.cs` | New | P4, P5 |
| `Network/DeactivateRoute/DeactivateRouteCommand.cs`, `DeactivateRouteHandler.cs` | New | P10 |
| `Network/GetRoute/GetRouteQuery.cs`, `GetRouteHandler.cs`, `RouteDto.cs`, `RouteStationDto.cs`, `RouteProjection.cs` | New | P11 |
| `Network/ListRoutes/ListRoutesQuery.cs`, `ListRoutesHandler.cs`, `RouteSummaryDto.cs` | New | P11 |
| `DependencyInjection.cs` | Unchanged | Handlers are registered by assembly scanning. `DependencyInjectionTests` proves the four new handlers resolve. |

### `src/YCR.Infrastructure/`

| File | New / Changed | Why |
|---|---|---|
| `Persistence/Configurations/Network/RouteConfiguration.cs` | New | Table, check constraints in the model, `UX_Routes_Code`, `IsActive` concurrency token, the `HasMany` to `RouteStation` (P1) |
| `Persistence/Configurations/Network/RouteStationConfiguration.cs` | New | Composite PK, `CK_RouteStations_Position`, the unique index (Q1), the FK to `Station` with `NO ACTION` and no navigation |
| `Persistence/YcrDbContext.cs` | Changed | `public DbSet<Route> Routes => Set<Route>();` |
| `Persistence/Migrations/<ts>_Network_CreateRoutes.cs` (+ `.Designer.cs`) | New | §DB changes |
| `Persistence/Migrations/<ts>_Identity_SeedRoutePermissionGrants.cs` (+ `.Designer.cs`) | New | ″ |
| `Persistence/Migrations/<ts>_Security_NetworkRouteGrants.cs` (+ `.Designer.cs`) | New | ″ |
| `Persistence/Migrations/YcrDbContextModelSnapshot.cs` | Changed | Generated by `dotnet ef migrations add` |

### `src/YCR.Api/`

| File | New / Changed | Why |
|---|---|---|
| `Contracts/Network/RouteContracts.cs` | New | `CreateRouteRequest` (+ `CreateRouteRequestValidator`, P6), `CreateRouteResponse`, `RouteResponse`, `RouteStationResponse`, `RouteSummaryResponse`; reuses `PagedResponse<T>` |
| `Endpoints/Network/RouteEndpoints.cs` | New | `MapRouteEndpoints`: four endpoints, tag `Routes`, same shape as `StationEndpoints` |
| `Program.cs` | Changed | `api.MapRouteEndpoints();` next to `MapStationEndpoints()` |
| `YCR.Api.http` | Changed | A "Routes (F-003)" section (§API changes) |

### Tests (names in §Test plan)

| File | New / Changed |
|---|---|
| `tests/YCR.Domain.Tests/Network/RouteTests.cs`, `RouteCodeTests.cs` | New |
| `tests/YCR.Domain.Tests/Network/BilingualNameTests.cs` | Changed: the overload cases added; existing cases untouched |
| `tests/YCR.Application.Tests/Network/CreateRouteHandlerTests.cs`, `DeactivateRouteHandlerTests.cs`, `RouteQueryHandlerTests.cs`, `ListRoutesSqlTests.cs`, `RouteTestData.cs` (a helper that inserts stations through `CreateStationHandler`) | New |
| `tests/YCR.Application.Tests/Network/DeactivateStationHandlerTests.cs` | Changed: one S17 case added |
| `tests/YCR.Application.Tests/DependencyInjectionTests.cs` | Changed: the four route handlers added to what it resolves |
| `tests/YCR.Infrastructure.Tests/Persistence/RouteModelTests.cs`, `RouteMigrationTests.cs` | New |
| `tests/YCR.Infrastructure.Tests/Persistence/DatabasePrivilegeTests.cs` | Changed: route grant tests added; the DDL theory gains two route rows; `ApplicationCredential_CannotWriteRolesOrGrants` asserts 24 rows, not 14 |
| `tests/YCR.Infrastructure.Tests/Identity/IdentitySeedTests.cs` | Changed: `Seed_ProducesExactlyEightRolesAndFourteenGrants` becomes `…TwentyFourGrants` with the ten route rows in its expected set; `ReadDocs10Grants` also parses `## Route permission grants` (the same bullet format as the station section) |
| `tests/YCR.Api.Tests/Network/RouteEndpointsTests.cs` | New |
| `tests/YCR.Api.Tests/Identity/RoutePermissionGrantTests.cs` | New: real tokens, seeded grants |
| `tests/YCR.Api.Tests/Common/DeployedShapeTests.cs` | Changed: four route rows in `ProtectedEndpoint_Anonymous_Returns401BearerChallengeWithProblemDetails` |
| `.github/workflows/ci.yml` | Changed: `api-smoke` route checks |

**Not changed:** `StationEndpoints`, `DeactivateStationHandler`, `Station` (R12). F-001's station scenarios, including S27, must stay green unchanged (spec §Notes).

---

## Domain changes

### `Route` (aggregate root, `YCR.Domain.Network`)

State: `Id`, `Code` (`RouteCode`), `Name` (`BilingualName`), `IsClosed`, `IsActive`, `CreatedAtUtc`, `DeactivatedAtUtc?`, `Stations` (`IReadOnlyList<RouteStation>`, in position order).

| Method | Rule, and where each check lives | Error |
|---|---|---|
| `Create(id, code, name, isClosed, stationIds, knownStationIsActive, nowUtc)` | `nowUtc` has a non-zero offset → throws (ADR-0018; same guard as `Station.Create`). Then, in this order (P4): **R7** a repeated id → `RouteStationRepeated(firstRepeatedId)`; **R8** `stationIds.Count < (isClosed ? 3 : 2)` → `RouteTooFewStations`; **R16** an id missing from the map → `RouteStationNotFound(id)`; **R11** an id mapped to `false` → `RouteStationInactive(id)`. On success: `IsActive = true`, `DeactivatedAtUtc = null`, one `RouteStation(Id, position, stationId)` per id with positions `1..n` (**R18**). **R6:** the factory never appends the first station at the end; a closed route is the `IsClosed` flag only. | see left |
| `Deactivate(nowUtc)` | Inactive → error, and nothing changes (**R15**). Non-UTC → throws. Otherwise sets `IsActive = false` and `DeactivatedAtUtc = nowUtc`, and raises `RouteDeactivated` | `RouteAlreadyInactive` |

**R9 (immutability)** is structural. `Route` has private setters, no method other than `Deactivate` changes state, and `Stations` is read-only. A domain test checks this by reflection (`Route_ExposesNoMutatorOtherThanDeactivate`). The database enforces it as well (R10, S25).

### Where each sequence check lives

| Rule | Domain (authority for the error code) | Database (authority for integrity) |
|---|---|---|
| R7 repeated station | `Route.Create` | The unique index on the station pair (Q1). A violation is mapped by constraint name to `RouteStationRepeated`. This is defence in depth: one validated aggregate insert cannot trigger it. |
| R8 minimum length | `Route.Create` | none: a check constraint cannot count rows (spec §7) |
| R11 inactive station | `Route.Create`, with the handler's loaded map | none. The race with a station deactivation is **accepted** (spec §5, hein 2026-09-24): no lock and no serialisation, because the end state equals the serial order "create, then deactivate", which R12 allows. **Not reopened here.** |
| R16 station exists | `Route.Create`, with the loaded map | `FK_RouteStations_Stations_StationId`. Stations are never deleted (no `DELETE` grant; F-001 R3), so the pre-check cannot go stale. |
| R18 contiguous positions | `Route.Create` assigns them | `PK_RouteStations (RouteId, Position)` + `CK_RouteStations_Position` (`>= 1`) |

`RouteCode` (P2) and `BilingualName` (P3) enforce **R13**. `UX_Routes_Code` enforces **R14**, including inactive routes, because rows are never deleted.

---

## DB changes

Three migrations, applied in order by the EF migration bundle under `ycr_migrator` (F-001 P1; ADR-0022), each reviewed per `docs/workflows/04-database-change.md`. On-disk names are `yyyyMMddHHmmss_<Module>_<Change>` (`docs/20` §6). EF generates the timestamp. `Security_` follows the two existing grant migrations; `docs/20` §1's module list does not name it, but the precedent does. **Upgrade path:** all three are additive on F-002's schema (last migration `20260923133743_Security_IdentityGrants`), and `RouteMigrationTests` migrates a database already at that migration, with station rows in it (`docs/21` §Data).

### 1. `<ts>_Network_CreateRoutes` (EF model-built)

Every object below is declared in the EF model, including the check constraints, so `has-pending-model-changes` sees drift (V4; the `StationConfiguration` R-4 rule).

**`network.Routes`**

| Column | Type | Null |
|---|---|---|
| `Id` | `uniqueidentifier` | no; PK clustered; `ValueGeneratedNever()` (R19, ADR-0006) |
| `Code` | `nvarchar(10)` | no |
| `NameEn` | `nvarchar(100)` | no |
| `NameMy` | `nvarchar(100)` | no |
| `IsClosed` | `bit` | no |
| `IsActive` | `bit` | no; EF concurrency token |
| `CreatedAtUtc` | `datetimeoffset(3)` | no |
| `DeactivatedAtUtc` | `datetimeoffset(3)` | yes |

Objects: `PK_Routes` · `UX_Routes_Code` (unique on `Code`) · `CK_Routes_CreatedAtUtc_Utc` = `DATEPART(TZOFFSET, [CreatedAtUtc]) = 0` · `CK_Routes_DeactivatedAtUtc_Utc` = `[DeactivatedAtUtc] IS NULL OR DATEPART(TZOFFSET, [DeactivatedAtUtc]) = 0`. No `rowversion`, no direction column (spec §7).

**`network.RouteStations`**

| Column | Type | Null |
|---|---|---|
| `RouteId` | `uniqueidentifier` | no |
| `Position` | `int` | no |
| `StationId` | `uniqueidentifier` | no |

Objects: `PK_RouteStations` on `(RouteId, Position)` · `CK_RouteStations_Position` = `[Position] >= 1` · the unique station index (**Q1**: `UX_RouteStations_StationId_RouteId` on `(StationId, RouteId)` recommended, or the spec's `UX_RouteStations_RouteId_StationId` on `(RouteId, StationId)`) · `FK_RouteStations_Routes_RouteId` → `network.Routes(Id)` **NO ACTION** · `FK_RouteStations_Stations_StationId` → `network.Stations(Id)` **NO ACTION** (O4: EF's default for the route FK is `Cascade`, so `OnDelete(NoAction)` is explicit, and `RouteModelTests` pins both).

`network.Stations` is not altered. No existing column changes. Data impact: none (new, empty tables). **No seed or fixture route** (R23, OQ1).

**Index decision — `IX_RouteStations_StationId`: dropped (P7, ruling "IX" in spec §0.8).** Each query and write in F-003, with the index it uses:

| Named query / write | Access path |
|---|---|
| `CreateRouteHandler` code pre-check `Routes.Any(Code == …)` | `UX_Routes_Code` seek |
| `CreateRouteHandler` station states `Stations.Where(ids.Contains(Id))` | `PK_Stations` (O5: one JSON parameter) |
| `INSERT` into `RouteStations`, FK validation | `PK_Routes`, `PK_Stations` |
| `DeactivateRouteHandler` load route and children; station codes for the snapshot | `PK_Routes`; `PK_RouteStations` range on `RouteId`; `PK_Stations` |
| `GetRouteHandler` route row; stations by position joined to `Stations` | `PK_Routes`; `PK_RouteStations` range on `RouteId`; `PK_Stations` |
| `ListRoutesHandler` count, page by `Code`, `stationCount` | `UX_Routes_Code` (ordered scan); `PK_RouteStations` prefix per route |
| Reverse FK check on `DELETE FROM Stations` | **never runs**: `ycr_app` has no `DELETE` on `Stations`, and codes are never reused (F-001 R3) |
| "Which routes contain station X?" | **no such query in F-003.** OQ39 removed the station-deactivation check it was designed for (spec §7) |

No query filters `RouteStations` by `StationId`, so the index is dropped. EF adds it back by convention unless another index leads with `StationId` (O1–O3). The recommended way to drop it is Q1 (a). If a later feature adds a "routes through station X" read, it gets its index from the Q1 (a) unique index, or from its own reviewed migration under Q1 (b).

**Rollback.** `Down()` drops `RouteStations`, then `Routes`. It **does not** drop the `network` schema, which `Network_CreateStations` owns (FACT above). After real routes exist, a down-migration destroys route history, so the recovery path in production is restoring a backup, not `Down()` (as in F-002). **Roll-forward:** any correction is a new migration. An applied migration is never edited (`docs/20` §6).

### 2. `<ts>_Identity_SeedRoutePermissionGrants`

Inserts exactly ten `identity.RolePermissions` rows, per `docs/10` §Route permission grants:

- `routes.manage` → `SystemAdministrator`, `RailwayAdministrator`
- `routes.read` → all eight roles

Role ids are the fixed GUIDs from `Identity_SeedRolesAndPermissionGrants`. They are re-declared as constants, because that migration's constants are private and a migration must not depend on another migration's code. Header comment: "BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-24; T-032, OQ40) — not a Myanma Railways answer." `IdentitySeedTests.Seed_MatchesDocs10GrantTables` keeps this file and `docs/10` in step. No role and no user is added.

**Rollback.** `Down()` deletes exactly those ten `(RoleId, Permission)` pairs. That is safe, because nothing references `RolePermissions`. The effect is that route endpoints return `403` to everyone, which is the correct result of removing the grants. **Roll-forward:** a changed grant (for example, an official Myanma Railways answer to OQ40) is a new reviewed migration, never an API call (F-002 D8).

### 3. `<ts>_Security_NetworkRouteGrants`

Exactly spec R10 / S25, as raw SQL, like `Security_IdentityGrants`:

```sql
GRANT SELECT, INSERT ON [network].[Routes] TO [ycr_app];
GRANT UPDATE ON [network].[Routes]([IsActive], [DeactivatedAtUtc]) TO [ycr_app];
GRANT SELECT, INSERT ON [network].[RouteStations] TO [ycr_app];
```

**Deliberately absent**, and asserted by `DatabasePrivilegeTests`: `DELETE` on either table; `UPDATE` on `Routes.Id`, `Code`, `NameEn`, `NameMy`, `IsClosed`, `CreatedAtUtc`; any `UPDATE` on `RouteStations`; any DDL (`ALTER`, `CONTROL`, `CREATE`, `DROP`). The migration's XML comment states R9: these grants are the database-level statement that a stored sequence never changes.

**Rollback.** `Down()` revokes exactly these grants, in reverse order. It destroys no data. **Roll-forward:** a wider grant needs a new migration, a spec change and a review. It is never widened to make a test pass.

**Order and why.** 1 must come before 3, because 3 grants on objects 1 creates. 2 depends only on F-002's roles. The order 1 → 2 → 3 mirrors F-002 (schema → seed → grants).

---

## API changes

Base path `/api/v1`, JSON camelCase, ProblemDetails with `errorCode` and `traceId` (ADR-0004). Every endpoint below also returns F-002's `401 Auth.Unauthenticated` (with `WWW-Authenticate: Bearer`) and, during a must-change session, `403 Auth.PasswordChangeRequired` (`docs/08` §Applies to every protected endpoint). None takes an `Idempotency-Key` (R21). No request or response carries a version (R17).

### Endpoint inventory

| Method | Path | Permission | Idempotency | Response / errors |
|---|---|---|---|---|
| POST | `/api/v1/routes` | `routes.manage` | No (R21) | `201` + `CreateRouteResponse { id }` and `Location: /api/v1/routes/{id}` · `400 Common.ValidationFailed` · `400 Network.InvalidRouteCode` · `400 Network.InvalidRouteName` · `401` · `403` · `409 Network.RouteCodeAlreadyExists` · `422 Network.RouteStationRepeated` · `422 Network.RouteTooFewStations` · `422 Network.RouteStationNotFound` · `422 Network.RouteStationInactive` |
| GET | `/api/v1/routes/{id:guid}` | `routes.read` | n/a | `200` + `RouteResponse` · `401` · `403` · `404 Network.RouteNotFound` |
| GET | `/api/v1/routes` | `routes.read` | n/a | `200` + `{ items: RouteSummaryResponse[], page, pageSize, totalCount }`, ordered by `code`, inactive routes included · `400 Network.InvalidPageRequest` · `401` · `403` |
| POST | `/api/v1/routes/{id:guid}/deactivate` | `routes.manage` | No (R21) | `204` · `401` · `403` · `404 Network.RouteNotFound` · `422 Network.RouteAlreadyInactive` |
| POST | `/api/v1/stations/{id:guid}/deactivate` | `stations.manage` | No | **Unchanged** (F-001). No new error code (R12) |

**Deliberately absent** (spec §6): `PUT /routes/{id}/stations`, `PATCH /routes/{id}`, reactivation, `DELETE`.

### Contracts

```text
CreateRouteRequest   { code: string?, nameEn: string?, nameMy: string?, isClosed: bool?, stationIds: string?[]? }  // P6
CreateRouteResponse  { id }
RouteResponse        { id, code, nameEn, nameMy, isClosed, isActive, createdAtUtc, deactivatedAtUtc?,
                       stations: RouteStationResponse[] }            // position order
RouteStationResponse { position, stationId, code, nameEn, nameMy, isActive }   // the station's current values (R24)
RouteSummaryResponse { id, code, nameEn, nameMy, isClosed, isActive, stationCount, createdAtUtc, deactivatedAtUtc? }
```

`CreateRouteRequest` has no actor, address or correlation field, and never gets one (S22; `CreateStationRequest` comment). `deactivatedAtUtc` is `null` while the route is active (S23). Every response is mapped explicitly from a DTO. No EF entity is serialised (AGENTS.md rule 4), and the P11 architecture rule forbids the Api from referencing `YCR.Domain.Network` at all.

### Error code → HTTP

| Code | `ErrorType` | HTTP |
|---|---|---|
| `Common.ValidationFailed` (validation filter) | — | 400 |
| `Network.InvalidRouteCode`, `Network.InvalidRouteName`, `Network.InvalidPageRequest` | Validation | 400 |
| `Network.RouteNotFound` | NotFound | 404 |
| `Network.RouteCodeAlreadyExists` (pre-check, or `UX_Routes_Code` by name) | Conflict | 409 |
| `Network.RouteStationNotFound`, `Network.RouteStationInactive`, `Network.RouteStationRepeated` (also the station-index name), `Network.RouteTooFewStations`, `Network.RouteAlreadyInactive` (also `DbUpdateConcurrencyException`) | BusinessRule | 422 |

Only `ResultExtensions` maps these, through the ADR-0004 table. Neither an endpoint nor a handler chooses a status code. A unique violation on any **other** constraint is not caught and propagates as `500`, as in `CreateStationHandler`.

### Concurrency

- **Deactivation:** `IsActive` is the concurrency token (P10; F-001 Amendment 1). Two parallel deactivations give one `204`, one `422 Network.RouteAlreadyInactive` and one audit event. The loser changes neither column (S23).
- **Duplicate codes:** `UX_Routes_Code` settles the race. `CreateRouteHandler` catches `UniqueConstraintViolationException` only when `ConstraintName == NetworkConstraints.RouteCodeUniqueIndex` → `409` (S21). A second filter maps the station-index name to `RouteStationRepeated`. Both names are pinned by `RouteModelTests`.
- **Creation vs station deactivation:** accepted, not serialised (spec §5). No lock and no test of the interleaving.

### OpenAPI, `.http`, smoke

- **OpenAPI/Scalar exposure: unchanged from F-001.** `/openapi/v1.json` and `/scalar` are served in Development only. The route endpoints appear in that document under the tag `Routes`, with `.WithName(...)`, `.WithSummary(...)`, `.Produces<…>()` and `.ProducesProblem(...)` as `StationEndpoints` declares them.
- **`YCR.Api.http`**, a new "Routes (F-003)" section: create an open route (3 stations) → `201`; a closed route with 2 stations → `422 Network.RouteTooFewStations`; a repeated station → `422 Network.RouteStationRepeated`; list → `200`; read one → `200`; deactivate → `204`, and again → `422 Network.RouteAlreadyInactive`. It adds `@routeId` and `@stationId2`/`@stationId3` variables, and each comment names the permission and the errors, as the station section does.
- **`api-smoke` additions** (after the existing real-token `GET /api/v1/stations`, with the same administrator token, so they run through the seed and the grants under `ycr_app` in the deployed shape):
  1. Anonymous `GET /api/v1/routes` → `401` with `errorCode` `Auth.Unauthenticated`.
  2. Create two stations (`SMA`, `SMB`) → `201` each.
  3. `POST /api/v1/routes` `{ code: "SMOKE1", isClosed: false, stationIds: [SMA, SMB] }` → `201`.
  4. `GET /api/v1/routes/{id}` → `200` with two stations.
  5. `GET /api/v1/routes` → `200`.
  6. `POST /api/v1/routes/{id}/deactivate` → `204`, and again → `422` with `errorCode` `Network.RouteAlreadyInactive`.

  `CiWorkflowTests` inspects the job's `env:` and provisioning, not its checks, so it needs no change. Step 7 confirms that.

---

## Audit

Through `IAuditWriter.Record`, inside the handler's single `SaveChangesAsync` (ADR-0017, ADR-0021; F-001 pattern). A losing concurrent request therefore writes no event.

| Action | When | `SubjectType` | `SubjectId` | `BeforeJson` | `AfterJson` | `AuthorizedByPermission` |
|---|---|---|---|---|---|---|
| `Network.RouteCreated` | S1 | `Network.Route` | route id | null | snapshot | `routes.manage` (from the endpoint, through `ICurrentUser`) |
| `Network.RouteDeactivated` | S23 | `Network.Route` | route id | snapshot (active) | snapshot (inactive, `deactivatedAtUtc` set) | `routes.manage` |
| `Network.StationDeactivated` | S17 | unchanged from F-001 | | | | |

`NetworkAuditSubjects.Route = "Network.Route"`. `PayloadVersion = 1`.

```text
RouteAuditSnapshot(string Code, string NameEn, string NameMy, bool IsClosed, bool IsActive,
                   DateTimeOffset? DeactivatedAtUtc,
                   IReadOnlyList<RouteStationAuditSnapshot> Stations) : IAuditSnapshot
RouteStationAuditSnapshot(int Position, Guid StationId, string StationCode)
```

Both are records in `YCR.Application.Network`, so the existing `AuditSnapshots_WithSourceAssemblies_AreValid` rule covers the outer one. The station code makes a ledger row readable without a join, and it is stable because station codes are never reused (F-001 R3). No personal data, token or key can appear (ADR-0021 rule 4). The XML comment carries the "changing this shape bumps `PayloadVersion`" warning from `StationAuditSnapshot`.

**Logging:** message templates only (`docs/20` §7). The handlers log nothing beyond what F-001's handlers log. **Metrics:** none (`docs/17` names no route event; spec §8).

---

## Security impact

| Item | Treatment |
|---|---|
| Permissions | `routes.manage` on `POST /routes` and `POST /routes/{id}/deactivate`; `routes.read` on both `GET`s; each through `.RequireAuthorization(Permissions.…)`. No anonymous route endpoint. `stations.*` gives no route right and the reverse (R3); tested both ways (S15). |
| Grants | Data, seeded by the reviewed `Identity_SeedRoutePermissionGrants` (OQ40 provisional ruling). No API edits them. `RoutePermissionGrantTests` proves the seed end to end with **real tokens** (a `TicketOperator` can read but not manage; a `RailwayAdministrator` can manage). |
| Database least privilege | `Security_NetworkRouteGrants` (R10). `ycr_app` cannot rewrite or delete a sequence even with arbitrary SQL. `DatabasePrivilegeTests` asserts presences **and** absences (S25), and every Application and API test runs as `ycr_app`, so a missing grant fails a test. |
| Audit integrity | Actor fields come from `ICurrentUser`, never from the body (S22). The ledger is append-only (ADR-0017), and snapshots are explicit records. |
| Input handling | Codes and names are validated by value objects. Station ids must be `D`-format GUIDs (P6). Pagination is capped at 200. The `stationIds` count has no limit (Q2). EF parameterises all SQL, and the id list is one JSON parameter (O5). |
| Error disclosure | Error messages name only the caller's own input (a code, or a station id). A `500` carries no internals (F-001 S25 handler). |

**`docs/18` threats this feature touches:** *Unauthorized configuration* (route definitions are railway operational configuration; `routes.manage` held by two roles; every change audited). *Privilege escalation* (seeded grants only, no grant API, `DatabasePrivilegeTests`). *Insider manipulation* (immutable sequences enforced by the grants as well as the domain; deactivation audited with before and after). *Data disclosure* (reads need `routes.read`; responses carry no personal data). *API abuse* (pagination cap; Q2 for the sequence length). *Audit tampering* (unchanged ledger controls). Cookie, CSP and XSS controls are not touched: F-003 adds no cookie-bearing endpoint and no SPA.

---

## Test plan

Names follow `docs/20` §2. "TH" = F-001's test authentication handler; "Real" = real ES256 tokens through F-002's pipeline. Application and API tests run as `ycr_app`. Stage 5 (a different agent) owns completeness, and checks itself against this table.

### `YCR.Domain.Tests/Network`

| Test | Spec |
|---|---|
| `RouteCode_Create_WithValidCode_ReturnsCode` (theory: `R1`, `AB`, `ABCDEFGHIJ`, ` LOOP1 ` trimmed) | S11, R13 |
| `RouteCode_Create_WithInvalidCode_ReturnsInvalidRouteCode` (theory: 1 char, 11 chars, lowercase, `R-1`, `R 1`, empty, whitespace, null) | S11, R13 |
| `BilingualName_Create_WithRouteError_WhenInvalid_ReturnsSuppliedError` (theory: blank, whitespace-only, 101 chars after trimming, either side) | S11, R13 |
| `BilingualName_Create_WithoutErrorArgument_StillReturnsInvalidStationName` | regression (P3) |
| `Create_WithThreeActiveStations_ReturnsActiveOpenRouteWithPositionsOneToThree` | S1, S24, R18 |
| `Create_WithNonUtcTime_Throws` | R22 |
| `Create_WithRepeatedStation_ReturnsRouteStationRepeated` (theory incl. `[A,B,C,A]` closed) | S8, R6, R7 |
| `Create_OpenWithOneStation_ReturnsRouteTooFewStations` · `Create_OpenWithTwoStations_Succeeds` · `Create_ClosedWithTwoStations_ReturnsRouteTooFewStations` · `Create_ClosedWithThreeStations_Succeeds` | S9, R8 |
| `Create_WithUnknownStation_ReturnsRouteStationNotFound` | S6, R16 |
| `Create_WithInactiveStation_ReturnsRouteStationInactive` | S7, R11 |
| `Create_WithSeveralFailures_ReportsThemInFixedPrecedence` | P4 |
| `Create_ClosedRoute_HasFirstStationOnlyAtPositionOne` | S28, R6 |
| `Deactivate_WhenActive_SetsInactiveAndDeactivatedAtAndRaisesEvent` | S23, R15 |
| `Deactivate_WhenInactive_ReturnsRouteAlreadyInactiveAndKeepsFirstTimestamp` | S23, R15 |
| `Deactivate_WithNonUtcTime_Throws` | R22 |
| `Route_ExposesNoMutatorOtherThanDeactivate` (reflection: no public setter or state-changing method; `Stations` read-only) | R9 |
| `RouteStation_HasNoNavigationToStation` (reflection: no property of type `Station`) | E5, R16 |
| existing `StationCodeTests`, `BilingualNameTests`, `StationTests` | regression for P2/P3 |

### `YCR.Application.Tests/Network` (real SQL Server, `ycr_app`, `TestClock`)

| Test | Spec |
|---|---|
| `CreateRoute_WithValidCommand_PersistsRoutePositionsAndOneAuditEvent` (row values, positions 1..3, `IsClosed = 0`, `IsActive = 1`, `DeactivatedAtUtc` null; audit subject `Network.Route`, snapshot content, `AuthorizedByPermission` = `routes.manage`) | S1, S24 |
| `CreateRoute_WithUnknownStation_ReturnsRouteStationNotFoundAndWritesNothing` | S6 |
| `CreateRoute_WithInactiveStation_ReturnsRouteStationInactiveAndWritesNothing` | S7 |
| `CreateRoute_WithRepeatedStation_ReturnsRouteStationRepeatedAndWritesNothing` | S8 |
| `CreateRoute_AtAndBelowMinimumLength_CreatesOrReturnsTooFew` (theory: open 1/2, closed 2/3) | S9 |
| `CreateRoute_WithCodeOfActiveRoute_ReturnsConflictAndWritesNothing` | S10 |
| `CreateRoute_WithInvalidCodeOrName_ReturnsValidationErrorAndWritesNothing` (theory) | S11 |
| `CreateRoute_WithParallelDuplicateCodes_PersistsExactlyOneRouteAndOneAuditEvent` (two scopes, `Task.WhenAll`) | **S21** |
| `CreateRoute_ParallelDuplicateLoser_LeavesNoRouteStationsRows` | S24 |
| `CreateRoute_AuditActor_ComesFromCurrentUserOnly` | S22 |
| `CreateRoute_WithMyanmarName_RoundTripsExactly` (`\u`-escaped literal, F-001 R-10) | S26 |
| `CreateRoute_SharingStationsWithAnotherRoute_CreatesBothWithOwnPositions` | S29 |
| `CreateRoute_WithCodeOfDeactivatedRoute_ReturnsConflictAndWritesNothing` | S30 |
| `DeactivateRoute_WhenActive_SetsInactiveAndClockTimestampAndWritesOneAuditEvent` (before/after snapshots) | S23 |
| `DeactivateRoute_WhenInactive_ReturnsAlreadyInactiveWritesNoAuditAndKeepsTimestamp` | S23 |
| `DeactivateRoute_WithParallelRequests_OneSucceedsOneAlreadyInactiveOneAuditEvent` (the loser changes neither column) | **S23** |
| `DeactivateRoute_KeepsRouteAndRouteStationsRows` | S23 |
| `DeactivateRoute_WithUnknownId_ReturnsNotFound` | S12 |
| `GetRoute_WithKnownId_ReturnsStationsInPositionOrderWithCurrentStationData` | S2, R24 |
| `GetRoute_ActiveRoute_HasNullDeactivatedAt` | S23 |
| `GetRoute_AfterStationDeactivated_ShowsStationInactiveAtSamePosition` | S17, R12 |
| `GetRoute_ClosedRoute_ReturnsIsClosedAndThreeStationsFirstOnlyAtPositionOne` | S28 |
| `GetRoute_WithUnknownId_ReturnsNotFound` | S12 |
| `ListRoutes_WithThreeRoutesOneInactive_ReturnsPagedEnvelopeOrderedByCode` (incl. `stationCount`, inactive listed) | S3 |
| `ListRoutes_WithPageSizeAbove200_ReturnsInvalidPageRequest` | S13 |
| `ListRoutesSql_PagesCountsAndStationCountsInSql` (V3) | S3 |
| `DeactivateStation_WhenStationIsInActiveRoute_DeactivatesAndLeavesRouteStationsUnchanged` (in `DeactivateStationHandlerTests`) | S17 |
| `DependencyInjection_ResolvesEveryHandler` (existing test, four route handlers added) | ADR-0004 |

### `YCR.Infrastructure.Tests`

| Test | Credential | Spec |
|---|---|---|
| `RouteModel_MapsTablesKeysAndConstraintNames` (unique index names equal `NetworkConstraints` constants; PKs; no `rowversion`; `IsActive` concurrency token) | — | §7, R14, R17 |
| `RouteModel_RouteStationsHasNoStationIdOnlyIndex` (the index decision) | — | P7, Q1 |
| `RouteModel_ForeignKeys_AreNoActionWithoutStationNavigation` | — | §7, E5 |
| `Model_WithUtcColumn_DeclaresItsCheckConstraint` (existing theory, plus `CK_Routes_CreatedAtUtc_Utc` and `CK_Routes_DeactivatedAtUtc_Utc`) · `RouteModel_DeclaresPositionCheck` | — | ADR-0018, §7 |
| `DownMigration_NetworkCreateRoutes_DropsTablesButNotNetworkSchema` | — | Rollback |
| `Migrate_FromF002Schema_CreatesRouteTablesConstraintsIndexesAndGrants` (starts at `Security_IdentityGrants` with station rows present) | migrator | `docs/21` §Data |
| `Migrate_DownToF002_RemovesRouteObjectsGrantsAndSeedRowsAndKeepsStations` | migrator | Rollback |
| `RouteConstraints_RejectNonUtcOffsetsAndNonPositivePositions` | migrator | ADR-0018, §7 |
| `RouteStations_SameStationTwiceInOneRoute_RejectedByUniqueIndex` · `RouteStations_UnknownStation_RejectedByForeignKey` | migrator | R7, R16 |
| `ApplicationCredential_HasExactlyTheRouteGrants`: **presence:** `Routes` `SELECT`, `INSERT`, `UPDATE(IsActive)`, `UPDATE(DeactivatedAtUtc)`; `RouteStations` `SELECT`, `INSERT`. **Absence:** `DELETE` on both; `UPDATE` of `Routes.Id`, `Code`, `NameEn`, `NameMy`, `IsClosed`, `CreatedAtUtc`; `UPDATE` on `RouteStations` (table and each of `RouteId`, `Position`, `StationId`); `ALTER` and `CONTROL` on both | `ycr_app` | **S25**, R10 |
| `ApplicationCredential_CanDeactivateARouteButNotRewriteIt` (executes: the `IsActive`/`DeactivatedAtUtc` update succeeds; `UPDATE … Code`, `UPDATE RouteStations`, `DELETE` on either table are denied, and the rows are unchanged afterwards) | `ycr_app` | **S25**, R9 |
| `ApplicationCredential_AttemptingDdl_IsDenied` (existing theory, plus `ALTER TABLE [network].[Routes] ADD …` and `DROP TABLE [network].[RouteStations]`) | `ycr_app` | S25 |
| `ApplicationCredential_CannotWriteRolesOrGrants` (existing; asserts **24** rows) | `ycr_app` | OQ40, D8 |
| `Seed_ProducesExactlyEightRolesAndTwentyFourGrants` (renamed from `…FourteenGrants`; the ten route rows in the expected set) · `Seed_MatchesDocs10GrantTables` (also parses §Route permission grants) | migrator | OQ40, `docs/10` |
| `Seed_EveryPermissionIsAPermissionsConstant` (existing, unchanged; passes only once `RoutesManage`/`RoutesRead` exist) | migrator | OQ40 |

### `YCR.Api.Tests`

| Test | Mode | Spec |
|---|---|---|
| `Post_WithValidRequest_Returns201WithLocationAndId` | TH | S1 |
| `Get_WithKnownId_ReturnsRouteResponseRecordNotEntity` (exact JSON property set, including the nested stations) | TH | S2 |
| `List_WithThreeRoutesOneInactive_ReturnsPagedEnvelopeOrderedByCode` | TH | S3 |
| `Post_WithInvalidBody_Returns400CommonValidationFailed` (theory: missing `stationIds`; empty; a non-GUID id; a null element; missing `code`, `nameEn`, `nameMy`; missing `isClosed`) | TH | **S5** |
| `Post_WithUnknownStation_Returns422RouteStationNotFound` | TH | S6 |
| `Post_WithInactiveStation_Returns422RouteStationInactive` | TH | S7 |
| `Post_WithRepeatedStation_Returns422RouteStationRepeated` (incl. closed `[A,B,C,A]`) | TH | S8 |
| `Post_AtAndBelowMinimumLength_Returns201Or422` (theory: open 1/2, closed 2/3) | TH | S9 |
| `Post_WithDuplicateCode_Returns409WithErrorCode` | TH | S10 |
| `Post_WithInvalidCode_Returns400InvalidRouteCode` (theory) · `Post_WithInvalidName_Returns400InvalidRouteName` (theory) | TH | S11 |
| `GetAndDeactivate_WithUnknownId_Return404RouteNotFound` | TH | S12 |
| `List_WithPageSizeAbove200_Returns400InvalidPageRequest` | TH | S13 |
| `AnyRouteEndpoint_Anonymous_Returns401` (theory over the four endpoints) | TH | S14 |
| `Post_WithOnlyRoutesRead_Returns403` · `Deactivate_WithOnlyRoutesRead_Returns403` · `Post_WithOnlyStationsManage_Returns403` · `List_WithOnlyStationsRead_Returns403` | TH | **S15** |
| `DeactivateStation_InActiveRoute_Returns204AndRouteShowsStationInactive` | TH | S17 |
| `Post_WhenRequestTriesToSupplyActorFields_RecordsTheAuthenticatedActor` | TH | S22 |
| `Deactivate_WhenActiveThenRepeated_Returns204Then422AndReadsBackInactive` | TH | S23 |
| `Post_WithMyanmarName_RoundTripsThroughGet` | TH | S26 |
| `Get_ClosedRoute_ReturnsIsClosedAndThreeStations` | TH | S28 |
| `Post_SecondRouteSharingStations_Returns201` | TH | S29 |
| `Post_WithCodeOfDeactivatedRoute_Returns409` | TH | S30 |
| `ProtectedEndpoint_Anonymous_Returns401BearerChallengeWithProblemDetails` (existing `DeployedShapeTests` theory, plus four route rows) | **Unmodified** | S14, `docs/21` §Tests |
| `TicketOperator_ReadsRoutesButCannotCreateOrDeactivate` · `RailwayAdministrator_CreatesAndDeactivatesRoute` (`RoutePermissionGrantTests`) | Real | S15, OQ40 |
| existing F-001 `StationEndpointsTests` and every F-002 suite | TH / Real | regression, unchanged |

### `YCR.ArchitectureTests`

No new rule. The existing rules cover the new types: the Api allowlist forbids `YCR.Domain.Network` in the Api (so `Route` cannot be returned), the module-boundary rules scan `YCR.Application.Network`, and the audit-snapshot rules check `RouteAuditSnapshot`. `RouteStation_HasNoNavigationToStation` is a domain test (above), because it is a property of one type, not a dependency rule.

### `YCR.IntegrationTests`

None. F-003 adds no Worker behaviour.

### Scenario coverage

Live scenarios: **24**, which is S1–S30 minus the six removed (S4, S16, S18, S19, S20, S27). Every one maps to at least one named test above:

| Scenario | Tests (project) |
|---|---|
| S1 | Domain, Application, Api |
| S2 | Application, Api |
| S3 | Application (×2), Api |
| S5 | Api |
| S6, S7, S8, S9 | Domain, Application, Api |
| S10 | Application, Api |
| S11 | Domain (×3), Application, Api (×2) |
| S12 | Application (×2), Api |
| S13 | Application, Api |
| S14 | Api (TH + Unmodified) |
| S15 | Api (TH ×4, Real ×2) |
| S17 | Application (×2), Api |
| S21 | Application (parallel) |
| S22 | Application, Api |
| S23 | Domain (×2), Application (×5 incl. parallel), Api |
| S24 | Domain, Application (×2) |
| S25 | Infrastructure (×3) |
| S26 | Application, Api |
| S28 | Domain, Application, Api |
| S29 | Application, Api |
| S30 | Application, Api |

**Counts:** 24 scenarios; **93 named tests** (a theory counts once). **86 are new:** Domain 20, Application 27, Infrastructure 12, Api 27 (including the two real-token tests). **7 are existing tests changed to cover F-003:** `DependencyInjection_ResolvesEveryHandler`, `Model_WithUtcColumn_DeclaresItsCheckConstraint`, `ApplicationCredential_AttemptingDdl_IsDenied`, `ApplicationCredential_CannotWriteRolesOrGrants`, `Seed_ProducesExactlyEightRolesAndTwentyFourGrants` (renamed), `Seed_MatchesDocs10GrantTables` and `ProtectedEndpoint_Anonymous_Returns401BearerChallengeWithProblemDetails`. `Seed_EveryPermissionIsAPermissionsConstant` covers the new rows without a code change. The "existing … regression" rows are not counted. No test is disabled, skipped or deleted.

### Whole suite

`dotnet build YCR.sln` and `dotnet test YCR.sln` green, with no skipped tests. CI green on `origin` for `feature/F-003`, including `has-pending-model-changes` and the extended `api-smoke`.

---

## New packages

**None** (P12). EF Core, FluentValidation, ASP.NET Core OpenAPI, Testcontainers and xUnit v3 are already pinned in `Directory.Packages.props`.
---

## Risks

| # | Risk | Likelihood | Mitigation |
|---|---|---|---|
| R-1 | Q1 is ruled (b), and an index that no query uses stays | Low impact | Its cost is one small nonclustered index on an insert-only table. `docs/07` records why it exists. |
| R-2 | EF writes more than the two granted columns on deactivation, or touches `RouteStations` | Medium | V1. The test fails under `ycr_app` rather than in production. The fix is in the mapping, never in the grant. |
| R-3 | The seed's count changes break F-002 tests that pinned 14 | Certain, planned | Those tests are updated to the exact new set in step 3, each with its reason (§Facts). Their assertions are extended, not loosened. |
| R-4 | `Seed_MatchesDocs10GrantTables` depends on `docs/10`'s route section format | Medium | The route section already uses the station bullet format. The parser reuses that code path. A formatting change fails the test by name (F-002 R-6). |
| R-5 | A handler starts checking sequence rules itself, instead of `Route.Create` | Medium | P4 puts every rule in one factory with domain tests. Stage 6 checks that `CreateRouteHandler` only loads data and maps results. |
| R-6 | A JSON type mismatch (for example `"isClosed": "yes"`) returns the framework's `400`, which may have no `errorCode` | Low | V5 records the behaviour. It predates F-003 and applies to every endpoint, and S5 does not list it, so F-003 does not change it. If hein wants it, it is a cross-cutting follow-up task. |
| R-7 | A very long `stationIds` list | Low | Only two administrator roles can call the endpoint, and each call is audited. O5 shows no parameter-limit failure. Q2. |
| R-8 | The provisional route rulings (OQ36–OQ41) change when Myanma Railways answers | Medium | Each is in one place: `RouteCode`/`NetworkCodeFormat` (OQ41), `Route.Create` (OQ37, OQ39), the seed migration (OQ40). An official answer needs its own follow-up task (spec §0.8). |
| R-9 | Parallel tests are flaky on slow runners | Low | Same pattern as F-001 S13/S27 and F-002 S21: two scopes, `Task.WhenAll`, and assertions on outcome counts, never on which request wins. |

---

## Rollback and forward compatibility

**Migrations.** All three have working `Down()` methods (details in §DB changes). `Security_NetworkRouteGrants` revokes. `Identity_SeedRoutePermissionGrants` deletes exactly its ten rows. `Network_CreateRoutes` drops the two tables and keeps the `network` schema and `Stations`. Once real routes exist, rolling back the schema destroys route history, so production recovery is a restore, not a down-migration (as in F-002). The audit ledger is untouched: only new rows are added, and no schema changes.

**API.** Additive only: four new endpoints. No existing contract changes, and `POST /stations/{id}/deactivate` is unchanged (R12). No client exists yet (no SPA, OQ22).

**Partially deployed instances.** Deployment order is migrations first, as in F-001 and F-002. New code on an old database: the route endpoints fail with `500` until the migrations run (missing table), while station and identity endpoints keep working. Old code on a new database: unaffected, since the tables and grants are additive and the seeded `routes.*` permissions name endpoints the old code does not have.

**Forward compatibility.** `PayloadVersion` 1 on route snapshots. The Q1 (a) index already serves a future "routes through station X" read. `DeactivatedAtUtc` answers "which routes were active on date X" without the ledger (A1).

---

## Steps

Each step ends with `dotnet test YCR.sln` green. No step leaves the branch red. Step 2 needs **Q1** ruled. Step 6's validator needs **Q2** only if Q2 is ruled (b).

| # | Step | Ends green with |
|---|---|---|
| 1 | **Domain:** `NetworkCodeFormat` (and `StationCode` switched to it), `RouteCode`, the `BilingualName` overload, the route errors in `NetworkErrors`, `RouteStation`, `Route`, `RouteDeactivated` | Every `YCR.Domain.Tests` row above, plus the existing `StationCodeTests`, `BilingualNameTests` and `StationTests` unchanged |
| 2 | **Persistence (Q1):** `Permissions.RoutesManage`/`RoutesRead`; `INetworkDbContext.Routes`; `NetworkConstraints`, `NetworkAuditSubjects.Route`; `RouteConfiguration`, `RouteStationConfiguration`, `YcrDbContext.Routes`; migration `Network_CreateRoutes`. **V4** | `RouteModel_*`, the extended UTC theory, `DownMigration_NetworkCreateRoutes_…`, `RouteConstraints_…`, `RouteStations_…` (migrator); all existing tests |
| 3 | **Seed:** migration `Identity_SeedRoutePermissionGrants`; update `IdentitySeedTests` (24 rows, docs/10 route section) and `ApplicationCredential_CannotWriteRolesOrGrants` (24) | The three `Seed_*` tests; the extended privilege test |
| 4 | **Grants:** migration `Security_NetworkRouteGrants`; the new `DatabasePrivilegeTests` cases and the two DDL rows; `RouteMigrationTests` upgrade and down tests | `ApplicationCredential_HasExactlyTheRouteGrants`, `…CanDeactivateARouteButNotRewriteIt`, `Migrate_FromF002Schema_…`, `Migrate_DownToF002_…` |
| 5 | **Application:** `RouteAuditSnapshot`; `CreateRoute`, `DeactivateRoute`, `GetRoute`, `ListRoutes` with DTOs and projections. **V1, V2, V3** | Every `YCR.Application.Tests` row above, including the parallel S21 and S23 tests and the S17 station case |
| 6 | **API:** `RouteContracts` (+ validator, P6; Q2 if (b)), `RouteEndpoints`, `Program.cs` mapping. **V5** | Every `YCR.Api.Tests` row above, including `DeployedShapeTests` (Unmodified) and `RoutePermissionGrantTests` (Real); all F-001/F-002 API suites |
| 7 | **`YCR.Api.http` and CI:** the Routes section; the `api-smoke` route checks. Push `feature/F-003` and **prove the GitHub Actions run green** (build, tests, `has-pending-model-changes`, `api-smoke`, trunk-only filter untouched, gitleaks). Record the run URL in `progress.md` | A green GitHub Actions run on `origin` for `feature/F-003` |

### Stage 8 documentation (not stage 4)

- **`docs/07`:** a new "F-003 route tables, constraints, indexes and grants" section, with both tables, every object in §DB changes, both FKs `NO ACTION`, the UTC checks declared in the model, **the index decision and why** (the named-query table), and the `Security_NetworkRouteGrants` grant table with its absences.
- **`docs/08`:** "Implemented in F-003 — routes" with the endpoint table, contracts and error codes as implemented. In "Initial resources", `GET/POST/PATCH /routes` is replaced with a note that `PATCH /routes` is **not provided** (OQ41 ruling) and that there is **no `PUT /routes/{id}/stations`** (OQ38 ruling). The OpenAPI/Scalar paragraph stays as it is.
- **`docs/10`:** no change expected, since §Route permission grants already matches the seed. Stage 8 confirms it and records the migration name there.
- **`docs/glossary.md`** (**Q3**): entries for *Route code* (`RouteCode`, OQ41 provisional), *IsClosed* (closed/open route; last → first connection from the flag, first station never repeated; OQ37), and *RouteStation / position* (1-based contiguous ordinal within one route; not the station index, not the station code; R18).
- **`docs/features/F-003-route-management/spec.md`:** only if Q1 is ruled (a), as a recorded stage-2 amendment to the index name (hein's approval).
- **README:** no change (it lists no endpoints).

---

## Stop point

⛔ **Plan needs hein's approval before implementation.** T-034 is `blocked` (`approval`). No code, tests or migrations exist for F-003. Stage 4 starts only after approval and after **Q1** is ruled (it blocks step 2). Q2 and Q3 can be ruled later, as marked.

---

## Review history

**Revision 1 — 2026-09-24, claude (T-034).** First draft.

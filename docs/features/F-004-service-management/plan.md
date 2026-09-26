# Plan: F-004 Service management — the `Service` aggregate, the first `Timetable` module slice and the first cross-module contract

Spec: `spec.md` — **Approved (hein, 2026-09-25)** at `c2ea4f0`; every ruling is in spec §0.9–§0.10.
Stage: 3 (PLAN), `docs/workflows/02-feature-development.md`. Task T-045.
Binding inputs: ADR-0004, ADR-0006, ADR-0012, ADR-0017, ADR-0018, ADR-0019, ADR-0021, ADR-0022, **ADR-0025 (Accepted)**; `docs/07`, `docs/08`, `docs/10` §Service permission grants; `docs/20-coding-conventions.md`; `docs/21-definition-of-done.md`; `docs/workflows/04-database-change.md`. Worked examples: the F-003 plan and `review-codex.md`, and the existing `Network` slice (routes and stations).

**Status: Approved (hein, 2026-09-25).** Revision 2. hein ruled on plan Q1–Q3 on 2026-09-25 (§Rulings on the plan questions; these are the plan's questions, distinct from the spec's §0.10 Q2 and Q3), and every row that depended on them follows the rulings. P1–P23 are approved as amended there. T-046 (stage 4) may proceed. **Amended during stage 4:** Amendments 1 and 2 (hein, 2026-09-25, T-046 G1 and G2; §Amendments during implementation).

---

## Understanding

F-004 creates the `Timetable` module and fills it with one aggregate. A **service** is master data: a code, a bilingual name, one route, a direction (`Forward`/`Reverse`), an ordered list of stops (no times), a set of days of the week and an effective period. It is created whole by `POST /services`, read by id or as a page, and changed only by **withdrawal**, which shortens its period. Nothing is ever deleted. The feature has six parts:

1. **Shared kernel.** The code format and `BilingualName` move from `YCR.Domain.Network` to `YCR.Domain.Common`, because a Timetable value object may not depend on the Network domain (ADR-0012 item 6; `DomainModuleMustNotDependOnOtherDomains`). Behaviour does not change (P2, ruling Q1).
2. **The Network contract (ADR-0025).** `YCR.Application.Network.Contracts.INetworkReader` returns primitive records; an `internal` class in `YCR.Application.Network` implements it; a new architecture rule keeps every Contracts namespace free of domain and context types.
3. **Domain.** `Service` owns its `ServiceStop` rows and decides every creation rule (R9–R15, R17, R19, R39) from facts the handler gives it, including the stop-order rule R12 with wrap and full-circuit closure. `Service.Withdraw` decides R21. The overlap rule R35 is a domain predicate over periods the handler loads.
4. **Persistence.** Schema `timetable`, two tables, two cross-schema `NO ACTION` foreign keys into `network`, least-privilege grants that make the rows insert-only except the two withdrawal columns, and the ten service permission grants.
5. **Serialisation.** Create and withdraw take an exclusive `sp_getapplock` named after the service code, inside a short explicit transaction, so one code's periods can never overlap (R35).
6. **API and tests.** Four endpoints under `/api/v1/services` with explicit body limits on real Kestrel; every live scenario (53, including S40a) mapped to named tests.

The risks are in the details F-003 did not have: a cross-module read, a lock that is not a unique index, "today" in Asia/Yangon for the first time, and a seed that changes counts other tests pin. The plan names each existing test that must change and why. None is weakened.

---

## Facts / Assumptions / Open questions

**FACT — `src/YCR.Domain/Network/NetworkCodeFormat.cs`:** the `^[A-Z0-9]{2,10}$` rule is `internal` to `YCR.Domain.Network`. **`src/YCR.Domain/Network/BilingualName.cs`:** `BilingualName` is in `YCR.Domain.Network`, and its two-argument `Create` returns `NetworkErrors.InvalidStationName`. `tests/YCR.ArchitectureTests/ArchitectureRules.cs` `DomainModuleMustNotDependOnOtherDomains` forbids `YCR.Domain.Timetable` from using either, and `CommonMustNotDependOnAnyModule` forbids a `YCR.Domain.Common` type from naming `NetworkErrors`. Hence P2 (ruling Q1).
**FACT — `ArchitectureRules.ApplicationModuleMayDependOnlyOnAllowedTypes`:** its "other application" pattern is `^YCR\.Application\.(?!<module>(\.|$)|Common(\.|$)|(<all modules>)\.Contracts(\.|$)).+` and its "other domain" pattern is `^YCR\.Domain\.(?!<module>(\.|$)|Common(\.|$)).+`. For `Timetable` this **admits** any type under `YCR.Application.Network.Contracts` and **rejects** `YCR.Domain.Network.*`, `YCR.Application.Network.INetworkDbContext` and every other `YCR.Application.Network.*` type, including the `internal NetworkReader`, which is otherwise reachable because it is in the same assembly. `Timetable` is already in `ArchitectureRules.Modules`, so `ApplicationAndDomainRules_ForEveryBoundedContext_HaveNoSourceViolations` starts checking real Timetable code with no change.
**FACT — `src/YCR.Infrastructure/Identity/SqlServerIdentityAdministratorLock.cs`, F-002 plan P14:** a named, exclusive, transaction-owned `sp_getapplock` behind an Application abstraction, inside an explicit transaction opened on the module context's `Database`, is an accepted pattern. `public` may execute `sp_getapplock`, so `ycr_app` needs no grant; `DatabasePrivilegeTests.ApplicationCredential_CanTakeTheAdministratorApplock` proves it. The one explicit transaction is described there as "the one documented exception to ADR-0004's single-save default"; F-004 is its second use (ruling Q2).
**FACT — ADR-0018 §Time:** "The configured local zone is Asia/Yangon and is never hard-coded in domain logic." No code configures a zone yet (`grep -rn "Yangon\|TimeZone" src` finds nothing). F-004 is the first feature that needs "today" in Asia/Yangon (R21, R38, R39). Hence P11 (ruling Q3).
**FACT — `src/YCR.Worker/Program.cs`:** the Worker has two start paths, the `bootstrap-administrator` command (which builds its own configuration from `appsettings.json`, `appsettings.{env}.json` and environment variables, without a host) and the bare host (`Host.CreateApplicationBuilder`). `YCR.Worker.csproj` uses `Microsoft.NET.Sdk.Worker`, which copies `appsettings.json` to the output. CI's `api-smoke` runs the built Worker DLL for the bootstrap; `BootstrapAdministratorCommandTests` runs it once as a real process and otherwise in process through `BootstrapAdministratorCli.RunAsync`, which does not go through `Program.Main`.
**FACT — time-zone data in the environments the tests run in (checked 2026-09-25):** CI's jobs run directly on the GitHub-hosted `ubuntu-latest` runner with `actions/setup-dotnet` (`.github/workflows/ci.yml`: `runs-on: ubuntu-latest`, no `container:`), **not** inside a .NET container image, so the zone data CI uses is the runner image's `/usr/share/zoneinfo`; the proof is `LocalCalendarTests.AsiaYangon_ResolvesInThisEnvironment` running green in CI (step 9). The .NET images were checked with `docker` on 2026-09-25: `mcr.microsoft.com/dotnet/runtime-deps:10.0` (Ubuntu 24.04.5 LTS, the base of the `runtime`, `aspnet` and `sdk` 10.0 images) has package `tzdata` installed (`Priority: required`) and `/usr/share/zoneinfo/Asia/Yangon`; `runtime-deps:10.0-noble-chiseled` has **no** `Asia/Yangon` file; `runtime-deps:10.0-noble-chiseled-extra` has it. So a future container image must not be the plain chiseled variant (§Stage 8, `docs/15`). The Windows dev machines resolve IANA ids through ICU.
**FACT — `src/YCR.Infrastructure/DependencyInjection.cs`:** `UseSqlServer` is called without `EnableRetryOnFailure`, so a user-initiated transaction needs no execution-strategy wrapper.
**FACT — `src/YCR.Application/DependencyInjection.cs`:** handlers are registered by scanning public `*Handler` types. `DependencyInjectionTests.HandlerTypes_IncludesEveryHandlerDefined` asserts **26**; F-004 adds four handlers, so **30**.
**FACT — the grant count 24 is pinned in six tests and two permission lists in four more** (full list in §Test plan, "Existing tests whose counts or lists change"). The service seed adds ten rows (docs/10 §Service permission grants), so 24 → 34.
**FACT — `docs/10` §Service permission grants** uses the same bullet format as the station and route sections (`- \`perm\` → … roles …`), which `IdentitySeedTests.ReadDocs10Grants` already parses by heading; only the heading list changes.
**FACT — `src/YCR.Api/Endpoints/Network/RouteEndpoints.cs`:** the F-003 body limit is `RequestSizeLimitAttribute` endpoint metadata (32 KiB), proved on real Kestrel by `RouteRequestLimitTests`; `TestServer` has no body-size feature.
**FACT — `README.md`** lists no endpoints (F-003 plan), so stage 8 does not change it.
**FACT — T-042** (limits for every other endpoint, and whether framework `413`/`400` bodies get an `errorCode`) is `todo`. F-004 follows F-003 exactly and does not decide T-042's question.

### Verifications done at PLAN

Run in a scratch console app outside the repository (`net10.0`, SDK 10.0.302, `Microsoft.EntityFrameworkCore.SqlServer` 10.0.12, the repository's pins), with stand-in `Station`, `Route`, `Service` and `ServiceStop` types configured as §DB changes describes. The model was inspected through `IDesignTimeModel` and `IMigrationsModelDiffer`, which is what `has-pending-model-changes` compares.

| # | Question | Result |
|---|---|---|
| O1 | Does `HasOne<Route>().WithMany().HasForeignKey(s => s.RouteId).OnDelete(NoAction)` in a `timetable` entity produce a cross-schema foreign key with no navigation? | **Yes.** `FK_Services_Routes_RouteId -> network.Routes(Id) onDelete=NoAction`; `Route` has no navigations. Likewise `FK_ServiceStops_Stations_StationId -> network.Stations(Id)`. The default names equal spec §7's names. |
| O2 | Which indexes does EF add by convention? | Exactly two, both wanted by spec §7: **`IX_Services_RouteId`** (FK on `RouteId`; nothing else leads with it) and **`IX_ServiceStops_StationId`** (FK on `StationId`). The `ServiceId` FK is covered by `PK_ServiceStops (ServiceId, Position)`, so no `IX_ServiceStops_ServiceId`. Plus the declared `IX_Services_Code_EffectiveFrom`. This is the F-003 IX lesson applied in advance: both convention indexes are declared explicitly with those names (P16), and a model test pins the exact index set. |
| O3 | With the Network model already in place, does adding the Timetable entities emit anything against `network`? | **No.** The diff is exactly `EnsureSchema timetable`, `CreateTable timetable.Services`, `CreateTable timetable.ServiceStops` and the three indexes. |
| O4 | Column types and the concurrency token | `DateOnly` → `date`, `DateOnly?` → nullable `date`; the enum with `HasConversion<string>().HasMaxLength(10)` → `nvarchar(10)`; owned operating days → `bit` columns in `Services`; `EffectiveTo.IsConcurrencyToken` is `true`. |
| O5 | Largest valid bodies | `CreateServiceRequest`: **9,280 bytes** worst case (§API changes, "Body limits"); `WithdrawServiceRequest`: **29 bytes**. |

### Verifications left to implementation, each with a step and a fallback

| # | VERIFY | Step | If it fails |
|---|---|---|---|
| V1 | After moving `BilingualName` to `YCR.Domain.Common`, `dotnet ef migrations has-pending-model-changes` is still clean (the snapshot names the owned type `YCR.Domain.Network.BilingualName` as a string; the differ compares the relational model, which does not change) | 1 | Do step 1 together with step 4, so the snapshot is regenerated by `Timetable_CreateServices`, and record it in `progress.md`. Never an empty migration just to rewrite the snapshot. |
| V2 | Withdrawal emits **one** `UPDATE [timetable].[Services] SET [EffectiveTo], [WithdrawnAtUtc] … WHERE [Id] = @id AND [EffectiveTo] = @orig` (or `IS NULL` when the original is null, as F-002 V4 found for `ReplacedByTokenId`) and **nothing** against `ServiceStops` | 7 | Fix the mapping (for example, do not load the stops for withdrawal and build the snapshot from a projection). **Never widen the grant** (AGENTS.md rule 8). The handler tests run as `ycr_app`, so a stray `UPDATE` fails a test. |
| V3 | `ListServices` counts with `COUNT`, pages with `OFFSET/FETCH`, computes `stopCount` as a SQL subquery, and orders by `Code, EffectiveFrom, Id` | 7 | `ListServicesSqlTests` asserts the SQL, as `ListRoutesSqlTests` does. |
| V4 | `has-pending-model-changes` is clean after each migration | 4–6 | Fix the model; never hand-edit a migration to match. |
| V5 | `sp_getapplock` called through `ExecuteSqlAsync` with the resource as a **parameter** returns `0`/`1` and blocks a second transaction on the same resource but not on another code | 7 | `ApplicationCredential_CanTakeTheServiceCodeApplock` fails; fix the call. Never interpolate the code into SQL text. |
| V6 | `TimeZoneInfo.FindSystemTimeZoneById("Asia/Yangon")` resolves on the Windows dev machines and on the `ubuntu-latest` runner (§Facts) | 7, 9 | `LocalCalendarTests.AsiaYangon_ResolvesInThisEnvironment` fails by name. If a host lacks ICU or tzdata, the API and the Worker refuse to start (P11), which is the intended fail-closed behaviour; stop and report, never fall back to a fixed offset. |
| V7 | What minimal APIs return for a malformed `routeId` query value on `GET /services?routeId=abc` (framework `400`, as for a malformed `page` today) | 8 | Record it in `progress.md`. It predates F-004 and is T-042's cross-cutting question (R-5). |
| V8 | The EF-generated `Down()` of `Timetable_CreateServices` drops both tables; `DropSchema("timetable")` is added by hand before the migration is ever applied (as `Network_CreateStations` does for `network`) | 4 | `DownMigration_TimetableCreateServices_DropsTablesAndTimetableSchema` fails until it does. |

**ASSUMPTION:** none. Every business rule used is a spec rule (R1–R42) resting on hein's rulings in spec §0.9–§0.10.

**OPEN QUESTION:** none new in `docs/19`. Plan Q1–Q3 were engineering questions to hein, not Myanma Railways questions; all three are ruled (below).

---

## Rulings on the plan questions (hein, 2026-09-25)

Revision 1 asked three questions (Q1–Q3). hein ruled on all three on 2026-09-25; each is an **ENGINEERING DECISION (tech lead, hein, 2026-09-25; T-045)**, also recorded in the T-045 row of `TASKS.md`. The rows below and every plan row that depended on them follow the rulings.

| # | Question asked | Ruling | Where applied |
|---|---|---|---|
| **Q1** | Where the shared code format and `BilingualName` live, since the architecture rules forbid `YCR.Domain.Timetable` from using `YCR.Domain.Network` types | **Move both to `YCR.Domain.Common`, as recommended (option (a)); callers pass their module's error.** `CodeFormat` (`internal static`, the renamed `NetworkCodeFormat`, regex unchanged) and `BilingualName` with only `Create(en, my, Error whenInvalid)`. `CreateStationHandler` passes `NetworkErrors.InvalidStationName`, `CreateRouteHandler` keeps passing `NetworkErrors.InvalidRouteName`, `CreateServiceHandler` passes `TimetableErrors.InvalidServiceName`. **Station and route behaviour stay unchanged, and every existing F-001/F-003 test keeps its assertion** (call sites only add the explicit station error). **The one rewritten test**, `BilingualName_Create_WithoutErrorArgument_StillReturnsInvalidStationName` → `BilingualName_Create_WithStationError_WhenInvalid_ReturnsInvalidStationName`, **asserts exactly what it asserted before**: for `("   ", "Yangon Myanmar")`, `Assert.True(result.IsFailure)` and `Assert.Equal(NetworkErrors.InvalidStationName, result.Error)`; only the call gains the explicit error argument, because the two-argument overload no longer exists. The change to the Network module and to `YCR.Domain.Common` is covered by the **existing** architecture tests, unchanged: `CommonKernel_WithModuleDependency_DetectsViolation` (evaluates `CommonMustNotDependOnAnyModule` on the source, so a `BilingualName` in Common that still named `NetworkErrors` fails it), `ApplicationAndDomainRules_ForEveryBoundedContext_HaveNoSourceViolations` (`DomainModuleMustNotDependOnOtherDomains` and `ApplicationModuleMayDependOnlyOnAllowedTypes` for every module, Network and Timetable included), `DomainModuleBoundary_WithForeignDomainFixture_DetectsViolation`, `DomainLayerRules_WithForbiddenDependencies_DetectViolations`, and `ApiRules_WithForbiddenTypes_DetectViolations` (`ApiMayUseOnlyAllowlistedCommonTypes`: the Api still may not use `BilingualName`, now a Common type). | P2; §Affected files; §Test plan (Domain, changed items 15–16, Architecture); step 1 |
| **Q2** | An explicit transaction for the code lock, although ADR-0004 reserves explicit transactions for handlers with several saves | **Accepted: `sp_getapplock` inside an explicit transaction, as the second use of the F-002 P14 exception.** Stage 8 adds (1) a `docs/20` rule stating when a handler may open its own transaction: to guard a **set invariant across rows that one unique index cannot enforce**, with an **application lock scoped to the smallest key** (here one service code), one save, then commit; and (2) **ADR-0026 "Application locks for set invariants"**, drafted as **Proposed** at stage 8, citing ADR-0004, F-002 plan P14 and F-004 spec R35. hein accepts it at stage 9. | P9, P10; §R35; §Stage 8 documentation |
| **Q3** | How "today in Asia/Yangon" is configured | **Accepted: `Time:LocalTimeZone` = `"Asia/Yangon"` (the IANA id) in `appsettings.json`, and `ILocalCalendar.Today()` over `TimeProvider`.** **Both `YCR.Api` and `YCR.Worker` validate the zone at startup and refuse to start if it is missing or cannot be resolved.** Tests: the zone resolves in the environment the tests run in (CI is Linux; §Facts records what CI runs on and what the .NET images carry); startup fails with a clear error for a missing or unknown zone, for the Api and for the Worker; `Today()` crosses local midnight correctly — `17:29:59Z` → one date, `17:30:00Z` → the next — because Asia/Yangon is UTC+06:30 with no DST. | P11; §Affected files (Api, Worker, Infrastructure); §Test plan (Infrastructure, Api, Integration); R-4; steps 7–9; §Stage 8 documentation |

## Amendments during implementation

Two gaps found at the end of step 8 (T-046, CI run [36114002714](https://github.com/heinmindev/ycr/actions/runs/36114002714)) were ruled by hein on 2026-09-25 as **ENGINEERING DECISIONS (tech lead)**, also recorded in the T-046 row of `TASKS.md`. They change the rows named below; nothing else.

| # | Gap | Amendment | Where applied |
|---|---|---|---|
| **Amendment 1** (hein, 2026-09-25, T-046 G1) | The zone test as written ("no adjustment rules, i.e. no DST") cannot pass on Linux: Ubuntu tzdata gives Asia/Yangon nine historical pre-1946 rules (all with `DaylightDelta` 0), so `GetAdjustmentRules()` is not empty and `SupportsDaylightSavingTime` is true, although the offset is +06:30 with no DST over the service horizon. | **The zone test asserts behaviour, not `SupportsDaylightSavingTime`.** `LocalCalendarTests.AsiaYangon_ResolvesInThisEnvironment` asserts: (1) `Asia/Yangon` resolves from `Time:LocalTimeZone` (bound into `LocalTimeOptions` from configuration, then `LocalTimeOptions.ResolveZone`); (2) `GetUtcOffset` is exactly +06:30 at 1 Jan 00:00 and 1 Jul 00:00 local of every year 2026–2040, and at both sides of each of those local midnights (the instants one second before and at local midnight); (3) no adjustment rule whose `DateStart..DateEnd` overlaps 2026–2040 has a non-zero `DaylightDelta`. It does **not** assert `SupportsDaylightSavingTime`; the historical pre-1946 rules on Linux are allowed. The midnight-crossing tests are unchanged. It must pass on Windows locally and on `ubuntu-latest` in CI. | §Test plan (`YCR.Infrastructure.Tests`, `LocalCalendarTests`); step 7 |
| **Amendment 2** (hein, 2026-09-25, T-046 G2) | `api-smoke` started the API from the repository root, so its content root was the root and the shipped `src/YCR.Api/appsettings.json` was never read; the step-8 fail-closed zone check then refused to start ("Time:LocalTimeZone is not configured"). | **Option (b): the `api-smoke` job starts the API from its build output directory** (`working-directory: src/YCR.Api/bin/Release/net10.0`), so the shipped `appsettings.json`, with `Time:LocalTimeZone`, is what it reads. **No `Time__LocalTimeZone` (or any other override that hides a missing file value) in the job's environment.** The check that fails if the file is not read is the API's own fail-closed startup: without the file, "Run the API" fails (proved by starting it from the repository root, §progress.md). The smoke step also asserts that the API log names the output directory as its content root and that the API process has no `Time__` environment variable. The file's `Auth` values equal the `AuthOptions` code defaults the job relied on before, so reading the file changes no auth behaviour. **Stage 8** adds to `docs/15` and `docs/local-development.md` that the API and the Worker run with their output directory as content root. | §OpenAPI, `.http`, smoke; step 9; §Stage 8 documentation |

---

## Decisions (P1–P23) — made by this plan, approved (hein, 2026-09-25)

ENGINEERING DECISION (plan, T-045) unless stated. Each cites the rule or precedent it implements.

| # | Decision | Why | Rejected alternative |
|---|---|---|---|
| **P1** | **`ServiceStop` is a child entity of `Service`**, a plain `sealed class` (`ServiceId`, `Position`, `StationId`) with an internal factory, exposed as `IReadOnlyList<ServiceStop> Stops` over a private list; configured with `HasMany(...).WithOne().HasForeignKey(ServiceId).OnDelete(NoAction)`, field access. **`ITimetableDbContext`** exposes `DbSet<Service> Services`, `DatabaseFacade Database` (for P10 only, as `IIdentityDbContext` does) and `SaveChangesAsync`. No `DbSet<ServiceStop>`. | F-003 P1 (`Route`/`RouteStation`), spec E2. | `OwnsMany` |
| **P2** | **Shared kernel (ruling Q1).** `YCR.Domain.Common.CodeFormat` (moved from `NetworkCodeFormat`; `StationCode`, `RouteCode` and `ServiceCode` call it) and `YCR.Domain.Common.BilingualName` (moved; only the error-taking factory; every caller passes its own module's error). Station and route behaviour and every existing test assertion are unchanged; the one rewritten test asserts exactly what it did (§Rulings Q1). XML comments keep both rulings' standing (OQ26 final, OQ41 and OQ43 provisional). | ADR-0012 item 6 and the domain-boundary rule; F-003 P2 "one rule, one home". A shared kernel is what `YCR.Domain.Common` is for; the code format and a bilingual text are not Network concepts. | A Timetable-only `ServiceName` value object (two name types for one rule); keeping the station overload as a C# 14 extension member in `YCR.Domain.Network` |
| **P3** | **`ServiceCode`** (`YCR.Domain.Timetable`): `Create` trims, calls `CodeFormat.IsValid`, returns `Timetable.InvalidServiceCode`; `internal From` for EF (`AssemblyInfo` already grants `YCR.Infrastructure`). Not unique (R35). | R6; the `RouteCode` shape. | Reusing `RouteCode` (a service code is not a route code) |
| **P4** | **Every creation rule lives in the domain, given facts.** `EffectivePeriod.ForNewService(from, to, today)` decides R19/R39. `Service.Create(id, code, name, route: ServiceRouteFacts, direction, stopStationIds, operatingDays, period, nowUtc)` decides R15 (route active, stop station active), R11, R14, R13, R12 in the R37 order, and assigns positions `1..k` in request order. `ServiceRouteFacts(RouteId, IsActive, IsClosed, Stations: [ServiceRouteStationFacts(StationId, IsActive)] in route position order)` is a **Timetable domain** record the handler fills from the contract, so the domain never sees a Network or Contracts type. | AGENTS.md rule 3; F-003 P4. Every pattern rule is testable without a database. | Checks in the handler; the domain taking `RouteReference` (Domain → Application dependency) |
| **P5** | **`CreateServiceHandler` order (R37):** validation filter (`400 Common.ValidationFailed`) → `ServiceCode` → `BilingualName(…, InvalidServiceName)` → `EffectivePeriod.ForNewService` (`400` then `422`) → `INetworkReader.GetRouteAsync` (`null` → `422 Timetable.ServiceRouteNotFound`) → `Service.Create` (the other `422`s) → **begin transaction → lock the code → load the code's periods → overlap (`409`)** → add + audit → one `SaveChangesAsync` → commit. | R37 exactly. The Network read happens before the transaction, so the lock is held only for the overlap read and the insert. The create-vs-deactivation race is accepted (spec §5 and §0.10 Q3 ruling). | Taking the lock first (holds it across the Network read for no gain) |
| **P6** | **Request fields that are not plain strings travel as strings** — `routeId`, `direction`, `stopStationIds`, `operatingDays`, `effectiveFrom`, `effectiveTo`, `withdrawFrom` — and the validators parse them: GUIDs in `D` format, dates exactly `yyyy-MM-dd` (invariant culture), `direction` exactly `Forward` or `Reverse`, day names exactly `Monday`…`Sunday` (ordinal; `"monday"`, `"1"` and `"Mon"` are unknown). `ToCommand()` converts after validation. The command carries `Guid`, `DateOnly`, `DayOfWeek` and the direction name as a `string` (the Api may not reference `YCR.Domain.Timetable`). | S21 requires every malformed value to answer **`400 Common.ValidationFailed`**; typed properties would fail JSON binding first, without that `errorCode` (F-003 P6, V5). `Enum.TryParse<DayOfWeek>` would accept `"1"` and ignore case, so it is not used. | Typed DTO properties with a global binding-error customisation (out of scope, AGENTS.md rule 11) |
| **P7** | **Operating days** are an owned value object `OperatingDays` (`OwnsOne`, table splitting) with seven `bool` properties mapped to `RunsOnMonday`…`RunsOnSunday`. `OperatingDays.Create(IReadOnlyCollection<DayOfWeek>)` **throws** on an empty or repeated collection: the validator refuses those first with `400 Common.ValidationFailed` (R17), the domain states the invariant, and `CK_Services_OperatingDays` binds every other writer. `Days` lists them Monday first (R40). | R17, R40; the `BilingualName` mapping style. A thrown exception, not a `Result`, because the spec gives no `Timetable.*` code for it and the validator makes it unreachable from the API. | Seven properties on `Service`; a bit-mask column (spec §7 fixes seven `bit` columns) |
| **P8** | **`Direction`** is an enum `{ Forward, Reverse }` in `YCR.Domain.Timetable`, stored with `HasConversion<string>().HasMaxLength(10)` (O4) and guarded by `CK_Services_Direction`. | R9; spec §7 stores text. | An int column |
| **P9** | **R35 lock: `sp_getapplock` on `timetable.ServiceCode:<CODE>`**, exclusive, transaction-owned, 30 s timeout, through `IServiceCodeLock` (§R35 below). | F-002 P14 precedent; exact per-code scope; no grant. | `UPDLOCK, HOLDLOCK` range read (§R35) |
| **P10** | **One explicit transaction per create and per withdrawal** (ruling Q2: the second use of the F-002 P14 exception to ADR-0004's single-save default), containing the lock, the reads that decide, the one `SaveChangesAsync` and the commit. Default isolation (`READ COMMITTED`); the context never sets `SNAPSHOT`. The general rule (a set invariant across rows that one unique index cannot enforce, guarded by an application lock scoped to the smallest key) goes into `docs/20` and ADR-0026 (Proposed) at stage 8. | The lock needs an owner. | `@LockOwner = 'Session'` with an explicit release (a lock that can outlive a failed request) |
| **P11** | **`ILocalCalendar.Today()`** (ruling Q3): `TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone)` → `DateOnly`, zone from `Time:LocalTimeZone` = `"Asia/Yangon"` (IANA id) in the `appsettings.json` of **both** `YCR.Api` and `YCR.Worker`. One resolver, `LocalTimeOptions.ResolveZone(string? id)` in `YCR.Infrastructure/Time`, throws `InvalidOperationException` with a clear message ("`Time:LocalTimeZone` is not configured" / "`Time:LocalTimeZone` '<id>' is not a time zone this host can resolve; install IANA time-zone data") for a missing or unknown id. **Api:** `LocalTimeOptions` bound from `Time`, validated with that resolver, `ValidateOnStart()`, so the host fails before serving. **Worker:** `Program.Main` resolves the zone from its configuration **before either start path** (the `bootstrap-administrator` command and the bare host) and, on failure, writes the message to stderr and exits `1` without doing anything else. Handlers pass `today` into the domain. | ADR-0018 §Time; R38. `TimeProvider` stays the only clock, so tests control "today" through `TestClock`. Asia/Yangon is UTC+06:30 with no DST, so local midnight is 17:30:00Z. | Hard-coding `+06:30` (violates ADR-0018); reading the server's local time |
| **P12** | **Withdrawal.** `Service.Withdraw(DateOnly withdrawFrom, DateOnly today, DateTimeOffset nowUtc)`: non-UTC `nowUtc` throws; `withdrawFrom < today` → `WithdrawalDateInPast`; `newEnd = withdrawFrom − 1 day`; unless `newEnd < (EffectiveTo ?? unbounded)` → `WithdrawalDoesNotShorten`; else sets `EffectiveTo = newEnd` and `WithdrawnAtUtc = nowUtc`. A refused call changes nothing. `WithdrawServiceHandler`: read the service's code by id (`AsNoTracking`; unknown → `404`) → begin transaction → lock the code → **reload the service tracked, with its stops** → `Withdraw` → audit (before/after) → one save → commit. `EffectiveTo` is the only concurrency token (R36); `DbUpdateConcurrencyException` → `409 Timetable.ServiceChangedConcurrently`, nothing written. | R21, R36, R41. The code never changes (R20), so reading it without the lock is safe; everything that decides is read after the lock. | Deciding on the unlocked read (would let two withdrawals both pass) |
| **P13** | **The overlap rule is a domain predicate.** `EffectivePeriod.Overlaps(DateOnly otherFrom, DateOnly? otherTo)`: `false` when the other period is empty (`otherTo < otherFrom`, R42); otherwise inclusive, with `null` as unbounded. Under the lock, the handler loads `(EffectiveFrom, EffectiveTo)` of every service with the code (`IX_Services_Code_EffectiveFrom` seek) and refuses with `409 Timetable.ServiceCodePeriodOverlap(code)` if any overlaps. | AGENTS.md rule 3: the rule is tested without a database; the handler only loads facts. A code has a handful of rows over years, so loading them is cheap. | A SQL `EXISTS` with the rule written in LINQ (a second home for the rule) |
| **P14** | **The Network contract** (§The Network contract below): `INetworkReader` with three read methods and four `sealed record`s; `internal sealed class NetworkReader` in `YCR.Application.Network`; `AddApplication` registers `services.AddScoped<INetworkReader, NetworkReader>()`. | ADR-0025 items 1–3, 7. | Returning Network domain types (ADR-0025 option A1) |
| **P15** | **Contracts architecture rule** `ContractsMustNotDependOnModuleDomainOrContext`, with violating fixtures, plus Timetable boundary fixtures (§The Network contract). | ADR-0025 item 4, REQUIRED CONTROL; spec S50. | — |
| **P16** | **Indexes declared explicitly**: `IX_Services_Code_EffectiveFrom` (not unique), `IX_Services_RouteId`, `IX_ServiceStops_StationId`, all with `HasDatabaseName`, even though EF would add the last two by convention (O2). `TimetableModelTests` pins the exact index set per table. | Spec §7; the F-003 IX lesson: every index is deliberate and named, none appears by accident. | Relying on convention names |
| **P17** | **Reads project and never materialise the aggregate.** `GetServiceHandler`: the service row and its stops (`AsNoTracking`, ordered by `Position`), then `INetworkReader.GetRouteSummariesAsync([routeId])` and `GetStationsAsync(stop ids)` for current code, names and active flags (R30). `ListServicesHandler`: optional `routeId` filter, `ORDER BY Code, EffectiveFrom, Id`, SQL `COUNT`, `OFFSET/FETCH`, `stopCount` subquery, then one `GetRouteSummariesAsync` for the page's route ids. `neverRuns` is computed when mapping (`EffectiveTo < EffectiveFrom`). DTOs (`ServiceDto`, `ServiceRouteDto`, `ServiceStopDto`, `ServiceSummaryDto`) are separate from API responses; direction and days are strings in DTOs. | F-003 P11, R30. `Id` is the final tie-break so paging is stable when two services share a code and an `EffectiveFrom` (possible when one never runs, R42); it is also the clustering key, so the index already orders by it. | Materialising `Service` for reads |
| **P18** | **Audit.** `TimetableAuditActions.ServiceCreated = "Timetable.ServiceCreated"`, `ServiceWithdrawn = "Timetable.ServiceWithdrawn"`; `TimetableAuditSubjects.Service = "Timetable.Service"`; `ServiceAuditSnapshot` (§Audit). Route and station codes come from the contract. | Spec §8; ADR-0021. A constants class, as Identity has, because the module is new (F-003 P9 declined only a refactor of existing literals). | Literal strings in handlers |
| **P19** | **Three migrations, kept separate:** `Timetable_CreateServices` → `Identity_SeedServicePermissionGrants` → `Security_TimetableGrants`. | F-002/F-003 P8: separate review concerns and separate `Down()`s. Names from spec §7 and `docs/20` §6. | One migration |
| **P20** | **Body limits:** `POST /services` **32 KiB**; `POST /services/{id}/withdraw` **1 KiB**; `RequestSizeLimitAttribute` endpoint metadata, tested on real Kestrel (§API changes). | R31; F-003 R27 mechanism. | One value for both (1 KiB is 35 times the withdrawal body; 32 KiB would be 1,100 times) |
| **P21** | **No domain event.** `docs/04` lists no service event, nothing would consume one, and F-004 dispatches nothing. | YAGNI; F-001 P6 events are collected, never dispatched. | `ServiceWithdrawn` event |
| **P22** | **`TimetableErrors`** holds all 17 codes (§API changes). `Timetable.InvalidPageRequest` reuses `Paging`. No `TimetableConstraints` class: no handler matches a constraint name, because there is no unique index in `timetable` (R35). | `docs/20` §2; F-003 pattern. | — |
| **P23** | **No new packages.** | Everything used is already referenced. | — |

---

## The Network contract (ADR-0025)

### Interface and records — `src/YCR.Application/Network/Contracts/`

```csharp
namespace YCR.Application.Network.Contracts;

/// Read-only (ADR-0025 item 3). Every value is the row's current value. Runs on the caller's
/// scoped YcrDbContext, so it shares the caller's connection and any open transaction (item 2).
public interface INetworkReader
{
    /// The route with its stations in position order 1..n; null when no route has this id.
    Task<RouteReference?> GetRouteAsync(Guid routeId, CancellationToken cancellationToken);

    /// Header data for each existing route id; unknown ids are absent from the result.
    Task<IReadOnlyDictionary<Guid, RouteSummaryReference>> GetRouteSummariesAsync(
        IReadOnlyCollection<Guid> routeIds, CancellationToken cancellationToken);

    /// Each existing station id; unknown ids are absent from the result.
    Task<IReadOnlyDictionary<Guid, StationReference>> GetStationsAsync(
        IReadOnlyCollection<Guid> stationIds, CancellationToken cancellationToken);
}

public sealed record RouteReference(
    Guid Id, string Code, string NameEn, string NameMy, bool IsClosed, bool IsActive,
    IReadOnlyList<RouteStationReference> Stations);

public sealed record RouteStationReference(
    int Position, Guid StationId, string StationCode, string StationNameEn, string StationNameMy,
    bool StationIsActive);

public sealed record RouteSummaryReference(
    Guid Id, string Code, string NameEn, string NameMy, bool IsClosed, bool IsActive);

public sealed record StationReference(
    Guid Id, string Code, string NameEn, string NameMy, bool IsActive);
```

Used by: `CreateServiceHandler` (`GetRouteAsync`), `GetServiceHandler` and `WithdrawServiceHandler` (`GetRouteSummariesAsync` for the route code, `GetStationsAsync` for stop codes and flags), `ListServicesHandler` (`GetRouteSummariesAsync`). No write method (item 3).

### Implementation — `src/YCR.Application/Network/NetworkReader.cs`

`internal sealed class NetworkReader(INetworkDbContext db) : INetworkReader`, namespace `YCR.Application.Network` (outside `Contracts`, item 2). Every query is `AsNoTracking` and projects scalars (the `StationProjection` rule: whole value objects, never `Code.Value`, inside the expression). `GetRouteAsync` reads the route header, then `Routes → SelectMany(Stations) → Join(Stations)` ordered by `Position`, the same two statements as `GetRouteHandler` (F-003 V2). The id-list methods use `ids.Contains(x.Id)`, one JSON parameter (F-003 O5). Mapping to the records happens after materialisation.

### DI registration

In `YCR.Application.DependencyInjection.AddApplication`: `services.AddScoped<INetworkReader, NetworkReader>();` (scoped, because it uses the scoped context). The class does not end in `Handler`, so the handler scan does not see it. `DependencyInjectionTests` gains `AddApplication_RegistersNetworkReaderAsScoped`.

### The new Contracts rule — `ArchitectureRules.ContractsMustNotDependOnModuleDomainOrContext`

```csharp
Types().That().ResideInNamespaceMatching(@"^YCR\.Application\.[^.]+\.Contracts(?:\..*)?$")
    .Should().NotDependOnAny(Types().That()
        .HaveFullNameMatching(@"^YCR\.Domain\.(?!Common(?:\.|$)).+")              // any module domain
        .Or().HaveFullNameMatching(@"^YCR\.Application\.[^.]+\.I[^.]*DbContext$")  // any module context
        .Or().ResideInNamespaceMatching(@"^Microsoft\.EntityFrameworkCore(?:\..*)?$")) // no DbSet/IQueryable leak
    .WithoutRequiringPositiveResults();
```

The EF Core clause is the strict reading of ADR-0025 item 1 ("built from primitives, `Guid`, `DateOnly`, `DateTimeOffset`, strings and other Contracts records only"): an `IQueryable<Station>` would leak the domain as surely as a `Station`.

**Violating fixtures** — `tests/YCR.ArchitectureTests/Violations/ContractViolations.cs`, namespace `YCR.Application.Network.Contracts.Violations`: `ContractExposingNetworkDomain` (a property of type `YCR.Domain.Network.Route`), `ContractExposingNetworkContext` (a property of type `INetworkDbContext`), `ContractExposingEntityFramework` (a `DbSet<YCR.Domain.Network.Station>`).

**Timetable boundary fixtures** — `tests/YCR.ArchitectureTests/Violations/TimetableBoundaryViolations.cs`, namespace `YCR.Application.Timetable.Violations`: `TimetableUsingNetworkDomain` (`YCR.Domain.Network.Route`), `TimetableUsingNetworkContext` (`INetworkDbContext`), and the **permitted** `TimetableUsingNetworkContracts` (`INetworkReader`, `RouteReference`).

**Confirmation for the existing rule** (§Facts): `ApplicationModuleMayDependOnlyOnAllowedTypes("Timetable")` admits `YCR.Application.Network.Contracts.*` and rejects `YCR.Domain.Network.*` and `YCR.Application.Network.INetworkDbContext`. Tests prove all three directions: `TimetableApplication_DependingOnNetworkDomainOrContext_IsDetected` (both fixtures appear among the failures) and `TimetableApplication_DependingOnNetworkContracts_IsAllowed` (the rule fails on the violations architecture, but `TimetableUsingNetworkContracts` is **not** among the failures). Adding the new fixtures does not disturb existing tests: `AssertRuleProtectsFixture` only asserts that the named fixture appears among a rule's failures.

---

## Affected modules and files

Modules touched: **`Timetable`** (new), **`Network`** (the contract and its implementation; the shared-kernel move), **`Identity`** (a data-only seed migration), solution-wide `Common` (two permission constants, `ILocalCalendar`, the moved kernel types) and both hosts' startup configuration (`Time:LocalTimeZone` in the Api and the Worker, ruling Q3). `Audit` gets new rows, not new code. The only cross-module dependency added is Timetable → `YCR.Application.Network.Contracts`; Network never calls Timetable (ADR-0025 Follow-up, OQ46 ruling).

### `src/YCR.Domain/`

| File | New / Changed | Why |
|---|---|---|
| `Common/CodeFormat.cs` | New (moved) | `NetworkCodeFormat` renamed and moved, regex unchanged (P2). XML comment lists stations (OQ26), routes (OQ41) and services (OQ43) |
| `Common/BilingualName.cs` | New (moved) | From `Network/`; only `Create(en, my, whenInvalid)` (P2, ruling Q1; callers pass their module's error). Comment adds service names (OQ43) |
| `Network/NetworkCodeFormat.cs`, `Network/BilingualName.cs` | Deleted (moved) | P2 |
| `Network/StationCode.cs`, `Network/RouteCode.cs` | Changed | Call `CodeFormat.IsValid`; behaviour unchanged (existing `StationCodeTests`, `RouteCodeTests`) |
| `Network/Station.cs`, `Network/Route.cs` | Changed | `using YCR.Domain.Common` only |
| `Timetable/Service.cs` | New | Aggregate: `Create` (P4), `Withdraw` (P12), `NeverRuns`; no other mutator (R20) |
| `Timetable/ServiceStop.cs` | New | `ServiceId`, `Position`, `StationId`; internal factory; no navigation (R8, R29) |
| `Timetable/ServiceCode.cs` | New | P3 |
| `Timetable/Direction.cs` | New | P8 |
| `Timetable/OperatingDays.cs` | New | P7 |
| `Timetable/EffectivePeriod.cs` | New | `ForNewService` (R19, R39), `Overlaps` (R35, R42) (P13) |
| `Timetable/ServiceRouteFacts.cs` | New | `ServiceRouteFacts`, `ServiceRouteStationFacts` (P4) |
| `Timetable/TimetableErrors.cs` | New | The 17 codes (§API changes) |
| `Properties/AssemblyInfo.cs` | Unchanged | Already grants `YCR.Infrastructure` internals (`ServiceCode.From`) |

### `src/YCR.Application/`

| File | New / Changed | Why |
|---|---|---|
| `Common/Authorization/Permissions.cs` | Changed | `ServicesManage = "services.manage"`, `ServicesRead = "services.read"`, with the OQ49 provisional-ruling comment |
| `Common/Abstractions/ILocalCalendar.cs` | New | `DateOnly Today()` (P11, ruling Q3) |
| `Network/Contracts/INetworkReader.cs`, `Network/Contracts/NetworkReferences.cs` | New | P14 |
| `Network/NetworkReader.cs` | New | `internal` implementation (P14) |
| `Network/CreateStation/CreateStationHandler.cs` | Changed | Passes `NetworkErrors.InvalidStationName` explicitly (P2) |
| `Network/*` other files | Changed only if the compiler asks | `using YCR.Domain.Common` for `BilingualName`, where not already present |
| `Timetable/ITimetableDbContext.cs` | New | P1 |
| `Timetable/Abstractions/IServiceCodeLock.cs` | New | `Task AcquireAsync(ServiceCode code, CancellationToken)`; throws `InvalidOperationException` with no open transaction (P9) |
| `Timetable/TimetableAuditSubjects.cs`, `Timetable/TimetableAuditActions.cs`, `Timetable/ServiceAuditSnapshot.cs` | New | P18 |
| `Timetable/CreateService/CreateServiceCommand.cs`, `CreateServiceHandler.cs` | New | P5 |
| `Timetable/WithdrawService/WithdrawServiceCommand.cs`, `WithdrawServiceHandler.cs` | New | P12 |
| `Timetable/GetService/GetServiceQuery.cs`, `GetServiceHandler.cs`, `ServiceDto.cs` (+ `ServiceRouteDto`, `ServiceStopDto`), `ServiceProjection.cs` | New | P17 |
| `Timetable/ListServices/ListServicesQuery.cs`, `ListServicesHandler.cs`, `ServiceSummaryDto.cs` | New | P17 |
| `Timetable/ServiceReadMapping.cs` | New | Direction and operating-day names (Monday first) and `neverRuns` for both read DTOs, in one place |
| `DependencyInjection.cs` | Changed | Registers `INetworkReader` (P14). Handlers by scanning, as today |

### `src/YCR.Infrastructure/`

| File | New / Changed | Why |
|---|---|---|
| `Persistence/Configurations/Timetable/ServiceConfiguration.cs` | New | Table, columns, the four check constraints and two UTC checks in the model, `IX_Services_Code_EffectiveFrom`, `IX_Services_RouteId`, `EffectiveTo` concurrency token, owned `BilingualName` and `OperatingDays`, `HasMany` stops, `HasOne<Route>().WithMany()` `NO ACTION` (O1) |
| `Persistence/Configurations/Timetable/ServiceStopConfiguration.cs` | New | Composite PK, `CK_ServiceStops_Position`, `IX_ServiceStops_StationId`, `HasOne<Station>().WithMany()` `NO ACTION` |
| `Persistence/YcrDbContext.cs` | Changed | Implements `ITimetableDbContext`; `DbSet<Service> Services` |
| `Persistence/Migrations/<ts>_Timetable_CreateServices.cs` (+ Designer) | New | §DB changes |
| `Persistence/Migrations/<ts>_Identity_SeedServicePermissionGrants.cs` (+ Designer) | New | ″ |
| `Persistence/Migrations/<ts>_Security_TimetableGrants.cs` (+ Designer) | New | ″ |
| `Persistence/Migrations/YcrDbContextModelSnapshot.cs` | Changed | Generated |
| `Timetable/SqlServerServiceCodeLock.cs` | New | P9 |
| `Time/LocalTimeOptions.cs` (+ `ResolveZone`), `Time/LocalCalendar.cs` | New | P11 (ruling Q3): the one zone resolver both hosts use, and `ILocalCalendar` |
| `DependencyInjection.cs` | Changed | `ITimetableDbContext` → the scoped `YcrDbContext`; `IServiceCodeLock` → `SqlServerServiceCodeLock` (scoped); `ILocalCalendar` → `LocalCalendar` (singleton) |

### `src/YCR.Api/`

| File | New / Changed | Why |
|---|---|---|
| `Contracts/Timetable/ServiceContracts.cs` | New | `CreateServiceRequest` (+ validator, P6, `MaxStopStationIds = 200`), `CreateServiceResponse`, `WithdrawServiceRequest` (+ validator), `ServiceResponse`, `ServiceRouteResponse`, `ServiceStopResponse`, `ServiceSummaryResponse`; reuses `PagedResponse<T>` |
| `Endpoints/Timetable/ServiceEndpoints.cs` | New | `MapServiceEndpoints`, tag `Services`, the two body-limit constants (P20) |
| `Program.cs` | Changed | `api.MapServiceEndpoints();`; `LocalTimeOptions` bound from `Time`, validated with `LocalTimeOptions.ResolveZone`, `ValidateOnStart()` (ruling Q3) |
| `appsettings.json` | Changed | `"Time": { "LocalTimeZone": "Asia/Yangon" }` (ruling Q3; the IANA id) |
| `YCR.Api.http` | Changed | "Services (F-004)" section |

### `src/YCR.Worker/` (ruling Q3)

| File | New / Changed | Why |
|---|---|---|
| `Program.cs` | Changed | Resolves `Time:LocalTimeZone` through `LocalTimeOptions.ResolveZone` before either start path (the `bootstrap-administrator` command and the bare host); on failure writes the clear message to stderr and exits `1` (P11) |
| `appsettings.json` | Changed | `"Time": { "LocalTimeZone": "Asia/Yangon" }` — the Worker resolves no Timetable handler today, but it refuses to start without a resolvable zone (ruling Q3) |

### Other

| File | New / Changed | Why |
|---|---|---|
| `.github/workflows/ci.yml` | Changed | `api-smoke` service checks (§API changes) |
| Tests | New / Changed | §Test plan |

**Not changed:** `StationEndpoints`, `RouteEndpoints`, `DeactivateStationHandler`, `DeactivateRouteHandler`, every Network table, column, index and grant (ADR-0025 item 6; R16). The Worker's bootstrap command and its composition (`AddBootstrapAdministrator`) are unchanged; only its startup zone check is new.

---

## Domain changes

### `Service` (aggregate root, `YCR.Domain.Timetable`)

State: `Id`, `Code` (`ServiceCode`), `Name` (`BilingualName`), `RouteId`, `Direction`, `OperatingDays`, `EffectiveFrom` (`DateOnly`), `EffectiveTo` (`DateOnly?`), `CreatedAtUtc`, `WithdrawnAtUtc?`, `Stops` (`IReadOnlyList<ServiceStop>`, position order). Derived: `NeverRuns => EffectiveTo < EffectiveFrom` (R41).

| Method | Rule | Error |
|---|---|---|
| `EffectivePeriod.ForNewService(from, to, today)` | `to < from` → **R19**; then `to < today` → **R39**. `from` may be in the past (OQ50). | `InvalidEffectivePeriod` (Validation); `ServiceEffectiveToInPast` (BusinessRule) |
| `Service.Create(...)` | Non-UTC `nowUtc` throws (ADR-0018). Then the stop algorithm below (**R15, R11, R14, R13, R12, R15** in R37 order). On success: positions `1..k` in request order (**S46**), `WithdrawnAtUtc = null`. | see below |
| `Service.Withdraw(withdrawFrom, today, nowUtc)` | P12 (**R21, R36, R41**). A refused call changes nothing. | `WithdrawalDateInPast`, `WithdrawalDoesNotShorten` (BusinessRule) |
| `EffectivePeriod.Overlaps(otherFrom, otherTo)` | P13 (**R35, R42**) | — (the handler returns `ServiceCodePeriodOverlap`) |

**R20 (immutability)** is structural: private setters, no public method other than `Withdraw` changes state, `Stops` read-only; `Service_ExposesNoMutatorOtherThanWithdraw` checks it by reflection, and the grants enforce it in the database (S47).

### The stop algorithm (R12, R14, R13, R11, R15) and the check order (R37)

`route.Stations` is in route position order, so a station's position is its index + 1; *n* = `route.Stations.Count`; *k* = number of stops; `mod(x, n)` is the non-negative remainder in `0..n−1`.

```text
Service.Create(id, code, name, route, direction, stops, days, period, nowUtc)
  if nowUtc.Offset != 0                                    → throw (ADR-0018)

  // (earlier in the handler: validation 400, code 400, names 400, period 400, EffectiveTo 422,
  //  route exists 422 — R37)
  1. if not route.IsActive                                 → ServiceRouteInactive(route.RouteId)      R15
  2. pos := { stationId → position } over route.Stations
     for s in stops, in order: if s ∉ pos                  → ServiceStopNotOnRoute(s)               R11
  3. closure := route.IsClosed and k ≥ 2 and stops[k−1] = stops[0]
     seen := ∅
     for i in 0 .. k−1:
        if stops[i] ∈ seen and not (closure and i = k−1)  → ServiceStopRepeated(stops[i])          R14
        seen := seen ∪ { stops[i] }
  4. distinct := closure ? k − 1 : k
     if distinct < (closure ? 3 : 2)                       → ServiceTooFewStops                     R13, R14
  5. if not route.IsClosed:                                                                         R12 open
        for i in 1 .. k−1:
           inOrder := direction = Forward ? pos[stops[i]] > pos[stops[i−1]]
                                          : pos[stops[i]] < pos[stops[i−1]]
           if not inOrder                                  → ServiceStopsOutOfOrder(stops[i])
     else:                                                                                          R12 closed
        travelled := 0
        for i in 1 .. k−1:
           step := direction = Forward ? mod(pos[stops[i]] − pos[stops[i−1]], n)   // wraps last → first
                                       : mod(pos[stops[i−1]] − pos[stops[i]], n)   // wraps first → last
           travelled := travelled + step
           limit := (closure and i = k−1) ? n : n − 1      // exactly one circuit only for the closure
           if travelled > limit                            → ServiceStopsOutOfOrder(stops[i])
  6. for s in stops, in order:
        if not route.Stations[pos[s] − 1].IsActive         → ServiceStopStationInactive(s)          R15
  7. create; stops get positions 1..k in request order (the closure stays the last row)
```

Why this is the ruling and nothing more:
- **Wrap.** On a closed route `mod` makes the step from the last station to the first (`Forward`) or from the first to the last (`Reverse`) a normal step of 1. On an open route there is no `mod`, so a wrap is out of order (S11).
- **Full circuit.** Only a closed route can have a closure (step 3), so `[P, Q, R, P]` on an open route is a repeated stop (S12). Any walk that returns to its start travels a multiple of *n*; with every earlier cumulative sum at most *n* − 1 and the last step at most *n* − 1, the closure's total is exactly *n*: one circuit.
- **More than one circuit.** Without the closure, a sum ≥ *n* means the service passes its first stop again, which the ruling forbids (spec §0.10 reading 1): `[D, A, C, E]` travels 2 + 2 + 2 = 6 > 4 (S9). With a stop repeated before the end, step 3 refuses it first (`[C, D, E, A, B, C, D]`, S9).
- **Steps are never 0** inside step 5: step 3 has already refused every repeat except the closure, whose last step is from a different station.
- **Only stops are checked for being active** (step 6; spec §0.10 reading 2), so passing an inactive station is allowed (S18).
- **The first offending stop is reported** in each check (R37): the unknown stop, the repeat, the stop at which order breaks or the cumulative distance first exceeds the limit, the inactive stop.

**Domain test cases that pin it** (all in `ServiceTests`, route RC = closed `[A, B, C, D, E]`, RO = open `[P, Q, R, S]`, as in spec §4):

| Case | Expected |
|---|---|
| RC `Forward` `[A, C, E]` | created; positions 1–3 (S1) |
| RC `Forward` `[D, E, A, B]`; `Reverse` `[B, A, E, D]`; `Forward` `[D, A, C]`; `Forward` `[C, B]` | created (S4, S5, S6) |
| RC `Forward` `[C, D, E, A, B, C]`, `[C, E, B, C]`; `Reverse` `[C, A, D, C]` | created, closing stop at the last position (S7) |
| RC `Forward` `[C, E, C]` | `ServiceTooFewStops` (S8) |
| RC `Forward` `[C, D, E, A, B, C, D]` | `ServiceStopRepeated(C)` (S9) |
| RC `Forward` `[D, A, C, E]` | `ServiceStopsOutOfOrder(E)` (S9) |
| RO `Forward` `[Q, R]`; `Reverse` `[S, R, P]` | created (S10) |
| RO `Forward` `[R, S, P]`; `Reverse` `[Q, P, S]` | `ServiceStopsOutOfOrder(P)`; `ServiceStopsOutOfOrder(S)` (S11) |
| RO `Forward` `[P, Q, R, P]` | `ServiceStopRepeated(P)` (S12) |
| RC `Forward` `[A, B, B, C]`; `[A, B, A, C]` | `ServiceStopRepeated(B)`; `ServiceStopRepeated(A)` (S13) |
| RC `Forward` `[A, C, B]`; RO `Forward` `[P, R, Q]` | `ServiceStopsOutOfOrder(B)`; `ServiceStopsOutOfOrder(Q)` (S14) |
| RC `[A]`; RC `[A, A]` | `ServiceTooFewStops` (S15; R14) |
| a stop not in RC; an id that names no station | `ServiceStopNotOnRoute(id)` (S16) |
| RC inactive | `ServiceRouteInactive` (S17) |
| C inactive: `[A, C, E]`; `Forward` `[B, D]` | `ServiceStopStationInactive(C)`; created (S18) |
| several faults at once (inactive route + stop off route; off route + repeat; repeat + too few; too few + order; order + inactive stop) | the earlier one in R37 order |
| every ordered pair of distinct stations on RC, both directions | created (a closed route allows any 2-stop extent) |
| every stop list of length ≤ *n* + 1 on closed and open routes of 3–6 stations, both directions | the algorithm agrees with an independent **walking simulation** in the test (walk the route one station at a time in the direction, wrapping only on a closed route, stopping when the next listed stop is reached; valid iff every stop is reached without visiting a station twice, except arriving back at the first stop as the last one) |

---

## R35 serialisation — the code lock

### Choice: `sp_getapplock` on the code, inside the transaction (P9, P10)

```sql
DECLARE @result int;
EXEC @result = sp_getapplock
    @Resource    = @resource,          -- N'timetable.ServiceCode:' + the validated code; a parameter
    @LockMode    = N'Exclusive',
    @LockOwner   = N'Transaction',
    @LockTimeout = 30000;
IF @result < 0
    THROW 50035, N'Could not acquire the service-code lock (R35).', 1;
```

`SqlServerServiceCodeLock.AcquireAsync(ServiceCode code, …)` refuses to run without `Database.CurrentTransaction` (`InvalidOperationException`, as the F-002 lock does), then runs the batch with `ExecuteSqlAsync` so the resource is a parameter (V5). It takes a `ServiceCode`, so only a value that passed `^[A-Z0-9]{2,10}$` can reach it. Resource names compare as binary, and codes are upper-case by construction, so one code is one resource.

**Why this and not an `UPDLOCK, HOLDLOCK` range read on `IX_Services_Code_EffectiveFrom`:**
1. **Scope.** A key-range lock on a code with no rows locks the gap up to the next key, which belongs to a **different** code, so requests on neighbouring codes would block each other. R35 requires that they do not. An application lock names exactly one code.
2. **Deadlocks.** Two serialisable readers of the same empty range both get `RangeS-U`, then both need `RangeI-N` to insert: the classic conversion deadlock, surfacing as error 1205 → `500`. An exclusive applock is taken before any read, so the second request simply waits.
3. **Plan dependence.** What a range lock covers depends on the index the optimizer chooses; a scan would lock far more. The applock does not depend on any plan.
4. **Code shape.** EF Core has no table-hint API, so a hinted read means raw SQL for the one query that decides the rule. The applock is one call through an abstraction with a precedent (F-002 P14) that has already been reviewed.

### What it serialises

Every operation that can create or change a period for code *X* takes the lock on *X* **before** it reads the periods that decide it, and holds it until commit or rollback. Reads run under `READ COMMITTED` (P10); a statement that starts after the lock is granted sees every commit made under it (also true under `READ_COMMITTED_SNAPSHOT`, which is statement-level).

| Race | Serialised outcome | Scenario |
|---|---|---|
| **create vs create**, same code | The second create reads after the first commits: overlapping → `409`, nothing written; adjacent → both `201` | S27, S28 |
| **create vs withdraw**, same code | Withdraw first: the create sees the shortened period → `204`, `201`. Create first: it sees the open-ended period → `409`, then the withdrawal → `204`. Never two overlapping rows | S29 |
| **withdraw vs withdraw**, same service | The second reloads `EffectiveTo` under the lock: "11-01 then 10-20" → `204`, `204`; "10-20 then 11-01" → `204`, `422 WithdrawalDoesNotShorten`; final `EffectiveTo` 2026-10-19 either way | S37 |
| different codes | Different resources: no waiting | R35 |
| a writer that bypasses the lock (direct SQL) | `EffectiveTo` concurrency token: `409 ServiceChangedConcurrently`, nothing written | R36 |

The lock covers `EffectiveFrom`/`EffectiveTo`, the only values the overlap rule reads; nothing else about a service changes (R20).

### How it is tested against real SQL Server

- **Forced orders, deterministic** (S29 both orders, S37 both orders): `YCR.Application.Tests/Timetable/GatedServiceCodeLock.cs`, a test decorator registered over the real `IServiceCodeLock`. It signals when a caller has **acquired** the real lock and then waits on a gate. The test starts operation 1, waits for "acquired", starts operation 2, waits until it signals "acquiring", asserts after 500 ms that operation 2 has **not** acquired (proving it is blocked by the real SQL lock), then opens operation 1's gate. The order is fixed by construction, not by timing. **Mutation check for stage 6:** with `AcquireAsync` removed from either handler, the "still waiting" assertion fails.
- **Unforced parallel** (S27, S28, and one S29 run): two or more scopes, `Task.WhenAll`, assertions on outcome counts and on the committed rows (exactly one service and one `Timetable.ServiceCreated` event for overlapping periods; never two overlapping committed rows), never on which request wins (F-003 R-9).
- **Different codes do not wait:** `CreateService_WhileAnotherCodeIsLocked_DoesNotWait`.
- **Privilege:** `DatabasePrivilegeTests.ApplicationCredential_CanTakeTheServiceCodeApplock` — as `ycr_app`, no grant: refuses without a transaction; a second transaction on the same code waits; a transaction on another code does not.
- **Backstop:** `WithdrawService_WhenRowChangesBehindTheLock_Returns409ChangedConcurrentlyAndWritesNothing` changes `EffectiveTo` by direct SQL between the handler's load and its save (through a test `IAuditWriter` decorator whose `Record` runs the update on a separate connection), then asserts `409`, no audit row, and the directly written value intact.

### What the application login needs

**Nothing extra.** `sp_getapplock` is executable by `public` (F-002 V5). `Security_TimetableGrants` therefore contains no `EXECUTE` grant, and the privilege test proves the lock works under `ycr_app` with only spec §7's grants.

---

## DB changes

Three migrations, applied in order by the EF migration bundle under `ycr_migrator` (ADR-0022), each reviewed per `docs/workflows/04-database-change.md`, named `yyyyMMddHHmmss_<Module>_<Change>` (`docs/20` §6). **Upgrade path:** all three are additive on F-003's schema (last migration `20260924152836_Security_NetworkRouteGrants`); `TimetableMigrationTests` migrates a database already at that migration, with station and route rows in it (`docs/21` §Data). No `network` column, index, key or grant changes (ADR-0025 item 6; O3).

### 1. `<ts>_Timetable_CreateServices` (EF model-built)

Every object is declared in the EF model, including the check constraints, so `has-pending-model-changes` sees drift (V4).

**`timetable.Services`** — exactly spec §7: `Id uniqueidentifier` PK (`ValueGeneratedNever`, R23) · `Code nvarchar(10)` · `NameEn nvarchar(100)` · `NameMy nvarchar(100)` · `RouteId uniqueidentifier` · `Direction nvarchar(10)` · `RunsOnMonday` … `RunsOnSunday bit` · `EffectiveFrom date` · `EffectiveTo date NULL` (concurrency token) · `CreatedAtUtc datetimeoffset(3)` · `WithdrawnAtUtc datetimeoffset(3) NULL`. No `rowversion`.

| Object | Definition |
|---|---|
| `PK_Services` | clustered on `Id` |
| `IX_Services_Code_EffectiveFrom` | nonclustered on `(Code, EffectiveFrom)`, **not unique** (R35) |
| `IX_Services_RouteId` | nonclustered on `RouteId` |
| `FK_Services_Routes_RouteId` | → `network.Routes(Id)`, `NO ACTION` (R29; O1) |
| `CK_Services_Direction` | `[Direction] IN (N'Forward', N'Reverse')` |
| `CK_Services_OperatingDays` | `[RunsOnMonday] = 1 OR [RunsOnTuesday] = 1 OR … OR [RunsOnSunday] = 1` |
| `CK_Services_EffectivePeriod` | `[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom] OR [WithdrawnAtUtc] IS NOT NULL` |
| `CK_Services_CreatedAtUtc_Utc` | `DATEPART(TZOFFSET, [CreatedAtUtc]) = 0` |
| `CK_Services_WithdrawnAtUtc_Utc` | `[WithdrawnAtUtc] IS NULL OR DATEPART(TZOFFSET, [WithdrawnAtUtc]) = 0` |

**`timetable.ServiceStops`** — `ServiceId uniqueidentifier` · `Position int` · `StationId uniqueidentifier`.

| Object | Definition |
|---|---|
| `PK_ServiceStops` | clustered on `(ServiceId, Position)`; also covers the `ServiceId` foreign key (O2) |
| `CK_ServiceStops_Position` | `[Position] >= 1` |
| `IX_ServiceStops_StationId` | nonclustered on `StationId`, **not unique** (the closure repeats a station, R14) |
| `FK_ServiceStops_Services_ServiceId` | → `timetable.Services(Id)`, `NO ACTION` (EF's default would be `Cascade`, so it is explicit) |
| `FK_ServiceStops_Stations_StationId` | → `network.Stations(Id)`, `NO ACTION` (R29; O1) |

**Cross-schema foreign keys (ADR-0025 items 5–7).** Configured in `Configurations/Timetable` with `HasOne<Route>().WithMany()` and `HasOne<Station>().WithMany()`: no navigation on either side (O1), created by this migration (the referencing module's), `NO ACTION`. Network rows are never deleted (`ycr_app` has no `DELETE` on them), so the keys cost nothing on delete. The handler checks existence through the contract first for a stable error code; the key is the backstop (S48). No key to `network.RouteStations` (item 6; R11 is the aggregate's).

**Index decision** — each query and write, with the index it uses:

| Named query / write | Access path |
|---|---|
| Create, under the lock: periods of a code `Services WHERE Code = @c` | `IX_Services_Code_EffectiveFrom` seek (+ key lookup for `EffectiveTo`, a few rows) |
| `INSERT` FK validation | `PK_Routes`, `PK_Stations`, `PK_Services` |
| Withdraw: code by id; reload with stops | `PK_Services`; `PK_ServiceStops` range on `ServiceId` |
| Get: service row; stops by position | `PK_Services`; `PK_ServiceStops` range |
| List: `ORDER BY Code, EffectiveFrom, Id`, `COUNT`, page | `IX_Services_Code_EffectiveFrom` ordered scan (a non-unique nonclustered index carries the clustering key `Id` in its key, so the order is complete) |
| List `?routeId=` | `IX_Services_RouteId` |
| `stopCount` | `PK_ServiceStops` prefix |
| Contract reads | `PK_Routes`, `PK_RouteStations` range, `PK_Stations` (unchanged Network indexes) |
| Reverse FK check on `DELETE FROM network.Stations` / `network.Routes` | **never runs** (no `DELETE` grant) |

`IX_ServiceStops_StationId` serves no F-004 query. It is kept because spec §7 approved it and EF adds it by convention for the foreign key unless another index leads with `StationId` (O2; F-003 O1–O3); a later "services calling at station X" read will use it. It is declared explicitly so it is deliberate (P16).

Data impact: none (new, empty tables). **No seed or fixture service** (R27, OQ1).

**Rollback.** `Down()` drops `ServiceStops`, then `Services`, then **the `timetable` schema**, which this migration creates and owns (V8; the `Network_CreateStations` pattern). The cross-schema keys go with their tables, so `network` is untouched. After real services exist, a down-migration destroys service history; production recovery is a restore, not `Down()` (as F-002/F-003). **Roll-forward:** any correction is a new migration; an applied migration is never edited.

### 2. `<ts>_Identity_SeedServicePermissionGrants`

Inserts exactly ten `identity.RolePermissions` rows (`docs/10` §Service permission grants): `services.manage` → `SystemAdministrator`, `RailwayAdministrator`; `services.read` → all eight roles. Role ids are re-declared constants (a migration never depends on another migration's code). Header comment: "BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-25; T-044, OQ49) — not a Myanma Railways answer." No role, no user, no `trains.*` or `schedules.*` row (OQ42, B1).

**Rollback.** `Down()` deletes exactly those ten `(RoleId, Permission)` pairs; nothing references them; the service endpoints then return `403` to everyone. **Roll-forward:** a changed grant is a new reviewed migration, never an API call.

### 3. `<ts>_Security_TimetableGrants`

Exactly spec §7 / S47, as raw SQL (like `Security_NetworkRouteGrants`):

```sql
GRANT SELECT, INSERT ON [timetable].[Services] TO [ycr_app];
GRANT UPDATE ON [timetable].[Services]([EffectiveTo], [WithdrawnAtUtc]) TO [ycr_app];
GRANT SELECT, INSERT ON [timetable].[ServiceStops] TO [ycr_app];
```

**Deliberately absent**, asserted by `DatabasePrivilegeTests`: `DELETE` on both tables (R33); `UPDATE` of `Services.Id`, `Code`, `NameEn`, `NameMy`, `RouteId`, `Direction`, every `RunsOn…`, `EffectiveFrom`, `CreatedAtUtc`; any `UPDATE` on `ServiceStops`; `ALTER`, `CONTROL` and any DDL; any `EXECUTE` (the applock needs none). The migration's XML comment states R20 and R33: these grants are the database statement that a service is immutable except for withdrawal and is never deleted.

**Rollback.** `Down()` revokes exactly these, in reverse order; no data is lost. **Roll-forward:** widening needs a new migration, a spec change and a review, never a failing test.

**Order.** 1 before 3 (3 grants on 1's objects); 2 depends only on F-002's roles. 1 → 2 → 3 mirrors F-002 and F-003.

---

## API changes

Base path `/api/v1`, JSON camelCase, dates `YYYY-MM-DD`, ProblemDetails with `errorCode` and `traceId` (ADR-0004). Every endpoint also returns `401 Auth.Unauthenticated` (with `WWW-Authenticate: Bearer`) and, during a must-change session, `403 Auth.PasswordChangeRequired` (`docs/08`). No `Idempotency-Key` (R25). No version token (E10).

### Endpoint inventory

| Method | Path | Permission | Body limit | Idempotency | Response / errors |
|---|---|---|---|---|---|
| POST | `/api/v1/services` | `services.manage` | **32 KiB** | No (R25) | `201` + `CreateServiceResponse { id }`, `Location: /api/v1/services/{id}` · `400 Common.ValidationFailed` · `400 Timetable.InvalidServiceCode` · `400 Timetable.InvalidServiceName` · `400 Timetable.InvalidEffectivePeriod` · `400` malformed JSON · `401` · `403` · `409 Timetable.ServiceCodePeriodOverlap` · `413` · `422 Timetable.ServiceEffectiveToInPast` · `422 Timetable.ServiceRouteNotFound` · `422 Timetable.ServiceRouteInactive` · `422 Timetable.ServiceStopNotOnRoute` · `422 Timetable.ServiceStopRepeated` · `422 Timetable.ServiceTooFewStops` · `422 Timetable.ServiceStopsOutOfOrder` · `422 Timetable.ServiceStopStationInactive` |
| GET | `/api/v1/services/{id:guid}` | `services.read` | — | n/a | `200` + `ServiceResponse` · `401` · `403` · `404 Timetable.ServiceNotFound` |
| GET | `/api/v1/services` | `services.read` | — | n/a | `?page=1&pageSize=50` (max 200) `&routeId=` (optional); ordered by `code`, `effectiveFrom`, then `id`; withdrawn included; an unknown `routeId` gives an empty page · `200` + `{ items, page, pageSize, totalCount }` · `400 Timetable.InvalidPageRequest` · `401` · `403` |
| POST | `/api/v1/services/{id:guid}/withdraw` | `services.manage` | **1 KiB** | No (R25) | `204` · `400 Common.ValidationFailed` · `400` malformed JSON · `401` · `403` · `404 Timetable.ServiceNotFound` · `409 Timetable.ServiceChangedConcurrently` · `413` · `422 Timetable.WithdrawalDateInPast` · `422 Timetable.WithdrawalDoesNotShorten` |

**Deliberately absent** (spec §6): `PATCH`/`PUT /services/{id}`, reactivation, `DELETE /services/{id}` (R20, R33), `/trains` (R5), `/schedules/versions*` and any time field (R22). A test pins that each answers `404`/`405` (§Test plan).

### Contracts

```text
CreateServiceRequest   { code: string?, nameEn: string?, nameMy: string?, routeId: string?,
                         direction: string?, stopStationIds: string?[]?, operatingDays: string?[]?,
                         effectiveFrom: string?, effectiveTo: string? }                 // P6
CreateServiceResponse  { id }
WithdrawServiceRequest { withdrawFrom: string? }                                        // P6
ServiceResponse        { id, code, nameEn, nameMy, direction,
                         route: ServiceRouteResponse, stops: ServiceStopResponse[],
                         operatingDays: string[], effectiveFrom, effectiveTo?, neverRuns,
                         createdAtUtc, withdrawnAtUtc? }
ServiceRouteResponse   { id, code, nameEn, nameMy, isClosed, isActive }                 // current values (R30)
ServiceStopResponse    { position, stationId, code, nameEn, nameMy, isActive }          // current values (R30)
ServiceSummaryResponse { id, code, nameEn, nameMy, routeId, routeCode, direction, stopCount,
                         operatingDays, effectiveFrom, effectiveTo?, neverRuns, createdAtUtc,
                         withdrawnAtUtc? }
```

Exactly spec §6. No request has an actor field (S45). Responses are mapped explicitly from DTOs; no EF entity is serialised, and the Api allowlist forbids `YCR.Domain.Timetable` in the Api.

**Validators** (shape only; every business rule stays in the domain):
- `CreateServiceRequestValidator`: `code`, `nameEn`, `nameMy` `NotEmpty()` (blank → `400 Common.ValidationFailed`, F-003 Amendment 3); `routeId` a `D`-format GUID; `direction` exactly `Forward` or `Reverse`; `stopStationIds` present, non-empty, **at most `MaxStopStationIds = 200`** (REQUIRED CONTROL comment, R31; spec §0.10 Q2 ruling), each element a `D`-format GUID; `operatingDays` present, 1–7 elements, each an exact day name, no repeats; `effectiveFrom` present and `yyyy-MM-dd`; `effectiveTo` absent, `null` or `yyyy-MM-dd`.
- `WithdrawServiceRequestValidator`: `withdrawFrom` present and `yyyy-MM-dd`.

### Error code → HTTP

| Code | `ErrorType` | HTTP |
|---|---|---|
| `Common.ValidationFailed` (validation filter) | — | 400 |
| `Timetable.InvalidServiceCode`, `InvalidServiceName`, `InvalidEffectivePeriod`, `InvalidPageRequest` | Validation | 400 |
| `Timetable.ServiceNotFound` | NotFound | 404 |
| `Timetable.ServiceCodePeriodOverlap`, `ServiceChangedConcurrently` | Conflict | 409 |
| `Timetable.ServiceEffectiveToInPast`, `ServiceRouteNotFound` (a `422`, not a `404`: the addressed resource is the new service, F-003 E8), `ServiceRouteInactive`, `ServiceStopNotOnRoute`, `ServiceStopRepeated`, `ServiceTooFewStops`, `ServiceStopsOutOfOrder`, `ServiceStopStationInactive`, `WithdrawalDateInPast`, `WithdrawalDoesNotShorten` | BusinessRule | 422 |
| Oversized body; malformed JSON | framework (T-042) | 413; 400 |

Only `ResultExtensions` maps these. Messages name only the caller's input (a code, a station or route id). An exception from the lock (timeout, error `50035`) is not caught and becomes the F-001 opaque `500`.

### Body limits (P20, R31)

Largest valid `CreateServiceRequest`, compact JSON as `System.Text.Json` writes it by default (every non-ASCII character escaped as `\uXXXX`, 6 bytes):

| Part | Bytes |
|---|---|
| Property names and punctuation (`{"code":"","nameEn":"",…,"effectiveTo":""}`) | 138 |
| `code` (10) · `direction` (7) · `routeId` (36) · two dates (20) | 73 |
| `nameEn` 100 characters, worst case all escaped | 600 |
| `nameMy` 100 Myanmar characters, escaped | 600 |
| `stopStationIds`: 200 × 38 (quoted GUID) + 199 commas | 7,799 |
| `operatingDays`: all seven quoted names + 6 commas | 70 |
| **Total** | **9,280** (8,780 with an ASCII `nameEn`) |

**32 KiB** (32,768) leaves 3.5 times the largest valid body for pretty-printing and client variation, matches F-003's value, and is far below the server default. A body that escapes ASCII characters too (never done by standard serialisers) could exceed it and would get `413`; accepted.

`WithdrawServiceRequest` is `{"withdrawFrom":"2026-11-01"}`, **29 bytes**. **1 KiB** leaves 35 times that for whitespace and unknown properties (ignored by the binder).

Both are `RequestSizeLimitAttribute` metadata on the endpoint (constants `ServiceEndpoints.CreateServiceMaxRequestBodyBytes = 32 * 1024` and `WithdrawServiceMaxRequestBodyBytes = 1024`), copied by endpoint routing into Kestrel's `IHttpMaxRequestBodySizeFeature` before the body is read. `ServiceRequestLimitTests` proves both on real Kestrel (`UseKestrel(0)`), declared-length and chunked, plus malformed JSON, plus the largest valid body reaching the handler (§Test plan). Other endpoints stay T-042's.

### OpenAPI, `.http`, smoke

- **OpenAPI/Scalar:** unchanged exposure (Development only). The four endpoints appear under tag `Services` with `.WithName`, `.WithSummary`, `.Produces<…>` and `.ProducesProblem(...)`, as `RouteEndpoints` declares them. The create summary says stop ids are station ids in GUID `D` format, in stop order, at most 200; dates are `YYYY-MM-DD`.
- **`YCR.Api.http`**, a "Services (F-004)" section: create a `Forward` service on a closed route → `201`; a wrap → `201`; a full circuit → `201`; a stop out of order → `422 Timetable.ServiceStopsOutOfOrder`; the same code with an overlapping period → `409 Timetable.ServiceCodePeriodOverlap`; list, list by `routeId`, read one → `200`; withdraw → `204`; withdraw to a later date → `422 Timetable.WithdrawalDoesNotShorten`; the timetable-change pair (withdraw from D, create the same code from D). Variables `@serviceId`, `@routeId`; each comment names the permission and errors, as the route section does.
- **`api-smoke` additions**, after the route checks, same administrator token, so they run through the seed, the grants, the contract and the lock under `ycr_app` in the deployed shape. `today=$(TZ=Asia/Yangon date +%F)`, `later=$(TZ=Asia/Yangon date -d '+30 days' +%F)`:
  1. Anonymous `GET /api/v1/services` → `401`, `errorCode` `Auth.Unauthenticated`.
  2. `POST /api/v1/routes` `SMOKE2` (open, `[SMA, SMB]`) → `201` (the F-003 route was deactivated by its own checks).
  3. `POST /api/v1/services` `{ code: "SV1", routeId: SMOKE2, direction: "Forward", stopStationIds: [SMA, SMB], operatingDays: ["Monday"], effectiveFrom: $today }` → `201`.
  4. `GET /api/v1/services/{id}` → `200`, two stops; `GET /api/v1/services` → `200`.
  5. `POST /api/v1/services/{id}/withdraw` `{ withdrawFrom: $later }` → `204`; again → `422`, `errorCode` `Timetable.WithdrawalDoesNotShorten`.

  `CiWorkflowTests` inspects `env:` and provisioning, not the checks, so it needs no change (confirmed at step 9).
- **Amendment 2 (hein, 2026-09-25, T-046 G2):** "Run the API" starts `YCR.Api.dll` with `working-directory: src/YCR.Api/bin/Release/net10.0`, so the shipped `appsettings.json` is read; the job sets no `Time__LocalTimeZone`. After the health checks, the smoke asserts the API log's `Content root path:` is that directory and the API process environment has no `Time__` variable. If the file were not read the API would refuse to start and "Run the API" would fail.

---

## Audit

Through `IAuditWriter.Record`, inside the handler's single `SaveChangesAsync`, in the P10 transaction (ADR-0017, ADR-0021). A refused or losing request writes no event.

| Action | When | `SubjectType` | `SubjectId` | `BeforeJson` | `AfterJson` | `AuthorizedByPermission` |
|---|---|---|---|---|---|---|
| `Timetable.ServiceCreated` | S1 | `Timetable.Service` | service id | null | snapshot | `services.manage` (endpoint → `ICurrentUser`) |
| `Timetable.ServiceWithdrawn` | S30, and each later withdrawal (S35) | `Timetable.Service` | service id | snapshot | snapshot | `services.manage` |

`PayloadVersion = 1`.

```text
ServiceAuditSnapshot(string Code, string NameEn, string NameMy, Guid RouteId, string RouteCode,
                     string Direction, IReadOnlyList<ServiceStopAuditSnapshot> Stops,
                     IReadOnlyList<string> OperatingDays,               // Monday first
                     DateOnly EffectiveFrom, DateOnly? EffectiveTo,
                     DateTimeOffset? WithdrawnAtUtc) : IAuditSnapshot
ServiceStopAuditSnapshot(int Position, Guid StationId, string StationCode)
```

Exactly spec §8. Records in `YCR.Application.Timetable`, so `AuditSnapshots_WithSourceAssemblies_AreValid` covers them. `ServiceAuditSnapshot.From(Service, string routeCode, IReadOnlyDictionary<Guid, string> stationCodes)`; the codes come from the contract (create: the `RouteReference` already loaded; withdraw: `GetRouteSummariesAsync` and `GetStationsAsync`). They are stable because station and route codes are never reused. No personal data, token or key (ADR-0021 rule 4). XML comment carries the "changing this shape bumps `PayloadVersion`" warning.

**Logging:** message templates only (`docs/20` §7); nothing beyond what the F-003 handlers log. **Metrics:** none (`docs/17`; spec §8).

---

## Security impact

| Item | Treatment |
|---|---|
| Permissions | `services.manage` on both POSTs; `services.read` on both GETs; `.RequireAuthorization(Permissions.…)` on each. No anonymous service endpoint. **No station or route permission grants a service right, and no service permission grants a station or route right** (R4): API tests with only `routes.manage`, `routes.read`, `stations.manage` or `stations.read` get `403` on POST and GET `/services` (S42), and `ServicePermissionGrantTests` checks the seeded rights of all eight roles with real tokens. |
| Grants | Data, seeded by the reviewed `Identity_SeedServicePermissionGrants` (OQ49 provisional ruling). No API edits them. The seed test keeps it equal to `docs/10`. |
| Database least privilege | `Security_TimetableGrants`: insert-only rows except `EffectiveTo`/`WithdrawnAtUtc`; no `DELETE`; no DDL; no `EXECUTE`. `DatabasePrivilegeTests` asserts presences **and** absences (S47) and executes the allowed and denied statements. Every Application and API test runs as `ycr_app`. |
| Cross-module integrity | The FKs refuse a service naming a missing route or station from any writer, including direct SQL under `ycr_app` (S48). The contract is read-only (ADR-0025 item 3); Timetable cannot change Network data through it, and the architecture rules keep Timetable out of Network's domain and context (S50). |
| Concurrency integrity | R35 by the code lock (§R35), R36 backstop by the concurrency token. |
| Audit integrity | Actor fields from `ICurrentUser`, never the body (S45); append-only ledger; explicit snapshot records. |
| Input handling | Codes and names by value objects; GUIDs, dates, direction and day names by the validators (P6); **`stopStationIds` ≤ 200 (REQUIRED CONTROL)**; `pageSize` ≤ 200; body limits 32 KiB / 1 KiB before binding (**REQUIRED CONTROL**, R31); EF parameterises all SQL; the applock resource is a parameter built from a validated code. |
| Error disclosure | Messages name only the caller's input; `413`/`400` framework bodies carry no internals (tested as F-003); a lock timeout is an opaque `500`. |
| Denial of service | A request holds the code lock only for one indexed read and one insert or update; the lock times out after 30 s; only two administrator roles can call the write endpoints, and every call is audited. |

**`docs/18` threats this feature touches:** *Unauthorized configuration* (services are railway operational configuration; `services.manage` held by two roles; every change audited). *Privilege escalation* (seeded grants only; no grant API; exact-grant tests). *Insider manipulation* (immutability enforced by the grants as well as the domain; each withdrawal audited with before and after). *Data disclosure* (reads need `services.read`; no personal data). *API abuse* (body limits, stop cap, page cap, lock timeout). *Audit tampering* (unchanged ledger controls). Cookie, CSP and XSS controls are not touched (no cookie-bearing endpoint, no SPA).

---

## Test plan

Names follow `docs/20` §2. "TH" = F-001's test authentication handler; "Real" = real ES256 tokens; "Kestrel" = real Kestrel. Application and API tests run as `ycr_app`. Unless stated, the test clock is **2026-10-01 03:00 UTC** (2026-10-01 09:30 Asia/Yangon), and routes RC and RO are built through `CreateStationHandler`/`CreateRouteHandler` (`TimetableTestData`). Stage 5 (a different agent) owns completeness and checks itself against this section.

### `YCR.Domain.Tests`

| Test | Spec |
|---|---|
| `Common/BilingualNameTests` (moved from `Network/`): every existing case unchanged, with the explicit station error where it was implicit | regression (P2) |
| ″ `BilingualName_Create_WithStationError_WhenInvalid_ReturnsInvalidStationName` (rewrites `…WithoutErrorArgument_StillReturnsInvalidStationName` with exactly its assertions: `IsFailure` and `NetworkErrors.InvalidStationName` for `("   ", "Yangon Myanmar")`; ruling Q1) | regression (P2) |
| ″ `BilingualName_Create_WithServiceError_WhenInvalid_ReturnsInvalidServiceName` (theory: blank, whitespace, 101 characters after trimming, either side) | S22, R7 |
| `Timetable/ServiceCodeTests.ServiceCode_Create_WithValidCode_ReturnsCode` (theory: `S1`, `S101`, `ABCDEFGHIJ`, ` LOOP1 ` trimmed) | R6 |
| ″ `ServiceCode_Create_WithInvalidCode_ReturnsInvalidServiceCode` (theory: `s1`, `A`, `ABCDEFGHIJK`, `S-1`, `S 1`, empty, whitespace, null) | S22, R6 |
| `Timetable/OperatingDaysTests.Create_WithSundayAndMonday_HasExactlyThoseDaysMondayFirst` | S52, R40 |
| ″ `Create_WithNoDays_Throws` · `Create_WithRepeatedDay_Throws` | R17 |
| `Timetable/EffectivePeriodTests.ForNewService_WithToBeforeFrom_ReturnsInvalidEffectivePeriod` | S23, R19 |
| ″ `ForNewService_WithToEqualToFrom_IsAOneDayPeriod` | S23 |
| ″ `ForNewService_WithFromBeforeToday_Succeeds` | S40, R39 |
| ″ `ForNewService_WithToBeforeToday_ReturnsEffectiveToInPast` · `ForNewService_WithToEqualToToday_Succeeds` | S40a, R39 |
| ″ `ForNewService_WithBothFaults_ReturnsInvalidEffectivePeriodFirst` | R37 |
| ″ `Overlaps_WithPeriods_FollowsTheInclusiveRule` (theory: open-ended vs later; inclusive edge 12-31/12-31; adjacent 12-31/01-01; both open-ended; past `from` vs future open-ended; adjacent past period) | S24, S25, S26, S28, S40, R35 |
| ″ `Overlaps_WithAPeriodThatNeverRuns_IsFalse` | R42 |
| `Timetable/ServiceTests.Create_ForwardOnClosedRoute_HasContiguousPositionsInRequestOrder` | S1, S46 |
| ″ `Create_WrapOnClosedRoute_Succeeds` (theory: S4, S5, S6 ×2) | S4–S6, R12 |
| ″ `Create_FullCircuit_SucceedsWithClosingStopLast` (theory ×3) | S7, R14 |
| ″ `Create_FullCircuitWithTwoDistinctStations_ReturnsTooFewStops` | S8 |
| ″ `Create_MoreThanOneCircuit_IsRefused` (theory: repeated; out of order) | S9 |
| ″ `Create_OpenRoutePartAndReverse_Succeeds` (theory ×2) | S10, R10 |
| ″ `Create_WrapOnOpenRoute_ReturnsOutOfOrder` (theory ×2) | S11 |
| ″ `Create_ClosureOnOpenRoute_ReturnsRepeated` | S12 |
| ″ `Create_RepeatedStop_ReturnsRepeated` (theory ×2) | S13 |
| ″ `Create_StopsOutOfOrder_ReturnsOutOfOrder` (theory: RC, RO) | S14 |
| ″ `Create_WithFewerThanTwoStops_ReturnsTooFewStops` (theory: `[A]`, `[A, A]`) | S15, R13 |
| ″ `Create_StopNotOnRoute_ReturnsStopNotOnRoute` (theory: other station; unknown id) | S16, R11 |
| ″ `Create_OnInactiveRoute_ReturnsRouteInactive` | S17, R15 |
| ″ `Create_WithInactiveStopStation_ReturnsStopStationInactive` · `Create_PassingInactiveStationWithoutStopping_Succeeds` | S18, R15 |
| ″ `Create_WithSeveralFailures_ReportsThemInR37Order` (theory) | R37 |
| ″ `Create_WithinOneCheck_ReportsTheFirstOffendingStop` (theory) | R37 |
| ″ `Create_AnyTwoDistinctStationsOnClosedRoute_SucceedInBothDirections` | R10, R12 |
| ″ `Create_OnSmallRoutes_AgreesWithWalkingSimulation` | R12, R14 |
| ″ `Create_WithNonUtcTime_Throws` | R26 |
| ″ `Withdraw_FromFutureDate_SetsEffectiveToDayBeforeAndWithdrawnAt` | S30, R21 |
| ″ `Withdraw_FromToday_EndsYesterday` | S31 |
| ″ `Withdraw_FromPastDate_ReturnsWithdrawalDateInPastAndChangesNothing` | S32 |
| ″ `Withdraw_ThatWouldExtendOrKeepTheEnd_ReturnsDoesNotShortenAndChangesNothing` (theory: S33 ×2, S34) | S33, S34 |
| ″ `Withdraw_SecondTime_ShortensFurtherButNeverLengthens` | S35 |
| ″ `Withdraw_OnOrBeforeEffectiveFrom_NeverRuns` | S36, R41 |
| ″ `Withdraw_WithNonUtcTime_Throws` | R26 |
| ″ `Service_ExposesNoMutatorOtherThanWithdraw` (reflection) | R20 |
| ″ `ServiceAndServiceStop_HaveNoNavigationToNetworkTypes` (reflection) | R8, R29 |
| ″ `ServiceStop_HasOnlyServiceIdPositionAndStationId` (no times, no stop attributes) | R14, R22 |
| existing `StationCodeTests`, `RouteCodeTests`, `StationTests`, `RouteTests` | regression (P2) |

### `YCR.Application.Tests` (real SQL Server, `ycr_app`, `TestClock`)

Support: `Timetable/TimetableHandlerTestBase.cs` (as `NetworkHandlerTestBase`, plus `LocalTimeOptions` = `Asia/Yangon`), `Timetable/TimetableTestData.cs`, `Timetable/GatedServiceCodeLock.cs`, `Timetable/AuditRecordHook.cs`.

| Test | Spec |
|---|---|
| `Timetable/CreateServiceHandlerTests.CreateService_WithValidCommand_PersistsServiceStopsAndOneAuditEvent` (row values, `RunsOn…`, `EffectiveTo`/`WithdrawnAtUtc` null, positions 1..3; audit subject, snapshot, `AuthorizedByPermission`) | S1, S46 |
| ″ `CreateService_WithValidPatterns_Creates` (theory: S4, S5, S6 ×2, S7 ×3, S10 ×2) | S4–S7, S10 |
| ″ `CreateService_WithInvalidPatterns_ReturnsErrorAndWritesNothing` (theory: S8, S9 ×2, S11 ×2, S12, S13 ×2, S14 ×2, S15, S16 ×2) | S8, S9, S11–S16 |
| ″ `CreateService_OnInactiveRoute_ReturnsRouteInactiveAndWritesNothing` | S17 |
| ″ `CreateService_WithInactiveStopStation_ReturnsStopStationInactiveAndWritesNothing` · `CreateService_PassingInactiveStation_Creates` | S18 |
| ″ `CreateService_WithUnknownRoute_ReturnsRouteNotFoundAndWritesNothing` | S19 |
| ″ `CreateService_WithInvalidCodeOrName_ReturnsValidationErrorAndWritesNothing` (theory) | S22 |
| ″ `CreateService_WithEffectiveToBeforeEffectiveFrom_ReturnsInvalidPeriod` · `CreateService_WithOneDayPeriod_Creates` | S23 |
| ″ `CreateService_WithOverlappingPeriodForSameCode_ReturnsConflictAndWritesNothing` (theory: open-ended, inclusive edge, other route/direction/days, past-from open-ended) | S24, S25, S40, R35 |
| ″ `CreateService_WithAdjacentPeriodForSameCode_Creates` (theory: 2027-01-01 after 12-31; 09-01–10-04 before 10-05) | S25, S40 |
| ″ `CreateService_WithPastEffectiveFrom_CreatesAndStampsCreatedAtWithClockNow` (row and `Timetable.ServiceCreated` carry the same instant) | S40, R39 |
| ″ `CreateService_WithEffectiveToBeforeToday_ReturnsEffectiveToInPastAndWritesNothing` · `CreateService_WithEffectiveToToday_Creates` | S40a |
| ″ `CreateService_AtYangonMidnight_UsesTheYangonDateAsToday` (clock 2026-09-30 17:29:59Z vs 17:30:00Z, `effectiveTo` 2026-09-30) | R38 |
| ″ `CreateService_WithSeveralFailures_ReturnsTheFirstInR37Order` (theory, including overlap last) | R37 |
| ″ `CreateService_WithParallelSameCodeOverlappingPeriods_PersistsExactlyOneServiceAndOneAuditEvent` (five scopes, `Task.WhenAll`) | **S27** |
| ″ `CreateService_WithParallelSameCodeAdjacentPeriods_CreatesBoth` | **S28** |
| ″ `CreateService_WhileAnotherCodeIsLocked_DoesNotWait` | R35 |
| ″ `CreateService_AuditActor_ComesFromCurrentUserOnly` | S45 |
| ″ `CreateService_WithMyanmarName_RoundTripsExactly` (`\u`-escaped literal) | S49 |
| ″ `CreateService_OperatingDays_StoredAsBitsAndReadMondayFirst` | S52, R40 |
| `Timetable/WithdrawServiceHandlerTests.WithdrawService_FromFutureDate_UpdatesBothColumnsAndWritesOneEventWithBeforeAndAfter` | S30, R36 |
| ″ `WithdrawService_FromToday_EndsYesterday` | S31 |
| ″ `WithdrawService_FromPastDate_ReturnsDateInPastAndWritesNothing` | S32 |
| ″ `WithdrawService_ThatDoesNotShorten_ReturnsDoesNotShortenAndWritesNothing` (theory: S33 ×2, S34) | S33, S34 |
| ″ `WithdrawService_Twice_ShortensAgainThenRefusesLonger` | S35 |
| ″ `WithdrawService_BeforeEffectiveFrom_NeverRunsAndFreesTheCode` | S36, R41, R42 |
| ″ `WithdrawService_ThenCreateSameCodeFromSameDate_BothSucceed` | S26 |
| ″ `WithdrawService_WithUnknownId_ReturnsNotFound` | S38 |
| ″ `WithdrawService_AfterRouteOrStopStationDeactivated_Succeeds` (theory) | S39 |
| ″ `WithdrawAndCreate_SameCode_WithdrawFirst_Returns204Then201` (forced) | **S29** |
| ″ `WithdrawAndCreate_SameCode_CreateFirst_Returns409Then204` (forced) | **S29** |
| ″ `WithdrawAndCreate_SameCodeInParallel_NeverCommitOverlappingServices` (unforced) | **S29** |
| ″ `WithdrawService_TwoWithdrawalsForced_EndOnTheShorterDateInEitherOrder` (theory: both orders; outcomes and event counts) | **S37** |
| ″ `WithdrawService_WhenRowChangesBehindTheLock_Returns409ChangedConcurrentlyAndWritesNothing` | R36 |
| ″ `WithdrawServiceSql_UpdatesOnlyEffectiveToAndWithdrawnAtAndNoServiceStops` (V2) | R36, S47 |
| `Timetable/ServiceQueryHandlerTests.GetService_WithKnownId_ReturnsStopsInOrderWithCurrentRouteAndStationValues` | S2, R30 |
| ″ `GetService_FullCircuit_KeepsTheClosingStopAtTheLastPosition` | S7 |
| ″ `GetService_AfterRouteOrStationDeactivated_ShowsInactiveFlagsAndRowsUnchanged` (theory) | S20, R16 |
| ″ `GetService_ThatNeverRuns_ReturnsStoredDatesAndNeverRunsTrue` | S36, R41 |
| ″ `GetService_WithUnknownId_ReturnsNotFound` | S38 |
| ″ `ListServices_ReturnsPagedEnvelopeOrderedByCodeThenEffectiveFromWithWithdrawn` | S3, S26 |
| ″ `ListServices_FilteredByRoute_ReturnsOnlyThatRoutesServices` | S3 |
| ″ `ListServices_WithPageSizeAbove200_ReturnsInvalidPageRequest` | S44 |
| `Timetable/ListServicesSqlTests.ListServicesSql_PagesCountsAndStopCountsInSql` (V3) | S3 |
| `Network/NetworkReaderTests.GetRoute_ReturnsStationsInPositionOrderWithCurrentValues` · `GetRoute_WithUnknownId_ReturnsNull` · `GetRouteSummariesAndStations_OmitUnknownIds` · `Reads_TrackNothingAndSeeTheCallersOpenTransaction` | R28, R30; ADR-0025 items 2–3 |
| `DependencyInjectionTests.HandlerTypes_IncludesEveryHandlerDefined` (**changed**: four Timetable handlers named; 26 → **30**) | ADR-0004 |
| `DependencyInjectionTests.AddApplication_RegistersNetworkReaderAsScoped` (new) | P14 |

### `YCR.Infrastructure.Tests`

| Test | Credential | Spec |
|---|---|---|
| `Persistence/TimetableModelTests.TimetableModel_MapsTablesColumnsTypesAndKeys` (schema, `date`, `nvarchar(10)`, `bit`, `ValueGeneratedNever`, no `rowversion`, `EffectiveTo` the only concurrency token) | — | §7, R23, R36 |
| ″ `TimetableModel_HasExactlyTheDeclaredIndexes` (`Services`: PK, `IX_Services_Code_EffectiveFrom` non-unique, `IX_Services_RouteId`; `ServiceStops`: PK, `IX_ServiceStops_StationId` non-unique; nothing else) | — | P16, R35 |
| ″ `TimetableModel_ForeignKeys_AreNoActionAcrossSchemasWithoutNavigations` | — | R29, ADR-0025 |
| ″ `TimetableModel_DeclaresEveryCheckConstraint` | — | §7 |
| ″ `UpMigration_TimetableCreateServices_TouchesOnlyTheTimetableSchema` | — | ADR-0025 item 6, O3 |
| ″ `DownMigration_TimetableCreateServices_DropsTablesAndTimetableSchema` | — | V8, rollback |
| `Persistence/StationModelTests.Model_WithUtcColumn_DeclaresItsCheckConstraint` (**changed**: two rows) | — | ADR-0018 |
| `Persistence/TimetableMigrationTests.Migrate_FromF003Schema_CreatesTimetableObjectsGrantsAndSeed` (from `Security_NetworkRouteGrants` with station and route rows; no service rows; exact tables, checks, indexes, FKs with `NO_ACTION` and referenced schema `network`; the five grant rows; ten `services.%` rows; **34** in total) | migrator | `docs/21` §Data, R27 |
| ″ `Migrate_DownToF003_RemovesTimetableObjectsGrantsAndSeedRowsAndKeepsNetwork` (and forward again) | migrator | rollback |
| ″ `TimetableConstraints_RejectInvalidRows` (theory: non-UTC `CreatedAtUtc`/`WithdrawnAtUtc`; `Direction` `Sideways`; all days 0; `EffectiveTo < EffectiveFrom` without `WithdrawnAtUtc`; `Position` 0) · `TimetableConstraints_AllowAnEmptyPeriodOnlyAfterWithdrawal` | migrator | §7, R19, R41 |
| ″ `ForeignKeys_RejectAServiceOrStopNamingNoNetworkRow` | migrator | R29 |
| `Persistence/DatabasePrivilegeTests.ApplicationCredential_HasExactlyTheTimetableGrants`: **presence** `Services` `SELECT`, `INSERT`, `UPDATE(EffectiveTo)`, `UPDATE(WithdrawnAtUtc)`; `ServiceStops` `SELECT`, `INSERT`. **Absence** `DELETE` on both; table-level `UPDATE` on both; `UPDATE` of `Services.Id`, `Code`, `NameEn`, `NameMy`, `RouteId`, `Direction`, each `RunsOn…`, `EffectiveFrom`, `CreatedAtUtc`; `UPDATE` of each `ServiceStops` column; `ALTER`, `CONTROL` | `ycr_app` | **S47**, R33 |
| ″ `ApplicationCredential_CanWithdrawAServiceButNotRewriteIt` (executes: the two-column update succeeds; `UPDATE … Code`, `UPDATE ServiceStops`, `DELETE` on either are denied; rows unchanged) | `ycr_app` | **S47**, R20 |
| ″ `ApplicationCredential_CannotInsertAServiceOrStopNamingNoNetworkRow` (direct SQL; FK error 547) | `ycr_app` | **S48** |
| ″ `ApplicationCredential_CanTakeTheServiceCodeApplock` (no grant; same code waits; other code does not) | `ycr_app` | R35, V5 |
| ″ `ApplicationCredential_AttemptingDdl_IsDenied` (**changed**: `CREATE TABLE [timetable].[Smuggled]`, `ALTER TABLE [timetable].[Services] ADD …`, `DROP TABLE [timetable].[ServiceStops]`) | `ycr_app` | S47 |
| ″ `ApplicationCredential_CannotWriteRolesOrGrants` (**changed**: 24 → **34**) | `ycr_app` | OQ49 |
| `Identity/IdentitySeedTests.Seed_ProducesExactlyEightRolesAndThirtyFourGrants` (**changed and renamed** from `…TwentyFourGrants`: 24 → 34, the ten service rows added to the expected set) | migrator | OQ49 |
| ″ `Seed_MatchesDocs10GrantTables` (**changed**: also parses `## Service permission grants`, which must yield grants) | migrator | `docs/10` |
| ″ `Seed_EveryPermissionIsAPermissionsConstant` (unchanged; passes only once `ServicesManage`/`ServicesRead` exist) | migrator | OQ49 |
| `Persistence/RouteMigrationTests.Migrate_FromF002Schema_CreatesRouteTablesConstraintsIndexesAndGrants` (**changed**: total grants 24 → **34**, because it migrates to the latest migration; the `routes.%` count stays 10) | migrator | — |
| ″ `Migrate_DownToF002_RemovesRouteObjectsGrantsAndSeedRowsAndKeepsStations` (**changed**: after migrating forward again, 24 → **34**; the 14 after the rollback is unchanged; the rollback now also runs the three F-004 `Down()`s first) | migrator | rollback |
| `Persistence/ModuleInterfacesTests.ModuleInterfaces_TimetableContext_ResolvesToSameInstance` | `ycr_app` | ADR-0012 item 3 |
| `Time/LocalCalendarTests.AsiaYangon_ResolvesInThisEnvironment` (**as amended by Amendment 1**: the IANA id bound from `Time:LocalTimeZone` resolves on this host — the Windows dev machines and, in CI, the Linux `ubuntu-latest` runner; `GetUtcOffset` is +06:30 at 1 Jan and 1 Jul 00:00 local of every year 2026–2040 and at both sides of those local midnights; no adjustment rule overlapping 2026–2040 has a non-zero `DaylightDelta`; `SupportsDaylightSavingTime` is not asserted) | — | ruling Q3, V6, Amendment 1 |
| ″ `Today_AroundYangonMidnight_ReturnsTheYangonDate` (theory: `2026-09-30T17:29:59Z` → 2026-09-30; `2026-09-30T17:30:00Z` → 2026-10-01; one mid-day case) | — | R38, ruling Q3 |
| ″ `ResolveZone_WithMissingOrUnknownZone_ThrowsWithClearMessage` (theory: null, empty, whitespace, `Mars/Olympus`, `+06:30`; the message names `Time:LocalTimeZone` and the id) | — | ruling Q3 |

### `YCR.Api.Tests`

| Test | Mode | Spec |
|---|---|---|
| `Timetable/ServiceEndpointsTests.Post_WithValidRequest_Returns201WithLocationAndId` | TH | S1 |
| ″ `Get_WithKnownId_ReturnsServiceResponseRecordNotEntity` (exact JSON property set, nested `route` and `stops`, no time field) | TH | S2, R22 |
| ″ `List_ReturnsPagedEnvelopeOrderedByCodeThenEffectiveFrom` · `List_FilteredByRouteId_ReturnsOnlyThatRoute` | TH | S3 |
| ″ `Post_WithValidPatterns_Returns201` (theory: S4–S7, S10) · `Post_WithInvalidPatterns_Returns422WithErrorCode` (theory: S8, S9, S11–S16) | TH | S4–S16 |
| ″ `Post_OnInactiveRoute_Returns422RouteInactive` · `Post_WithInactiveStopStation_Returns422` · `Post_WithUnknownRoute_Returns422RouteNotFound` | TH | S17–S19 |
| ″ `DeactivateRouteOrStation_UsedByAService_Returns204AndServiceShowsInactive` (theory) | TH | S20 |
| ″ `Post_WithInvalidBody_Returns400CommonValidationFailed` (theory: every S21 item, including 201 stop ids, `"monday"`, `"1"`, a repeated day, a non-GUID `routeId`, `"2026-13-01"`) | TH | **S21**, R31 |
| ″ `Post_WithExactly200StopIds_PassesValidationAndReachesTheHandler` (→ `422 Timetable.ServiceRouteNotFound`) | TH | S21, R31 |
| ″ `Post_WithInvalidCode_Returns400InvalidServiceCode` (theory) · `Post_WithInvalidName_Returns400InvalidServiceName` | TH | S22 |
| ″ `Post_WithEffectiveToBeforeFrom_Returns400` · `Post_WithOneDayPeriod_Returns201` | TH | S23 |
| ″ `Post_WithOverlappingPeriod_Returns409` (theory: S24, S25 edge and variants, S40) · `Post_WithAdjacentPeriod_Returns201` | TH | S24, S25, S40 |
| ″ `TimetableChange_WithdrawThenCreateSameCode_Returns204Then201AndListsBoth` | TH | S26 |
| ″ `Withdraw_ReturnsExpectedStatusAndReadsBack` (theory: S30, S31, S32, S33 ×2, S34, S36) | TH | S30–S34, S36 |
| ″ `Withdraw_Twice_Returns204Then204Then422` | TH | S35 |
| ″ `UnknownIdOrInvalidWithdrawBody_Returns404Or400` (theory: GET unknown id, withdraw unknown id → `404 Timetable.ServiceNotFound`; withdraw with a missing or malformed `withdrawFrom` → `400 Common.ValidationFailed`) | TH | S38 |
| ″ `Withdraw_AfterRouteDeactivated_Returns204` | TH | S39 |
| ″ `Post_WithPastEffectiveFrom_Returns201` · `Post_WithEffectiveToInPast_Returns422` · `Post_WithEffectiveToToday_Returns201` | TH | S40, S40a |
| ″ `AnyServiceEndpoint_Anonymous_Returns401` (theory: four endpoints) | TH | S41 |
| ″ `WriteEndpoints_WithOnlyServicesRead_Return403` (theory: POST, withdraw) | TH | S42 |
| ″ `ServiceEndpoints_WithOnlyStationOrRoutePermissions_Return403` (theory: `routes.manage`, `routes.read`, `stations.manage`, `stations.read` × POST and GET) | TH | **S42**, R4 |
| ″ `List_WithPageSizeAbove200_Returns400InvalidPageRequest` | TH | S44 |
| ″ `Post_WhenRequestTriesToSupplyActorFields_RecordsTheAuthenticatedActor` | TH | S45 |
| ″ `Post_WithMyanmarName_RoundTripsThroughGet` | TH | S49 |
| ″ `Post_WithSundayAndMonday_ReadsBackMondayFirst` | TH | S52 |
| ″ `AbsentEndpoints_AreNotRouted` (theory: `PATCH`/`PUT`/`DELETE /services/{id}`, `GET /trains`, `POST /schedules/versions` → `404`/`405`) | TH | R5, R20, R22, R33 |
| `Timetable/ServiceRequestLimitTests.Limits_Are32KiBAnd1KiB` | — | R31 |
| ″ `Post_WithBodyOverLimit_Returns413WithoutInternals` (theory: create/withdraw × declared/chunked) | Kestrel | **S43** |
| ″ `Post_WithMalformedJson_Returns400WithoutInternals` (theory: create ×3, withdraw ×2) | Kestrel | **S43** |
| ″ `Post_WithLargestValidCreate_IsUnderTheLimitAndReachesTheHandler` (≤ a third of 32 KiB; `422 Timetable.ServiceRouteNotFound`) · `Withdraw_WithLargestValidBody_ReachesTheHandler` (`404 Timetable.ServiceNotFound`) | Kestrel | S43, R31 |
| `Identity/ServicePermissionGrantTests.EveryRole_HasExactlyTheSeededServiceRights` (theory over the eight roles: `GET` 200 for all; `POST` 201 only for `SystemAdministrator`, `RailwayAdministrator`, else 403; nothing written on 403) | Real | S41, S42, OQ49 |
| ″ `RailwayAdministrator_CreatesAndWithdrawsServiceAuditedAsServicesManage` | Real | S1, S30, R24 |
| `Common/LocalTimeZoneStartupTests.Startup_WithMissingOrUnknownLocalTimeZone_FailsWithClearError` (theory: key absent, empty, `Mars/Olympus`; the host throws at start and the message names `Time:LocalTimeZone`, the `SigningKeyStartupTests` pattern) · `Startup_WithAsiaYangon_Starts` | Unmodified + config | P11, ruling Q3 |
| `Common/DeployedShapeTests.ProtectedEndpoint_Anonymous_Returns401BearerChallengeWithProblemDetails` (**changed**: four service rows) | **Unmodified** | S41, `docs/21` §Tests |
| `Identity/UserAdministrationEndpointTests.ListRoles_ReturnsEightRolesWithPermissions` (**changed**: 24 → **34**) | Real | OQ49 |
| `Identity/PasswordEndpointTests.Me_ReturnsUserNameRolesAndPermissions` (**changed**: + `services.manage`, `services.read`) | Real | OQ49 |
| existing `StationEndpointsTests`, `RouteEndpointsTests`, `RouteRequestLimitTests`, `RoutePermissionGrantTests` and every F-002 suite | TH / Real / Kestrel | regression, unchanged |

### `YCR.ArchitectureTests`

| Test | Spec |
|---|---|
| `ContractsRule_WithDomainContextOrEfFixtures_DetectsViolations` (`ContractExposingNetworkDomain`, `ContractExposingNetworkContext`, `ContractExposingEntityFramework`; the rule passes on the source) | **S50**, ADR-0025 item 4 |
| `TimetableApplication_DependingOnNetworkDomainOrContext_IsDetected` (`TimetableUsingNetworkDomain`, `TimetableUsingNetworkContext`) | **S50**, R28 |
| `TimetableApplication_DependingOnNetworkContracts_IsAllowed` (`TimetableUsingNetworkContracts` is not among the failures) | R28, ADR-0012 item 4 |
| existing, unchanged, covering the ruling-Q1 move of `CodeFormat` and `BilingualName` into `YCR.Domain.Common`: `CommonKernel_WithModuleDependency_DetectsViolation`, `ApplicationAndDomainRules_ForEveryBoundedContext_HaveNoSourceViolations`, `DomainModuleBoundary_WithForeignDomainFixture_DetectsViolation`, `DomainLayerRules_WithForbiddenDependencies_DetectViolations`, `ApiRules_WithForbiddenTypes_DetectViolations`; also `AuditSnapshots_WithSourceAssemblies_AreValid` | ruling Q1; now non-vacuous for Timetable |

### `YCR.IntegrationTests`

| Test | Spec |
|---|---|
| `Worker/WorkerStartupTests.Worker_WithMissingOrUnknownLocalTimeZone_ExitsOneWithClearError` (theory: the real `YCR.Worker` process, as `BootstrapAdministratorCommandTests` runs it, with `Time__LocalTimeZone` set to empty or `Mars/Olympus`, × the `bootstrap-administrator` command and the bare host; exit code 1, stderr names `Time:LocalTimeZone`, nothing written to the database) | P11, ruling Q3 |
| existing `BootstrapAdministratorCommandTests` (unchanged; its real-process test now also proves the Worker starts with the shipped `appsettings.json` zone) | regression |

### Scenario coverage

Live scenarios: **53** (S1–S52 and S40a). Every one maps to at least one named test:

| Scenario | Tests (project) |
|---|---|
| S1 | Domain, Application, Api (TH, Real) |
| S2 | Application, Api |
| S3 | Application (×3), Api (×2) |
| S4–S7 | Domain, Application, Api (S7 also Application read-back) |
| S8, S9, S11–S16 | Domain, Application, Api |
| S10 | Domain, Application, Api |
| S17, S18, S19 | Domain (S17, S18), Application, Api |
| S20 | Application, Api |
| S21 | Api (×2) |
| S22 | Domain (×2), Application, Api (×2) |
| S23 | Domain (×2), Application (×2), Api (×2) |
| S24, S25 | Domain, Application (×2), Api (×2) |
| S26 | Domain, Application (×2), Api |
| **S27, S28** | Application (parallel), Domain (S28 periods) |
| **S29** | Application (forced ×2, unforced) |
| S30–S34 | Domain, Application, Api |
| S35 | Domain, Application, Api |
| S36 | Domain, Application (×2), Api |
| **S37** | Application (forced, both orders) |
| S38 | Application (×2), Api |
| S39 | Application, Api |
| S40, S40a | Domain, Application, Api |
| S41 | Api (TH, Unmodified, Real) |
| S42 | Api (TH ×2, Real) |
| S43 | Api (Kestrel ×3) |
| S44 | Application, Api |
| S45 | Application, Api |
| S46 | Domain, Application (every "writes nothing" case) |
| S47 | Infrastructure (×3), Application (SQL capture) |
| S48 | Infrastructure (×2) |
| S49 | Application, Api |
| S50 | Architecture (×3) |
| S51 | **Deliberately not tested for its interleaving** (spec §0.10 Q3 ruling, spec §5); its end state is "create, then deactivate", which S20's tests prove |
| S52 | Domain, Application, Api |

**Rules without a dedicated scenario:** R5 (`AbsentEndpoints_AreNotRouted`), R8/R28 (architecture tests; reflection test), R16 (S20), R18 (no observable effect in F-004; the zone is proved by R38's tests), R20 (`Service_ExposesNoMutatorOtherThanWithdraw`, grants), R22 (`ServiceStop_HasOnlyServiceIdPositionAndStationId`, `Get_…RecordNotEntity`), R23 (`TimetableModel_MapsTablesColumnsTypesAndKeys`), R24 (every "one event" assertion, run inside the transaction), R25 (no `Idempotency-Key` handling exists; nothing to test), R26 (UTC throws and constraints), R27 (`Migrate_FromF003Schema_…` asserts no service rows), R29 (model, migration and privilege FK tests), R30 (`GetService_…CurrentRouteAndStationValues`, S20), R32 (the error table through `ResultExtensions`, exercised by every API error test), R33 (grants, `AbsentEndpoints_AreNotRouted`), R34 (analysis only), R36 (SQL capture and backstop tests), R37 (domain and handler precedence tests), R38 (`LocalCalendarTests`, `CreateService_AtYangonMidnight_…`), R41/R42 (S36 tests).

### Existing tests whose counts or lists change (the F-003 V3 lesson — the complete list)

Found with `grep` over `tests/` for `24`, `14`, `26`, the route permission names and every permission list:

| # | Test (file) | Change | Why |
|---|---|---|---|
| 1 | `IdentitySeedTests.Seed_ProducesExactlyEightRolesAndTwentyFourGrants` → renamed `…ThirtyFourGrants` (`tests/YCR.Infrastructure.Tests/Identity/IdentitySeedTests.cs`) | 24 → 34; ten service rows in the expected set | The seed |
| 2 | `IdentitySeedTests.Seed_MatchesDocs10GrantTables` (same file) | The heading list gains `## Service permission grants` | `docs/10` §Service permission grants |
| 3 | `DatabasePrivilegeTests.ApplicationCredential_CannotWriteRolesOrGrants` (`tests/YCR.Infrastructure.Tests/Persistence/DatabasePrivilegeTests.cs:265`) | 24 → 34 | The seed |
| 4 | `RouteMigrationTests.Migrate_FromF002Schema_CreatesRouteTablesConstraintsIndexesAndGrants` (`…/Persistence/RouteMigrationTests.cs:186`) | total 24 → 34 | Migrates to the latest migration |
| 5 | `RouteMigrationTests.Migrate_DownToF002_RemovesRouteObjectsGrantsAndSeedRowsAndKeepsStations` (`…:233`) | 24 → 34 after migrating forward again (`:215`'s 14 unchanged) | Same |
| 6 | `UserAdministrationEndpointTests.ListRoles_ReturnsEightRolesWithPermissions` (`tests/YCR.Api.Tests/Identity/UserAdministrationEndpointTests.cs:376`) | 24 → 34 | The seed |
| 7 | `AdministrationHandlerTests.ListRoles_ReturnsEightRolesWithPermissions` (`tests/YCR.Application.Tests/Identity/AdministrationHandlerTests.cs:477, 479`) | `SystemAdministrator` list + `services.manage`, `services.read`; `ReportingUser` list + `services.read` | The seed |
| 8 | `SessionHandlerTests.Resolve_ActiveSession_ReturnsUserRolesAndPermissionUnion` (`tests/YCR.Application.Tests/Identity/SessionHandlerTests.cs:185`) | + `services.manage`, `services.read` | The seed |
| 9 | `SessionHandlerTests.GetCurrentUser_ReturnsUserNameRolesAndPermissions` (`…:259`) | + `services.manage`, `services.read` (`RailwayAdministrator`) | The seed |
| 10 | `PasswordEndpointTests.Me_ReturnsUserNameRolesAndPermissions` (`tests/YCR.Api.Tests/Identity/PasswordEndpointTests.cs:126`) | + `ServicesManage`, `ServicesRead` | The seed |
| 11 | `DependencyInjectionTests.HandlerTypes_IncludesEveryHandlerDefined` (`tests/YCR.Application.Tests/DependencyInjectionTests.cs:62`) | 26 → 30; four handlers named | Four new handlers |
| 12 | `DatabasePrivilegeTests.ApplicationCredential_AttemptingDdl_IsDenied` | + three `InlineData` rows | New schema |
| 13 | `StationModelTests.Model_WithUtcColumn_DeclaresItsCheckConstraint` | + two `InlineData` rows | New UTC columns |
| 14 | `DeployedShapeTests.ProtectedEndpoint_Anonymous_Returns401BearerChallengeWithProblemDetails` | + four `InlineData` rows | New endpoints |
| 15 | `BilingualNameTests` (moved to `Common/`) — `…WithoutErrorArgument_StillReturnsInvalidStationName` rewritten as `…WithStationError_WhenInvalid_ReturnsInvalidStationName` with **exactly the same assertions** (`IsFailure`; `Error` equals `NetworkErrors.InvalidStationName` for `("   ", "Yangon Myanmar")`); the other cases and the `AssertValidationFailure` helper only gain the explicit station error | P2, ruling Q1 |
| 16 | Call sites passing the station error explicitly (no assertion changes): `StationTests` (×3), `LedgerMigrationTests` (×2), `MigrationBundleTests`, `ModuleInterfacesTests`, `UniqueConstraintTranslationTests` | P2, ruling Q1: no assertion changes |

Items 1–11 change an exact count or list; 12–14 extend a theory; 15–16 follow the move. **Each assertion stays exact; none is loosened.** Checked and **not** affected: `IdentityMigrationTests` (counts `identity` objects only), `MigrationBundleTests.Migrate_AgainstPinnedImage_RecordsEveryMigrationAsApplied` (compares with the assembly's own list), `LedgerMigrationTests.DownMigration_ThrowsInsteadOfDroppingTheLedgerTable`, `RevocationLatencyTests`, `RoutePermissionGrantTests`, `RouteRequestLimitTests`, `CiWorkflowTests`, the `RouteMigrationTests` index and FK queries (filtered to `network` tables).

### Counts

**About 179 named tests** (a theory counts once): **163 new** — Domain 44, Application 53 (including the four `NetworkReaderTests` and the new DI test), Infrastructure 19, Api 43 (four on Kestrel, two with real tokens), Architecture 3, Integration 1; **16 existing tests changed** (the table above; items 15–16 are mechanical). The exact figure is fixed at stage 4 and reported in `progress.md`. No test is disabled, skipped or deleted.

### Whole suite

`dotnet build YCR.sln` and `dotnet test YCR.sln` green with no skipped tests; CI green on `origin` for `feature/F-004`, including `has-pending-model-changes` and the extended `api-smoke`.

---

## New packages

**None** (P23). EF Core, FluentValidation, ASP.NET Core, ArchUnitNET, Testcontainers and xUnit v3 are already pinned in `Directory.Packages.props`; `TimeZoneInfo` and `DateOnly` are in the BCL.

---

## Risks

| # | Risk | Likelihood | Mitigation |
|---|---|---|---|
| R-1 | The shared-kernel move ripples wider than listed, or changes the model snapshot | Low | It is compile-driven; step 1 is on its own and ends green before any Timetable code. V1 checks the snapshot; fallback in V1. |
| R-2 | The code lock times out or deadlocks under load → `500` | Low | One applock per transaction, taken before any `timetable` read; the lock covers one indexed read and one write; different codes never wait. Timeout 30 s, as F-002. |
| R-3 | The seed's count change breaks tests pinned at 24 | Certain, planned | All eleven are listed above and changed in step 5, each exact. |
| R-4 | A host without ICU (Windows) or tzdata (Linux; for example the plain `runtime-deps:10.0-noble-chiseled` image, checked 2026-09-25) cannot resolve `Asia/Yangon` | Low today (CI runs on the `ubuntu-latest` runner; the standard .NET 10 images carry `tzdata`), real for a future container image | The API and the Worker refuse to start with a clear error (fail closed; `LocalTimeZoneStartupTests`, `WorkerStartupTests`); `AsiaYangon_ResolvesInThisEnvironment` fails by name wherever the tests run; stage 8 records the image facts in `docs/15` for whoever writes the container image. Never fall back to a fixed offset. |
| R-5 | Framework `413`/`400` (malformed JSON, a malformed `routeId` query) carry no `errorCode` | Known | Same as F-003 R-6; T-042 owns the cross-cutting decision. V7 records the query case. |
| R-6 | A handler starts deciding pattern rules itself | Medium | P4/P13 put every rule in the domain with an oracle test; stage 6 checks that handlers only load facts and map results. |
| R-7 | EF writes more than the two granted columns on withdrawal, or touches `ServiceStops` | Medium | V2 and the SQL capture test fail under `ycr_app` first; the fix is the mapping, never the grant. |
| R-8 | Forced-order concurrency tests are flaky | Low | The order is fixed by the gate, not by timing; the one timing assertion ("still waiting after 500 ms") can only fail when the lock is missing. Unforced tests assert outcome counts only (F-003 R-9). |
| R-9 | `Seed_MatchesDocs10GrantTables` depends on `docs/10`'s format | Low | The service section uses the route format; a reformatted section fails by name. |
| R-10 | The provisional rulings (OQ42–OQ50) change when Myanma Railways answers | Medium | Each lives in one place: `ServiceCode`/`CodeFormat` (OQ43), `Service.Create` (OQ44–OQ46), `OperatingDays` (OQ47), `EffectivePeriod`/`Service.Withdraw` (OQ48, OQ50), the seed (OQ49). |
| R-11 | A list page costs an extra round trip for route codes | Low | One `GetRouteSummariesAsync` per page (≤ 200 ids, one JSON parameter). |

---

## Rollback and forward compatibility

**Migrations.** All three have working `Down()` methods (§DB changes): the grants are revoked, the ten seed rows deleted, the two tables and the `timetable` schema dropped; `network` is untouched. Once real services exist, rolling back the schema destroys service history, so production recovery is a restore (as F-002/F-003). The audit ledger only gains rows.

**Code.** The shared-kernel move is source-only (namespaces); no stored value, column or API changes.

**API.** Additive: four new endpoints; no existing contract changes. No client exists yet (no SPA, OQ22).

**Partially deployed instances.** Migrations run first, as before. New code on an old database: the service endpoints fail with `500` (missing tables) and everything else works; the API and the Worker also need the new `Time:LocalTimeZone` setting, which ships in each host's `appsettings.json` with the code; a deployment that overrides configuration without it, or runs on a host without time-zone data, fails at startup with a clear error instead of running with a wrong date (ruling Q3). Old code on a new database: unaffected; the new tables, grants and permissions are unused.

**Forward compatibility.** `PayloadVersion` 1. Stable service ids and no deletes keep OQ4's service-bound-ticket option open (R34). `IX_ServiceStops_StationId` serves a later "services at station X" read. FR-004 can reference services by id from its own tables. A later feature that needs a reverse dependency (Network calling Timetable) must say so and get a ruling (ADR-0025 Follow-up).

---

## Steps

Each step ends with `dotnet test YCR.sln` green. No step leaves the branch red.

| # | Step | Ends green with |
|---|---|---|
| 1 | **Shared kernel (P2, ruling Q1):** move `NetworkCodeFormat` → `Common/CodeFormat` and `BilingualName` → `Common/`; `StationCode`, `RouteCode`, `CreateStationHandler` and the test call sites follow; `BilingualNameTests` moves. **V1** | Every existing test, with items 15–16 of the changed-tests table; `has-pending-model-changes` clean; every existing assertion unchanged and the ruling-Q1 architecture tests green |
| 2 | **Timetable domain:** `ServiceCode`, `Direction`, `OperatingDays`, `EffectivePeriod`, `ServiceRouteFacts`, `ServiceStop`, `Service`, `TimetableErrors` | Every `YCR.Domain.Tests` row above, including the walking-simulation oracle |
| 3 | **Network contract and architecture rules:** `INetworkReader` and its records, `NetworkReader`, DI; `ContractsMustNotDependOnModuleDomainOrContext`; the Contracts and Timetable fixtures | The three architecture tests, `NetworkReaderTests`, `AddApplication_RegistersNetworkReaderAsScoped`; existing architecture tests unchanged |
| 4 | **Persistence:** `Permissions.ServicesManage`/`ServicesRead`; `ITimetableDbContext`; `ServiceConfiguration`, `ServiceStopConfiguration`; `YcrDbContext`; DI; migration `Timetable_CreateServices` (with `DropSchema` in `Down`, V8). **V4** | `TimetableModelTests`, the two UTC rows, `TimetableConstraints_*` and `ForeignKeys_*` (migrator), `ModuleInterfaces_TimetableContext_…`; all existing tests |
| 5 | **Seed:** migration `Identity_SeedServicePermissionGrants`; changed-tests items 1–10 | The seed tests; every count/list test at its new exact value |
| 6 | **Grants:** migration `Security_TimetableGrants`; the new `DatabasePrivilegeTests` cases (except the applock one) and the DDL rows; `TimetableMigrationTests` upgrade and down | `ApplicationCredential_HasExactlyTheTimetableGrants`, `…CanWithdrawAServiceButNotRewriteIt`, `…CannotInsertAServiceOrStopNamingNoNetworkRow`, `Migrate_FromF003Schema_…`, `Migrate_DownToF003_…` |
| 7 | **Application:** `ILocalCalendar` + `LocalCalendar` + `LocalTimeOptions.ResolveZone` (ruling Q3); `IServiceCodeLock` + `SqlServerServiceCodeLock`; audit subjects, actions and snapshot; `CreateService`, `WithdrawService`, `GetService`, `ListServices` with DTOs; DI test 26 → 30. **V2, V3, V5, V6** | Every `YCR.Application.Tests` row, including the parallel and forced S27–S29 and S37 tests; `ApplicationCredential_CanTakeTheServiceCodeApplock`; `LocalCalendarTests` (resolves here, midnight crossing, clear error) |
| 8 | **API:** `ServiceContracts` (+ validators, P6, the 200 cap), `ServiceEndpoints` (+ limits, P20), `Program.cs` (mapping, options validated at start), `appsettings.json`; **Worker:** `Program.cs` zone check before both start paths, `appsettings.json` (ruling Q3). **V7** | Every `YCR.Api.Tests` row, including `DeployedShapeTests` (Unmodified), `ServiceRequestLimitTests` (Kestrel), `ServicePermissionGrantTests` (Real), `LocalTimeZoneStartupTests`; `WorkerStartupTests` and the unchanged `BootstrapAdministratorCommandTests` (Integration); all F-001/F-002/F-003 API suites |
| 9 | **`YCR.Api.http` and CI:** the Services section; the `api-smoke` checks. Push `feature/F-004` and **prove a green GitHub Actions run** (build, tests, `has-pending-model-changes`, `api-smoke`, gitleaks; trunk-only filter untouched). Record the run URL in `progress.md` — the `api-smoke` job also starts the built Worker (bootstrap) and the API with their shipped `appsettings.json`, so it proves the zone check passes on the Linux runner. **Amendment 2:** the API starts from its build output directory, with no `Time__` override, and the smoke asserts its content root | **A green GitHub Actions run on `origin` for `feature/F-004`** |

### Stage 8 documentation (not stage 4)

- **`docs/07`:** a new "F-004 timetable tables, constraints, indexes and grants" section — both tables, every object above, both cross-schema `NO ACTION` keys, the two convention indexes and why they are declared, the index table, the three migration names, the `Security_TimetableGrants` table with its absences, and the code lock (`sp_getapplock`, no grant, like F-002's). §Core tables: `Trains` marked not used in Phase 1 (OQ42), `TrainServices` → `Services`. §Module schemas: the ADR-0025 paragraph on cross-schema foreign keys (referencing module's migration, primary keys of never-deleted rows only, `NO ACTION`).
- **`docs/08`:** "Implemented in F-004 — services" (endpoints, contracts, error codes, precedence, the 32 KiB / 1 KiB limits). §Initial resources: remove `GET/POST/PATCH /trains` (OQ42) and `PATCH /services` (OQ48); `/schedules/*` stays for FR-004.
- **`docs/20`:** §1 a "Cross-module reads" row (ADR-0025 follow-up: contract in `<Module>/Contracts/`, `internal` implementation in `YCR.Application.<Module>`, registered in `AddApplication`, the Contracts architecture rule). **A rule stating when a handler may open its own transaction** (ruling Q2): only to guard a set invariant across rows that one unique index cannot enforce, with an application lock scoped to the smallest key (for example one service code), one `SaveChangesAsync`, then commit — citing ADR-0026 (Proposed). The `Time:LocalTimeZone` setting (IANA id, both hosts, validated at startup) and `ILocalCalendar.Today()` as the only source of a local date (ruling Q3).
- **`docs/10`:** no content change expected; record the seed migration's name in §Service permission grants.
- **`docs/15`:** the API and the Worker need IANA time-zone data for `Asia/Yangon` and refuse to start without it (ruling Q3): ICU on Windows; `tzdata` on Linux. Record the 2026-09-25 image facts (§Facts): `mcr.microsoft.com/dotnet/runtime-deps:10.0` and the images built on it carry `tzdata`; the plain `…-noble-chiseled` variant does **not**, `…-noble-chiseled-extra` does. CI runs on the `ubuntu-latest` runner, not in these images (R-4).
- **`docs/15` and `docs/local-development.md` (Amendment 2, hein, 2026-09-25, T-046 G2):** the API and the Worker run with their output directory as content root, so each reads its shipped `appsettings.json` (and `Time:LocalTimeZone`); started from elsewhere the API refuses to start.
- **ADR-0026 "Application locks for set invariants"** (ruling Q2): drafted as **Proposed** at stage 8 in `docs/decisions/`, with its row in `docs/decisions/README.md`. It cites ADR-0004 (single save; explicit transaction only for several saves), F-002 plan P14 (the SystemAdministrator lock) and F-004 spec R35 (the service-code lock), and states the pattern: a set invariant across rows that one unique index cannot enforce is guarded by a transaction-owned `sp_getapplock` scoped to the smallest key, taken before the reads that decide, with one save and a commit. hein accepts it at stage 9.
- **`docs/glossary.md`:** *Service* rewritten (C7), *Service code*, *Direction* (`Forward`/`Reverse` relative to route order; wrap; full circuit), *ServiceStop / stop position* (1-based within the service; not the route position), *Operating days*, *Effective period* (`EffectiveFrom`, inclusive `EffectiveTo`, open-ended) and *Withdrawal* (from a date D; `EffectiveTo` = D − 1; never runs). Every Myanmar term stays **OPEN QUESTION**.
- **`docs/features/F-004-service-management/spec.md`:** no change expected; any change is a recorded amendment.
- **README:** no change (it lists no endpoints). `docs/19`: no change (no new OQ).

---

## Stop point

**Passed 2026-09-25.** The plan is **Approved (hein, 2026-09-25)**, revision 2, with plan Q1–Q3 ruled (§Rulings on the plan questions). T-045 is done. Stage 4 is T-046, a separate task, and is not started by T-045.

---

## Review history

**Amendments 1 and 2 — 2026-09-25, hein (T-046 G1, G2), recorded by claude.** Engineering rulings on two gaps found at the end of step 8: the zone test asserts behaviour over 2026–2040 rather than the absence of adjustment rules (Amendment 1), and `api-smoke` starts the API from its build output directory with no `Time__` override (Amendment 2). §Amendments during implementation.

**Revision 2 — 2026-09-25, hein: approved.** Rulings on plan Q1–Q3 (all ENGINEERING DECISIONS): **Q1** move `BilingualName` and the code-format rule to `YCR.Domain.Common`, callers pass their module's error, station and route behaviour and every existing F-001/F-003 assertion unchanged, the one rewritten test asserts exactly what it did, the existing architecture tests named (§Rulings, P2, §Test plan). **Q2** `sp_getapplock` inside an explicit transaction accepted as the second use of the F-002 P14 exception; stage 8 adds the `docs/20` rule and ADR-0026 "Application locks for set invariants" as Proposed (P10, §Stage 8). **Q3** `Time:LocalTimeZone` = `"Asia/Yangon"` and `ILocalCalendar.Today()` accepted; both the Api and the Worker validate the zone at startup; the Worker added to the affected files; tests for the zone resolving here, startup failure (Api and Worker) and the 17:29:59Z/17:30:00Z midnight; CI and .NET-image tzdata facts recorded (P11, §Facts, §Test plan, R-4, steps 7–9, §Stage 8). The Questions section is replaced by §Rulings on the plan questions. P1–P23 approved as amended.

**Revision 1 — 2026-09-25, claude (T-045).** First draft, against the approved spec at `c2ea4f0`.

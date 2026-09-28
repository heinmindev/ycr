# Database Design Proposal

## Core tables

- Stations
- Routes
- RouteStations
- ~~Trains~~ — not used in Phase 1 (OQ42 provisional ruling, hein, 2026-09-25: there is no `Train` concept; use case 3 is met by services)
- Services (`timetable.Services`; formerly listed as `TrainServices`, renamed by F-004 E2 to the aggregate's plural, `docs/20` §2)
- ServiceStops
- ScheduleVersions (`timetable.ScheduleVersions`, with its child tables `timetable.ScheduleVersionServices` and `timetable.ScheduleStopTimes`; implemented by F-005, §F-005 below)
- FareRuleSets
- FareRules
- PassengerCategories
- Tickets
- TicketValidations
- Payments
- Refunds
- CashierSessions
- Sales
- audit.AuditEvents (SQL Server ledger table)
- IdempotencyRecords

## Rules

- Use foreign keys.
- Use unique constraints for business identifiers.
- Use `datetimeoffset(3)` for instants, storing UTC values for `*Utc` columns; use `date` for calendar dates.
- Preserve historical fare/schedule versions.
- Never overwrite financial history.
- Add indexes based on measured query patterns.

## Module schemas and approved cross-cutting tables

**ENGINEERING DECISION (tech lead, cite ADR-0012):** operational tables use one schema per module (`network`, `timetable`, `fare`, `ticketing`, `payments`, `operations`, `identity`, `audit`). Reporting reads through read-only views/projections.

**ENGINEERING DECISION (tech lead, cite ADR-0025):** a table in one module's schema may carry a
foreign key to another module's table only under these conditions: it references that table's
**primary key** (never an alternate key or unique index, ADR-0025 item 6); the referenced rows are
**never deleted** (`ycr_app` holds no `DELETE` on the referenced table); the key is **`NO ACTION`**;
it is configured in `YCR.Infrastructure` with **no navigation property** on either side; and it is
created by the **referencing** module's migration. Why: the database is then the authority that a
stored cross-module identifier names a real row, whoever wrote it (AGENTS.md rule 5), at no cost
on delete, because the target is never deleted. The referencing handler still checks existence and
state through the owning module's read-only contract first, to return a stable error code; the key
is the backstop (ADR-0025 item 7). The cost is coupling: dropping or re-keying a referenced table,
or extracting a module into its own database, needs these keys removed first. The existing
cross-schema keys are `timetable.Services.RouteId` → `network.Routes(Id)` and
`timetable.ServiceStops.StationId` → `network.Stations(Id)` (§F-004 below). The dependency is
one-way: `network` has no key into `timetable`, and Network never calls Timetable (OQ46 ruling).

**ENGINEERING DECISION (tech lead, cite ADR-0017):** `audit.AuditEvents` is append-only ledger-backed; it is not the former generic `AuditLogs` table.

**ENGINEERING DECISION (tech lead, cite ADR-0019):** Sale, Payment, ticket-cancellation, and Refund records own their `BusinessDate` columns, with nullable `CashierSessionId` where session ownership applies. The operation and date are written atomically; there is no polymorphic `BusinessDateAssignments` table.

Exact columns must be designed after requirements discovery.

## F-001 tables, constraints and indexes

The first two tables the walking skeleton creates. Both are applied by the EF migration bundle
under the `ycr_migrator` credential; the application login has no DDL rights (spec E7, ADR-0017
item 3).

### `network.Stations`

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | no | PK, clustered, application-assigned through `IIdGenerator` (ADR-0006) |
| `Code` | `nvarchar(10)` | no | Human-facing station code |
| `NameEn` | `nvarchar(100)` | no | Owned value object `BilingualName` |
| `NameMy` | `nvarchar(100)` | no | Myanmar Unicode, never Zawgyi (`docs/20` §6; OQ29 covers input handling) |
| `IsActive` | `bit` | no | EF concurrency token, so two concurrent deactivations cannot both win |
| `CreatedAtUtc` | `datetimeoffset(3)` | no | UTC value (ADR-0018) |

| Object | Definition | Why it is there |
|---|---|---|
| `PK_Stations` | clustered on `Id` | Sequential GUIDs keep it from fragmenting (ADR-0006 REQUIRED CONTROL, proved by the trunk-only fragmentation test) |
| `UX_Stations_Code` | unique on `Code` | The concurrency authority for duplicate codes: the pre-check can lose a race, this cannot. Also what makes "codes are never reused" true, because deactivated rows are retained. `CreateStationHandler` matches this exact name to map the violation to `409` |
| `CK_Stations_CreatedAtUtc_Utc` | `DATEPART(TZOFFSET, [CreatedAtUtc]) = 0` | ADR-0018 requires `*Utc` columns to hold UTC. The domain factory rejects a non-zero offset, and this binds every other writer of the column, including direct SQL under the application credential |

Rows are never deleted. Deactivation sets `IsActive = 0` and the row stays, because the unique
index on a retained row is what enforces non-reuse of the code.

### `audit.AuditEvents`

An append-only SQL Server 2022 **ledger** table created by raw SQL in
`Audit_CreateAuditEventsLedger`, not by EF model building (ADR-0017 item 1). **ADR-0021 is
authoritative for its fourteen columns.** EF maps it with `ExcludeFromMigrations()` so it can
INSERT but can never emit DDL that converts or drops it (ADR-0017 item 6).

| Object | Definition | Why it is there |
|---|---|---|
| `PK_AuditEvents` | on `Id` | Application-assigned (ADR-0006) |
| `CK_AuditEvents_BeforeJson` | `BeforeJson IS NULL OR ISJSON(BeforeJson) = 1` | A malformed payload can never be repaired on an append-only table, so it must be refused on the way in (ADR-0021 item 5) |
| `CK_AuditEvents_AfterJson` | `AfterJson IS NULL OR ISJSON(AfterJson) = 1` | ″ |
| `CK_AuditEvents_ActorRole` | `ActorRole IS NULL OR ISJSON(ActorRole) = 1` | ″. `ISJSON` proves the text is JSON, not that it is an array; `IAuditWriter` owns that, with a test |
| `CK_AuditEvents_OccurredAtUtc_Utc` | `DATEPART(TZOFFSET, [OccurredAtUtc]) = 0` | The counterpart of the `Stations` check. It matters more here: `OccurredAtUtc` orders tamper-evident history and the row cannot be corrected afterwards |
| `IX_AuditEvents_Subject` | nonclustered on `(SubjectType, SubjectId)` | "What happened to this thing?" — the investigation path ADR-0021 names |
| `IX_AuditEvents_OccurredAtUtc` | nonclustered on `OccurredAtUtc` | "What happened in this window?" — the other investigation path |

All four `audit.AuditEvents` check constraints are created **with the table**, because
retro-fitting one onto a populated append-only ledger is materially harder (ADR-0021 item 5). `Down()` throws rather than
dropping the table (plan P7).

Both UTC check constraints are declared in the EF model with `HasCheckConstraint` as well as in
their migrations, and CI runs `dotnet ef migrations has-pending-model-changes`, so a constraint
removed from either side fails the build.

## F-001 station persistence controls

The `network.Stations.CreatedAtUtc` column is `datetimeoffset(3)` and carries
`CK_Stations_CreatedAtUtc_Utc`, which rejects any stored value whose offset is not zero. This is
the database counterpart to the domain factory's UTC guard required by ADR-0018.

The EF design-time factory is `src/YCR.Infrastructure/Persistence/YcrDbContextFactory.cs`. It is
used only by EF tooling and reads the required `YCR_DESIGN_TIME_CONNECTION` environment variable;
it has no fallback connection, so a design-time database update cannot silently target a developer's
LocalDB. **Set `YCR_DESIGN_TIME_CONNECTION` before running `dotnet ef migrations add` or any other
EF design-time command; there is no default and the command fails without it.** The test fixture
supplies an inert placeholder to the `dotnet ef` process it spawns when the variable is absent, so
`dotnet test YCR.sln` needs no setup, and it never replaces a value that is already set.
`src/YCR.Domain/Properties/AssemblyInfo.cs` grants `YCR.Infrastructure` access to the
domain's internal EF materialisation path (`StationCode.From`).

## F-002 identity tables, constraints, indexes and grants

Created by three migrations applied under `ycr_migrator` (F-002 spec §7; ADR-0023 item 1):
`Identity_CreateIdentitySchema` (the six tables), `Identity_SeedRolesAndPermissionGrants` (the role
catalogue and its grants) and `Security_IdentityGrants` (what `ycr_app` may do). Every foreign key
in `identity` is `NO ACTION`; nothing cascades in the database. Every `*Utc` column is
`datetimeoffset(3)` and carries a `CK_<Table>_<Column>_Utc` check (`DATEPART(TZOFFSET, …) = 0`,
ADR-0018; a null passes). The check constraints are declared in the EF model as well as in the
migration, so `has-pending-model-changes` sees drift.

### `identity.Users`

Staff accounts, managed through ASP.NET Core Identity's `AddIdentityCore` (ADR-0023 item 1).

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | no | PK, application-assigned (ADR-0006) |
| `UserName` | `nvarchar(50)` | no | 3–50 characters of `a-z`, `0-9` and `.` (spec R20) |
| `NormalizedUserName` | `nvarchar(50)` | no | Unique; the sign-in lookup key |
| `PasswordHash` | `nvarchar(256)` | no | ASP.NET Core Identity V3 hash; never the password |
| `SecurityStamp` | `nvarchar(64)` | no | ASP.NET Core Identity's security stamp |
| `IsDisabled` | `bit` | no | |
| `DisabledAtUtc` | `datetimeoffset(3)` | yes | Set exactly when `IsDisabled = 1` |
| `LockoutEndUtc` | `datetimeoffset(3)` | yes | End of a 15-minute lockout (ADR-0023 item 3) |
| `AccessFailedCount` | `int` | no | Consecutive failed sign-ins, 0–10 |
| `PasswordChangedAtUtc` | `datetimeoffset(3)` | no | |
| `MustChangePassword` | `bit` | no | Set for the bootstrap account and for every password an administrator sets (ADR-0023 item 9, U2) |
| `CreatedAtUtc` | `datetimeoffset(3)` | no | |
| `RowVersion` | `rowversion` | no | Concurrency token: administrator edits against sign-in counters |

| Object | Definition |
|---|---|
| `PK_Users` | on `Id` |
| `UX_Users_NormalizedUserName` | unique on `NormalizedUserName`; `CreateUserHandler` and the bootstrap match this name to map a race to `409 Identity.UserNameAlreadyExists` |
| `CK_Users_UserName_Format` | `LEN([UserName]) BETWEEN 3 AND 50` and no character outside `a-z0-9.` under the binary collation `Latin1_General_100_BIN2`, so upper case is refused whatever the database collation |
| `CK_Users_Disabled_Consistent` | `IsDisabled = 1` exactly when `DisabledAtUtc IS NOT NULL` |
| `CK_Users_AccessFailedCount` | `AccessFailedCount BETWEEN 0 AND 10` |
| `CK_Users_DisabledAtUtc_Utc`, `CK_Users_LockoutEndUtc_Utc`, `CK_Users_PasswordChangedAtUtc_Utc`, `CK_Users_CreatedAtUtc_Utc` | UTC checks |

### `identity.Roles` and `identity.RolePermissions`

The role catalogue and the role→permission grants, as data (spec D8, R11). Seeded by
`Identity_SeedRolesAndPermissionGrants` with fixed ids: the eight roles of the glossary's
§Staff roles and fourteen grants (`stations.manage` → `SystemAdministrator`,
`RailwayAdministrator`; `stations.read` → all eight; `users.read`, `users.manage`,
`users.roles.manage`, `auth-sessions.revoke` → `SystemAdministrator`), exactly `docs/10`'s tables.
`IdentitySeedTests` keeps the seed and `docs/10` in step. The application cannot write either
table; grants change only by a reviewed migration.

| Table | Columns | Objects |
|---|---|---|
| `Roles` | `Id uniqueidentifier` PK; `Name nvarchar(50)` | `UX_Roles_Name` unique on `Name` |
| `RolePermissions` | `RoleId uniqueidentifier`, `Permission nvarchar(100)`; PK `(RoleId, Permission)` | `FK_RolePermissions_Roles_RoleId`; `CK_RolePermissions_Permission_Format`: no character outside `a-z`, `.` and `-` (binary collation) |

### `identity.UserRoles`

A user may hold several roles (spec D7). PK `(UserId, RoleId)`; `FK_UserRoles_Users_UserId`,
`FK_UserRoles_Roles_RoleId`; `IX_UserRoles_RoleId` for the last-administrator count (R27). A role
change deletes and inserts rows; the history is in the `Identity.RolesChanged` audit rows.

### `identity.AuthSessions`

One row per sign-in; the row is the refresh-token family (ADR-0023 item 8, clarification C5).

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | no | PK; the access token's `sid` |
| `UserId` | `uniqueidentifier` | no | `FK_AuthSessions_Users_UserId`; `IX_AuthSessions_UserId` |
| `CreatedAtUtc` | `datetimeoffset(3)` | no | |
| `ExpiresAtUtc` | `datetimeoffset(3)` | no | `CreatedAtUtc` + 12 hours, absolute (ADR-0023 item 8) |
| `RevokedAtUtc` | `datetimeoffset(3)` | yes | Concurrency token: of two racing revocations, one wins and one audit row is written |
| `RevocationReason` | `nvarchar(40)` | yes | `Logout`, `PasswordChanged`, `AdministratorPasswordReset`, `UserDisabled`, `AdministratorRevoked` or `FamilyReuse` |

Checks: `CK_AuthSessions_Revocation_Consistent` (both revocation columns null or both set),
`CK_AuthSessions_RevocationReason` (the six values), `CK_AuthSessions_Expiry`
(`ExpiresAtUtc > CreatedAtUtc`), and the three UTC checks.

### `identity.RefreshTokens`

Hashes only: the raw token is 32 random bytes as base64url, and the row stores SHA-256 of it
(ADR-0016; spec R5, S31).

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | no | PK |
| `SessionId` | `uniqueidentifier` | no | `FK_RefreshTokens_AuthSessions_SessionId`; `IX_RefreshTokens_SessionId` |
| `TokenHash` | `binary(32)` | no | `UX_RefreshTokens_TokenHash` unique |
| `IssuedAtUtc` | `datetimeoffset(3)` | no | |
| `RotatedAtUtc` | `datetimeoffset(3)` | yes | |
| `ReplacedByTokenId` | `uniqueidentifier` | yes | `FK_RefreshTokens_RefreshTokens_ReplacedByTokenId`; concurrency token |

`UX_RefreshTokens_ReplacedByTokenId` (unique, filtered `WHERE [ReplacedByTokenId] IS NOT NULL`)
is the database authority that a token has at most one successor, so of two concurrent refreshes
exactly one rotates (spec R6, S33). Checks: `CK_RefreshTokens_Rotation_Consistent` (both rotation
columns null or both set), `CK_RefreshTokens_NotSelf` (`ReplacedByTokenId <> Id`), and the two
UTC checks.

### Grants to `ycr_app` (`Security_IdentityGrants`)

| Table | Granted | Deliberately absent |
|---|---|---|
| `Users` | `SELECT`, `INSERT`; `UPDATE` of `PasswordHash`, `SecurityStamp`, `PasswordChangedAtUtc`, `MustChangePassword`, `IsDisabled`, `DisabledAtUtc`, `LockoutEndUtc`, `AccessFailedCount` only | `DELETE`; `UPDATE` of `Id`, `UserName`, `NormalizedUserName`, `CreatedAtUtc` (no endpoint renames a user) |
| `Roles`, `RolePermissions` | `SELECT` | every write |
| `UserRoles` | `SELECT`, `INSERT`, `DELETE` | `UPDATE`. This is the only `DELETE` in `identity` |
| `AuthSessions` | `SELECT`, `INSERT`; `UPDATE` of `RevokedAtUtc`, `RevocationReason` only | `DELETE` |
| `RefreshTokens` | `SELECT`, `INSERT`; `UPDATE` of `RotatedAtUtc`, `ReplacedByTokenId` only | `DELETE` |

No DDL is granted anywhere. The last-administrator lock uses `sp_getapplock`, which `public` may
already execute. `DatabasePrivilegeTests` asserts both the grants and the absences. Sessions and
refresh tokens are never deleted in F-002; a retention job is a before-production follow-up
(T-026).

## F-003 route tables, constraints, indexes and grants

Created by three migrations applied in this order under `ycr_migrator` (F-003 spec §7, plan
§DB changes): `20260924145627_Network_CreateRoutes` (both tables, EF model-built),
`20260924150657_Identity_SeedRoutePermissionGrants` (the ten `routes.manage`/`routes.read` role
grants in `docs/10` §Route permission grants) and `20260924152836_Security_NetworkRouteGrants`
(what `ycr_app` may do). All three are additive: `network.Stations` and every existing column are
unchanged. Both foreign keys are `NO ACTION`. Both `*Utc` columns are `datetimeoffset(3)` with a
`CK_<Table>_<Column>_Utc` check, and every check constraint is declared in the EF model as well as
the migration, so `has-pending-model-changes` sees drift. No route data is seeded (OQ1; spec R23).

**BUSINESS DECISION — provisional tech-lead rulings (hein, 2026-09-24; T-032, OQ37, OQ38, OQ41) —
not a Myanma Railways answer:** a route's sequence, `IsClosed`, code and names never change after
creation, a station appears at most once in a route, and a route can only be deactivated. The
constraints and grants below are the database statement of those rulings.

### `network.Routes`

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | no | PK, clustered, application-assigned through `IIdGenerator` (ADR-0006) |
| `Code` | `nvarchar(10)` | no | 2–10 characters of `A`–`Z` and `0`–`9`, the station-code rule (OQ41); unique across all routes, never reused |
| `NameEn` | `nvarchar(100)` | no | Owned value object `BilingualName`; required, 1–100 characters after trimming, not unique |
| `NameMy` | `nvarchar(100)` | no | Myanmar Unicode, never Zawgyi (`docs/20` §6); same rules as `NameEn` |
| `IsClosed` | `bit` | no | Closed (`1`) or open (`0`), fixed at creation (OQ37). A closed route runs from its last station back to its first; the first station is never repeated |
| `IsActive` | `bit` | no | EF concurrency token, so of two concurrent deactivations exactly one wins |
| `CreatedAtUtc` | `datetimeoffset(3)` | no | UTC value (ADR-0018) |
| `DeactivatedAtUtc` | `datetimeoffset(3)` | yes | Null while active; set to the clock's UTC now by the same `UPDATE` that sets `IsActive = 0` |

There is no `rowversion` and no direction column (OQ36: direction belongs to services and fares).

| Object | Definition | Why it is there |
|---|---|---|
| `PK_Routes` | clustered on `Id` | ADR-0006 |
| `UX_Routes_Code` | unique on `Code` | The concurrency authority for duplicate codes, inactive routes included, so a deactivated route's code is never reused. `CreateRouteHandler` matches this exact name to map the violation to `409 Network.RouteCodeAlreadyExists` |
| `CK_Routes_CreatedAtUtc_Utc` | `DATEPART(TZOFFSET, [CreatedAtUtc]) = 0` | ADR-0018 |
| `CK_Routes_DeactivatedAtUtc_Utc` | `[DeactivatedAtUtc] IS NULL OR DATEPART(TZOFFSET, [DeactivatedAtUtc]) = 0` | ADR-0018 |

Rows are never deleted. Because sequences are immutable, the rows are the route history:
`CreatedAtUtc` and `DeactivatedAtUtc` answer which routes were active on a date without reading the
audit ledger.

### `network.RouteStations`

| Column | Type | Null | Notes |
|---|---|---|---|
| `RouteId` | `uniqueidentifier` | no | `FK_RouteStations_Routes_RouteId` → `network.Routes(Id)`, `NO ACTION` |
| `Position` | `int` | no | 1-based and contiguous (`1..n`) within the route. Not the ADR-0014 station index and not the station code |
| `StationId` | `uniqueidentifier` | no | `FK_RouteStations_Stations_StationId` → `network.Stations(Id)`, `NO ACTION` |

| Object | Definition | Why it is there |
|---|---|---|
| `PK_RouteStations` | clustered on `(RouteId, Position)` | One station per position. Contiguity and the minimum length (2 open, 3 closed) are enforced by the `Route` aggregate, because a check constraint cannot see other rows |
| `CK_RouteStations_Position` | `[Position] >= 1` | Rejects zero and negative positions from any writer |
| `UX_RouteStations_StationId_RouteId` | unique on `(StationId, RouteId)` | The database authority that a station appears at most once in one route (OQ37). `Route.Create` refuses a repeat first; `CreateRouteHandler` keeps a catch for this exact name as defence in depth. `StationId` leads, so the index also covers the station foreign key |
| `FK_RouteStations_Routes_RouteId` | → `network.Routes(Id)`, `NO ACTION` | EF's default for this required relationship is `Cascade`, so `NO ACTION` is set explicitly and pinned by `RouteModelTests` |
| `FK_RouteStations_Stations_StationId` | → `network.Stations(Id)`, `NO ACTION` | A sequence never names a missing station |

**Why there is no `IX_RouteStations_StationId`** (spec Amendment 1, hein, 2026-09-24, T-034 Q1).
No F-003 query or write filters `RouteStations` by `StationId` alone: route reads and the station
count use `PK_RouteStations` as a range on `RouteId`, and inserts validate their foreign keys through
`PK_Routes` and `PK_Stations`. The reverse check on `DELETE FROM network.Stations` never runs,
because `ycr_app` has no `DELETE` there and stations are never deleted. The index was designed for a
station-deactivation check that the OQ39 ruling removed. EF Core adds an index by convention for
every foreign key that no other index leads with, and adds it back if a migration drops it; making
`StationId` the leading column of the unique index covers the foreign key, so EF adds nothing. A
later "routes through station X" query can use the unique index.

### Grants to `ycr_app` (`Security_NetworkRouteGrants`)

| Table | Granted | Deliberately absent |
|---|---|---|
| `Routes` | `SELECT`, `INSERT`; `UPDATE` of `IsActive`, `DeactivatedAtUtc` only | `DELETE`; `UPDATE` of `Id`, `Code`, `NameEn`, `NameMy`, `IsClosed`, `CreatedAtUtc` |
| `RouteStations` | `SELECT`, `INSERT` | `UPDATE`; `DELETE` |

No DDL is granted. `RouteStations` is insert-only, so the database, not only the application,
guarantees that a stored sequence never changes. `DatabasePrivilegeTests` asserts both the grants
and the absences. `Down()` revokes exactly these grants; widening them needs a new migration, a
spec change and a review.

## F-004 timetable tables, constraints, indexes and grants

Created by three migrations applied in this order under `ycr_migrator` (F-004 spec §7, plan
§DB changes): `20260925065623_Timetable_CreateServices` (the `timetable` schema and both tables, EF
model-built), `20260925071216_Identity_SeedServicePermissionGrants` (the ten
`services.manage`/`services.read` role grants in `docs/10` §Service permission grants) and
`20260925072603_Security_TimetableGrants` (what `ycr_app` may do). All three are additive on F-003's
schema: no `network` table, column, index, key or grant changes (ADR-0025 item 6). Every foreign
key is `NO ACTION`. Both `*Utc` columns are `datetimeoffset(3)` with a `CK_<Table>_<Column>_Utc`
check, and every check constraint is declared in the EF model as well as the migration, so
`has-pending-model-changes` sees drift. No service data is seeded (OQ1; spec R27).

**BUSINESS DECISION — provisional tech-lead rulings (hein, 2026-09-25; T-044, OQ42–OQ50) — not a
Myanma Railways answer:** a service is immutable except for withdrawal, is never deleted, and two
services with one code may not have overlapping effective periods. The constraints, the code lock
and the grants below are the database statement of those rulings.

### `timetable.Services`

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | no | PK, clustered, application-assigned through `IIdGenerator` (ADR-0006) |
| `Code` | `nvarchar(10)` | no | 2–10 characters of `A`–`Z` and `0`–`9` (OQ43). **Not unique**: a code may be reused by a service whose period does not overlap (R35) |
| `NameEn` | `nvarchar(100)` | no | Owned value object `BilingualName`; required, 1–100 characters after trimming, not unique |
| `NameMy` | `nvarchar(100)` | no | Myanmar Unicode, never Zawgyi (`docs/20` §6); same rules as `NameEn` |
| `RouteId` | `uniqueidentifier` | no | `FK_Services_Routes_RouteId` → `network.Routes(Id)` |
| `Direction` | `nvarchar(10)` | no | `Forward` or `Reverse`, relative to the route's station order (OQ44); stored as text |
| `RunsOnMonday` … `RunsOnSunday` | `bit` × 7 | no | Operating days; owned value object `OperatingDays` (OQ47) |
| `EffectiveFrom` | `date` | no | First date of the period |
| `EffectiveTo` | `date` | yes | Last date, inclusive; null = open-ended. EF concurrency token (R36) |
| `CreatedAtUtc` | `datetimeoffset(3)` | no | UTC value (ADR-0018) |
| `WithdrawnAtUtc` | `datetimeoffset(3)` | yes | Null until the first withdrawal; then the **latest** withdrawal's instant, written by the same `UPDATE` as `EffectiveTo` |

There is no `rowversion` and no status column: whether a service runs on a date follows from its
period and operating days.

| Object | Definition | Why it is there |
|---|---|---|
| `PK_Services` | clustered on `Id` | ADR-0006 |
| `IX_Services_Code_EffectiveFrom` | nonclustered on `(Code, EffectiveFrom)`, **not unique** | The overlap read under the code lock (R35) and the list order (`Code, EffectiveFrom`, then `Id`, which a non-unique nonclustered index carries in its key). It cannot be unique: the rule is "no overlapping periods", not "no equal codes" |
| `IX_Services_RouteId` | nonclustered on `RouteId` | Covers `FK_Services_Routes_RouteId` and the `?routeId=` list filter |
| `FK_Services_Routes_RouteId` | → `network.Routes(Id)`, `NO ACTION` | Cross-schema key (§Module schemas, ADR-0025): a service never names a missing route, whoever writes it |
| `CK_Services_Direction` | `[Direction] IN (N'Forward', N'Reverse')` | From any writer |
| `CK_Services_OperatingDays` | at least one `RunsOn…` column is `1` | From any writer |
| `CK_Services_EffectivePeriod` | `[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom] OR [WithdrawnAtUtc] IS NOT NULL` | A period can be empty (end before start: the service never runs) only after a withdrawal |
| `CK_Services_CreatedAtUtc_Utc` | `DATEPART(TZOFFSET, [CreatedAtUtc]) = 0` | ADR-0018 |
| `CK_Services_WithdrawnAtUtc_Utc` | `[WithdrawnAtUtc] IS NULL OR DATEPART(TZOFFSET, [WithdrawnAtUtc]) = 0` | ADR-0018 |

### `timetable.ServiceStops`

| Column | Type | Null | Notes |
|---|---|---|---|
| `ServiceId` | `uniqueidentifier` | no | `FK_ServiceStops_Services_ServiceId` → `timetable.Services(Id)` |
| `Position` | `int` | no | 1-based and contiguous (`1..k`) in stop order within the service. Not the route position and not the ADR-0014 station index |
| `StationId` | `uniqueidentifier` | no | `FK_ServiceStops_Stations_StationId` → `network.Stations(Id)` |

| Object | Definition | Why it is there |
|---|---|---|
| `PK_ServiceStops` | clustered on `(ServiceId, Position)` | One station per position; also covers the `ServiceId` foreign key. Contiguity, order, the full-circuit closure and the minimum of two stops are enforced by the `Service` aggregate, because a check constraint cannot see other rows |
| `CK_ServiceStops_Position` | `[Position] >= 1` | From any writer |
| `IX_ServiceStops_StationId` | nonclustered on `StationId`, **not unique** | Covers the station foreign key. Not unique: a full circuit repeats its first station as its last stop |
| `FK_ServiceStops_Services_ServiceId` | → `timetable.Services(Id)`, `NO ACTION` | EF's default for this required relationship is `Cascade`, so `NO ACTION` is set explicitly |
| `FK_ServiceStops_Stations_StationId` | → `network.Stations(Id)`, `NO ACTION` | Cross-schema key (§Module schemas, ADR-0025): a stop never names a missing station |

There is no key to `network.RouteStations`: that a stop is on the service's route is the
aggregate's rule, and a key to another module's alternate key is not allowed (ADR-0025 item 6).

**Both convention indexes are declared.** EF Core adds an index by convention for every foreign
key that no other index leads with: here `IX_Services_RouteId` and `IX_ServiceStops_StationId`
(the `ServiceId` key is covered by `PK_ServiceStops`). Both are declared explicitly in the
configuration with `HasDatabaseName`, and `TimetableModelTests` pins the exact index set of each
table, so no index appears by accident (the F-003 `IX_RouteStations_StationId` lesson).
`IX_ServiceStops_StationId` serves no F-004 query; a later "services calling at station X" read
will use it.

| Query or write | Access path |
|---|---|
| Create, under the code lock: the periods of one code | `IX_Services_Code_EffectiveFrom` seek |
| `INSERT` foreign-key validation | `PK_Routes`, `PK_Stations`, `PK_Services` |
| Withdraw: the code by id; reload with stops | `PK_Services`; `PK_ServiceStops` range on `ServiceId` |
| Get one service and its stops | `PK_Services`; `PK_ServiceStops` range |
| List: `ORDER BY Code, EffectiveFrom, Id`, `COUNT`, `OFFSET/FETCH`, `stopCount` subquery | `IX_Services_Code_EffectiveFrom` ordered scan; `PK_ServiceStops` prefix |
| List `?routeId=` | `IX_Services_RouteId` |
| Reverse key checks on `DELETE FROM network.Routes` / `network.Stations` | never run: `ycr_app` has no `DELETE` there |

Rows are never deleted and a service's stops never change, so the rows are the history:
`EffectiveFrom`, `EffectiveTo` and `WithdrawnAtUtc` answer when a service applied without reading
the audit ledger. `Down()` of `Timetable_CreateServices` drops `ServiceStops`, `Services` and the
`timetable` schema; after real services exist, recovery is a restore, not `Down()`.

### The service-code lock (R35)

"No overlapping periods for one code" cannot be a unique index. `CreateServiceHandler` and
`WithdrawServiceHandler` therefore open a transaction, take an exclusive, transaction-owned
`sp_getapplock` on the resource `timetable.ServiceCode:<CODE>` (passed as a parameter; 30 s
timeout), and only then read the code's periods and write. Two requests on one code run one after
the other; requests on different codes never wait. A timeout is an opaque `500`. `sp_getapplock`
is executable by `public`, so, like F-002's last-administrator lock, it needs no grant. The general
rule is in `docs/20` §6 and ADR-0026 (Accepted). `EffectiveTo` is the concurrency token behind the
lock, for a writer that bypasses it (R36).

**Changed by F-005 (spec R19, §0.12; plan P9):** `WithdrawServiceHandler` also takes the
Timetable-wide lock `timetable.ScheduleVersions`, **after** this code lock, and reads which published
timetable versions list the service before it decides. `CreateServiceHandler` still takes only the
code lock. The order and why it cannot deadlock are in §F-005 §The Timetable-wide lock below.

### Grants to `ycr_app` (`Security_TimetableGrants`)

| Table | Granted | Deliberately absent |
|---|---|---|
| `Services` | `SELECT`, `INSERT`; `UPDATE` of `EffectiveTo`, `WithdrawnAtUtc` only | `DELETE`; `UPDATE` of `Id`, `Code`, `NameEn`, `NameMy`, `RouteId`, `Direction`, every `RunsOn…` column, `EffectiveFrom`, `CreatedAtUtc` |
| `ServiceStops` | `SELECT`, `INSERT` | `UPDATE`; `DELETE` |

No DDL and no `EXECUTE` is granted (the code lock needs none). These grants are the database
statement that a service is immutable except for withdrawal and is never deleted (spec R20, R33),
so even arbitrary SQL under the application credential cannot rewrite or delete a service or its
stops. `DatabasePrivilegeTests` asserts both the grants and the absences. `Down()` revokes exactly
these grants; widening them needs a new migration, a spec change and a review.

## F-005 timetable-version tables, constraints, indexes and grants

Created by three migrations applied in this order under `ycr_migrator` (F-005 spec §7, plan
§DB changes, P20): `20260927070804_Timetable_CreateScheduleVersions` (the three tables and every
object below, EF model-built), `20260927071025_Identity_SeedSchedulePermissionGrants` (the ten
`schedules.manage`/`schedules.read` role grants in `docs/10` §Schedule permission grants; 34 → 44
grants in all) and `20260927072142_Security_TimetableScheduleGrants` (what `ycr_app` may do). All
three are additive on F-004's schema: no `timetable.Services` or `timetable.ServiceStops` column,
constraint, index or grant changes, and nothing in `network`. Every key is inside the `timetable`
schema, so ADR-0025's cross-module conditions do not apply. Every foreign key is `NO ACTION`. Every
`*Utc` column is `datetimeoffset(3)` with a `CK_<Table>_<Column>_Utc` check, and every check
constraint is declared in the EF model as well as the migration, so `has-pending-model-changes`
sees drift. No timetable version is seeded (OQ1; spec R37).

**BUSINESS DECISION — provisional tech-lead rulings (hein, 2026-09-26; T-053, OQ51–OQ60; Amendments
1–2, 2026-09-27) — not a Myanma Railways answer:** one version covers the whole network; a draft is
created whole and never edited; a published version never changes except that it may be cancelled
before it takes effect; no version is ever deleted; start dates are unique among published versions;
no time runs past 23:59. The constraints, the lock and the grants below are the database statement
of those rulings.

### `timetable.ScheduleVersions`

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | no | PK, clustered, application-assigned through `IIdGenerator` (ADR-0006) |
| `Number` | `int` | no | System-assigned under the Timetable-wide lock: `1`, then the highest number ever assigned + 1, so contiguous and never reused (R7) |
| `NameEn` | `nvarchar(100)` | no | Owned value object `BilingualName`; required, 1–100 characters after trimming, not unique |
| `NameMy` | `nvarchar(100)` | no | Myanmar Unicode, never Zawgyi (`docs/20` §6); same rules as `NameEn` |
| `EffectiveFrom` | `date` | no | The start date; set at creation, never changed. There is no end date: a version is in force until the next published version's start date (R8) |
| `Status` | `nvarchar(10)` | no | `Draft`, `Published`, `Discarded` or `Cancelled`, stored as text. EF concurrency token (R47) |
| `CreatedAtUtc` | `datetimeoffset(3)` | no | UTC value (ADR-0018) |
| `PublishedAtUtc` | `datetimeoffset(3)` | yes | Set by publish; kept by cancel |
| `DiscardedAtUtc` | `datetimeoffset(3)` | yes | Set by discard |
| `CancelledAtUtc` | `datetimeoffset(3)` | yes | Set by cancel |

There is no `rowversion`. "In force" and "applies" are never stored: they are derived from the
published rows' start dates (R21).

| Object | Definition | Why it is there |
|---|---|---|
| `PK_ScheduleVersions` | clustered on `Id` | ADR-0006 |
| `UX_ScheduleVersions_Number` | unique on `Number` | R7 backstop behind the lock; also the list order and the "highest number" backward seek |
| `UX_ScheduleVersions_EffectiveFrom_Published` | unique on `EffectiveFrom` **`WHERE [Status] = N'Published'`** (filtered) | R22: no two published versions share a start date. Drafts, discarded and cancelled versions may share one with anything. It is the authority for that rule: publish does not pre-read, and the handler maps this constraint name to `409 Timetable.ScheduleVersionEffectiveFromTaken`. It also serves the timeline scan (in force, withdrawal guard) |
| `CK_ScheduleVersions_Number` | `[Number] >= 1` | From any writer |
| `CK_ScheduleVersions_Status` | `[Status] IN (N'Draft', N'Published', N'Discarded', N'Cancelled')` | From any writer |
| `CK_ScheduleVersions_StatusInstants` | `Draft`: all three transition instants null · `Published`: `PublishedAtUtc` only · `Discarded`: `DiscardedAtUtc` only · `Cancelled`: `PublishedAtUtc` and `CancelledAtUtc`, not `DiscardedAtUtc` | The lifecycle (Draft → Published → Cancelled, or Draft → Discarded) from any writer |
| `CK_ScheduleVersions_CreatedAtUtc_Utc` | `DATEPART(TZOFFSET, [CreatedAtUtc]) = 0` | ADR-0018 |
| `CK_ScheduleVersions_PublishedAtUtc_Utc`, `…_DiscardedAtUtc_Utc`, `…_CancelledAtUtc_Utc` | `[<column>] IS NULL OR DATEPART(TZOFFSET, [<column>]) = 0` | ADR-0018 |

### `timetable.ScheduleVersionServices`

One row per service a version lists.

| Column | Type | Null | Notes |
|---|---|---|---|
| `ScheduleVersionId` | `uniqueidentifier` | no | `FK_ScheduleVersionServices_ScheduleVersions_ScheduleVersionId` → `timetable.ScheduleVersions(Id)` |
| `ServiceId` | `uniqueidentifier` | no | `FK_ScheduleVersionServices_Services_ServiceId` → `timetable.Services(Id)`; no navigation |

| Object | Definition | Why it is there |
|---|---|---|
| `PK_ScheduleVersionServices` | clustered on `(ScheduleVersionId, ServiceId)` | A service is listed once per version (R17); covers the key to `ScheduleVersions` |
| `IX_ScheduleVersionServices_ServiceId` | nonclustered on `ServiceId` | Covers the key to `Services`, and answers "which versions list S" for the withdrawal guard (R19) |
| `FK_ScheduleVersionServices_ScheduleVersions_ScheduleVersionId` | → `ScheduleVersions(Id)`, `NO ACTION` | An entry never names a missing version |
| `FK_ScheduleVersionServices_Services_ServiceId` | → `timetable.Services(Id)`, `NO ACTION` | An entry never names a missing service, whoever writes it |

### `timetable.ScheduleStopTimes`

One row per stop of each listed service. A time is a whole number of minutes after local midnight
of the service's operating date (ADR-0027), exchanged in the API as `HH:mm`.

| Column | Type | Null | Notes |
|---|---|---|---|
| `ScheduleVersionId` | `uniqueidentifier` | no | With `ServiceId`, `FK_ScheduleStopTimes_ScheduleVersionServices_ScheduleVersionId_ServiceId` → `ScheduleVersionServices` |
| `ServiceId` | `uniqueidentifier` | no | With `Position`, `FK_ScheduleStopTimes_ServiceStops_ServiceId_Position` → `timetable.ServiceStops(ServiceId, Position)` |
| `Position` | `int` | no | The service's stop position, `1..k` |
| `ArrivalMinute` | `smallint` | yes | `0`–`1439`; null at stop 1 (R10) |
| `DepartureMinute` | `smallint` | yes | `0`–`1439`; null at the last stop (R10) |

| Object | Definition | Why it is there |
|---|---|---|
| `PK_ScheduleStopTimes` | clustered on `(ScheduleVersionId, ServiceId, Position)` | One time row per stop; covers the key to `ScheduleVersionServices`, and every read of one version's times is a range on it |
| `IX_ScheduleStopTimes_ServiceId_Position` | nonclustered on `(ServiceId, Position)`, **not unique** (one row per version) | Covers the key to `ServiceStops` (plan P21) |
| `FK_ScheduleStopTimes_ScheduleVersionServices_ScheduleVersionId_ServiceId` | → `ScheduleVersionServices(ScheduleVersionId, ServiceId)`, `NO ACTION` | A time belongs to a listed service |
| `FK_ScheduleStopTimes_ServiceStops_ServiceId_Position` | → `timetable.ServiceStops(ServiceId, Position)`, `NO ACTION`; no navigation | A time names a real stop of a real service, whoever writes it |
| `CK_ScheduleStopTimes_Minutes` | `([ArrivalMinute] IS NULL OR [ArrivalMinute] BETWEEN 0 AND 1439) AND ([DepartureMinute] IS NULL OR [DepartureMinute] BETWEEN 0 AND 1439)` | No running past midnight (R15, R16; OQ53) |
| `CK_ScheduleStopTimes_AnyTime` | `[ArrivalMinute] IS NOT NULL OR [DepartureMinute] IS NOT NULL` | Every stop has at least one time (R10) |
| `CK_ScheduleStopTimes_Dwell` | `[ArrivalMinute] IS NULL OR [DepartureMinute] IS NULL OR [DepartureMinute] >= [ArrivalMinute]` | Arrival ≤ departure at one stop (R12) |

Which stop may have which time (stop 1 a departure only, the last stop an arrival only, the others
both), that every position `1..k` is given once, and that each arrival is later than the previous
stop's departure are enforced by the `ScheduleVersion` aggregate, because a check constraint cannot
see other rows.

**The two convention indexes are declared.** EF Core adds an index for every foreign key that no
other index leads with: here `IX_ScheduleVersionServices_ServiceId` and
`IX_ScheduleStopTimes_ServiceId_Position`. Both are declared with `HasDatabaseName`, and
`ScheduleModelTests.ScheduleModel_HasExactlyTheDeclaredIndexes` pins the exact index set of each
table. `IX_ScheduleStopTimes_ServiceId_Position` serves no F-005
query; it is kept because leading the primary key with `(ServiceId, Position)` would lose the
per-version clustering every read uses and bring back a convention index for the other key.

| Query or write | Access path |
|---|---|
| Timeline: the published versions' `(Id, EffectiveFrom)` (in force; withdrawal guard) | `UX_ScheduleVersions_EffectiveFrom_Published` scan (filtered, covering); the guard adds `PK_ScheduleVersionServices` seeks |
| Highest number, under the lock (create) | `UX_ScheduleVersions_Number` backward seek |
| Service facts for create and publish | `PK_Services` seeks; `PK_ServiceStops` prefix for the stop count |
| A version's listed services (publish, get, in force) | `PK_ScheduleVersionServices` range on `ScheduleVersionId`; `PK_Services` seeks |
| One listed service's times | `PK_ScheduleStopTimes` range on `(ScheduleVersionId, ServiceId)`; `PK_ServiceStops` range for station ids |
| List: `ORDER BY Number`, `COUNT`, `OFFSET/FETCH`, `?status=` | `UX_ScheduleVersions_Number` ordered scan; `status` is a residual filter |
| Publish, discard, cancel: one status `UPDATE` | `PK_ScheduleVersions` |
| `INSERT` foreign-key validation | `PK_ScheduleVersions`, `PK_ScheduleVersionServices`, `PK_Services`, `PK_ServiceStops` |
| Reverse key checks on `DELETE` of `Services` / `ServiceStops` | never run: `ycr_app` has no `DELETE` there |

Rows are never deleted and a version's entries and times never change, so the rows are the history:
which times applied on a past date is answered from rows, not the audit ledger (spec R25, R36). The
creation audit event carries a digest of the times rather than the times themselves (spec §8, E13):
`stopTimesSha256` is SHA-256, as 64 lower-case hex characters, of the UTF-8 text (no BOM) made of
one line `"{serviceId}|{position}|{arrivalMinute}|{departureMinute}\n"` per stop time — `serviceId`
in lower-case `D` format, numbers as invariant-culture integers, a missing time as the empty
string — ordered by `serviceId` (ordinal) then `position`. A version with no stop times hashes the
empty input (`e3b0c442…b855`). Anyone can recompute it from these rows. Changing the form bumps the
event's `PayloadVersion` (ADR-0021 rule 3).

`Down()` of `Timetable_CreateScheduleVersions` drops `ScheduleStopTimes`, `ScheduleVersionServices`,
then `ScheduleVersions`; the `timetable` schema and F-004's tables stay. After real versions exist,
recovery is a restore, not `Down()`.

### The Timetable-wide lock (R46)

"No published version that lists S applies on a date on or after D" (the withdrawal guard, R19)
compares one service's period with other rows' dates, so no unique index can enforce it
(ADR-0026 item 1). Create, publish, discard, cancel and service withdrawal therefore open a
transaction and take an exclusive, transaction-owned `sp_getapplock` on the single resource
**`timetable.ScheduleVersions`** (passed as a parameter; 30 s timeout; a negative result is raised as
error `50036`, an opaque `500`) before the reads that decide, then save once and commit. The lock
also serialises the number assignment (R7) and the service re-check at publication (R17). It is a
whole-set lock (ADR-0026 item 2), like F-002's `identity.SystemAdministrators`. Service creation
does not take it (a new service is listed by no version), and no read takes it. `sp_getapplock` is
executable by `public`, so it needs no grant.

| Operation | `timetable.ServiceCode:<CODE>` | `timetable.ScheduleVersions` |
|---|---|---|
| Create, publish, discard, cancel a version | — | ✓ |
| Withdraw a service (F-004, changed) | ✓ first | ✓ second, after the reload and the Network reads, before the coverage read |
| Create a service (F-004) | ✓ | — |
| Every read | — | — |

**Order: the service-code lock, then the Timetable-wide lock** (plan P9). Only withdrawal takes
both, always in that order; no operation takes two code locks; no holder of
`timetable.ScheduleVersions` ever requests a code lock. Every row write happens in the single
`SaveChangesAsync` after all of a transaction's applocks are held. So no transaction waits for an
applock while holding a higher one or a row lock that another applock holder needs, and the two
locks cannot deadlock. `Status` is the concurrency token behind the lock, for a writer that bypasses
it: `409 Timetable.ScheduleVersionChangedConcurrently` (R47). The general rule and the list of lock
uses are in `docs/20` §6.

### Grants to `ycr_app` (`Security_TimetableScheduleGrants`)

| Table | Granted | Deliberately absent |
|---|---|---|
| `ScheduleVersions` | `SELECT`, `INSERT`; `UPDATE` of `Status`, `PublishedAtUtc`, `DiscardedAtUtc`, `CancelledAtUtc` only | `DELETE`; table-level `UPDATE`; `UPDATE` of `Id`, `Number`, `NameEn`, `NameMy`, `EffectiveFrom`, `CreatedAtUtc` |
| `ScheduleVersionServices` | `SELECT`, `INSERT` | `UPDATE`; `DELETE` |
| `ScheduleStopTimes` | `SELECT`, `INSERT` | `UPDATE`; `DELETE` |

No DDL and no `EXECUTE` is granted (the lock needs none). The F-004 grants on `Services` and
`ServiceStops` are unchanged. These grants are the database statement that a version's content
never changes after creation, only its status moves forward, and nothing is deleted (spec R25, R27,
R32), so even arbitrary SQL under the application credential cannot rewrite or delete a version,
its entries or its times. `DatabasePrivilegeTests` asserts both the grants and the absences, and
that `ycr_app` can take the lock. `Down()` revokes exactly these grants; widening them needs a new
migration, a spec change and a review.

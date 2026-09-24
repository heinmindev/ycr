# Database Design Proposal

## Core tables

- Stations
- Routes
- RouteStations
- Trains
- TrainServices
- ServiceStops
- ScheduleVersions
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

# ADR-0025: Cross-Module References: Contract Shape and Foreign Keys

## Status

Accepted — 2026-09-25 (hein). Drafted by claude (T-044); proposed 2026-09-25.

## Context

**FACT — ADR-0012 item 4:** `YCR.Application.<A>` may depend only on `I<A>DbContext`, `YCR.Domain.<A>`, Common, and another module's explicit `YCR.Application.<B>.Contracts`.

**FACT — ADR-0012 item 6:** "Domain modules continue to reference other aggregates by identifiers and never by cross-module navigation properties."

**FACT — ADR-0012 items 1 and 3:** one scoped `YcrDbContext` backs every module interface, so a contract call runs on the caller's connection and inside the caller's transaction.

**FACT — `docs/07-database-design.md` §Rules:** "Use foreign keys." §Module schemas: one schema per module (`network`, `timetable`, …).

**FACT — ADR-0012 is silent on foreign keys between schemas.** It forbids cross-module navigation properties in the domain and cross-module context interfaces in Application; it says nothing about whether `timetable.*` may carry a database foreign key to `network.*`.

**FACT — no module has a `Contracts` namespace yet.** `src/YCR.Application` has `Common`, `Identity` and `Network` only. `tests/YCR.ArchitectureTests/ArchitectureRules.cs` (`ApplicationModuleMayDependOnlyOnAllowedTypes`) already admits a dependency on *any* type in `YCR.Application.<B>.Contracts`.

**FACT — the admission is broader than ADR-0012 intends.** If a type in `YCR.Application.Network.Contracts` exposes a `YCR.Domain.Network` type in its signature (for example a method returning `Route`), a Timetable caller that uses it depends on `YCR.Domain.Network`. The existing rule catches the caller, but only after the contract has been written that way; nothing stops the contract itself.

**FACT — Network rows are never deleted.** `ycr_app` has no `DELETE` on `network.Stations`, `network.Routes` or `network.RouteStations` (`docs/07` §Grants, F-003 R10), and station and route codes are never reused (OQ26, OQ41 rulings).

**Trigger:** F-004 (`docs/features/F-004-service-management/spec.md` §0.4) is the first feature in which one module (Timetable) stores identifiers of another module's rows (Network routes and stations) and must read their current state.

## Options considered

### A. Contract shape

1. **Contracts return the owning module's domain types.** Least code. Leaks `YCR.Domain.<B>` into every caller and fails ADR-0012 item 4 at the caller.
2. **Contracts return primitive records defined in the Contracts namespace; the implementation is `internal` to the owning module.** A little mapping code. Callers see only what the contract names, and the owning module can change its domain freely.

### B. Foreign keys across schemas

1. **None.** The referencing module checks existence through the contract, and the database holds no cross-schema foreign key. Keeps schemas independent for a later extraction (ADR-0012 option 3). But nothing stops a buggy writer, or direct SQL under the application credential, from storing an identifier that names no row, and `docs/07` asks for foreign keys.
2. **Allowed, `NO ACTION`, only to rows that are never deleted, declared in Infrastructure without a navigation property.** The database is the authority that a stored identifier names a real row (AGENTS.md rule 5). Costs nothing on delete because the target is never deleted. Couples the two schemas: the referenced table cannot be dropped or re-keyed without touching the referencing module's migration, and extracting a module later means removing the foreign key first.
3. **Option 2, plus composite foreign keys to another module's alternate keys** (for example a stop's `(StationId, RouteId)` to `network.RouteStations`, proving the station is on the route). Strongest integrity, but it makes the referencing module depend on another module's *unique index* as well as its primary key, and EF Core would create an alternate-key constraint on the other module's table from the referencing module's migration.

## Decision

**Proposed: A2 and B2.**

1. A module's public read surface for other modules lives in `YCR.Application.<B>.Contracts`: interfaces plus `sealed record` result types built from primitives, `Guid`, `DateOnly`, `DateTimeOffset`, strings and other Contracts records only. No type in a Contracts namespace may depend on `YCR.Domain.<Module>` or on any `I<Module>DbContext`.
2. The implementation is an `internal` class in `YCR.Application.<B>` (outside `Contracts`), reading through `I<B>DbContext` with `AsNoTracking`. It is registered in DI against the contract interface.
3. A contract read by another module is **read-only**. A module that needs another module's data changed calls a command contract that the owning module defines for that purpose; none exists yet.
4. **REQUIRED CONTROL:** a new architecture rule fails the build when a type in any `YCR.Application.*.Contracts` namespace depends on a module domain namespace or a module context interface, with a deliberate violating fixture in `tests/YCR.ArchitectureTests/Violations`, in the pattern of the existing rules.
5. A table in one module's schema may carry a foreign key to another module's **primary key** only when the referenced rows are never deleted (the application credential holds no `DELETE` on that table). The foreign key is `NO ACTION`, is configured in `YCR.Infrastructure` with no navigation property on either side, and is created by the **referencing** module's migration.
6. A foreign key to another module's alternate key (option B3) is not allowed under this ADR; a feature that needs one proposes it in its own ADR.
7. The referencing handler still checks existence and state through the contract first, to return a stable error code; the foreign key is the backstop, as the unique indexes are for duplicate codes (ADR-0004).

## Consequences

Positive:
- A caller can never compile against another module's domain or persistence surface, and the contract shape is enforced where it is written, not only where it is used.
- A stored cross-module identifier always names a real row, whoever wrote it.
- Contract reads share the caller's transaction, so a check and a write see one consistent state within the request.

Negative:
- Mapping code in every contract implementation.
- Schemas are coupled at the database: dropping or re-keying a referenced table needs the referencing module's migration too, and extracting a module into its own database needs these foreign keys removed first.
- A contract check is not serialised with a concurrent change in the owning module (for example a route deactivated between the check and the commit). Each feature must state whether it accepts that race, as F-003 did for route creation against station deactivation.

Follow-up work:
- If Accepted, add a "Cross-module reads" row to `docs/20` §1 and a paragraph to `docs/07` §Module schemas.
- The first user is F-004 (Timetable → Network). A reverse dependency (Network calling a Timetable contract, for example to refuse deactivating a route that services use) would create a two-way dependency between modules; a feature that needs one must say so explicitly and hein must rule on it.
- **OQ46 ruling (hein, 2026-09-25; T-044):** deactivating a route or a station stays allowed and leaves services unchanged, so `Network` never calls `Timetable`; the dependency between the two modules stays one-way (Timetable → Network).

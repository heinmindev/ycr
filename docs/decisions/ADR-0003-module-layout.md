# ADR-0003: Module Folders Inside Shared Layer Projects

## Status

Superseded by ADR-0012

## Context

ADR-0001 chose a modular monolith with 9 bounded contexts (docs/05). Doc 06 defined only technical layer projects, so nothing said where a module physically lives or how its boundary is enforced. The team is three developers, so project count and build friction matter.

## Decision

Keep the layer projects from doc 06. Each one has **one folder per module**. Module boundaries are enforced by architecture tests on namespaces, not by project references.

```text
src/
  YCR.Domain/
    Common/            # AggregateRoot, Entity, Money, Result, Error ...
    Network/           # Station, Route  (Station & Network context)
    Timetable/         # Service, ScheduleVersion
    Fare/
    Ticketing/
    Payments/          # Payment, Refund
    Operations/        # CashierSession
    Audit/
  YCR.Application/
    Common/            # abstractions: IYcrDbContext, IAuditWriter, filters
    Network/
      CreateStation/   # one folder per use case (vertical slice)
      Contracts/       # the ONLY namespace other modules may use
    Fare/
      QuoteFare/
      Contracts/       # e.g. IFareQuoteService
    ...
  YCR.Infrastructure/
    Persistence/
      YcrDbContext.cs
      Configurations/<Module>/   # EF IEntityTypeConfiguration per entity
      Migrations/
    Identity/  Audit/  Crypto/  Time/
  YCR.Api/
    Endpoints/<Module>/
    Program.cs
  YCR.Worker/
```

Module names (singular, PascalCase): `Identity`, `Network`, `Timetable`, `Fare`, `Ticketing`, `Payments`, `Operations`, `Reporting`, `Audit`.

### Boundary rules (enforced in `YCR.ArchitectureTests` with NetArchTest)

1. Domain: `YCR.Domain.<A>` must not depend on `YCR.Domain.<B>`, except `YCR.Domain.Common`. Cross-module references are by ID (`Guid StationId`), never by navigation property.
2. Application: `YCR.Application.<A>` may depend only on `YCR.Application.<B>.Contracts`. It must not depend on another module's use-case folders.
3. Layer direction: Domain → nothing; Application → Domain; Infrastructure → Application + Domain; Api → Application (+ Infrastructure only in `Program.cs` composition).
4. One `YcrDbContext`, with **one SQL schema per module** (`network.Stations`, `ticketing.Tickets` …). A handler writes only its own module's tables. To change another module's data, it calls that module's contract. A single `SaveChanges` may still cover several modules, so atomic sale + payment is allowed.
5. Reporting may read across schemas, and only through read-only queries or views.

## Consequences

Positive:
- 4 projects + tests instead of ~30, and a flat, convention-driven structure the team already knows.
- Transactions across modules stay simple (one DbContext).

Negative:
- The compiler does not enforce boundaries, so the architecture tests are mandatory and must run in CI.
- Extracting a module later means splitting folders into projects. The `Contracts` namespaces make that mechanical.

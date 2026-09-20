# System Architecture

## Recommended architecture

Modular monolith + Clean Architecture + Vertical Slice features.

```text
Counter / Inspector / Admin SPA (separate frontend, ADR-0005)
          |
       YCR.Api (REST /api/v1)
          |
     Application
          |
       Domain
          |
   Infrastructure
          |
       SQL Server
```

Cross-cutting concerns:

- Identity
- Authorization
- Validation
- Logging
- Metrics
- Auditing
- Idempotency

## Projects

```text
src/
  YCR.Domain/
  YCR.Application/
  YCR.Infrastructure/
  YCR.Api/
  YCR.Worker/

tests/
  YCR.Domain.Tests/
  YCR.Application.Tests/
  YCR.Infrastructure.Tests/
  YCR.Api.Tests/
  YCR.IntegrationTests/
  YCR.ArchitectureTests/
```

Prefer feature folders inside Application/API rather than a huge technical-layer folder tree.

## Module layout (ADR-0012)

Inside each layer project there is one folder per module: `Identity, Network, Timetable, Fare, Ticketing, Payments, Operations, Reporting, Audit`. Use cases are vertical-slice folders in `YCR.Application/<Module>/<UseCase>/`.

Each application module receives only its own `I<Module>DbContext`; all module interfaces resolve to the same scoped concrete `YcrDbContext`. Other modules may be called only through `YCR.Application.<Module>.Contracts`. A dependency on another module's context interface or domain types fails `YCR.ArchitectureTests`. Reporting uses read-only `IReportingReadContext` query views.

`YCR.Web` was removed. The UI is a separate SPA (ADR-0005).

## Related decisions

- ADR-0012 Module-scoped persistence boundaries
- ADR-0004 Handler per use case, Result-based errors (amended 2026-09-19)
- ADR-0005 API-only backend
- ADR-0006 Identifiers
- ADR-0013 Independent ticket and financial lifecycles
- ADR-0014 Fixed-binary signed ticket QR
- ADR-0015 Online counter recovery and idempotency
- ADR-0016 Same-origin SPA authentication
- ADR-0017 Audit ledger (SQL Server 2022)
- ADR-0018 Time, business date and money

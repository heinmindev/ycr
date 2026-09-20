# ADR-0012: Module-Scoped Persistence Boundaries in the Shared Monolith

## Status

Accepted — 2026-09-19

## Supersedes

ADR-0003, while retaining its five-project modular-monolith layout.

## Context

**FACT — ADR-0003:** namespace architecture tests cannot prove which `DbSet` a handler writes when every module receives one shared `IYcrDbContext`.

**ENGINEERING DECISION (tech lead, cite ADR-0012):** the application must retain one scoped concrete `YcrDbContext` so cross-module contract calls can share one `SaveChangesAsync` and one transaction.

**ENGINEERING DECISION (tech lead, cite ADR-0012):** module boundaries must fail the build when an application module depends on another module's context interface or domain types.

## Options considered

1. Namespace tests over one shared context - low project cost, but no protection against cross-module table writes.
2. Module-scoped context interfaces over one concrete context - preserves atomic transactions with compile-time boundaries.
3. Separate module projects - strongest isolation, but higher build, migration, and transaction cost.

## Decision

1. Keep the existing shared layer projects and one scoped concrete `YcrDbContext`.
2. Define one persistence interface per module: `INetworkDbContext`, `ITimetableDbContext`, `IFareDbContext`, `ITicketingDbContext`, `IPaymentsDbContext`, `IOperationsDbContext`, `IIdentityDbContext`, and `IAuditDbContext`. Each interface exposes only that module's `DbSet`s and required persistence methods.
3. Register all module interfaces to the same scoped `YcrDbContext` instance. A cross-module application contract call therefore participates in the caller's transaction and final `SaveChangesAsync`.
4. `YCR.Application.<A>` may depend only on `I<A>DbContext`, `YCR.Domain.<A>`, Common, and another module's explicit `YCR.Application.<B>.Contracts`. A dependency on another module's context interface or domain namespace fails `YCR.ArchitectureTests` and CI.
5. Reporting uses `IReportingReadContext`, which exposes only read-only query views/projections. Reporting has no write context.
6. Domain modules continue to reference other aggregates by identifiers and never by cross-module navigation properties.

## Enforcement

**REQUIRED CONTROL:** architecture tests must inspect type dependencies and fail the build for the forbidden context/domain references above. Tests are a secondary guard; the narrow interfaces are the primary compile-time boundary.

**OPEN QUESTION:** whether a future Roslyn analyzer is needed in addition to architecture tests remains open until the walking skeleton demonstrates the test coverage.

## Consequences

Positive:
- One database transaction remains available for sale/payment and refund workflows.
- Application handlers cannot compile against another module's persistence surface.
- Reporting cannot accidentally write through the operational context.

Negative:
- The concrete context still contains all sets and requires careful registration.
- Cross-module orchestration must use contracts and preserve one unit of work.
- Architecture tests must run in CI and include negative dependency cases.

# ADR-0004: Handler per Use Case, Result-Based Errors

## Status

Accepted — 2026-09-19

## Decision

### Request flow

```text
Minimal API endpoint (YCR.Api/Endpoints/<Module>)
  → request DTO validated (FluentValidation, endpoint filter)
  → <UseCase>Handler (YCR.Application/<Module>/<UseCase>)
      → domain aggregates enforce invariants
      → IYcrDbContext (EF Core), no generic repositories
  → Result<T> → ProblemDetails / success response
```

- **One handler class per use case**, injected directly into the endpoint through DI. No MediatR, and no generic `IRepository<T>`.
- Cross-cutting concerns (validation, idempotency, audit correlation) run as **endpoint filters** or middleware. Transactions come from one `SaveChangesAsync` per handler. An explicit transaction is used only when a handler needs several saves.
- Queries may project straight to DTOs with `AsNoTracking()` and don't need domain objects.
- Application references `Microsoft.EntityFrameworkCore` through `IYcrDbContext`. This is a pragmatic choice, accepted to avoid a repository layer.

### Errors

- Expected business and validation failures are returned as `Result<T>` / `Result`, carrying an `Error(Code, Message, ErrorType)`.
- Error codes are stable strings `<Module>.<Reason>`, e.g. `Ticketing.TicketNotRefundable`, and defined as static members in `<Module>Errors` classes.
- `ErrorType` → HTTP: `Validation` 400, `Unauthorized` 401, `Forbidden` 403, `NotFound` 404, `Conflict` 409, `BusinessRule` 422.
- Every error response is RFC 9457 ProblemDetails with an `errorCode` extension and `traceId`.
- Exceptions are only for unexpected failures (bugs, infrastructure). A global handler turns them into 500 with no internal details. A DB unique-constraint violation on a business key is caught and mapped to `Conflict`.

## Rationale

Explicit handlers are easy for agents to find, copy and test. A Result makes every failure path visible in the signature, which suits rule 6 ("every important business rule requires tests"). MediatR's commercial licensing and indirection bring no benefit at this size.

## Consequences

- Slightly more wiring (one DI registration per handler; use assembly scanning).
- `Result<T>` must be kept small and in-house (`YCR.Domain.Common`) to avoid a dependency.

## Dated amendment — 2026-09-19

**ENGINEERING DECISION (tech lead, cite ADR-0012):** the persistence reference in the request-flow example is amended. A handler injects its module-scoped interface, such as `INetworkDbContext`, rather than the all-module `IYcrDbContext`. All module interfaces resolve to the same scoped `YcrDbContext`; cross-module work uses explicit Application Contracts.

**ENGINEERING DECISION (tech lead, cite ADR-0012):** Reporting uses `IReportingReadContext` and read-only query views. Architecture tests fail dependencies on another module's context interface or domain namespace.

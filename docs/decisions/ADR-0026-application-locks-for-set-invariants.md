# ADR-0026: Application Locks for Set Invariants

## Status

Accepted — 2026-09-26 (hein, at the F-004 merge, T-052). Drafted by claude (T-050; ruling Q2 of the F-004 plan, hein, 2026-09-25); proposed 2026-09-25.

## Context

**FACT — ADR-0004 §Decision:** "Transactions come from one `SaveChangesAsync` per handler. An explicit transaction is used only when a handler needs several saves."

**FACT — some rules are about a set of rows, not one row.** A unique index enforces "no two rows share this key" from any writer, and handlers rely on it as the concurrency authority (`UX_Stations_Code`, `UX_Routes_Code`; `docs/07`). It cannot enforce a rule that compares a new or changed row with *other* rows by something other than equality:

- **F-002 R27** — at least one active `SystemAdministrator` must remain. Two requests that each disable or demote a different administrator can both count "another administrator remains" before either writes.
- **F-004 R35** — two services with the same code may not have overlapping effective periods. Two creates with overlapping periods, or a create racing a withdrawal of the same code, can both read "no overlap" before either writes (`docs/features/F-004-service-management/spec.md` R35, S27–S29, S37).

With one `SaveChangesAsync` under the default `READ COMMITTED` isolation, the read that decides and the write it allows are not atomic, so both requests pass.

**FACT — F-002 plan P14** solved R27 with an exclusive `sp_getapplock` on `N'identity.SystemAdministrators'`, `@LockOwner = 'Transaction'`, `@LockTimeout = 30000`, inside an explicit transaction opened on the module context, taken before the count (`src/YCR.Infrastructure/Identity/SqlServerIdentityAdministratorLock.cs`). The plan called it "the one documented exception to ADR-0004's single-save default".

**FACT — F-004 plan P9, P10 (ruling Q2, hein, 2026-09-25)** is the second use: `sp_getapplock` on `timetable.ServiceCode:<code>` in `CreateServiceHandler` and `WithdrawServiceHandler` (`src/YCR.Infrastructure/Timetable/SqlServerServiceCodeLock.cs`). The ruling asked for the pattern to be written down, so that a third use follows a rule rather than a precedent.

**FACT — `sp_getapplock` is executable by `public`**, so `ycr_app` needs no grant (F-002 V5; `DatabasePrivilegeTests.ApplicationCredential_CanTakeTheAdministratorApplock` and `…CanTakeTheServiceCodeApplock`).

**FACT — `UseSqlServer` is configured without `EnableRetryOnFailure`** (`src/YCR.Infrastructure/DependencyInjection.cs`), so a user-initiated transaction needs no execution-strategy wrapper today.

## Options considered

1. **Single save only; accept the race.** No new mechanism. Breaks the invariant under concurrency, which AGENTS.md rule 5 forbids for R27 and R35.
2. **`SERIALIZABLE` or `UPDLOCK, HOLDLOCK` range reads on the index that serves the deciding query.** Standard SQL. But a key-range lock on an empty range also locks the gap up to the next key, which belongs to a *different* key (neighbouring service codes would block each other); two readers of the same empty range both take `RangeS-U` and then deadlock converting to `RangeI-N` (error 1205, a `500`); what is locked depends on the plan the optimizer picks; and EF Core has no table-hint API, so the deciding query becomes raw SQL (F-004 plan §R35).
3. **Optimistic re-check after the save.** A window remains between the check and the commit, and undoing a committed write is a second write.
4. **A trigger or a check constraint over other rows.** Puts a domain rule in SQL (AGENTS.md rule 3); a scalar-function check constraint is not safe under concurrency either.
5. **A named, exclusive, transaction-owned application lock scoped to the smallest key that covers the invariant, taken before the reads that decide.** One call behind an Application abstraction; plan-independent; no range-lock deadlock; the lock names exactly the set the rule is about. Costs an explicit transaction, which ADR-0004 otherwise reserves for several saves.

## Decision

**Proposed: option 5, as a bounded exception to ADR-0004's single-save default.**

1. **When it is allowed.** A handler may open its own transaction to guard a **set invariant across rows that one unique index cannot enforce** — a rule that compares the row being written with other rows by more than key equality (an overlap, a count, a minimum). Nothing else: a rule one unique index can enforce uses the index, and several saves remain ADR-0004's own case.
2. **Lock scope.** The lock is scoped to the **smallest key that covers the invariant**, so requests on unrelated keys never wait: one service code for R35; the whole `SystemAdministrator` set for R27, because that rule is about the set as a whole.
3. **Lock naming.** The resource is `<schema>.<Concept>` for a lock on a whole set (`identity.SystemAdministrators`) or `<schema>.<Concept>:<key>` for a keyed lock (`timetable.ServiceCode:SV1`). A keyed resource is built only from a value that has already passed its value object's validation, is passed to `sp_getapplock` as a **parameter**, never interpolated into SQL text, and must map one key to one resource (resource names compare as binary, so the key is normalised by its value object first).
4. **Transaction ownership.** `@LockMode = 'Exclusive'`, `@LockOwner = 'Transaction'`. The lock is acquired through a module Application abstraction (`I<Module><Concept>Lock`) whose SQL Server implementation refuses to run without an open transaction on the module context. The order is: begin the transaction → take the lock → read everything that decides → let the domain decide → add or change, audit → **one `SaveChangesAsync`** → commit. Reads that do not decide the invariant (for example another module's contract read) happen before the transaction, so the lock is held only for the deciding read and the write. Disposing the transaction without a commit rolls back and releases the lock; a lock never outlives its request (`@LockOwner = 'Session'` with an explicit release is not used). Default isolation (`READ COMMITTED`).
5. **Timeout.** `@LockTimeout = 30000` (30 s), as F-002. A negative `sp_getapplock` return (timeout, deadlock victim, error) is raised with `THROW`, so the operation fails instead of running unlocked.
6. **A timeout or a deadlock is an unexpected failure, not a business error.** It is not caught and not mapped to a module `Error`: it becomes the F-001 opaque `500` (ADR-0004; `src/YCR.Api/Common/ProblemDetailsSetup.cs`: ProblemDetails with `errorCode` `Common.UnexpectedError` and `traceId`, no message, stack trace or type name; the detail is logged). It is never a `409` or `422`, and the transaction rolls back, so nothing is written.
7. **A backstop stays.** The rows the lock protects keep a concurrency token or constraint for writers that bypass the lock (for example `Services.EffectiveTo`, F-004 R36), so a direct-SQL writer produces a conflict, not a broken invariant.
8. **Tests.** Each use has a real-SQL-Server test that the lock blocks a second transaction on the same resource and not on another one, run as `ycr_app`, and a forced-order concurrency test showing the invariant holds under the race it guards.

## Consequences

Positive:
- Set invariants hold under concurrency from every request path, with the rule still decided in the domain from facts the handler loaded under the lock.
- Requests on different keys never wait for each other, and there is no range-lock deadlock between two requests on the same key: the second waits.
- No grant, no raw SQL in the deciding query, and one reviewed shape for every future use.

Negative:
- A second transaction pattern next to ADR-0004's single save. Reviewers must check that each use really is a set invariant a unique index cannot enforce.
- Requests on one key are serialised; a slow request holds the key for up to its own duration, and a waiter gives up after 30 s with a `500`.
- `sp_getapplock` is SQL Server–specific. Another provider would need its own lock implementation behind the same abstraction.
- If connection resiliency (`EnableRetryOnFailure`) is ever enabled, every such handler must run its transaction inside the execution strategy.

Follow-up work:
- `docs/20` §6 states the rule and cites this ADR (T-050).
- If Accepted, F-002's `IIdentityAdministratorLock` and F-004's `IServiceCodeLock` are its two conforming uses; no code change is needed.

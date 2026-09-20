# ADR-0020: F-001 Authentication Scope and the Test Authentication Handler

## Status

Proposed — 2026-09-20

## Context

**FACT — `docs/21-definition-of-done.md` §Tests:** every feature needs API tests for 401 and 403 with a wrong permission. Endpoints must therefore be genuinely protected from F-001 onward; a skeleton with open endpoints could not satisfy its own Definition of Done.

**FACT — ADR-0016:** the approved authentication design is substantial: access JWTs carrying only `sub` and `sid`, an `identity.AuthSessions` table, hashed rotating refresh tokens in an `HttpOnly; Secure; SameSite=Strict` cookie scoped to `/api/v1/auth/refresh`, a ~20-second predecessor grace window returning `409 Auth.RefreshSuperseded`, family revocation on ancestor reuse, permission and revocation latency of no more than 30 seconds, and Origin checks on every cookie-bearing endpoint. Its §Required tests list alone is nine scenarios.

**ENGINEERING DECISION (tech lead, cite ADR-0020):** F-001 is a walking skeleton whose purpose is to give later features an executable pattern to copy (`docs/reviews/2026-09-19-starter-kit-review.md` §4 item 9). Implementing ADR-0016 in full inside F-001 would make the skeleton's dominant content an authentication subsystem rather than the vertical-slice pattern.

**FACT:** a test-only authentication handler that trusts request-supplied identity is, by construction, a total authentication bypass. If it were ever registered in a deployed environment it would be a Critical vulnerability, so its containment is a security control and not a convenience.

**Related open question:** OQ28 — role grants are unresolved, so no role→permission seed data may exist in F-001 (`docs/10-authorization-matrix.md` §Permission inventory; `docs/features/F-001-walking-skeleton/spec.md` R8).

## Options considered

1. **Implement ADR-0016 in full in F-001** — no deferral and no test-only handler to contain, but the skeleton grows into an authentication feature, delaying the pattern every other feature needs, and the refresh-rotation tests dominate the suite.
2. **Ship no authentication in F-001** — smallest skeleton, but `docs/21` §Tests cannot be satisfied, endpoints carry no `.RequireAuthorization(...)`, and the copied pattern would teach later features to omit authorization.
3. **Ship the authorization pipeline with an environment-fenced test authentication handler, and defer ADR-0016's token and refresh implementation** — the skeleton proves 401/403 and the permission pattern, at the cost of one dangerous component that must be provably unreachable outside tests.

## Decision

Option 3.

1. **F-001 ships the authorization pipeline in full:** `Permissions` constants in `src/YCR.Application/Common/Authorization/Permissions.cs` (`docs/20` §1), policy registration, and `.RequireAuthorization(<permission>)` on every endpoint, or an explicit `.AllowAnonymous()` with a comment giving the reason (`docs/20` §4).
2. **F-001 ships a test-only authentication handler** that establishes a principal with the permissions a test asks for. No token issuance, no refresh, no `identity.AuthSessions`.
3. **ADR-0016's token and refresh implementation is deferred** to a dedicated follow-up feature. ADR-0016 remains Accepted and binding; this ADR defers its implementation and changes none of its decisions.
4. **REQUIRED CONTROL — the test handler is fenced to the `Testing` environment.** It is registered only when the host environment is `Testing`. If it is registered under any other environment, **application startup throws** rather than logging a warning or silently continuing. Failing closed at startup is deliberate: a bypass that degrades quietly is worse than one that refuses to boot.
5. **REQUIRED CONTROL — a test proves both halves of the fence:** that registration succeeds under `Testing`, and that startup throws under `Production`. It is not enough for the guard to exist in source.
6. **No role→permission grants are seeded** (OQ28). Tests mint the permissions they need directly on the test principal. Nothing in F-001 may imply a role mapping.
7. The deferral is recorded here so a later agent reading `docs/20`'s reference slice cannot mistake the test handler for the approved authentication design.

## Consequences

Positive:
- `docs/21` §Tests is satisfiable from F-001: 401 and 403 are real, tested behaviour rather than a to-do.
- Every later slice copies an endpoint that is authorized by default.
- The authentication bypass is contained by a startup-time failure that is itself under test.
- Deferring ADR-0016 keeps its nine required test scenarios in the feature that implements them, where they can be reviewed as a whole.

Negative:
- A total authentication bypass exists in the codebase, and its safety depends on the environment fence holding. The security review for F-001 must treat the fence as a primary finding area.
- Until the follow-up feature lands, the system cannot authenticate a real user, so F-001 is not independently deployable.
- ADR-0016's Origin checks, CSP and dependency-audit controls are not exercised in F-001, because no cookie-bearing endpoint and no SPA exist yet.

Follow-up work:
- A dedicated feature implementing ADR-0016 (tokens, `identity.AuthSessions`, refresh rotation and grace, revocation latency, Origin checks) and removing the test handler's role in any non-test path.
- OQ28 must be answered before any role→permission seed data is added.

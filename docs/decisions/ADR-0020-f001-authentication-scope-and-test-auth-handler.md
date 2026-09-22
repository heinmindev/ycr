# ADR-0020: F-001 Authentication Scope and the Test Authentication Handler

## Status

Accepted — 2026-09-20 (hein)

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
2. **The test authentication handler never ships.** Its class lives only in `tests/YCR.Api.Tests` and is registered only through `WebApplicationFactory.ConfigureTestServices`. **No authentication handler of any kind exists in `src/`.** It establishes a principal with the permissions a test asks for; there is no token issuance, no refresh and no `identity.AuthSessions`.
3. **ADR-0016's token and refresh implementation is deferred** to a dedicated follow-up feature. ADR-0016 remains Accepted and binding; this ADR defers its implementation and changes none of its decisions.
4. **REQUIRED CONTROL (primary) — an architecture test asserts that no subtype of `AuthenticationHandler<>` exists anywhere in `src/`.** This is the control that matters: the bypass is absent from the deployable artifact rather than merely disabled within it. The test fails the build, per ADR-0012 §Enforcement.
5. **REQUIRED CONTROL (defence in depth) — the startup environment guard is retained.** In any environment other than `Testing`, startup validates the registered authentication schemes against an allowlist of expected production handler types and **throws** if an unexpected handler type is registered. It does not log a warning and continue. This guard is secondary: decision 4 already keeps the test handler out of the artifact, and this catches the case where a test assembly is somehow loaded into a running host. Failing closed is deliberate — a bypass that degrades quietly is worse than one that refuses to boot.
6. **REQUIRED CONTROL — a test proves both halves of the fence:** that registration succeeds under `Testing`, and that startup throws under `Production`. It is not enough for the guard to exist in source.
7. **No role→permission grants are seeded** (OQ28). Tests mint the permissions they need directly on the test principal. Nothing in F-001 may imply a role mapping.
8. The deferral is recorded here so a later agent reading `docs/20`'s reference slice cannot mistake the test handler for the approved authentication design.

## Consequences

Positive:
- `docs/21` §Tests is satisfiable from F-001: 401 and 403 are real, tested behaviour rather than a to-do.
- Every later slice copies an endpoint that is authorized by default.
- The bypass is absent from the deployable artifact, not merely disabled inside it, and an architecture test keeps it that way as the codebase grows.
- Deferring ADR-0016 keeps its nine required test scenarios in the feature that implements them, where they can be reviewed as a whole.

Negative:
- An authentication bypass still exists in the repository, in the test project. The security review for F-001 should confirm both the architecture test and the startup guard, not just one.
- Until the follow-up feature lands, the system cannot authenticate a real user, so F-001 is not independently deployable.
- ADR-0016's Origin checks, CSP and dependency-audit controls are not exercised in F-001, because no cookie-bearing endpoint and no SPA exist yet.
- The architecture rule in decision 4 assumes YCR writes no authentication handler of its own. That holds for ADR-0016 as written, because JWT bearer and cookie handling come from framework-provided handler types rather than YCR types. If the feature implementing ADR-0016 turns out to need a custom `AuthenticationHandler<>` subtype in `src/`, the rule needs a narrower formulation — an allowlist of permitted handler types rather than a blanket prohibition — and that is a superseding ADR, not a quiet edit to the test.

Follow-up work:
- A dedicated feature implementing ADR-0016 (tokens, `identity.AuthSessions`, refresh rotation and grace, revocation latency, Origin checks), which must also confirm whether the decision-4 architecture rule survives unchanged.
- OQ28 must be answered before any role→permission seed data is added.
- **`YCR.Api.Common.Authorization.AuthorizationResultHandler` must be proved to step aside once a real scheme is registered** (added 2026-09-21 by the F-001 implementation; recorded here rather than in the feature's own notes because it is the ADR-0016 feature that has to act on it). F-001 ships with no authentication handler, so a challenge has no scheme to challenge with and ASP.NET Core throws — an unauthenticated request returned `500` instead of `401` until that handler was added. It intercepts **only** when `GetDefaultChallengeSchemeAsync()` returns null, so registering a scheme should hand every challenge back to the framework's own handler, headers and all. The ADR-0016 feature must assert that directly: with its scheme registered, an unauthenticated request returns the framework's `401` with the expected `WWW-Authenticate` header, and `Common.Unauthenticated` no longer appears. If the interception turns out to be unnecessary by then, delete it rather than leaving a dormant branch in the authorization path.

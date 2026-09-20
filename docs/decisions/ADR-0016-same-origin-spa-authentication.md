# ADR-0016: Same-Origin SPA Authentication and Refresh Rotation

## Status

Accepted — 2026-09-19

## Supersedes

ADR-0009.

## Context

**ENGINEERING DECISION (tech lead, cite ADR-0016):** the SPA and API are served same-origin through a reverse proxy, with API requests under `/api/*`.

**FACT:** an HttpOnly cookie reduces token theft by JavaScript but does not prevent same-origin XSS from using an in-memory access token.

**ENGINEERING DECISION (tech lead, cite ADR-0016):** CSRF is Medium under the same-origin deployment, `SameSite=Strict`, path-scoped cookie, and Origin checks. XSS remains a separate High threat.

## Options considered

1. Same-origin SPA/API with hardened refresh rotation - fits the deployment and keeps implementation scope bounded.
2. Strict refresh rotation with no grace - stronger replay signal, but fragile under browser concurrency.
3. BFF session - reduces browser bearer-token exposure, but adds a stateful security boundary.

## Decision

### Access and session state

- Access JWTs contain only `sub` and `sid`; they contain no permission claims.
- `identity.AuthSessions` stores `Id`, `UserId`, and `RevokedAtUtc`.
- Each request resolves the session and permissions. A per-instance memory cache may reduce repeated lookups, but documented revocation and permission-change latency is no more than 30 seconds.
- Logout, password change, disablement, token-family reuse, and session administration revoke the relevant session/refresh family.

### Refresh cookie and rotation

- Refresh tokens are random, stored hashed, rotated on use, and sent only in an `HttpOnly; Secure; SameSite=Strict` cookie scoped to `/api/v1/auth/refresh`.
- Web Locks provide SPA single-flight refresh where supported.
- On rotation, persist `ReplacedByTokenId` and `RotatedAtUtc`.
- If the immediate predecessor is presented within approximately 20 seconds of rotation, return `409 Auth.RefreshSuperseded`, issue no tokens, and do not revoke the family. The client retries once because the cookie jar should already contain the successor.
- Predecessor reuse after the grace window, or reuse of any older ancestor, revokes the whole family and emits an audit event.

### Browser security

- Every cookie-bearing endpoint checks the request `Origin`.
- CSP uses `script-src 'self'` with no `unsafe-inline` or `unsafe-eval`; `frame-ancestors 'none'` and other secure headers are required.
- The SPA does not render raw HTML from API data.
- CI audits the frontend lockfile/dependencies and API tests cover missing Origin.

## Required tests

Refresh race with two tabs, predecessor reuse inside and outside grace, ancestor reuse, logout, password change, disabled user, removed permission effective within 30 seconds, missing Origin, and session revocation are required.

## Consequences

Positive:
- Same-origin deployment reduces cross-site cookie ambiguity.
- Permission changes and session revocation have an explicit bounded latency.
- Refresh races do not revoke a family for one immediate benign duplicate.

Negative:
- A 20-second predecessor response leaks no tokens but requires a client retry path.
- Per-request session/permission resolution remains dependent on database/cache health.
- CSP and dependency hygiene are mandatory to reduce the separate High XSS risk.

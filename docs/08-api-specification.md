# API Specification

Base path:

`/api/v1`

Initial resources:

- GET/POST/PATCH `/stations`
- GET/POST/PATCH `/routes`
- GET/POST/PATCH `/trains`
- GET/POST/PATCH `/services`
- POST `/schedules/versions`
- POST `/schedules/versions/{id}/publish`
- POST `/fares/versions`
- POST `/fares/versions/{id}/publish`
- POST `/sales` (creates Sale + Payment + Ticket in one transaction)
- GET `/sales/{id}`
- GET `/tickets/{id}`
- POST `/ticket-validations` (accepts the scanned signed QR payload)
- POST `/tickets/{id}/cancel`
- POST `/tickets/{id}/reprints`
- POST `/sales/{id}/refund-requests`
- POST `/refunds/{id}/approve`
- POST `/refunds/{id}/disburse`
- POST `/refunds/{id}/reject`
- GET `/cashier-sessions/current/recent-operations`
- POST `/cashier-sessions/open`
- POST `/cashier-sessions/{id}/close`
- GET `/ticket-qr/trust-list`
- GET `/idempotency/{key}` (optional; caller user + current session scoped)
- GET `/reports/sales`
- GET `/reports/reconciliation`

Published fare and timetable versions are immutable; do not PATCH a published version. Use DTOs, ProblemDetails, validation, authorization policies, pagination and idempotency where applicable.

## Implemented in F-001 — stations

The walking skeleton implements the station slice. Everything else above remains a proposal.

| Method | Path | Request | Success | Error codes | Permission |
|---|---|---|---|---|---|
| POST | `/api/v1/stations` | `CreateStationRequest { code, nameEn, nameMy }` | `201` + `CreateStationResponse { id }` and a `Location` header | `400` `Common.ValidationFailed` (missing or blank field) · `400` `Network.InvalidStationCode` · `400` `Network.InvalidStationName` · `401` · `403` · `409` `Network.StationCodeAlreadyExists` | `stations.manage` |
| POST | `/api/v1/stations/{id}/deactivate` | — | `204` | `401` · `403` · `404` `Network.StationNotFound` · `422` `Network.StationAlreadyInactive` | `stations.manage` |
| GET | `/api/v1/stations/{id}` | — | `200` + `StationResponse { id, code, nameEn, nameMy, isActive, createdAtUtc }` | `401` · `403` · `404` `Network.StationNotFound` | `stations.read` |
| GET | `/api/v1/stations` | `?page=1&pageSize=50` (max 200) | `200` + `{ items, page, pageSize, totalCount }` | `400` `Network.InvalidPageRequest` · `401` · `403` | `stations.read` |
| GET | `/health/live` | — | `200` | — | anonymous — a liveness probe must answer before authentication is reachable |
| GET | `/health/ready` | — | `200` / `503` | — | anonymous — readiness probe used by the reverse proxy |

Every error response is RFC 9457 ProblemDetails carrying `errorCode` and `traceId` (ADR-0004).
`ErrorType` maps to status as ADR-0004 fixes it: `Validation` 400, `Unauthorized` 401,
`Forbidden` 403, `NotFound` 404, `Conflict` 409, `BusinessRule` 422. Station management is neither
financial nor retryable, so no `Idempotency-Key` is required (`docs/20` §5).

**Not implemented:** `PATCH /stations` — station update, search and reactivation are deferred by
the F-001 spec §9. The station **field rules** behind these contracts are provisional under
OQ26–OQ28 and change when T-014 replaces them.

The OpenAPI document is served at `/openapi/v1.json` in Development only. The Scalar API reference
UI renders that same document at `/scalar` (redirects to `/scalar/`; `/scalar/v1` selects the `v1`
document explicitly), also in Development only. Health checks (`/health/live`, `/health/ready`) are
mapped with `MapHealthChecks`, which does not add them to the OpenAPI document, so neither the
document nor Scalar lists them.

## Implemented in F-002 — staff authentication and administration

ADR-0016 and ADR-0023. Errors from endpoints under `/auth/*` use `Auth.*`; errors from
`/users/*`, `/auth-sessions/*` and `/roles` use `Identity.*` (ADR-0023 item 9, U1). A missing
or blank required field is `400 Common.ValidationFailed` on every endpoint below that takes a body.

### Applies to every protected endpoint

- **`401 Auth.Unauthenticated`** with `WWW-Authenticate: Bearer` for a missing, malformed,
  wrongly signed, expired or otherwise invalid access token, and for a token whose session is
  revoked or expired or whose user is disabled. The body gives no validation detail.
- **`403`** when the caller lacks the endpoint's permission.
- **`403 Auth.PasswordChangeRequired`** while the caller's password must be changed: until then the
  session may call only `GET /auth/me`, `POST /auth/password`, `POST /auth/logout` and
  `POST /auth/refresh` (ADR-0023 item 9, U2). Anonymous endpoints are not affected.
- The access token is an ES256 JWT with a `kid` header and only `sub`, `sid`, `jti`, `iss`, `aud`,
  `iat`, `nbf` and `exp`; it lives 15 minutes. A token without a `kid`, or whose `kid` names no
  configured key, is rejected. Roles and permissions are resolved on the server from `sub` and
  `sid`, never read from the token.
- Every response from `/auth/*` carries `Cache-Control: no-store`.

### `/api/v1/auth` — sign-in, refresh, own account

| Method | Path | Request | Success | Error codes | Permission |
|---|---|---|---|---|---|
| POST | `/api/v1/auth/login` | `LoginRequest { userName, password }` | `200` + `AccessTokenResponse { accessToken, expiresAtUtc }` and the refresh cookie | `400` `Common.ValidationFailed` · `401` `Auth.InvalidCredentials` (identical for a wrong password, an unknown username, a disabled and a locked account) · `403` `Auth.OriginRejected` · `429` `Auth.TooManyRequests` | anonymous; `Origin` must be an allowed origin |
| POST | `/api/v1/auth/refresh` | the `ycr_refresh` cookie; no body | `200` + `AccessTokenResponse` and a rotated cookie | `401` `Auth.RefreshInvalid` (missing, unknown, revoked or expired, or a reused older token, which also revokes the session) · `403` `Auth.OriginRejected` · `409` `Auth.RefreshSuperseded` (the token just rotated, presented again within 20 seconds; nothing changes) · `429` `Auth.TooManyRequests` | anonymous (the cookie authenticates); `Origin` must be an allowed origin |
| POST | `/api/v1/auth/logout` | — | `204`; the session named by the token's `sid` is revoked and the cookie expired | `401` `Auth.Unauthenticated` · `403` `Auth.OriginRejected` | any signed-in user; `Origin` must be an allowed origin |
| GET | `/api/v1/auth/me` | — | `200` + `CurrentUserResponse { userId, userName, roles, permissions }` | `401` `Auth.Unauthenticated` | any signed-in user |
| POST | `/api/v1/auth/password` | `ChangePasswordRequest { currentPassword, newPassword }` | `204`; every other session of the caller is revoked, this one survives | `400` `Common.ValidationFailed` · `400` `Auth.PasswordRejected` · `401` `Auth.Unauthenticated` · `422` `Auth.CurrentPasswordIncorrect` | any signed-in user |

**Refresh cookie:** `ycr_refresh`, `HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth/refresh`,
expiring with the session, 12 hours after sign-in; a refresh does not extend it. The refresh
token never appears in a response body.

**Rate limits** (per instance, in process; ADR-0023 item 5): sign-in 5 per minute per username and
20 per minute per client address; refresh 30 per minute per client address. The Origin check runs
first, then the limit, then validation, so a refused request evaluates no credential.

**Password policy** (ADR-0023 item 2): 12 to 128 characters, no composition rules, not on the
shipped common-password blocklist.

### `/api/v1/users`, `/api/v1/auth-sessions`, `/api/v1/roles` — administration

| Method | Path | Request | Success | Error codes | Permission |
|---|---|---|---|---|---|
| GET | `/api/v1/users` | `?page=1&pageSize=50` (max 200) | `200` + `{ items: UserResponse[], page, pageSize, totalCount }`, ordered by username | `400` `Identity.InvalidPageRequest` · `401` · `403` | `users.read` |
| GET | `/api/v1/users/{id}` | — | `200` + `UserResponse { id, userName, roles, isDisabled, lockedUntilUtc, createdAtUtc }` | `401` · `403` · `404` `Identity.UserNotFound` | `users.read` |
| POST | `/api/v1/users` | `CreateUserRequest { userName, password, roles }` | `201` + `CreateUserResponse { id }` and a `Location` header; the password is must-change | `400` `Common.ValidationFailed` (also for a role name outside the catalogue) · `400` `Identity.InvalidUserName` · `400` `Identity.PasswordRejected` · `401` · `403` · `409` `Identity.UserNameAlreadyExists` · `422` `Identity.PrivilegedRoleRequiresMfa` (Production only) | `users.manage` |
| POST | `/api/v1/users/{id}/disable` | — | `204`; all the user's sessions are revoked | `401` · `403` · `404` `Identity.UserNotFound` · `422` `Identity.CannotDisableOwnAccount` · `422` `Identity.LastAdministrator` | `users.manage` |
| POST | `/api/v1/users/{id}/enable` | — | `204` | `401` · `403` · `404` `Identity.UserNotFound` | `users.manage` |
| POST | `/api/v1/users/{id}/unlock` | — | `204`; clears the lockout and the failed-attempt count | `401` · `403` · `404` `Identity.UserNotFound` | `users.manage` |
| PUT | `/api/v1/users/{id}/roles` | `ReplaceUserRolesRequest { roles }` — the complete new set; `[]` removes every role | `204`; live sessions see the change within 30 seconds | `400` `Common.ValidationFailed` (missing `roles`, or a role outside the catalogue) · `401` · `403` · `404` `Identity.UserNotFound` · `422` `Identity.CannotChangeOwnRoles` · `422` `Identity.PrivilegedRoleRequiresMfa` (Production only) · `422` `Identity.LastAdministrator` | `users.roles.manage` |
| POST | `/api/v1/users/{id}/password-reset` | `ResetUserPasswordRequest { newPassword }` | `204`; the password is must-change and all the user's sessions are revoked | `400` `Common.ValidationFailed` · `400` `Identity.PasswordRejected` · `401` · `403` · `404` `Identity.UserNotFound` · `422` `Identity.CannotResetOwnPassword` | `users.manage` |
| GET | `/api/v1/users/{id}/auth-sessions` | `?page=1&pageSize=50` (max 200) | `200` + `{ items: AuthSessionResponse[], page, pageSize, totalCount }`, newest first; `AuthSessionResponse { id, createdAtUtc, expiresAtUtc, revokedAtUtc, revocationReason }` | `400` `Identity.InvalidPageRequest` · `401` · `403` · `404` `Identity.UserNotFound` | `users.read` |
| POST | `/api/v1/auth-sessions/{id}/revoke` | — | `204` | `401` · `403` · `404` `Identity.SessionNotFound` | `auth-sessions.revoke` |
| GET | `/api/v1/roles` | — | `200` + `RoleResponse[] { name, permissions }` — read-only; no endpoint edits grants | `401` · `403` | `users.read` |

- **`Identity.PrivilegedRoleRequiresMfa`** (ADR-0023 item 4 as amended 2026-09-24): in the
  `Production` environment only, a request that creates a user with, or grants,
  `SystemAdministrator`, `RailwayAdministrator` or `FinanceOfficer` is refused and nothing is
  written. Keeping a privileged role the user already holds is not a grant. The MFA feature
  removes the rule.
- **`Identity.LastAdministrator`**: at least one active `SystemAdministrator` must remain;
  disabling or demoting the last one is refused, also under concurrent requests.
- An administrator cannot disable, reset the password of, or change the roles of their own
  account (the three `Cannot…Own…` codes).
- Responses never carry a password, hash, security stamp or token. Every accepted change writes
  one `Identity.*` audit row whose actor and permission come from the server, never from the
  request.
- `Identity.UnknownRole` exists for a role name that reaches a handler without passing request
  validation; through the API, validation answers `400 Common.ValidationFailed` first.

## Blocked behavior

- **OPEN QUESTION:** fare quote inputs, loop direction, ticket validity, cancellation guards, refund eligibility/amount, and repeat-use handling remain blocked by `docs/19-open-questions.md`.
- **OPEN QUESTION:** `AuthenticUnverified` ticket validation handling is blocked by OQ8.
- **OPEN QUESTION:** whether reprint invalidates earlier prints is blocked by OQ23.

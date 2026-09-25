# API Specification

Base path:

`/api/v1`

Initial resources:

- GET/POST/PATCH `/stations`
- GET/POST `/routes`, POST `/routes/{id}/deactivate` — implemented in F-003 (below). **`PATCH /routes` is not provided** (OQ41 ruling: code and names are fixed after creation), and there is no `PUT /routes/{id}/stations` (OQ38 ruling: sequences are immutable)
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

## Implemented in F-003 — routes

F-003 spec §6. A route is an ordered sequence of existing stations, created whole and never edited
afterwards. The route **rules** below are **BUSINESS DECISION — provisional tech-lead rulings (hein,
2026-09-24; T-032, OQ36–OQ41) — not a Myanma Railways answer**; an official, different answer
supersedes them. Every endpoint also has the responses in §Applies to every protected endpoint. No
route endpoint takes an `Idempotency-Key` (route management is neither financial nor retryable,
`docs/20` §5), and no request or response carries a version token.

| Method | Path | Request | Success | Error codes | Permission |
|---|---|---|---|---|---|
| POST | `/api/v1/routes` | `CreateRouteRequest { code, nameEn, nameMy, isClosed, stationIds }`. Body at most **32 KB** | `201` + `CreateRouteResponse { id }` and `Location: /api/v1/routes/{id}` | `400` `Common.ValidationFailed` · `400` `Network.InvalidRouteCode` · `400` `Network.InvalidRouteName` · `400` malformed JSON · `401` · `403` · `409` `Network.RouteCodeAlreadyExists` · `413` body over 32 KB · `422` `Network.RouteStationNotFound` · `422` `Network.RouteStationInactive` · `422` `Network.RouteStationRepeated` · `422` `Network.RouteTooFewStations` | `routes.manage` |
| POST | `/api/v1/routes/{id}/deactivate` | — | `204` | `401` · `403` · `404` `Network.RouteNotFound` · `422` `Network.RouteAlreadyInactive` | `routes.manage` |
| GET | `/api/v1/routes/{id}` | — | `200` + `RouteResponse` | `401` · `403` · `404` `Network.RouteNotFound` | `routes.read` |
| GET | `/api/v1/routes` | `?page=1&pageSize=50` (max 200); ordered by `code`; inactive routes included | `200` + `{ items: RouteSummaryResponse[], page, pageSize, totalCount }` | `400` `Network.InvalidPageRequest` · `401` · `403` | `routes.read` |

`POST /api/v1/stations/{id}/deactivate` is unchanged: deactivating a station that is in a route is
allowed, the station stays in every sequence, and route reads show it with `isActive = false`
(OQ39).

**Contracts.**

```text
CreateRouteRequest   { code, nameEn, nameMy, isClosed: bool, stationIds: string[] }
CreateRouteResponse  { id }
RouteResponse        { id, code, nameEn, nameMy, isClosed, isActive, createdAtUtc, deactivatedAtUtc,
                       stations: RouteStationResponse[] }        // in position order
RouteStationResponse { position, stationId, code, nameEn, nameMy, isActive }
RouteSummaryResponse { id, code, nameEn, nameMy, isClosed, isActive, stationCount, createdAtUtc,
                       deactivatedAtUtc }
```

`stationIds` is the whole sequence, in order: at least one id, at most **200**, each a GUID in
`D` format (`00000000-0000-0000-0000-000000000000`). `isClosed` is required and has no default.
`position` is 1-based and contiguous; it is neither the station code nor the ADR-0014 station
index. A route station's `code`, `nameEn`, `nameMy` and `isActive` are the station's **current**
values, read at query time. `deactivatedAtUtc` is `null` while the route is active. The request has
no actor field; audit actors come from the server.

**Error codes, and where each comes from.**

- **`400 Common.ValidationFailed`** — the request validator, before the handler runs: a missing,
  empty or whitespace-only `code`, `nameEn` or `nameMy`; a missing `isClosed`; a missing or empty
  `stationIds`; a `null` or non-GUID station id; more than 200 station ids. Nothing is read or
  written (spec S5, S11 as amended by Amendment 3).
- **`400 Network.InvalidRouteCode`** — the domain: a code that is not 2–10 characters of `A`–`Z`
  and `0`–`9` after trimming (lowercase, punctuation, an inner space, too short, too long).
- **`400 Network.InvalidRouteName`** — the domain: a name longer than 100 characters after
  trimming.
- **`409 Network.RouteCodeAlreadyExists`** — the code belongs to another route, **active or
  inactive**; codes are never reused. Also the answer to the loser of two concurrent creates with
  one code.
- **`422 Network.RouteStationRepeated`** — a station appears twice. A closed route never repeats its
  first station at the end.
- **`422 Network.RouteTooFewStations`** — fewer than 2 stations on an open route, or fewer than 3 on
  a closed one.
- **`422 Network.RouteStationNotFound`** / **`Network.RouteStationInactive`** — a station id that
  does not exist, or names an inactive station.
- **`422 Network.RouteAlreadyInactive`** — deactivating an inactive route, including the loser of
  two concurrent deactivations.

When a request breaks several rules, the code is checked first, then the names, then the sequence
(repeat, length, missing station, inactive station), then the uniqueness of the code.

**Request-body limit (REQUIRED CONTROL, hein, 2026-09-25; spec R27, review S-1).** `POST
/api/v1/routes` accepts a body of at most **32 KB (32,768 bytes)**. The largest valid request —
200 ids, a 10-character code and two 100-character names — is under 10 KB. The limit is endpoint
metadata (`IRequestSizeLimitMetadata`) that endpoint routing applies to the server before the body
is read, so Kestrel refuses a larger body, with a declared length or chunked, **before JSON
binding**. The answer is `413` as the framework's ProblemDetails (`type`, `title` "Content Too
Large", `status`, `instance`, `traceId`), with no stack trace, exception type or server path.
Malformed JSON is `400` in the same framework shape. Neither carries an `errorCode`, because
neither reaches the application; that is the existing framework behaviour for a body that cannot be
read or parsed, on every endpoint. The limit applies to this endpoint only: every other endpoint
keeps the server's default until T-042 sets deliberate limits.

**Not provided** (OQ38 and OQ41 rulings): no `PATCH /routes/{id}` (code, names and `isClosed` are
fixed at creation), no `PUT /routes/{id}/stations` or other sequence replacement (to change the
network, create a new route and deactivate the old one), no reactivation and no `DELETE`. The
route endpoints appear in the Development-only OpenAPI document under the tag `Routes`.

## Blocked behavior

- **OPEN QUESTION:** fare quote inputs, loop direction, ticket validity, cancellation guards, refund eligibility/amount, and repeat-use handling remain blocked by `docs/19-open-questions.md`.
- **OPEN QUESTION:** `AuthenticUnverified` ticket validation handling is blocked by OQ8.
- **OPEN QUESTION:** whether reprint invalidates earlier prints is blocked by OQ23.

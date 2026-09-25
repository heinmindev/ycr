# API Specification

Base path:

`/api/v1`

Initial resources:

- GET/POST/PATCH `/stations`
- GET/POST `/routes`, POST `/routes/{id}/deactivate` — implemented in F-003 (below). **`PATCH /routes` is not provided** (OQ41 ruling: code and names are fixed after creation), and there is no `PUT /routes/{id}/stations` (OQ38 ruling: sequences are immutable)
- ~~GET/POST/PATCH `/trains`~~ — **not provided** (OQ42 provisional ruling, hein, 2026-09-25: there is no `Train` concept in Phase 1; use case 3 is met by services)
- GET/POST `/services`, POST `/services/{id}/withdraw` — implemented in F-004 (below). **`PATCH /services` is not provided** (OQ48 ruling: a service is immutable except for withdrawal)
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

## Implemented in F-004 — services

F-004 spec §6. A **service** is master data in the `Timetable` module: a code, a bilingual name,
one route, a direction, an ordered list of stops (stations, no times), a set of operating days and
an effective period. It is created whole and afterwards can only be **withdrawn**, which shortens
its period. The service **rules** below are **BUSINESS DECISION — provisional tech-lead rulings
(hein, 2026-09-25; T-044, OQ42–OQ50) — not a Myanma Railways answer**; an official, different
answer supersedes them. Public holidays and per-date exceptions are not implemented (OQ47 stays
open). Every endpoint also has the responses in §Applies to every protected endpoint. No service
endpoint takes an `Idempotency-Key` (spec R25), and no request or response carries a version token.

| Method | Path | Request | Success | Error codes | Permission |
|---|---|---|---|---|---|
| POST | `/api/v1/services` | `CreateServiceRequest`. Body at most **32 KiB** | `201` + `CreateServiceResponse { id }` and `Location: /api/v1/services/{id}` | `400` `Common.ValidationFailed` · `400` `Timetable.InvalidServiceCode` · `400` `Timetable.InvalidServiceName` · `400` `Timetable.InvalidEffectivePeriod` · `400` malformed JSON · `401` · `403` · `409` `Timetable.ServiceCodePeriodOverlap` · `413` body over 32 KiB · `422` `Timetable.ServiceEffectiveToInPast` · `422` `Timetable.ServiceRouteNotFound` · `422` `Timetable.ServiceRouteInactive` · `422` `Timetable.ServiceStopNotOnRoute` · `422` `Timetable.ServiceStopRepeated` · `422` `Timetable.ServiceTooFewStops` · `422` `Timetable.ServiceStopsOutOfOrder` · `422` `Timetable.ServiceStopStationInactive` | `services.manage` |
| GET | `/api/v1/services/{id}` | — | `200` + `ServiceResponse` | `401` · `403` · `404` `Timetable.ServiceNotFound` | `services.read` |
| GET | `/api/v1/services` | `?page=1&pageSize=50` (max 200) `&routeId=` (optional); ordered by `code`, then `effectiveFrom`, then `id`; withdrawn services included; an unknown `routeId` gives an empty page | `200` + `{ items: ServiceSummaryResponse[], page, pageSize, totalCount }` | `400` `Timetable.InvalidPageRequest` · `401` · `403` | `services.read` |
| POST | `/api/v1/services/{id}/withdraw` | `WithdrawServiceRequest { withdrawFrom }`. Body at most **1 KiB** | `204` | `400` `Common.ValidationFailed` · `400` malformed JSON · `401` · `403` · `404` `Timetable.ServiceNotFound` · `409` `Timetable.ServiceChangedConcurrently` · `413` body over 1 KiB · `422` `Timetable.WithdrawalDateInPast` · `422` `Timetable.WithdrawalDoesNotShorten` | `services.manage` |

`services.manage` is held by `SystemAdministrator` and `RailwayAdministrator`; `services.read` by
all eight roles (`docs/10` §Service permission grants). A station or route permission gives no
service right, and a service permission gives no station or route right.

**Contracts.**

```text
CreateServiceRequest   { code, nameEn, nameMy, routeId, direction: "Forward" | "Reverse",
                         stopStationIds: string[],            // in stop order, 1..200
                         operatingDays: string[],             // "Monday".."Sunday", 1..7, no repeats
                         effectiveFrom: date, effectiveTo: date | null }
CreateServiceResponse  { id }
WithdrawServiceRequest { withdrawFrom: date }                 // the first date the service no longer runs
ServiceResponse        { id, code, nameEn, nameMy, direction,
                         route: { id, code, nameEn, nameMy, isClosed, isActive },
                         stops: ServiceStopResponse[],        // in position order
                         operatingDays: string[],             // Monday first
                         effectiveFrom, effectiveTo, neverRuns, createdAtUtc, withdrawnAtUtc }
ServiceStopResponse    { position, stationId, code, nameEn, nameMy, isActive }
ServiceSummaryResponse { id, code, nameEn, nameMy, routeId, routeCode, direction, stopCount,
                         operatingDays, effectiveFrom, effectiveTo, neverRuns, createdAtUtc,
                         withdrawnAtUtc }
```

- `routeId` and every `stopStationIds` element are GUIDs in `D` format
  (`00000000-0000-0000-0000-000000000000`); dates are exactly `YYYY-MM-DD`.
- `direction` is required, with no default, and is exactly `Forward` or `Reverse`: along the
  route's station order, or against it.
- `operatingDays` holds exact, case-sensitive English day names — `Monday`, `Tuesday`,
  `Wednesday`, `Thursday`, `Friday`, `Saturday`, `Sunday` — at least one, no repeats. `monday`,
  `Mon` and `1` are refused. Responses list them Monday first.
- `stopStationIds` is the whole stop list in order: at least one id (the domain then requires two),
  at most **200** (REQUIRED CONTROL, spec R31). A full circuit of a route with 200 stations needs
  201 stops and is therefore not supported.
- `effectiveTo` is inclusive and may be absent or `null` (open-ended).
- `position` is 1-based and contiguous in stop order; it is not the route position, the station
  code or the ADR-0014 station index. A full circuit keeps its closing stop (the first station
  again) at the last position.
- The route's and each stop station's `code`, `nameEn`, `nameMy`, `isActive` (and the route's
  `isClosed`) are **current** values, read at query time through the Network contract; a service
  row stores only ids. Deactivating a route or station is still allowed and changes no service.
- `effectiveTo` and `withdrawnAtUtc` are `null` until set. `withdrawnAtUtc` is the latest
  withdrawal's instant. `neverRuns` is `true` exactly when a withdrawal has made `effectiveTo`
  earlier than `effectiveFrom`; it is computed, not stored.
- No request has an actor field; audit actors come from the server.

**Error codes, and the order they are checked in.** A create that breaks several rules gets the
first failure in this order (spec R37); within one check, the first offending stop in stop order
is reported.

1. **`400 Common.ValidationFailed`** — the request validator, before the handler runs: a missing,
   empty or whitespace-only `code`, `nameEn` or `nameMy`; a missing or non-`D`-format `routeId`; a
   missing or unknown `direction`; a missing or empty `stopStationIds`, more than 200 of them, or a
   `null` or non-GUID element; a missing or empty `operatingDays`, a repeated day or an unknown day
   name; a missing or malformed `effectiveFrom`; a malformed `effectiveTo`. Nothing is read or
   written.
2. **`400 Timetable.InvalidServiceCode`** — not 2–10 characters of `A`–`Z` and `0`–`9` after
   trimming.
3. **`400 Timetable.InvalidServiceName`** — a name longer than 100 characters after trimming.
4. **`400 Timetable.InvalidEffectivePeriod`** — `effectiveTo` earlier than `effectiveFrom`.
5. **`422 Timetable.ServiceEffectiveToInPast`** — `effectiveTo` earlier than today's Asia/Yangon
   date. `effectiveFrom` may be in the past.
6. **`422 Timetable.ServiceRouteNotFound`** — no route has `routeId` (a `422`, not a `404`: the
   addressed resource is the new service).
7. **`422 Timetable.ServiceRouteInactive`** — the route is inactive.
8. **`422 Timetable.ServiceStopNotOnRoute`** — a stop is not a station of the route (including an
   id that names no station).
9. **`422 Timetable.ServiceStopRepeated`** — a station appears twice among the stops, other than a
   full circuit's closing stop.
10. **`422 Timetable.ServiceTooFewStops`** — fewer than 2 stops, or a full circuit with fewer than
    3 distinct stations.
11. **`422 Timetable.ServiceStopsOutOfOrder`** — the stops do not follow the route order in the
    service's direction. On an open route there is no wrap. On a closed route the stops may wrap
    from the last station to the first (`Forward`) or the first to the last (`Reverse`), and may
    cover at most one circuit; exactly one circuit only as a full circuit whose last stop repeats
    the first.
12. **`422 Timetable.ServiceStopStationInactive`** — a stop's station is inactive. Stations the
    service passes without stopping are not checked.
13. **`409 Timetable.ServiceCodePeriodOverlap`** — checked last, under the code lock: another
    service with the same code has a period that overlaps (inclusive dates; a `null` end is
    unbounded; a service that never runs overlaps nothing). Also the answer to the loser of two
    concurrent creates with one code and overlapping periods.

A withdrawal is checked in this order: `400 Common.ValidationFailed` (a missing or malformed
`withdrawFrom`); `404 Timetable.ServiceNotFound`; then, under the code lock, **`422
Timetable.WithdrawalDateInPast`** (`withdrawFrom` earlier than today's Asia/Yangon date) and
**`422 Timetable.WithdrawalDoesNotShorten`** (the new `effectiveTo`, `withdrawFrom` − 1 day, is not
earlier than the current one; a second withdrawal is allowed when it shortens further). **`409
Timetable.ServiceChangedConcurrently`** is the concurrency-token backstop for a writer that
bypassed the code lock; nothing is written. A withdrawal never causes `ServiceCodePeriodOverlap`,
because it only shortens a period, and it has no route or station guard. "Today" is always the
Asia/Yangon date of the server clock (`Time:LocalTimeZone`, `docs/15`).

A malformed `page`, `pageSize` or `routeId` query value (for example `?routeId=abc`) is a framework
`400` without an `errorCode`, as for every other list endpoint (T-042). A lock timeout is the
opaque `500` (`Common.UnexpectedError`, ADR-0026).

**Request-body limits (REQUIRED CONTROL, spec R31, plan P20).** `POST /api/v1/services` accepts a
body of at most **32 KiB (32,768 bytes)**; the largest valid request — 200 ids, all seven days, a
10-character code and two 100-character names escaped as `\uXXXX` — is 9,280 bytes. `POST
/api/v1/services/{id}/withdraw` accepts at most **1 KiB (1,024 bytes)**; its only valid body is 29
bytes. Both are endpoint metadata applied to Kestrel before the body is read, so a larger body,
with a declared length or chunked, is refused with the framework's `413` before JSON binding.
Malformed JSON is the framework's `400`. Neither carries an `errorCode` (as for `POST /routes`
above).

**Not provided** (OQ42 and OQ48 rulings): no `PATCH` or `PUT /services/{id}` and no other edit (to
change a timetable, withdraw the old service from date D and create a new one with the same code
from D), no reactivation, no `DELETE`, no `/trains`, and no `/schedules/versions*` or time field
(FR-004). Each answers `404` or `405`. The service endpoints appear in the Development-only OpenAPI
document under the tag `Services`.

## Blocked behavior

- **OPEN QUESTION:** fare quote inputs, loop direction, ticket validity, cancellation guards, refund eligibility/amount, and repeat-use handling remain blocked by `docs/19-open-questions.md`.
- **OPEN QUESTION:** `AuthenticUnverified` ticket validation handling is blocked by OQ8.
- **OPEN QUESTION:** whether reprint invalidates earlier prints is blocked by OQ23.

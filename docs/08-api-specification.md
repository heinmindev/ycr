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

## Blocked behavior

- **OPEN QUESTION:** fare quote inputs, loop direction, ticket validity, cancellation guards, refund eligibility/amount, and repeat-use handling remain blocked by `docs/19-open-questions.md`.
- **OPEN QUESTION:** `AuthenticUnverified` ticket validation handling is blocked by OQ8.
- **OPEN QUESTION:** whether reprint invalidates earlier prints is blocked by OQ23.

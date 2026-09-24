# ADR-0024: Client-Held Version for Edits Composed from an Earlier Read

## Status

Proposed — 2026-09-24 (claude, T-032). Needs hein's approval to become Accepted.

## Context

**FACT — `docs/20-coding-conventions.md` §6:** "aggregates that can be changed at the same time (Ticket, CashierSession, Refund) have a `rowversion` concurrency token."

**FACT — the tokens that exist today are server-side only.** `network.Stations.IsActive` is an EF concurrency token (F-001 spec §7, `src/YCR.Application/Network/DeactivateStation/DeactivateStationHandler.cs`) and `identity.Users.RowVersion` is a `rowversion` (`docs/07` §`identity.Users`). In both, the handler loads the row, changes it and saves it inside one request. The token stops two requests that loaded the same row from both saving. No response exposes a token, and no request carries one.

**FACT — that protection does not cover an edit the caller composed from an earlier read.** F-003's sequence change sends the complete new station sequence of a route (`docs/features/F-003-route-management/spec.md` §6). Administrator A reads the route, administrator B saves a new sequence, then A saves a sequence built from what A read. A's request loads B's row, which is current, so a server-side token raises nothing, and B's change is silently lost. The same pattern will recur wherever a client edits a whole document: route sequences now; fare and timetable drafts later (`docs/08` §Initial resources, `PATCH /routes`, `/trains`, `/services`).

**FACT — ADR-0004 §Errors** maps `ErrorType` to HTTP as `Validation` 400, `Unauthorized` 401, `Forbidden` 403, `NotFound` 404, `Conflict` 409, `BusinessRule` 422. It has no 412 or 428.

**FACT — no document defines an API-level concurrency convention.** `docs/08` and `docs/20` §4 say nothing about ETags, `If-Match` or version fields.

## Options considered

1. **Last writer wins.** No change to any contract. Loses a concurrent administrator's edit silently, which for master data that fares and timetables may later read is a data-integrity failure (AGENTS.md rule 5).
2. **HTTP `ETag` / `If-Match`.** The read returns an `ETag`; the edit sends `If-Match`; a stale value gets `412 Precondition Failed`, a missing one `428 Precondition Required`. Standard HTTP, but 412 and 428 are outside ADR-0004's mapping, so it needs an `ErrorType` addition in a superseding ADR, and the SPA must read and send headers as well as bodies.
3. **Version field in the body.** The resource response carries `version`, an opaque string. The edit request carries it back as `expectedVersion`, which is required. A mismatch returns `409 Conflict` with `<Module>.<Resource>VersionMismatch`, inside ADR-0004's mapping as it stands. Less idiomatic HTTP than option 2; visible in OpenAPI like every other field.

## Decision

**Proposed: option 3.**

1. An aggregate that an API client can edit from a representation it read earlier carries a SQL Server `rowversion` column named `RowVersion`, configured as the EF concurrency token.
2. The resource's `*Response` carries `version`: the `rowversion` bytes as a base64 string. Clients treat it as opaque, and it is never parsed or compared for order.
3. An edit request that replaces or modifies state the client composed from a read carries `expectedVersion`, and it is **required**. A missing or malformed value is `400 Common.ValidationFailed`.
4. The handler sets the loaded entity's original `RowVersion` to `expectedVersion` before saving, so EF's `WHERE RowVersion = @expected` is the authority. A mismatch detected before the save, and a `DbUpdateConcurrencyException` raised by the save, both return the same `409 <Module>.<Resource>VersionMismatch`, and nothing is written, including the audit row.
5. A successful edit returns the new `version`, so the client can make a further edit without reading again.
6. **Not required** on action commands whose outcome does not depend on what the caller read, such as `POST /stations/{id}/deactivate`. Those keep their existing server-side tokens.
7. This ADR adds nothing to ADR-0004's mapping. It does not cover `Idempotency-Key` (`docs/20` §5), which answers a different question: whether a retry repeats an operation.

## Consequences

Positive:
- A concurrent administrator's edit can no longer be lost silently. The loser gets a stable error code and re-reads.
- It stays within ADR-0004's error mapping and is visible in the OpenAPI document.
- Later features that edit drafts (fares, timetables) get a pattern to copy.

Negative:
- One more column, and one more required field on every edit request that falls under item 3.
- Option 3 is not how HTTP caches and generic clients understand concurrency. If an HTTP cache or a third-party client is ever placed in front of the API, option 2 may need revisiting through a superseding ADR.

Follow-up work:
- If Accepted, add an "Edits and concurrency" rule to `docs/20` §4 and a note to `docs/08`.
- F-003 is the first user (`docs/features/F-003-route-management/spec.md` R17).

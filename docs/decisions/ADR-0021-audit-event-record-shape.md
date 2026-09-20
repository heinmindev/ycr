# ADR-0021: Audit Event Record Shape

## Status

Accepted — 2026-09-20 (hein)

## Context

**FACT — ADR-0017 §Decision items 1–3:** `audit.AuditEvents` is an append-only SQL Server 2022 ledger table created by raw SQL in an EF migration. Actor fields are populated only from the authenticated server-side context, never from client request fields. The application database login has only INSERT/SELECT on the audit schema.

**FACT — ADR-0017 §Decision item 6:** "Audit schema changes are additive and reviewed; EF migrations must not silently convert or drop a ledger table." The initial column set therefore has to be deliberate, because removing or retyping a column later is not available.

**FACT — `docs/reviews/2026-09-19-starter-kit-review.md` §3 item 14:** "Audit record shape (who/what/when/before/after)" was listed as an engineering decision that could be made immediately. It was never made. ADR-0017 names only `ActorUserId` and `ActorRole`.

**FACT — `docs/20-coding-conventions.md` §2:** audit actions are named `<Module>.<Event>`, for example `Network.StationCreated`.

**FACT — `docs/20-coding-conventions.md` §7:** logs are not audit, and neither may contain passwords, tokens, refresh cookies, full QR payloads, private keys or personal data.

**Blocked by OQ14:** retention, deletion, archival and regulatory handling for append-only ledger rows (ADR-0017 §Blocked behaviour). This ADR defines shape only; it decides nothing about how long rows live.

## Options considered

1. **Minimal shape (who, what, when)** — smallest ledger and fastest inserts, but an investigator cannot tell what changed, and ADR-0017 §6 makes adding the missing detail later an additive-only migration on a ledger table.
2. **Full event envelope with before/after state** — answers "who changed what, from what, to what, in which request", at the cost of wider rows and a duty to keep personal data out of the state payloads.
3. **Per-module audit tables** — narrower typed columns per module, but multiplies ledger DDL and digest administration, and cross-module investigation needs a union.

## Decision

Option 2, one `audit.AuditEvents` ledger table.

### Columns

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | no | PK, application-assigned through `IIdGenerator` (ADR-0006) |
| `OccurredAtUtc` | `datetimeoffset(3)` | no | UTC, from `TimeProvider` (ADR-0018) |
| `Action` | `nvarchar(100)` | no | `<Module>.<Event>`, e.g. `Network.StationCreated` (`docs/20` §2) |
| `ActorUserId` | `uniqueidentifier` | yes | Server-side authenticated context only (ADR-0017 §2). Null only for system-initiated actions with no user. |
| `ActorRole` | `nvarchar(1000)` | yes | **JSON array of the roles the actor held at event time**, e.g. `["Admin","StationManager"]`. Server-side authenticated context only. Null when there is no actor. |
| `SubjectType` | `nvarchar(100)` | no | Aggregate, entity or concept the event is about. Always present, including when `SubjectId` is null. |
| `SubjectId` | `uniqueidentifier` | yes | Identifier of that subject. **Null where the event has no GUID subject**, for example a failed login where no user was resolved. |
| `BeforeJson` | `nvarchar(max)` | yes | Prior state; null on creation events |
| `AfterJson` | `nvarchar(max)` | yes | Resulting state; null on deletion-style events |
| `CorrelationId` | `nvarchar(100)` | no | From `traceparent` (`docs/20` §7) |
| `ClientIp` | `nvarchar(45)` | yes | Caller IP as the server observed it, never a client-supplied header value. Sized for IPv6 including an IPv4-mapped form. Null for system-initiated actions with no request. |
| `ReasonCode` | `nvarchar(100)` | yes | Operator-supplied reason for actions that require one, such as a void or an override. Null when the action needs no reason. |
| `AuthorizedByPermission` | `nvarchar(100)` | yes | The permission that authorized the action, e.g. `stations.manage` (`docs/20` §2 naming). Null for unauthenticated or system-initiated events. |
| `PayloadVersion` | `int` | no | Schema version of `BeforeJson`/`AfterJson`, starting at 1 |

### Constraints and indexes

| Object | Definition | Reason |
|---|---|---|
| `CK_AuditEvents_BeforeJson` | `BeforeJson IS NULL OR ISJSON(BeforeJson) = 1` | A malformed payload in an append-only table cannot be repaired later |
| `CK_AuditEvents_AfterJson` | `AfterJson IS NULL OR ISJSON(AfterJson) = 1` | ″ |
| `CK_AuditEvents_ActorRole` | `ActorRole IS NULL OR ISJSON(ActorRole) = 1` | Enforces that the role list really is a JSON array |
| `IX_AuditEvents_Subject` | nonclustered on `(SubjectType, SubjectId)` | "What happened to this thing" is the main investigation query |
| `IX_AuditEvents_OccurredAtUtc` | nonclustered on `OccurredAtUtc` | Time-window investigation and digest-period scoping |

### Rules

1. The table is created by **raw SQL in an EF migration** as a SQL Server 2022 ledger table, not by EF model building (ADR-0017 §1). The migration fails loudly on an unsupported SQL Server version or edition (ADR-0017 §5).
2. `ActorUserId`, `ActorRole`, `AuthorizedByPermission` and `ClientIp` are populated **only** from the authenticated server-side context and the server's view of the connection. A request that supplies these as body or header fields must not be able to influence them, and a test proves it.
3. `PayloadVersion` exists because ADR-0017 §6 allows only additive schema change. When the JSON shape of a subject changes, the version is incremented rather than the old rows being rewritten; readers switch on it.
4. **REQUIRED CONTROL:** `BeforeJson` and `AfterJson` must never contain passwords, tokens, refresh cookies, full QR payloads or private keys (`docs/20` §7). Each module is responsible for what it puts in its own state payloads.
5. Schema changes to this table are additive and reviewed (ADR-0017 §6). The check constraints and indexes above are created in the same raw-SQL migration as the table, because retro-fitting a check constraint onto a populated ledger table is materially harder than creating it with the table.
6. **`ActorRole` is widened to `nvarchar(1000)`** relative to the single-role `nvarchar(100)` this ADR first proposed. A JSON array of several role names does not reliably fit in 100 characters, and ADR-0017 §6's additive-only rule makes widening a ledger column later expensive. 1000 is chosen to be comfortably beyond any plausible role list rather than to be tight.

## Consequences

Positive:
- An investigator can answer who did what, to which subject, from which state to which state, from which address, under which permission, and under which correlation id.
- A nullable `SubjectId` lets authentication events — failed logins in particular, where no user is resolved — live in the same ledger as business events instead of needing a second mechanism.
- Recording `ActorRole` as the roles held **at event time** keeps the audit trail truthful after a role assignment later changes, which a join to a live roles table would not.
- `AuthorizedByPermission` answers "under what authority" directly, which matters once OQ28 is resolved and role grants start changing.
- The `ISJSON` constraints reject malformed payloads at write time, which is the only time they can be rejected in an append-only table.
- `PayloadVersion` gives an additive-only escape route for state-shape changes, which ADR-0017 §6 otherwise makes painful.
- `ReasonCode` is in place before the first feature that needs an operator justification, so no ledger schema change is needed to introduce it.

Negative:
- `nvarchar(max)` state payloads make the ledger the largest table in the system, which matters more because OQ14 leaves retention undecided. The two nonclustered indexes add to that footprint on every insert.
- Keeping personal data out of `BeforeJson`/`AfterJson` is a standing per-module obligation that only review can enforce.
- Nullable actor and subject fields mean queries must handle system-initiated and subject-less events explicitly.
- `ISJSON` validates only that the text is JSON, not that `ActorRole` is an array rather than an object or a scalar. Enforcing the array shape is the writer's responsibility and belongs in `IAuditWriter` with a test, not in the constraint.

Follow-up work:
- **OQ14** must be answered before production: retention, deletion, archival and regulatory handling for ledger rows.
- Digest generation, WORM storage, scheduling and verification ownership remain open in ADR-0017 §Blocked behaviour.
- Whether `ReasonCode` should be a constrained vocabulary rather than free text can be revisited once the first feature that requires a reason is specified.

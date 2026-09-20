# F-001: Walking skeleton — solution, infrastructure and the `Station` reference slice

Status: **Draft — BLOCKED at the stage-2 ⛔ stop.** Three blocking OPEN QUESTIONs (OQ26, OQ27, OQ28) and eight engineering decisions are unresolved. The spec cannot be Approved until they are answered (`docs/templates/feature-spec.md`; `docs/21-definition-of-done.md` §Specification).

Module(s): `Network` (reference slice); cross-cutting `Audit`, `Identity` (authentication host only), plus solution-wide infrastructure
Related: FR-001, UC "Manage stations" (`docs/03-use-cases.md` §16), ADR-0004, ADR-0005, ADR-0006, ADR-0012, ADR-0016, ADR-0017, ADR-0018, ADR-0019, `docs/20-coding-conventions.md` §3

Decision owner:
- Business rules (station codes, names, role grants): **Myanma Railways**, routed through `hein` (`docs/19-open-questions.md`).
- Engineering decisions: **tech lead (`hein`)**, recorded as ADRs per `docs/decisions/README.md`.

Authoritative sources: `docs/01-functional-requirements.md` §FR-001; `docs/20-coding-conventions.md` §§1–8; `docs/06-system-architecture.md`; ADR-0004/0005/0006/0012/0016/0017/0018/0019; `docs/21-definition-of-done.md`; `docs/reviews/2026-09-19-starter-kit-review.md` §4 item 9 and §5 row D. **No authoritative source exists for station field rules or role grants** — see §3 and §Blocked behaviour.

---

## 0. Discovery notes

Stage-1 output (`docs/workflows/02-feature-development.md` stage 1). Every note cites its source file/section and names the decision owner.

### 0.1 What F-001 is

**FACT — `docs/reviews/2026-09-19-starter-kit-review.md` §4 item 9 and §5 row D:** there is no `src/` in the repository. The recommended walking skeleton is "a compiling solution with one trivial slice (e.g. Stations CRUD), architecture tests, docker-compose with SQL Server and a green CI run", because agents "copy patterns much better than they follow prose".

**FACT — `docs/20-coding-conventions.md` §3:** `CreateStation` is already written as *the* reference slice that "[e]very new slice is modelled on", and the same section states it "is illustrative until the walking skeleton exists". F-001 is therefore the change that converts that illustration into executable, tested code. This is why the slice is `Network`/Station rather than something else.

**FACT — ADR-0017 §Decision item 5:** "The walking skeleton CI must execute the ledger DDL and a smoke test against a pinned SQL Server 2022 container image." This is the only place in the repository that places a named obligation on F-001 specifically, and it makes CI and the audit ledger in scope rather than optional.

**FACT — ADR-0012 §Enforcement:** whether a Roslyn analyzer is needed "remains open until the walking skeleton demonstrates the test coverage". F-001's architecture tests are the evidence that lets that question be closed later; F-001 does not itself resolve it.

**ENGINEERING DECISION (tech lead, cite ADR-0005):** the solution is backend only. No SPA work is in F-001 (`docs/06-system-architecture.md` §Module layout: "`YCR.Web` was removed").

### 0.2 Repository facts observed during discovery

| # | Observation | Source | Consequence for F-001 |
|---|---|---|---|
| D1 | Target platform is "C# / ASP.NET Core 10", EF Core, SQL Server, Docker | `README.md` §Technology | Taken as the platform of record. The exact SDK pin is E2. |
| D2 | No `src/`, `tests/`, `YCR.sln`, `docker-compose.yml`, CI workflow, `global.json`, `.editorconfig` or `.gitignore` exists | tracked files at commit `b6b6576`: only `AGENTS.md`, `CLAUDE.md`, `README.md`, `TASKS.md`, `.gitleaks.toml`, `docs/` | Every command in AGENTS.md §Commands is currently invalid. F-001 makes them valid. |
| D3 | `.gitleaks.toml` exists but no CI invokes it | repository root | Secret scanning must be wired into the F-001 CI job (`docs/21` §Security). |
| D4 | The repository trunk branch is `master`. `TASKS.md` §Protocol and rows T-001/T-009 say `main` | `git branch` in the coordination checkout | Process defect, not a code defect. Raised in §Notes; the T-002 claim ref was created from `master`. |
| D5 | `docs/glossary.md` is referenced as binding vocabulary but does not exist | `docs/20-coding-conventions.md` §2; `TASKS.md` T-010 is an unclaimed human decision | `docs/21` §Documentation requires the glossary to be updated. F-001 cannot satisfy that item until T-010 is decided. Recorded as a Definition-of-Done gap, not an F-001 blocker. |

### 0.3 Contradictions found (to be resolved, not worked around)

| # | Contradiction | Sources | Proposed resolution | Owner |
|---|---|---|---|---|
| C1 | `docs/10-authorization-matrix.md` grants "Manage stations" to Admin and Railway Admin in its table, but the same file's §"Permission inventory requiring explicit approval" lists `stations.manage` and states "[t]heir role grants are **OPEN QUESTION** until the authorization matrix is approved; agents must not infer grants from the names." | `docs/10` §table vs §inventory | The file contradicts itself. Treated as unresolved → **OQ28**. F-001 must not seed any role→permission grant. | Myanma Railways |
| C2 | The permission inventory contains `stations.manage` but no `stations.read`, while the matrix gives Auditor a "view" right over station management. | `docs/10` §table ("view") vs §inventory | Read authorization for `GET /stations` is undefined → folded into **OQ28**. | Myanma Railways |
| C3 | AGENTS.md §Commands documents `docker compose up -d` as "SQL Server 2022 for local dev **and integration tests**", while `docs/20` §3 requires handler tests to run "against real SQL Server (Testcontainers `mssql/server:2022`)". | `AGENTS.md` §Commands vs `docs/20` §3 | Not a true conflict: compose serves local development, Testcontainers serves automated tests. Both are in scope; the shared constraint is one pinned 2022 image reference (E4). Documented, not escalated. | tech lead |
| C4 | `docs/06` lists six test projects (`Domain`, `Application`, `Infrastructure`, `Api`, `Integration`, `Architecture`); `docs/20` §3 names only four as required per slice. | `docs/06` §Projects vs `docs/20` §3 | Create all six per `docs/06`; populate the four named in `docs/20` §3 plus `YCR.Infrastructure.Tests` for the ADR-0006 fragmentation control. `YCR.IntegrationTests` is created empty in F-001. | tech lead |
| C5 | `docs/20` §3 fixes `HasMaxLength(10)` for `Code` and `HasMaxLength(100)` for names, but the same section says station field rules "are OPEN QUESTIONS until the authoritative station list arrives". | `docs/20` §3 | The lengths are illustration, not an approved rule. Adopting them silently would violate AGENTS.md rule 1 and `docs/20` §8. → **OQ26**, **OQ27**. | Myanma Railways |
| C6 | `docs/21` §Documentation requires the glossary to be updated; the glossary does not exist and its creation is an undecided human task (T-010). | `docs/21` vs `TASKS.md` T-010 | F-001 cannot close that DoD item unilaterally. Flagged for the approver. | tech lead |

### 0.4 What discovery did **not** find

No document in `docs/` defines: the station code format; whether the Myanmar-script station name is mandatory; which roles hold `stations.manage`; the column shape of `audit.AuditEvents`; a CI provider; a pinned SQL Server image tag or digest; or the two-credential database model that ADR-0017 §3 requires. The first three are business rules (§3, OQ26–OQ28). The rest are engineering decisions (E3, E4, E7, E8).

---

## 1. Goal

Give every later feature an executable pattern to copy: a compiling, tested `YCR.sln` with the layer and module structure of `docs/06`, the cross-cutting infrastructure of ADR-0004/0006/0012/0017/0018, and one end-to-end vertical slice over `network.Stations` — endpoint → validator → handler → domain → EF → SQL Server — with architecture tests, an audit ledger proven in CI against a pinned SQL Server 2022 image, and a green build.

---

## 2. Actors and permissions

| Actor | Permission | Notes |
|---|---|---|
| Railway/system administrator | `stations.manage` | Creates and deactivates stations. **Which roles hold this permission is OQ28**; F-001 enforces the permission on the endpoint but must not seed any role→permission grant. |
| Any authenticated staff user reading stations | *undefined* | `GET /stations` authorization is **blocked by OQ28** (C2). No read endpoint ships with a guessed permission. |
| Anonymous | none | `GET /health/live` and `GET /health/ready` only, with an explicit `.AllowAnonymous()` and a comment giving the reason (`docs/20` §4). |

---

## 3. Business rules

| # | Rule | Label | Source |
|---|---|---|---|
| R1 | Administrators can create, update, activate/deactivate, search and view stations. F-001 implements **create, deactivate, get-by-id and list** only; update/search/reactivate are deferred (§9). | FACT | `docs/01-functional-requirements.md` §FR-001 |
| R2 | A station has a human-facing station code that is unique across stations. | FACT | ADR-0006 §3 (`StationCode` listed as a human-facing identifier with a unique constraint) |
| R3 | The **format** of a station code — character set, length, case, and whether a code may be reused after deactivation — is undefined. | **OPEN QUESTION — OQ26 (blocking)** | ADR-0006 §3 "format: OPEN QUESTION, from authoritative station list"; `docs/20` §3; OQ1 |
| R4 | A station has a name in English and a name in Myanmar script, stored as Unicode, never Zawgyi. | FACT for the storage encoding; **OPEN QUESTION — OQ27 (blocking)** for whether the Myanmar name is mandatory, whether names must be unique, and maximum lengths | `docs/20` §6 ("Never store Zawgyi") is FACT; `docs/20` §3 `BilingualName.Create(en, my)` and its `HasMaxLength(100)` are illustration only, per that same section |
| R5 | A newly created station is active. | ENGINEERING DECISION (tech lead, cite `docs/20` §3 reference slice, where `Station.Create(...)` sets `IsActive = true`) | `docs/20` §3 |
| R6 | Deactivating an already-inactive station is rejected with `Network.StationAlreadyInactive`. | ENGINEERING DECISION (tech lead, cite `docs/20` §3 reference slice `Station.Deactivate()`) | `docs/20` §3 |
| R7 | A duplicate station code is rejected with `Network.StationCodeAlreadyExists`, and the database unique index — not only the pre-check — is the authority under concurrency. | ENGINEERING DECISION (tech lead, cite ADR-0004 §Errors: "[a] DB unique-constraint violation on a business key is caught and mapped to `Conflict`") | `docs/20` §3; ADR-0004 |
| R8 | Which roles may manage stations, and whether a separate read permission exists for stations. | **OPEN QUESTION — OQ28 (blocking)** | `docs/10` §table vs §inventory (C1, C2) |
| R9 | Station identifiers are application-generated GUIDs, SQL-Server-ordered through `IIdGenerator`, mapped `ValueGeneratedNever()`. | ENGINEERING DECISION (tech lead, cite ADR-0006 §4 and its 2026-09-19 amendment) | ADR-0006 |
| R10 | Creating and deactivating a station are business-significant actions and write audit events `Network.StationCreated` and `Network.StationDeactivated`, with actor fields taken only from the authenticated server-side context. | ENGINEERING DECISION (tech lead, cite ADR-0017 §Decision items 1–2); naming from `docs/20` §2 | ADR-0017; `docs/20` §§2, 7 |
| R11 | Station management is not a financial or retryable command; no `Idempotency-Key` is required. | ENGINEERING DECISION (tech lead, cite `docs/20` §5, whose required list is sell / cancel / refund / payment / cashier-session and does not include station management) | `docs/20` §5 |
| R12 | Instants are `DateTimeOffset` in code and `datetimeoffset(3)` in SQL, read only through `TimeProvider`. Stations own no `BusinessDate` — ADR-0019's rule table covers Sale, Payment, cancellation and Refund only. | ENGINEERING DECISION (tech lead, cite ADR-0018 §Time and ADR-0019 §Decision) | ADR-0018; ADR-0019 |

**Blocking open questions:** **OQ26** (station code format), **OQ27** (station name rules), **OQ28** (who may manage and read stations). All three are newly registered in `docs/19-open-questions.md`. While they are open this spec **cannot be Approved** (`docs/workflows/02-feature-development.md` stage 2 exit criteria; `docs/21` §Specification).

---

## 4. Scenarios (Given / When / Then)

Scenarios marked **[B]** cannot be written as executable tests until the named OQ is answered. The infrastructure scenarios (S14–S23) are unaffected.

### Station slice — happy path

- **S1.** Given an authenticated user holding `stations.manage`, when they POST a valid station, then the response is `201 Created` with `Location: /api/v1/stations/{id}` and body `{ "id": "<guid>" }`, the row exists in `network.Stations` with `IsActive = 1`, and one `Network.StationCreated` audit event is present. **[B — OQ26/OQ27 define "valid"]**
- **S2.** Given an existing active station, when a permitted caller POSTs `/stations/{id}/deactivate`, then the response is `204 No Content`, `IsActive = 0`, and one `Network.StationDeactivated` audit event is written.
- **S3.** Given three stations exist, when a permitted caller GETs `/stations?page=1&pageSize=50`, then the response is `200` with `items`, `page`, `pageSize` and `totalCount` (`docs/20` §4). **[B — OQ28 defines the read permission]**
- **S4.** Given a station exists, when a permitted caller GETs `/stations/{id}`, then the response is `200` with a `StationResponse` and **no EF entity** is serialized (AGENTS.md rule 4). **[B — OQ28]**

### Station slice — failures, each with its error code

- **S5.** Duplicate code detected by the pre-check → `409` ProblemDetails, `errorCode = Network.StationCodeAlreadyExists`. **[B — OQ26]**
- **S6.** Deactivate an already-inactive station → `422`, `errorCode = Network.StationAlreadyInactive` (ADR-0004: `BusinessRule` → 422).
- **S7.** GET an unknown id → `404`, `errorCode = Network.StationNotFound`.
- **S8.** Request body failing validation (blank code, missing required name) → `400` ProblemDetails carrying `errorCode` and `traceId`. **[B — OQ26/OQ27 define the validation rules]**
- **S9.** `pageSize=201` → `400` (max 200, `docs/20` §4).
- **S10.** Anonymous POST `/stations` → `401`.
- **S11.** Authenticated user **without** `stations.manage` POSTs `/stations` → `403`. (`docs/21` §Tests requires both S10 and S11.)

### Concurrency and data integrity

- **S12.** Two parallel POSTs with the same code → exactly one `201`; the other returns `409 Network.StationCodeAlreadyExists` through the unique-index violation mapped per ADR-0004, and exactly one row exists. **[B — OQ26]**
- **S13.** Myanmar-script name round-trip: the value read back is identical to the value written (`nvarchar`, Unicode, `docs/20` §6). **[B — OQ27]**

### Infrastructure — not blocked by any open question

- **S14.** `dotnet build YCR.sln` and `dotnet test YCR.sln` both succeed from a clean clone, with no skipped tests (`docs/21` §Tests).
- **S15.** Architecture tests **fail** on each forbidden dependency and pass otherwise (ADR-0012 §Enforcement; `docs/21` §Code requires negative cases): (a) `YCR.Application.Network` referencing `ITicketingDbContext`; (b) `YCR.Application.Network` referencing a `YCR.Domain.Ticketing` type; (c) a Reporting type resolving a write context; (d) `YCR.Api` containing domain logic; (e) an endpoint returning an EF entity type.
- **S16.** Every registered module context interface resolves to the *same* scoped `YcrDbContext` instance within one request scope (ADR-0012 §Decision item 3).
- **S17.** Migrations applied against the pinned `mssql/server:2022` image create `network.Stations` and the `audit.AuditEvents` **ledger** table, and the ledger DDL executes successfully (ADR-0017 §Decision item 5).
- **S18.** The audit migration **fails loudly** on an unsupported SQL Server version or edition rather than silently degrading to a normal table (ADR-0017 §Decision item 5; AGENTS.md rule 8).
- **S19.** An audit event's `ActorUserId`/`ActorRole` come from the authenticated context; a request supplying actor fields in its body cannot influence them (ADR-0017 §Decision item 2).
- **S20.** `IIdGenerator` fragmentation control: insert 10,000 rows into the clustered GUID key and compare index fragmentation with a `NEWSEQUENTIALID()` baseline; pass when no more than 10 percentage points worse. The test records row count, index name, fragmentation and database compatibility level (ADR-0006 §REQUIRED CONTROL).
- **S21.** `GET /health/live` and `GET /health/ready` return `200` anonymously, and readiness reports SQL Server connectivity (`docs/02` §Reliability).
- **S22.** An unhandled exception returns `500` as ProblemDetails with a `traceId` and **no** internal detail (ADR-0004 §Errors).
- **S23.** CI runs build, tests (against the pinned SQL Server 2022 image) and the `.gitleaks.toml` secret scan, and fails the pipeline on any failure (D3; `docs/21` §Security).

---

## 5. State changes

| Entity | From | Event | Guard | To |
|---|---|---|---|---|
| Station | (none) | `CreateStation` | code satisfies OQ26 rules and is not already used; name satisfies OQ27 rules | Active |
| Station | Active | `DeactivateStation` | caller holds `stations.manage` (OQ28) | Inactive |
| Station | Inactive | `DeactivateStation` | — | rejected, `Network.StationAlreadyInactive` (R6) |

Reactivation is out of scope (§9); FR-001's "activate" is deferred to the follow-up feature.

---

## 6. API

Base path `/api/v1`, JSON camelCase, GUID resource ids, RFC 9457 ProblemDetails with `errorCode` and `traceId` (`docs/20` §4; ADR-0004).

| Method | Path | Request | Success | Error codes | Permission | Idempotency |
|---|---|---|---|---|---|---|
| POST | `/stations` | `CreateStationRequest { code, nameEn, nameMy }` | `201` + `CreateStationResponse { id }` and a `Location` header | `400` validation · `401` · `403` · `409 Network.StationCodeAlreadyExists` | `stations.manage` | No (R11) |
| POST | `/stations/{id}/deactivate` | — | `204` | `401` · `403` · `404 Network.StationNotFound` · `422 Network.StationAlreadyInactive` | `stations.manage` | No (R11) |
| GET | `/stations/{id}` | — | `200` + `StationResponse { id, code, nameEn, nameMy, isActive, createdAtUtc }` | `401` · `403` · `404` | **blocked, OQ28** | n/a |
| GET | `/stations` | `?page=1&pageSize=50` (max 200) | `200` + `{ items, page, pageSize, totalCount }` | `400` · `401` · `403` | **blocked, OQ28** | n/a |
| GET | `/health/live` | — | `200` | — | `.AllowAnonymous()` — a liveness probe must answer before auth is reachable | n/a |
| GET | `/health/ready` | — | `200` / `503` | — | `.AllowAnonymous()` — readiness probe used by the reverse proxy | n/a |

The exact request-field shape is provisional until OQ26/OQ27 fix the field rules. `docs/08-api-specification.md` lists `PATCH /stations`; update is deferred (§9).

---

## 7. Data

Schema `network` (`docs/07` §Module schemas; `docs/20` §2).

**`network.Stations`**

| Column | Type | Notes |
|---|---|---|
| `Id` | `uniqueidentifier` | PK, application-assigned, `ValueGeneratedNever()` (ADR-0006, R9) |
| `Code` | `nvarchar(n)` | **unique index**; `n` and the character-set constraint are **blocked by OQ26** |
| `NameEn` | `nvarchar(n)` | owned value object `BilingualName`; length **blocked by OQ27** |
| `NameMy` | `nvarchar(n)` | Unicode, never Zawgyi (`docs/20` §6); nullability **blocked by OQ27** |
| `IsActive` | `bit` | not null |
| `CreatedAtUtc` | `datetimeoffset(3)` | UTC value (ADR-0018) |

No `rowversion` concurrency token: `docs/20` §6 requires one for Ticket, CashierSession and Refund; Station is not in that list, and F-001 has no concurrent-mutation path beyond the unique index. **ENGINEERING DECISION (tech lead, cite `docs/20` §6) — to be confirmed at approval.**

**`audit.AuditEvents`** — append-only SQL Server 2022 ledger table, created by **raw SQL in an EF migration** rather than by EF model building (ADR-0017 §Decision item 1). Its column shape is undefined anywhere in `docs/` and is engineering decision **E8**.

**Not in F-001:** `IdempotencyRecords` (no financial command in scope, R11), `identity.AuthSessions` (depends on E1), and every other table in `docs/07` §Core tables.

Migrations are named `YYYYMMDD_<Module>_<Change>` and reviewed per `docs/workflows/04-database-change.md` (`docs/20` §6).

---

## 8. Audit, logging, metrics

- **Audit** (ADR-0017): `Network.StationCreated` and `Network.StationDeactivated` through `IAuditWriter`, written in the same `SaveChangesAsync` as the business change, with before/after state. Actor fields come only from the authenticated server context (S19). The application database login gets **INSERT/SELECT only** on the `audit` schema; migration and digest administration use separate credentials (ADR-0017 §Decision item 3) — see **E7**.
- **Logging** (`docs/20` §7): `ILogger` with message templates, never interpolation. Correlation from `traceparent`. Never log passwords, tokens, refresh cookies, QR payloads, keys or personal data.
- **Metrics** (`docs/17`): F-001 ships the baseline request-latency, error-rate and database-latency instrumentation that later features extend. None of `docs/17`'s listed business events belongs to the Network module, so no business metric is added here.
- **Out of scope:** digest generation, WORM storage, scheduling and alert routing — ADR-0017 §Blocked behaviour leaves the provider, schedule and ownership open.

---

## 9. Out of scope

Update/rename a station (`PATCH /stations`), reactivation, search and filtering beyond simple pagination, `Routes`/`RouteStations` and the remaining tables in `docs/07` §Core tables, the full ADR-0016 authentication stack beyond what **E1** decides, `IdempotencyRecords`, `YCR.Worker` behaviour (the project is created empty), the SPA and its CSP/dependency-audit controls (ADR-0016 §Browser security — no frontend exists yet), ledger digest operations, seed/fixture station data (blocked by OQ1), and the creation of `docs/glossary.md` (human decision T-010).

Renaming a station raises a data-history question — does a rename rewrite historical station names on already-issued tickets? — that the follow-up feature must resolve before implementing `PATCH`.

---

## Blocked behaviour

Required by `docs/21` §Specification. No placeholder rule is implemented for any item below.

| Behaviour | Blocking item | Effect on F-001 |
|---|---|---|
| `StationCode` value-object validation | **OQ26** | `StationCode.Create` cannot be written. S1, S5, S8 and S12 cannot be finalised. |
| `BilingualName` validation, column nullability and length | **OQ27** | S1, S8 and S13 cannot be finalised; the `Stations` DDL cannot be fixed. |
| Authorization for `GET /stations` and `GET /stations/{id}`; all role→permission grants | **OQ28** | The read endpoints cannot ship. `docs/10` cannot be updated with a grant. S3 and S4 cannot be finalised. |
| Role/permission **seed data** | **OQ28** | No seeding in F-001. Tests must mint permissions directly rather than through seeded roles. |
| Ledger digest schedule, storage and verification | ADR-0017 §Blocked behaviour | Out of scope (§9); F-001 proves the DDL only. |
| Glossary update required by `docs/21` §Documentation | T-010 (undecided) | That DoD item cannot be closed by F-001. The approver must waive it or decide T-010 first. |

---

## Engineering decisions required before stage 3

These are not business rules, so per `TASKS.md` §Protocol item 8 they are engineering blockers and do **not** go in `docs/19`. Each needs a tech-lead decision at the ⛔ stop; those marked *(ADR)* should be recorded as a new ADR per `docs/decisions/README.md`.

| # | Decision | Why it is needed | Recommendation |
|---|---|---|---|
| **E1** | *(ADR)* How much authentication ships in F-001. | `docs/21` §Tests requires 401 and 403 tests, so endpoints must be genuinely protected. ADR-0016's full stack (JWT with `sub`/`sid` only, `identity.AuthSessions`, hashed rotating refresh cookies, the 20-second predecessor grace, ≤30-second revocation latency, Origin checks) is a feature in its own right and would dominate the skeleton. | Ship the **permission-based authorization pipeline** (`Permissions` constants, policy registration, `.RequireAuthorization(...)` on every endpoint) with a **test-only authentication handler**, and defer the ADR-0016 token and refresh implementation to a dedicated follow-up feature. Record the deferral in the new ADR so no later agent mistakes the shim for the design. Alternative: implement ADR-0016 in full and accept a much larger F-001. |
| **E2** | SDK and target-framework pin. | `README.md` says ASP.NET Core 10 but nothing pins an SDK, and CI and local builds must agree. | Add `global.json` with a pinned SDK and `rollForward`, plus `.gitignore`, `.editorconfig`, `Directory.Build.props` (nullable enabled, warnings as errors) and `Directory.Packages.props` for central package management. |
| **E3** | CI provider and workflow location. | ADR-0017 §5 mandates a CI job; none exists (D2). | GitHub Actions at `.github/workflows/ci.yml`: restore → build → test (Testcontainers, pinned image) → gitleaks. Confirm the remote host before committing to a provider, since the repository currently has no remote. |
| **E4** | The pinned SQL Server 2022 image reference. | ADR-0017 §5 requires it to be pinned, and C3 means compose and Testcontainers must use the same one. | Pin by **digest** rather than a floating tag, in one shared constant consumed by both `docker-compose.yml` and the test fixture. |
| **E5** | Scope of the ADR-0006 fragmentation control in F-001. | It is a REQUIRED CONTROL, but a 10,000-row insert lengthens every CI run. | Implement it in `YCR.Infrastructure.Tests` within F-001, in a test category that CI runs on the trunk build. Decide separately whether it also runs on every pull request. |
| **E6** | *(VERIFY)* Ledger support in the chosen image's edition, and the exact ledger DDL. | ADR-0017 §Blocked behaviour already carries this as a VERIFY, and F-001 is where it gets verified. | Verify against the E4 image during stage 4 and record the result in `progress.md`. If the edition does not support ledger tables, stop and return to stage 2 rather than substituting a normal table. |
| **E7** | The two-credential database model. | ADR-0017 §3: the application login has only INSERT/SELECT on `audit`, and migrations use a separate credential. This shapes connection-string handling, compose, CI and the test fixture. | Two connection strings from the start (`Migrator`, `Application`) with the least-privilege grants scripted in the migration. Retrofitting this later is expensive, and it is a REQUIRED CONTROL. |
| **E8** | *(ADR)* The `audit.AuditEvents` column shape. | Flagged as an open engineering decision in `docs/reviews/2026-09-19-starter-kit-review.md` §3 item 14 and never resolved; ADR-0017 names only `ActorUserId` and `ActorRole`. ADR-0017 §6 requires audit schema changes to be additive, so the initial shape must be deliberate. | Decide it as a small ADR before stage 3. At minimum: id, occurred-at UTC, action (`<Module>.<Event>`), actor user id, actor role, subject type, subject id, before/after JSON, and correlation/trace id. |

---

## Notes for the ledger and the next stage

- **D4 (process):** `TASKS.md` §Protocol and rows T-001/T-009 refer to branch `main`, but the repository trunk is `master`. The T-002 claim ref was created as `claim/T-002` from `master`. A human should either correct the protocol text or rename the branch; agents should not do either unilaterally.
- **Next stage (T-003, PLAN)** must not start until a human has answered OQ26, OQ27 and OQ28 and decided E1–E8, because E1, E7 and E8 change the project and migration layout that the plan would enumerate.

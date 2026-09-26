# F-001: Walking skeleton — solution, infrastructure and the `Station` reference slice

Status: **Approved (hein, 2026-09-20)**. The station field rules (R3, R4) and station permissions (R8) were provisional, skeleton-only ASSUMPTIONs pending OQ26–OQ28; **T-014 (hein, 2026-09-22) closed that gap with a final tech-lead ruling** — not a Myanma Railways answer, but this project chose not to wait for one. See §Blocked behaviour.

**Amendment 1 — 2026-09-20 (hein), raised by the T-003 plan review.** Scenario **S27** added to §4 (concurrent deactivation), and §7 records that `IsActive` is an EF concurrency token. No business rule changed; both are concurrency mechanics the plan review surfaced.

Module(s): `Network` (reference slice); cross-cutting `Audit`, `Identity` (authentication host only), plus solution-wide infrastructure
Related: FR-001, core use case 1 "Manage stations" (`docs/03-use-cases.md`), ADR-0004, ADR-0005, ADR-0006, ADR-0012, ADR-0016, ADR-0017, ADR-0018, ADR-0019, ADR-0020, ADR-0021, `docs/20-coding-conventions.md` §3

Decision owner:
- Business rules (station codes, names, role grants): **Myanma Railways** remains the authoritative decision owner, routed through `hein` (`docs/19-open-questions.md`). Myanma Railways has not answered OQ26–OQ28, so `hein` made a **final tech-lead ruling** (T-014, 2026-09-22) so F-001 is not blocked indefinitely; it is **not** a Myanma Railways decision and a later Myanma Railways answer, if different, supersedes it.
- Engineering decisions: **tech lead (`hein`)**, recorded as ADRs per `docs/decisions/README.md`.

Authoritative sources: `docs/01-functional-requirements.md` §FR-001; `docs/20-coding-conventions.md` §§1–8; `docs/06-system-architecture.md`; ADR-0004/0005/0006/0012/0016/0017/0018/0019; `docs/21-definition-of-done.md`; `docs/reviews/2026-09-19-starter-kit-review.md` §4 item 9 and §5 row D. **No Myanma Railways-authoritative source exists for station field rules or role grants** — T-014's tech-lead ruling (hein, 2026-09-22) stands in instead; see §3, §Blocked behaviour, `docs/19-open-questions.md` OQ26–OQ28 and `docs/10-authorization-matrix.md`.

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
| D4 | The repository trunk branch was `master`, while `TASKS.md` §Protocol and rows T-001/T-009 say `main` | `git branch` in the coordination checkout | **Resolved by hein 2026-09-20:** the trunk is being renamed `master` → `main`, so the Protocol text becomes correct. The `claim/T-002` ref was created from `master` before the rename. |
| D5 | `docs/glossary.md` is referenced as binding vocabulary but does not exist | `docs/20-coding-conventions.md` §2; `TASKS.md` T-010 | **Resolved by hein 2026-09-20:** T-010 decided yes; the glossary is being written in parallel with F-001. F-001 contributes the Network-module vocabulary it introduces. |

### 0.3 Contradictions found and how they were resolved

| # | Contradiction | Sources | Resolution | Owner |
|---|---|---|---|---|
| C1 | `docs/10-authorization-matrix.md` grants "Manage stations" to Admin and Railway Admin in its table, but the same file's §"Permission inventory requiring explicit approval" lists `stations.manage` and states "[t]heir role grants are **OPEN QUESTION** until the authorization matrix is approved; agents must not infer grants from the names." | `docs/10` §table vs §inventory | **Resolved by hein 2026-09-20:** the inventory governs and the table is labelled a proposal. `docs/10` updated accordingly. No role→permission grant is seeded in F-001; tests mint permissions directly. **The underlying role question, OQ28, is resolved by T-014's final tech-lead ruling (hein, 2026-09-22):** `stations.manage` → Admin and Railway Admin. | Myanma Railways (open; tech-lead ruling stands in) |
| C2 | The permission inventory contains `stations.manage` but no `stations.read`, while the matrix gives Auditor a "view" right over station management. | `docs/10` §table ("view") vs §inventory | **Resolved by hein 2026-09-20:** `stations.read` added to the `docs/10` inventory. **Which roles hold it is resolved by T-014's final tech-lead ruling (hein, 2026-09-22):** any authenticated operator role (Admin, Railway Admin, Station Manager, Operator, Inspector, Finance, Auditor). | Myanma Railways (open; tech-lead ruling stands in) |
| C3 | AGENTS.md §Commands documents `docker compose up -d` as "SQL Server 2022 for local dev **and integration tests**", while `docs/20` §3 requires handler tests to run "against real SQL Server (Testcontainers `mssql/server:2022`)". | `AGENTS.md` §Commands vs `docs/20` §3 | Not a true conflict: compose serves local development, Testcontainers serves automated tests. Both are in scope; the shared constraint is one pinned 2022 image reference (E4). | tech lead — closed |
| C4 | `docs/06` lists six test projects (`Domain`, `Application`, `Infrastructure`, `Api`, `Integration`, `Architecture`); `docs/20` §3 names only four as required per slice. | `docs/06` §Projects vs `docs/20` §3 | Create all six per `docs/06`; populate the four named in `docs/20` §3 plus `YCR.Infrastructure.Tests` for the ADR-0006 fragmentation control. `YCR.IntegrationTests` is created empty in F-001. | tech lead — closed |
| C5 | `docs/20` §3 fixes `HasMaxLength(10)` for `Code` and `HasMaxLength(100)` for names, but the same section says station field rules "are OPEN QUESTIONS until the authoritative station list arrives". | `docs/20` §3 | **Resolved by hein 2026-09-20** as a provisional skeleton-only ASSUMPTION (R3, R4), matching those lengths; **made final by T-014 (hein, 2026-09-22)** as a tech-lead ruling, not a Myanma Railways answer. OQ26/OQ27 stay open in `docs/19-open-questions.md`, resolved-by-tech-lead-ruling; a later Myanma Railways answer would supersede it. | Myanma Railways (open; tech-lead ruling stands in) |
| C6 | `docs/21` §Documentation requires the glossary to be updated; the glossary did not exist and its creation was an undecided human task (T-010). | `docs/21` vs `TASKS.md` T-010 | **Resolved by hein 2026-09-20:** T-010 decided yes, glossary written in parallel. F-001 adds the Network vocabulary it introduces, so the DoD item is satisfiable. | tech lead — closed |

### 0.4 What discovery did **not** find

No document in `docs/` defines: the station code format; whether the Myanmar-script station name is mandatory; which roles hold `stations.manage`; the column shape of `audit.AuditEvents`; a CI provider; a pinned SQL Server image tag or digest; or the two-credential database model that ADR-0017 §3 requires. The first three are business rules; §3 recorded provisional assumptions at discovery time, later made final by T-014's tech-lead ruling (hein, 2026-09-22) over OQ26–OQ28 — see `docs/19-open-questions.md`. The rest are settled as engineering decisions E3, E4, E7 and E8.

---

## 1. Goal

Give every later feature an executable pattern to copy: a compiling, tested `YCR.sln` with the layer and module structure of `docs/06`, the cross-cutting infrastructure of ADR-0004/0006/0012/0017/0018, and one end-to-end vertical slice over `network.Stations` — endpoint → validator → handler → domain → EF → SQL Server — with architecture tests, an audit ledger proven in CI against a pinned SQL Server 2022 image, and a green build.

---

## 2. Actors and permissions

| Actor | Permission | Notes |
|---|---|---|
| Railway/system administrator | `stations.manage` | Creates and deactivates stations. |
| Staff user reading stations | `stations.read` | Added to the `docs/10` inventory by hein 2026-09-20 (C2). |
| Anonymous | none | `GET /health/live` and `GET /health/ready` only, with an explicit `.AllowAnonymous()` and a comment giving the reason (`docs/20` §4). |

**Approved role grants (T-014, hein, 2026-09-22 — tech-lead ruling, not a Myanma Railways answer):** `stations.manage` → Admin and Railway Admin only; `stations.read` → any authenticated operator role (Admin, Railway Admin, Station Manager, Operator, Inspector, Finance, Auditor). See `docs/10-authorization-matrix.md`. **F-001 still seeds no role→permission grants in code** — no identity/role provisioning system exists until the ADR-0016 follow-up feature ships (ADR-0020). Tests mint the permission they need directly on the test principal.

---

## 3. Business rules

| # | Rule | Label | Source |
|---|---|---|---|
| R1 | Administrators can create, update, activate/deactivate, search and view stations. F-001 implements **create, deactivate, get-by-id and list** only; update/search/reactivate are deferred (§9). | FACT | `docs/01-functional-requirements.md` §FR-001 |
| R2 | A station has a human-facing station code that is unique across stations. | FACT | ADR-0006 §3 (`StationCode` listed as a human-facing identifier with a unique constraint) |
| R3 | A station code is 2–10 characters, drawn only from `A`–`Z` and `0`–`9`, unique across **all** stations including inactive ones, and **never reused** once assigned. | **DECISION (tech lead, hein, 2026-09-22) — final; not a Myanma Railways answer** | Not a Myanma Railways decision; a later Myanma Railways answer, if different, supersedes it (own follow-up task). Scoped to `StationCode`. Underlying question: OQ26 (resolved by tech-lead ruling in `docs/19-open-questions.md`), related OQ1 |
| R4 | `NameEn` and `NameMy` are both required, each 1–100 characters after trimming, and neither is unique. Both are stored as Unicode `nvarchar`, never Zawgyi. | "Unicode, never Zawgyi" is **FACT** (`docs/20` §6). The required/length/uniqueness rules are **DECISION (tech lead, hein, 2026-09-22) — final; not a Myanma Railways answer** | Not a Myanma Railways decision; a later Myanma Railways answer, if different, supersedes it (own follow-up task). Scoped to `BilingualName`. Underlying question: OQ27 (resolved by tech-lead ruling in `docs/19-open-questions.md`) |
| R5 | A newly created station is active. | ENGINEERING DECISION (tech lead, cite `docs/20` §3 reference slice, where `Station.Create(...)` sets `IsActive = true`) | `docs/20` §3 |
| R6 | Deactivating an already-inactive station is rejected with `Network.StationAlreadyInactive`. | ENGINEERING DECISION (tech lead, cite `docs/20` §3 reference slice `Station.Deactivate()`) | `docs/20` §3 |
| R7 | A duplicate station code is rejected with `Network.StationCodeAlreadyExists`, and the database unique index — not only the pre-check — is the authority under concurrency. | ENGINEERING DECISION (tech lead, cite ADR-0004 §Errors: "[a] DB unique-constraint violation on a business key is caught and mapped to `Conflict`") | `docs/20` §3; ADR-0004 |
| R8 | Managing stations requires `stations.manage`; reading them requires `stations.read`. The approved role grants are `stations.manage` → Admin and Railway Admin only; `stations.read` → any authenticated operator role (Admin, Railway Admin, Station Manager, Operator, Inspector, Finance, Auditor). F-001 still seeds no grants in code — no identity/role provisioning system exists until the ADR-0016 follow-up feature ships. | **DECISION (tech lead, hein, 2026-09-22) — final; not a Myanma Railways answer** for the permission constants and role grants. The code-level absence of seeded grants is now an engineering sequencing fact (ADR-0020, ADR-0016 follow-up), not a business-rule placeholder. | `docs/10` §table and §inventory (updated 2026-09-22); T-014; C1, C2 |
| R9 | Station identifiers are application-generated GUIDs, SQL-Server-ordered through `IIdGenerator`, mapped `ValueGeneratedNever()`. | ENGINEERING DECISION (tech lead, cite ADR-0006 §4 and its 2026-09-19 amendment) | ADR-0006 |
| R10 | Creating and deactivating a station are business-significant actions and write audit events `Network.StationCreated` and `Network.StationDeactivated`, with actor fields taken only from the authenticated server-side context. | ENGINEERING DECISION (tech lead, cite ADR-0017 §Decision items 1–2 and ADR-0021); naming from `docs/20` §2 | ADR-0017; ADR-0021; `docs/20` §§2, 7 |
| R11 | Station management is not a financial or retryable command; no `Idempotency-Key` is required. | ENGINEERING DECISION (tech lead, cite `docs/20` §5, whose required list is sell / cancel / refund / payment / cashier-session and does not include station management) | `docs/20` §5 |
| R12 | Instants are `DateTimeOffset` in code and `datetimeoffset(3)` in SQL, read only through `TimeProvider`. Stations own no `BusinessDate` — ADR-0019's rule table covers Sale, Payment, cancellation and Refund only. | ENGINEERING DECISION (tech lead, cite ADR-0018 §Time and ADR-0019 §Decision) | ADR-0018; ADR-0019 |

**Blocking open questions:** none. **OQ26, OQ27 and OQ28 are resolved by a final tech-lead ruling (T-014, hein, 2026-09-22)** — not a Myanma Railways answer; Myanma Railways' own answer, if it differs, would supersede this ruling and needs its own follow-up task. R3, R4 and R8 state the ruling directly. See §Blocked behaviour.

---

## 4. Scenarios (Given / When / Then)

### Station slice — happy path

- **S1.** Given an authenticated user holding `stations.manage`, when they POST a valid station, then the response is `201 Created` with `Location: /api/v1/stations/{id}` and body `{ "id": "<guid>" }`, the row exists in `network.Stations` with `IsActive = 1`, and one `Network.StationCreated` audit event is present.
- **S2.** Given an existing active station, when a caller holding `stations.manage` POSTs `/stations/{id}/deactivate`, then the response is `204 No Content`, `IsActive = 0`, and one `Network.StationDeactivated` audit event is written.
- **S3.** Given three stations exist, when a caller holding `stations.read` GETs `/stations?page=1&pageSize=50`, then the response is `200` with `items`, `page`, `pageSize` and `totalCount` (`docs/20` §4).
- **S4.** Given a station exists, when a caller holding `stations.read` GETs `/stations/{id}`, then the response is `200` with a `StationResponse` and **no EF entity** is serialized (AGENTS.md rule 4).

### Station slice — failures, each with its error code

- **S5.** Duplicate code detected by the pre-check → `409` ProblemDetails, `errorCode = Network.StationCodeAlreadyExists`.
- **S6.** Reuse of a **deactivated** station's code → `409 Network.StationCodeAlreadyExists` (R3, "never reused"). The deactivated row is retained, so the unique index enforces this without a separate mechanism; the test proves it rather than assuming it.
- **S7.** Deactivate an already-inactive station → `422`, `errorCode = Network.StationAlreadyInactive` (ADR-0004: `BusinessRule` → 422).
- **S8.** GET an unknown id → `404`, `errorCode = Network.StationNotFound`.
- **S9.** Request body failing validation → `400` ProblemDetails carrying `errorCode` and `traceId`. Cases, each asserted separately (R3, R4): blank code; 1-character code; 11-character code; lower-case code; code containing a hyphen, space or punctuation; missing `nameEn`; missing `nameMy`; whitespace-only name; 101-character name after trim.
- **S10.** `pageSize=201` → `400` (max 200, `docs/20` §4).
- **S11.** Anonymous POST `/stations` → `401`.
- **S12.** Authenticated user **without** `stations.manage` POSTs `/stations` → `403`. A user holding only `stations.read` is one such case. (`docs/21` §Tests requires both S11 and S12.)

### Concurrency and data integrity

- **S13.** Two parallel POSTs with the same code → exactly one `201`; the other returns `409 Network.StationCodeAlreadyExists` through the unique-index violation mapped per ADR-0004, and exactly one row exists.
- **S14.** Myanmar-script name round-trip: the value read back is identical to the value written (`nvarchar`, Unicode, `docs/20` §6). The fixture uses real Myanmar Unicode text, not a Latin placeholder.
- **S27.** Two parallel deactivations of the same active station → exactly one `204`, one `422 Network.StationAlreadyInactive`, and exactly one `Network.StationDeactivated` audit event. *(Stage-2 amendment approved by hein, 2026-09-20, on the T-003 plan review. Numbered S27 so the existing S1–S26 references in `plan.md` stay valid.)*

### Infrastructure

- **S15.** `dotnet build YCR.sln` and `dotnet test YCR.sln` both succeed from a clean clone, with no skipped tests (`docs/21` §Tests).
- **S16.** Architecture tests **fail** on each forbidden dependency and pass otherwise (ADR-0012 §Enforcement; `docs/21` §Code requires negative cases): (a) `YCR.Application.Network` referencing `ITicketingDbContext`; (b) `YCR.Application.Network` referencing a `YCR.Domain.Ticketing` type; (c) a Reporting type resolving a write context; (d) `YCR.Api` containing domain logic; (e) an endpoint returning an EF entity type; (f) any `AuthenticationHandler<>` subtype in `src/` (S21a, ADR-0020 §Decision item 4).
- **S17.** Every registered module context interface resolves to the *same* scoped `YcrDbContext` instance within one request scope (ADR-0012 §Decision item 3).
- **S18.** Migrations applied against the pinned `mssql/server:2022` image create `network.Stations` and the `audit.AuditEvents` **ledger** table, and the ledger DDL executes successfully (ADR-0017 §Decision item 5).
- **S19.** The audit migration **fails loudly** on an unsupported SQL Server version or edition rather than silently degrading to a normal table (ADR-0017 §Decision item 5; AGENTS.md rule 8).
- **S20.** An audit event's `ActorUserId`/`ActorRole` come from the authenticated context; a request supplying actor fields in its body cannot influence them (ADR-0017 §Decision item 2; ADR-0021).
- **S21.** **The test authentication handler never ships (E1, ADR-0020).** Two controls, tested separately:
  - **S21a (primary).** An architecture test asserts that **no subtype of `AuthenticationHandler<>` exists anywhere in `src/`**. The handler class lives only in `tests/YCR.Api.Tests` and is registered only through `WebApplicationFactory.ConfigureTestServices`.
  - **S21b (defence in depth).** In any environment other than `Testing`, startup validates the registered authentication schemes against an allowlist of expected production handler types and **throws** on an unexpected one. A test asserts both halves — registration succeeds under `Testing`, and startup throws under `Production`.
- **S22.** **The application login has no DDL rights (E7):** the application does not migrate at startup; migration runs as a separate step under the migrator credential. A test asserts that the application credential cannot execute DDL against `network` and holds only INSERT/SELECT on `audit` (ADR-0017 §Decision item 3).
- **S23.** `IIdGenerator` fragmentation control: insert 10,000 rows into the clustered GUID key and compare index fragmentation with a `NEWSEQUENTIALID()` baseline; pass when no more than 10 percentage points worse. The test records row count, index name, fragmentation and database compatibility level (ADR-0006 §REQUIRED CONTROL).
- **S24.** `GET /health/live` and `GET /health/ready` return `200` anonymously, and readiness reports SQL Server connectivity (`docs/02` §Reliability).
- **S25.** An unhandled exception returns `500` as ProblemDetails with a `traceId` and **no** internal detail (ADR-0004 §Errors).
- **S26.** CI (GitHub Actions) runs build, tests against the pinned SQL Server 2022 image, and the `.gitleaks.toml` secret scan, and fails the pipeline on any failure (D3; `docs/21` §Security).

---

## 5. State changes

| Entity | From | Event | Guard | To |
|---|---|---|---|---|
| Station | (none) | `CreateStation` | caller holds `stations.manage`; code matches `^[A-Z0-9]{2,10}$` and is unused by any station, active or inactive (R3); both names present and 1–100 characters after trim (R4) | Active |
| Station | Active | `DeactivateStation` | caller holds `stations.manage` | Inactive |
| Station | Inactive | `DeactivateStation` | — | rejected, `Network.StationAlreadyInactive` (R6) |

Reactivation is out of scope (§9); FR-001's "activate" is deferred to the follow-up feature. Because codes are never reused (R3), deactivated rows are retained rather than deleted.

---

## 6. API

Base path `/api/v1`, JSON camelCase, GUID resource ids, RFC 9457 ProblemDetails with `errorCode` and `traceId` (`docs/20` §4; ADR-0004).

| Method | Path | Request | Success | Error codes | Permission | Idempotency |
|---|---|---|---|---|---|---|
| POST | `/stations` | `CreateStationRequest { code, nameEn, nameMy }` | `201` + `CreateStationResponse { id }` and a `Location` header | `400` validation · `401` · `403` · `409 Network.StationCodeAlreadyExists` | `stations.manage` | No (R11) |
| POST | `/stations/{id}/deactivate` | — | `204` | `401` · `403` · `404 Network.StationNotFound` · `422 Network.StationAlreadyInactive` | `stations.manage` | No (R11) |
| GET | `/stations/{id}` | — | `200` + `StationResponse { id, code, nameEn, nameMy, isActive, createdAtUtc }` | `401` · `403` · `404 Network.StationNotFound` | `stations.read` | n/a |
| GET | `/stations` | `?page=1&pageSize=50` (max 200) | `200` + `{ items, page, pageSize, totalCount }` | `400` · `401` · `403` | `stations.read` | n/a |
| GET | `/health/live` | — | `200` | — | `.AllowAnonymous()` — a liveness probe must answer before auth is reachable | n/a |
| GET | `/health/ready` | — | `200` / `503` | — | `.AllowAnonymous()` — readiness probe used by the reverse proxy | n/a |

The request-field shape follows the R3/R4 rules, now final per T-014 (hein, 2026-09-22). `docs/08-api-specification.md` lists `PATCH /stations`; update is deferred (§9).

---

## 7. Data

Schema `network` (`docs/07` §Module schemas; `docs/20` §2).

**`network.Stations`**

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | no | PK, application-assigned, `ValueGeneratedNever()` (ADR-0006, R9) |
| `Code` | `nvarchar(10)` | no | **unique index** across all rows, active and inactive (R3). `nvarchar` per `docs/20` §6 (a person types and reads it), even though R3 restricts the characters to ASCII. Character-set and length are enforced in `StationCode`, with the unique index as the concurrency authority (R7). |
| `NameEn` | `nvarchar(100)` | no | owned value object `BilingualName` (R4) |
| `NameMy` | `nvarchar(100)` | no | Unicode, never Zawgyi (`docs/20` §6; R4) |
| `IsActive` | `bit` | no | |
| `CreatedAtUtc` | `datetimeoffset(3)` | no | UTC value (ADR-0018) |

No `rowversion` concurrency token — **accepted by hein 2026-09-20**. `docs/20` §6 requires one for Ticket, CashierSession and Refund; Station is not in that list.

**`IsActive` is configured as an EF concurrency token** (`IsConcurrencyToken()`), added by the T-003 plan review on 2026-09-20 to satisfy S27. EF then issues `UPDATE ... WHERE Id = @id AND IsActive = @original`, so the loser of a concurrent deactivation gets a `DbUpdateConcurrencyException`, which maps to `422 Network.StationAlreadyInactive`. This adds no column: it reuses one that already exists, which is why it does not contradict the no-`rowversion` decision above.

Rows are never deleted, because R3 forbids code reuse and the unique index is what enforces it.

**`audit.AuditEvents`** — append-only SQL Server 2022 ledger table, created by **raw SQL in an EF migration** rather than by EF model building (ADR-0017 §Decision item 1). **ADR-0021 is authoritative** for this shape; it is restated here because the constraints and indexes are part of F-001's migration.

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | no | PK, application-assigned through `IIdGenerator` (ADR-0006) |
| `OccurredAtUtc` | `datetimeoffset(3)` | no | UTC, from `TimeProvider` (ADR-0018) |
| `Action` | `nvarchar(100)` | no | `<Module>.<Event>`, e.g. `Network.StationCreated` |
| `ActorUserId` | `uniqueidentifier` | yes | Server-side authenticated context only; null for system-initiated actions |
| `ActorRole` | `nvarchar(1000)` | yes | **JSON array of roles held at event time**, e.g. `["Admin","StationManager"]` |
| `SubjectType` | `nvarchar(100)` | no | Always present, including when `SubjectId` is null |
| `SubjectId` | `uniqueidentifier` | **yes** | Null where the event has no GUID subject, e.g. a failed login with no resolved user |
| `BeforeJson` | `nvarchar(max)` | yes | Null on creation events |
| `AfterJson` | `nvarchar(max)` | yes | Null on deletion-style events |
| `CorrelationId` | `nvarchar(100)` | no | From `traceparent` (`docs/20` §7) |
| `ClientIp` | `nvarchar(45)` | yes | Server's view of the connection, never a client-supplied header |
| `ReasonCode` | `nvarchar(100)` | yes | Operator-supplied reason where an action requires one |
| `AuthorizedByPermission` | `nvarchar(100)` | yes | Permission that authorized the action, e.g. `stations.manage` |
| `PayloadVersion` | `int` | no | Schema version of the JSON payloads, starting at 1 |

Constraints and indexes, created in the same migration as the table:

| Object | Definition |
|---|---|
| `CK_AuditEvents_BeforeJson` | `BeforeJson IS NULL OR ISJSON(BeforeJson) = 1` |
| `CK_AuditEvents_AfterJson` | `AfterJson IS NULL OR ISJSON(AfterJson) = 1` |
| `CK_AuditEvents_ActorRole` | `ActorRole IS NULL OR ISJSON(ActorRole) = 1` |
| `IX_AuditEvents_Subject` | nonclustered on `(SubjectType, SubjectId)` |
| `IX_AuditEvents_OccurredAtUtc` | nonclustered on `OccurredAtUtc` |

For F-001's two audit actions, `SubjectType` is `Station`, `SubjectId` is the station id, and `AuthorizedByPermission` is `stations.manage`. The nullable `SubjectId` and the `ISJSON` array shape are exercised by later features; F-001 only has to create them correctly. `ISJSON` proves the text is JSON, not that `ActorRole` is an array — `IAuditWriter` owns that, with a test (ADR-0021 §Consequences).

**Not in F-001:** `IdempotencyRecords` (no financial command in scope, R11), `identity.AuthSessions` (deferred by ADR-0020), and every other table in `docs/07` §Core tables.

Migrations are named `YYYYMMDD_<Module>_<Change>` and reviewed per `docs/workflows/04-database-change.md` (`docs/20` §6). **Migrations never run at application startup** (E7): the migrator is a separate step with its own credential, and the application login has no DDL rights.

---

## 8. Audit, logging, metrics

- **Audit** (ADR-0017, ADR-0021): `Network.StationCreated` and `Network.StationDeactivated` through `IAuditWriter`, written in the same `SaveChangesAsync` as the business change, with before/after state. Actor fields come only from the authenticated server context (S20). The application database login gets **INSERT/SELECT only** on the `audit` schema and no DDL rights anywhere; migration and digest administration use separate credentials (ADR-0017 §Decision item 3; E7; S22).
- **Logging** (`docs/20` §7): `ILogger` with message templates, never interpolation. Correlation from `traceparent`. Never log passwords, tokens, refresh cookies, QR payloads, keys or personal data.
- **Metrics** (`docs/17`): F-001 ships the baseline request-latency, error-rate and database-latency instrumentation that later features extend. None of `docs/17`'s listed business events belongs to the Network module, so no business metric is added here.
- **Out of scope:** digest generation, WORM storage, scheduling and alert routing — ADR-0017 §Blocked behaviour leaves the provider, schedule and ownership open.

---

## 9. Out of scope

Update/rename a station (`PATCH /stations`), reactivation, search and filtering beyond simple pagination, `Routes`/`RouteStations` and the remaining tables in `docs/07` §Core tables, the ADR-0016 token and refresh implementation (deferred by ADR-0020), `IdempotencyRecords`, `YCR.Worker` behaviour (the project is created empty), the SPA and its CSP/dependency-audit controls (ADR-0016 §Browser security — no frontend exists yet), ledger digest operations, and seed/fixture station data (blocked by OQ1).

Renaming a station raises a data-history question — does a rename rewrite historical station names on already-issued tickets? — that the follow-up feature must resolve before implementing `PATCH`.

---

## Blocked behaviour

Required by `docs/21` §Specification.

### Waiver closed — T-014 (hein, 2026-09-22)

The explicit waiver approved 2026-09-20 covered only the `StationCode` value object, the `BilingualName` value object, and the permission constants (R3, R4, R8), pending OQ26–OQ28. **T-014 closes it**: OQ26, OQ27 and OQ28 are resolved by a final tech-lead ruling (hein, 2026-09-22) — not a Myanma Railways answer, but this project chose not to wait for one. R3, R4 and R8 are no longer provisional and no longer carry the `ASSUMPTION (provisional...)` label; see `docs/19-open-questions.md` and `docs/10-authorization-matrix.md`. If Myanma Railways later gives an official, different answer to any of these, that supersedes this ruling and needs its own follow-up task. AGENTS.md §"When a business rule is missing" applies unchanged to every other missing business rule; this closure is not a precedent.

### Still blocked

| Behaviour | Blocking item | Effect on F-001 |
|---|---|---|
| Role→permission grants and any permission seed data | No identity/role provisioning system exists yet — real authentication is deferred to the ADR-0016 follow-up feature (ADR-0020) | Approved grants are recorded in `docs/10-authorization-matrix.md`; F-001 still seeds nothing in code. Tests mint permissions directly on the test principal. |
| Real station data (seed/fixture station list) | **OQ1** | No station fixtures ship. Tests construct their own stations. |
| Ledger digest schedule, storage and verification | ADR-0017 §Blocked behaviour | Out of scope (§9); F-001 proves the DDL only. |
| Audit retention and archival for ledger rows | **OQ14** (ADR-0017 §Blocked behaviour) | No retention behaviour is implemented. |

---

## Engineering decisions — accepted by hein, tech lead, 2026-09-20

`TASKS.md` §Protocol item 8 classes these as engineering, so they are recorded here and as ADRs rather than in `docs/19`.

| # | Decision as accepted |
|---|---|
| **E1** | **Accepted with additions — ADR-0020 (Accepted 2026-09-20).** F-001 ships the permission-based authorization pipeline (`Permissions` constants, policy registration, `.RequireAuthorization(...)` on every endpoint) with a **test-only authentication handler**; the ADR-0016 token and refresh implementation is deferred to a dedicated follow-up feature. **Additions:** the handler class lives only in `tests/YCR.Api.Tests` and is registered only through `WebApplicationFactory.ConfigureTestServices`, never in `src/`; an architecture test asserts no `AuthenticationHandler<>` subtype exists in `src/` (S21a); the startup environment guard is retained as defence in depth, throwing outside `Testing`, with a test proving both halves (S21b). |
| **E2** | **Accepted as recommended.** `global.json` with a pinned SDK and `rollForward`, plus `.gitignore`, `.editorconfig`, `Directory.Build.props` (nullable enabled, warnings as errors) and `Directory.Packages.props` for central package management. |
| **E3** | **Accepted — CI provider is GitHub Actions** (confirmed by hein 2026-09-20). `.github/workflows/ci.yml`: restore → build → test (Testcontainers, pinned image) → gitleaks. The repository has no remote yet; adding one is a prerequisite for the workflow actually running. |
| **E4** | **Accepted as recommended.** The SQL Server 2022 image is pinned **by digest**, not by a floating tag, in one shared constant consumed by both `docker-compose.yml` and the test fixture. |
| **E5** | **Accepted as recommended.** The ADR-0006 fragmentation control is implemented in `YCR.Infrastructure.Tests` in F-001, in a test category CI runs on the trunk build. Whether it also runs on every pull request is decided when the workflow is written. |
| **E6** | **Accepted as recommended (VERIFY).** Ledger support in the chosen image's edition, and the exact ledger DDL, are verified against the E4 image during stage 4, with the result recorded in `progress.md`. If the edition does not support ledger tables, stop and return to stage 2 rather than substituting a normal table. |
| **E7** | **Accepted with additions.** Two connection strings from the start (`Migrator`, `Application`) with least-privilege grants scripted in the migration. **Additions:** **no migrations at application startup**; the migrator is a separate step; the application login has **no DDL rights**. Covered by S22. |
| **E8** | **Accepted with additions — ADR-0021 (Accepted 2026-09-20).** `audit.AuditEvents` carries id, occurred-at UTC, action (`<Module>.<Event>`), actor user id, actor role, subject type, subject id, before/after JSON and correlation/trace id, **plus `ClientIp`, nullable `ReasonCode` and `PayloadVersion`**. **Additions:** `SubjectId` is nullable; `ActorRole` holds a JSON array of the roles held at event time; `AuthorizedByPermission nvarchar(100) null` added; `ISJSON` check constraints on `BeforeJson`, `AfterJson` and `ActorRole`; nonclustered indexes on `(SubjectType, SubjectId)` and `OccurredAtUtc`. See §7. |
| — | **Station has no `rowversion` concurrency token: accepted** (§7). |

**ADR-0020 and ADR-0021 are Accepted (hein, 2026-09-20)** and therefore binding. ADR-0001 and ADR-0002 were also accepted on the same date (T-011), so `docs/decisions/README.md` no longer lists any Proposed ADR.

---

## Notes for the next stage

- **Trunk rename:** the trunk is being renamed `master` → `main` (D4), which makes `TASKS.md` §Protocol correct as written. The `claim/T-002` ref was created from `master` before the rename.
- **Glossary:** T-010 decided yes and is being written in parallel (D5). F-001 contributes the Network vocabulary it introduces — `Station`, `StationCode`, `BilingualName`, active/inactive — so `docs/21` §Documentation is satisfiable.
- **T-003 (PLAN)** is unblocked by this approval but must not be started until hein says so.
- **T-014** (hein, 2026-09-22) replaced the provisional station rules with a final tech-lead ruling over OQ26–OQ28; it was a release gate, not an F-001 blocker, and is now closed.
- **T-015** creates `docs/glossary.md`. F-001's Network vocabulary feeds into it (D5).
- **ADR-0020 §Consequences carries one caveat the follow-up authentication feature must check:** the "no `AuthenticationHandler<>` in `src/`" rule holds only while YCR writes no authentication handler of its own. ADR-0016 as written uses framework-provided handler types, so it holds today; if that changes, the rule needs a narrower formulation through a superseding ADR rather than an edit to the test.

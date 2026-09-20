# Starter Kit Review — 2026-09-19

Scope: `AGENTS.md`, `README.md`, all of `docs/` (35 files).
Method: (1) consistency audit across docs, (2) dry-run of one feature through `workflows/02-feature-development.md`, (3) gaps that will hurt agentic development.
Nothing in the repo was changed. Labels follow AGENTS.md rule 2.

---

## 0. Verdict

The kit has good instincts (versioned policy, idempotent finance, ticket ≠ payment state, "don't invent rules"). But most docs are 300–800 byte outlines. Right now an agent **cannot implement a single feature without guessing**. That violates rule 1 ("do not invent railway business rules"), so a disciplined agent will stall and an undisciplined one will fabricate.

Top 5 fixes, in order:

1. **Unify vocabulary.** Add `docs/glossary.md` and make actors, roles, entities and endpoints use one name each (§1.1).
2. **Fix the ticket lifecycle.** It currently contradicts the payment/refund doc (§1.2).
3. **Decide how modules physically exist in the .NET solution.** 05/ADR-0001 promise 9 module boundaries, but 06 only defines technical layers (§1.5).
4. **Model the loop.** YCR is a circular line, and nothing in the fare engine or route model accounts for direction (§2).
5. **Make the workflows executable.** Each stage needs inputs, outputs, a file location and a stop point. Add a feature-spec template and a Definition of Done (§4).

---

## 1. Consistency audit (contradictions between docs)

### 1.1 Naming drift — agents will create duplicate concepts

| Concept | Names used | Where |
|---|---|---|
| Timetable | Timetables / Schedules / ScheduleVersion / "Service & Timetable" | FR-004, 03 UC4, 04, 05, 07, 08 `/schedules` |
| Service | Train service / Service / Trains + TrainServices | FR-003, 04 (no Train aggregate), 07 (both tables), 08 (`/trains` + `/services`) |
| Route | "railway lines/routes" / Route | FR-002, 04, 07 |
| Admin role | System Administrator / Admin | 03 vs 10 |
| Operator | Ticket Operator / Operator | 03 vs 10 |
| Fare | FareRuleSet + FareRules / "fares" | 04, 07, 08 `/fares` |

**Fix:** add a glossary with one canonical English term, its Myanmar term and forbidden synonyms. Then rename across docs.

### 1.2 Ticket lifecycle (11) contradicts itself and doc 13

- `Draft → Issued → Valid`: the difference between *Issued* and *Valid* is never defined.
- `Refunded` hangs off `Expired` in the diagram. So is refund only possible after expiry, and not after cancellation? This is almost certainly unintended.
- 13 says to keep ticket state and refund state separate, but 11 puts `Refunded` inside the ticket lifecycle.
- Is `Validated` terminal? What about a return ticket, a day pass or re-entry? That depends on OQ3, but the diagram already assumes single use.
- `Expired` needs a trigger (a scheduled job in `YCR.Worker`, or derived at read time). Neither doc says which.
- There is no `Reprinted` event or state, although UC8 "Reprint ticket" exists (see 1.4).

**Fix:** use a state × event transition table (from, event, guard, to, who may trigger it), with refund state kept on Refund/Payment. Agents implement tables far more reliably than ASCII diagrams.

### 1.3 Authorization matrix (10) vs use cases (03)

- 03 has 8 actors; 10 has 7 columns. **Reporting User** has no column.
- 15 use cases, but the matrix covers only 8 capabilities. There are no rows for routes, trains/services, schedules, **open/close cashier session**, **reprint**, reconcile or passenger categories.
- The values `policy` and `limited` are undefined. An agent must either invent the policy (a rule 1 violation) or block.
- Operators can't refund, so a passenger who wants money back at the counter has no path. Is refund initiation separate from approval? A maker–checker split looks intended but isn't stated.

### 1.4 Concepts that exist in some docs but not others

| Concept | Present in | Missing from |
|---|---|---|
| Cashier session | 03 (UC6, UC12), 04 aggregate, 07 table, 14 report, 17 events | 01 FRs, 08 API, 10 matrix, 04 domain events |
| Reprint | 03 UC8 | 01, 08, 10, 11, 18 (reprinting is a forgery/duplication vector) |
| Sale | 07 `Sales` table, 13 "sale state" | 04 (no Sale aggregate), no definition anywhere |
| Passenger data | 18 lists it as an asset | no Passenger entity exists. Are tickets anonymous? If so, remove the asset; if not, a PII/retention policy is missing |
| SchedulePublished / FareRulePublished | 04 events | 17 observability. Config changes are among the most audit-worthy events |
| CashierOpened / CashierClosed | 17 | 04 events |
| Reconciliation | 03 UC13, 08, 14 | 01 has no FR for it; its rules are undefined |

### 1.5 Architecture (06) vs modular monolith (05, ADR-0001)

- 05 lists 9 bounded contexts, and ADR-0001 says boundaries must be "explicit". But the 06 project list is purely technical (`Domain/Application/Infrastructure/Api/Web/Worker`). Where does the `Ticketing` module live? What stops `Ticketing` from querying `Fare` tables directly?
- "Clean Architecture + Vertical Slice + modular monolith" is three styles at once with no worked example. Agents will mix them inconsistently.
- `YCR.Api` and `YCR.Web` both exist, and no doc says what Web is (Razor UI, BFF, SPA host?).
- `YCR.ArchitectureTests` exists, but no rules are listed for it to enforce.

**Fix:** ADR-0003 on module layout, e.g. `src/Modules/Ticketing/{Domain,Application,Infrastructure,Api}` or namespaces checked by NetArchTest, plus cross-module rules (public contracts/events only, no shared DbContext writes). Include one complete reference slice.

### 1.6 API (08) vs versioning decisions (ADR-0002, FR-004/005)

- `PATCH /fares` and `PATCH /schedules` contradict "versioned, historical preserved". A published version should be immutable. The model should be `POST …/versions` → `POST …/versions/{id}/publish`.
- `POST /tickets/{id}/refund` makes refund a ticket operation, while FR-009 says refund is per *financial transaction*. This is the same confusion as 1.2.
- `POST /tickets/{id}/validate` uses the internal id, but an inspector scans a QR code or reads a printed number. Validation should look up by ticket number or signed payload.
- Missing: fare quote (`POST /fares/quote`), cashier sessions, reprint, passenger categories, audit log query.
- "Idempotency where applicable" has no header name, key scope, TTL or replay semantics, even though `IdempotencyRecords` is already a table.

### 1.7 Scope vs open questions

- 00 puts "offline validator synchronization" out of scope, but OQ8 asks whether validation is offline. FR-007 requires duplicate-use detection, which is hard to guarantee offline. **This answer changes the ticket format** (signed self-verifying QR vs server lookup), so it must be settled before Ticketing starts, not later.
- OQ16 (existing Laravel system to migrate) affects the schema, IDs and cutover. If a legacy system exists, it needs its own doc.

### 1.8 AGENTS.md vs the docs

- Rule 2 requires FACT/ASSUMPTION/OPEN QUESTION/BUSINESS DECISION labels, but the docs don't use them. Only OQ5 labels its assumption. Agents copy what they see, not what they're told.
- The AGENTS.md loop ends in HUMAN APPROVAL; workflow 02 drops it.
- Both ADRs are still "Proposed", so every agent may treat the architecture as open for debate.

---

## 2. Domain gaps specific to YCR

- **FACT:** the Yangon Circular Railway is a loop. **Consequence:** origin → destination is ambiguous (clockwise vs anticlockwise). `RouteSegmentResolver` must define whether the fare uses the direction travelled, the shortest arc or a flat/zone fare. `Route` = "ordered station sequence" (FR-002) also needs a wrap-around rule. Neither 04 nor 12 mentions this. Add it as OQ17 and model it explicitly.
- **Bilingual data:** station names and printed tickets need Myanmar and English text. Store Myanmar text as Unicode (not Zawgyi), and state whether printed tickets use Myanmar numerals. None of this is specified.
- **Money:** the currency is MMK. Decide the `Money` precision (whole kyat vs decimal) and the rounding rule now. It touches fares, refunds and reconciliation.
- **Time:** the local zone is Asia/Yangon (UTC+06:30, no DST). 07 says "UTC where appropriate" and 14 says "business date". Define the **business day boundary** and what happens to a cashier session that crosses midnight. Travel date is a local date, not a UTC instant.
- **ASSUMPTION to verify:** most sales are cash at station counters, with patchy connectivity. If so, counter availability during network loss is a core NFR, not an edge case.
- **Authoritative sources:** there is no `docs/sources/` folder. The discovery agent is told to "inspect authoritative sources" and "update the appropriate files", but it has nothing to inspect. That is exactly the setup where agents fabricate.

---

## 3. Dry-run: "Sell a single-journey ticket at a station counter"

I followed `workflows/02` and `prompts/implementation-agent.md` up to the plan. These are the points where I would have had to **guess**:

| # | Needed to know | Doc status |
|---|---|---|
| 1 | Is a ticket per journey, per day or per train? | OQ3/OQ4, open |
| 2 | Fare for A → B on a loop, and which direction | Not addressed |
| 3 | Passenger categories and discounts | OQ9, open |
| 4 | Must an open cashier session exist to sell? | Implied by 03/14, never stated |
| 5 | Payment methods, and whether the payment is recorded in the same transaction | OQ11 open; 13 vague |
| 6 | Ticket number format, and how QR contents are signed | TicketNumber VO only |
| 7 | Is the ticket bound to a travel date or service, and what is its validity window? | Unspecified, yet `Expired` depends on it |
| 8 | Is there a `Draft` state in a counter sale? | 11 undefined |
| 9 | Idempotency key: source (counter device?), header, TTL | Unspecified |
| 10 | Printed ticket content and language | Unspecified |
| 11 | Which module owns `Sale`, and can Ticketing call Fare directly? | 1.4, 1.5 |
| 12 | Folder/namespace for this slice; handler/validator/endpoint naming | No conventions doc |
| 13 | Where the spec and plan go, and who approves them | Workflow 02 has no artifacts |
| 14 | Audit record shape (who/what/when/before/after) | Unspecified |
| 15 | When the feature counts as done | No DoD |

**Result:** 15 guesses for the most central feature. Items 1–7 are business decisions for Myanma Railways. Items 8–15 are engineering decisions you can make today.

---

## 4. Gaps in the agentic setup itself

1. **CLAUDE.md** is missing. Add a two-line file pointing to AGENTS.md so Claude Code loads the constitution automatically.
2. **Keep AGENTS.md as an index.** Add a "read this for that" table (feature type → docs to read), the commands (`dotnet build`, `dotnet test`, `docker compose up`) and a hard rule: *"If a business rule is missing, add it to 19-open-questions.md and stop; do not implement a placeholder."*
3. **Conventions doc** (`docs/20-coding-conventions.md`): one complete reference slice (endpoint → command/handler → domain → EF config → tests), naming, error → ProblemDetails mapping, result types, and DTO mapping.
4. **Templates:** `docs/templates/feature-spec.md`, `adr.md`, `plan.md`, `review-report.md`. Agents produce consistent output when they fill in templates.
5. **Artifact locations:** `docs/features/<id>-<slug>/{spec,plan,review}.md`. Every workflow stage writes to a known file, so work survives across sessions and can be reviewed.
6. **Stop points:** workflow 02 should mark where the agent must stop for human approval (after SPECIFY, after PLAN, before merge).
7. **Definition of Done:** tests (including a concurrency test for financial paths), authz row present, audit event emitted, docs/glossary updated, ADR if material, migration reviewed.
8. **Prompt orchestration:** a table of stage → prompt → input files → output file. Give review-agent the same severity scale as security-agent. Add a *requirements-auditor* prompt that checks docs against each other, i.e. this review, repeatable.
9. **Walking skeleton:** there is no `src/` yet. A compiling solution with one trivial slice (e.g. Stations CRUD), architecture tests, docker-compose with SQL Server and a green CI run is the best single thing to give agents. They copy patterns much better than they follow prose.
10. **Seed/fixture data:** a station list (bilingual, loop order) and a sample fare table marked `ASSUMPTION — test data only`.
11. **Missing ADRs:** module layout (1.5), ID strategy (int PK + GUID/business key?), auth approach, ticket authenticity (signed QR), online/offline counter model, audit immutability (append-only/hash chain against "audit tampering").

---

## 5. Suggested next sessions

| Session | Agent mode | Output |
|---|---|---|
| A. Engineering decisions (§3 items 8–15, §4) | Interview: options → you decide | ADR-0003…0007, conventions, templates |
| B. Vocabulary + lifecycle fix | Edit docs | glossary, transition table, updated 04/07/08/10/11 |
| C. Business questions pack for Myanma Railways | Draft | One document with OQ1–17, why each matters and what it blocks |
| D. Walking skeleton | Implement | `src/`, `tests/`, compose, CI, Stations slice |
| E. Re-run the dry-run on "sell ticket" | Plan only | Guess count should fall from 15 to ≤7 (business-only) |

Reusable audit prompt for later:

> Read AGENTS.md and all of docs/. Report only (1) contradictions between docs with file+section citations, (2) concepts present in some docs but absent from others, (3) points where implementing feature X would require guessing. Do not edit files. Label every claim FACT/ASSUMPTION/OPEN QUESTION.

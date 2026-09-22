# Definition of Done

A feature is done only when **every** item is true. Reviewers and review agents check this list item by item and cite evidence (file, test name, command output).

## Specification
- [ ] Spec exists at `docs/features/<id>-<slug>/spec.md` and was approved by a human.
- [ ] Every rule it uses is labelled FACT, BUSINESS DECISION, or ENGINEERING DECISION (with source/ADR). No unresolved OPEN QUESTION affects the implemented behaviour.
- [ ] Any unresolved but intentionally deferred behavior is listed in a `Blocked behaviour` section with the blocking OQ and no placeholder implementation.

## Code
- [ ] Follows `docs/20-coding-conventions.md` and all Accepted ADRs.
- [ ] No domain logic in endpoints. No EF entities in API responses.
- [ ] Architecture tests pass.
- [ ] Architecture tests include negative cases for forbidden module context/domain dependencies and the reporting read-only context.

## Tests
- [ ] Domain tests for every invariant and state transition touched.
- [ ] Handler integration tests against SQL Server for the happy path and each error code.
- [ ] API tests for 401, 403 (wrong permission) and 400 validation.
- [ ] **At least one test runs the unmodified production composition** — no `ConfigureTestServices` overrides, no substituted services. A suite where every test replaces part of the wiring is blind to whatever the replacement hides. F-001's `MissingSchemeTests` is the reference: every other API test registers the test authentication handler, and that is exactly why none of them could see that an unauthenticated request returned `500` instead of `401` in the shape that actually ships.
- [ ] Financial or retryable commands have idempotency and concurrency tests (duplicate request, parallel request).
- [ ] `dotnet test` is fully green, with no skipped tests added.

## Security
- [ ] Endpoint permission added, and `docs/10-authorization-matrix.md` updated.
- [ ] Security review done (`docs/prompts/security-agent.md`) with no open Critical or High findings.
- [ ] Cookie-bearing endpoints have Origin/CSRF evidence; XSS controls include CSP and dependency audit evidence where the SPA is affected.
- [ ] No secrets, tokens or personal data in code, config or logs.

## Data
- [ ] Migration reviewed per `workflows/04-database-change.md`. Upgrade tested on a copy of the current schema.
- [ ] SQL Server 2022-specific DDL (including ledger where applicable) is exercised in CI against the pinned container image.
- [ ] Constraints and indexes deliberate and documented in `docs/07`.

## Observability and audit
- [ ] Audit event written for security-sensitive or financial actions.
- [ ] Metrics and log events added where `docs/17` requires them.

## Documentation
- [ ] Affected docs updated in the same change (API 08, DB 07, lifecycle 11, glossary …).
- [ ] ADR added if an architectural decision was made.
- [ ] `docs/features/<id>-<slug>/review.md` records the review outcome.
- [ ] Review evidence names the command, test, or document section supporting every checked item.

## Approval
- [ ] Human approval recorded with PR URL or repository review reference, approver identity, approval date, and approved commit/ref.

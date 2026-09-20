# Codex Second-Opinion Review - 2026-09-19

## Scope

This review challenged ADR-0003 through ADR-0011, checked consistency across `docs/`, and then applied the decisions approved interactively by the human reviewer. No application code was present in the starter kit; this change is documentation and test-vector only.

## Approved decisions applied

1. **Module boundaries:** ADR-0012 supersedes ADR-0003. Each application module receives only its own `I<Module>DbContext`; all interfaces resolve to one scoped `YcrDbContext`. Cross-module calls use Contracts and share one `SaveChangesAsync`. Reporting uses read-only `IReportingReadContext`. Architecture tests fail the build for another module's context or domain dependency.
2. **Lifecycle:** ADR-0013 separates Ticket, Sale, Payment, and Refund state. TicketValidation is append-only evidence, expiry is derived, Draft is removed, and counter issuance creates Sale + Payment + Ticket atomically. Reprint records an event/count/audit entry without changing Ticket status. Business-dependent guards remain blocked.
3. **QR:** ADR-0014 defines fixed binary v1, exact received-byte verification, Base45/ECC-M encoding, P1363 signatures, stable station indices, key lifecycle/compromise handling, CI vectors, and `Valid`/`Invalid`/`AuthenticUnverified` results. Offline admission and reprint invalidation remain blocked.
4. **Counter recovery:** ADR-0015 makes server-side recent operations authoritative, keeps IndexedDB best-effort, binds idempotency to cashier sessions, stores only committed/final 4xx outcomes, and requires crash/concurrency/outage tests. Paper/offline fallback remains blocked by OQ18.
5. **Authentication:** ADR-0016 fixes same-origin reverse-proxy deployment, removes permission claims from JWTs, adds session/permission resolution with a documented 30-second latency, bounded predecessor handling, Origin checks, CSP, and XSS controls.
6. **Audit:** ADR-0017 retains SQL Server ledger, distinguishes alteration detection from fabrication through application credentials, derives actor fields from server authentication context, and requires SQL Server 2022 migration proof in CI.
7. **Time and money:** ADR-0018 uses `DateTimeOffset`/`datetimeoffset(3)` and ADR-0019 puts business-date columns on operation records with a documentation rule table. Rounding, session policy, and Finance date behavior remain blocked.

## Files changed

### New

- `docs/decisions/ADR-0012-module-scoped-persistence-boundaries.md`
- `docs/decisions/ADR-0013-ticket-and-financial-lifecycle.md`
- `docs/decisions/ADR-0014-fixed-binary-signed-ticket-qr.md`
- `docs/decisions/ADR-0015-online-counter-recovery-and-idempotency.md`
- `docs/decisions/ADR-0016-same-origin-spa-authentication.md`
- `docs/decisions/ADR-0017-ledger-audit-operational-boundaries.md`
- `docs/decisions/ADR-0018-time-business-date-and-money.md`
- `docs/decisions/ADR-0019-operation-owned-business-dates.md`
- `docs/test-vectors/ticket-qr-v1.json`

### Updated

- Superseded ADR status/index: `docs/decisions/ADR-0003`, `ADR-0007`, `ADR-0008`, `ADR-0009`, `ADR-0010`, `ADR-0011`, and `docs/decisions/README.md`.
- Dated ADR amendments: `docs/decisions/ADR-0004-application-pattern-and-errors.md`, `docs/decisions/ADR-0006-identifiers.md`.
- Constitution/configuration: `AGENTS.md`, `.gitleaks.toml`.
- Domain/lifecycle/API/payment docs: `docs/04-domain-model.md`, `docs/07-database-design.md`, `docs/08-api-specification.md`, `docs/11-ticket-lifecycle.md`, `docs/13-payment-and-refund.md`.
- Security/operations/conventions: `docs/05-bounded-contexts.md`, `docs/06-system-architecture.md`, `docs/10-authorization-matrix.md`, `docs/15-deployment-architecture.md`, `docs/17-observability.md`, `docs/18-threat-model.md`, `docs/19-open-questions.md`, `docs/20-coding-conventions.md`, `docs/21-definition-of-done.md`.
- Workflow/templates: `docs/templates/feature-spec.md`, `docs/templates/plan.md`, `docs/templates/review-report.md`, `docs/workflows/02-feature-development.md`.

## Remaining blocked behavior

- OQ3/OQ4/OQ19: ticket unit, service binding, validity window, and printed content.
- OQ8: whether `AuthenticUnverified` is admitted during network loss.
- OQ10: cancellation/refund eligibility, amount, and void rules.
- OQ14: immutable audit retention, deletion, and archival.
- OQ15: availability and peak-load targets.
- OQ18: paper/offline counter fallback.
- OQ21: MMK rounding.
- OQ23: whether a reprint invalidates earlier prints.
- Role grants for new permissions in `docs/10-authorization-matrix.md`.
- Exact SQL Server ledger DDL/procedure behavior and QR physical readability are VERIFY items.

## Verification

- `docs/test-vectors/ticket-qr-v1.json` parsed successfully as JSON and contains four vectors with 39 signed bytes and 64-byte signatures.
- The test-only public key verifies the valid vector with explicit P1363/SHA-256 and rejects the tampered vector (`valid_signature=True`, `tampered_signature=False`).
- The RFC 9285 vector, including its leading space after `YCR1:`, decodes and re-encodes with the independent published `SimpleBase` 5.6.4 byte-array library (`bytes_match=True`, `roundtrip=True`).
- Read-only searches found no active API/Web deployment wording or active `/tickets/{id}/refund` and `/tickets/{id}/validate` entries outside historical review/superseded ADR text.
- No `dotnet build` or `dotnet test` was executed because the starter kit still has no solution, projects, or application code.
- Read-only workspace check confirms `YCR.sln`, `src/`, `tests/`, `docker-compose.yml`, and `.github/workflows/` are absent.
- No database migration or SQL Server container was executed; those remain walking-skeleton work.

## Security review

The approved authentication and QR controls address the review findings, but implementation remains unverified. The major residual risks are XSS, compromised application credentials fabricating audit rows, QR physical readability, and the unresolved offline validation policy.

## Performance and operational considerations

The shared context preserves atomic cross-module writes. Session/permission caching has a documented maximum 30-second revocation/permission-change latency. Recent-operation recovery and ledger digest verification require operational metrics and runbooks before production.

## Fixes

- **B1:** Replaced the QR vector with the RFC 9285 Base45 value, added the `YCR1:` prefix and prefix-rejection rule, and recorded independent verification with published `SimpleBase` 5.6.4. The vector was verified against raw bytes, not an in-repository encoder.
- **B2:** Removed the invented Phase 1 cash-only business decision. Cash-only is now an explicitly labeled assumption and OQ11 is open again.
- **B3:** Added `ENGINEERING DECISION (tech lead, cite ADR)` and `REQUIRED CONTROL` to AGENTS.md rule 2, then relabeled technical/security statements in the affected ADRs and documents.
- **B4:** Added a dated ADR-0004 amendment pointing to ADR-0012; corrected the CreateStation example to use `INetworkDbContext`, `DateTimeOffset`, `datetimeoffset(3)`, `TimeProvider.GetUtcNow()`, and `CreateStationResponse`; updated AGENTS.md and docs/06 pointers.
- **B5:** Added ADR-0019, superseding the BusinessDateAssignments portion of ADR-0018. Business dates now belong on operation records; the polymorphic table is removed from the database design.
- **S1:** Standardized lifecycle permissions to plural `<resource>.<action>` names, including separate `refunds.reject`.
- **S2:** Added creation, refund-request, reprint, and refund-approval cancellation rows to ADR-0013 and docs/11.
- **S3:** Added cashier open/close, ticket reprint, and QR trust-list API entries to docs/08.
- **S4:** Split revoked-key vectors into existing-before-compromise (`Valid`) and absent-from-database (`Invalid`) online cases.
- **S5:** Resolved as a 1-byte version. QR v1 now uses `"YCR" + 0x01`, a 36-byte signed payload, and `YCR1:` whose digit must match the binary version.
- **S6:** Added the concrete `SqlServerSequentialGuidIdGenerator` decision and a fragmentation threshold against a `NEWSEQUENTIALID()` baseline to ADR-0006.
- **S7:** Added discovery/source/owner fields, endpoint inventory, rollback guidance, security threat IDs/evidence, workflow handoff requirements, and explicit human-approval evidence requirements.
- **S8:** Added open questions for multiple tickets per sale and ownership of the key-compromise procedure.
- **S9:** Added the canonical bounded-context-to-module mapping in docs/05.
- **S10:** Deleted the tool-specific `docs/superpowers/plans/` plan artifact.
- **S11:** Added a `.gitleaks.toml` allowlist entry for the deterministic test vector and documented that production key loaders must reject test-only key material.

No glossary was created, and ADR-0001/ADR-0002 were not changed.

### Final fixes

- **S5 final:** ADR-0014 has a dated amendment for the 1-byte version/domain-separated magic, and `ticket-qr-v1.json` was regenerated with all payloads, signatures, Base45 text, tampered data, and both revoked-key cases.
- **Lifecycle error:** `Sales.SaleNotSellable` is now `Payments.SaleNotSellable` in ADR-0013 and docs/11.
- **Permission inventory:** docs/10 now lists endpoint identifiers including validation, cashier open/close, trust-list read, station/fare/schedule management, and report read; grants remain OPEN QUESTION.
- **Rule labels:** docs/21 and the feature-spec template allow FACT, BUSINESS DECISION, or ENGINEERING DECISION with source/ADR.
- **Workspace cleanup:** the empty `docs/superpowers/` folder was removed.
- **Independent verification:** SimpleBase 5.6.4 passed all four RFC 9285 Base45 vectors; BouncyCastle.Cryptography 2.7.0 independently verified the valid P-256/P1363 signature and rejected the tampered payload.

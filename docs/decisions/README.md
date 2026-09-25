# Architecture Decision Records

| ADR | Title | Status |
|---|---|---|
| [0001](ADR-0001-modular-monolith.md) | Start as a modular monolith | Accepted |
| [0002](ADR-0002-versioned-fare-rules.md) | Version fare rules | Accepted |
| [0003](ADR-0003-module-layout.md) | Module folders inside shared layer projects | Superseded by [0012](ADR-0012-module-scoped-persistence-boundaries.md) |
| [0004](ADR-0004-application-pattern-and-errors.md) | Handler per use case, Result-based errors | Accepted |
| [0005](ADR-0005-api-only-backend.md) | API-only backend, separate SPA frontend | Accepted |
| [0006](ADR-0006-identifiers.md) | GUID primary keys + human business numbers | Accepted |
| [0007](ADR-0007-signed-ticket-qr.md) | Signed ticket QR with online duplicate-use check | Superseded by [0014](ADR-0014-fixed-binary-signed-ticket-qr.md) |
| [0008](ADR-0008-online-counter-phase1.md) | Counter sales online-only in Phase 1 | Superseded by [0015](ADR-0015-online-counter-recovery-and-idempotency.md) |
| [0009](ADR-0009-authentication.md) | ASP.NET Core Identity + JWT | Superseded by [0016](ADR-0016-same-origin-spa-authentication.md) |
| [0010](ADR-0010-tamper-evident-audit.md) | Audit log on SQL Server 2022 ledger table | Superseded by [0017](ADR-0017-ledger-audit-operational-boundaries.md) |
| [0011](ADR-0011-time-and-money.md) | Time, business date and money | Superseded by [0018](ADR-0018-time-business-date-and-money.md) |
| [0012](ADR-0012-module-scoped-persistence-boundaries.md) | Module-scoped persistence boundaries in the shared monolith | Accepted |
| [0013](ADR-0013-ticket-and-financial-lifecycle.md) | Independent ticket and financial lifecycles | Accepted |
| [0014](ADR-0014-fixed-binary-signed-ticket-qr.md) | Fixed-binary signed ticket QR | Accepted |
| [0015](ADR-0015-online-counter-recovery-and-idempotency.md) | Online counter recovery and session-bound idempotency | Accepted |
| [0016](ADR-0016-same-origin-spa-authentication.md) | Same-origin SPA authentication and refresh rotation | Accepted |
| [0017](ADR-0017-ledger-audit-operational-boundaries.md) | Ledger audit operational boundaries | Accepted |
| [0018](ADR-0018-time-business-date-and-money.md) | Time, business date and money representation | Accepted |
| [0019](ADR-0019-operation-owned-business-dates.md) | Operation-owned business dates | Accepted |
| [0020](ADR-0020-f001-authentication-scope-and-test-auth-handler.md) | F-001 authentication scope and the test authentication handler | Accepted |
| [0021](ADR-0021-audit-event-record-shape.md) | Audit event record shape | Accepted |
| [0022](ADR-0022-migrator-db-owner-role.md) | Migrator `db_owner` role | Accepted |
| [0023](ADR-0023-staff-account-security-and-access-tokens.md) | Staff account security and access tokens | Accepted |
| [0024](ADR-0024-client-held-version-for-edits.md) | Client-held version for edits composed from an earlier read | Proposed |
| [0025](ADR-0025-cross-module-references.md) | Cross-module references: contract shape and foreign keys | Accepted |

Rules for agents:
- **Accepted** ADRs are binding. Do not work against one; propose a new ADR that supersedes it.
- New ADRs use `docs/templates/adr.md`, take the next number, start as **Proposed**, and need human approval to become Accepted.
- Never edit the Decision section of an Accepted ADR. Supersede it instead.

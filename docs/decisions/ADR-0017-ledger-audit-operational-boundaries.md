# ADR-0017: Ledger Audit Operational Boundaries

## Status

Accepted — 2026-09-19

## Supersedes

ADR-0010.

## Context

**FACT:** SQL Server 2022 ledger detects alteration of committed ledger history when its digests are protected and verified.

**FACT:** a caller holding the authorized application credential could still fabricate a valid-looking audit row through the permitted insert path. Ledger detects alteration, not fabrication through an authorized application credential.

**ENGINEERING DECISION (tech lead, cite ADR-0017):** retain SQL Server ledger for the audit table and do not add application-level event signing in Phase 1.

## Options considered

1. SQL Server ledger with protected external digests - detects alteration and matches the production SQL Server decision.
2. Normal append-only table - cheaper, but materially weaker against privileged database tampering.
3. Application-signed audit events - stronger attribution against application forgery, but adds key management and is not approved for Phase 1.

## Decision

1. Production uses SQL Server 2022 and `audit.AuditEvents` is an append-only ledger table created by raw SQL in an EF migration.
2. Actor fields (`ActorUserId`, `ActorRole`) are populated only from the authenticated server-side context, never from client request fields.
3. The application database login has only INSERT/SELECT on the audit schema. Migration and digest administration use separate credentials.
4. Database digests are generated on a schedule, stored outside the database in immutable/WORM storage owned by a different administrator, and verified on a schedule and on demand. **ENGINEERING DECISION (tech lead, cite ADR-0017, hein, 2026-09-22):** the generation schedule is daily; alert routing and disaster-recovery ownership for the digest belong to the implementing engineering team, internally — no named external tool, since `docs/15-deployment-architecture.md` establishes none. The digest storage provider itself remains undecided; see Blocked behaviour.
5. The walking skeleton CI must execute the ledger DDL and a smoke test against a pinned SQL Server 2022 container image. The migration must fail loudly on unsupported SQL Server versions or editions.
6. Audit schema changes are additive and reviewed; EF migrations must not silently convert or drop a ledger table.

## Blocked behaviour

- **OPEN QUESTION — OQ14:** retention, deletion, archival, and regulatory handling for append-only ledger rows.
- **VERIFY:** exact ledger DDL and digest/verification procedure behavior in the selected SQL Server 2022 container image.
- **OPEN QUESTION:** digest storage provider. Blocked on an undecided hosting/infrastructure choice (`docs/15-deployment-architecture.md` names no provider), not on a ruling. The schedule and alert/disaster-recovery-ownership clauses this item previously covered are resolved by Decision item 4.

## Consequences

Positive:
- Alteration detection remains available without application-level cryptographic signing.
- Server-derived actor fields reduce client spoofing of audit identity.
- CI proves the production-specific DDL instead of relying on a normal relational table.

Negative:
- Ledger does not make a compromised application credential truthful.
- Ledger retention and schema evolution require operational planning.
- SQL Server 2022 becomes a hard development and CI prerequisite.
- Internal, unnamed ownership of digest alerting and disaster recovery means no external escalation path exists until a storage provider and its operational tooling are chosen.

# ADR-0022: Migrator `db_owner` Role

## Status

Accepted — 2026-09-22

## Supersedes

None.

## Context

**FACT:** `ycr_migrator`'s SQL Server login has the `db_owner` role (full schema DDL and DROP, not `sysadmin`), found in T-007a's re-review at `52326b3` (S-1).

**FACT:** `db_owner` is broader than the migrations themselves need — it carries `SELECT` on the ledger and DDL including `DROP`, and nothing bounds the credential from above beyond `sysadmin = 0`.

**FACT:** the append-only audit ledger (ADR-0017) detects alteration of committed history, including alteration performed through an authorized credential such as `ycr_migrator`.

## Options considered

1. Keep `db_owner` for `ycr_migrator` — full schema DDL and DROP, not `sysadmin`. Simplest, matches EF Core's need to create/alter/drop schema objects across migrations, and the ledger already compensates for the residual risk of a compromised or misused credential.
2. Narrow the role to a bespoke permission set scoped to exactly the DDL verbs migrations use. Reduces blast radius further, but adds an ongoing maintenance burden — every new migration pattern (new schema, new object type) risks silently failing against too-narrow grants, with no clear catalogue of exactly which permissions EF Core's migrator requires.

## Decision

`db_owner` (full schema DDL + DROP, not sysadmin) is the deliberate, accepted scope for the migrator identity. The append-only audit ledger (ADR-0017) is the compensating control. Narrowing the role to something more granular than db_owner was considered and declined.

## Blocked behaviour

None. This ADR closes S-1; no open question remains from it.

## Consequences

Positive:
- Migrations are not blocked by incremental, hand-maintained permission grants as schema needs evolve.
- No additional permission-set maintenance burden alongside EF Core migration authoring.

Negative:
- `ycr_migrator` can DROP any object in the database, not only the ones a given migration touches.
- A compromised or misused `ycr_migrator` credential relies on the ledger (ADR-0017) for alteration detection, not on the database role for prevention.

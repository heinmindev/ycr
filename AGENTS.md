# AGENTS.md — Engineering Constitution

## Mission

Build a production-grade Yangon Circular Railway ticketing platform.

## Non-negotiable rules

1. Do not invent railway business rules.
2. Clearly label FACT, ASSUMPTION, OPEN QUESTION, BUSINESS DECISION, and ENGINEERING DECISION (tech lead, cite ADR). Use REQUIRED CONTROL for security requirements.
3. Domain logic must not live in controllers.
4. Do not expose EF Core entities directly from APIs.
5. Protect data integrity before optimizing performance.
6. Every important business rule requires automated tests.
7. Never disable or delete tests merely to make a build pass.
8. Never silently change database behavior.
9. Record architectural decisions as ADRs.
10. Security and authorization are part of feature completion.
11. Keep changes scoped to the requested feature.
12. Update documentation when behavior or architecture changes.

## Development loop

DISCOVER → SPECIFY → PLAN → IMPLEMENT → TEST → REVIEW → SECURITY REVIEW → DOCUMENT → HUMAN APPROVAL

## Before implementation

Produce:

- Understanding
- Facts
- Assumptions
- Open questions
- Affected modules
- Domain changes
- DB changes
- API changes
- Security impact
- Test strategy
- Risks
- Implementation plan

## After implementation

Report:

- Files changed
- Tests executed
- Database changes
- Security review
- Performance considerations
- Documentation updated
- Remaining risks

## When a business rule is missing

Do not guess and do not implement a placeholder rule. Add the question to `docs/19-open-questions.md`, mark the affected spec as blocked, and stop for a human decision.

## Binding decisions

- Accepted ADRs in `docs/decisions/` (index: `docs/decisions/README.md`) are binding. To change one, propose a superseding ADR.
- Code follows `docs/20-coding-conventions.md`. Work is done when `docs/21-definition-of-done.md` is satisfied.

## What to read for what

| Task | Read first |
|---|---|
| Any feature | `docs/workflows/02-feature-development.md`, `docs/20`, `docs/21`, the module's docs |
| Tickets / lifecycle | `docs/11`, ADR-0013, ADR-0014 |
| Fares | `docs/12`, ADR-0002, ADR-0018 |
| Payments / refunds / cashier sessions | `docs/13`, ADR-0015, ADR-0018, `docs/20` §Idempotency |
| Auth / permissions | `docs/09`, `docs/10`, ADR-0016 |
| Database change | `docs/07`, `docs/workflows/04-database-change.md`, ADR-0006, ADR-0017, ADR-0019 |
| Module boundaries | `docs/05`, ADR-0012 |

## Task ledger (multiple agents)

Codex, Claude and humans coordinate through `TASKS.md` at the repository root. Read its Protocol before starting.

- Claim before any work: lock the task with `git branch claim/T-xxx main` (this fails if the task is already taken), then commit your row update on `main` from the coordination checkout.
- Read and edit `TASKS.md` only in the coordination checkout. The copy inside a worktree is stale.
- Work in the feature's worktree (`feature/F-xxx`) or the task's worktree, never in the coordination checkout. Log checkpoints in the row's Progress file (template `docs/templates/progress.md`).
- Scenario tests, code review and security review must be done by a different agent than the implementer.
- When blocked, record the blocker type. Only business blockers need an OQ in `docs/19`.

## Where work artifacts go

`docs/features/<id>-<slug>/spec.md`, `plan.md`, `review.md`, using `docs/templates/`. Every workflow stage writes its output there, so work survives across sessions.

## Commands

Valid once the walking skeleton exists:

```bash
dotnet build YCR.sln
dotnet test YCR.sln
docker compose up -d        # SQL Server 2022 for local dev and integration tests
```

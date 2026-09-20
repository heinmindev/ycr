# Feature Development Workflow

Every feature gets a folder: `docs/features/<id>-<slug>/` (e.g. `F-003-create-station`).
**⛔ = stop and wait for human approval.** Do not continue in the same session without it.

| Stage | Who / prompt | Input | Output | Exit criteria |
|---|---|---|---|---|
| 1. DISCOVER | discovery-agent | FR/UC, docs, `docs/19` | notes in `spec.md` §0 and §3, with source and decision owner | Every rule labelled; missing rules added to `docs/19` |
| 2. SPECIFY | discovery-agent | stage 1 | `spec.md` (template `docs/templates/feature-spec.md`) | No blocking OPEN QUESTION ⛔ |
| 3. PLAN | architect-agent | approved spec, ADRs, `docs/20` | `plan.md` (template `docs/templates/plan.md`) | Files, tests, migration and risks listed ⛔ |
| 4. IMPLEMENT | implementation-agent | approved plan | code + migration | Plan steps done, each ending with green tests |
| 5. TEST | testing-agent | spec §4 scenarios | tests | All scenarios covered; `dotnet test` green |
| 6. REVIEW | review-agent | diff, spec, `docs/21` | `review.md` (template `docs/templates/review-report.md`) | No blocking findings |
| 7. SECURITY REVIEW | security-agent | diff, `docs/09`, `docs/18` | `review.md` §Security review with threat IDs and evidence | No open Critical/High |
| 8. DOCUMENT | implementation-agent | all of the above | updated docs 07/08/10/11/…, glossary, ADR if needed | Docs match behaviour |
| 9. HUMAN APPROVAL | lead | `review.md` + PR | PR approved ⛔ | `docs/21` fully checked |

Rules:
- If a later stage finds a spec gap, go back to stage 2. Don't patch around it in code.
- Never implement unrelated refactors in the same feature unless explicitly requested.
- Each stage may run in a fresh session. The feature folder is the handoff, so write enough in it for the next agent to continue without this conversation.
- Every stage records source file/section, decision owner, commands/tests executed, and unresolved `OPEN QUESTION` or `BLOCKED` behavior in the feature folder.
- Each stage is a task in `TASKS.md`. Claim it before starting (TASKS.md §Protocol). All stages of a feature run on one branch, `feature/<id>`, one after another, and log checkpoints in `docs/features/<id>-<slug>/progress.md`.
- Stage 5 (tests), stage 6 (review) and stage 7 (security) are done by a different agent than stage 4. Stage 8 remediation is done by the stage 4 owner, and affected reviews then re-run against the new commit.

# Task Ledger

Single source of truth for **who is working on what**. Codex, Claude and humans all use this file.
Agents: read this whole file, including the Protocol, before picking a task.

**Status:** `todo` · `doing` · `review` (implementation waiting on reviews) · `blocked` · `done`
**Owner:** `codex` · `claude` · `hein` (human) · `—` (unclaimed). One owner per active task; contributors are listed in the Done table.
**Blocker types:** `business` (needs an OQ in docs/19) · `engineering` (needs an ADR or tech-lead decision) · `dependency` · `hardware` · `external` · `approval` (waiting at a ⛔ stop)
**Updated:** `YYYY-MM-DD HH:MM` in Asia/Yangon time.

## Active and pending

| ID | Task | Status | Owner | Depends on | Branch | Output / exit criteria | Progress file | Updated | Notes / blocker |
|---|---|---|---|---|---|---|---|---|---|
| T-001 | Commit current docs as baseline (`git init` if needed); tag `docs-baseline-2026-09-19` | done | hein | — | main | Tag exists on `main` | — | 2026-09-19 23:00 | Human only |
| T-002 | F-001 walking skeleton: stages 1–2, discover + spec | doing | claude | T-001 | feature/F-001 | `docs/features/F-001-walking-skeleton/spec.md` per workflow 02 ⛔ | `docs/features/F-001-walking-skeleton/progress.md` | 2026-09-20 01:53 | Stops for human approval |
| T-003 | F-001 stage 3, plan | todo | — | T-002 | feature/F-001 | `plan.md` ⛔ | same as T-002 | 2026-09-19 21:00 | Stops for human approval |
| T-004 | F-001 stage 4, implement (unit tests with each plan step) | todo | — | T-003 | feature/F-001 | Plan steps done; `dotnet test` green; commit SHA recorded in Notes | same as T-002 | 2026-09-19 21:00 | |
| T-005 | F-001 stage 5, scenario tests from spec §4 (testing-agent prompt) | todo | — | T-004 | feature/F-001 | All spec scenarios covered; `dotnet test` green | same as T-002 | 2026-09-19 21:00 | Owner ≠ T-004 owner |
| T-006 | F-001 stage 6, code review of the commit named in T-005 Notes | todo | — | T-005 | feature/F-001 | `review.md` with verdict | same as T-002 | 2026-09-19 21:00 | Owner ≠ T-004 owner |
| T-007 | F-001 stage 7, security review | todo | — | T-006 | feature/F-001 | `review.md` §Security, no open Critical/High | same as T-002 | 2026-09-19 21:00 | Owner ≠ T-004 owner |
| T-008 | F-001 stage 8, remediate findings + update docs | todo | — | T-006, T-007 | feature/F-001 | All blocking findings fixed; affected reviews re-run; docs match behaviour | same as T-002 | 2026-09-19 21:00 | Owner = T-004 owner |
| T-009 | F-001 stage 9, human approval + merge `feature/F-001` → `main` | todo | hein | T-008 | main | Merged; `docs/21` fully checked | same as T-002 | 2026-09-19 21:00 | Human only; closes T-004 |
| T-010 | Decide: create glossary (`docs/glossary.md`) | todo | hein | — | — | Decision recorded in Notes | — | 2026-09-19 21:00 | Human decision |
| T-011 | Decide: accept ADR-0001 and ADR-0002 | todo | hein | — | — | ADR status updated | — | 2026-09-19 21:00 | Human decision |
| T-012 | Draft business-questions pack for Myanma Railways from docs/19 (why each matters, what it blocks, suggested options) | todo | — | T-001 | task/T-012-mr-questions | `docs/business/mr-questions-pack.md` | `docs/progress/T-012.md` | 2026-09-19 21:00 | Must not propose answers as decisions |
| T-013 | Physical QR print/scan test using the `ticket-qr-v1.json` valid vector (ADR-0014 VERIFY) | blocked | — | — | task/T-013-qr-physical | `docs/reviews/qr-physical-test.md`: printer, paper, size, scanner, results | `docs/progress/T-013.md` | 2026-09-19 21:00 | hardware: thermal printer + inspector scanner. Needs no app code |

## Done

| ID | Task | Contributors | Completed | Evidence |
|---|---|---|---|---|
| T-000 | Starter-kit design review, ADR-0012–0019, QR vectors, task ledger | claude, codex | 2026-09-19 21:00 | `docs/reviews/2026-09-19-*.md` |

## Protocol

### Checkouts and branches

- **Coordination checkout:** the original repository folder. It always stays on `main` and is used only for ledger commits and human merges. Agents never write code there.
- **One branch per feature** (`feature/F-001`), with one worktree per feature (`git worktree add ../ycr-F-001 feature/F-001`). A feature's stages run one after another, so only the current stage owner works in that worktree. Parallel work happens across different features or tasks, never within one feature.
- **Standalone tasks** (not feature stages) use `task/T-xxx-<slug>` in their own worktree, and are merged to `main` by a human when done.
- Reviews always name the exact commit SHA they reviewed.
- **The only live ledger is `TASKS.md` in the coordination checkout.** Every worktree also has a copy of `TASKS.md`, and that copy is stale. Never read the worktree copy for status and never edit it. When merging a branch to `main`, keep `main`'s version of `TASKS.md`.

### Picking and claiming a task

1. **Pick** the first row that has Status `todo`, Owner `—`, and all dependencies `done`. Never take a row owned by someone else, including `hein`. Hold only one `doing` task at a time.
2. **Lock it atomically**, from the coordination checkout:
   `git branch claim/T-xxx main`
   Git creates a ref only once, so if this command fails, someone else has the task: pick another. Don't edit the ledger before the lock succeeds.
3. **Record the claim.** Re-read `TASKS.md` from disk, edit **only your row** (Status `doing`, Owner, Updated), and commit on `main` with the message `chore(tasks): claim T-xxx`. If git reports `index.lock`, wait a few seconds and retry.
4. **Verify** with `git show --stat HEAD` and the diff: your commit must change only your own row. If it reverted anyone else's change, restore it immediately.
5. Once the task is `done`, delete the lock with `git branch -D claim/T-xxx`.

### While working

6. Work in the feature or task worktree, never in the coordination checkout.
7. **Progress log.** At every checkpoint (stage finished, session ending, blocker hit), append an entry to the row's Progress file using `docs/templates/progress.md`. A new session must be able to continue from that file alone. Tasks with Progress `—` are human-only.
8. **Blocked.** Set Status `blocked` and write the blocker type and reason in Notes. Only `business` blockers need an OQ in `docs/19`. Stop; don't work around the blocker.

### Finishing and the review loop

9. A task becomes `done` only when its **Output / exit criteria** are met. At a ⛔ stop, set Status `blocked` with blocker type `approval` until a human approves. The human then sets the row to `done`.
10. **Implementation tasks** (for example T-004) move to `review` once they're finished, and record the commit SHA in Notes.
    - If a review finds blocking issues, the reviewer notes them in `review.md`, and the remediation task (T-008) addresses them. Affected reviews are set back to `todo` and re-run against the new SHA.
    - The implementation task becomes `done` only when the human approval and merge task (T-009) is `done`.
11. **Cross-review.** Scenario tests, code review and security review must be done by a different agent than the implementer. Remediation is done by the implementer.
12. **Stale claims.** Only a human may release a `doing` task whose progress file has had no entry for 24 hours. The human deletes its `claim/` ref and resets the row.
13. **New work.** Agents may add `todo` rows (next free ID, Owner `—`). They must not reorder, delete or reassign other rows; humans own the priorities.

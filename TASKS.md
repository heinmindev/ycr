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
| T-002 | F-001 walking skeleton: stages 1–2, discover + spec | done | claude | T-001 | feature/F-001 | `docs/features/F-001-walking-skeleton/spec.md` per workflow 02 ⛔ | `docs/features/F-001-walking-skeleton/progress.md` | 2026-09-20 02:27 | Spec Approved (hein, 2026-09-20) at `cb20c7c` on `feature/F-001`. Provisional station rules under the explicit waiver in spec §Blocked behaviour; replacement tracked by T-014. ADR-0020 and ADR-0021 added as Proposed. |
| T-003 | F-001 stage 3, plan | done | claude | T-002 | feature/F-001 | `plan.md` ⛔ | same as T-002 | 2026-09-20 04:25 | Plan Approved (hein, 2026-09-20), revision 4 at `bbe8dda`. P1-P12 all resolved; spec Amendment 1 adds S27 |
| T-004 | F-001 stage 4, implement (unit tests with each plan step) | review | claude | T-003 | feature/F-001 | Plan steps done; `dotnet test` green; commit SHA recorded in Notes | same as T-002 | 2026-09-21 06:20 | **All 13 plan steps done at `9106d53`** on `feature/F-001`. CI green on both events at that SHA: push [35577574172](https://github.com/heinmindev/ycr/actions/runs/35577574172) and pull_request [35577575935](https://github.com/heinmindev/ycr/actions/runs/35577575935) (all four jobs, trunk-only included). Branch run 147 passed / 0 skipped; trunk-only run 4 passed — complements, so every test ran. V1–V6 all resolved; details in the progress file. [PR #1](https://github.com/heinmindev/ycr/pull/1) is open and **must not be merged by an agent** — T-009 is the human gate. **Authorship split: steps 1 and 7–13 by claude; steps 2–6 and the step-5 review fixes by codex (up to `89551cb`).** Under §Protocol item 11 neither may review their own steps, so T-005–T-007 need a third agent or a split by author. |
| T-005 | F-001 stage 5, scenario tests from spec §4 (testing-agent prompt) | done | codex | T-004 | feature/F-001 | All spec scenarios covered; `dotnet test` green | same as T-002 | 2026-09-21 16:05 | Review report: `docs/features/F-001-walking-skeleton/review-codex.md` at `269bbc2`; one Medium S9 coverage finding is open for T-008. |
| T-006 | F-001 stage 6, code review of the commit named in T-005 Notes | todo | — | T-005 | feature/F-001 | `review.md` with verdict | same as T-002 | 2026-09-19 21:00 | Owner ≠ T-004 owner |
| T-006b | F-001 stage 6b, code review of **codex's** part of stage 4 (plan steps 2-6 and the step-5 review fixes, `0572ff3..89551cb`) at `9106d53` | done | claude | T-004 | feature/F-001 | `docs/features/F-001-walking-skeleton/review-claude.md` with verdict | same as T-002 | 2026-09-21 09:45 | **Verdict: Ready. No blocking finding.** Review at `c27596703136219aacad5f633bd965c990aa1400` on `feature/F-001`. Twelve findings C-1..C-12, none Critical or High; C-9 and C-10 already remediated by later commits, the other ten are T-008's. Evidence: build 0 warnings / 0 errors; Domain 30, Architecture 12, the two codex Infrastructure classes 3 — all passed, 0 skipped; whole suite from CI at `9106d53`. T-006 stays open for claude's steps 1 and 7-13 (§Protocol item 11). Ran ahead of T-005; scenario coverage not asserted. |
| T-006a | F-001 stage 6a, code review of claude's stage-4 commits (step 1 and steps 7–13) at `9106d53` | done | codex | T-005 | feature/F-001 | `docs/features/F-001-walking-skeleton/review-codex.md` with verdict | same as T-002 | 2026-09-21 16:35 | **Verdict: Not ready.** Review report at `43c9dbe`; High F-006A-1 (fixture uses SA as migrator) and Low F-006A-2 (CI progress path ignore) open for T-008. |
| T-007b | F-001 stage 7b, security review of the same codex commits at `9106d53` | done | claude | T-006b | feature/F-001 | `review-claude.md` §Security, no open Critical/High | same as T-002 | 2026-09-21 09:45 | **No open Critical or High findings.** `review-claude.md` §Security review, at `c27596703136219aacad5f633bd965c990aa1400`. Threat categories with a surface in range: unauthorized configuration, privilege escalation, insider manipulation, data disclosure, API abuse; `docs/18`'s authentication controls have no surface in codex's files. No secrets, no raw SQL, non-backtracking validation regex, no seeded role grants. C-9 (caller-supplied `authorizedByPermission` in the original `IAuditWriter`) was already closed at `6dfb67a`. T-007 stays open for claude's steps 1 and 7-13. |
| T-007a | F-001 stage 7a, security review of claude's stage-4 commits (step 1 and steps 7–13) at `9106d53` | done | codex | T-006a | feature/F-001 | `docs/features/F-001-walking-skeleton/review-codex.md` §Security, no open Critical/High | same as T-002 | 2026-09-21 17:05 | **Verdict: Not ready.** Review report at `0f66e06`; High S-007A-1/F-006A-1 (fixture uses SA as migrator) and Medium S-007A-2 (passwords in process args) open for T-008. |
| T-007 | F-001 stage 7, security review | todo | — | T-006 | feature/F-001 | `review.md` §Security, no open Critical/High | same as T-002 | 2026-09-19 21:00 | Owner ≠ T-004 owner |
| T-008 | F-001 stage 8, remediate findings + update docs | todo | — | T-006, T-007 | feature/F-001 | All blocking findings fixed; affected reviews re-run; docs match behaviour | same as T-002 | 2026-09-19 21:00 | Owner = T-004 owner |
| T-009 | F-001 stage 9, human approval + merge `feature/F-001` → `main` | todo | hein | T-008 | main | Merged; `docs/21` fully checked | same as T-002 | 2026-09-19 21:00 | Human only; closes T-004 |
| T-010 | Decide: create glossary (`docs/glossary.md`) | done | hein | — | — | Decision recorded in Notes | — | 2026-09-20 02:37 | Decided yes; see T-015 |
| T-011 | Decide: accept ADR-0001 and ADR-0002 | done | hein | — | — | ADR status updated | — | 2026-09-20 02:37 | ADR-0001 and ADR-0002 Accepted (hein, 2026-09-20) at `564e081` on `feature/F-001`; index updated |
| T-012 | Draft business-questions pack for Myanma Railways from docs/19 (why each matters, what it blocks, suggested options) | review | codex | T-001 | task/T-012-mr-questions | `docs/business/mr-questions-pack.md` | `docs/progress/T-012.md` | 2026-09-20 09:35 | OQ22 excluded as tech-lead engineering decision; OQ26–28 added as provisional F-001 placeholders, not answers; merge cb8d575; output bf87872; progress 34ddc2e |
| T-013 | Physical QR print/scan test using the `ticket-qr-v1.json` valid vector (ADR-0014 VERIFY) | blocked | — | — | task/T-013-qr-physical | `docs/reviews/qr-physical-test.md`: printer, paper, size, scanner, results | `docs/progress/T-013.md` | 2026-09-19 21:00 | hardware: thermal printer + inspector scanner. Needs no app code |
| T-014 | Replace F-001's provisional station rules with the answered OQ26/OQ27/OQ28 rules | blocked | — | T-009 | task/T-014-station-rules | `StationCode`, `BilingualName` and the station permission constants match the approved answers; spec R3/R4/R8 relabelled; the waiver in spec §Blocked behaviour removed; `docs/10` role grants recorded | `docs/progress/T-014.md` | 2026-09-20 02:27 | business: OQ26, OQ27, OQ28 unanswered by Myanma Railways. Release gate — F-001's provisional station rules must not reach production until this is done |
| T-015 | Create `docs/glossary.md` from docs 03/04/05/10/11 and the ADRs; canonical English term, Myanmar term (OPEN QUESTION where unknown, never invented), forbidden synonyms | review | codex | T-010 | task/T-015-glossary | `docs/glossary.md` | `docs/progress/T-015.md` | 2026-09-20 11:48 | Glossary commit b053a65; progress commits 2849f67 and c4bf936; Myanmar translations remain OPEN QUESTION where no authoritative term exists |
| T-016 | Cross-review T-012's MR questions pack at `34ddc2e` (`task/T-012-mr-questions`) against `docs/19` on `main` | done | claude | T-012 | task/T-012-mr-questions | `docs/reviews/2026-09-21-claude-review-of-T-012.md` with verdict | `docs/progress/T-012.md` | 2026-09-21 09:35 | **Verdict: ready as an internal draft; NOT ready to send to Myanma Railways until T12-1 and T12-3 are closed.** Review at `6491f4a` on `task/T-012-mr-questions`, covering pack SHA `34ddc2e`. No Critical or High. Medium: **T12-1** OQ22's exclusion is nowhere stated in the pack (the OQ20 gap is explained, this one is not); **T12-2** four unresolved items live only in ADR-0015/0017/0018/0019 and never reached `docs/19`, so they cannot reach MR — cashier-session length blocks `docs/20` §5 idempotency retention — **needs a hein ruling**; **T12-3** OQ5 and OQ11 lack the `Status:` block OQ26-OQ28 get, and `docs/19`'s "this is not a BUSINESS DECISION" is dropped from OQ11. Low: T12-4 label wording, T12-5 requirement riders, T12-6 no OQ index, T12-7 add OQ29 (Zawgyi, from review-claude.md C-11) once the F-001 remediation lands it in `docs/19`. Passes: 26/26 questions complete; no option list favours its own option; OQ26-OQ28 values transcribed without drift. Pack not edited. |
| T-017 | Cross-review T-015's glossary at `c4bf936` (`task/T-015-glossary`) against docs 03/04/05/10/11, the ADRs and the F-001 code terms at `9106d53` | doing | claude | T-015 | task/T-015-glossary | `docs/reviews/2026-09-21-claude-review-of-T-015.md` with verdict | `docs/progress/T-015.md` | 2026-09-21 09:45 | Cross-review under §Protocol item 11: owner must not be codex, who wrote T-015. Dependency T-015 is in `review`, per §Protocol item 10. Hardest checks: no invented Myanmar term, canonical English terms match code and ADRs, forbidden synonyms absent from docs 03-11 and the F-001 code. Findings only — the reviewer does not edit the glossary. |

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
- **Push `main` after every ledger commit** (`git push origin main`), so the ledger other agents read is never stale.
- **Never push a `claim/*` ref.** Locks are local coordination; pushing one would leave a stale claim that another machine cannot clear.

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

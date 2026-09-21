# Review: T-012 — Myanma Railways business-questions pack

Reviewer: **claude** (review-agent). Task **T-016**, cross-review under `TASKS.md` §Protocol item 11 — T-012 was written by codex.

**Commit reviewed: `34ddc2ea4c4168641d4b32cca767aab21396a130`** (`34ddc2e`) on `task/T-012-mr-questions`.
Artefacts: `docs/business/mr-questions-pack.md` (250 lines, content commit `bf87872`) and `docs/progress/T-012.md` (117 lines).
Source of truth for coverage: **`docs/19-open-questions.md` on `main` at `1528dac`**. Verified identical to the copy in this worktree (`git diff main -- docs/19-open-questions.md` is empty), so the pack was built from the same text this review checks it against.

Also read: `AGENTS.md`, `TASKS.md` §Protocol, `docs/10-authorization-matrix.md`, `docs/20-coding-conventions.md` §§2/5/6, ADR-0005, ADR-0006, ADR-0013, ADR-0014, ADR-0015, ADR-0017, ADR-0018, ADR-0019, `docs/features/F-001-walking-skeleton/spec.md` §3, and `docs/features/F-001-walking-skeleton/review-claude.md` at `c275967` (for C-11 / OQ29).

**This review changes nothing.** Per the task brief it records findings only; the pack was not edited.

---

## Checks run

| # | Check | Command / method | Result |
|---|---|---|---|
| 1 | Which OQ numbers appear in the pack | `grep -o "OQ[0-9]\+" … \| sort -u -V` | OQ1–OQ21, OQ23–OQ28. **OQ22 appears nowhere.** |
| 2 | Every question carries why / blocks / options / owner / sources | `awk` over `### OQ` sections, counting the five bold field labels | **26 / 26 complete.** Every business question has all five. OQ20 correctly has none — it is a resolved-item note, not a question. |
| 3 | OQ26–OQ28 marked as open with provisional values labelled | read `mr-questions-pack.md:31-64` against `docs/19` OQ26–OQ28 | **Pass.** Each carries a `**Status:**` block: "**OPEN QUESTION** for Myanma Railways … **not a Myanma Railways answer**". Provisional values match `docs/19` exactly (see §Fidelity below). |
| 4 | No option list favours one of its own options | read all 26 `Suggested options (non-binding)` lines | **Pass** — no option is marked preferred, recommended, default or "current design". Two lists append a requirement rider (T12-5). |
| 5 | OQ29 (Zawgyi) status | `grep -rn "OQ29" docs/` | **No OQ29 anywhere.** Correctly absent today; see T12-7. |
| 6 | Unnumbered OPEN QUESTIONs in ADRs vs `docs/19` | `grep -rn "OPEN QUESTION" docs/decisions/*.md \| grep -v "OQ[0-9]"`, then each hit against `docs/19` | **Four items exist only in ADRs** and never reached `docs/19` — see T12-2. |

Verbatim output of check 1:

```
OQ1 OQ2 OQ3 OQ4 OQ5 OQ6 OQ7 OQ8 OQ9 OQ10 OQ11 OQ12 OQ13 OQ14 OQ15
OQ16 OQ17 OQ18 OQ19 OQ20 OQ21 OQ23 OQ24 OQ25 OQ26 OQ27 OQ28
```

### Fidelity of the provisional F-001 values

Checked word by word, because this is the one place where a transcription slip would put a tech-lead placeholder in front of Myanma Railways as if it were their own rule.

| OQ | `docs/19` says | Pack says (line) | Match |
|---|---|---|---|
| OQ26 | 2–10 characters, `A`–`Z` and `0`–`9` only, unique across all stations including inactive, never reused | "2–10 characters, uppercase `A`–`Z` and `0`–`9`, unique across active and inactive stations, and never reused" (33) | ✓ (also matches the code: `StationCode.cs` `^[A-Z0-9]{2,10}$`) |
| OQ27 | `NameEn` and `NameMy` both required, each 1–100 characters after trim, neither unique | "`NameEn` and `NameMy` are both required, each 1–100 characters after trimming, and neither is unique" (42) | ✓ |
| OQ28 | F-001 seeds no role→permission grants; its tests mint permissions directly | "seeds no role-to-permission grants, and mints permissions directly in tests" (59) | ✓ |

The pack also carries `docs/19`'s two `BLOCKS:` annotations faithfully — OQ8's `AuthenticUnverified` handling (pack line 135) and OQ10's ticket-cancellation and refund transitions (pack line 187) — and it keeps T-014 named as the release gate in all three of OQ26, OQ27 and OQ28.

---

## Findings

| # | Severity | Finding | Evidence (file:line) | Recommended action | Status |
|---|---|---|---|---|---|
| **T12-1** | Medium | **OQ22's exclusion is nowhere stated in the pack.** The pack devotes a whole section to explaining why it skips OQ20 — "included here only to make clear why the pack skips its numbering" — and then skips OQ22 in silence. A reader checking the pack against `docs/19` finds exactly one unexplained gap in OQ1–OQ28 and has no way to tell a deliberate exclusion from an oversight. The reason **does** exist and is sound (OQ22 is the SPA framework, an ENGINEERING DECISION for the tech lead under ADR-0005, not a Myanma Railways question) — but it lives only in `TASKS.md` T-012 Notes and `docs/progress/T-012.md:78`, and neither travels with the pack when it is sent out. The exclusion was deliberate: codex removed the section in `4e675f9` after an earlier review. Only the statement is missing. | Absent from `docs/business/mr-questions-pack.md` (confirmed by check 1); explained only at `docs/progress/T-012.md:51,70,78` and in the ledger T-012 Notes; the contrasting treatment of OQ20 is at `mr-questions-pack.md:234-238` | Add OQ22 to the existing "Resolved item not requiring a business answer" section — retitled to cover both cases — stating that OQ22 is an **ENGINEERING DECISION** for the tech lead (ADR-0005) and that no Myanma Railways answer is requested. One sentence closes it. | Open — T-012 owner |
| **T12-2** | Medium | **Four unresolved items live only in the ADRs, never reached `docs/19`, and are therefore invisible to this pack.** The pack's scope is correctly and explicitly "the unresolved business questions in `docs/19-open-questions.md`", so this is a completeness defect in `docs/19` that the pack inherits rather than a defect the pack author introduced — but a cross-review is where it has to surface, because the consequence is that these questions will never be put to Myanma Railways. Confirmed absent from `docs/19` by grep. Three of the four are business decisions: (a) **maximum cashier-session length and whether one operator may hold more than one open session** — this one has a concrete downstream block, since `docs/20` §5 defines idempotency-key retention as "the maximum cashier-session length plus an operational margin", so the retention rule cannot be implemented without it; (b) **Finance confirmation for no-session refund business dates**, which names Finance as the decider; (c) **digest storage provider, schedule, alert routing and disaster-recovery ownership**, whose ownership half is a business decision (the pack's OQ25 covers key-compromise ownership and OQ14 covers retention, but neither covers digest custody). A fourth is an unnumbered **ASSUMPTION** that requires a business action: "station connectivity is sufficient for online operations; it must be validated by a survey." A fifth ADR item, the Roslyn-analyzer question, is correctly engineering and out of scope. | `docs/decisions/ADR-0018-time-business-date-and-money.md:50,51`; `ADR-0019-operation-owned-business-dates.md:40,41,42`; `ADR-0017-ledger-audit-operational-boundaries.md:38`; `ADR-0015-online-counter-recovery-and-idempotency.md:44`; `ADR-0012-module-scoped-persistence-boundaries.md:38` (correctly excluded, engineering); `docs/20-coding-conventions.md:179` (the retention dependency); all four verified absent from `docs/19-open-questions.md` | **Needs a human ruling** on which of the four are business questions for Myanma Railways and which are tech-lead calls. Then number the business ones into `docs/19` and add them to the pack. Not the T-012 owner's call to make alone. | Open — needs hein |
| **T12-3** | Medium | **OQ5 and OQ11 carry project-side assumptions that the pack does not mark the way it marks OQ26–OQ28.** `docs/19` #11 states the cash-only position and then says, in terms, "**this is not a BUSINESS DECISION**". The pack drops that sentence from the body: the assumption survives only as a parenthetical in the Sources line — "(cash-only is labelled an assumption)" — while option (a) presents "cash only for Phase 1" as an ordinary neutral option with nothing saying it is what the system currently does. OQ5 is the same shape: the heading carries "(Initial assumption: no.)" with no owner, no label and no "not a Myanma Railways answer" marker, and option (a) is "no reservation". A reader in the workshop sees the project's current provisional position sitting first in a list of equals and may reasonably read it as the recommended answer — which is precisely the failure mode the OQ26–OQ28 `Status:` blocks were written to prevent. The pack already knows how to do this correctly; it just does not do it here. | `mr-questions-pack.md:108` and `:112` (OQ5); `:192-198` (OQ11, assumption only at `:198`); contrast the correct treatment at `:33`, `:42`, `:59`; source labels at `docs/19-open-questions.md` items 5 and 11 | Give OQ5 and OQ11 the same `**Status:**` block as OQ26–OQ28, carrying `docs/19`'s own words including "this is not a BUSINESS DECISION" for OQ11, and mark which option restates the current provisional position. | Open — T-012 owner |
| **T12-4** | Low | **The label wording drifts from AGENTS.md rule 2.** The pack calls the F-001 placeholders a "provisional tech-lead **engineering assumption**" and an "**engineering placeholder**"; `docs/19` and the F-001 spec call them a "provisional tech-lead **ASSUMPTION**". AGENTS.md rule 2 fixes the label set — FACT, ASSUMPTION, OPEN QUESTION, BUSINESS DECISION, ENGINEERING DECISION — and the pack's own §Status paragraph uses "**ENGINEERING DECISION**" in bold for a different and stronger meaning ("implementation constraints already accepted by the project"). Putting the word *engineering* next to *assumption* invites a reader to collapse the two and treat a placeholder as a settled call. The surrounding sentences are unambiguous, which is why this is Low and not Medium. | `mr-questions-pack.md:33,42,59` ("engineering assumption", "engineering placeholder") vs `docs/19-open-questions.md` OQ26/OQ27 ("provisional tech-lead ASSUMPTION (hein, 2026-09-20)") and `mr-questions-pack.md:5` (bold ENGINEERING DECISION, different meaning) | Use the exact label: "provisional tech-lead **ASSUMPTION** (hein, 2026-09-20)". | Open — T-012 owner |
| **T12-5** | Low | **Two option lists append a requirement rider.** OQ10 closes with "The policy should also state fees, evidence, deadlines, and whether a refunded ticket is cancelled"; OQ25 with "In every option, define compromise detection, approval thresholds, revocation SLA, trust-list distribution, device refresh, and passenger remediation." Neither favours one option over another, so neither smuggles a decision *between* the options — the brief's main concern is satisfied. What they do is assert obligations on the answer ("should", "revocation SLA") that Myanma Railways has not agreed to, inside a document whose stated contract is that it proposes nothing. | `mr-questions-pack.md:188` (OQ10); `:230` (OQ25) | Reframe as a prompt rather than a requirement — e.g. "the workshop should also capture …" — or move both to a separate "what a complete answer needs to cover" line outside the options list. | Open — T-012 owner |
| **T12-6** | Low | **No OQ-number index, and the pack is ordered thematically.** Sections run OQ1, 2, 26, 27, 12, 28, 13, 15, 16, 3, 4, 5, 6, 7, 8, 19, 23, 9, 17, 21, 10, 11, 18, 24, 14, 25, 20. The thematic grouping is good for a workshop and should stay, but it means coverage against `docs/19` cannot be confirmed by reading — it needs a script. This is the defect that let T12-1 through: codex's own completeness check was, by its own description, a "source/pack question-number validation **excluding OQ22** from business-pack expectations", so the check could confirm the section was gone but could never notice that nothing replaced it. | `mr-questions-pack.md:13-238` (section order); `docs/progress/T-012.md:74,93` (the check's own exclusion) | Add a short coverage table near the top — OQ number → section → status (asked / resolved / excluded-as-engineering) — so completeness is verifiable by eye and every gap has to be spelled. | Open — T-012 owner |
| **T12-7** | Low | **OQ29 (Zawgyi input handling) is pending and must be added to the pack once the F-001 remediation lands it in `docs/19`.** `review-claude.md` finding C-11 at `c275967` records that `docs/20` §6's "Never store Zawgyi" is a FACT with no control: `BilingualName` validates presence and length only, Zawgyi occupies the same Unicode range as correct Myanmar, and whether it must be rejected at the boundary, normalised, or merely never produced by our own clients is answered nowhere in `docs/`. Verified: `grep -rn "OQ29" docs/` returns nothing, so **the pack is correct as it stands** — an OQ that does not exist yet cannot be missing from it. Recorded here so the dependency is not lost between the two tasks. | `docs/features/F-001-walking-skeleton/review-claude.md:144` (C-11) and `:190`, at `c275967` on `feature/F-001`; `grep -rn "OQ29" docs/` → no match; related `docs/19` OQ27 and the T-014 release gate | **Add OQ29 to the pack once the F-001 remediation task lands it in `docs/19`**, most naturally beside OQ27, which already owns station naming and which T-014 will replace at the same time. Note for the ledger: the brief calls this task T-008b; the row currently in `TASKS.md` is **T-008**. | Deferred — depends on the F-001 remediation task |

### What the pack gets right

Recorded because a remediator should not undo it:

- **Every one of the 26 business questions carries all five elements** — why it matters, what it blocks or constrains, non-binding options, a suggested decision owner, and sources. Verified by script, not by sampling; 26 / 26.
- **No option list favours one of its own options.** I read all 26. There is no "recommended", no "preferred", no "current design", and no ordering cue that would make option (a) read as the answer. For a document written by the party that would benefit from a particular answer, that discipline is the whole point of the artefact.
- **The OQ26–OQ28 `Status:` blocks are exactly right**, and the provisional values are transcribed from `docs/19` without drift (table above). Each names T-014 as the release gate, so a reader who accepts the placeholder still learns it has an expiry.
- **Decision-owner routing is offered as a proposal, not an assumption of authority** — "(role to be nominated)" on every entry, and an explicit paragraph saying the labels are "proposed routing labels, not assumed authorities".
- **The process instruction at the top is correct**: an answer goes into `docs/19` and, where it changes a binding constraint, into a superseding or new ADR before implementation. That matches AGENTS.md §Binding decisions.

---

## Verdict

**Ready as an internal draft. Not ready to send to Myanma Railways until T12-1 and T12-3 are closed.**

No Critical or High finding. No question is misrepresented, no answer is invented, and nothing in the pack asks Myanma Railways to ratify a decision the project has already made — the property that matters most here holds.

Three Medium findings:

- **T12-1** and **T12-3** are the two to fix before the pack goes out, and both are small edits. T12-1 is one sentence about OQ22. T12-3 is applying a `Status:` block the pack already uses correctly three times to two more questions.
- **T12-2 needs a human ruling** and is not the T-012 owner's to resolve: four unresolved items sit in ADR-0015, ADR-0017, ADR-0018 and ADR-0019 and were never numbered into `docs/19`, so they cannot reach Myanma Railways through this pack. Someone has to decide which are business questions. The cashier-session-length one has a real downstream block — `docs/20` §5 defines idempotency-key retention in terms of it.

Four Low findings (T12-4 to T12-7) are wording and process hygiene. T12-7 requires no action now; it is a dependency to honour later.

### What this verdict does not cover

- **Whether the options themselves are the right options for Myanma Railways.** I checked that they are neutral, sourced and complete against `docs/19`. Whether option (b) for OQ17 is commercially sensible is a business judgement, not a review finding.
- **The suggested decision-owner routing.** `docs/progress/T-012.md:38` asks a human to confirm the routing is suitable for Myanma Railways' actual org structure. Nothing in the repository establishes that structure, so I cannot check it and did not try.
- **`docs/19`'s own correctness.** I checked the pack against `docs/19`, and separately found four items that never reached `docs/19` (T12-2). I did not audit whether every question already in `docs/19` is well posed.

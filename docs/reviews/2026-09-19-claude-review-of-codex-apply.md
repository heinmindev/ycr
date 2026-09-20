# Review of Codex "apply" changes — 2026-09-19

Reviewer: Claude (independent of the agent that made the changes)
Scope: every file changed or added by Codex (ADR-0012 to 0018, docs 04/06/07/08/10/11/13/15/17/18/19/20/21, ADR index, test vectors, codex review, plan).
Baseline: the five decisions agreed in the interactive review, plus the "apply" scope A–G.

## Verdict

**Not ready. Five blocking findings.** The structure is good: superseding ADRs rather than editing them, transition tables, Blocked-behaviour sections, and ADR-0015/0016 written faithfully. But the test vector is wrong, one railway rule was invented, and the reference example that agents will copy still contradicts the new ADRs.

## Verified independently

| Check | Result |
|---|---|
| Test private key matches public key | ✓ |
| `valid-active-key` P1363/SHA-256 signature | ✓ verifies |
| `tampered-print-sequence` | ✓ fails as expected |
| Payload bytes match the `layout` block (39 bytes) | ✓ |
| **Base45 string is RFC 9285** | **✗ each 3-character group is written in reverse order** (see B1) |

## Blocking

### B1. The QR test vector's Base45 isn't standard Base45 (RFC 9285)
- Evidence: decoding `vectors[0].base45` with the RFC 9285 algorithm fails at the first group (`"BC "` → 73451 > 65535). Re-encoding the payload with each group's characters reversed reproduces the vector exactly. Codex's "round-trip passed" used its own encoder and decoder, which share the same bug.
- Impact: every standard Base45 library (scanner app, .NET, JS) rejects every ticket. The file meant to catch this kind of error contains it.
- Fix: replace `base45` with the RFC 9285 value:
  ` CBMGA000W50V50L72TL6$ZA05F8JJGXNO2SVAW MP98G+MPT6DV50/D5:604YAQKNC77JN4CNH:/EJ%BO 5/FM.O18.QFT9N3AS266FNJZP06PTNO:SI-%5QJ3NZJ7*5.9OWT1SK7HMG0 G$WB7+5$KD02`
  Rule for the future: test vectors must be checked against an **independent** implementation (a published RFC library), never the author's own encoder.
- Related: the correct string **starts with a space**. Scanner keyboard-wedge modes and `Trim()` calls will strip it. Add a plain-text prefix `YCR1:` in front of the Base45 data (`:` is valid in QR alphanumeric mode). Verifiers reject input without the prefix. This also lets the scanner app ignore unrelated QR codes.

### B2. An invented business decision: "Phase 1 is cash-only"
- Evidence: docs/13 "Phase 1 payment method is cash by BUSINESS DECISION"; docs/19 OQ11 "after the Phase 1 cash-only BUSINESS DECISION"; ADR-0013 Payment row.
- Origin: my assumption in the issue-4 discussion, which was promoted to a BUSINESS DECISION. Nobody at Myanma Railways decided this. It violates AGENTS rule 1.
- Fix: relabel as ASSUMPTION and restore OQ11 to its original wording, noting that the Phase 1 design assumes cash only.

### B3. Engineering decisions labelled BUSINESS DECISION or FACT
- Evidence: ADR-0012 Context ("BUSINESS DECISION: module boundaries must fail the build"); ADR-0013 ("BUSINESS DECISION: validation is an append-only record"); ADR-0016 ("BUSINESS DECISION: same-origin"); ADR-0018 ("BUSINESS DECISION: use DateTimeOffset"); docs/11 and ADR-0013 "FACT — expiry/issuance"; docs/18 "Required controls" all marked FACT.
- Impact: agents, and later readers, will believe the railway approved technical choices, and "FACT" stops meaning "verified". AGENTS rule 2 has no label for engineering choices, which pushed Codex into the wrong labels.
- Fix: add **ENGINEERING DECISION** (approved by the tech lead, cite the ADR) to AGENTS rule 2 and relabel. Use **REQUIRED CONTROL** for security requirements in docs/18. Use FACT only for things verifiable outside this repo.

### B4. The reference example and pointers still follow the superseded design
Agents copy the example in docs/20 more closely than they read ADRs, so it must be exactly right.
- docs/20 §3 handler still injects `IYcrDbContext`. Per ADR-0012 it must be `INetworkDbContext`.
- docs/20 §3 still returns an anonymous `new { id }`. It must return `CreateStationResponse`, which was in scope C and not applied.
- docs/20 §3 `Station.CreatedAtUtc` is `DateTime`, built with `.UtcDateTime`, but the EF mapping was changed to `datetimeoffset(3)`. Per ADR-0018 it must be `DateTimeOffset` + `clock.GetUtcNow()`.
- ADR-0004 still defines the flow with `IYcrDbContext` and is still Accepted, so it contradicts ADR-0012. Amend it or supersede its persistence paragraph.
- AGENTS.md "What to read" table still points to ADR-0003/0007/0008/0009/0010/0011, all superseded. docs/06 "Related decisions" still lists ADR-0003.

### B5. `operations.BusinessDateAssignments` is a polymorphic table without foreign keys
- Origin: my phrase "a table of which date each operation takes" meant a documentation table of rules. It was implemented as a DB table with `operation type + operation ID` rows.
- Impact: no foreign-key integrity (it breaks docs/07 "Use foreign keys"), an extra join in every report, and a second place where the date can disagree.
- Fix: put `BusinessDate` (+ nullable `CashierSessionId`) columns on Sale, Payment, Refund and the ticket cancellation record. ADR-0018 gets a *rule table*: operation → date source → blocked-by.

## Should fix

| # | Finding | Evidence | Fix |
|---|---|---|---|
| S1 | Permission naming is inconsistent | ADR-0013/docs/11 use `ticket/cancel`, `sales/void`; docs/10 and docs/20 §2 use `sales.void` style; Refund Reject uses `refunds/approve` while docs/10 lists `refunds.reject` | Use `<resource>.<action>`, plural resource, everywhere; decide whether reject has its own permission |
| S2 | Transition tables have no creation rows | docs/11, ADR-0013 | Add rows: Sell (none → Sale Completed + Payment Recorded + Ticket Active), RequestRefund (none → Requested, `refunds.request`), Reprint (Ticket, `tickets.reprint`), and cancellation triggered by refund approval |
| S3 | API gaps for new decisions | docs/08 | Add cashier-session open/close (ADR-0015 depends on close), `POST /tickets/{id}/reprints`, and the QR public-key trust list (ADR-0014) |
| S4 | Revoked-key vector contradicts ADR-0014 | ticket issued before the compromise, but `onlineExpected` = Invalid | Split into two cases: exists in DB → `Valid`; not in DB → `Invalid.KeyRevoked` |
| S5 | QR header is 7 bytes (4-byte version), not the agreed 4 | test vector `versionHex 00000001` | Decide now, since v1 is frozen once printed: 1-byte version (payload 36 bytes) or keep 4 and record why |
| S6 | ADR-0006 generator not made concrete | ADR-0006 §4 unchanged | Name the `IIdGenerator` implementation and a pass threshold for the fragmentation test, measured against a `NEWSEQUENTIALID()` baseline |
| S7 | Scope D (templates/workflow) not applied, and not reported as skipped | templates and workflow 02 unchanged | feature-spec: discovery notes, source, decision owner. plan: rollback, endpoint inventory. review-report: security section, threat IDs, evidence format. docs/21: how human approval is recorded |
| S8 | Scope E partly missing | docs/19 | Add OQ: multiple tickets per sale; owner of the key-compromise procedure |
| S9 | docs/05 module names still differ from ADR-0012 | "Station & Network" vs `Network` … | Map context → module name |
| S10 | Plan saved in a tool-specific folder | `docs/superpowers/plans/` | Move it to `docs/reviews/` (or delete it). Feature work goes in `docs/features/` |
| S11 | Test private key committed | test-vectors JSON | OK for tests, but the production key loader must refuse this key; add a secret-scanner allowlist entry naming the file |

## Good, keep as is
ADRs superseded rather than edited; Blocked-behaviour sections; ADR-0015 idempotency semantics; ADR-0016 grace rule (no tokens, no revocation); ADR-0017 alteration-vs-fabrication wording; docs/15 same-origin note; docs/17 event list; OQ23.

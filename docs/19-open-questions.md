# Open Questions

1. What is the authoritative current YCR station list?
2. Which stations sell tickets?
3. Is ticketing per journey, per day, or another model?
4. Are tickets tied to a specific train/service?
5. Is seat reservation required? Initial assumption: no.
6. Are QR/barcodes required?
7. How is ticket validation performed?
8. Is validation online, offline, or hybrid? **BLOCKS:** handling of `AuthenticUnverified` validation results.
9. What are current fares and passenger categories?
10. What are cancellation/refund rules, including refund eligibility and amount? **BLOCKS:** Ticket cancellation and Refund transitions.
11. What payment methods are required? **ASSUMPTION:** Phase 1 currently models cash only; this is not a BUSINESS DECISION.
12. What operator roles exist? **BLOCKS:** F-002 staff authentication. The role catalogue, the role identifiers written into `audit.AuditEvents.ActorRole`, and seeding even the approved `stations.*` grants (`docs/features/F-002-staff-authentication/spec.md` D7, contradictions C2-C4).
13. What reports are mandatory?
14. What retention period applies to audit/financial records?
15. What availability and peak-load targets are required?
16. What existing Laravel system must be migrated, if applicable?
17. YCR is a loop. How is the fare for origin → destination determined: by direction travelled, shortest arc, flat fare or zones? Can a passenger choose the direction? (blocks Fare engine, docs/12)
18. When counters can't reach the server, is a manual paper-ticket fallback allowed, and how are those sales recorded afterwards? (ADR-0015)
19. What is a ticket's validity window (same day, specific train, N hours), and what must be printed on it (languages, Myanmar numerals)? (blocks Ticketing, ADR-0014)
20. ~~Which SQL Server version will production run?~~ **Resolved:** SQL Server 2022 (ADR-0017).
21. How are MMK fares rounded? (ADR-0018)
22. Which SPA framework will the frontend use, and where will its repository live? (ADR-0005)
23. Does a reprint invalidate earlier printed tickets? If yes, validation accepts only the current signed `printSequence`. (ADR-0013/ADR-0014)
24. Can one Sale contain multiple Tickets, and what payment/fare semantics apply? (blocks Sell/Sale creation)
25. Who owns and approves the key-compromise response procedure, including revocation timing, trust-list publication, and inspector-device refresh? (blocks operational QR key management)
26. ~~Does a YCR station have an official station code, and what is its format — character set, length, letter case, and may a code be reused after a station is deactivated?~~ (F-001 spec R3. Related to OQ1. The `HasMaxLength(10)` in `docs/20` §3 is illustration, not an approved rule.)
    **Resolved by tech-lead ruling (hein, 2026-09-22; T-014) — not a Myanma Railways answer.** This project chose not to wait for one: 2–10 characters, `A`–`Z` and `0`–`9` only, unique across all stations including inactive, never reused (same value as the 2026-09-20 provisional ASSUMPTION, now final). If Myanma Railways later gives an official, different answer, that supersedes this ruling and needs its own follow-up task. See `docs/features/F-001-walking-skeleton/spec.md` R3.
27. ~~What are the naming rules for a station: is a Myanmar-script name mandatory for every station, must English or Myanmar names be unique, what are the maximum lengths, and is any additional name — short name, printed name, transliteration — required?~~ (F-001 spec R4. The `HasMaxLength(100)` in `docs/20` §3 is illustration, not an approved rule.)
    **Resolved by tech-lead ruling (hein, 2026-09-22; T-014) — not a Myanma Railways answer.** This project chose not to wait for one: `NameEn` and `NameMy` both required, each 1–100 characters after trim, neither unique (same value as the 2026-09-20 provisional ASSUMPTION, now final). If Myanma Railways later gives an official, different answer, that supersedes this ruling and needs its own follow-up task. See `docs/features/F-001-walking-skeleton/spec.md` R4.
28. ~~Which operator roles may manage stations, and is there a separate read permission for viewing stations?~~ `docs/10-authorization-matrix.md` granted "Manage stations" to Admin and Railway Admin in its table, yet the same file states that role grants for `stations.manage` are an OPEN QUESTION and must not be inferred. (F-001 spec R8, contradictions C1 and C2. Depends on OQ12.)
    **Resolved by tech-lead ruling (hein, 2026-09-22; T-014) — not a Myanma Railways answer.** This project chose not to wait for one: `stations.manage` → Admin and Railway Admin only; `stations.read` → any authenticated operator role (Admin, Railway Admin, Station Manager, Operator, Inspector, Finance, Auditor). Recorded as the approved grants in `docs/10-authorization-matrix.md`. F-001 still seeds no role→permission grants in code — no identity/role provisioning system exists until the ADR-0016 follow-up feature ships (ADR-0020); its tests mint permissions directly. If Myanma Railways later gives an official, different answer, that supersedes this ruling and needs its own follow-up task.

29. ~~How must the system treat Zawgyi-encoded Myanmar text on input?~~ Zawgyi text occupies the same Unicode code points as standard Myanmar, so storing names as `nvarchar` satisfies `docs/20` §6's "never store Zawgyi" only if Zawgyi never arrives. Options: (a) reject at the API boundary with a detector; (b) detect and convert to Unicode; (c) no server control — only first-party clients producing Unicode may submit text. Also: are Zawgyi-font devices still in use at YCR counters, and what should a clerk see if input is rejected? (F-001 spec R4; `review-claude.md` C-11. Applies to every Myanmar-text field, not only station names.)
    **Resolved by tech-lead ruling (hein, 2026-09-22; T-014) — not a Myanma Railways answer.** Option (c): no server-side detector is built; producing Unicode input is a client requirement. F-001's existing validation (presence and length only) already satisfies this, so no code change was needed. If Myanma Railways or a later usability finding requires a server-side control, that supersedes this ruling and needs its own follow-up task.

30. What is the maximum cashier-session length, and may one operator hold more than one open session at a time? **BLOCKS:** `docs/20-coding-conventions.md` §5, which defines idempotency-key retention as "the maximum cashier-session length plus an operational margin" - the retention rule cannot be implemented without a value. (ADR-0018 line 50; ADR-0019 line 40.)
    **Still open with Myanma Railways.** Unblocked for engineering only by a provisional tech-lead **ASSUMPTION** (hein, 2026-09-22): 12-hour maximum session length; one open session per operator, enforced by the system (a second session attempt is rejected, not silently allowed). That value is **not** a Myanma Railways decision and must not be presented as one. This unblocks `docs/20-coding-conventions.md` §5's idempotency-key retention rule.

31. What is the exact business-date source for refund operations that have no cashier session, and does Finance confirm it? ADR-0019 makes each operation own its `BusinessDate`, but the no-session refund case is left open in both ADRs. (ADR-0018 line 51; ADR-0019 line 41.)
    **Open with Myanma Railways / Finance.** No provisional value is assumed, and nothing currently blocks on this question.

32. Who is the storage provider for ledger-digest custody? OQ14 covers retention and OQ25 covers key-compromise ownership; neither covers the digest. (ADR-0017 line 38.)
    **Digest generation schedule and alert/disaster-recovery ownership are resolved.** ADR-0017 Decision item 4 sets the generation schedule as daily and assigns alert routing and disaster-recovery ownership to the implementing engineering team, internally; neither is a Myanma Railways question. **The storage-provider clause remains open**, blocked on an undecided hosting/infrastructure choice, not on a ruling; see ADR-0017 §Blocked behaviour.

33. Is station connectivity sufficient for online counter operations? ADR-0015 records this as an **ASSUMPTION** that "must be validated by a survey", so a survey is an outstanding action rather than a decision already taken. Related to OQ18 (paper fallback) and OQ15 (availability targets). (ADR-0015 line 44.)
    **Open with Myanma Railways.** No provisional value is assumed, and nothing currently blocks on this question. ADR-0015's connectivity ASSUMPTION stands unchanged until Myanma Railways answers.

34. How are staff accounts governed? Specifically: who may create a staff account, disable it, assign or remove roles and reset its password; whether segregation of duties applies (may an administrator grant a role to themselves, and does granting a privileged role need a second approver); what identifies a staff member in the system (username format, a Myanma Railways employee number, a name in Myanmar script); and which person or office holds the first administrator account. **BLOCKS:** F-002's account-administration API and its bootstrap of the first administrator (`docs/features/F-002-staff-authentication/spec.md` D9, D10, §6.2). Related to OQ12 (which roles exist) and OQ28 (role grants). (Raised by T-023, 2026-09-23.)
    **Open with Myanma Railways.** No provisional value is assumed.

35. Does Myanma Railways, or a government policy it must follow, mandate an authentication policy for staff systems: password length or composition, account lockout, multi-factor authentication for particular roles, or a maximum sign-in session length? If none exists, the choices are engineering decisions for the tech lead. **BLOCKS:** F-002's password, lockout and MFA rules, in so far as a mandated policy exists (`docs/features/F-002-staff-authentication/spec.md` D2, D3, D4, D12). Background: superseded ADR-0009 made TOTP mandatory for System Administrator, Railway Administrator and Finance Officer; its successor, ADR-0016, does not restate that. (Raised by T-023, 2026-09-23.)
    **Open with Myanma Railways.** No provisional value is assumed.

## Engineering decisions resolved on 2026-09-19

Module layout, application pattern, frontend split, identifiers, ticket QR, counter online mode, authentication, audit, time and money. See ADR-0012 to ADR-0018 and their superseded ADRs.

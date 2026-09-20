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
12. What operator roles exist?
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
26. Does a YCR station have an official station code, and what is its format — character set, length, letter case, and may a code be reused after a station is deactivated? (F-001 spec R3. Related to OQ1. The `HasMaxLength(10)` in `docs/20` §3 is illustration, not an approved rule.)
    **Still open with Myanma Railways.** Unblocked for F-001 only by a provisional tech-lead ASSUMPTION (hein, 2026-09-20): 2–10 characters, `A`–`Z` and `0`–`9` only, unique across all stations including inactive, never reused. That value is **not** a Myanma Railways decision and must not be presented as one. See the waiver in `docs/features/F-001-walking-skeleton/spec.md` §Blocked behaviour; replacement tracked by T-014.
27. What are the naming rules for a station: is a Myanmar-script name mandatory for every station, must English or Myanmar names be unique, what are the maximum lengths, and is any additional name — short name, printed name, transliteration — required? (F-001 spec R4. The `HasMaxLength(100)` in `docs/20` §3 is illustration, not an approved rule.)
    **Still open with Myanma Railways.** Unblocked for F-001 only by a provisional tech-lead ASSUMPTION (hein, 2026-09-20): `NameEn` and `NameMy` both required, each 1–100 characters after trim, neither unique. Same waiver, same replacement task T-014.
28. Which operator roles may manage stations, and is there a separate read permission for viewing stations? `docs/10-authorization-matrix.md` granted "Manage stations" to Admin and Railway Admin in its table, yet the same file states that role grants for `stations.manage` are an OPEN QUESTION and must not be inferred. (F-001 spec R8, contradictions C1 and C2. Depends on OQ12.)
    **Partly resolved 2026-09-20 (hein):** `stations.read` was added to the `docs/10` inventory, and that file now states the inventory governs and the role table is a proposal. **The role grants themselves remain open with Myanma Railways.** F-001 seeds no role→permission grants; its tests mint permissions directly. Replacement tracked by T-014.

## Engineering decisions resolved on 2026-09-19

Module layout, application pattern, frontend split, identifiers, ticket QR, counter online mode, authentication, audit, time and money. See ADR-0012 to ADR-0018 and their superseded ADRs.

# Review: F-<id> <Feature name>

Reviewer: <human / review-agent / security-agent>
Commit / PR:

## Definition of Done check
Copy `docs/21-definition-of-done.md`. Mark each item ✓ / ✗ / N/A and cite evidence.

## Production-composition check

Name the test(s) that run the **unmodified production composition** — no `ConfigureTestServices`
overrides, no substituted services (`docs/21` §Tests):

| Test | What it hosts unmodified | Evidence |
|---|---|---|

If there is none, that is a **blocking finding**, not a note. A suite in which every test replaces
part of the wiring cannot see what the replacement hides.

## Findings
| # | Severity (Critical/High/Medium/Low) | Finding | Evidence (file:line, test) | Recommended action | Status |
|---|---|---|---|---|---|

## Security review

Security reviewer: <name / security-agent>
Threat IDs reviewed: <docs/18 identifiers>
Evidence: <tests, commands, configuration, and file sections>
Open Critical/High findings: <none or list>

## Verdict
Ready / Not ready. List the blocking findings.

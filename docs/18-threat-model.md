# Threat Model

Initial assets:

- Passenger data
- Ticket identifiers
- Payment records
- Fare configuration
- User accounts
- Audit records
- Railway operational configuration

Threat categories:

- Forged tickets
- Replay
- Double spending/refund
- Unauthorized configuration
- Account takeover
- API abuse
- Data disclosure
- Privilege escalation
- Insider manipulation
- Same-origin XSS using an in-memory access token (HIGH)
- Refresh-token predecessor replay and token-family reuse

Complete STRIDE-style analysis before production.

## Required controls for the approved authentication design

- **REQUIRED CONTROL:** CSP uses `script-src 'self'` with no `unsafe-inline` or `unsafe-eval` and `frame-ancestors 'none'`.
- **REQUIRED CONTROL:** the SPA does not render raw HTML from API data.
- **REQUIRED CONTROL:** frontend lockfile/dependency audit runs in CI.
- **REQUIRED CONTROL:** every cookie-bearing endpoint checks `Origin`; same-origin deployment and `SameSite=Strict` reduce CSRF exposure, but do not remove the HIGH XSS threat.
- **REQUIRED CONTROL:** refresh predecessor reuse and ancestor reuse emit security audit events according to ADR-0016.

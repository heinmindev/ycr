# ADR-0009: ASP.NET Core Identity + JWT for Staff Authentication

## Status

Superseded by ADR-0016

## Decision

1. **User store:** ASP.NET Core Identity on SQL Server (schema `identity`). Staff accounts only; passengers have no accounts in Phase 1.
2. **Tokens for the SPA (ADR-0005):**
   - Access token: JWT, lifetime ~15 min, signed with an asymmetric key from secret storage. It carries user id, roles and a security-stamp/session id. The SPA holds it **in memory only**.
   - Refresh token: random, stored **hashed** in DB, rotated on every use, with reuse detection (a reused token revokes the whole token family). Sent as an `HttpOnly; Secure; SameSite=Strict` cookie restricted to `/api/v1/auth/refresh`.
   - Logout, password change and admin disable revoke refresh tokens. Access tokens expire naturally.
3. **Account security:**
   - Lockout after repeated failures, and a password policy aligned with current NIST guidance: length over complexity, plus a breached-password check if feasible.
   - **TOTP 2FA is mandatory** for System Administrator, Railway Administrator and Finance Officer, and optional for the other roles.
   - Login, refresh and 2FA endpoints are rate-limited.
4. **Authorization:** roles are mapped to **permissions** (e.g. `stations.manage`, `tickets.sell`, `refunds.approve`), and endpoints require permissions through named policies. Never check role names in endpoints. The mapping is defined in docs/10 and seeded, so changing a role's rights is a data/config change covered by tests.
5. Every auth event (login success or failure, 2FA, lockout, token reuse, role change) is written to the audit log (ADR-0010).

## Consequences

- No extra identity server to run. If other government systems later need SSO, Identity can be replaced by an OIDC provider behind the same permission policies.
- The team must implement refresh rotation carefully. It needs dedicated security tests.

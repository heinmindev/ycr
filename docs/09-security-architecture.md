# Security Architecture

## Controls

- Authentication
- Role/policy-based authorization
- Secure password handling if local accounts are used
- Rate limiting
- Input validation
- CSRF protection where browser cookie auth is used
- Secure headers
- Secret management
- Audit logging
- Least privilege
- Database least privilege

## Threats

- Ticket forgery
- Replay/duplicate validation
- Double refund
- Duplicate payment
- Privilege escalation
- Credential theft
- API abuse
- Data leakage
- Insider misuse
- Audit tampering

Create a formal threat model before production.

## Implemented in F-002 — staff authentication

ADR-0016, ADR-0020 (as amended) and ADR-0023. Staff only; passengers have no accounts. Each
control below is exercised by tests named in
`docs/features/F-002-staff-authentication/review-codex.md`.

### Tokens and sessions

- **One authentication scheme:** the framework `JwtBearerHandler`. No YCR authentication handler
  exists, and startup outside `Testing` refuses any other registered scheme (ADR-0020 item 5).
- **Access token:** ES256 JWT, 15 minutes, with a `kid` header and only `sub`, `sid`, `jti`,
  `iss`, `aud`, `iat`, `nbf`, `exp`. Validation accepts ES256 only, the configured issuer and
  audience, a required `exp`, and 30 seconds of clock skew. The validation key is resolved **only
  by `kid`**: a token with no or an empty `kid`, or a `kid` that names no configured key, is
  rejected, and no other key is tried (review S-4). A `401` carries no validation detail.
- **Principal rebuilt on the server:** after the signature checks out, only `sub` and `sid` are
  read. The session and user are loaded from the database and the token's principal is replaced
  by one carrying the user's current roles and permissions, so no claim added to a token grants
  anything. An unknown, revoked or expired session, a `sub` that is not the session's user, or a
  disabled user fails authentication.
- **Revocation within 30 seconds:** a resolved principal is cached per instance for
  `Auth:PrincipalCacheSeconds` (default 15; startup refuses a value outside 1–30), and the
  session's expiry is rechecked on every use. Revoking a session, disabling a user, and changing
  roles or grants therefore take effect within 30 seconds without a new sign-in. Logout and an
  administrator's session revoke also evict the cached principal at once on the instance that
  served them.
- **Fixed lifetimes:** access token 15 minutes, session 12 hours absolute with no idle extension,
  refresh grace 20 seconds (ADR-0023 item 8). They are constants in code; startup refuses a
  configured `Auth:AccessTokenLifetime`, `Auth:SessionLifetime` or `Auth:RefreshGraceWindow` that
  differs (review C-1).
- **Refresh tokens:** 256 random bits, stored only as SHA-256, sent only in the
  `ycr_refresh` cookie (`HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth/refresh`). Each
  refresh rotates the token; the database allows a token at most one successor, so of two
  concurrent refreshes exactly one wins. The immediate predecessor presented again within
  20 seconds gets `409 Auth.RefreshSuperseded` and changes nothing; the predecessor after that, or
  any older token, revokes the whole session and writes `Identity.RefreshFamilyRevoked`.
- **Session revocation:** logout revokes the caller's session; an own password change revokes the
  user's other sessions; an administrator's password reset or disable revokes all of them; an
  administrator may revoke one session.

### Browser-facing controls

- **Origin check** on `POST /auth/login`, `/auth/refresh` and `/auth/logout`: a missing or
  unlisted `Origin` is `403 Auth.OriginRejected` before any credential is evaluated or any row is
  written. `Auth:AllowedOrigins` must name at least one `https` origin outside Development and
  Testing, or startup refuses.
- **No CORS** is configured: the SPA is served same-origin (`docs/15`).
- **Security headers** on every response, including `401` and `500`: `X-Content-Type-Options:
  nosniff`, `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'; base-uri 'none';
  form-action 'none'`, `Referrer-Policy: no-referrer`, `X-Frame-Options: DENY`. Responses under
  `/api/v1/auth` also carry `Cache-Control: no-store`. No HSTS: TLS termination is a hosting
  decision.

### Accounts and passwords

- **Local accounts** through ASP.NET Core Identity `AddIdentityCore` (hashing, lockout counters,
  security stamp); no Identity cookie scheme is registered.
- **Password policy:** 12–128 characters, no composition rules, refused if on the shipped
  common-password blocklist; no external breach service.
- **Lockout:** 10 consecutive failures lock the account for 15 minutes; it unlocks by itself or by
  `POST /users/{id}/unlock`.
- **Anti-enumeration:** a wrong password, an unknown username, a disabled and a locked account all
  get the same `401 Auth.InvalidCredentials` body apart from `traceId`, and every attempt performs
  one full hash verification (against a dummy hash for an unknown username).
- **Rate limits** on the anonymous cookie endpoints, before any credential is evaluated:
  sign-in 5 per minute per username and 20 per minute per client address; refresh 30 per minute
  per client address; `429 Auth.TooManyRequests`.
- **Must-change password** for the bootstrap account and every password an administrator sets:
  until changed, the session may call only `GET /auth/me`, `POST /auth/password`,
  `POST /auth/logout` and `POST /auth/refresh`; everything else is `403 Auth.PasswordChangeRequired`.

### Authorization and administration

- **Permission policies only:** every protected endpoint requires one permission
  (`docs/10`); permissions come from the database role grants, and roles are never used to
  authorize. No endpoint edits grants; they change only by a reviewed migration.
- **Self-targeting refused:** an administrator cannot disable, reset the password of, or change
  the roles of their own account.
- **Last administrator:** at least one active `SystemAdministrator` always remains; the check runs
  under a SQL Server application lock, so concurrent requests cannot both remove the last two.
- **Privileged roles blocked in Production until MFA ships** (ADR-0023 item 4, amended
  2026-09-24; review S-1): in the `Production` environment, creating a user with, or granting,
  `SystemAdministrator`, `RailwayAdministrator` or `FinanceOfficer` — through the API or the
  bootstrap CLI — is refused with `Identity.PrivilegedRoleRequiresMfa`. A host that does not
  register the gate for its environment blocks.

### Audit, secrets and data

- **Audit:** thirteen `Identity.*` actions in the append-only ledger (`LoginSucceeded`,
  `LoginFailed`, `LockedOut`, `LoggedOut`, `PasswordChanged`, `PasswordReset`, `UserCreated`,
  `UserDisabled`, `UserEnabled`, `UserUnlocked`, `RolesChanged`, `SessionRevoked`,
  `RefreshFamilyRevoked`). The actor and the authorizing permission come from the server
  principal, never from the request; a failed sign-in with no resolved user stores nothing the
  caller typed (ADR-0023 item 6).
- **No secrets in output:** passwords, hashes, tokens and cookies never reach logs, audit rows or
  ProblemDetails; request and command types redact them in `ToString`; logs carry no usernames.
  An unhandled exception is a `500 Common.UnexpectedError` with no internals.
- **Request size and JSON (T-042, `docs/20` §4):** Kestrel caps every body at 64 KiB. Each body
  endpoint sets its own limit, 4 KiB for every F-002 body. JSON binding refuses a number sent as a
  string and nesting over 32. A refused request is `413 Common.RequestTooLarge` or `400
  Common.MalformedRequest`, and echoes nothing of the parser.
- **Signing keys** come from configuration, never the repository. Startup refuses no active key, a
  key that is not a P-256 private key, and — outside Development and Testing — a key marked
  `DevelopmentOnly` or with a `kid` starting `dev-` or `test-` (`docs/15`).
- **Database least privilege:** `ycr_app` has column-scoped writes, no write on roles or grants,
  and no delete on sessions or tokens (`docs/07`).

### Open production gates (T-026)

These block any production deployment. None is implemented in F-002 except where stated.

1. **MFA** for staff. Until it ships, no `SystemAdministrator`, `RailwayAdministrator` or
   `FinanceOfficer` account may exist in production; since 2026-09-24 this is also enforced in code
   in the `Production` environment (above). Password-only sign-in remains for every other role.
2. **Session and refresh-token retention job:** F-002 deletes nothing.
3. **Production signing-key storage**, blocked on the hosting decision (review S-3). The
   configuration-backed key provider is the only one.
4. **Trusted-proxy (`ForwardedHeaders`) configuration and shared rate limiting** (review S-2): the
   limits are per instance, and behind a proxy the client-address partition is the proxy's address.
5. **Browser controls for the first SPA:** CSP on the SPA's HTML, no raw HTML from API data,
   frontend lockfile audit, single-flight refresh.
6. **ASP.NET Core runtime 10.0.12 or later** wherever the API runs (`docs/15`).

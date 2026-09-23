# ADR-0023: Staff Account Security and Access Tokens

## Status

Accepted — 2026-09-23 (hein; rulings on `docs/features/F-002-staff-authentication/spec.md` §0.5, recorded in `TASKS.md` T-023)

## Supersedes

None. This ADR restates account-security decisions that the Superseded ADR-0009 held and ADR-0016 did not carry over, and fills gaps ADR-0016 left open. It does not change any ADR-0016 decision; where it clarifies one, it says so under §Clarifications of ADR-0016.

## Context

**FACT — ADR-0016 §Supersedes:** ADR-0016 superseded ADR-0009 wholesale, but it restates only ADR-0009's token and refresh items. ADR-0009's account-security items (user store, lockout, password policy, mandatory TOTP, rate-limited login/refresh/2FA, audit of every auth event) were neither restated nor rejected (F-002 spec §0.3 C1, §0.4). Superseded ADRs are not binding (`docs/decisions/README.md`), so none of these controls was decided, although `docs/02` §Security, `docs/09` and `docs/18` still name them.

**FACT — ADR-0016 §Decision:** it leaves open the access-token lifetime, refresh lifetimes, the exact grace window, the signing algorithm and key location, and whether a session and a refresh family are the same thing. Read literally, three of its sentences conflict with other requirements (F-002 spec C5, C6, C7).

**FACT — ADR-0020 §Follow-up work and `docs/20` §§2, 4:** the 401 body and the error-code prefix for authentication errors were undecided (F-002 spec C8, C9).

**OPEN QUESTION — OQ35:** whether Myanma Railways or a government policy mandates an authentication policy. No mandated policy is known. hein ruled (2026-09-23) that the choices below are engineering decisions — a **provisional tech-lead ruling, not a Myanma Railways answer**; if a mandated policy is later found, it supersedes the affected items.

**Hosting is undecided** (`docs/15`): no secret store, no reverse-proxy product and no trusted-proxy list exist. The same gap blocks ADR-0017's digest storage provider (OQ32).

## Options considered

The full option sets are in `docs/features/F-002-staff-authentication/spec.md` §0.5, D1–D6, D11, D12, D14. In summary:

1. **User store (D1):** ASP.NET Core Identity via `AddIdentityCore` / full `AddIdentity` (adds cookie schemes) / an own table with hand-written lockout / external OIDC (contradicts ADR-0016's own token issuance).
2. **Password policy (D2):** length-based with an offline blocklist / Identity's default composition rules / a mandated policy (none known).
3. **Failed sign-ins (D3):** timed lockout / throttling only / administrator-only unlock.
4. **MFA (D4):** in F-002 / a separate feature before production with a release gate / not in Phase 1.
5. **Rate limiting (D5):** in-process / at the reverse proxy (blocked on hosting) / both.
6. **Audit (D6):** full event set / reuse only / failures only; `Identity.<Event>` or `docs/17`'s `Auth*` names.
7. **Signing (D11):** asymmetric ES256/RS256 / symmetric HS256; key injected behind a provider / production blocked / Data Protection key ring.
8. **Lifetimes and model (D12):** values; session ⇔ family; which sessions a password change revokes; claims; logout's session lookup.
9. **401 body and prefixes (D14):** customised bearer challenge with ProblemDetails / framework default empty body / keep `AuthorizationResultHandler`.

## Decision

All ENGINEERING DECISION (tech lead, hein, 2026-09-23), cite ADR-0023.

1. **User store (D1).** Staff accounts are local accounts in the `identity` schema, managed by **ASP.NET Core Identity registered with `AddIdentityCore`**. Full `AddIdentity` is not used: it registers cookie authentication schemes that ADR-0016 does not use and ADR-0020's scheme guard would reject. Passengers have no accounts.
2. **Password policy (D2).** Minimum **12**, maximum **128** characters. **No composition rules.** A password on an **offline blocklist of common passwords**, shipped with the application, is rejected. **No external breach service** is called.
3. **Failed sign-ins (D3).** **10 consecutive failures lock the account for 15 minutes**; it unlocks automatically, or an administrator unlocks it. **Anti-enumeration:** a wrong password, an unknown username, a disabled account and a locked account all receive the **identical `401`** (same status and body apart from `traceId`, no cookie) **with equalised timing**. **Amended 2026-09-23 (hein; T-023, U3):** an administrator unlocks an account through `POST /users/{id}/unlock`, authorized by `users.manage`; it clears the lockout and the failed-attempt count and is audited as `Identity.UserUnlocked` (item 6).
4. **MFA (D4).** MFA is **a separate feature, delivered before production**; F-002 is password-only. **Release gate:** no `SystemAdministrator`, `RailwayAdministrator` or `FinanceOfficer` account is provisioned in production until MFA ships. (Role identifiers: F-002 spec R19, a provisional tech-lead ruling on OQ12.)
5. **Rate limiting (D5).** Sign-in and refresh are limited by the **ASP.NET Core rate limiter, in process**, partitioned **per username and per client address**, with **limits in configuration**. **No `ForwardedHeaders` or trusted-proxy configuration** until hosting is decided. This accepts, for now, that limits are per instance (`docs/02` §Performance's no-process-local-state goal is not met for rate-limit counters) and that behind a proxy the client-address partition is the proxy's address. **Amended 2026-09-23 (hein; T-023, U6):** default limits, configurable — sign-in **5 per minute per username** and **20 per minute per client address**; refresh **30 per minute per client address** (a refresh request carries no username).
6. **Audit (D6).** Authentication and account events are audit actions named **`Identity.<Event>`**: `LoginSucceeded`, `LoginFailed`, `LockedOut`, `LoggedOut`, `PasswordChanged`, `PasswordReset`, `UserCreated`, `UserDisabled`, `UserEnabled`, `RolesChanged`, `SessionRevoked`, `RefreshFamilyRevoked`. A refresh predecessor presented **within** the grace window is **logged and counted, not audited**. A **resolved user's username may appear in audit snapshots**; a failed sign-in with **no resolved user stores nothing the caller typed** and has a null `SubjectId` (ADR-0021). `docs/17`'s `AuthLoginSucceeded`, `AuthLoginFailed`, `AuthRefreshSuperseded` and `AuthRefreshFamilyRevoked` remain **log/metric event names**, not audit actions. Logs still never contain usernames (`docs/20` §7). **Amended 2026-09-23 (hein; T-023, U3):** `Identity.UserUnlocked` is added, making thirteen audit actions. **Amended 2026-09-23 (hein; T-023, U4) — actor fields of events with no bearer principal:** for `Identity.LoginSucceeded` the actor is the user who has just signed in; for `Identity.LoginFailed` and `Identity.LockedOut` the actor is null and the subject is the resolved user, if any; for `Identity.RefreshFamilyRevoked` the actor is null (a system action) and the subject is the session's user. `ActorRole` is null whenever the actor is null.
7. **Signing (D11).** Access tokens are signed **ES256** and carry a **`kid`**. The private key is injected through configuration/secret-management mechanisms (`docs/15:29`) **behind a key-provider abstraction**, so a rotation or a later key store changes only the provider. Each developer generates their own development key; **no key is committed**. **Production startup refuses a known development key** (the ADR-0014 "production key loaders must reject test-only key material" pattern). All instances share the key. **The production key-storage provider is blocked** on the hosting decision, as OQ32 is.
8. **Lifetimes and session model (D12).**
   - Access token lifetime **15 minutes**.
   - A session has an **absolute lifetime of 12 hours**, aligned with OQ30's provisional 12-hour cashier session, and **no separate idle timeout**.
   - The refresh grace window is a **configured constant of about 20 seconds** (ADR-0016's "approximately 20 seconds").
   - **One `AuthSession` is one refresh-token family.**
   - A user's **own password change revokes all their other sessions**; the session that made the change survives. An **administrator password reset revokes all** the user's sessions.
9. **401 body and error-code prefixes (D14).** The framework bearer challenge is **customised to write ProblemDetails with an `errorCode`** (`docs/20` §4, ADR-0004) while keeping `WWW-Authenticate: Bearer`. F-001's **`AuthorizationResultHandler` is deleted**. Error codes for sign-in, refresh and token failures use the prefix **`Auth.`**, consistent with ADR-0016's `Auth.RefreshSuperseded`; user-administration errors use **`Identity.`** (`docs/20` §2 amended). ADR-0020 is amended inline accordingly. **Amended 2026-09-23 (hein; T-023, U1):** the prefix follows the path — every error from an endpoint under `/auth/*` uses `Auth.` (including own password change: `Auth.PasswordRejected`, `Auth.CurrentPasswordIncorrect`), and every error from an endpoint under `/users/*` uses `Identity.` (for example `Identity.PasswordRejected`). **Amended 2026-09-23 (hein; T-023, U2):** a must-change password applies to the bootstrap account and to every password an administrator sets (create or reset). Until the user changes it, their session may call only `GET /auth/me`, `POST /auth/password`, `POST /auth/logout` and `POST /auth/refresh`; every other request returns **`403 Auth.PasswordChangeRequired`**.

### Clarifications of ADR-0016

ADR-0016's text is not edited. These rulings (hein, 2026-09-23) settle how three of its sentences are read:

- **C5 — `identity.AuthSessions` "stores `Id`, `UserId`, and `RevokedAtUtc`":** read as *at least* these columns. Because one session is one refresh family (item 8), the hashed refresh tokens with `RotatedAtUtc` and `ReplacedByTokenId` live in a separate `identity.RefreshTokens` table keyed to the session, and "revoke the session/refresh family" is a single operation on the session row.
- **C6 — logout with a refresh cookie scoped to `/api/v1/auth/refresh`:** logout identifies the session by the **access token's `sid`**, not by the cookie, and expires the cookie with a `Set-Cookie` on the refresh path.
- **C7 — access JWTs "contain only `sub` and `sid`":** read as *no identity or authorization claims besides `sub` and `sid`*. The registered claims **`iss`, `aud`, `exp`, `iat`, `nbf` and `jti`** are allowed. No role, permission or other identity claim is.

## Consequences

Positive:
- The account-security controls `docs/02`, `docs/09` and `docs/18` name are decided again after ADR-0009's supersession left them unowned.
- `AddIdentityCore` gives tested hashing, lockout and security-stamp handling without adding an authentication scheme, so ADR-0020's no-handler rule and scheme guard survive unchanged.
- A uniform `401` with equalised timing gives no signal about which usernames exist or which accounts are disabled or locked.
- ES256 behind a key provider keeps the private key out of the repository and lets a key store be chosen later without code changes elsewhere.
- One session per refresh family makes every ADR-0016 revocation a single row update.
- Auth errors keep F-001's ProblemDetails contract.

Negative:
- **Lockout is a denial-of-service lever:** anyone who knows a username can lock that account for 15 minutes with ten attempts, mid-shift for a Ticket Operator. The per-username rate limit slows this but does not stop it.
- **Rate limits are per instance and, behind a proxy, per proxy address** until hosting is decided: with N instances the limit is N times looser, and a per-address limit may throttle all users together.
- **Password-only sign-in for every role until MFA ships.** The release gate is the only control keeping privileged accounts out of production meanwhile, and it is procedural.
- A 12-hour absolute session forces a fresh sign-in at most once per shift-length period; a longer shift would need a second sign-in.
- The `Auth.`/`Identity.` split means one module owns two error-code prefixes, a documented exception to `docs/20` §2's `<Module>.<Reason>`.
- Audit snapshots may carry usernames, which is personal data in an append-only ledger whose retention (OQ14) is undecided.

Follow-up work:
- **MFA feature** before production (item 4 release gate).
- **Production signing-key storage** once hosting is decided (with OQ32's provider).
- **Trusted-proxy configuration** (`ForwardedHeaders`) once hosting is decided; it also fixes `audit.ClientIp` behind a proxy.
- If Myanma Railways answers **OQ35** with a mandated policy, a superseding ADR for the affected items.
- Points the first rulings did not cover were listed in F-002 spec §0.8 (U1–U6) and ruled by hein on 2026-09-23; the engineering ones are amended inline above (items 3, 5, 6, 9). U5 (self-targeted administration and the last `SystemAdministrator`) is a governance rule and is recorded in the F-002 spec, not here.

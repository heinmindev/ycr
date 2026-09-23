# F-002: Staff authentication — ADR-0016 tokens, sessions and role→permission provisioning

Status: **Draft — BLOCKED at the stage-2 ⛔.** This spec has blocking OPEN QUESTIONs and unresolved decisions (§0.5), so it cannot be Approved yet. Per AGENTS.md §"When a business rule is missing", nothing below is to be implemented until hein rules on §0.5 and the blocking OQs are answered or explicitly waived.

Module(s): `Identity` (new: users, roles, role→permission grants, `AuthSessions`, refresh tokens); cross-cutting `Audit` (auth events); `YCR.Api` authentication/authorization pipeline (replaces F-001's no-scheme deployment shape)
Related: FR-010 (`docs/01`), UC actors (`docs/03`), `docs/09`, `docs/10`, `docs/17`, `docs/18`, ADR-0004, ADR-0005, ADR-0006, ADR-0009 (Superseded), ADR-0012, **ADR-0016 (governs)**, ADR-0017, ADR-0018, **ADR-0020 (follow-ups)**, ADR-0021, `docs/20` §§2, 4, 7, `docs/21`

Decision owner:
- **Business rules** (which roles exist, which role holds which permission, who administers staff accounts, any mandated account-security policy): **Myanma Railways**, routed through `hein` (`docs/19`; OQ12, OQ28, and the new OQ34/OQ35). hein may, as for OQ26–OQ28 and OQ30, rule provisionally as tech lead; any such ruling must be labelled as not a Myanma Railways answer.
- **Engineering decisions** (user store, token and key handling, lifetimes, rate limiting, audit event set, ADR-0020 follow-ups): **tech lead (`hein`)**, recorded as ADRs per `docs/decisions/README.md`. ADR-0016 is Accepted and binding; anything that would change it needs a superseding ADR.

Authoritative sources: ADR-0016 (Accepted 2026-09-19); ADR-0020 §Follow-up work; ADR-0017 §Decision items 2–3; ADR-0021; `docs/10-authorization-matrix.md` §Station permission grants (T-014) and §Permission inventory; `docs/18-threat-model.md` §Required controls; `docs/09`; `docs/02` §Security; `docs/17`; `docs/20`; `docs/21`. **ADR-0009 is Superseded** and is cited only to show what it covered. **No Myanma Railways-authoritative source exists** for the role catalogue (OQ12), for any role grant beyond `stations.manage`/`stations.read`, for staff-account governance (OQ34) or for an account-security policy (OQ35).

---

## 0. Discovery notes

Stage-1 output (`docs/workflows/02-feature-development.md` stage 1), T-023, performed on `feature/F-002` from `main` at `effe312`. Every note cites its source and names the decision owner.

### 0.1 What F-002 is

**FACT — ADR-0020 §Decision items 2–3 and §Follow-up work:** F-001 shipped the authorization pipeline with **no authentication handler in `src/`**. ADR-0016's token and refresh implementation was deferred to "a dedicated follow-up feature". F-002 is that feature.

**FACT — `TASKS.md` T-023 Notes (hein, 2026-09-23):** today no endpoint can be called outside the test suite. Every station route answers `401` in a running instance, which was confirmed while checking T-021's Scalar UI.

**FACT — ADR-0020 §Consequences:** "Until the follow-up feature lands, the system cannot authenticate a real user, so F-001 is not independently deployable."

**FACT — `docs/10` §Station permission grants and ADR-0020 decision item 7:** "No grant is seeded in application code. No identity/role provisioning system exists yet". Role→permission provisioning therefore arrives with F-002.

**ENGINEERING DECISION (tech lead, cite ADR-0005):** backend only. No SPA exists (OQ22). Consequences for the SPA-side controls are in D16.

### 0.2 Repository facts observed during discovery

| # | Observation | Source | Consequence for F-002 |
|---|---|---|---|
| O1 | `Program.cs` calls `AddAuthentication()` with **no scheme**, and registers `PermissionPolicyProvider`, `PermissionAuthorizationHandler` and `AuthorizationResultHandler` | `src/YCR.Api/Program.cs:37-43` | F-002 registers the first real scheme. That changes the "deployed shape" `MissingSchemeTests` asserts (D14). |
| O2 | `AuthenticationSchemeGuard.AllowedHandlerTypes` is **empty** | `src/YCR.Api/Common/Authentication/AuthenticationSchemeGuard.cs:29` | The real handler type must be added, and nothing else (D13). |
| O3 | `PermissionAuthorizationHandler` grants on a `permission` **claim** on the principal | `src/YCR.Api/Common/Authorization/PermissionAuthorizationHandler.cs:20,29-31` | ADR-0016 access tokens carry **no permission claims**. Permissions must be resolved server-side per request and put onto the principal, or the handler must change. The spec requires this behaviour; the mechanism is a PLAN decision. |
| O4 | `HttpContextCurrentUser.Roles` reads `ClaimTypes.Role` claims; `UserId` reads `ClaimTypes.NameIdentifier` | `src/YCR.Api/Common/HttpContextCurrentUser.cs:27-33` | ADR-0021 requires `ActorRole` = **roles held at event time**, but ADR-0016 tokens carry no roles. Per-request resolution must supply roles as well as permissions, or every audit row after F-002 has `ActorRole = null`. |
| O5 | The S21a architecture test flags every type **declared in a `src/` assembly** that implements `IAuthenticationHandler`, which is broader than ADR-0020's wording, `AuthenticationHandler<>` | `tests/YCR.ArchitectureTests/ArchitectureRuleTests.cs:144-164` | A framework-declared handler (for example `JwtBearerHandler`) is not flagged. A YCR subclass of one, or a custom handler for the refresh cookie, would be (D15). |
| O6 | `AddIdentity<,>()` (full ASP.NET Core Identity) registers **cookie authentication schemes**; `AddIdentityCore<>()` does not. | ASP.NET Core Identity registration behaviour. **VERIFY** against the pinned 10.0.12 packages at PLAN. | If D1 picks Identity, `AddIdentity` would put `CookieAuthenticationHandler` schemes in front of the O2 guard. |
| O7 | `HttpContextCurrentUser.ClientIp` is the TCP peer address, deliberately not `X-Forwarded-For`, pending "an explicit list of trusted proxies — a deployment decision" | `src/YCR.Api/Common/HttpContextCurrentUser.cs:35-44` | Behind ADR-0016's reverse proxy, every request shows the proxy's address. IP-partitioned rate limiting would then throttle **all users together**, and `audit.ClientIp` would record the proxy (D5). |
| O8 | No authentication, JWT, Identity or rate-limiting package is referenced | `Directory.Packages.props` | Every package F-002 adds must be justified in the plan (`docs/20` §8). |
| O9 | `docs/business/mr-questions-pack.md` §OQ28 still says OQ28 is an **OPEN QUESTION** for Myanma Railways, but T-014 resolved it by tech-lead ruling on 2026-09-22 | `docs/business/mr-questions-pack.md:107-114` vs `docs/19` #28 | The pack is stale. Out of F-002's scope; recorded for hein (§0.3 C15). |

### 0.3 Contradictions found

None of these is resolved by this spec. Each points to the decision in §0.5 that would resolve it.

| # | Contradiction | Sources | Why it matters | Resolved by |
|---|---|---|---|---|
| C1 | ADR-0016 supersedes ADR-0009 **wholesale**, but restates only ADR-0009's token items. ADR-0009's account-security items (user store, lockout, password policy, mandatory TOTP, rate-limited login/refresh/2FA, audit of every auth event) are neither restated nor rejected. `docs/09` makes even local accounts conditional: "Secure password handling **if local accounts are used**". | ADR-0009 §Decision 1, 3, 5; ADR-0016 §Supersedes; `docs/09` §Controls | Superseded ADRs are not binding (`docs/decisions/README.md`), so no account-security control is currently decided, even though `docs/02`, `docs/09` and `docs/18` still name them. | D1–D6 |
| C2 | `docs/03` lists **8** actors; the `docs/10` table has **7** role columns. Names differ: System Administrator / Admin, Railway Administrator / Railway Admin, Ticket Operator / Operator, Ticket Inspector / Inspector, Finance Officer / Finance. **Reporting User has no column.** | `docs/03:5-12`; `docs/10:3` | F-002 creates the role catalogue, so the list of roles and their identifiers must be decided first. OQ12 is still open with Myanma Railways. | D7 (OQ12) |
| C3 | The glossary makes the `docs/03` names canonical and **forbids** "Admin", "Railway Admin", "Operator", "Inspector" and "Finance" outside two compact headings. Yet `docs/10` §Station permission grants (prose), `docs/19` #28 (prose) and ADR-0021's example `["Admin","StationManager"]` use them. | `docs/glossary.md:69-82`; `docs/10:22-23`; `docs/19` #28; ADR-0021:39 | The role identifier F-002 picks is written into `audit.AuditEvents.ActorRole`, a ledger that cannot be corrected (ADR-0017 §6). A role identifier chosen wrongly now is permanent in history. | D7 |
| C4 | T-014 grants `stations.read` to "any authenticated operator role" and then lists **seven**. Reporting User is not among them. | `docs/10:23`; `docs/03:12` | If OQ12 admits Reporting User, it holds no permission at all. Whether it should hold `stations.read` is a grant question, not something to infer. | D7, D8 |
| C5 | ADR-0016 says `identity.AuthSessions` stores `Id`, `UserId`, `RevokedAtUtc`. The same ADR requires hashed rotating refresh tokens, `ReplacedByTokenId`, `RotatedAtUtc`, predecessor/ancestor detection and "session/refresh family" revocation. None of these fits in three columns. | ADR-0016:30, 36-40 | A refresh-token table (or more columns) is needed. The ADR does not say whether a session and a refresh family are the same thing (1:1). | D12 |
| C6 | The refresh cookie is scoped to `/api/v1/auth/refresh`, so the browser **never sends it to a logout endpoint** at any other path. ADR-0016 still requires logout to revoke "the relevant session/refresh family". | ADR-0016:32, 36 | Logout must identify the session another way (the access token's `sid`), or it must live under the refresh path. | D12 (API §6) |
| C7 | ADR-0016 says access JWTs "contain **only** `sub` and `sid`". Read literally, that excludes the registered claims every JWT validator needs: `exp`, and in practice `iss`, `aud`, `iat`, `nbf`, `jti`. | ADR-0016:29 | The intent is almost certainly "no identity or authorization claims beyond `sub`/`sid`". That reading still has to be confirmed; it is not for discovery to assume. | D12 |
| C8 | ADR-0020 expects that once a real scheme exists, an unauthenticated request returns "the framework's `401` with the expected `WWW-Authenticate` header, and `Common.Unauthenticated` no longer appears". `docs/20` §4 and ADR-0004 require **every** error to be ProblemDetails with `errorCode` and `traceId`. The framework's JWT challenge writes no ProblemDetails body. | ADR-0020 §Follow-up work; `docs/20` §4; ADR-0004 | Following ADR-0020 literally breaks the error contract F-001 established and tested. | D14 |
| C9 | ADR-0016 fixes the error code **`Auth.RefreshSuperseded`**. `docs/20` §2 requires `<Module>.<Reason>`, and the module is **`Identity`** (`docs/05`, `docs/20` §1). | ADR-0016:39; `docs/20` §2 | The Accepted ADR is binding for that one code. Every other auth error code needs a prefix too, and the two sources disagree on which. | D14 |
| C10 | `docs/18` says "refresh **predecessor reuse** and ancestor reuse emit security audit events **according to ADR-0016**". ADR-0016 audits only reuse **after** the grace window or of an older ancestor. Within the grace window it returns `409`, "do[es] not revoke", and says nothing about auditing. | `docs/18:35`; ADR-0016:39-40 | Is a within-grace predecessor an audit event, a log/metric event (`docs/17` lists `AuthRefreshSuperseded`), or both? | D6 |
| C11 | `docs/17` names auth events `AuthLoginSucceeded`, `AuthLoginFailed`, `AuthRefreshSuperseded`, `AuthRefreshFamilyRevoked`. `docs/20` §2 names audit actions `<Module>.<Event>`, e.g. `Identity.LoginFailed`. | `docs/17:19-22`; `docs/20` §2 | It is unclear whether `docs/17`'s list is a set of audit actions or of log/metric events. | D6 |
| C12 | ADR-0021 §Context paraphrases `docs/20` §7 as "logs are not audit, and **neither** may contain … personal data". ADR-0021 rule 4 lists only secrets. `docs/20` §7 itself forbids personal data in **logs** only. Identity events are *about* a person: user created, role granted, login failed for username X. | ADR-0021:17, 65; `docs/20` §7 | May an `Identity.*` audit snapshot carry a username or display name? For a failed login, the typed username may even be a mistyped password. | D6 |
| C13 | `docs/02` §Performance: "support horizontal scaling **without relying on process-local state**". ASP.NET Core's built-in rate limiter keeps its counters in process. ADR-0016 explicitly permits a per-instance cache for session and permission lookups, but says nothing about rate-limit state. | `docs/02:22`; ADR-0016:31 | With N instances, an in-process limit is N times looser. | D5 |
| C14 | `docs/20` §4: "Every endpoint has `.RequireAuthorization(<permission>)` or an explicit `.AllowAnonymous()`". Self-service endpoints (current user, logout, change own password) need **authenticated, with no specific permission**. That is a third case §4 does not allow. | `docs/20` §4 | Either §4 gains a third case, or a permission such as `auth.self` is invented. Inventing one is a grant question. | D17 |
| C15 | The Myanma Railways questions pack still presents OQ28 as open (O9). | `docs/business/mr-questions-pack.md` §OQ28 | Stale document. Not F-002's to fix; recorded so it is not sent to Myanma Railways as is. | hein (outside F-002) |
| C16 | ADR-0005 says "CORS allows only the configured frontend origins". ADR-0016 and `docs/15` make the SPA **same-origin**, which needs no CORS at all. | ADR-0005 §Decision; ADR-0016 §Context; `docs/15:31` | Enabling credentialed CORS "because ADR-0005 says so" would widen the cookie's exposure for no benefit. The configured origin list is the same data the `Origin` check needs. | D16 |

### 0.4 ADR-0009 account-security items — do they still appear intended?

Point (1) of T-023. For each item, the evidence that it is still intended, and what needs a ruling. **"Appears intended" is discovery's reading of the evidence, not a decision.**

| ADR-0009 item | Evidence it is still intended after ADR-0016 | Evidence against / gaps | Appears | Needs ruling | Decision |
|---|---|---|---|---|---|
| **User store:** ASP.NET Core Identity on SQL Server, schema `identity`, staff only | ADR-0016 keeps the `identity` schema and "password change" (so local passwords exist). `docs/07` lists `identity` as a module schema. ADR-0009 §Consequences anticipated a later move to OIDC, not an immediate one. | `docs/09` "if local accounts are used" leaves local accounts conditional. Full `AddIdentity` brings cookie schemes (O6). | **Intended: local staff accounts in `identity`.** The *mechanism* (Identity or not, `AddIdentity` or `AddIdentityCore`) is open. | Yes | D1 |
| **Lockout** after repeated failures | `docs/18` "Account takeover". `docs/09` "Secure password handling". | Lockout lets anyone who knows a username lock a Ticket Operator out mid-shift. That is an availability trade-off with an operational owner. No threshold anywhere. | Probably intended; the form is open | Yes | D3 |
| **Password policy** (NIST: length over complexity; breached-password check "if feasible") | `docs/09` "Secure password handling"; `docs/02` "Strong authentication" | No length, no blocklist source. A breached-password service would be an external dependency, and hosting is undecided. | Intended; values open | Yes | D2 |
| **Mandatory TOTP 2FA** for System Administrator, Railway Administrator, Finance Officer; optional for others | `docs/02` "Strong authentication"; `docs/18` "Account takeover", "Privilege escalation" | Names three roles OQ12 has not approved. Enrolment, recovery codes and admin reset make it a feature-sized addition. ADR-0016 is silent. | **Unclear**, the item most at risk of being dropped silently | Yes | D4 |
| **Rate-limit** login, refresh and 2FA | `docs/02` "Rate limiting"; `docs/09` "Rate limiting"; `docs/18` "API abuse". These three are still binding FACTs about required controls, but none names endpoints or limits. | C13 (process-local state); O7 (proxy address) | **Intended**, as a generic control that still binds | Values and mechanism | D5 |
| **Audit every auth event** (login success/failure, 2FA, lockout, token reuse, role change) | FR-010 "Security-sensitive … operations must be auditable" (FACT). ADR-0021 designs `SubjectId` null "for example a failed login where no user was resolved". `docs/18` requires audit for reuse. `docs/17` lists four auth events. | C10, C11, C12. Ledger growth while OQ14 (retention) is open. | **Intended**; the event set, names and payload content are open | Yes | D6 |
| Roles mapped to permissions, never role names in endpoints; mapping "seeded" | `docs/10`, `docs/20` §4 and F-001's `PermissionPolicyProvider` all already work this way. ADR-0020 decision 7 defers seeding to this feature. | ADR-0016 requires "removed permission effective within 30 seconds", which implies grants change at **runtime** (data), not by redeploying code. | Intended | Mechanism (seed data vs code) | D8 |

### 0.5 Decisions required from hein

Stage 2 cannot exit while the **Blocking** ones are open. "Discovery note" gives evidence, **not** a recommendation to be read as decided. Business items have no discovery preference.

| # | Question | Options found | Label / owner | Blocking? |
|---|---|---|---|---|
| **D1** | **User store.** | (a) ASP.NET Core Identity via **`AddIdentityCore<TUser>()`**: `UserManager`, `PasswordHasher`, lockout and TOTP support, **no cookie schemes**, tables mapped into `identity`. (b) Full **`AddIdentity<,>()`**: brings cookie schemes that ADR-0016 does not use and the O2 guard would have to allowlist. (c) **Own minimal user table**, using only the framework's `PasswordHasher<T>`, with lockout and TOTP written by hand. (d) **External IdP / OIDC.** This contradicts ADR-0016's own token issuance and would need a superseding ADR. *Discovery note:* (a) is the option that matches ADR-0009's intent without adding a scheme. | ENGINEERING DECISION — tech lead; ADR required (the Superseded ADR-0009 cannot be cited) | **Yes** (drives §7) |
| **D2** | **Password policy.** | (a) NIST SP 800-63B style: minimum length only, no composition rules, a maximum of at least 64, a blocklist of common/breached passwords shipped **offline** with the application. **VERIFY** the current NIST minimums: rev. 4 distinguishes single-factor from multi-factor use. (b) ASP.NET Core Identity's default composition rules. (c) Adopt whatever Myanma Railways or government policy mandates (OQ35). | ENGINEERING DECISION, unless OQ35 finds a mandated policy (then BUSINESS) | **Yes** for account creation and password change |
| **D3** | **Lockout and credential-failure handling.** | (a) Identity lockout: N consecutive failures → timed lockout, auto-unlock. (b) **No hard lockout**: per-account and per-source throttling only. This avoids the denial of service in §0.4 at the cost of weaker guessing resistance. (c) Lockout with administrator unlock only. **Also:** does an unknown username get the same response and timing as a wrong password (anti-enumeration)? Does a disabled account? | ENGINEERING DECISION, with an operational trade-off Myanma Railways may want a say in (OQ35) | **Yes** |
| **D4** | **MFA / TOTP.** | (a) In F-002: TOTP enrolment, verification and recovery, mandatory for roles hein names. The role names depend on OQ12. (b) **Separate feature before production**: F-002 is password-only, and a release gate records that privileged roles must not be provisioned in production until MFA ships. (c) Not required in Phase 1; ADR records why. | ENGINEERING DECISION on mechanism and scope; *which* roles is BUSINESS (OQ12 / OQ35) | **Yes** (scope) |
| **D5** | **Rate limiting of login and refresh** (and 2FA if D4a). | (a) ASP.NET Core rate limiter **in process**, partitioned by account and by client address. This accepts C13 (per-instance limits) and O7 (proxy address) until a trusted-proxy list exists. (b) At the **reverse proxy**, which is blocked on hosting (`docs/15`). (c) Both. **Also:** the limit values, and whether F-002 configures `ForwardedHeaders` with trusted proxies (which also fixes `audit.ClientIp`). | ENGINEERING DECISION — tech lead | **Yes** for S5 and S12's rate-limit rows; the rest of F-002 can proceed |
| **D6** | **Auth audit event set and content.** | Event set: (a) the ADR-0009 list (login success/failure, lockout, token reuse, role change) plus logout, password change, disable/enable, session revocation; (b) only what ADR-0016 and `docs/18` strictly require (family revocation on reuse); (c) failures and security events as audit, successes as logs/metrics only (ledger volume while OQ14 is open). **Naming:** `Identity.<Event>` per `docs/20` §2, or `docs/17`'s `Auth*` names (C11). **Within-grace predecessor:** audit, or log/metric only (C10)? **Personal data:** may snapshots carry username or display name (C12)? For a failed login with no resolved user, discovery notes that ADR-0021's null `SubjectId` path exists, and that storing the **typed** username risks storing a password typed into the wrong field. | ENGINEERING DECISION (tech lead); personal-data handling may be BUSINESS/compliance | **Yes** |
| **D7** | **Role catalogue (OQ12).** Which roles exist, their canonical identifiers, whether a user may hold several, and whether roles are station-scoped. | (a) `docs/03`'s **8** actors under their glossary names. (b) `docs/10`'s **7** columns (drop or fold Reporting User). (c) **Wait for Myanma Railways** (OQ12 is in the questions pack). (d) A **provisional tech-lead ASSUMPTION** in the OQ26–OQ30 pattern, explicitly not a Myanma Railways answer. **Identifier format** matters now because of C3 (for example `SystemAdministrator` vs `Admin`); it is permanent in the ledger. **Multiple roles per user:** ADR-0021's `ActorRole` is a JSON *array*, which suggests yes, but that is a column shape, not an approval. **Station scoping** is OQ12 option (c) in the pack. | **BUSINESS DECISION — Myanma Railways (OQ12)**; tech-lead provisional ruling possible | **Yes** |
| **D8** | **Grants and permissions F-002 may provision.** | Only `stations.manage` → Admin + Railway Admin and `stations.read` → the seven roles are approved (`docs/10`, T-014). F-002 **must not** seed any other grant. **F-002 also needs new permissions** for its own administration endpoints. Discovery's candidate names: `users.read`, `users.manage`, `users.roles.manage`, `auth-sessions.revoke`. None is in the `docs/10` inventory, and **names are not grants**. Options: (a) add the names to the inventory, hold them by **no role**, and administer only via the D10 bootstrap path until grants are approved; (b) hein rules **provisional grants** for the new identity permissions (T-014 pattern); (c) leave the administration API out of F-002 (D9). **Mechanism:** grants as data seeded by migration (runtime-changeable, as S19 needs) or as code. | Names: ENGINEERING. Grants: **BUSINESS (OQ12/OQ28 family)**. Mechanism: ENGINEERING | **Yes** |
| **D9** | **Account governance and administration scope (new OQ34).** Who may create staff accounts, disable them, assign roles and reset passwords? Is there segregation of duties (can an administrator grant themselves Finance Officer; does granting a privileged role need a second approver)? What identifies a staff member: username format, Myanma Railways employee number, name in Myanmar? And **which administration operations are in F-002 at all**? ADR-0016 requires only that "disablement" and "session administration" revoke sessions. | Scope options: (a) full administration API (§6.2); (b) **minimum for ADR-0016's required tests**: disable user, revoke session, change a user's roles; (c) no administration API: the bootstrap/CLI path only. Governance: Myanma Railways. | Scope: ENGINEERING. Governance: **BUSINESS — Myanma Railways (OQ34)** | **Yes** |
| **D10** | **Bootstrap: how the first administrator exists.** | (a) A **one-time CLI command** (for example in `YCR.Worker` or a dedicated tool) that creates the first administrator with a one-time password that must be changed, refuses if one exists, and writes an audit event with a null actor. (b) **Environment-driven first-run** creation at API startup. Discovery note: it puts a privileged write in the startup path, has a race between instances, and sits badly with F-001's no-work-at-startup stance (E7). (c) A **migration** that seeds a user: puts credential material in a migration. (d) A **documented SQL runbook** under the migrator credential. **Also:** *who* holds that account is OQ34. | Mechanism: ENGINEERING. Holder: **BUSINESS (OQ34)** | **Yes** |
| **D11** | **JWT signing key and algorithm, while hosting is undecided** (`docs/15` names no secret store; the same gap blocks ADR-0017's digest storage provider, OQ32). | Algorithm: (a) asymmetric (ES256/RS256), as ADR-0009 had; (b) symmetric HS256. Issuer and verifier are the same process, so the public-key benefit only matters if another party verifies. Storage: (a) key material **injected through environment/secret-management mechanisms** (the `docs/15:29` FACT) behind a key-provider abstraction, with a `kid`-based rotation path. Development keys are generated per developer and never committed, and production **refuses** a known development key, mirroring ADR-0014:58. (b) **Block production deployment** until a secret store is chosen, with dev/test only. (c) ASP.NET Data Protection key ring, which is itself storage-dependent. **Also:** all instances must share the key. | ENGINEERING DECISION — tech lead; ADR required | **Yes** (algorithm); storage provider can stay Blocked like OQ32 |
| **D12** | **Token, session and family model** (C5, C6, C7). | (i) Access-token lifetime. ADR-0009 had ~15 min; ADR-0016 is silent. (ii) Refresh-token idle and absolute lifetimes; neither ADR has them. They interact with OQ30's **provisional 12-hour cashier session**: a Ticket Operator must not be forced to log in again mid-session unless that is intended. (iii) The **exact grace window**: "approximately 20 seconds", so a configured constant. (iv) **Session ⇔ family 1:1?** Discovery's candidate: one `AuthSession` per login, whose refresh tokens are its family, so revoking the family is revoking the session. (v) **Which sessions a password change revokes**: all of the user's, or all except the current one? (vi) **C7:** confirm "only `sub` and `sid`" means "no identity or authorization claims besides these", with `iss`/`aud`/`exp`/`iat`/`jti` allowed. (vii) **C6:** logout by `sid` from the access token, expiring the cookie via `Set-Cookie` on the refresh path; or logout placed under the refresh path. | ENGINEERING DECISION — tech lead. (ii) may need an operational input (OQ30) | **Yes** |
| **D13** | **ADR-0020 follow-up — `AuthenticationSchemeGuard` allowlist.** | (a) Allowlist exactly the framework JWT bearer handler type; nothing else. (b) Also keep the F-001 test handler for non-auth features' API tests (still `Testing`-only) and require a **real-token** test path for ADR-0016's required tests and the `docs/21` "unmodified production composition" test. (c) Replace the test handler entirely with real tokens minted by a test helper. | ENGINEERING DECISION — tech lead | Plan-level; stated here because ADR-0020 names it |
| **D14** | **ADR-0020 follow-up — `AuthorizationResultHandler` step-aside, and the 401 body** (C8, C9). | ADR-0020 says prove it steps aside, then **delete it** if unnecessary. Once a default challenge scheme exists, its only branch is dead in production. Options for the 401 body: (a) **framework challenge + ProblemDetails**: customise the bearer challenge to write ProblemDetails with an `errorCode`, keeping `WWW-Authenticate: Bearer …`, and delete `AuthorizationResultHandler`. This contradicts ADR-0020's "`Common.Unauthenticated` no longer appears" only if the code is kept. (b) Framework default 401 with an empty body: contradicts `docs/20` §4. (c) Keep the handler as defence in depth: ADR-0020 says not to leave a dormant branch. **Error-code prefix:** `Auth.*` (follows ADR-0016's `Auth.RefreshSuperseded`) or `Identity.*` (follows `docs/20` §2). `MissingSchemeTests` must be rewritten, because its premise ("no scheme") stops being the deployed shape. | ENGINEERING DECISION — tech lead; may amend `docs/20` | **Yes** (error contract) |
| **D15** | **ADR-0020 follow-up — does "no `AuthenticationHandler<>` subtype in `src/`" survive unchanged?** | Evidence: if F-002 uses the framework JWT bearer handler and handles the refresh cookie **inside the refresh endpoint** (not as a scheme), no handler type is declared in `src/`, and the rule and the S21a test (O5) survive unchanged. Options: (a) **survives unchanged**; F-002 must not subclass a framework handler or write a cookie scheme. (b) Narrow it to an allowlist of permitted handler types, which needs a superseding ADR (ADR-0020 §Consequences). | ENGINEERING DECISION — tech lead | Plan-level; (b) would need an ADR first |
| **D16** | **SPA-side and browser controls** while no frontend exists (OQ22). | CSP `script-src 'self'`/`frame-ancestors 'none'`, "no raw HTML from API data", frontend lockfile audit in CI, and Web Locks single-flight refresh all live in the SPA or in whatever serves its HTML. **None can be built or tested without a frontend.** Options: (a) **Blocked behaviour** in F-002 with a named release gate: no production release with a SPA until these controls have evidence. F-002 still ships the API-side parts: the `Origin` check (required on cookie-bearing endpoints regardless of the SPA), API-response secure headers (`X-Content-Type-Options: nosniff`, a restrictive CSP and `frame-ancestors 'none'` on API responses, `Referrer-Policy`), and **no CORS** (C16). (b) All browser controls deferred to the first SPA feature, with a `TASKS.md` row. (c) Hold F-002 until OQ22 is answered. **Also:** should `POST /auth/login` check `Origin` too? It *sets* the cookie rather than sending it, but login CSRF is a known pattern. | ENGINEERING DECISION — tech lead (OQ22 is itself tech-lead) | **Yes** (scope of the Origin check) |
| **D17** | **Authenticated-only endpoints** (C14): `GET /auth/me`, `POST /auth/logout`, change own password. | (a) Amend `docs/20` §4 to allow `.RequireAuthorization()` with no permission, only with a comment giving the reason (mirrors the `.AllowAnonymous()` rule). (b) A self-service permission (for example `auth.self`) held by every role. That is a **grant**, so D8/OQ12 apply. | ENGINEERING DECISION (a) / BUSINESS grant (b) | **Yes** |
| **D18** | **Retention of `AuthSessions` and refresh-token rows**, and the application login's grants on `identity`. | Rows accumulate at one or more per login. Deleting expired rows needs a `DELETE` grant, which the least-privilege model (ADR-0017 §3, E7) has avoided so far. Or a Worker job under another credential. Not ledger rows, so OQ14 does not govern them. | ENGINEERING DECISION — tech lead | No: can be settled at PLAN |

### 0.6 New OPEN QUESTIONs added to `docs/19`

- **OQ34** — staff-account governance: who creates, disables and assigns roles to staff accounts; segregation of duties; the staff identifier; who holds the first administrator account (D9, D10).
- **OQ35** — whether Myanma Railways or a government policy mandates an authentication policy (passwords, lockout, MFA, session length) that the system must follow (D2, D3, D4, D12-ii).

OQ12 gains a **BLOCKS** clause naming F-002. No other OQ text is changed.

### 0.7 What discovery did **not** find

No document defines: a staff username or identifier format; access or refresh token lifetimes; a signing algorithm or key location; lockout thresholds; a password length; rate-limit values; the list of auth audit actions; any role grant beyond `stations.*`; any permission for administering users or sessions; how the first account is created; which roles MFA applies to under the new role names; or whether roles are station-scoped. The business ones are OQ12, OQ34 and OQ35. The rest are §0.5's engineering decisions.

---

## 1. Goal

Let real staff sign in and call the API. Implement ADR-0016's design end to end: access JWT (`sub`, `sid`), `identity.AuthSessions`, hashed rotating refresh tokens with the ~20 s predecessor grace, per-request session and permission resolution with ≤30 s revocation latency, and `Origin` checks. Provision users, roles and the approved role→permission grants, so F-001's station endpoints become usable outside the test suite. Close ADR-0020's follow-ups.

---

## 2. Actors and permissions

| Actor | Permission | Notes |
|---|---|---|
| Anonymous | none | `POST /auth/login` and `POST /auth/refresh` are `.AllowAnonymous()` with a reason comment: the caller has no access token yet, or has an expired one. Refresh is authenticated by its cookie. Health endpoints are unchanged from F-001. |
| Any authenticated staff user | *(authenticated, no permission)* | `GET /auth/me`, `POST /auth/logout`, change own password. **BLOCKED by D17** (C14). |
| User administrator | `users.read`, `users.manage`, `users.roles.manage` *(candidate names, D8)* | **Held by no role** until D8/OQ12/OQ34 are ruled. Names are not grants. |
| Session administrator | `auth-sessions.revoke` *(candidate name, D8)* | Same as above. |
| Roles holding `stations.manage` / `stations.read` | as `docs/10` §Station permission grants | **The only grants F-002 may seed.** The role *identifiers* they attach to are **BLOCKED by D7** (C2, C3). |

---

## 3. Business rules

| # | Rule | Label | Source |
|---|---|---|---|
| R1 | Access JWTs carry `sub` and `sid` and **no permission or role claims**. | ENGINEERING DECISION (tech lead, cite ADR-0016). Whether registered claims (`exp`, `iss`, `aud`, …) are permitted is **OPEN — D12(vi)** (C7) | ADR-0016:29 |
| R2 | `identity.AuthSessions` stores at least `Id`, `UserId`, `RevokedAtUtc`. Every request resolves the session and the caller's permissions server-side. | ENGINEERING DECISION (cite ADR-0016) | ADR-0016:30-31 |
| R3 | Revocation and permission changes take effect within **30 seconds**. A per-instance cache may be used within that bound. | ENGINEERING DECISION (cite ADR-0016) | ADR-0016:31 |
| R4 | Logout, password change, disablement, token-family reuse and session administration revoke the relevant session/refresh family. | ENGINEERING DECISION (cite ADR-0016). *Which* sessions a password change revokes is **OPEN — D12(v)** | ADR-0016:32 |
| R5 | Refresh tokens are random, stored **hashed**, rotated on every use, and sent only in an `HttpOnly; Secure; SameSite=Strict` cookie with `Path=/api/v1/auth/refresh`. | ENGINEERING DECISION (cite ADR-0016) | ADR-0016:36 |
| R6 | On rotation, `ReplacedByTokenId` and `RotatedAtUtc` are persisted. | ENGINEERING DECISION (cite ADR-0016). *Where* (table shape) is **OPEN — D12(iv)** (C5) | ADR-0016:38 |
| R7 | The immediate predecessor presented within ~20 s of its rotation returns `409 Auth.RefreshSuperseded`, issues **no** tokens and does **not** revoke the family. | ENGINEERING DECISION (cite ADR-0016). Exact window **OPEN — D12(iii)** | ADR-0016:39 |
| R8 | The predecessor after the grace window, or any older ancestor, **revokes the whole family** and emits an audit event. | ENGINEERING DECISION (cite ADR-0016); REQUIRED CONTROL (`docs/18`) | ADR-0016:40; `docs/18:35` |
| R9 | Every cookie-bearing endpoint checks the request `Origin`; a missing `Origin` is covered by an API test. | ENGINEERING DECISION (cite ADR-0016); REQUIRED CONTROL (`docs/18:34`). Whether login is included is **OPEN — D16** | ADR-0016:44, 47 |
| R10 | Authorization is by permission, never by role name. Endpoints use `.RequireAuthorization(<permission>)` or `.AllowAnonymous()` with a reason. | FACT (`docs/10`, `docs/20` §4; F-001 pipeline). Authenticated-only third case **OPEN — D17** | `docs/20` §4 |
| R11 | F-002 seeds **only** `stations.manage` → the two administrator roles and `stations.read` → the seven roles. No other grant is seeded. | **DECISION (tech lead, hein, 2026-09-22; T-014) — final; not a Myanma Railways answer** (the F-001 R8 label). The role *identifiers* are **BLOCKED — D7 / OQ12** | `docs/10` §Station permission grants |
| R12 | Actor fields on audit rows come only from the server-side authenticated context. `ActorRole` is the JSON array of roles held at event time. | ENGINEERING DECISION (cite ADR-0017 §2, ADR-0021) | ADR-0017:28; ADR-0021 |
| R13 | Passwords, tokens, refresh cookies and private keys never appear in logs, audit payloads or error responses. | REQUIRED CONTROL (cite `docs/20` §7, ADR-0021 rule 4) | `docs/20` §7 |
| R14 | Secrets, including the signing key, are injected through environment or secret-management mechanisms, never committed. | FACT (`docs/15:29`); REQUIRED CONTROL (`docs/21` §Security) | `docs/15` |
| R15 | The deployable artifact contains no authentication handler type of YCR's own. Startup refuses any scheme outside the allowlist, except under `Testing`. | ENGINEERING DECISION (cite ADR-0020 items 4–6). Whether this survives F-002 unchanged is **OPEN — D15**; the allowlist contents are **OPEN — D13** | ADR-0020 |
| R16 | Login and refresh are rate-limited. | FACT that the control is required (`docs/02`, `docs/09`). Limits and mechanism **OPEN — D5** | `docs/02` §Security |
| R17 | Local staff accounts with passwords exist in the `identity` schema. | ASSUMPTION (evidence in §0.4 row 1). Mechanism **OPEN — D1** | ADR-0016:32 ("password change"); `docs/07` |
| R18 | Password policy, lockout, MFA. | **OPEN QUESTION — D2, D3, D4, OQ35** | §0.4 |
| R19 | Which roles exist, and their identifiers. | **OPEN QUESTION — OQ12 / D7** | `docs/03`, `docs/10`, glossary |
| R20 | Who administers accounts; the staff identifier; the first administrator. | **OPEN QUESTION — OQ34 / D9, D10** | — |
| R21 | Auth error codes are ProblemDetails with `errorCode` and `traceId`. | FACT (`docs/20` §4, ADR-0004). Prefix and the 401 body **OPEN — D14** (C8, C9) | `docs/20` §4 |
| R22 | Auth events written to the audit ledger. | FACT that security-sensitive operations are auditable (FR-010). Event set, names and payload **OPEN — D6** | FR-010; `docs/18` |

**Blocking open questions:** **OQ12** (D7), **OQ34** (D9, D10), **OQ35** (D2–D4, where a mandated policy exists), and engineering decisions **D1–D12, D14, D16, D17**. The spec cannot be Approved until each is ruled on, answered or explicitly waived by hein.

---

## 4. Scenarios (Given / When / Then)

Scenarios marked **[BLOCKED Dn]** are stated so the shape is reviewable, but their exact expectations depend on the named decision. Error codes shown `Auth.*` are provisional under D14.

### Login

- **S1.** Given an active user with valid credentials and a request whose `Origin` matches the configured origin, when they POST `/api/v1/auth/login`, then the response is `200` with `{ accessToken, expiresAtUtc }`. `Set-Cookie` carries the refresh token with `HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth/refresh`. One `identity.AuthSessions` row exists with `RevokedAtUtc` null, and the stored refresh-token value is a **hash**, never the raw token (R5; S31).
- **S2.** Wrong password → `401 Auth.InvalidCredentials`. **[BLOCKED D3]**: whether an unknown username, a disabled account and a locked account get the identical response and timing.
- **S3.** Disabled user with a correct password → rejected; no session and no cookie. **[BLOCKED D3]** (status code / uniformity).
- **S4.** Repeated failures → lockout or throttling behaviour. **[BLOCKED D3]**
- **S5.** Login attempts over the limit → `429` ProblemDetails. **[BLOCKED D5]**
- **S6.** Login without a matching `Origin` → rejected. **[BLOCKED D16]** (whether login is Origin-checked).

### Refresh and rotation (ADR-0016 §Required tests)

- **S7.** Valid refresh cookie + matching `Origin` → `200` with a new access token. The presented token gets `RotatedAtUtc` and `ReplacedByTokenId`, and the new cookie replaces it.
- **S8. Two-tab race.** Two concurrent refreshes with the same token → exactly one `200` with rotation. The other gets `409 Auth.RefreshSuperseded` with **no** tokens, and the family is **not** revoked. A retry with the successor cookie succeeds.
- **S9. Predecessor inside grace.** The immediate predecessor presented within the grace window → `409 Auth.RefreshSuperseded`, no `Set-Cookie`, no access token, family intact. **[D6: audit or log-only]** (C10).
- **S10. Predecessor outside grace.** The same presentation after the window → the whole family is revoked (the session's `RevokedAtUtc` is set), `401`, and one audit event is written (R8). A subsequent refresh with the successor also fails.
- **S11. Ancestor reuse.** An older ancestor, at any time → the whole family is revoked and one audit event is written.
- **S12. Missing `Origin`** on `/auth/refresh` → rejected; no rotation, no tokens (R9; `docs/18:34`). **Wrong `Origin`** likewise. Rate limit → `429` **[BLOCKED D5]**.
- **S13.** Expired refresh token → `401`, no rotation. **[BLOCKED D12(ii)]** (lifetimes).
- **S14.** Refresh on a revoked session → `401`, no tokens.

### Logout, password change, disablement, revocation (ADR-0016 §Required tests)

- **S15. Logout.** An authenticated user POSTs `/auth/logout` → `204`. The session's `RevokedAtUtc` is set, the refresh cookie is expired, the next refresh fails, and a request with the old access token is rejected within **≤30 s** (R3). **[D12(vii)]** (how logout finds the session, C6); **[D17]** (authorization shape).
- **S16. Password change.** Changing one's own password revokes sessions per **[D12(v)]**. Revoked sessions' access tokens are rejected within ≤30 s and their refresh fails. **[BLOCKED D2]** (policy on the new password).
- **S17. Disabled user.** A user with a live session is disabled → their access token is rejected within ≤30 s and their refresh fails. **[BLOCKED D9]** (who disables and through what). The test may disable through the store directly if D9 leaves no API.
- **S18. Removed permission within 30 s.** A user holding `stations.read` through a role has that role (or the grant) removed → `GET /api/v1/stations` returns `403` within ≤30 s, with no re-login. **[BLOCKED D7, D8]** (role identifiers; runtime-changeable grants).
- **S19. Session revocation.** An administrator revokes a user's session → that session's access token is rejected within ≤30 s and its refresh fails. **[BLOCKED D8, D9]**

### Access tokens and the pipeline

- **S20.** A decoded access token contains `sub` and `sid` and no role or permission claim (R1). **[D12(vi)]** for the registered claims.
- **S21.** A token that is tampered with, expired, signed with the wrong key, or whose `sid` names a revoked or unknown session → `401`.
- **S22. `AuthorizationResultHandler` steps aside (ADR-0020 follow-up).** With the real scheme registered, an unauthenticated request to a protected endpoint returns `401` with `WWW-Authenticate: Bearer`. The body is per **[D14]**, and whether `Common.Unauthenticated` still appears is per D14. `AuthorizationResultHandler` is then deleted or retained per D14.
- **S23. Startup guard.** Outside `Testing`, startup succeeds with exactly the allowlisted handler type(s) and **throws** if any other scheme is registered. Both halves are tested (ADR-0020 item 6). **[D13]** (allowlist contents).
- **S24. Architecture rule.** The S21a test still passes with F-002's code, and its violating fixture still fails it. **[D15]**
- **S25. Unmodified production composition (`docs/21` §Tests).** With no `ConfigureTestServices` overrides: log in with a real account holding `stations.read`, call `GET /api/v1/stations` with the issued token → `200`; the same call anonymously → `401`. This replaces the premise of F-001's `MissingSchemeTests`. **[BLOCKED D7]** (a role to hold the grant), **[D10]** (how the test account is created).
- **S26. Wrong permission.** An authenticated real user without `stations.manage` POSTs `/stations` → `403` (`docs/21` §Tests).

### Audit and secrets

- **S27.** Every auth audit event in the D6 set has actor fields from the server-side context only. A request body or header naming another user or role cannot influence them (R12; F-001 S20 pattern). **[BLOCKED D6]** (event set).
- **S28.** After exercising S1–S21, no captured log line, no audit row and no ProblemDetails body contains a password, an access token, a refresh token or the cookie value (R13).
- **S29.** After S8–S11, the ledger contains exactly the audit rows D6 prescribes: one per family revocation, none for the benign duplicate unless D6 says otherwise.

### Provisioning and bootstrap

- **S30.** On a fresh database, the migration seeds the approved `stations.*` grants and **no other** role→permission row. A test enumerates the seeded grants and compares them with `docs/10`. **[BLOCKED D7]** (role identifiers), **[D8]** (seed mechanism).
- **S31.** Refresh tokens and passwords are stored only as hashes; a query over `identity` finds no raw token.
- **S32.** Bootstrap creates exactly one first administrator, refuses to run a second time, and writes an audit event. **[BLOCKED D10, OQ34]**

### Concurrency

- **S33.** Parallel rotations of the same token never produce two successors: exactly one `ReplacedByTokenId`, enforced in the database, not only in code. This is the storage half of S8.

---

## 5. State changes

| Entity | From | Event | Guard | To |
|---|---|---|---|---|
| AuthSession | (none) | Login | credentials valid; user active and not locked (D3) | Active |
| AuthSession | Active | Logout / password change (D12-v) / user disabled / administrator revocation / family reuse (R8) | per event | Revoked (`RevokedAtUtc` set; terminal) |
| AuthSession | Active | Absolute lifetime reached | **D12(ii)** | Expired (derived or stored: D12) |
| Refresh token | Current | Presented within its lifetime, session active | — | Rotated (`RotatedAtUtc`, `ReplacedByTokenId`); successor becomes Current |
| Refresh token | Rotated (immediate predecessor) | Presented within grace | — | unchanged; `409 Auth.RefreshSuperseded` |
| Refresh token | Rotated (predecessor after grace, or any ancestor) | Presented | — | family revoked → AuthSession Revoked |
| User | Active | Disable | **D9** | Disabled (revokes all sessions) |
| User | Disabled | Enable | **D9** | Active |
| User | Active | Failed-login threshold | **D3** | Locked (if D3 has lockout) |

---

## 6. API

Base path `/api/v1`, JSON camelCase, ProblemDetails with `errorCode` and `traceId` (`docs/20` §4), subject to D14. **Proposal only**: every row carries the decision it depends on.

### 6.1 Authentication (`/auth`)

| Method | Path | Request | Success | Error codes (provisional prefix, D14) | Authorization | Cookie / Origin | Idempotency |
|---|---|---|---|---|---|---|---|
| POST | `/auth/login` | `{ username, password }` (+ TOTP code if D4a) | `200 { accessToken, expiresAtUtc }` + refresh `Set-Cookie` | `400` validation · `401 Auth.InvalidCredentials` · `429` (D5) · lockout (D3) | `.AllowAnonymous()`: caller has no token yet | Sets cookie; Origin check **D16** | No |
| POST | `/auth/refresh` | none (cookie) | `200 { accessToken, expiresAtUtc }` + rotated cookie | `401 Auth.RefreshInvalid` · `409 Auth.RefreshSuperseded` (ADR-0016) · `403` Origin · `429` (D5) | `.AllowAnonymous()`: authenticated by the cookie, not a bearer token | **Cookie-bearing; Origin checked (R9)** | No. A benign duplicate is handled by R7, not `Idempotency-Key` (`docs/20` §5 does not list it) |
| POST | `/auth/logout` | none | `204` + cookie expiry `Set-Cookie` on the refresh path | `401` | Authenticated (**D17**); session from `sid` (**D12-vii**, C6) | Not cookie-bearing under D12-vii (a) | No |
| GET | `/auth/me` | — | `200 { userId, displayName?, roles[], permissions[] }` | `401` | Authenticated (**D17**) | — | n/a |
| POST | `/auth/password` | `{ currentPassword, newPassword }` | `204` | `400` policy (**D2**) · `401` · `422 Auth.CurrentPasswordIncorrect` | Authenticated (**D17**) | — | No |

`displayName` in `/auth/me` depends on OQ34 (the staff identifier and personal data).

### 6.2 Administration: entire section BLOCKED by D8, D9, OQ34

| Method | Path | Purpose | Candidate permission (D8) |
|---|---|---|---|
| GET | `/users` · `/users/{id}` | list / view staff accounts | `users.read` |
| POST | `/users` | create account (initial password per D2/D10) | `users.manage` |
| POST | `/users/{id}/disable` · `/users/{id}/enable` | disablement (revokes sessions, R4) | `users.manage` |
| PUT | `/users/{id}/roles` | replace role assignments | `users.roles.manage` |
| POST | `/users/{id}/password-reset` | administrator reset | `users.manage` |
| GET | `/users/{id}/auth-sessions` | list a user's sessions | `users.read` |
| POST | `/auth-sessions/{id}/revoke` | session administration (ADR-0016) | `auth-sessions.revoke` |
| GET | `/roles` | read the role catalogue and its permissions | `users.read` |

No endpoint edits role→permission grants. Grants change by reviewed migration (D8 mechanism), because an API would let the holder of that permission grant anything to anyone.

---

## 7. Data

Schema `identity` (`docs/07` §Module schemas; ADR-0012 `IIdentityDbContext`). **Shape depends on D1.** Under D1(a), Identity's default tables are remapped into `identity` under these names. Columns marked *(D-n)* depend on that decision. All `*Utc` columns are `datetimeoffset(3)` with a UTC check constraint (`docs/20` §2, `docs/07` pattern). All ids are application-assigned GUIDs (ADR-0006).

**`identity.Users`**: `Id` PK · `UserName` `nvarchar(?)` unique on its normalized form *(format: OQ34)* · `PasswordHash` · `SecurityStamp` *(D1)* · `IsDisabled` / `DisabledAtUtc` · `LockoutEndUtc`, `AccessFailedCount` *(D3)* · TOTP secret and recovery codes *(D4; secret encrypted at rest, the key is a D11-style secret)* · `PasswordChangedAtUtc` · `MustChangePassword` *(D10)* · `CreatedAtUtc` · `rowversion` *(concurrent admin edits vs login counters; PLAN)*.

**`identity.Roles`**: `Id` PK · `Name` unique. **Values BLOCKED by OQ12/D7.**

**`identity.UserRoles`**: `(UserId, RoleId)` PK, FKs. Multiple roles per user **per D7**.

**`identity.RolePermissions`**: `(RoleId, Permission nvarchar(100))` PK, FK to Roles. `Permission` follows `docs/20` §2 (`<resource>.<action>`), checked against `Permissions` constants by a test. **Seeded rows: the `stations.*` grants only (R11, S30).**

**`identity.AuthSessions`**: `Id` PK · `UserId` FK · `RevokedAtUtc` null (ADR-0016). **Proposed additions (D12):** `CreatedAtUtc`, `ExpiresAtUtc` (absolute lifetime), `RevocationReason` (enum-as-string: `Logout`, `PasswordChanged`, `UserDisabled`, `AdministratorRevoked`, `FamilyReuse`), index on `UserId`.

**`identity.RefreshTokens`** *(C5; D12-iv, assumes session ⇔ family 1:1)*: `Id` PK · `SessionId` FK → AuthSessions · `TokenHash` `binary(32)` **unique** · `IssuedAtUtc` · `ExpiresAtUtc` · `RotatedAtUtc` null · `ReplacedByTokenId` null, FK → RefreshTokens.Id, **filtered unique index where not null** (S33: a token can have at most one successor, enforced by the database).

**Database grants** (migration, `Security_AppDatabaseRole` pattern; ADR-0017 §3 spirit): `ycr_app` gets SELECT/INSERT on the identity tables, and UPDATE only on the columns that legitimately change (`RevokedAtUtc`, `RevocationReason`, `RotatedAtUtc`, `ReplacedByTokenId`, lockout/password/disabled columns). **No DELETE (D18).** A test asserts the grants, as F-001 S22 does.

**Not in F-002:** `IdempotencyRecords`, and any TOTP table if D4 ≠ (a).

Migrations are named `yyyyMMddHHmmss_Identity_<Change>` and reviewed per `docs/workflows/04-database-change.md`. Seed data containing role identifiers waits for D7.

---

## 8. Audit, logging, metrics

- **Audit** (ADR-0017, ADR-0021): the event set, names, the within-grace case and payload content are **BLOCKED by D6**. Fixed regardless of D6: family revocation on reuse writes an audit event (R8, `docs/18`). Actor fields come from the server context. For a refresh-reuse event there is no bearer principal: the actor is the user **resolved server-side from the token hash's session**. D6 must confirm that this counts as "authenticated server-side context" under ADR-0017 §2. Snapshots never contain passwords, hashes, tokens or cookies (R13).
- **Logging** (`docs/20` §7): message templates; never log credentials, tokens, cookie values or, per C12/D6, usernames.
- **Metrics** (`docs/17`): `AuthLoginSucceeded`, `AuthLoginFailed`, `AuthRefreshSuperseded` and `AuthRefreshFamilyRevoked` as events or counters, and **session/permission cache revocation latency** (`docs/17:35`), which is the measurable side of R3.

---

## 9. Out of scope

The SPA and everything that needs it: CSP on the SPA's HTML, "no raw HTML from API data", frontend lockfile/dependency audit, Web Locks single-flight refresh (D16, OQ22). Also out of scope: OIDC/SSO and passenger accounts (ADR-0009 §1: staff only); station- or duty-scoped authorization unless D7 includes it; editing role→permission grants through the API (§6.2); any grant other than `stations.*` (R11); cashier-session behaviour (Operations; OQ30); ledger digest operations (ADR-0017); rows retention jobs (D18, unless ruled into scope); the stale questions pack (C15).

---

## 10. Security controls: mapping

### 10.1 ADR-0016 §Required tests

| Required test | Scenario | Status |
|---|---|---|
| Refresh race with two tabs | S8, S33 | Specified |
| Predecessor reuse inside grace | S9 | Specified; audit per D6 |
| Predecessor reuse outside grace | S10 | Specified |
| Ancestor reuse | S11 | Specified |
| Logout | S15 | D12(vii), D17 |
| Password change | S16 | **BLOCKED D2, D12(v)** |
| Disabled user | S17 | **BLOCKED D9** (mechanism) |
| Removed permission effective within 30 s | S18 | **BLOCKED D7, D8** |
| Missing Origin | S12 (S6 for login, D16) | Specified |
| Session revocation | S19 | **BLOCKED D8, D9** |

### 10.2 `docs/18` §Required controls

| Control | F-002 treatment |
|---|---|
| CSP `script-src 'self'`, no `unsafe-*`, `frame-ancestors 'none'` | **SPA part blocked (D16, OQ22).** API-response headers proposed in D16(a). |
| SPA renders no raw HTML from API data | **Blocked (OQ22)**: no SPA. Release gate per D16. |
| Frontend lockfile/dependency audit in CI | **Blocked (OQ22)**: no frontend lockfile exists. Release gate per D16. |
| Every cookie-bearing endpoint checks `Origin` | **In scope:** R9, S12 (S6 per D16). |
| Predecessor and ancestor reuse emit security audit events | **In scope:** R8, S10, S11, S29; within-grace per D6 (C10). |

### 10.3 `docs/18` threat categories and `docs/09` controls touched

Account takeover (D2–D5, S2–S6); privilege escalation (R10, R11, D8, D9; no grant API); API abuse (D5); data disclosure (R13, S28, C12); insider manipulation (audit, D6; segregation of duties, OQ34); same-origin XSS using an in-memory token (HIGH; SPA-side, D16); refresh predecessor replay and family reuse (R7, R8, S8–S11). From `docs/09`: authentication, role/policy authorization, secure password handling (D1, D2), rate limiting (D5), CSRF protection (R9), secure headers (D16), secret management (R14, D11), audit logging (D6), least privilege and database least privilege (§7 grants).

### 10.4 `docs/21` items with F-002-specific evidence

401/403/400 API tests (S2, S21, S26); **unmodified production composition** (S25); cookie endpoints' Origin/CSRF evidence (S12); "XSS controls include CSP and dependency audit evidence **where the SPA is affected**": the SPA is not affected in F-002, and D16 records why; no secrets in code/config/logs (R13, R14, S28); `docs/10` updated (the new permission names, D8).

---

## Blocked behaviour

Required by `docs/21` §Specification. **No placeholder implementation for any row.**

| Behaviour | Blocking item | Effect on F-002 |
|---|---|---|
| Role catalogue and role identifiers | **OQ12** / D7 (C2, C3) | No role rows, so no grants can be attached and no real user can hold `stations.*`. S18, S25, S30 cannot pass. |
| Any role grant beyond `stations.*`, including grants of the new identity permissions | **OQ12/OQ28 family**, D8 | Administration endpoints held by no role. |
| Staff-account governance, the staff identifier, the first administrator's holder | **OQ34** / D9, D10 | §6.2 and bootstrap cannot be specified exactly. |
| Mandated authentication policy, if any | **OQ35** / D2, D3, D4 | Password, lockout and MFA rules not implementable. |
| User store mechanism | D1 | §7 table shapes provisional. |
| Password policy | D2 | Account creation and password change (S16) blocked. |
| Lockout / anti-enumeration | D3 | S2–S4 expectations open. |
| MFA scope | D4 | Unknown whether F-002 includes TOTP. |
| Rate limits | D5 | S5 and the rate-limit half of S12 blocked. |
| Auth audit events, names, payloads | D6 (C10, C11, C12) | S27, S29 blocked. |
| Signing algorithm and key location | D11; storage provider also waits on hosting (`docs/15`, same gap as OQ32) | Token issuance not implementable. Storage may stay blocked for production with a dev/test path if hein rules so. |
| Token/session lifetimes and model | D12 | S13, S15, S16 details open; §7 `RefreshTokens` provisional. |
| 401 body and error-code prefix | D14 (C8, C9) | S22 open. |
| Origin check on login; API secure headers | D16 | S6 open. |
| Authenticated-only endpoints | D17 (C14) | `/auth/me`, logout and change password lack an allowed authorization shape. |
| SPA CSP, raw-HTML rule, lockfile audit, Web Locks | **OQ22** (D16) | Not buildable without a frontend; release gate, not F-002 scope. |
| `AuthSessions`/`RefreshTokens` row retention | D18 | No cleanup job; plan-level. |

---

## Notes for the next stage

- **Stop here.** Stage 2 exits only with no blocking OPEN QUESTION (`docs/workflows/02-feature-development.md`). T-023 is set to `blocked` (`approval`) pending hein's rulings on §0.5.
- Once D1, D4, D11, D12 and D14 are ruled, at least one new ADR is expected, restating the account-security decisions ADR-0009 held (C1). If D15(b) is chosen, a superseding ADR for ADR-0020 decision 4 is required too.
- If hein rules D7/D8 provisionally (OQ26–OQ30 pattern), the rulings must be labelled "not a Myanma Railways answer" in `docs/10`, `docs/19` and this spec, as T-014 did.
- OQ34 and OQ35 are new in `docs/19`. Whether they go into `docs/business/mr-questions-pack.md` is hein's call (the T-012 classification pattern), as is fixing C15.
- O3/O4: the plan must make per-request resolution produce **both** permissions and roles, or `ActorRole` goes null for every audit row after F-002.

# Plan: F-002 Staff authentication — ADR-0016 tokens, sessions and role→permission provisioning

Spec: `spec.md` — **Approved (hein, 2026-09-23)** at `b28e0fe`; closing doc syncs at `d2a368d`.
Stage: 3 (PLAN), `docs/workflows/02-feature-development.md`. Task T-024. Architect-agent stage (`docs/prompts/architect-agent.md`).
Binding inputs: ADR-0004, ADR-0005, ADR-0006, ADR-0012, **ADR-0016**, ADR-0017, ADR-0018, **ADR-0020 (as amended 2026-09-23)**, **ADR-0021 (as amended 2026-09-23)**, ADR-0022, **ADR-0023**; `docs/10` §Identity permission grants; `docs/20-coding-conventions.md`; `docs/21-definition-of-done.md`. Worked example: `docs/features/F-001-walking-skeleton/plan.md`.

**Status: Draft — awaiting hein's approval.** Two spec gaps (G1, G2) are listed in §Spec gaps and are **not** decided here; the plan items that depend on them are marked. Fifteen plan decisions (P1–P15) are made and listed in §Decisions for hein to confirm or overturn. Stop at the ⛔: nothing is implemented until hein approves.

---

## Understanding

F-002 replaces F-001's deliberately authentication-less deployment with ADR-0016's design and ADR-0023's account-security decisions. Concretely it builds five things:

1. **The `Identity` module** — the first module after `Network`: two aggregates (`StaffUser`, `AuthSession` with its `RefreshToken` children), the role catalogue and role→permission grants as migration-seeded data, and eighteen use cases (sign-in, refresh, sign-out, own password, current user, the administration API of spec §6.2, the bootstrap, and per-request principal resolution).
2. **The authentication pipeline** — the framework's `JwtBearerHandler` (ES256, `kid`), with the principal **rebuilt server-side on every request** from the session and the database (roles *and* permissions, spec O3/O4), a per-instance cache bounded at ≤30 s (R3), the challenge customised to ProblemDetails (D14), and `AuthorizationResultHandler` deleted.
3. **Browser-facing controls on the API** — `Origin` checks on the three cookie endpoints, in-process rate limits, the hardened path-scoped refresh cookie, security headers, no CORS, and the must-change-password gate (R26).
4. **Provisioning** — eight roles and fourteen grants by migration, least-privilege grants for `ycr_app` on `identity`, and a one-time bootstrap command for the first `SystemAdministrator`.
5. **Tests for all of it**, including every ADR-0016 required test with real tokens and one test over the unmodified production composition (S25).

What makes this feature unusual: it is security infrastructure that every later feature trusts without re-reading. A mistake in principal resolution silently grants or denies everywhere; a mistake in the audit actor path writes wrong history into an append-only ledger. The plan therefore prefers mechanisms whose correctness is visible in one place (one resolver, one cache, one audit-actor rule enforced by an architecture test) over mechanisms spread across endpoints.

---

## Facts / Assumptions / Open questions

**FACT — spec O1, O2 (re-read at `d2a368d`):** `Program.cs` calls `AddAuthentication()` with no scheme and registers `PermissionPolicyProvider`, `PermissionAuthorizationHandler` and `AuthorizationResultHandler`; `AuthenticationSchemeGuard.AllowedHandlerTypes` is empty.
**FACT — `src/YCR.Api/Common/Authorization/PermissionPolicyProvider.cs:44-52` (found at PLAN):** `LooksLikeAPermission` accepts exactly one `.` and only lower-case letters. It **rejects `users.roles.manage` (two dots) and `auth-sessions.revoke` (hyphen)** — both approved in `docs/10`. Without a change those names fall through to the default provider and every endpoint using them throws on its first request. Step 1 fixes the pattern (P-note in §API changes).
**FACT — `PermissionAuthorizationHandler` (O3)** grants on a `permission` claim; **`HttpContextCurrentUser` (O4)** reads `ClaimTypes.NameIdentifier` and `ClaimTypes.Role`. Both stay unchanged if the server-built principal carries those claims (P3).
**FACT — `AuditWriter`** takes every actor field from `ICurrentUser` (ADR-0017 §2). On `/auth/login` and `/auth/refresh` a caller may still send *some* bearer token, which the default scheme would authenticate; the actor rules of R12/U4 therefore need an explicit mechanism (P5), not the ambient principal.
**FACT — `.github/workflows/ci.yml:260-267`:** the API smoke job asserts `401` with `errorCode` `Common.Unauthenticated`. After F-002 the deployed shape needs a signing key and an allowed origin to start, and the code becomes `Auth.Unauthenticated`; the job changes in step 12.
**FACT — ADR-0004:** transactions come from one `SaveChangesAsync` per handler. P14 makes one documented exception (R27).

### Verifications done at PLAN (spec O6)

Run in a scratch console app outside the repository (`net10.0`, SDK 10.0.302; packages pinned at 10.0.12; the machine's installed ASP.NET Core runtime is **10.0.10**, see V2).

| # | Question | Result |
|---|---|---|
| O6-a | Does `AddIdentityCore<T>()` register an authentication scheme? | **No.** `AddAuthentication()` + `AddIdentityCore<T>()` → zero schemes, zero handlers, no default scheme. Also zero with `.AddRoles<>().AddEntityFrameworkStores<>()` and `.AddSignInManager()`. Full `AddIdentity<,>()` registers four `CookieAuthenticationHandler` schemes (`Identity.Application`, `Identity.External`, `Identity.TwoFactorRememberMe`, `Identity.TwoFactorUserId`) — confirming D1's reason. |
| O6-b | Where do the Identity types live? | `AddIdentityCore`, `UserManager<T>`, `PasswordHasher<T>`: `Microsoft.Extensions.Identity.Core` (in the `Microsoft.AspNetCore.App` shared framework; also a NuGet package, 10.0.12). `IdentityUser<T>`, `UserOnlyStore<>`: `Microsoft.Extensions.Identity.Stores`. The EF stores need the **NuGet** package `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.12 (→ `Identity.Stores` 10.0.12, `EntityFrameworkCore.Relational` 10.0.12). |
| O6-c | Table mapping with `UserOnlyStore` over a plain `DbContext` into `identity` | Works: `ToTable("Users","identity")`, and `Ignore(...)` of inherited `Email`/`Phone`/`TwoFactor` properties is allowed. **But** the store reports `SupportsUserClaim/Login/AuthenticationTokens/Email/PhoneNumber/TwoFactor = true` although no table exists, and fails only at call time (`Cannot create a DbSet for 'IdentityUserClaim<Guid>'`); its lockout uses Identity's own clock and fields (`LockoutEnd`, `LockoutEnabled`, `ConcurrencyStamp nvarchar(max)`), not spec §7's columns. |
| O6-d | Hand-written store over a POCO (not `IdentityUser`) | Works with `AddIdentityCore<T>().AddUserStore<TStore>()`. Only the implemented capabilities are reported as supported; the rest throw `NotSupportedException`. `IUserStore` (10) + `IUserPasswordStore` (3) + `IUserSecurityStampStore` (2) + `Dispose` = 16 members. |
| O6-e | `PasswordHasher` defaults | `IdentityV3`: PBKDF2-HMAC-SHA512, **100 000 iterations**, 16-byte salt, 32-byte subkey → an 84-character Base64 string. A dummy verification against a precomputed hash costs the same as a real one (≈40 ms each, 20 runs). |
| O6-f | JwtBearer 10.0.12 | Exists; depends on `Microsoft.IdentityModel.Protocols.OpenIdConnect` **8.19.2**; the whole `Microsoft.IdentityModel.*` set and `System.IdentityModel.Tokens.Jwt` resolve at **8.19.2**. Handler type `Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerHandler`. `JsonWebTokenHandler` (issuance) is available transitively. Defaults: `MapInboundClaims = true` (renames `sub`), `ClockSkew` 5 min, `UseSecurityTokenValidators = false`. |
| O6-g | ES256 token shape | Header `{"alg":"ES256","kid":…,"typ":"JWT"}`; payload exactly `sub, sid, jti, iss, aud, exp, iat, nbf` — the handler adds `iat`/`nbf` itself and nothing else. With `ValidAlgorithms = [ES256]`, HS256, `alg=none`, another EC key under the same `kid`, and HS256 keyed with the EC public key are all rejected, both directly and through the JwtBearer pipeline. |

**Conclusion for spec O6:** D1's reasoning holds on the pinned packages. The mapping decision this enables is P1 (a hand-written store over a domain aggregate — no Identity-owned tables, so there is nothing of Identity's to "remap"; the `identity` tables are exactly spec §7's).

### Verifications left to implementation, each with a step and a fallback

| # | VERIFY | Step | If it fails |
|---|---|---|---|
| V1 | `AddIdentityCore` registers no scheme **on the pinned `Microsoft.Extensions.Identity.Core` 10.0.12** (O6-a ran on the 10.0.10 shared framework) | 1 (test `AddInfrastructure_RegistersNoAuthenticationScheme`) | Stop: D1's premise fails; return to hein |
| V2 | With the explicit `Microsoft.Extensions.Identity.Core` 10.0.12 reference, the loaded assembly is 10.0.12 even where the installed runtime is older (app-local higher version wins) | 1 (test asserts `typeof(UserManager<>).Assembly` version) | Record the actual version in `progress.md`; the risk becomes R-9's |
| V3 | A `TokenValidationParameters.LifetimeValidator` driven by `TimeProvider` is honoured by `JwtBearerHandler` (so S13's fake-clock expiry works) | 7 | Validate lifetime in `OnTokenValidated` against `TimeProvider` and fail there; same observable behaviour |
| V4 | EF emits `WHERE [ReplacedByTokenId] IS NULL` for a nullable concurrency token and throws `DbUpdateConcurrencyException` on zero rows | 3 | Conditional `ExecuteUpdateAsync` inside the handler's transaction, rows-affected checked |
| V5 | `ycr_app` may call `sp_getapplock` (public-role default) | 4 | Grant `EXECUTE` on it explicitly in `Security_IdentityGrants`, recorded in the migration's reasons |
| V6 | ArchUnitNET 0.13.4 can assert "only `LoginHandler` calls `IAuditWriter.RecordSignIn`" | 5 | A reflection-over-IL-free fallback: a test that scans `src/` source text for `RecordSignIn(` outside `LoginHandler.cs`, with its own violating fixture |
| V7 | Column-level `UPDATE` grants on `identity.Users` survive EF updates (EF `SET`s only modified columns; `rowversion` is server-generated) | 3 | Same as F-001's `IsActive` grant pattern; if EF sets more, fix the mapping, never widen the grant silently |
| V8 | A `WebApplicationFactory` client with base address `https://localhost` stores and returns a `Secure; SameSite=Strict; Path=/api/v1/auth/refresh` cookie | 8 | Tests read `Set-Cookie` and send `Cookie` explicitly; the attribute assertions are unchanged |

**ASSUMPTION — none.** Every business rule used is a spec rule (R1–R27) or a provisional tech-lead ruling already labelled in the spec.

**OPEN QUESTION — none new in `docs/19`.** G1 and G2 below are spec gaps for hein, not Myanma Railways questions.

---

## Spec gaps (stop — hein to decide)

Found while planning. Neither is patched; the affected plan rows say "**G1**" or "**G2**".

| # | Gap | Where the spec disagrees or is silent | Options | Blocks |
|---|---|---|---|---|
| **G1** | **Audit subject of `Identity.RefreshFamilyRevoked`.** | R12, S27 and ADR-0023 item 6 (U4): "subject the **session's user**". Spec §8, first sentence: "`SubjectType` is the user (or the **session** for `SessionRevoked` and `RefreshFamilyRevoked` — PLAN fixes the subject constants)". The same §8 bullet then repeats U4's "subject the session's user". The ledger is append-only, so the choice is permanent. | (a) Subject = user (`Identity.User`, user id), session id and reason in `AfterJson` — follows U4, the later ruling. (b) Subject = session (`Identity.AuthSession`), user id in `AfterJson` — follows §8's first sentence and matches `SessionRevoked`. | Audit-subject table (§Audit); step 5 (`RefreshSessionHandler`), S10/S27/S29 assertions |
| **G2** | **Disabling a disabled account, enabling an active one.** | §6.2 and S17 define `disable`/`enable` only from the opposite state; §5's transitions have no row for either repeat. hein's gap-fill for *unlock* (a no-op `204`, no audit row) covers unlock only. | (a) No-op `204`, no audit row, no session change (mirrors the accepted unlock rule). (b) `422 Identity.UserAlreadyDisabled` / `Identity.UserAlreadyEnabled`. | `StaffUser.Disable/Enable` return shape; step 6 handlers; S17 tests |

---

## Decisions (P1–P15) — made by this plan, for hein to confirm

ENGINEERING DECISION (plan, T-024) unless stated; each cites the ruling it implements.

| # | Decision | Why | Rejected alternative |
|---|---|---|---|
| **P1** | **Identity store: a hand-written `StaffUserStore : IUserPasswordStore<StaffUser>, IUserSecurityStampStore<StaffUser>` over the `StaffUser` domain aggregate**, registered with `AddIdentityCore<StaffUser>().AddUserStore<StaffUserStore>()`. `UserManager` provides hashing (with rehash-on-verify), the password-validator pipeline and name normalisation. No `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, no `IdentityUser`, no Identity-owned tables; the `identity` tables are exactly spec §7's. Implements D1 (ADR-0023 item 1). | `YCR.Domain` references nothing (ADR-0004), so the aggregate cannot derive from `IdentityUser`; O6-c shows the EF store over-reports capabilities and brings its own lockout columns and clock. 16 small members vs. a second persistence model. | `UserOnlyStore<IdentityUser-derived>`: a second user model in Infrastructure mirroring the aggregate, `Supports*` lies (O6-c), a new package |
| **P2** | **Lockout lives in the domain, on `TimeProvider`.** `StaffUser.RecordFailedSignIn(nowUtc)` counts and locks at 10 for 15 minutes (constants in `AccountLockoutPolicy`, ADR-0023 item 3); `StaffUserStore` does **not** implement `IUserLockoutStore`, so `UserManager` never touches lockout. | ADR-0018 / `docs/20` §8: time from `TimeProvider`; S4's "after 15 minutes" needs a controllable clock; AGENTS.md rule 3 wants the rule in the domain with domain tests. | Identity's lockout (own clock, `DateTimeOffset.UtcNow`) |
| **P3** | **Per-request resolution (spec O3/O4).** `JwtBearerOptions.Events.OnTokenValidated` reads `sub`/`sid` (with `MapInboundClaims = false`), calls `ResolveSessionPrincipalHandler` through `SessionPrincipalCache`, and **replaces `context.Principal` with a new server-built identity** carrying `ClaimTypes.NameIdentifier` (user id), one `ClaimTypes.Role` per role (canonical identifiers), one `permission` claim per permission (union over roles), and a `ycr:must_change_password` claim when set. No claim from the token survives except `sid` (copied for logout). Session unknown/revoked/past `ExpiresAtUtc`, `sub`≠session user, or user disabled → `context.Fail(...)` → `401 Auth.Unauthenticated`. The framework handler is used as is — D15 holds. | Puts **both** permissions and roles on the principal, so `PermissionAuthorizationHandler` and `HttpContextCurrentUser` stay unchanged and `ActorRole` is populated (ADR-0021). Replacing the principal means nothing a token could carry can grant anything. | A custom `IClaimsTransformation` (runs for every scheme incl. the test handler; harder to reason about); changing the permission handler to query the DB (spreads resolution) |
| **P4** | **Revocation bound (R3).** `SessionPrincipalCache` holds `(principal data, loadedAtUtc)` in `IMemoryCache` keyed by `sid`. An entry is used only while `TimeProvider.GetUtcNow() − loadedAtUtc < Auth:PrincipalCacheSeconds`; after that it is reloaded. Default **15 s**; options validation **fails startup** unless `0 < value ≤ 30`. Session `ExpiresAtUtc` is re-checked against `TimeProvider` on every use, cached or not. `IMemoryCache` expiry (real time, TTL + 5 s) only bounds memory. Worst-case latency = the configured TTL ≤ 30 s, per instance, independent of instance count. Metric `ycr.auth.principal_cache.entry_age` (histogram, seconds, recorded on every cache hit) is `docs/17`'s "session/permission cache revocation latency". | Deterministic under a fake clock, so the ≤30 s bound is **tested**, not asserted: S10, S15–S19 advance the clock by exactly the configured TTL (30 s in those tests) and expect `401`/`403`; a startup test proves 31 s is refused. | Cross-instance invalidation (needs a bus; hosting undecided); TTL 30 s default (leaves no margin for clock drift between cache fill and check) |
| **P5** | **Audit actor for events with no bearer principal (R12/U4).** `IAuditWriter` gains two methods: `RecordWithoutActor(...)` — `ActorUserId`, `ActorRole`, `AuthorizedByPermission` forced null, `ClientIp`/`CorrelationId` from `ICurrentUser` — used for `LoginFailed`, `LockedOut`, `RefreshFamilyRevoked` and the bootstrap `UserCreated`; and `RecordSignIn(StaffUser verifiedUser, IReadOnlyCollection<string> roles, ...)` — actor = the user whose password was just verified — used **only** by `LoginHandler`, enforced by an architecture test with a violating fixture (V6). Every other event uses the existing `Record`. | A null actor cannot forge anyone, so `RecordWithoutActor` is safe anywhere. The one actor-naming path is confined to the one handler that has just verified credentials, and the build fails if that spreads. Ignoring the ambient principal is what makes S27's "a login request whose headers name another user cannot change any of these" true. | A settable "current actor" service (any handler could call it); reading the ambient principal (a bearer header on `/auth/login` would be recorded as the actor) |
| **P6** | **Rate limiting (D5, U6)** uses `System.Threading.RateLimiting` partitioned **sliding-window** limiters (window 1 min, 6 segments) held as singletons and applied by an endpoint filter, `AuthRateLimitFilter`, **after** model binding — so the login's username partition can be read from the bound body. Login: per-normalised-username (5/min) and per-client-address (20/min); refresh: per-client-address (30/min); all from `Auth:RateLimits:*`. Order on every cookie endpoint: `OriginCheckFilter` → `AuthRateLimitFilter` → validation → handler, so an Origin or limit rejection evaluates no credential, writes no audit row and counts no failure (hein's accepted choice at `43ed652`). Rejection: `429` ProblemDetails `Auth.TooManyRequests`. | The ASP.NET Core `RateLimiter` middleware's partitioner is synchronous and runs before model binding; it cannot read a JSON body without buffering tricks. Same primitives, same in-process semantics as D5. | Middleware with a buffered-body partitioner (fragile, reads untrusted bodies before validation); fixed windows (allow 2× bursts across a boundary, weaker than "N per minute") |
| **P7** | **D18 — retention and grants.** F-002 **deletes nothing**: `AuthSessions` and `RefreshTokens` rows are retained; `ycr_app` has **no `DELETE`** on either. Growth estimate: ≤48 refresh tokens per 12 h session (15-min access tokens); 500 staff × 2 sessions/day ≈ 48 k token rows/day worst case, ≈ 17 M/year, ~100 bytes each. Acceptable until production; a follow-up task (proposed: retention job in `YCR.Worker` under a separate credential, retention period decided with OQ14 in mind because `SessionRevoked`/`RefreshFamilyRevoked` audit rows reference session ids) must land **before production**. Role replacement **deletes** `UserRoles` rows (the only `DELETE` grant in `identity`); role history is in the `RolesChanged` audit rows. Full grant set in §DB changes. | No retention period is decided anywhere, and a deletion job is irreversible; the audit ledger keeps the history either way. | Worker purge now (invents a retention period); end-dated `UserRoles` rows (duplicates what audit already records) |
| **P8** | **Bootstrap (D10) is a `YCR.Worker` subcommand**: `dotnet YCR.Worker.dll bootstrap-administrator --username <name>`. It reads the initial password from **standard input** (never argv, never an environment variable), runs `BootstrapAdministratorHandler`, prints nothing secret, and exits `0`/non-zero **without starting the host**. Actor null via `SystemCurrentUser` (`ClientIp` null, `CorrelationId` a fresh activity id). Credential: `ConnectionStrings:Application` (`ycr_app` needs only `INSERT` on `Users`/`UserRoles` and on the ledger). | `YCR.Worker` already exists, composes `Application` + `Infrastructure`, and is deployed with the same configuration; no new project or `docs/06` change. `stdin` keeps the password out of process listings and shell history. | A new `src/YCR.Cli` project (new deployable, `docs/06` change); an API endpoint (an anonymous privileged endpoint in the web artifact) |
| **P9** | **Password blocklist (D2)**: an embedded resource `src/YCR.Infrastructure/Identity/common-passwords.txt`, derived from SecLists `Passwords/Common-Credentials/10-million-password-list-top-1000000.txt` (MIT), **filtered to entries of 12–128 characters, lower-cased, de-duplicated** (shorter entries are already rejected by length). The source URL, commit, filter command and the file's SHA-256 are recorded in `THIRD-PARTY-NOTICES.md` and asserted by a test. Comparison is case-insensitive (ordinal on the lower-cased password). **Not a NuGet package.** | Offline, reproducible, reviewable, licensed; D2 forbids an external service. | A NuGet blocklist package (none maintained that we could find; would need its own §8 justification) |
| **P10** | **Security headers (R24, S26c)** on every response by `SecurityHeadersMiddleware` (first in the pipeline, so exception and status-code responses get them): `X-Content-Type-Options: nosniff`; `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'`; `Referrer-Policy: no-referrer`; `X-Frame-Options: DENY`. Plus `Cache-Control: no-store` on every `/api/v1/auth/*` response (tokens in bodies). In **Development only**, `/scalar/*` is exempt from the CSP (the UI needs scripts). No HSTS (TLS termination is a hosting decision, `docs/15`). **No `AddCors`** anywhere (S26b). | Stricter than ADR-0016's SPA CSP, which is correct for JSON responses; the SPA's own CSP is R25b's gate. | HSTS in the app before hosting is known |
| **P11** | **Token time.** `iat`/`nbf`/`exp` come from `TimeProvider`; validation uses a `TimeProvider`-driven `LifetimeValidator` (V3), `ClockSkew = 30 s`, `ValidAlgorithms = [ES256]`, `ValidateIssuer/Audience/IssuerSigningKey = true`, `RequireExpirationTime = true`, `IncludeErrorDetails = false` (no validation reason in `WWW-Authenticate`). `iss` and `aud` from `Auth:Issuer`/`Auth:Audience`. | The framework validates against wall-clock time otherwise, which makes S13 untestable and breaks every fake-clock test once the clock moves. 30 s skew tolerates instance clock drift; the 5-min default would stretch a 15-min token to 20. | Default skew |
| **P12** | **Password length** is counted in **Unicode scalar values** (`string.EnumerateRunes().Count()`), no normalisation (the hasher hashes the UTF-8 of the string as given, as Identity does). | "Characters" in D2 should not let a Myanmar or emoji password count double through UTF-16 surrogates. | UTF-16 `Length`; NFKC normalisation (changes what the user typed; no ruling) |
| **P13** | **Pin `Microsoft.Extensions.Identity.Core` 10.0.12 explicitly** in `YCR.Infrastructure` (V2). | A class library cannot see the shared framework without a `FrameworkReference`; the explicit package also stops the Identity version following whatever runtime is installed (O6 ran on 10.0.10). | `FrameworkReference Microsoft.AspNetCore.App` in Infrastructure (drags the whole web framework into the layer the Worker also loads) |
| **P14** | **R27 under concurrency (S19e).** `DisableUserHandler`, `ReplaceUserRolesHandler` and `BootstrapAdministratorHandler` take an **exclusive application lock** — `sp_getapplock @Resource = N'identity.SystemAdministrators', @LockOwner = 'Transaction'` — through `IIdentityAdministratorLock` (Application abstraction, SQL Server implementation), inside an explicit transaction, **before** counting the other active `SystemAdministrator`s; then change, audit, `SaveChangesAsync`, commit. The lock is taken only when the operation could reduce the active-administrator set (target holds `SystemAdministrator` and is active, or the bootstrap). This is the one documented exception to ADR-0004's single-save default. | Two administrators disabling each other at once must not both pass a count that each read before the other's write; `SERIALIZABLE` range locks on a join are harder to reason about than one named lock. | Optimistic re-check after save (a window remains); a DB trigger (domain rule in SQL) |
| **P15** | **Refresh rotation race (S8, S33).** `RefreshToken.ReplacedByTokenId` is an **EF concurrency token**: the rotation `UPDATE … WHERE Id = @id AND ReplacedByTokenId IS NULL` (V4) is evaluated by SQL Server under the row lock, so a token can move from "current" to "rotated" exactly once; the successor `INSERT` is in the same `SaveChangesAsync` and rolls back with a losing update. The loser reloads, finds a rotation within the grace window, and returns `409 Auth.RefreshSuperseded`. `AuthSession.RevokedAtUtc` is likewise a concurrency token, so two revocations (e.g. logout racing family reuse) produce one revocation and one audit row. | Database-enforced at-most-one successor without adding a column to spec §7. | A `PredecessorTokenId` column with a unique index (changes the approved data shape) |

---

## Affected modules and files

Modules touched: **`Identity`** (new), `Audit` (writer gains two methods), solution-wide `Common` (API pipeline, permission provider). `Network` is untouched except that its endpoints now authenticate for real.

### Build, configuration and CI

| File | New / Changed | Why |
|---|---|---|
| `Directory.Packages.props` | Changed | Two pins: `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.12, `Microsoft.Extensions.Identity.Core` 10.0.12 (§New packages) |
| `src/YCR.Api/YCR.Api.csproj` | Changed | `JwtBearer` reference; `UserSecretsId` for per-developer signing keys (D11) |
| `src/YCR.Infrastructure/YCR.Infrastructure.csproj` | Changed | `Identity.Core` reference; `common-passwords.txt` as `EmbeddedResource` |
| `src/YCR.Api/appsettings.json` | Changed | Non-secret `Auth` defaults: issuer, audience, lifetimes (15 min / 12 h), grace 20 s, cache 15 s, rate limits (U6), `AllowedOrigins: []`. No key material |
| `src/YCR.Worker/appsettings.json` | New | Logging defaults only; the connection string comes from the environment |
| `.env.example` | Changed | Documents `Auth__AllowedOrigins__0` and that the signing key goes in user-secrets, never in `.env` |
| `README.md` | Changed | Developer setup: generate a P-256 dev key (`openssl ecparam -name prime256v1 -genkey \| openssl pkcs8 -topk8 -nocrypt`), store with `dotnet user-secrets`, mark `DevelopmentOnly: true`; bootstrap command usage |
| `THIRD-PARTY-NOTICES.md` | New | SecLists MIT notice and provenance of the blocklist (P9) |
| `.gitleaks.toml` | Changed | Rule for PEM private keys (`-----BEGIN PRIVATE KEY-----`) if the default ruleset does not already flag it; verified in step 12 |
| `.github/workflows/ci.yml` | Changed | Smoke job: generate an ephemeral ES256 key per run (masked), set `Auth__AllowedOrigins__0`; expect `Auth.Unauthenticated` and `WWW-Authenticate: Bearer`; add header checks (S26c) |

### `src/YCR.Domain/Identity/` (all new)

| File | Why |
|---|---|
| `StaffUser.cs` | Aggregate: `Create`, `RecordFailedSignIn`, `IsLockedOut`, `RecordSuccessfulSignIn`, `Unlock`, `Disable`, `Enable` (**G2**), `ReplaceRoles`, `SetPasswordByAdministrator`, `ChangeOwnPassword`, `ApplyPasswordHash`/`ApplySecurityStamp` (store only). Self-target rules (R20/U5) live here |
| `UserName.cs` | Value object, R20: 3–50 of `a-z`, `0-9`, `.`; `Create` → `Result<UserName>`; `From` for EF |
| `UserRole.cs` | `(UserId, RoleId)` child of `StaffUser` |
| `Role.cs`, `RolePermission.cs` | Read-only catalogue entities (seeded by migration, never written by the app) |
| `RoleNames.cs` | The eight canonical identifiers (R19) — used for R27 and validation, never for authorization (R10) |
| `AccountLockoutPolicy.cs` | `MaxFailedAttempts = 10`, `LockoutDuration = 15 min` (ADR-0023 item 3) |
| `AuthSession.cs` | Aggregate = one refresh family (D12): `Start`, `Rotate` → `RefreshOutcome`, `Revoke`, `IsActive(now)` |
| `RefreshToken.cs` | Child entity: `Id`, `TokenHash`, `IssuedAtUtc`, `RotatedAtUtc`, `ReplacedByTokenId` |
| `RefreshOutcome.cs` | `Rotated` / `Superseded` / `FamilyReused` / `Invalid` |
| `RevocationReason.cs` | Enum stored as string: `Logout`, `PasswordChanged`, `AdministratorPasswordReset`, `UserDisabled`, `AdministratorRevoked`, `FamilyReuse` (spec §7) |
| `IdentityErrors.cs` | All `Auth.*` and `Identity.*` error codes (list in §API changes) |

### `src/YCR.Application/`

| File | New / Changed | Why |
|---|---|---|
| `Common/Abstractions/IAuditWriter.cs` | Changed | `RecordWithoutActor`, `RecordSignIn` (P5) |
| `Common/Authorization/Permissions.cs` | Changed | `UsersRead`, `UsersManage`, `UsersRolesManage`, `AuthSessionsRevoke` (D8); stale "no grant is seeded" comment corrected |
| `Identity/IIdentityDbContext.cs` | New | ADR-0012 item 2: `Users`, `Roles`, `AuthSessions`, `SaveChangesAsync`, `Database` transaction access for P14 |
| `Identity/IdentityAuditSubjects.cs` | New | `Identity.User`, `Identity.AuthSession` (**G1** decides which one `RefreshFamilyRevoked` uses) |
| `Identity/UserAuditSnapshot.cs`, `UserRolesAuditSnapshot.cs`, `AuthSessionAuditSnapshot.cs` | New | ADR-0021 payloads: username, roles, disabled, locked-until, must-change; roles array; session id, user id, reason. Never hashes, stamps or tokens (R13) |
| `Identity/Abstractions/IPasswordService.cs` | New | `VerifyAsync(StaffUser? user, password)` (dummy hash when null — R23), `SetPasswordAsync(user, password)` → policy result |
| `Identity/Abstractions/IAccessTokenIssuer.cs` | New | `Issue(userId, sessionId)` → token + `expiresAtUtc` |
| `Identity/Abstractions/IRefreshTokenGenerator.cs` | New | 32 random bytes → raw (for the cookie) + SHA-256 hash (for storage) |
| `Identity/Abstractions/IIdentityAdministratorLock.cs` | New | P14 |
| `Identity/AuthSettings.cs` | New | Session lifetime, grace window, principal-cache TTL (bound from `Auth`) |
| `Identity/IdentityTelemetry.cs` | New | `Meter "YCR.Identity"` counters for `AuthLoginSucceeded`, `AuthLoginFailed`, `AuthRefreshSuperseded`, `AuthRefreshFamilyRevoked` (log/metric names, D6), the cache-age histogram (P4), and `LoggerMessage` event ids — no usernames |
| `Identity/Login/LoginCommand.cs`, `LoginHandler.cs`, `SignInResult.cs` | New | R1, R2, R12, R18, R22, R23 |
| `Identity/RefreshSession/RefreshSessionCommand.cs`, `RefreshSessionHandler.cs` | New | R5–R8 |
| `Identity/Logout/LogoutCommand.cs`, `LogoutHandler.cs` | New | R4, C6 |
| `Identity/ChangeOwnPassword/ChangeOwnPasswordCommand.cs`, `ChangeOwnPasswordHandler.cs` | New | R4, R18, R26, U1 |
| `Identity/GetCurrentUser/GetCurrentUserQuery.cs`, `GetCurrentUserHandler.cs`, `CurrentUserDto.cs` | New | `/auth/me` |
| `Identity/ResolveSessionPrincipal/ResolveSessionPrincipalQuery.cs`, `ResolveSessionPrincipalHandler.cs`, `SessionPrincipal.cs` | New | P3 — one query, `AsNoTracking`, session + user + roles + permissions |
| `Identity/ListUsers/…`, `GetUser/…` (`UserDto`, `UserSummaryDto`) | New | §6.2 reads |
| `Identity/CreateUser/…`, `DisableUser/…`, `EnableUser/…`, `UnlockUser/…`, `ReplaceUserRoles/…`, `ResetUserPassword/…` | New | §6.2 writes (R20, R26, R27) |
| `Identity/ListUserSessions/…`, `RevokeSession/…`, `ListRoles/…` | New | §6.2 sessions and catalogue |
| `Identity/BootstrapAdministrator/BootstrapAdministratorCommand.cs`, `BootstrapAdministratorHandler.cs` | New | D10; shared by the CLI and S25 |

### `src/YCR.Infrastructure/`

| File | New / Changed | Why |
|---|---|---|
| `Persistence/YcrDbContext.cs` | Changed | Implements `IIdentityDbContext` |
| `Persistence/Configurations/Identity/StaffUserConfiguration.cs`, `UserRoleConfiguration.cs`, `RoleConfiguration.cs`, `RolePermissionConfiguration.cs`, `AuthSessionConfiguration.cs`, `RefreshTokenConfiguration.cs` | New | Spec §7 mapping, check constraints, concurrency tokens (P15) |
| `Persistence/Migrations/<ts>_Identity_CreateIdentitySchema.cs` | New | §DB changes |
| `Persistence/Migrations/<ts>_Identity_SeedRolesAndPermissionGrants.cs` | New | ″ (D8, R11) |
| `Persistence/Migrations/<ts>_Security_IdentityGrants.cs` | New | ″ (P7) |
| `Identity/StaffUserStore.cs` | New | P1 |
| `Identity/PasswordService.cs` | New | `UserManager<StaffUser>` wrapper; precomputed dummy hash for the unknown-user path (R23) |
| `Identity/PasswordPolicyValidator.cs` | New | `IPasswordValidator<StaffUser>`: 12–128 scalar values (P12), blocklist (P9). Identity's default composition validators are **not** registered |
| `Identity/CommonPasswordBlocklist.cs`, `Identity/common-passwords.txt` | New | P9 |
| `Identity/RefreshTokenGenerator.cs` | New | `RandomNumberGenerator` + SHA-256 |
| `Identity/SqlServerIdentityAdministratorLock.cs` | New | P14 (`sp_getapplock`) |
| `Audit/AuditWriter.cs` | Changed | P5's two methods |
| `DependencyInjection.cs` | Changed | `IIdentityDbContext`; `AddIdentityCore<StaffUser>(o => …)` with `User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyz0123456789."`, `RequireUniqueEmail = false`, the default password validators removed; store, services |

### `src/YCR.Api/`

| File | New / Changed | Why |
|---|---|---|
| `Program.cs` | Changed | Pipeline order: `SecurityHeadersMiddleware` → exception handler → status pages → `UseAuthentication` → `PasswordChangeRequiredMiddleware` → `UseAuthorization`; `AddJwtBearer`; options validation; auth endpoints; no `AddCors` |
| `Common/Authentication/AuthenticationSchemeGuard.cs` | Changed | Allowlist = `{ typeof(JwtBearerHandler) }` (ADR-0020 item 5 as amended) |
| `Common/Authentication/JwtBearerSetup.cs` | New | `IConfigureNamedOptions<JwtBearerOptions>`: P3, P11, and `OnChallenge` writing ProblemDetails `Auth.Unauthenticated` with `WWW-Authenticate: Bearer` (D14) |
| `Common/Authentication/SessionPrincipalCache.cs` | New | P4 |
| `Common/Authentication/SigningKeyProvider.cs`, `ConfigurationSigningKeyProvider.cs`, `SigningKeyOptions.cs` | New | D11: `Auth:Signing:ActiveKeyId`, `Auth:Signing:Keys[]` `{ KeyId, PrivateKeyPkcs8Pem, DevelopmentOnly }`; outside Development/Testing, startup **throws** if any key is `DevelopmentOnly`, has a `kid` starting `dev-`/`test-`, is not P-256, or no active key exists (S26a) |
| `Common/Authentication/JwtAccessTokenIssuer.cs` | New | `IAccessTokenIssuer` with `JsonWebTokenHandler` (in the Api to reuse JwtBearer's transitive IdentityModel instead of adding a package to Infrastructure) |
| `Common/Authentication/AuthOptions.cs`, `AuthOptionsValidator.cs` | New | Binding + validation: cache TTL ≤ 30 s (P4); non-empty absolute `https` origins outside Development/Testing; positive limits |
| `Common/Authentication/RefreshCookie.cs` | New | Name `ycr_refresh`, `HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth/refresh`, `Expires` = session `ExpiresAtUtc`; expiry `Set-Cookie` for logout |
| `Common/Authentication/OriginCheckFilter.cs` | New | R9; `403 Auth.OriginRejected`; exact scheme+host+port match against `Auth:AllowedOrigins` |
| `Common/Authentication/AuthRateLimitFilter.cs`, `AuthRateLimiters.cs` | New | P6 |
| `Common/Authentication/PasswordChangeRequiredMiddleware.cs`, `AllowedWhilePasswordChangeRequiredAttribute.cs` | New | R26 (see §Security impact for scope) |
| `Common/SecurityHeadersMiddleware.cs` | New | P10 |
| `Common/Authorization/AuthorizationResultHandler.cs` | **Deleted** | D14 |
| `Common/Authorization/PermissionPolicyProvider.cs` | Changed | Pattern: two or more `.`-separated segments of `[a-z]+(-[a-z]+)*` |
| `Common/Authorization/PermissionAuthorizationHandler.cs` | Changed | Doc comment only: permissions now come from P3's server-built principal |
| `Common/HttpContextCurrentUser.cs` | Changed | Doc comment only (the claims it reads are now server-built) |
| `Contracts/Identity/LoginRequest.cs` (+`Validator`), `AccessTokenResponse.cs`, `CurrentUserResponse.cs`, `ChangePasswordRequest.cs` (+`Validator`), `CreateUserRequest.cs` (+`Validator`), `CreateUserResponse.cs`, `UserResponse.cs`, `UserSummaryResponse.cs`, `ReplaceUserRolesRequest.cs` (+`Validator`), `ResetUserPasswordRequest.cs` (+`Validator`), `AuthSessionResponse.cs`, `RoleResponse.cs` | New | `docs/20` §2 naming; validators check role names against `RoleNames.All` (unknown role → `400 Common.ValidationFailed`, spec §6) |
| `Endpoints/Identity/AuthEndpoints.cs` | New | §6.1 |
| `Endpoints/Identity/UserEndpoints.cs`, `AuthSessionEndpoints.cs`, `RoleEndpoints.cs` | New | §6.2 |
| `YCR.Api.http` | Changed | Login/refresh/me examples |

### `src/YCR.Worker/`

| File | New / Changed | Why |
|---|---|---|
| `Program.cs` | Changed | `bootstrap-administrator` dispatch before the host starts (P8) |
| `BootstrapAdministratorCommand.cs` | New | Argument parsing, stdin read, exit codes; testable entry `RunAsync(args, stdin, stdout, services)` |
| `SystemCurrentUser.cs` | New | `ICurrentUser` for a process with no request (all actor fields null) |

### `tests/` — see §Test plan for the test names

| File | New / Changed |
|---|---|
| `tests/YCR.TestSupport/TestClock.cs` | New — a settable `TimeProvider` (no package) |
| `tests/YCR.TestSupport/TestSigningKey.cs` | New — ephemeral P-256 key per run, as configuration values |
| `tests/YCR.Domain.Tests/Identity/UserNameTests.cs`, `StaffUserTests.cs`, `AuthSessionTests.cs`, `RoleNamesTests.cs` | New |
| `tests/YCR.Application.Tests/Identity/IdentityHandlerTestBase.cs` + one `*HandlerTests.cs` per use case (18 files) | New |
| `tests/YCR.Infrastructure.Tests/Identity/IdentityMigrationTests.cs`, `IdentitySeedTests.cs`, `IdentityConstraintTests.cs`, `PasswordPolicyValidatorTests.cs`, `CommonPasswordBlocklistTests.cs`, `PasswordServiceTests.cs`, `IdentityCoreRegistrationTests.cs`, `RefreshTokenGeneratorTests.cs` | New |
| `tests/YCR.Infrastructure.Tests/Persistence/DatabasePrivilegeTests.cs`, `ModuleInterfacesTests.cs`, `tests/YCR.Infrastructure.Tests/Audit/AuditWriterTests.cs` (new if absent) | Changed / New |
| `tests/YCR.Api.Tests/Authentication/YcrApiFactory.cs` | Changed — `AuthMode.TestHandler` (default, F-001 tests unchanged) or `AuthMode.RealTokens`; test signing key, allowed origin, `TestClock` and an `https://localhost` base address; a test-only `IStartupFilter` setting `RemoteIpAddress` from a header for S5 |
| `tests/YCR.Api.Tests/Authentication/RealAuthApiTestBase.cs`, `StaffUserSeeder.cs` | New — creates users through the handlers, signs in for real |
| `tests/YCR.Api.Tests/Common/MissingSchemeTests.cs` | **Replaced** by `DeployedShapeTests.cs` (D14: premise rewritten, not deleted — every old case has a successor, listed in §Test plan) |
| `tests/YCR.Api.Tests/Common/AuthenticationSchemeGuardTests.cs` | Changed (S23) |
| `tests/YCR.Api.Tests/Identity/*.cs` (13 files, §Test plan) | New |
| `tests/YCR.IntegrationTests/Bootstrap/BootstrapAdministratorCommandTests.cs`, `SqlServerFixture.cs`; `YCR.IntegrationTests.csproj` | New / Changed — first use of the project (references Worker and TestSupport) |
| `tests/YCR.ArchitectureTests/ArchitectureRuleTests.cs` | Changed — Worker assembly added to the S21a scan; new rules below |
| `tests/YCR.ArchitectureTests/Violations/RecordSignInOutsideLoginHandler.cs`, `IdentityUsingNetworkContext.cs`, `ApplicationUsingAspNetIdentity.cs` | New fixtures |

**Documentation (stage 8, not stage 4):** `docs/07` (identity tables, constraints, indexes, grants), `docs/08` (endpoints), `docs/09` (controls now implemented), `docs/17` (metric/log names), `docs/15` (deployment needs: signing key, allowed origins, bootstrap), `docs/20` §3 note on the `Identity` slice and `IAuditWriter`'s new methods, `docs/glossary.md` role labels (if hein puts it in scope), README.

---

## Domain changes

### `StaffUser` (aggregate, `rowversion`)

State: `Id`, `UserName`, `NormalizedUserName`, `PasswordHash`, `SecurityStamp`, `IsDisabled`, `DisabledAtUtc`, `LockoutEndUtc`, `AccessFailedCount`, `PasswordChangedAtUtc`, `MustChangePassword`, `CreatedAtUtc`, `Roles` (`UserRole` children).

| Method | Rule | Error |
|---|---|---|
| `Create(id, userName, nowUtc)` | UTC only; no roles; `MustChangePassword = true` until a password is applied by one of the two setters | — |
| `RecordFailedSignIn(nowUtc)` → `LockedOut: bool` | While locked: no change (the lockout is not extended; hein's S4 wording). Else count +1; at 10 → `LockoutEndUtc = now + 15 min`, count 0, returns `true` | — |
| `IsLockedOut(nowUtc)` | `LockoutEndUtc > now` | — |
| `RecordSuccessfulSignIn()` | count 0 | — |
| `Unlock()` → `changed: bool` | Clears `LockoutEndUtc` and count; `false` if not locked (no audit — hein's gap-fill) | — |
| `Disable(callerId, nowUtc)` / `Enable()` | Self-target refused; repeat behaviour **G2** | `Identity.CannotDisableOwnAccount` |
| `ReplaceRoles(roleIds, callerId)` | Self-target refused | `Identity.CannotChangeOwnRoles` |
| `SetPasswordByAdministrator(callerId, nowUtc)` | Self-target refused; `MustChangePassword = true` (U2) | `Identity.CannotResetOwnPassword` |
| `ChangeOwnPassword(nowUtc)` | `MustChangePassword = false`, `PasswordChangedAtUtc = now` | — |

R27 (last active `SystemAdministrator`) is **not** in the aggregate — it is a set invariant across users, so it lives in the handlers under P14's lock and is tested there (S19e).

### `AuthSession` (aggregate) and `RefreshToken`

`Start(id, userId, firstTokenId, tokenHash, nowUtc, lifetime)` → `ExpiresAtUtc = now + 12 h`. `Rotate(presentedHash, newTokenId, newHash, nowUtc, grace)`:

1. Session revoked or `now ≥ ExpiresAtUtc`, or hash not in this family → `Invalid`.
2. Presented token current (not rotated) → mark rotated, add successor → `Rotated`.
3. Presented token's successor is current **and** `now − RotatedAtUtc ≤ grace` → `Superseded` (no change).
4. Otherwise (successor also rotated = older ancestor, or beyond grace) → `Revoke(FamilyReuse)` → `FamilyReused`.

`Revoke(reason, nowUtc)` → `changed: bool` (already revoked → `false`, no second audit). `IsActive(nowUtc)`.

The refresh handler looks the token up by hash (unique index), loads its session **with the family** (≤48 rows) and calls `Rotate`; the domain decides, the handler persists. Domain tests cover every branch with no database.

---

## DB changes

Three migrations, applied by the migration bundle under `ycr_migrator` (F-001 P1), reviewed per `docs/workflows/04-database-change.md`. On-disk names are `yyyyMMddHHmmss_<Module>_<Change>` (`docs/20` §6). Upgrade path: all three are additive on F-001's schema; tested by migrating a database already at F-001's last migration (`docs/21` §Data).

### `<ts>_Identity_CreateIdentitySchema` (EF model-built; `Down()` drops the tables and schema)

| Table | Columns (all `*Utc` `datetimeoffset(3)`) | Constraints and indexes |
|---|---|---|
| `identity.Users` | `Id uniqueidentifier` PK · `UserName nvarchar(50)` · `NormalizedUserName nvarchar(50)` · `PasswordHash nvarchar(256)` · `SecurityStamp nvarchar(64)` · `IsDisabled bit` · `DisabledAtUtc` null · `LockoutEndUtc` null · `AccessFailedCount int` · `PasswordChangedAtUtc` · `MustChangePassword bit` · `CreatedAtUtc` · `RowVersion rowversion` — all not null unless stated | `UX_Users_NormalizedUserName` unique · `CK_Users_UserName_Format`: `LEN([UserName]) BETWEEN 3 AND 50 AND [UserName] NOT LIKE '%[^a-z0-9.]%' COLLATE Latin1_General_100_BIN2` (binary collation, so upper case is refused whatever the database collation) · `CK_Users_Disabled_Consistent`: `IsDisabled` ⇔ `DisabledAtUtc` not null · `CK_Users_AccessFailedCount`: `BETWEEN 0 AND 10` · one `CK_Users_<Col>_Utc` per UTC column (`DATEPART(TZOFFSET, …) = 0`) |
| `identity.Roles` | `Id` PK · `Name nvarchar(50)` | `UX_Roles_Name` |
| `identity.UserRoles` | `UserId`, `RoleId` | PK `(UserId, RoleId)`; FKs (no cascade); `IX_UserRoles_RoleId` (the R27 count) |
| `identity.RolePermissions` | `RoleId`, `Permission nvarchar(100)` | PK `(RoleId, Permission)`; FK; `CK_RolePermissions_Permission_Format`: `NOT LIKE '%[^a-z.-]%' COLLATE Latin1_General_100_BIN2` |
| `identity.AuthSessions` | `Id` PK · `UserId` · `CreatedAtUtc` · `ExpiresAtUtc` · `RevokedAtUtc` null · `RevocationReason nvarchar(40)` null | FK Users; `IX_AuthSessions_UserId`; `CK_AuthSessions_Revocation_Consistent` (both null or both set); `CK_AuthSessions_RevocationReason` (the six values); `CK_AuthSessions_Expiry` (`ExpiresAtUtc > CreatedAtUtc`); UTC checks |
| `identity.RefreshTokens` | `Id` PK · `SessionId` · `TokenHash binary(32)` · `IssuedAtUtc` · `RotatedAtUtc` null · `ReplacedByTokenId` null | FK AuthSessions; self-FK on `ReplacedByTokenId`; `UX_RefreshTokens_TokenHash`; `UX_RefreshTokens_ReplacedByTokenId` filtered `WHERE ReplacedByTokenId IS NOT NULL` (spec §7, S33); `CK_RefreshTokens_Rotation_Consistent` (both null or both set); `CK_RefreshTokens_NotSelf` (`ReplacedByTokenId <> Id`); `IX_RefreshTokens_SessionId`; UTC checks |

Every FK is `NO ACTION`: nothing in `identity` is deleted except `UserRoles` rows. Data impact: none (new tables).

### `<ts>_Identity_SeedRolesAndPermissionGrants` (`Down()` deletes exactly the seeded rows)

Eight `identity.Roles` rows with **fixed GUIDs written in the migration** (ADR-0006 application-assigned; fixed so later migrations can reference them) and the fourteen `identity.RolePermissions` rows of R11. **No user is seeded** (S30). BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-22 T-014; 2026-09-23 T-023) — not a Myanma Railways answer; the migration's header comment says so.

### `<ts>_Security_IdentityGrants` (`Down()` revokes exactly these; safe, no data)

| Object | `ycr_app` gets | Deliberately not |
|---|---|---|
| `identity.Users` | `SELECT`, `INSERT`; `UPDATE` on `PasswordHash`, `SecurityStamp`, `PasswordChangedAtUtc`, `MustChangePassword`, `IsDisabled`, `DisabledAtUtc`, `LockoutEndUtc`, `AccessFailedCount` | `UPDATE` on `Id`, `UserName`, `NormalizedUserName`, `CreatedAtUtc`; `DELETE` |
| `identity.Roles`, `identity.RolePermissions` | `SELECT` | any write (D8: grants change by migration) |
| `identity.UserRoles` | `SELECT`, `INSERT`, `DELETE` | `UPDATE` (P7) |
| `identity.AuthSessions` | `SELECT`, `INSERT`; `UPDATE` on `RevokedAtUtc`, `RevocationReason` | `DELETE` (D18/P7) |
| `identity.RefreshTokens` | `SELECT`, `INSERT`; `UPDATE` on `RotatedAtUtc`, `ReplacedByTokenId` | `DELETE` (D18/P7) |

No DDL anywhere. `sp_getapplock` relies on the public-role default (V5). `DatabasePrivilegeTests` asserts presences **and** absences, as F-001's S22 does.

---

## API changes

### Endpoint inventory

All under `/api/v1`. Every authenticated endpoint can also return `401 Auth.Unauthenticated` and, while must-change is set, `403 Auth.PasswordChangeRequired` (not the four marked ✓).

| Method | Path | Authorization | Filters | Response / errors |
|---|---|---|---|---|
| POST | `/auth/login` | `.AllowAnonymous()` — no token yet | Origin → rate limit (user + address) → validation | `200 AccessTokenResponse` + cookie · `400 Common.ValidationFailed` · `401 Auth.InvalidCredentials` · `403 Auth.OriginRejected` · `429 Auth.TooManyRequests` |
| POST | `/auth/refresh` ✓ | `.AllowAnonymous()` — authenticated by the cookie inside the endpoint (D15) | Origin → rate limit (address) | `200` + rotated cookie · `401 Auth.RefreshInvalid` · `403 Auth.OriginRejected` · `409 Auth.RefreshSuperseded` · `429 Auth.TooManyRequests` |
| POST | `/auth/logout` ✓ | `.RequireAuthorization()` — self-service (D17) | Origin | `204` + expiring cookie · `403 Auth.OriginRejected` |
| GET | `/auth/me` ✓ | `.RequireAuthorization()` — self-service | — | `200 CurrentUserResponse { userId, userName, roles, permissions }` |
| POST | `/auth/password` ✓ | `.RequireAuthorization()` — self-service | validation | `204` · `400 Auth.PasswordRejected` · `422 Auth.CurrentPasswordIncorrect` |
| GET | `/users`, `/users/{id}` | `users.read` | — | `200` paged / `UserResponse` · `404 Identity.UserNotFound` |
| POST | `/users` | `users.manage` | validation | `201 CreateUserResponse { id }` + `Location` · `400` · `400 Identity.PasswordRejected` · `409 Identity.UserNameAlreadyExists` |
| POST | `/users/{id}/disable` | `users.manage` | — | `204` · `404` · `422 Identity.CannotDisableOwnAccount` · `422 Identity.LastAdministrator` · repeat: **G2** |
| POST | `/users/{id}/enable` | `users.manage` | — | `204` · `404` · repeat: **G2** |
| POST | `/users/{id}/unlock` | `users.manage` | — | `204` (no-op when not locked) · `404` |
| PUT | `/users/{id}/roles` | `users.roles.manage` | validation | `204` · `400` · `404` · `422 Identity.CannotChangeOwnRoles` · `422 Identity.LastAdministrator` |
| POST | `/users/{id}/password-reset` | `users.manage` | validation | `204` · `400 Identity.PasswordRejected` · `404` · `422 Identity.CannotResetOwnPassword` |
| GET | `/users/{id}/auth-sessions` | `users.read` | — | `200` paged `AuthSessionResponse` · `404` |
| POST | `/auth-sessions/{id}/revoke` | `auth-sessions.revoke` | — | `204` (already revoked → `204`, no second audit) · `404 Identity.SessionNotFound` |
| GET | `/roles` | `users.read` | — | `200 RoleResponse[] { name, permissions[] }` |

Idempotency: none of these is in `docs/20` §5's list; R7 handles a benign refresh duplicate. No endpoint returns a hash, stamp or token other than the caller's own new access token (spec §6.2).

**Error codes** (`IdentityErrors`, prefix by path per U1): `Auth.InvalidCredentials` (401), `Auth.RefreshInvalid` (401), `Auth.RefreshSuperseded` (409), `Auth.PasswordRejected` (400), `Auth.CurrentPasswordIncorrect` (422); pipeline/filters: `Auth.Unauthenticated` (401), `Auth.PasswordChangeRequired` (403), `Auth.OriginRejected` (403), `Auth.TooManyRequests` (429); administration: `Identity.UserNotFound`, `Identity.SessionNotFound` (404), `Identity.UserNameAlreadyExists` (409), `Identity.PasswordRejected` (400), `Identity.CannotChangeOwnRoles`, `Identity.CannotDisableOwnAccount`, `Identity.CannotResetOwnPassword`, `Identity.LastAdministrator` (422). A password-policy failure is one shared domain error mapped to the path's prefix by the handler that owns the path.

**Permission-name pattern (FACT found at PLAN, fixed in step 1).** `PermissionPolicyProvider` accepts a name as a permission when it is two or more `.`-separated segments, each `[a-z]+(-[a-z]+)*`. Anything else still falls through to the default provider and fails loudly. `docs/20` §2's `<resource>.<action>` is read with `users.roles` and `auth-sessions` as resources, which is what hein approved in `docs/10`.

---

## Security impact

| Item | Treatment |
|---|---|
| Authentication | ES256 `JwtBearerHandler` only (D11, D13); P3 rebuilds the principal per request; `IncludeErrorDetails = false`; tokens carry exactly eight claims (S20) |
| Authorization | Unchanged model (R10). Four new permissions, `SystemAdministrator` only (D8); `docs/10` already updated at T-023 |
| Must-change gate (R26) | `PasswordChangeRequiredMiddleware` after authentication, before authorization: when the authenticated principal carries the must-change claim and the endpoint **requires authorization** without `[AllowedWhilePasswordChangeRequired]`, it writes `403 Auth.PasswordChangeRequired`. The attribute is on `/auth/me`, `/auth/password`, `/auth/logout`; `/auth/refresh` is anonymous and so never gated. Anonymous endpoints (`/auth/login`, health) are not "authenticated requests" in R26's sense and are unaffected — **hein to confirm this reading** (§Decisions note N1) |
| Revocation latency | P4: ≤ configured TTL ≤ 30 s, startup-enforced, fake-clock tested |
| Refresh | 256-bit random token, SHA-256 at rest (S31), rotation raced safely (P15), grace per R7, family revocation per R8 |
| CSRF / Origin | `OriginCheckFilter` on login, refresh, logout (R9) |
| Brute force | Lockout (P2), per-username and per-address limits (P6); uniform `401` with a full hash verification on every path (R23; PasswordService always verifies — dummy hash for unknown users, real hash for disabled/locked users) |
| Audit integrity | P5; S27 spoofing tests; `ActorRole` = canonical identifiers from P3 |
| Secrets | Signing key only via configuration/user-secrets (D11); production refuses dev keys (S26a); password never in argv/env for the CLI (P8); S28 scans logs, audit rows and bodies |
| Headers / CORS | P10; no `AddCors` |
| DB least privilege | P7 grant table; `DatabasePrivilegeTests` |
| Test handler | Stays `Testing`-only (D13); guard allowlist = `JwtBearerHandler`; S21a scan extended to `YCR.Worker` |

**`docs/18` threats touched:** account takeover, privilege escalation, API abuse, data disclosure, insider manipulation, audit tampering, administrative lock-out (R27), refresh replay. Same-origin XSS stays HIGH and SPA-side (R25b).

**Residual risks accepted by rulings** (not re-litigated): lockout as a DoS lever (ADR-0023), per-instance and per-proxy rate limits (D5), password-only until MFA with a procedural release gate (D4), usernames in audit snapshots (D6).

---

## Test plan

Names follow `docs/20` §2. Stage 5 (a different agent) owns completeness; this is what it checks itself against. "Real" = real ES256 tokens through `JwtBearerHandler`; "TH" = F-001's test handler.

### `YCR.Domain.Tests/Identity`

| Test | Spec |
|---|---|
| `UserName_Create_WithValidName_ReturnsUserName` · `UserName_Create_WithInvalidName_ReturnsValidationError` (theory: 2 and 51 chars, upper case, space, `-`, `@`, `_`, non-ASCII letter and digit) | S32a, R20 |
| `Create_WithUtcTime_ReturnsActiveUserWithNoRoles` · `Create_WithNonUtcTime_Throws` | R20, ADR-0018 |
| `RecordFailedSignIn_NineTimes_DoesNotLock` · `RecordFailedSignIn_TenthTime_LocksForFifteenMinutesAndResetsCount` · `IsLockedOut_AfterFifteenMinutes_ReturnsFalse` · `RecordFailedSignIn_WhileLocked_DoesNotExtendLockout` · `RecordSuccessfulSignIn_ResetsFailedCount` | S4, R18 |
| `Unlock_WhenLocked_ClearsLockoutAndCount` · `Unlock_WhenNotLocked_ReportsNoChange` | S4, S19c |
| `Disable_ByAnotherUser_Disables` · `Disable_BySelf_ReturnsCannotDisableOwnAccount` · `Enable_WhenDisabled_Enables` · repeat cases **G2** | S17, U5 |
| `ReplaceRoles_ByAnotherUser_ReplacesSet` · `ReplaceRoles_BySelf_ReturnsCannotChangeOwnRoles` | S18, S19b |
| `SetPasswordByAdministrator_ForAnotherUser_SetsMustChange` · `SetPasswordByAdministrator_ForSelf_ReturnsCannotResetOwnPassword` · `ChangeOwnPassword_ClearsMustChange` | S19a, S19d, S16 |
| `Start_SetsExpiryTwelveHoursAfterCreation` | S1 |
| `Rotate_CurrentToken_RotatesAndAddsSuccessor` · `Rotate_ImmediatePredecessorWithinGrace_ReturnsSuperseded` · `Rotate_ImmediatePredecessorAfterGrace_RevokesFamily` · `Rotate_OlderAncestor_RevokesFamilyEvenWithinGrace` · `Rotate_AfterAbsoluteLifetime_ReturnsInvalid` · `Rotate_OnRevokedSession_ReturnsInvalid` · `Rotate_UnknownHash_ReturnsInvalid` | S7, S9–S11, S13, S14 |
| `Revoke_WhenActive_SetsRevokedAtAndReason` · `Revoke_WhenRevoked_ReportsNoChange` · `IsActive_AtExpiry_ReturnsFalse` | S15, S13 |
| `RoleNames_All_IsExactlyTheEightCanonicalIdentifiers` | R19, S30 |

### `YCR.Application.Tests/Identity` (real SQL Server, `ycr_app`, `TestClock`)

| Test | Spec |
|---|---|
| **Login:** `Handle_WithValidCredentials_CreatesSessionWithHashedTokenAndAuditsLoginSucceeded` · `Handle_WithWrongPassword_ReturnsInvalidCredentialsAndAuditsLoginFailed` · `Handle_WithUnknownUser_VerifiesDummyHashAndAuditsNullSubject` · `Handle_WithDisabledUserAndCorrectPassword_ReturnsInvalidCredentials` · `Handle_WithLockedUserAndCorrectPassword_ReturnsInvalidCredentials` · `Handle_TenthConsecutiveFailure_AuditsLoginFailedThenLockedOut` · `Handle_AfterLockoutExpires_Succeeds` · `Handle_WithParallelWrongPasswords_CountsEveryFailure` · `Handle_LoginSucceeded_AuditActorIsSignedInUserWithRoles` · `Handle_LoginFailedWithAmbientPrincipal_AuditActorIsNull` | S1–S4, S27, S31, R23 |
| **Refresh:** `Handle_WithCurrentToken_RotatesAndKeepsSessionExpiry` · `Handle_WithParallelSameToken_OneRotatesOneSuperseded` · `Handle_PredecessorWithinGrace_ReturnsSupersededAndWritesNoAudit` · `Handle_PredecessorAfterGrace_RevokesFamilyAndAuditsOnce` · `Handle_Ancestor_RevokesFamily` · `Handle_AfterSessionLifetime_ReturnsRefreshInvalid` · `Handle_RevokedSessionOrUnknownToken_ReturnsRefreshInvalid` · `Handle_FamilyRevokedWithAmbientPrincipal_AuditActorIsNull` (subject per **G1**) | S7–S11, S13, S14, S27, S29, S33 |
| **Logout:** `Handle_RevokesOnlyTheCallersSessionAndAudits` · `Handle_RacingFamilyRevocation_WritesOneRevocation` | S15, P15 |
| **ChangeOwnPassword:** `Handle_WithCorrectCurrentPassword_RevokesOtherSessionsAndKeepsCurrent` · `Handle_WithPolicyViolation_ReturnsAuthPasswordRejected` (11, 129 scalar values, blocklisted) · `Handle_WithWrongCurrentPassword_ReturnsCurrentPasswordIncorrect` · `Handle_WhenMustChange_ClearsFlag` | S16, S19d, U1 |
| **ResolveSessionPrincipal:** `Handle_ActiveSession_ReturnsUserRolesAndPermissionUnion` · `Handle_RevokedExpiredOrUnknownSession_ReturnsNone` · `Handle_DisabledUser_ReturnsNone` · `Handle_MustChangeUser_FlagsPrincipal` | O3/O4, S13, S17, S21 |
| **GetCurrentUser:** `Handle_ReturnsUserNameRolesAndPermissions` | §6.1 |
| **CreateUser:** `Handle_WithValidCommand_CreatesMustChangeUserAndAudits` · `Handle_WithDuplicateUserName_ReturnsConflict` · `Handle_WithParallelDuplicateUserNames_CreatesOne` · `Handle_WithPolicyViolation_ReturnsIdentityPasswordRejected` | S19d, S32a, U1 |
| **DisableUser:** `Handle_RevokesAllSessionsAndAudits` · `Handle_Self_ReturnsCannotDisableOwnAccount` · `Handle_LastActiveAdministrator_ReturnsLastAdministrator` · `Handle_TwoAdministratorsDisablingEachOtherConcurrently_ExactlyOneSucceeds` · repeat **G2** | S17, S19e, R27 |
| **EnableUser:** `Handle_WhenDisabled_EnablesAndAudits` · repeat **G2** | S17 |
| **UnlockUser:** `Handle_WhenLocked_ClearsAndAudits` · `Handle_WhenNotLocked_ChangesNothingAndWritesNoAudit` · `Handle_UnknownUser_ReturnsNotFound` | S4, S19c |
| **ReplaceUserRoles:** `Handle_ReplacesRolesAndAuditsBeforeAndAfter` · `Handle_Self_ReturnsCannotChangeOwnRoles` · `Handle_RemovingRoleFromLastAdministrator_ReturnsLastAdministrator` · `Handle_ConcurrentDemotionsOfTheLastTwoAdministrators_ExactlyOneSucceeds` | S18, S19b, S19e |
| **ResetUserPassword:** `Handle_RevokesAllSessionsSetsMustChangeAndAudits` · `Handle_Self_ReturnsCannotResetOwnPassword` · `Handle_WithPolicyViolation_ReturnsIdentityPasswordRejected` | S19a, U5 |
| **Sessions/roles/users reads:** `ListUserSessions_ReturnsPagedSessionsWithReasons` · `RevokeSession_RevokesNamedSessionOnlyAndAudits` · `RevokeSession_UnknownSession_ReturnsNotFound` · `ListRoles_ReturnsEightRolesWithPermissions` · `ListUsers_ReturnsPagedEnvelope` · `GetUser_UnknownId_ReturnsNotFound` | S19, §6.2 |
| **Bootstrap:** `Handle_WithNoAdministrator_CreatesMustChangeAdministratorAndAuditsNullActor` · `Handle_WhenAnAdministratorExists_RefusesAndWritesNothing` · `Handle_TwoConcurrentRuns_CreateOneAdministrator` | S32, R27 |
| **Secrets at rest:** `IdentityTables_AfterSignInAndRefresh_ContainNoRawTokenOrPassword` | S31 |

### `YCR.Infrastructure.Tests`

| Test | Credential | Spec |
|---|---|---|
| `Migrate_FromF001Schema_CreatesIdentityTablesConstraintsAndIndexes` | migrator | §7, `docs/21` §Data |
| `Seed_ProducesExactlyEightRolesAndFourteenGrants` · `Seed_MatchesDocs10GrantTables` (parses `docs/10` §Station and §Identity permission grants) · `Seed_EveryPermissionIsAPermissionsConstant` · `Seed_CreatesNoUser` | migrator | S30 |
| `UserNameConstraint_RejectsInvalidNames` · `UtcConstraints_RejectNonUtcOffsets` · `RevocationConstraints_RejectHalfSetRows` | migrator | S32a, ADR-0018 |
| `RefreshTokens_SecondRotationOfSameToken_AffectsNoRow` · `RefreshTokens_SameSuccessorTwice_IsRejectedByUniqueIndex` | `ycr_app` | S33 |
| `ApplicationCredential_HasExactlyTheIdentityGrants` · `ApplicationCredential_HasNoDeleteOnSessionsOrTokens` · `ApplicationCredential_CannotWriteRolesOrGrants` · `ApplicationCredential_CanUpdateOnlyListedUserColumns` · `ApplicationCredential_CanTakeTheAdministratorApplock` | `ycr_app` | §7 grants, P7, V5 |
| `ModuleInterfaces_IdentityContext_ResolvesToSameInstance` | `ycr_app` | ADR-0012 |
| `AddInfrastructure_RegistersNoAuthenticationScheme` · `IdentityCore_LoadedAssembly_Is10_0_12` | — | V1, V2 |
| `PasswordPolicy_ElevenScalarValues_Rejected` · `_TwelveAccepted` · `_128Accepted` · `_129Rejected` · `_BlocklistedRejectedCaseInsensitively` · `_CountsScalarValuesNotUtf16Units` · `_HasNoCompositionRule` | — | R18, S16, P12 |
| `Blocklist_LoadsFromEmbeddedResource` · `Blocklist_HasNoEntryShorterThanTwelve` · `Blocklist_Sha256MatchesNotice` | — | P9 |
| `Verify_UnknownUser_PerformsFullHashVerification` (spy on the hasher, not a stopwatch) · `Verify_DisabledOrLockedUser_PerformsFullHashVerification` | — | S2, R23 |
| `Generate_Returns32RandomBytesAndTheirSha256` | — | R5 |
| `RecordWithoutActor_IgnoresAmbientPrincipal` · `RecordSignIn_UsesVerifiedUserAndRoles` · `Record_ActorRoleIsJsonArrayOfCanonicalIdentifiers` | `ycr_app` | S27, R12 |

### `YCR.Api.Tests`

| File · Test | Mode | Spec |
|---|---|---|
| `Identity/LoginEndpointTests` · `Login_WithValidCredentials_Returns200TokenAndHardenedCookie` · `Login_UniformRejection_FourCasesIdenticalApartFromTraceId` · `Login_WithInvalidBody_Returns400` | Real | S1, S2, `docs/21` |
| `Identity/LockoutEndpointTests` · `Login_TenFailures_LocksThenAdministratorUnlockRestoresAccess` · `Unlock_WithoutUsersManage_Returns403` · `Unlock_UnknownId_Returns404` | Real | S4, S19c |
| `Identity/RateLimitTests` · `Login_OverPerUserNameLimitFromVariedAddresses_Returns429` · `Login_OverPerAddressLimitForVariedUserNames_Returns429` · `Login_RejectedByLimit_EvaluatesNoCredentialAndWritesNoAudit` · `Login_DefaultLimits_AreFivePerUserNameAndTwentyPerAddress` · `Refresh_OverPerAddressLimit_Returns429` · `Refresh_DefaultLimit_IsThirtyPerAddress` | Real | S5, S12, U6 |
| `Identity/OriginTests` · `Login_MissingOrForeignOrigin_Returns403WithNoCookieNoAuditNoCount` · `Refresh_MissingOrForeignOrigin_Returns403WithoutRotation` · `Logout_MissingOrigin_Returns403AndRevokesNothing` | Real | S6, S12, S15, R9 |
| `Identity/RefreshEndpointTests` · `Refresh_WithValidCookie_RotatesAndKeepsSessionExpiry` · `Refresh_TwoTabRace_OneRotatesOther409ThenSuccessorWorks` · `Refresh_PredecessorInsideGrace_Returns409NoCookieNoAudit` · `Refresh_PredecessorOutsideGrace_RevokesFamilyAndAccessTokenFailsWithin30s` · `Refresh_Ancestor_RevokesFamily` · `Refresh_AfterTwelveHours_Returns401` · `Refresh_MissingRevokedOrUnknownCookie_Returns401` · `Ledger_AfterRefreshScenarios_HasExactlyTwoFamilyRevokedRows` | Real | S7–S11, S13, S14, S29 |
| `Identity/LogoutEndpointTests` · `Logout_RevokesSessionExpiresCookieAndAccessTokenFailsWithin30s` · `Logout_LeavesOtherSessionsActive` · `Logout_WithoutBearer_Returns401` | Real | S15 |
| `Identity/PasswordEndpointTests` · `ChangePassword_RevokesOtherSessionsWithin30sAndKeepsCurrent` · `ChangePassword_PolicyViolation_Returns400AuthPasswordRejected` · `ChangePassword_WrongCurrent_Returns422` · `Me_ReturnsUserNameRolesAndPermissions` · `Me_Anonymous_Returns401` | Real | S16, §6.1 |
| `Identity/AccessTokenTests` · `Token_HasEs256KidAndExactlyEightClaims` · `Token_Invalid_Returns401` (theory: tampered, expired, wrong key, HS256, `none`, wrong `iss`, wrong `aud`, revoked `sid`, unknown `sid`) · `Token_PastExp_Returns401` · `Token_UnexpiredButSessionPastLifetime_Returns401` | Real | S20, S21, S13 |
| `Identity/RevocationLatencyTests` · `RemovedRole_WithinThirtySeconds_Returns403WithoutReLogin` · `RemovedRolePermissionRow_WithinThirtySeconds_Returns403` · `DisabledUser_AccessTokenRejectedWithin30s` · `RevokedSession_AccessTokenRejectedWithin30s` · `PrincipalCacheTtlAboveThirtySeconds_FailsStartup` | Real | S17, S18, S19, R3 |
| `Identity/UserAdministrationEndpointTests` · create (201, must-change, 400 username rule, 400 `Identity.PasswordRejected`, 409 duplicate, 400 unknown role) · disable/enable (204, sessions revoked, 422 self, **G2** repeat) · roles (204 with audit before/after, 422 self) · reset (204, sessions revoked, must-change, 422 self, 400 policy) · list/get (paged, 404) · sessions list · revoke (204, other sessions untouched, 404) · roles catalogue · `EveryAdministrationEndpoint_Anonymous_Returns401` · `EveryAdministrationEndpoint_WithoutPermission_Returns403` · `EveryAdministrationEndpoint_WithInvalidBody_Returns400` | Real | S17, S19, S19a, S19b, S32a, S26, `docs/21` |
| `Identity/MustChangePasswordTests` · `MustChangeSession_MayCallOnlyMeRefreshLogoutAndPassword` (theory over every other endpoint → `403 Auth.PasswordChangeRequired`) · `AfterPasswordChange_SameSessionSucceedsWithoutSignIn` · `AdministratorCreatedAndResetPasswords_AreMustChange` | Real | S19d, R26 |
| `Identity/LastAdministratorEndpointTests` · `DisableAndReEnableSecondAdministrator_AllowedWhileOneRemains` · `MutualDisableAtOnce_ExactlyOneSucceeds` | Real | S19e |
| `Identity/AuditActorTests` · `AdministrationEvents_ActorAndPermissionFromServerContext_BodyAndHeadersIgnored` · `LoginEvents_ActorFieldsPerU4_BearerOfAnotherUserIgnored` · `FamilyRevoked_ActorNull` | Real | S27 |
| `Identity/SecretLeakTests` · `AfterAuthScenarios_NoSecretInLogsAuditOrProblemBodies_NoUserNameInLogs` (captured `ILoggerProvider`) | Real | S28 |
| `Identity/WrongPermissionTests` · `TicketOperator_PostStations_Returns403` · `TicketOperator_AnyAdministrationEndpoint_Returns403` | Real | S26 |
| `Identity/ProductionCompositionTests` · `Bootstrap_ChangePassword_Login_ListStations_WithNoTestOverrides` (runs `BootstrapAdministratorCommand` in-process against the same database; configuration only via `UseSetting`; no `ConfigureTestServices`) · `ListStations_Anonymous_Returns401AuthUnauthenticated` | **Unmodified** | S25, `docs/21` §Tests |
| `Common/DeployedShapeTests` (replaces `MissingSchemeTests`) · `ProtectedEndpoint_Anonymous_Returns401BearerChallengeWithProblemDetails` (the four station routes — successor of the old theory) · `HealthEndpoint_Anonymous_StaysAnonymous` (successor) · `CommonUnauthenticated_AppearsNowhereInSrc` · `AuthorizationResultHandler_TypeNoLongerExists` | Unmodified | S22 |
| `Common/AuthenticationSchemeGuardTests` · `Startup_WithOnlyJwtBearerOutsideTesting_Succeeds` · `Startup_WithTestHandlerOutsideTesting_Throws` · `Startup_UnderTesting_Succeeds` | — | S23 |
| `Common/SigningKeyStartupTests` · `Production_WithDevelopmentOnlyKey_FailsStartup` · `Production_WithDevOrTestKid_FailsStartup` · `Startup_WithNoActiveKey_FailsStartup` · `Startup_WithNonP256Key_FailsStartup` | — | S26a, R14 |
| `Common/BrowserControlsTests` · `Preflight_FromForeignOrigin_HasNoCorsHeaders` · `EveryResponseKind_CarriesSecurityHeaders` (200, 204, 400, 401, 403, 404, 429, 500) · `AuthResponses_AreNoStore` | Real | S26b, S26c, P10 |
| `Common/PermissionPolicyProviderTests` (new or extended) · `GetPolicy_ForMultiSegmentAndHyphenatedPermission_ReturnsPermissionPolicy` · `GetPolicy_ForNonPermissionName_FallsThrough` | — | P-note |
| F-001 `Network/StationEndpointsTests`, `ProblemDetailsTests`, `HealthEndpointsTests` | TH | unchanged; must stay green (regression) |

### `YCR.IntegrationTests`

| Test | Spec |
|---|---|
| `Bootstrap_FirstRun_CreatesOneMustChangeAdministratorAndExitsZero` · `Bootstrap_SecondRun_ExitsNonZeroAndWritesNothing` · `Bootstrap_InvalidUserName_ExitsNonZero` · `Bootstrap_PolicyViolatingPassword_ExitsNonZero` · `Bootstrap_PasswordAppearsInNoOutputOrLog` · `Bootstrap_PasswordOnCommandLine_IsRefused` | S32, S32a, R13, P8 |

### `YCR.ArchitectureTests`

| Test | Spec |
|---|---|
| `SourceTypes_WithAuthenticationHandler_HaveNoViolations` (now also scans `YCR.Worker`) | S24, D15 |
| `RecordSignIn_CalledOutsideLoginHandler_IsDetected` (+ fixture) | P5, V6 |
| `IdentityApplication_DependingOnAnotherModuleContext_IsDetected` · `NetworkApplication_DependingOnIdentityContext_IsDetected` (+ fixture) | ADR-0012 |
| `Application_DependingOnAspNetIdentity_IsDetected` (+ fixture) — Identity stays in Infrastructure | P1 |
| existing Api→Domain allowlist rule | unchanged; `YCR.Domain.Identity` is forbidden to the Api like any module |

### ADR-0016 §Required tests → tests

| Required test | Test(s) |
|---|---|
| Refresh race with two tabs | `Refresh_TwoTabRace_…` (Api), `Handle_WithParallelSameToken_…` (App), `RefreshTokens_SecondRotationOfSameToken_AffectsNoRow` (Infra) |
| Predecessor reuse inside grace | `Refresh_PredecessorInsideGrace_…`, `Rotate_ImmediatePredecessorWithinGrace_…` |
| Predecessor reuse outside grace | `Refresh_PredecessorOutsideGrace_…`, `Rotate_ImmediatePredecessorAfterGrace_…` |
| Ancestor reuse | `Refresh_Ancestor_…`, `Rotate_OlderAncestor_…` |
| Logout | `Logout_RevokesSession…`, `Logout_LeavesOtherSessionsActive` |
| Password change | `ChangePassword_RevokesOtherSessions…`, `Handle_WithCorrectCurrentPassword_…` |
| Disabled user | `DisabledUser_AccessTokenRejectedWithin30s`, disable endpoint tests |
| Removed permission effective within 30 s | `RemovedRole_WithinThirtySeconds_…`, `RemovedRolePermissionRow_WithinThirtySeconds_…` |
| Missing Origin | the three `OriginTests` |
| Session revocation | `RevokedSession_AccessTokenRejectedWithin30s`, revoke endpoint tests |

Every ADR-0016 required test and S25 uses real tokens (D13).

### Whole suite

`dotnet build YCR.sln` and `dotnet test YCR.sln` green with no skipped tests; CI green on `origin` for `feature/F-002`, including the updated smoke job.

---

## New packages

| Package | Where | Reason (`docs/20` §8) |
|---|---|---|
| `Microsoft.AspNetCore.Authentication.JwtBearer` **10.0.12** | `YCR.Api` | The framework `JwtBearerHandler` that D13 allowlists and ADR-0020 item 4 relies on (no YCR handler). Not in the shared framework (O6-f). Brings `Microsoft.IdentityModel.*` and `System.IdentityModel.Tokens.Jwt` **8.19.2** transitively; `JsonWebTokenHandler` from that set issues tokens, so no separate token library is added. Central transitive pinning keeps one IdentityModel version |
| `Microsoft.Extensions.Identity.Core` **10.0.12** | `YCR.Infrastructure` | `AddIdentityCore`, `UserManager<T>`, `PasswordHasher<T>` for D1 (P1). An explicit pin rather than a `FrameworkReference`, so the Identity version does not follow the installed runtime (V2, P13) |

**Deliberately not added:** `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (P1); `Microsoft.Extensions.TimeProvider.Testing` (a ten-line `TestClock` in `YCR.TestSupport` does what the tests need); any rate-limiting package (`System.Threading.RateLimiting` is in the shared framework, P6); any password-blocklist package (P9, embedded file); any caching package (`IMemoryCache` is in the shared framework); `Microsoft.IdentityModel.JsonWebTokens` as a direct reference (transitive via JwtBearer; issuance lives in the Api for that reason).

---

## Risks

| # | Risk | Likelihood | Mitigation |
|---|---|---|---|
| R-1 | Per-request principal resolution adds a DB round trip every ≤15 s per session, and a DB outage makes every authenticated request fail | Medium | Cache (P4); one indexed query per miss; accepted by ADR-0016 §Consequences ("remains dependent on database/cache health") |
| R-2 | A replaced principal accidentally keeps a token claim (e.g. a future token claim becomes a permission) | Low | P3 builds a new identity; `Token_HasEs256KidAndExactlyEightClaims` and a resolver test that a forged `permission` claim in a (test-signed) token grants nothing |
| R-3 | Fake-clock tests diverge from framework components that read wall-clock time (JWT lifetime, rate limiter windows) | Medium | V3 / P11 for JWT; rate-limit tests fire bursts well inside one window and never advance the fake clock |
| R-4 | Lockout DoS on known usernames (ADR-0023 negative) | Accepted | Per-username rate limit; administrator unlock (U3) |
| R-5 | R27 lock serialises administration writes | Low | Taken only when the operation could reduce the active-administrator set (P14); staff administration is low volume |
| R-6 | `docs/10` parsing test (`Seed_MatchesDocs10GrantTables`) is brittle to doc formatting | Medium | Parses only the two grant tables by heading; a failure names the row; the doc and the migration must change together, which is the point |
| R-7 | Dev key handling leaks a key into the repo | Medium | Keys only in user-secrets/env; gitleaks PEM rule (step 12); production refuses dev-marked keys (S26a) |
| R-8 | `Origin` checks break non-browser clients (scripts, the smoke job) that send no `Origin` | Medium | Intended (R9); the smoke job and `YCR.Api.http` send `Origin`; documented in README |
| R-9 | The installed ASP.NET Core runtime (10.0.10 here) differs from the pinned 10.0.12 | Medium | P13 explicit pin + V2 test; CI's `setup-dotnet` uses `global.json` |
| R-10 | Sliding-window partition state grows under username spraying | Low | The partitioned limiter disposes idle partitions; per-address limit caps one source |
| R-11 | Retained session/token rows grow (~17 M/year worst case) | Medium | P7 follow-up task before production |
| R-12 | The Identity slice becomes the pattern later auth-adjacent features copy, including the P5 actor exception | Medium | P5 is architecture-tested to one caller; stage 8 documents it in `docs/20` |
| R-13 | G1/G2 decided late forces rework of audit rows | Low | They block only the rows named; the ledger is written only in steps 5–6, after hein rules |

---

## Rollback and forward compatibility

**Migrations.** All three have working `Down()` methods: `Security_IdentityGrants` revokes; `Identity_SeedRolesAndPermissionGrants` deletes exactly the seeded rows (it fails if a `UserRoles` row references them, which is correct once users exist); `Identity_CreateIdentitySchema` drops the tables. Once users exist, rolling back the schema destroys accounts — a restore, not a down-migration, is the recovery path in production. The audit ledger is untouched (additive rows only; no schema change).

**API.** New endpoints only. The one behavioural change to an existing contract is the `401` body's `errorCode`: `Common.Unauthenticated` → `Auth.Unauthenticated`, with `WWW-Authenticate: Bearer` added. No client exists yet (no SPA, OQ22), so nothing breaks; the CI smoke job is the only consumer and changes in step 12.

**Partially deployed instances.** A new instance with an old database fails `/health/ready`-adjacent requests on missing tables; deployment order is migrations first (as F-001). Instances must share the signing key and configuration (D11); with differing `Auth:PrincipalCacheSeconds` the bound is the largest value, still ≤30 s by validation.

**Forward compatibility built in.** `kid` in every token and a key list in configuration make key rotation a configuration change. `RevocationReason` is a check-constrained string, so a new reason is an additive constraint change. `PayloadVersion` 1 for all Identity snapshots.

---

## Steps

Each step ends with `dotnet test YCR.sln` green. No step leaves the branch red. Steps 5 and 6 cannot finish until **G1** and **G2** are ruled.

| # | Step | Ends green with |
|---|---|---|
| 1 | Packages (§New packages); `Permissions` constants; `PermissionPolicyProvider` pattern; `TestClock`, `TestSigningKey`. **V1, V2** | `PermissionPolicyProviderTests`; `AddInfrastructure_RegistersNoAuthenticationScheme`; `IdentityCore_LoadedAssembly_Is10_0_12`; all F-001 tests |
| 2 | `YCR.Domain.Identity`: `UserName`, `StaffUser`, `AuthSession`, `RefreshToken`, `RoleNames`, `AccountLockoutPolicy`, `IdentityErrors` | All Domain rows (G2 rows once ruled) |
| 3 | Persistence: `IIdentityDbContext`, six configurations, the three migrations. **V4, V7** | All Infrastructure migration/seed/constraint/privilege rows; `ModuleInterfaces_IdentityContext_…` |
| 4 | Infrastructure services: `StaffUserStore`, `PasswordService`, `PasswordPolicyValidator`, blocklist + notice, `RefreshTokenGenerator`, `SqlServerIdentityAdministratorLock`, `AuditWriter` methods, DI. **V5** | Password/blocklist/generator/`AuditWriter` rows; applock grant test |
| 5 | Application sign-in slice: `Login`, `RefreshSession`, `Logout`, `ChangeOwnPassword`, `GetCurrentUser`, `ResolveSessionPrincipal`, snapshots, audit subjects (**G1**), telemetry; architecture rules for P5 and module boundaries. **V6** | Application rows for those handlers; architecture rows |
| 6 | Application administration slice: all §6.2 handlers and `BootstrapAdministrator` with P14 (**G2**) | Remaining Application rows incl. S19e concurrency |
| 7 | API pipeline swap: `JwtBearerSetup` (P3, P11, challenge), `SessionPrincipalCache`, signing-key provider and issuer, options validation, guard allowlist, `PasswordChangeRequiredMiddleware`, `SecurityHeadersMiddleware`; delete `AuthorizationResultHandler`; `YcrApiFactory` modes; `MissingSchemeTests` → `DeployedShapeTests`. **V3** | `DeployedShapeTests`, `AuthenticationSchemeGuardTests`, `SigningKeyStartupTests`, `AccessTokenTests` (with tokens minted by the issuer directly), F-001 station tests on TH |
| 8 | Auth endpoints + contracts + `OriginCheckFilter`, `AuthRateLimitFilter`, `RefreshCookie`. **V8** | Login, lockout (sign-in half), rate-limit, origin, refresh, logout, password, me, must-change, secret-leak, audit-actor (sign-in half), browser-controls rows |
| 9 | Administration endpoints + contracts | Administration, revocation-latency, last-administrator, wrong-permission, remaining audit-actor rows |
| 10 | `YCR.Worker` bootstrap command, `SystemCurrentUser`; `YCR.IntegrationTests` wired to TestSupport | Bootstrap CLI rows; `ProductionCompositionTests` (S25) |
| 11 | README, `.env.example`, `appsettings`, `YCR.Api.http`, `THIRD-PARTY-NOTICES.md` | Build + full suite (no new tests) |
| 12 | CI: smoke job (key, origin, `Auth.Unauthenticated`, headers), gitleaks PEM rule; push `feature/F-002` and **prove the run green**, recording the URL in `progress.md` | Green GitHub Actions run on `origin` for `feature/F-002` |

Documentation updates listed under §Affected modules belong to **stage 8**.

---

## Decisions note for hein

- **Confirm or overturn P1–P15.** The two with the widest consequences: **P1** (hand-written store over the domain aggregate instead of Identity's EF store) and **P5** (two new `IAuditWriter` methods, one of them architecture-restricted to `LoginHandler`).
- **N1 — R26 scope.** The plan reads "every other authenticated request" as "every request to an endpoint that requires authorization": anonymous endpoints (`/auth/login`, `/health/*`) are unaffected even if a must-change bearer token is attached. Confirm.
- **G1 and G2** (§Spec gaps) must be ruled before steps 5–6 finish.
- **Spec housekeeping (not edited — the spec is Approved):** spec §Notes' last bullet still says `docs/10`'s unlock and `docs/19` OQ34's U5 are pending; both were done at `d2a368d`.

---

## Review history

**Revision 1 — 2026-09-23, claude (T-024).** First draft.

---

## Stop point

⛔ **Plan needs hein's approval before implementation.** T-024 is `blocked` (`approval`). No code, tests or migrations exist for F-002; the implementation task starts only after approval and after G1 and G2 are ruled.

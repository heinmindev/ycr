# Deployment Architecture

Initial production shape:

```text
Clients
   |
Reverse Proxy / Load Balancer
   |
ASP.NET Core API
   |
SQL Server

Optional:
   |---- Background Worker
   |---- Distributed Cache
   |---- Observability stack
```

Use Docker for repeatable environments.

Separate:

- Development
- Test
- Staging
- Production

Secrets must be injected through environment/secret-management mechanisms.

The SPA and API are served same-origin through a reverse proxy; API requests use `/api/*`. The reverse proxy is responsible for routing the separate SPA build and API without creating a cross-site cookie deployment.

## F-002 deployment requirements — staff authentication

The API refuses to start when any of these is missing or wrong, before it serves a request.

### Runtime

- **ASP.NET Core runtime 10.0.12 or later** wherever the API runs (T-026 item 6). The API loads
  ASP.NET Core Identity from the installed shared runtime, not from the pinned 10.0.12 package
  that `YCR.Worker` uses, so an older runtime would run older Identity code. The production image
  and the CI runtime must be at that patch level or later.

### Signing key (`Auth:Signing`)

Access tokens are signed ES256; every instance must have the same keys (ADR-0023 item 7).

| Setting | Meaning |
|---|---|
| `Auth:Signing:ActiveKeyId` | The `kid` that signs new tokens; must be one of the keys below |
| `Auth:Signing:Keys:<n>:KeyId` | The key's `kid`; unique |
| `Auth:Signing:Keys:<n>:PrivateKeyPkcs8Pem` | A PKCS#8 PEM **P-256** private key. **Secret**: inject it from the environment or a secret store, never a file in the repository |
| `Auth:Signing:Keys:<n>:DevelopmentOnly` | `true` marks a developer's own key |

Startup refuses: no active key or an active `kid` not among the keys; a key without a `kid` or a
repeated `kid`; a key that is not a P-256 private key; and, outside Development and Testing, a key
marked `DevelopmentOnly` or with a `kid` starting `dev-` or `test-`. The error names the `kid`,
never key material. Tokens are validated only by their `kid`, so removing a key from the list
immediately invalidates every unexpired token it signed. **The production key store and rotation
procedure are not decided** (T-026 item 3, blocked on hosting); the configuration-backed provider
is the only one implemented. Developers
generate their own key (`README.md`).

### Allowed origins (`Auth:AllowedOrigins`)

`Auth:AllowedOrigins:<n>` lists the exact origins (scheme, host, optional port; no path or
trailing `/`) from which `POST /api/v1/auth/login`, `/refresh` and `/logout` are accepted.
Outside Development and Testing at least one is required and each must be `https`. In the
same-origin deployment this is the site's own public origin.

### Other `Auth` settings

| Setting | Default | Rule |
|---|---|---|
| `Auth:Issuer`, `Auth:Audience` | `YCR.Api` | Must be set |
| `Auth:PrincipalCacheSeconds` | 15 | 1–30 (revocation takes effect within 30 seconds) |
| `Auth:RateLimits:LoginPerUserNamePerMinute` | 5 | Positive |
| `Auth:RateLimits:LoginPerClientAddressPerMinute` | 20 | Positive |
| `Auth:RateLimits:RefreshPerClientAddressPerMinute` | 30 | Positive |

The access-token lifetime (15 minutes), the session lifetime (12 hours) and the refresh grace
window (20 seconds) are fixed in code (ADR-0023 item 8). They are not settings: startup refuses
`Auth:AccessTokenLifetime`, `Auth:SessionLifetime` or `Auth:RefreshGraceWindow` set to any other
value. The rate limits are per instance, and no trusted-proxy configuration exists yet, so behind
a proxy the client address is the proxy's (T-026 item 4).

### The first administrator (`YCR.Worker bootstrap-administrator`)

```bash
printf '%s\n' "$INITIAL_PASSWORD" | \
  ConnectionStrings__Application="<the ycr_app connection string>" \
  DOTNET_ENVIRONMENT=<environment> \
  dotnet YCR.Worker.dll bootstrap-administrator --username <name>
```

- Creates the first `SystemAdministrator` with a must-change password, once: it refuses when any
  user already holds `SystemAdministrator`. It runs under the application credential `ycr_app`,
  does not start the host, and writes one `Identity.UserCreated` audit row with no actor.
- The password is read from standard input (or typed without echo at a terminal). A `--password`
  or `-p` argument is refused.
- Exit codes: `0` created; `1` refused by a rule, nothing written; `2` usage error.
- The environment is `DOTNET_ENVIRONMENT`, **`Production` when unset**. In Production the command
  is refused with `Identity.PrivilegedRoleRequiresMfa` until the MFA feature ships (ADR-0023
  item 4 as amended 2026-09-24); the CI smoke job therefore runs it with
  `DOTNET_ENVIRONMENT=Testing` against an API running as Production.

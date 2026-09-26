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

## F-004 deployment requirements — the local time zone

"Today" (for example the earliest date a service may be withdrawn from) is the calendar date in
the configured local zone, never the server's own local time and never a fixed offset (ADR-0018
§Time; F-004 plan P11, ruling Q3). Both the API and the Worker **refuse to start** without it.

### `Time:LocalTimeZone`

| Setting | Shipped value | Rule |
|---|---|---|
| `Time:LocalTimeZone` | `Asia/Yangon` (in the `appsettings.json` of both `YCR.Api` and `YCR.Worker`) | An **IANA** time-zone id that the host can resolve |

- **API:** the setting is validated at startup (`ValidateOnStart`), so the host stops before it
  serves a request, with `Time:LocalTimeZone is not configured…` when the value is missing or
  blank, or `Time:LocalTimeZone '<id>' is not a time zone this host can resolve; install IANA
  time-zone data.` when it cannot be resolved.
- **Worker:** checked before either start path (the `bootstrap-administrator` command and the bare
  host); on failure it writes the same message to standard error and exits `1`, doing nothing
  else.
- There is no fallback: a host that cannot resolve the zone does not run. Asia/Yangon is UTC+06:30
  with no daylight saving time, so its local midnight is 17:30:00Z.

### IANA time-zone data

Resolving `Asia/Yangon` needs the operating system's time-zone data:

- **Windows:** .NET resolves IANA ids through **ICU** (present on current Windows).
- **Linux:** the **`tzdata`** package (`/usr/share/zoneinfo/Asia/Yangon`).

**FACT (checked with `docker`, 2026-09-25):** `mcr.microsoft.com/dotnet/runtime-deps:10.0` (Ubuntu
24.04.5 LTS, the base of the `runtime`, `aspnet` and `sdk` 10.0 images) has `tzdata` installed and
resolves `Asia/Yangon`. `runtime-deps:10.0-noble-chiseled` has **no** `Asia/Yangon` file, so the
**plain `-chiseled` images must not be used**; `runtime-deps:10.0-noble-chiseled-extra` has it.
CI does not run in these images: its jobs run directly on the GitHub-hosted `ubuntu-latest`
runner, whose own `/usr/share/zoneinfo` is used, and
`LocalCalendarTests.AsiaYangon_ResolvesInThisEnvironment` fails by name wherever the zone cannot be
resolved. On Linux tzdata, `Asia/Yangon` carries historical pre-1946 adjustment rules and .NET
reports `SupportsDaylightSavingTime = true`; the offset is still +06:30 with no daylight delta for
every date from 2026 to 2040, which is what that test asserts (F-004 plan Amendment 1). No
production image exists yet; whoever writes it must pick an image with `tzdata`.

### Content root: run each host from its output directory

The API reads `appsettings.json` from its **content root**, which is the process's working
directory. It must therefore be started **with its build or publish output directory as the
working directory** (for example `cd <output>` then `dotnet YCR.Api.dll`), so that it reads its
shipped `appsettings.json`, including `Time:LocalTimeZone`. Started from any other directory, it
reads no `appsettings.json` and refuses to start (`Time:LocalTimeZone is not configured`). CI's
API smoke job starts it this way (`working-directory: src/YCR.Api/bin/Release/net10.0`), sets no
`Time__` override, and asserts that the logged `Content root path:` is that directory (F-004 plan
Amendment 2).

The Worker also runs from its output directory. Its zone check and the `bootstrap-administrator`
command read `appsettings.json` from the directory that holds `YCR.Worker.dll`, wherever it is
started; its bare host (no command) takes the working directory as content root, like the API.

Every setting may still be supplied or overridden by an environment variable
(`Time__LocalTimeZone`), as for `Auth` settings; the shipped file makes that unnecessary.

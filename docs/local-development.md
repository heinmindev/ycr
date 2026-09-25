# Local development: setup, running and testing

How to get the YCR platform running on a developer machine, sign in, call the API, and run the
tests. Written for Windows and PowerShell, which the team uses; bash equivalents are given where
they differ. Every command runs from the repository root unless a step says otherwise.

This guide describes the state of `main` after F-002 (staff authentication). It does not
change any rule; the authoritative sources are the ADRs, `docs/08` (API), `docs/07` (database),
`docs/15` (deployment) and the README.

---

## 1. Prerequisites

| Tool | Version | Why |
|---|---|---|
| .NET SDK | 10.0.302 or a later 10.0.3xx patch (`global.json`) | build, run, test |
| ASP.NET Core runtime | **10.0.12 or later** | the API loads ASP.NET Core Identity from the installed runtime, not from the pinned package (T-026 item 6). Older patches build but run older Identity code |
| Docker Desktop | current, Linux containers | local SQL Server, and every database-backed test |
| Git | any | Git for Windows also ships `openssl`, used in §5 on Windows PowerShell 5.1 |

Check them:

```powershell
dotnet --list-sdks
dotnet --list-runtimes     # look for Microsoft.AspNetCore.App 10.0.12 or later
docker version             # both Client and Server must answer; Server needs Docker Desktop running
```

The `dotnet-ef` tool is pinned in `.config/dotnet-tools.json` (10.0.12). Restore it once per clone:

```powershell
dotnet tool restore
```

---

## 2. The local database

### 2.1 `.env`

```powershell
Copy-Item .env.example .env
```

Fill in the three passwords in `.env`. Two rules, both learned the hard way:

- **SQL Server's password policy applies to all three.** Each needs at least 8 characters and at
  least 3 of: upper case, lower case, digits, symbols. A weak `MSSQL_SA_PASSWORD` makes the
  server container exit a few seconds after it starts (exit code 255). A weak
  `YCR_APP_PASSWORD` or `YCR_MIGRATOR_PASSWORD` makes the init container exit with 1, because
  both logins are created with `CHECK_POLICY = ON`.
- **Do not use `$`, `'`, `"` or spaces.** Compose treats `$` as a variable, and the init script
  places the passwords inside `'...'` in SQL.

`MSSQL_PORT` defaults to 1433. If a local SQL Server already holds 1433, set `MSSQL_PORT=14330`
and use `localhost,14330` in every connection string below.

### 2.2 Start SQL Server

```powershell
docker compose up -d
docker compose ps -a
```

Expected: `ycr-sqlserver` is `Up (healthy)` and `ycr-sqlserver-init` is `Exited (0)`. The init
container runs `docker/sqlserver/init-principals.sql` once: it creates the `YCR` database and the
two logins, `ycr_migrator` (applies migrations, nothing else) and `ycr_app` (what the API and the
Worker run as). It creates no role and no grant; those come from migrations (ADR-0017 item 3).

If the init container is not `Exited (0)`, its log names the failing statement:

```powershell
docker compose logs sqlserver-init --tail 30
```

After fixing `.env`, re-run only the init container. The script is idempotent:

```powershell
docker compose up -d sqlserver-init
```

### 2.3 Apply the migrations (as `ycr_migrator`)

The design-time factory reads `YCR_DESIGN_TIME_CONNECTION` and has no default, so an EF command
can never hit a database by accident (`docs/07` §F-001 station persistence controls). Use the
migrator login and the `YCR_MIGRATOR_PASSWORD` from `.env`:

```powershell
$env:YCR_DESIGN_TIME_CONNECTION = "Server=localhost,1433;Database=YCR;User Id=ycr_migrator;Password=<migrator password>;TrustServerCertificate=True;Current Language=us_english"
dotnet ef database update --project src/YCR.Infrastructure --startup-project src/YCR.Infrastructure
Remove-Item Env:YCR_DESIGN_TIME_CONNECTION
```

This applies twelve migrations (F-001: stations, the audit ledger, the `ycr_app` role; F-002: the
`identity` schema, the seeded roles and grants, the identity grants; F-003: routes, their seeded
grants, the route grants; F-004: the `timetable` schema, the seeded service grants, the timetable
grants; `docs/07`) and ends with `Done.`

`Current Language=us_english` is required on every connection string: unique-constraint
violations are recognised from the English error text (`.env.example`).

### 2.4 Join the application login to its role

The first init run could not do this, because the `ycr_app` role did not exist yet. Run it again:

```powershell
docker compose up -d sqlserver-init
docker compose logs sqlserver-init --tail 5
```

The log must say `ycr_app_user added to the ycr_app role.` Until it does, the API connects but
can read and write nothing.

### 2.5 Start over

To throw the local database away completely (for example if you lose the only administrator's
password):

```powershell
docker compose down -v      # -v deletes the data volume
```

Then repeat §2.2 to §2.4 and §3.

---

## 3. The first administrator

No user is seeded (spec S30). Create the first `SystemAdministrator` once with the Worker, under
the **application** login. Use a fresh PowerShell window afterwards for anything else, so these
variables do not leak into other commands.

```powershell
$env:DOTNET_ENVIRONMENT = "Development"
$env:ConnectionStrings__Application = "Server=localhost,1433;Database=YCR;User Id=ycr_app;Password=<app password>;TrustServerCertificate=True;Current Language=us_english"
dotnet run --project src/YCR.Worker -- bootstrap-administrator --username <name>
```

- The username is 3 to 50 characters of lower-case `a`-`z`, `0`-`9` and `.`.
- The password is read from the console, never from an argument. It must be 12 to 128 characters
  and not on the shipped common-password list (ADR-0023 item 2).
- The password is **must-change**: the first sign-in can do nothing until it is changed (§6).
- Exit codes: `0` created; `1` refused, nothing written (an administrator already exists, invalid
  username or password, or the environment is Production); `2` usage error.
- `DOTNET_ENVIRONMENT` must not be `Production` or unset (unset means Production). In Production
  the command is refused with `Identity.PrivilegedRoleRequiresMfa` until the MFA feature ships
  (ADR-0023 item 4 as amended).

---

## 4. The API's local configuration (once per machine)

The API refuses to start without a signing key and an allowed origin (spec S26a, R9). They are
per developer and live in `dotnet user-secrets`, which the API reads in `Development` only.
**No key is ever committed**; `*.pem` and `*.key` are git-ignored.

### 4.1 Generate a development signing key (ES256, P-256)

PowerShell 7 or later (`$PSVersionTable.PSVersion`):

```powershell
$ec  = [System.Security.Cryptography.ECDsa]::Create([System.Security.Cryptography.ECCurve+NamedCurves]::nistP256)
$pem = $ec.ExportPkcs8PrivateKeyPem()
```

Windows PowerShell 5.1, using the `openssl` from Git for Windows:

```powershell
& "C:\Program Files\Git\usr\bin\openssl.exe" genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -out ycr-dev-key.pem
$pem = Get-Content ycr-dev-key.pem -Raw
Remove-Item ycr-dev-key.pem
```

### 4.2 Store the settings

Use your own name in the key id. The `dev-` prefix and `DevelopmentOnly = true` are deliberate:
a Production start refuses such a key.

```powershell
cd src\YCR.Api
dotnet user-secrets set "Auth:Signing:ActiveKeyId" "dev-<you>"
dotnet user-secrets set "Auth:Signing:Keys:0:KeyId" "dev-<you>"
dotnet user-secrets set "Auth:Signing:Keys:0:DevelopmentOnly" "true"
dotnet user-secrets set "Auth:Signing:Keys:0:PrivateKeyPkcs8Pem" "$pem"
dotnet user-secrets set "Auth:AllowedOrigins:0" "http://localhost:5080"
dotnet user-secrets set "ConnectionStrings:Application" "Server=localhost,1433;Database=YCR;User Id=ycr_app;Password=<app password>;TrustServerCertificate=True;Current Language=us_english"
cd ..\..
```

`dotnet user-secrets list --project src/YCR.Api` shows what is stored. The allowed origin must be
the exact scheme, host and port you will open the API on (§5).

Settings you **cannot** change: the access-token lifetime (15 minutes), the session lifetime
(12 hours) and the refresh grace window (20 seconds) are fixed in code, and startup refuses a
configured value that differs (ADR-0023 item 8, review C-1). The rate limits in
`appsettings.json` (`Auth:RateLimits`) and `Auth:PrincipalCacheSeconds` (1 to 30) can be
changed.

---

## 5. Run the API

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5080"
dotnet run --project src/YCR.Api --no-launch-profile
```

Wait for `Now listening on: http://localhost:5080`. If it stops at startup instead, the first
error line names the setting it refused (a missing key, a key that is not P-256, no allowed
origin, a changed lifetime, `Time:LocalTimeZone`).

**Content root and the time zone (F-004).** The API and the Worker read `Time:LocalTimeZone`
(`Asia/Yangon`) from their shipped `appsettings.json` and **refuse to start** if it is missing or
this machine cannot resolve it (`docs/15` §F-004). `dotnet run --project src/YCR.Api` (and
`src/YCR.Worker`) runs the app with the **project directory** as working directory, which holds
that `appsettings.json`, so the commands in this guide work from the repository root. To run a
**built** host directly, start it from its output directory, as CI does:

```powershell
cd src/YCR.Api/bin/Debug/net10.0
dotnet YCR.Api.dll
```

Started from anywhere else (for example `dotnet src/YCR.Api/bin/Debug/net10.0/YCR.Api.dll` from the
repository root), the API reads no `appsettings.json` and stops with `Time:LocalTimeZone is not
configured`. The Worker's zone check and bootstrap command read the file next to `YCR.Worker.dll`
wherever they start, but run it from its output directory too. Windows resolves `Asia/Yangon`
through ICU; on Linux (WSL, containers) the `tzdata` package must be installed, and a plain
`-chiseled` .NET image has no zone data (`docs/15`).

What is served:

| URL | What | Environments |
|---|---|---|
| `http://localhost:5080/scalar` | interactive API reference (Scalar) | Development only |
| `http://localhost:5080/openapi/v1.json` | the OpenAPI document | Development only |
| `http://localhost:5080/health/live` | liveness, anonymous | all |
| `http://localhost:5080/health/ready` | readiness incl. the database, anonymous | all |
| `http://localhost:5080/api/v1/...` | the API (`docs/08`) | all |

Outside Development, `/scalar` and `/openapi/v1.json` answer 404 on purpose: the document maps
every route and permission.

---

## 6. Sign in and try the API

Open `http://localhost:5080/scalar`. Scalar sends requests from the same origin, so the browser
adds the right `Origin` header by itself.

1. **Sign in.** `POST /api/v1/auth/login`, body
   `{ "userName": "<name>", "password": "<bootstrap password>" }`. The response is
   `{ accessToken, expiresAtUtc }` plus the `ycr_refresh` cookie.
2. **Use the token.** In Scalar's Authentication panel choose Bearer and paste `accessToken`. If
   the panel is not offered, add the header `Authorization: Bearer <accessToken>` to each request.
3. **Change the password first.** `POST /api/v1/auth/password`, body
   `{ "currentPassword": "...", "newPassword": "..." }`, answers `204`. Until then every other
   endpoint except `GET /auth/me`, `POST /auth/logout` and `POST /auth/refresh` answers
   `403 Auth.PasswordChangeRequired` (U2).
4. **Check who you are.** `GET /api/v1/auth/me` returns your roles and permissions.
5. **Call the business endpoints.** For example `POST /api/v1/stations` with
   `{ "code": "YGN", "nameEn": "Yangon Central", "nameMy": "<Myanmar name>" }`, then
   `GET /api/v1/stations`.

The access token lives 15 minutes. Afterwards sign in again, or `POST /api/v1/auth/refresh`: the
browser sends the refresh cookie itself (the session lasts 12 hours from sign-in and a refresh
does not extend it).

### 6.1 More users

As the administrator, create a user for each role you want to try, for example a Ticket Operator:

`POST /api/v1/users` with `{ "userName": "operator.one", "password": "<12+ characters>", "roles": ["TicketOperator"] }`

The eight role identifiers are `SystemAdministrator`, `RailwayAdministrator`, `StationManager`,
`TicketOperator`, `TicketInspector`, `FinanceOfficer`, `Auditor`, `ReportingUser`; what each may
do is `docs/10`. A password an administrator sets is must-change, so that user also starts with
§6 step 3. No one can change their own roles, disable their own account or reset their own
password through the administration endpoints, and the last active `SystemAdministrator` cannot
be disabled or lose that role (U5).

### 6.2 Without a browser

`src/YCR.Api/YCR.Api.http` holds every request, with the `Origin` header already set, for VS Code
(REST Client), Rider or Visual Studio. Set `@host` and `@origin` to `http://localhost:5080` and
paste the access token into `@token`. Scripts and REST clients must send `Origin` on
`/auth/login`, `/auth/refresh` and `/auth/logout`, or they get `403 Auth.OriginRejected`.

---

## 7. Tests

### 7.1 Everything

Docker Desktop must be running: the database tests start their own pinned SQL Server container
through Testcontainers and never touch the `docker compose` database or `.env`.

```powershell
dotnet build YCR.sln
dotnet test YCR.sln
```

The whole suite takes a few minutes. There are six test projects:

| Project | What it covers | Needs Docker |
|---|---|---|
| `tests/YCR.Domain.Tests` | domain rules (stations, identity aggregates) | no |
| `tests/YCR.Application.Tests` | handlers against a real SQL Server | yes |
| `tests/YCR.Infrastructure.Tests` | migrations, constraints, grants, EF mapping, password and token services | yes |
| `tests/YCR.Api.Tests` | HTTP behaviour, real tokens, Origin, rate limits, security headers | yes |
| `tests/YCR.IntegrationTests` | the Worker's bootstrap command end to end | yes |
| `tests/YCR.ArchitectureTests` | module boundaries and forbidden dependencies | no |

### 7.2 One project

```powershell
dotnet test tests/YCR.Api.Tests
```

### 7.3 Trunk-only tests

A few slow tests (for example sequential-GUID fragmentation) run only when asked, as CI does on
pull requests and `main`:

```powershell
$env:YCR_RUN_TRUNK_ONLY_TESTS = "1"
dotnet test YCR.sln --no-build
Remove-Item Env:YCR_RUN_TRUNK_ONLY_TESTS
```

### 7.4 What CI runs

`.github/workflows/ci.yml`: build and test, the trunk-only tests, an API smoke test against the
built API in the Production environment (with a real bootstrap, forced password change and sign-in),
and a secret scan. Keep `dotnet test YCR.sln` green locally before pushing.

---

## 8. Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `ycr-sqlserver` `Exited (255)` seconds after start | `MSSQL_SA_PASSWORD` fails SQL Server's policy | stronger password (§2.1), then `docker compose down -v` and `up -d` |
| `ycr-sqlserver-init` `Exited (1)` | app or migrator password fails the policy, or contains `'` / `$` | fix `.env`, `docker compose up -d sqlserver-init` |
| `port is already allocated` on 1433 | a local SQL Server holds the port | `MSSQL_PORT=14330` in `.env`, `localhost,14330` in connection strings |
| `error during connect ... dockerDesktopLinuxEngine` | Docker Desktop is not running | start it and wait for "Engine running" |
| EF says `YCR_DESIGN_TIME_CONNECTION must be set` | the variable is missing in this window | §2.3 |
| API connects but every query fails with a permission error | §2.4 was skipped | re-run the init container |
| API stops at startup | a missing or rejected signing key, no allowed origin, or a changed lifetime | §4; the error line names it |
| API or Worker stops with `Time:LocalTimeZone is not configured` | the host was started outside its project or output directory, so its `appsettings.json` was not read | §5, Content root and the time zone |
| `… is not a time zone this host can resolve; install IANA time-zone data` | no ICU (Windows) or no `tzdata` (Linux) | install the zone data (`docs/15` §F-004) |
| `403 Auth.OriginRejected` | the request's `Origin` is not the allowed origin | open the API on exactly the origin in user-secrets, or send `Origin` |
| `403 Auth.PasswordChangeRequired` | first sign-in with a must-change password | §6 step 3 |
| `401 Auth.InvalidCredentials` on a correct password | the account is locked (10 failures lock it for 15 minutes) or disabled; the response is deliberately identical | wait 15 minutes, or have an administrator use `POST /users/{id}/unlock` |
| `429 Auth.TooManyRequests` | 5 sign-ins per minute per username, 20 per client address | wait a minute |
| `401` a few minutes after signing in | the 15-minute access token expired | sign in again or refresh |
| Bootstrap exits `1` with `Identity.PrivilegedRoleRequiresMfa` | `DOTNET_ENVIRONMENT` unset or `Production` | set `Development` (§3) |
| Bootstrap exits `1`: an administrator exists | only one bootstrap per database | sign in with that account, or §2.5 |
| Lost the only administrator's password | an administrator cannot reset their own password | §2.5 on a local database |
| Many database tests fail at once | Docker Desktop stopped during the run | restart it and re-run |

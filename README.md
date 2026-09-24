# Yangon Circular Railway Ticketing System

Production-oriented ticketing platform for Yangon Circular Railway (YCR), implemented with C# / ASP.NET Core 10 and SQL Server.

## Guiding principle

Treat railway rules as configurable domain policy. Never hard-code assumptions about current fares, schedules, station operations, or refund rules.

## Technology

- C# / ASP.NET Core 10
- Entity Framework Core
- SQL Server
- REST API
- Docker
- Automated testing
- Structured logging / OpenTelemetry-ready observability

## Repository

See `AGENTS.md` for the engineering constitution.

Start with `docs/00-project-specification.md`, then execute the discovery workflow in `docs/workflows/01-discovery.md`.

## Developer setup

```bash
cp .env.example .env            # then fill in the three passwords
docker compose up -d            # SQL Server 2022 for local development and integration tests
dotnet build YCR.sln
dotnet test YCR.sln             # the tests start their own SQL Server container; Docker must be running
```

### Signing key and allowed origin for the API (F-002)

The API signs access tokens with an ES256 (P-256) key and will not start without one. Each
developer generates their own; **no key is ever committed** (ADR-0023 item 7). Store it in
`dotnet user-secrets`, which the API reads in the `Development` environment only:

```bash
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -out ycr-dev-key.pem
cd src/YCR.Api
dotnet user-secrets set "Auth:Signing:ActiveKeyId" "dev-$(whoami)"
dotnet user-secrets set "Auth:Signing:Keys:0:KeyId" "dev-$(whoami)"
dotnet user-secrets set "Auth:Signing:Keys:0:DevelopmentOnly" "true"
dotnet user-secrets set "Auth:Signing:Keys:0:PrivateKeyPkcs8Pem" "$(cat ../../ycr-dev-key.pem)"
dotnet user-secrets set "Auth:AllowedOrigins:0" "http://localhost:5080"
rm ../../ycr-dev-key.pem        # the secret store holds it now
```

(PowerShell: `"$(Get-Content ..\..\ycr-dev-key.pem -Raw)"` for the key value.) The `dev-` key id
and `DevelopmentOnly: true` are deliberate: a production start refuses such a key (spec S26a).
`*.pem` is git-ignored.

`/auth/login`, `/auth/refresh` and `/auth/logout` reject a request whose `Origin` header is not
an allowed origin (R9), so scripts and REST clients must send `Origin` too (see
`src/YCR.Api/YCR.Api.http`). Outside Development every allowed origin must be `https`.

### The first administrator

No user is seeded. Create the first `SystemAdministrator` once, with the Worker, under the
application credential; the password is read from standard input (never pass it as an argument)
and must be changed at the first sign-in:

```bash
export ConnectionStrings__Application="Server=localhost,1433;Database=YCR;User Id=ycr_app;Password=...;TrustServerCertificate=True;Current Language=us_english"
dotnet run --project src/YCR.Worker -- bootstrap-administrator --username <name>
```

It exits `0` when it created the account, `1` when it refused (an administrator already exists, or
the username or password is invalid; nothing is written) and `2` on a usage error.

## Important

This starter contains architecture and requirements proposals, not verified operational rules for every YCR service. Validate operational details with Myanma Railways before production use.

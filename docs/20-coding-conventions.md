# Coding Conventions

Binding for humans and agents. It implements ADR-0012 to ADR-0018 and preserves the historical decisions they supersede. When code and this doc disagree, raise it; don't silently pick one.

## 1. Where things go

| Thing | Location | Namespace |
|---|---|---|
| Aggregate / entity / value object | `src/YCR.Domain/<Module>/` | `YCR.Domain.<Module>` |
| Module errors | `src/YCR.Domain/<Module>/<Module>Errors.cs` | ″ |
| Use case (command or query) | `src/YCR.Application/<Module>/<UseCase>/` | `YCR.Application.<Module>.<UseCase>` |
| Public module API for other modules | `src/YCR.Application/<Module>/Contracts/` | `YCR.Application.<Module>.Contracts` |
| Cross-module reads (ADR-0025) | Contract: an interface plus `sealed record` results in `src/YCR.Application/<Module>/Contracts/`, built only from primitives, `Guid`, `DateOnly`, `DateTimeOffset`, strings and other Contracts records — never a domain type, a context interface or an EF Core type. Implementation: an `internal sealed class` in `src/YCR.Application/<Module>/` (outside `Contracts`), reading through `I<Module>DbContext` with `AsNoTracking`, registered against the interface in `AddApplication`. Read-only. The architecture rule `ContractsMustNotDependOnModuleDomainOrContext` fails the build otherwise. Example: `INetworkReader` / `NetworkReader` | `YCR.Application.<Module>.Contracts` (contract); `YCR.Application.<Module>` (implementation) |
| Module persistence interface (ADR-0012 item 2) | `src/YCR.Application/<Module>/I<Module>DbContext.cs` | `YCR.Application.<Module>` |
| Audit subject constants and snapshot records (ADR-0021) | `src/YCR.Application/<Module>/` | ″ |
| EF configuration | `src/YCR.Infrastructure/Persistence/Configurations/<Module>/` | |
| Endpoints | `src/YCR.Api/Endpoints/<Module>/<Resource>Endpoints.cs` | `YCR.Api.Endpoints.<Module>` |
| Permissions | `src/YCR.Application/Common/Authorization/Permissions.cs` | |
| Tests | mirror the source path in the matching `tests/` project | |

Modules: `Identity, Network, Timetable, Fare, Ticketing, Payments, Operations, Reporting, Audit`.

## 2. Naming

| Kind | Pattern | Example |
|---|---|---|
| Use-case folder | Verb + Noun | `CreateStation`, `SellTicket`, `QuoteFare` |
| Command / query | `<UseCase>Command` / `<UseCase>Query` | `CreateStationCommand` |
| Handler | `<UseCase>Handler` | `CreateStationHandler` |
| API request / response | `<UseCase>Request` / `<Resource>Response` | `CreateStationRequest`, `StationResponse` |
| Validator | `<UseCase>RequestValidator` | |
| Error code | `<Module>.<Reason>` | `Network.StationCodeAlreadyExists` |
| Domain event | past tense | `TicketIssued` |
| Audit action | `<Module>.<Event>` | `Network.StationCreated` |
| Permission | `<resource>.<action>` lower-case | `stations.manage`, `refunds.approve` |
| DB schema | module, lower-case | `network`, `ticketing` |
| DB table | plural PascalCase | `network.Stations` |
| UTC column | `<Name>Utc` | `IssuedAtUtc` |
| Test method | `Method_State_ExpectedResult` | `Create_WithDuplicateCode_ReturnsConflict` |

**Authentication error codes (ENGINEERING DECISION, tech lead, hein, 2026-09-23; ADR-0023, F-002 D14):** the `Identity` module's error codes are split by prefix. Sign-in, refresh and token errors use **`Auth.<Reason>`** — consistent with ADR-0016's `Auth.RefreshSuperseded` — for example `Auth.InvalidCredentials`. User-administration errors use **`Identity.<Reason>`**, for example `Identity.UserNameAlreadyExists`. Audit actions stay `Identity.<Event>` (for example `Identity.LoginFailed`). `Auth` is an error-code prefix only, not a module. **The prefix follows the path (hein, 2026-09-23; ADR-0023 item 9, F-002 U1):** every error from an endpoint under `/auth/*` uses `Auth.` — including changing one's own password (`Auth.PasswordRejected`, `Auth.CurrentPasswordIncorrect`) — and every error from an endpoint under `/users/*` uses `Identity.` (`Identity.PasswordRejected`). The authentication pipeline's own rejections (`Auth.Unauthenticated`, `Auth.PasswordChangeRequired`) keep `Auth.` on every path.

`*Utc` fields are required to contain UTC values (`DateTimeOffset.Offset == TimeSpan.Zero`).
Domain factories reject non-UTC offsets, and SQL Server persistence adds a matching check
constraint where the column is created. Test methods may use `Method_ExpectedResult` when there is
no meaningful setup state; use `Method_State_ExpectedResult` when a meaningful state exists.

Use the vocabulary in `docs/glossary.md` (to be created). Don't introduce synonyms.

## 3. Reference slice — `CreateStation`

Every new slice is modelled on this one. It is illustrative until the walking skeleton exists. Station field rules (code format, required names) are settled by a final tech-lead ruling, not a Myanma Railways answer (OQ26/OQ27, resolved by T-014, hein 2026-09-22; see `docs/19-open-questions.md`).

### Domain — `YCR.Domain/Network/Station.cs`

```csharp
namespace YCR.Domain.Network;

public sealed class Station : AggregateRoot
{
    public StationCode Code { get; private set; } = null!;
    public BilingualName Name { get; private set; } = null!;   // English + Myanmar (Unicode)
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Station() { } // EF

    public static Station Create(Guid id, StationCode code, BilingualName name, DateTimeOffset nowUtc) =>
        new() { Id = id, Code = code, Name = name, IsActive = true, CreatedAtUtc = nowUtc };

    public Result Deactivate()
    {
        if (!IsActive) return NetworkErrors.StationAlreadyInactive;
        IsActive = false;
        Raise(new StationDeactivated(Id));
        return Result.Success();
    }
}
```

### Errors — `YCR.Domain/Network/NetworkErrors.cs`

```csharp
public static class NetworkErrors
{
    public static readonly Error StationAlreadyInactive =
        Error.BusinessRule("Network.StationAlreadyInactive", "Station is already inactive.");
    public static Error StationCodeAlreadyExists(string code) =>
        Error.Conflict("Network.StationCodeAlreadyExists", $"Station code '{code}' already exists.");
}
```

### Use case — `YCR.Application/Network/CreateStation/`

```csharp
public sealed record CreateStationCommand(string Code, string NameEn, string NameMy);

public sealed class CreateStationHandler(
    INetworkDbContext db, IIdGenerator ids, IAuditWriter audit, TimeProvider clock)
{
    public async Task<Result<Guid>> Handle(CreateStationCommand cmd, CancellationToken ct)
    {
        var code = StationCode.Create(cmd.Code);
        if (code.IsFailure) return code.Error;

        var name = BilingualName.Create(cmd.NameEn, cmd.NameMy);
        if (name.IsFailure) return name.Error;

        if (await db.Stations.AnyAsync(s => s.Code == code.Value, ct))
            return NetworkErrors.StationCodeAlreadyExists(cmd.Code);

        var station = Station.Create(ids.New(), code.Value, name.Value, clock.GetUtcNow());
        db.Stations.Add(station);
        // Subject and payload are separate, and the payload is an explicit snapshot record, never
        // the aggregate: an audit row cannot be corrected, so it must not carry a shape that will
        // grow fields nobody reviewed (ADR-0021; hein's ruling, 2026-09-20).
        audit.Record(
            "Network.StationCreated",
            NetworkAuditSubjects.Station,
            station.Id,
            before: null,
            after: StationAuditSnapshot.From(station));

        await db.SaveChangesAsync(ct);   // unique index still guards the race → mapped to Conflict
        return station.Id;
    }
}
```

### Endpoint — `YCR.Api/Endpoints/Network/StationEndpoints.cs`

```csharp
public sealed record CreateStationResponse(Guid Id);

public static class StationEndpoints
{
    public static RouteGroupBuilder MapStationEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/stations").WithTags("Stations");

        g.MapPost("/", async (CreateStationRequest req, CreateStationHandler h, CancellationToken ct) =>
                (await h.Handle(req.ToCommand(), ct))
                .ToHttpResult(id => TypedResults.Created($"/api/v1/stations/{id}", new CreateStationResponse(id))))
         .RequireAuthorization(Permissions.StationsManage)
         .AddEndpointFilter<ValidationFilter<CreateStationRequest>>()
         .WithName("CreateStation");

        return g;
    }
}
```

### EF configuration — `YCR.Infrastructure/Persistence/Configurations/Network/StationConfiguration.cs`

```csharp
public sealed class StationConfiguration : IEntityTypeConfiguration<Station>
{
    public void Configure(EntityTypeBuilder<Station> b)
    {
        b.ToTable("Stations", "network");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();                 // ADR-0006
        b.Property(x => x.Code).HasConversion(c => c.Value, v => StationCode.From(v)).HasMaxLength(10);
        b.HasIndex(x => x.Code).IsUnique();
        b.OwnsOne(x => x.Name, n =>
        {
            n.Property(p => p.En).HasColumnName("NameEn").HasMaxLength(100);
            n.Property(p => p.My).HasColumnName("NameMy").HasMaxLength(100);   // nvarchar, Unicode
        });
        b.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset(3)");
    }
}
```

### Tests (required for every slice)

| Project | Test |
|---|---|
| `YCR.Domain.Tests` | invariants and state transitions (`Deactivate_WhenInactive_ReturnsError`) |
| `YCR.Application.Tests` | handler behaviour against real SQL Server (Testcontainers `mssql/server:2022`), including the duplicate-code conflict |
| `YCR.Api.Tests` | `WebApplicationFactory`: 201 happy path, 400 validation, **401 anonymous, 403 wrong permission**, ProblemDetails shape |
| `YCR.ArchitectureTests` | boundary rules from ADR-0012 (these run globally, not per slice) |

## 4. API rules

- Base path `/api/v1`. JSON camelCase. Resource IDs are GUIDs.
- Collections are paginated: `?page=1&pageSize=50` (max 200), and the response includes `items, page, pageSize, totalCount`.
- Errors: ProblemDetails with `errorCode` and `traceId` (ADR-0004).
- Versioned policy (fares, timetables): create a version, then publish it. **Never PATCH a published version.** For fares this is ADR-0002, which covers fares only; timetables follow the same rule by this project's convention (this section, `docs/07` §Rules "Preserve historical fare/schedule versions", `docs/08` §Initial resources), as F-005 implements it (F-005 spec R26, R32; C4).
- Never return EF entities. Map to `*Response` records explicitly (no AutoMapper).
- Every endpoint has `.RequireAuthorization(<permission>)` or an explicit `.AllowAnonymous()` with a comment saying why. **Third case (ENGINEERING DECISION, tech lead, hein, 2026-09-23; T-023, F-002 D17):** a self-service endpoint that any authenticated user may call, and that acts only on the caller's own account or session (for example `GET /auth/me`, `POST /auth/logout`, `POST /auth/password`), may use `.RequireAuthorization()` with no permission, **only** with a comment saying why. It is never a shortcut for an endpoint that reads or changes anyone else's data; that needs a permission.

## 5. Idempotency (financial and retryable commands)

- Required on: sell ticket, cancel, refund request/approve, record payment, open/close cashier session.
- Header: `Idempotency-Key: <uuid>`, generated by the client **once per user intent** and reused on retries.
- Keys are bound to the caller's open cashier session. Retention is the maximum cashier-session length plus an operational margin; retry after session close returns `409 Operations.SessionClosed`.
- Stored in `IdempotencyRecords`: key + user id + cashier session id + endpoint (unique together), request hash, final response status + body, and `CreatedAtUtc`.
- The record is written in the same transaction as the business change. A record means committed and replays the stored response; no record means the operation did not commit or rolled back and a same-key retry is safe. An in-flight duplicate returns `409 Idempotency.InProgress` using locking/uniqueness, not a persisted pending state.
- Store final 4xx business outcomes; never store 5xx responses. Same key + different body returns `422 Idempotency.KeyReused`.
- The idempotency record is saved in the **same transaction** as the business change.
- Recovery is server-authoritative through `GET /api/v1/cashier-sessions/current/recent-operations`. An optional idempotency lookup is scoped to the authenticated user and current session.
- IndexedDB is best-effort only and may store the key plus non-personal request fields; clear it on resolution and logout.

## 6. Data and persistence

- Migrations: one per change, named `yyyyMMddHHmmss_<Module>_<Change>`, reviewed per `workflows/04-database-change.md`. Never edit a migration that has already been applied. (Corrected from `YYYYMMDD_<Module>_<Change>` at F-001 stage 8: EF Core generates the full timestamp, a date-only prefix collides whenever two migrations land on one day, and EF's ordering depends on the time part. The `_<Module>_<Change>` half is what we choose and what the rule is really about.)
- Money: `decimal(18,2)` + `char(3)` currency (ADR-0018). Dates: `date`. Instants: `datetimeoffset(3)` with UTC values for `*Utc` fields.
- Times of day (ADR-0027, Accepted 2026-09-26; ENGINEERING DECISION, tech lead, hein): a timetable time is the value object `TimetableTime`, whole minutes after local midnight of the service's operating date, stored as `smallint` with a check bounding it to `0`–`1439` while no service may run past midnight (OQ53 provisional ruling), and exchanged as exactly `HH:mm`. Never `TimeOnly`/`time`, never an instant, and no module adds `+06:30` itself.
- Text: `nvarchar` for anything a person may type or read, including Myanmar Unicode. Never store Zawgyi.
- Concurrency: aggregates that can be changed at the same time (Ticket, CashierSession, Refund) have a `rowversion` concurrency token.
- **When a handler may open its own transaction (ADR-0026, Accepted 2026-09-26; ENGINEERING DECISION, tech lead, hein, 2026-09-25, F-004 plan ruling Q2).** The default stays one `SaveChangesAsync` per handler (ADR-0004), and a rule that one unique index can enforce uses the index. A handler may open its own transaction **only** to guard a **set invariant across rows that one unique index cannot enforce** (an overlap, a count, a minimum), and then exactly like this: begin a transaction on the module context → take an exclusive, **transaction-owned** `sp_getapplock` through a module Application abstraction (`I<Module><Concept>Lock`), scoped to the **smallest key that covers the invariant** and named `<schema>.<Concept>` or `<schema>.<Concept>:<key>` (the key from a validated value object, passed as a parameter) → read everything that decides → let the domain decide → add or change and audit → **one `SaveChangesAsync`** → commit. Reads that do not decide (for example another module's contract) happen before the transaction. The lock timeout is **30 s**; a timeout or deadlock is an unexpected failure (the opaque `500`, ADR-0004), never a business error. Uses: `identity.SystemAdministrators` (F-002 R27); `timetable.ServiceCode:<code>` (F-004 R35), taken by service create and service withdrawal; and `timetable.ScheduleVersions` (F-005 R46, a whole-set lock), taken by timetable-version create, publish, discard and cancel **and by service withdrawal**. **A handler that takes two locks** takes them in one fixed order, the keyed lock before the whole-set lock: service withdrawal takes `timetable.ServiceCode:<code>` first, then `timetable.ScheduleVersions`; no holder of `timetable.ScheduleVersions` ever requests a service-code lock, and no handler takes two code locks, so the two cannot deadlock (F-005 plan P9 and its proof; `docs/07` §F-005 §The Timetable-wide lock). A new lock use states where it sits in this order.
- Financial history is never updated in place. Corrections are new rows (reversal, adjustment).

## 7. Logging and audit

- Use `ILogger` with message templates (no string interpolation). Correlation ID comes from `traceparent`.
- Never log: passwords, tokens, refresh cookies, full QR payloads, private keys, or personal data.
- Business-significant actions call `IAuditWriter` (ADR-0017). Logs are not audit.

## 8. Things agents must not do

- Invent fares, station codes, validity windows, refund rules or role rights. Add an OPEN QUESTION to `docs/19` and stop.
- Add NuGet packages without stating why in the plan.
- Use `DateTime.Now`, `DateTime.UtcNow`, `Guid.NewGuid()` or `new Random()` in domain or application code. Use `TimeProvider`, `IIdGenerator` and injected services.
- Compute a local calendar date any other way than `ILocalCalendar.Today()`, or hard-code a zone or an offset such as `+06:30`. "Today" is `TimeProvider.GetUtcNow()` converted to the zone in `Time:LocalTimeZone` (the IANA id `Asia/Yangon`, in both hosts' `appsettings.json`, validated at startup; ADR-0018 §Time, F-004 plan P11). Handlers pass the date into the domain.
- Catch-and-ignore exceptions, or add `[Skip]` or `#pragma` to silence failing tests or analyzers.

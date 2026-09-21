# Review: F-001 Walking skeleton — codex's part of stage 4

Reviewer: **claude** (review-agent and security-agent). Tasks **T-006b** and **T-007b**.

Commit / PR: reviewed **at `9106d53`** (`feature/F-001`, [PR #1](https://github.com/heinmindev/ycr/pull/1)).

**Scope — codex's commits only.** `TASKS.md` T-004 Notes record the authorship split: plan steps 1 and 7–13 are claude's, plan **steps 2–6 and the step-5 review fixes are codex's, up to `89551cb`**. §Protocol item 11 forbids an agent reviewing its own work, so this review is the codex half of stages 6 and 7. The claude half (steps 1, 7–13) stays with **T-006** and **T-007** and needs a different reviewer. **T-005 (scenario tests) has not run**; this review was ordered ahead of it, so scenario coverage is out of scope here.

Commits in range (`0572ff3..89551cb`; all are committed under the `heinmindev` git identity, so authorship is taken from `progress.md`, not from `git author`):

| SHA | Subject | Plan step |
|---|---|---|
| `80b7419` | feat(F-001): implement domain common primitives | 2 |
| `76a1214` | docs(F-001): record domain common checkpoint | 2 |
| `21d236b` | feat(F-001): implement station domain slice | 3 |
| `67bb0f3` | docs(F-001): record network domain checkpoint | 3 |
| `113eef6` | docs(F-001): record step-4 engineering blocker | 4 |
| `2f25d3b` | chore(F-001): apply EF boundary ruling | 4 |
| `704d872` | feat(F-001): add application contracts | 4 |
| `66bc844` | docs(F-001): record application contracts checkpoint | 4 |
| `5e901f7` | feat(F-001): add station persistence | 5 |
| `c8efd48` | docs(F-001): record persistence checkpoint | 5 |
| `27c9922` | test(F-001): enforce architecture boundaries | 6 |
| `de881b6` | docs(F-001): record architecture checkpoint | 6 |
| `9a09b36` | test(F-001): harden architecture rules | step-5 review fixes |
| `89551cb` | docs(F-001): record step 7 environment blocker | — |

Files in scope at `9106d53` (codex-authored and unchanged since `89551cb` unless noted):

`src/YCR.Domain/Common/*` · `src/YCR.Domain/Network/*` (`NetworkErrors.cs` gained `InvalidPageRequest` at step 10 — claude's, excluded) · `src/YCR.Domain/Properties/AssemblyInfo.cs` · `src/YCR.Application/Common/Abstractions/IIdGenerator.cs` · `src/YCR.Application/Common/Authorization/Permissions.cs` · `src/YCR.Application/Common/Pagination/PagedResult.cs` · `src/YCR.Application/Network/INetworkDbContext.cs` · `src/YCR.Infrastructure/Persistence/YcrDbContext.cs` (original body only; the `SaveChangesAsync` translation and the internal `AuditEvents` set are claude's, step 9) · `Configurations/Network/StationConfiguration.cs` · `Persistence/YcrDbContextFactory.cs` · `Persistence/Migrations/20260920064342_Network_CreateStations*.cs` · `Identifiers/SqlServerSequentialGuidIdGenerator.cs` · `DependencyInjection.cs` (original body only) · `tests/YCR.ArchitectureTests/ArchitectureRules.cs` · `ArchitectureRuleTests.cs` (original body only; the audit-snapshot facts are claude's) · `Violations/*` except `AuditSnapshotViolations.cs` and `DomainAuditSnapshot.cs` · `tests/YCR.Domain.Tests/**` · `tests/YCR.Infrastructure.Tests/Persistence/StationModelTests.cs` and `ModuleInterfacesTests.cs` (both later revised by claude — see C-10).

Docs read for this review: `AGENTS.md`, `docs/20-coding-conventions.md`, `docs/21-definition-of-done.md`, `docs/05`, `docs/07`, `docs/09`, `docs/10`, `docs/18`, `docs/19`, `docs/workflows/02-feature-development.md`, `docs/workflows/04-database-change.md`, ADR-0004, ADR-0006, ADR-0012, ADR-0017, ADR-0018, ADR-0019, ADR-0020, ADR-0021, and the feature's `spec.md` and `plan.md`.

---

## Commands run for this review

| Command | Result |
|---|---|
| `dotnet build YCR.sln` | **Build succeeded. 0 Warning(s), 0 Error(s)** (12.06 s). `TreatWarningsAsErrors` is on, so this is a real zero-warning result. |
| `dotnet test tests/YCR.Domain.Tests/YCR.Domain.Tests.csproj --no-build` | **Passed. total 30, failed 0, succeeded 30, skipped 0** |
| `dotnet test tests/YCR.ArchitectureTests/YCR.ArchitectureTests.csproj --no-build` | **Passed. total 12, failed 0, succeeded 12, skipped 0** (8.6 s) |
| `dotnet test tests/YCR.Infrastructure.Tests --filter-class …StationModelTests --filter-class …ModuleInterfacesTests` | **Passed. total 3, failed 0, succeeded 3, skipped 0** (34.2 s, against the pinned SQL Server 2022 container) |

Whole-suite evidence is CI's, recorded in T-004 Notes: push run [35577574172](https://github.com/heinmindev/ycr/actions/runs/35577574172) and pull_request run [35577575935](https://github.com/heinmindev/ycr/actions/runs/35577575935) at `9106d53` — 147 passed / 0 skipped on the branch job, 4 passed on the trunk-only job. I did not re-run the whole suite locally; the trunk-only fragmentation test is excluded from the branch filter and is claude's step 12.

No `[Skip]`, `#pragma warning disable` or `Assert.True(true)` exists in any file in scope (`grep` over `src/` and `tests/`, excluding `obj/`). AGENTS.md rule 7 holds.

---

## Definition of Done check

Scoped to codex's range. Items owned entirely by claude's steps 1 and 7–13 are marked **out of range** and belong to T-006 / T-007.

### Specification

| Item | Verdict | Evidence |
|---|---|---|
| Spec exists and was approved by a human | ✓ | `spec.md` line 3: "Approved (hein, 2026-09-20)", subject to the §Blocked behaviour waiver |
| Every rule labelled FACT / BUSINESS / ENGINEERING DECISION; no unresolved OQ affects implemented behaviour | ✓ | `spec.md` §3 R1–R12. The provisional R3/R4/R8 carry the waiver label verbatim in the code: `StationCode.cs:9-12`, `BilingualName.cs:8-11`, `Permissions.cs:5-8`. Codex confined them to exactly the three files the waiver names and added no fourth. |
| Deferred behaviour listed in `Blocked behaviour` with no placeholder implementation | ✓ | `spec.md` §Blocked behaviour; T-014 is the release gate. Nothing in the range implements a rule that is not in §3. |

### Code

| Item | Verdict | Evidence |
|---|---|---|
| Follows `docs/20` and all Accepted ADRs | ✓ with findings | `Station`, `NetworkErrors` and `StationConfiguration` match `docs/20` §3's reference slice line for line, including `ValueGeneratedNever()`, the `StationCode` conversion, the owned `BilingualName` with `NameEn`/`NameMy`, and `datetimeoffset(3)`. `ErrorType` carries exactly ADR-0004's six categories. Deviations: **C-3**, **C-6**, **C-8** below. |
| No domain logic in endpoints; no EF entities in API responses | N/A in range | No endpoint or response type is codex's. |
| Architecture tests pass | ✓ | 12/12, run above |
| Architecture tests include negative cases for forbidden module context/domain dependencies **and the reporting read-only context** | ✓ | `Violations/NetworkUsingTicketingContext.cs`, `NetworkUsingTicketingDomain.cs`, `AdditionalBoundaryViolations.cs` (`ReportingUsingNetworkContext`, `DomainUsingOtherModule`, `DomainUsingApplication`, `DomainUsingEntityFrameworkCore`, `ApplicationUsingInfrastructure`), `ApiUsingNetworkDomain.cs`, `ApiUsingAggregateRoot.cs`, `ApplicationUsingSqlServerProvider.cs`, `SourceUsingAuthenticationHandler.cs`. Each is asserted through `AssertRuleProtectsFixture` (`ArchitectureRuleTests.cs:238-247`), which checks both halves: clean on `SourceArchitecture`, **and** violated on `ViolationsArchitecture` **with the fixture named in the failure text**. That last assertion is what makes this more than a smoke test — a rule that failed for an unrelated reason would not satisfy it. Coverage gap: **C-2**. |

### Tests

| Item | Verdict | Evidence |
|---|---|---|
| Domain tests for every invariant and state transition touched | ✓ | `StationTests` (create-active; deactivate raises the event; deactivate-when-inactive returns the error **and raises no second event**, `StationTests.cs:50`), `StationCodeTests` (trim, too short, too long, lower case, three punctuation cases, blank, whitespace), `BilingualNameTests` (trim, each name missing, whitespace-only, 101 characters on each side), `ResultTests`, `ErrorTests`, `AggregateRootTests`. 30 tests, all passing. R3 and R4 are each fully covered in the single file that owns them. |
| Handler integration tests against SQL Server for happy path and each error code | N/A in range | No handler is codex's (step 10, claude) |
| API tests for 401, 403, 400 | N/A in range | Step 11, claude |
| At least one test runs the unmodified production composition | N/A in range | See §Production-composition check |
| Idempotency / concurrency tests for financial or retryable commands | N/A | R11: station management is neither |
| `dotnet test` fully green, no skipped tests added | ✓ | 30 + 12 + 3 above, 0 skipped; CI 147 / 0 at `9106d53` |

### Security

Covered in §Security review below.

### Data

| Item | Verdict | Evidence |
|---|---|---|
| Migration reviewed per `workflows/04-database-change.md`; upgrade tested on a copy of the current schema | ✓ | `20260920064342_Network_CreateStations.cs`. The table is new, so there is no existing schema to upgrade; the migration is applied from empty against the pinned container by the fixture on every integration run (verified above). |
| SQL Server 2022-specific DDL exercised in CI against the pinned image | N/A in range | The ledger DDL is claude's step 8 |
| Constraints and indexes deliberate and documented in `docs/07` | ✗ (deferred) | The unique index is deliberate and **named** `UX_Stations_Code` rather than left to EF's default — correct, because claude's step-9 translator matches on that name. `docs/07` has not been updated; `plan.md` §Steps assigns all documentation to stage 8, so this is scheduled work, not an omission by codex. Tracked by T-008. |

### Observability and audit

| Item | Verdict | Evidence |
|---|---|---|
| Audit event written for security-sensitive actions | N/A in range | Codex delivered the `IAuditWriter` abstraction only; the writer and the call sites are steps 8 and 10. The abstraction as codex shipped it is **C-9**. |
| Metrics and log events added where `docs/17` requires them | N/A in range | No logging surface in range |

### Documentation

| Item | Verdict | Evidence |
|---|---|---|
| Affected docs updated in the same change | ✗ (deferred to stage 8 by the approved plan) | `plan.md` §Steps: "Documentation updates (`docs/07`, `docs/08`, `docs/20` §3, `docs/20` §6's migration-naming correction, the glossary contribution) belong to **stage 8**, not here." |
| ADR added if an architectural decision was made | ✓ | Codex made no architectural decision on its own. At step 4 it hit the missing `Microsoft.EntityFrameworkCore` reference for `INetworkDbContext`, **stopped, recorded an ENGINEERING BLOCKER in `progress.md` (`113eef6`), and waited for a tech-lead ruling** instead of adding the package. That is exactly AGENTS.md's stop-and-ask behaviour. |
| `review.md` records the review outcome | ✓ | This file, for the codex half |
| Review evidence names the command, test or document section for every checked item | ✓ | Above |

### Approval

Out of range — T-009.

---

## Production-composition check

| Test | What it hosts unmodified | Evidence |
|---|---|---|
| `tests/YCR.Api.Tests/Common/MissingSchemeTests.cs` | The real `Program` composition with **no** `ConfigureTestServices` override and no test authentication handler, so an anonymous request goes through the shipping pipeline | `docs/21` §Tests names it as the reference for this item |
| `tests/YCR.Infrastructure.Tests/Persistence/ModuleInterfacesTests.cs` | The unmodified `AddInfrastructure(...)` registration graph against the pinned container under the `ycr_app` credential — no substituted `DbContext`, no in-memory provider | Run above: 3/3 passed in 34.2 s. This is the test that proves ADR-0012 item 3 in the composition that ships. |

`MissingSchemeTests` is claude's (step 11), so it is not evidence *this* review produces; it is named because the template requires the item to be answered and because its absence would be a blocking finding. `ModuleInterfacesTests` is codex's, in the form claude later moved onto the real container (**C-10**). **There is at least one such test. No blocking finding here.**

---

## Findings

Severity is measured against `docs/21` and the Accepted ADRs, not against reviewer preference. **No finding in this range is Critical or High, and none blocks stage 8.**

| # | Severity | Finding | Evidence (file:line, test) | Recommended action | Status |
|---|---|---|---|---|---|
| **C-1** | Medium | **Nothing enforces that `CreatedAtUtc` is actually UTC.** `Station.Create` takes any `DateTimeOffset`, `datetimeoffset(3)` preserves whatever offset it is given, and no value object, aggregate guard, check constraint or test rejects a non-zero offset. ADR-0018 §Time and spec R12 require UTC values for `*Utc` fields, and `docs/20` §2 makes the `*Utc` suffix mean exactly that. The only current caller is correct, so nothing is wrong in the database today — but this is the reference slice every later aggregate is copied from, and the next one may reach for `GetLocalNow()` in an Asia/Yangon deployment. The failure would be silent and would corrupt report ordering rather than throw. | `src/YCR.Domain/Network/Station.cs:19-32` (no guard); `Configurations/Network/StationConfiguration.cs:40-42` (no constraint); `tests/YCR.Domain.Tests/Network/StationTests.cs:8` (only `TimeSpan.Zero` is ever passed); correct caller at `src/YCR.Application/Network/CreateStation/CreateStationHandler.cs:52` (`clock.GetUtcNow()`) | Guard in `Station.Create` — reject a non-zero `Offset`, or normalise with `ToUniversalTime()` — and add a domain test that passes `+06:30`. Reject-vs-normalise is an **ENGINEERING DECISION** the tech lead owns: ADR-0018 states the rule but not the enforcement point. | Fixed at `700500a` |
| **C-2** | Low | **The shared-kernel namespaces are never the subject of a module-boundary rule.** `DomainModuleMustNotDependOnOtherDomains(m)` constrains only `YCR.Domain.<m>`, and `ApplicationModuleMayDependOnlyOnAllowedTypes(m)` only `YCR.Application.<m>`; `Common` appears in both rules only as an *allowed target*. So `YCR.Domain.Common → YCR.Domain.Network` and `YCR.Application.Common → YCR.Application.Ticketing` would both pass CI. The layer rules (`DomainMustNotDependOnApplicationInfrastructureApi`, `ApplicationMustNotDependOnInfrastructureOrApi`) do take `Common` as a subject, so the *layer* direction is protected; the *module* direction is not. ADR-0012 item 4 does not name Common as a subject, so this is a coverage gap rather than a rule breach, and there is no violation in the code today. | `tests/YCR.ArchitectureTests/ArchitectureRules.cs:15-46` (module rules — subjects are `YCR.Application.<m>` / `YCR.Domain.<m>` only) vs `:48-71` (layer rules, whose subjects do include Common) | Add `CommonMustNotDependOnAnyModule` with a `Violations` fixture, and record the intent as a clarification to ADR-0012 item 4: a shared kernel that may reach into one module couples all nine. | Fixed at `e8ae962` |
| **C-3** | Low | **`Network_CreateStations.Down()` does not drop the `network` schema, which the approved plan says it does.** `plan.md` §DB changes: "`Down()` drops the table and schema." The migration drops only the table, leaving an orphan empty schema after a rollback. Operationally harmless; the problem is that the approved plan and the shipped migration disagree, and AGENTS.md rule 8 is about database behaviour never diverging silently. | `src/YCR.Infrastructure/Persistence/Migrations/20260920064342_Network_CreateStations.cs:43-48` vs `plan.md` §DB changes → `<ts>_Network_CreateStations` | Either add `migrationBuilder.DropSchema("network")` to `Down()`, or correct that plan line at stage 8 and say why. Per `docs/20` §6 the migration has not been applied to any persistent environment, so editing it is still permitted. | Fixed at `c79afcc` |
| **C-4** | Low | **`SqlServerSequentialGuidIdGenerator` passes `null!` as the `EntityEntry`.** `_generator.Next(null!)` works only because EF's current `SequentialGuidValueGenerator` ignores the argument — undocumented behaviour of a public API whose parameter is declared non-null. An EF minor upgrade that started reading the entry would throw `NullReferenceException` on every insert. The *choice* is right: ADR-0006's 2026-09-19 amendment names this exact wrapping. Mitigation already in place: `SequentialGuidGenerator_ReturnsDistinctNonEmptyIds` and the S23 fragmentation test both call `New()`, so the break lands in CI, not in production — which is why this is Low rather than Medium. | `src/YCR.Infrastructure/Identifiers/SqlServerSequentialGuidIdGenerator.cs:10`; `tests/YCR.Infrastructure.Tests/Persistence/StationModelTests.cs:41-51` | Add a comment naming the EF version the `null` argument was verified against (10.0.12), so an upgrade review has something to check; or produce the SQL-Server-ordered bytes directly, which ADR-0006 item 4 also permits ("a v7 generator with bytes reordered for SQL Server"). | Fixed at `b3d7ce0` |
| **C-5** | Low | **`AggregateRoot.DomainEvents` is excluded from the EF model by convention alone.** There is no `builder.Ignore(s => s.DomainEvents)` anywhere and no test asserts the exclusion. It is genuinely excluded today — `YcrDbContextModelSnapshot` maps exactly the five `Station` columns and no navigation — but the intent is nowhere stated, and this is the pattern every later aggregate inherits. | `src/YCR.Domain/Common/AggregateRoot.cs:7`; no `Ignore` in `StationConfiguration.cs`; `Migrations/YcrDbContextModelSnapshot.cs:25-48` | Add the explicit `Ignore` (or a model-building convention over `AggregateRoot`), and assert that `Station` has no navigations in `StationModelTests`. | Fixed at `e57a3a2` |
| **C-6** | Low | **Test method names do not follow `docs/20` §2's `Method_State_ExpectedResult`.** Two-segment names throughout `YCR.Domain.Tests/Common`, `StationModelTests` and all of `ArchitectureRuleTests`: `Success_HasNoError`, `Failure_CarriesError`, `Error_UsesValueEquality`, `FactoryMethods_AssignTheExpectedErrorType`, `Raise_CollectsDomainEventWithoutDispatchingIt`, `Model_MapsStationTableAndCodeIndex`, `SequentialGuidGenerator_ReturnsDistinctNonEmptyIds`, `ApplicationModuleAllowlist_DetectsForeignContextAndDomainFixtures`, and seven more. The `Network` domain tests **do** follow it (`Create_WithLowerCaseCode_ReturnsValidationError`), which makes the split worse rather than better: a later agent copying this skeleton finds two contradictory examples in the same solution. | `tests/YCR.Domain.Tests/Common/ResultTests.cs:8,18,30,38,48`; `ErrorTests.cs:8,18,27`; `AggregateRootTests.cs:8`; `tests/YCR.Infrastructure.Tests/Persistence/StationModelTests.cs:12,41`; `tests/YCR.ArchitectureTests/ArchitectureRuleTests.cs:44,55,69,77,85,96,104,112,123,136,167,181` — against `docs/20` §2 "Test method \| `Method_State_ExpectedResult`" | Pick one and make the skeleton consistent: rename at stage 8, or amend `docs/20` §2 to permit a two-segment form where there is no meaningful state (`Success_HasNoError` has none). A silent split is the one outcome to avoid. | Fixed at `2ec9708` |
| **C-7** | Low | **`ReportingMustUseOnlyReadContext` carries a clause that can never bite.** `DoNotHaveFullName("YCR.Application.Reporting.IReportingReadContext")` excludes a name the preceding pattern `^YCR\.Application\..*\.I.*DbContext$` could not have matched — `IReportingReadContext` does not end in `DbContext`. Harmless today, and the rule does catch the real case (the `ReportingUsingNetworkContext` fixture passes). It matters because it reads as a deliberate, load-bearing carve-out, so nobody will re-derive it: if Reporting's interface were ever renamed to `IReportingDbContext` — which ADR-0012 item 5 does not forbid — the carve-out would suddenly be needed, and the rule's `And`/`Or` sequencing would have to be re-checked at the moment it stopped being dead code. | `tests/YCR.ArchitectureTests/ArchitectureRules.cs:73-81` | Either delete the clause, or widen the pattern to `^YCR\.Application\..*\.I.*(DbContext\|ReadContext)$` so the exclusion is live and tested. | Fixed at `9e9f164` |
| **C-8** | Low | **`YcrDbContextFactory` hard-codes a LocalDB target and is not in the approved plan's file inventory.** The factory is genuinely required for `dotnet ef migrations add`, so it should exist — but `plan.md` §Affected modules and files lists no such file, and a design-time factory with a working default makes `dotnet ef database update` a single command on a developer box, running DDL under that developer's own LocalDB identity. E7 and spec S22 govern the *application login*, not a developer, so this breaches neither; it is an undocumented path around "migrations are a separate step under the migrator credential". No credential is embedded, so there is no secret-scanning concern. The same applies to `src/YCR.Domain/Properties/AssemblyInfo.cs`, also absent from the inventory (see §Security review, "Widened assembly visibility"). | `src/YCR.Infrastructure/Persistence/YcrDbContextFactory.cs:10-14`; `src/YCR.Domain/Properties/AssemblyInfo.cs:3`; `plan.md` §Affected modules and files (both absent) | Record both files and their reasons in `plan.md` / `docs/07` at stage 8. Consider reading the design-time target from an environment variable with **no** default, so `migrations add` still works and `database update` cannot succeed by accident. | Fixed at `0c3d0fd` |
| **C-9** | Low (already remediated) | **The `IAuditWriter` codex shipped let a handler name its own authority and pass an aggregate as the payload.** As written at `704d872`: `Record(string action, object subject, object? before, object? after, string? authorizedByPermission = null, string? reasonCode = null)`. Two problems: `object` payloads invite passing the aggregate straight into an append-only row — exactly what `docs/20` §3 line 104 illustrates and what hein's later ruling forbids — and a caller-supplied `authorizedByPermission` is a claim that becomes unfalsifiable the moment it is written to a table that cannot be edited. ADR-0017 item 2 names only `ActorUserId`/`ActorRole` as server-derived, so this was not a breach of the ADR as written; it was a design weakness the ADR had not yet closed, in a step whose plan text did not specify the shape. | `git show 704d872:src/YCR.Application/Common/Abstractions/IAuditWriter.cs`; closed by hein's ruling and implemented at `6dfb67a` / `b6bf336` — the current interface takes `IAuditSnapshot` and has no `authorizedByPermission` parameter (`src/YCR.Application/Common/Abstractions/IAuditWriter.cs:35-41`) | None. Recorded because it is the one substantive design defect that reached a commit in this range, and because it was caught by a human reading the code rather than by any test or architecture rule — which is worth knowing when judging how much the automated controls actually cover. | **Fixed** at `6dfb67a` (outside this range) |
| **C-10** | Low (already remediated) | **The S17 test, as codex shipped it, could not prove what its checkpoint claimed.** `ModuleInterfacesTests` at `9a09b36` built the provider against `Server=(localdb)\MSSQLLocalDB;Database=YcrScopeTest` and asserted only DI reference identity; the connection was never opened. The progress entry for `9a09b36` states it "proves the module interface and concrete context are the same within one scope" — true of DI identity, but the property ADR-0012 item 3 actually depends on is that the shared instance shares a **transaction**, and that was untested. | `git show 9a09b36:tests/YCR.Infrastructure.Tests/Persistence/ModuleInterfacesTests.cs`; the current version at `tests/YCR.Infrastructure.Tests/Persistence/ModuleInterfacesTests.cs:24-66` adds the write-through-the-interface / read-from-another-scope assertion against the pinned container under `ycr_app` | None. Verified fixed: the run above exercises the current version, 3/3 green. | **Fixed** at step 7/9 (outside this range) |
| **C-11** | Low | **R4's "never Zawgyi" half has no control and is not recorded as unenforced.** `BilingualName` validates presence and length only. Zawgyi text occupies the same Unicode range as correct Myanmar, so `nvarchar` storage — which spec S14 does test — satisfies the *storage* half of the FACT and nothing addresses the *input* half. Whether Zawgyi must be rejected at the boundary, normalised, or merely never produced by our own clients is answered nowhere in `docs/`. Per AGENTS.md that is a missing rule, not something to guess at. | `src/YCR.Domain/Network/BilingualName.cs:24-34`; `docs/20` §6 "Never store Zawgyi"; `spec.md` §3 R4 | Raise as an OPEN QUESTION in `docs/19` and fold it into **T-014**, which already owns R4's replacement. Do not implement a detector against a guessed rule. Not blocking: nothing in F-001 depends on the answer. | Fixed at `bde5542` |
| **C-12** | Low | **`Result<T>.Success(null)` reports success with a null `Value`.** `Result.Failure` guards its argument with `ArgumentNullException.ThrowIfNull`; `Result<T>.Success` does not, and the implicit `T → Result<T>` conversion means `return (StationCode)null;` silently yields `IsSuccess == true` whose `Value` returns null through `_value!` — in a solution built with nullable reference types and `TreatWarningsAsErrors`, where that `!` is precisely what suppresses the only warning that would have shown it. No caller does this today. This is the primitive every handler signature is built on, so the asymmetry is worth closing while there is one implementation to change. | `src/YCR.Domain/Common/Result.cs:39-43, 54, 64`; the guarded counterpart at `:26-30, 56-60` | `ArgumentNullException.ThrowIfNull(value)` in `Result<T>.Success` for reference types, plus a test. Separately: `Result<T>` inherits the static `Result.Success()`, so `Result<int>.Success()` compiles and returns a non-generic `Result` — a trap worth a comment if not a fix. | Open — T-008 |

### What this range got right, and should not be undone

Not findings, but the parts a later reviewer or remediator should leave alone:

- **The step-4 stop.** Codex found that `INetworkDbContext` needed a `Microsoft.EntityFrameworkCore` reference the approved plan had not budgeted for, and stopped before writing any code (`113eef6`), recording an ENGINEERING BLOCKER with the precise evidence rather than adding the package and moving on. AGENTS.md's stop-and-ask rule is the easiest one to skip quietly; it was not skipped.
- **The architecture fixtures assert the failure text.** `AssertRuleProtectsFixture` requires the fixture's *name* to appear in the violation output (`ArchitectureRuleTests.cs:244-246`), so a rule that failed for an unrelated reason would still be caught. That is the difference between a negative case and a negative case that means something.
- **`Src_ContainingAuthenticationHandler_IsDetectedAtAnyInheritanceDepth`** replaced a direct-base-class check with an `IAuthenticationHandler` assignability scan (`:136-155`), closing the obvious evasion of subclassing one level deeper. ADR-0020 item 4 is satisfied at any depth.
- **`Application_ReferencesNoSqlServerAssemblies`** (`:123-133`) checks assembly references by reflection, not by type-name substring. It is strictly stronger than the `HaveFullNameContaining("Microsoft.EntityFrameworkCore.SqlServer")` rule beside it, which would miss a provider type such as `Microsoft.EntityFrameworkCore.Metadata.SqlServerAnnotationNames`. Keeping both is right; the reflection one is the control.
- **`StationCode`'s regex uses `RegexOptions.NonBacktracking`** (`StationCode.cs:15-17`), so the validator cannot be made to backtrack on hostile input. On a pattern this simple it costs nothing, and it is the right default for a skeleton to hand to every later value object.
- **The provisional rules are confined exactly where the waiver says.** Three files, three labels, no fourth. T-014 has a small, findable surface.

---

## Security review

Security reviewer: **claude** (security-agent), task **T-007b**.

Threat IDs reviewed: `docs/18-threat-model.md` names categories, not numbered IDs. Those with a surface in this range: **Unauthorized configuration**, **Privilege escalation**, **Insider manipulation**, **Data disclosure**, **API abuse**. The authentication-specific REQUIRED CONTROLs in `docs/18` (CSP, SPA rendering, frontend dependency audit, `Origin` checks, refresh-token reuse events) have **no surface here** — there is no endpoint, cookie, SPA or token handling in codex's files. They belong to T-007 over claude's step 11 and to later features.

Evidence: the four commands in §Commands run above, plus file-by-file reading of every file in scope and a `grep` over `src/` and `tests/` for secrets, skips and suppressions.

| Control | Verdict | Evidence |
|---|---|---|
| No secrets, tokens or personal data in code, config or logs | ✓ | Two connection strings appear in the range — `YcrDbContextFactory.cs:11` and `StationModelTests.cs:15` — both `Server=(localdb)\MSSQLLocalDB;Database=…` with **no user id, password or token**. `.gitleaks.toml` runs in CI at this SHA (T-004 Notes) and is green. Nothing in the range logs anything at all, so `docs/20` §7's never-log list is satisfied. |
| Audit actor fields come from the authenticated server-side context (ADR-0017 item 2) | ✓ at `9106d53`; ✗ as codex shipped it | `ICurrentUser` (`704d872`) already exposed `UserId`, `Roles`, `ClientIp` and `CorrelationId` as server-side properties with no setter — correct. The `IAuditWriter` beside it took `authorizedByPermission` from the caller: see **C-9**. The current interface has no such parameter and the field is derived server-side. **No open issue at the reviewed SHA.** |
| Least privilege; no DDL from the application credential (E7, spec S22) | ✓ in range | Nothing codex wrote runs a migration at startup: `AddInfrastructure` registers the context and three services and calls no `Migrate()` (`DependencyInjection.cs:14-31`). The role migration and the privilege test are claude's step 9. **C-8** is the one advisory note. |
| Injection surface | ✓ | No raw SQL, no string-concatenated query, no `FromSqlRaw`/`ExecuteSqlRaw` anywhere in the range. The only SQL codex authored is the generated `Network_CreateStations` migration, which uses `MigrationBuilder` primitives with no interpolation. |
| Untrusted input and denial of service | ✓ | `StationCode` bounds input to 2–10 characters and validates with a **non-backtracking** regex, so there is no ReDoS. `BilingualName` bounds both names to 100 characters *before* they reach an `nvarchar(100)` column, so the length rule is enforced in the domain rather than by truncation or a provider error. Both reject rather than normalise, which is what spec S9 requires. |
| Mass assignment / over-posting into the aggregate | ✓ | `Station` has only private setters and a private parameterless constructor for EF; the sole construction path is `Create`, which requires already-validated value objects and null-checks both. `Entity.Id` is `protected set`. There is no path from a request DTO to a domain field that skips validation — though that path itself is claude's to review (step 11). |
| Data disclosure through the domain surface | ✓ | `StationCode.ToString()` returns the code, which is not sensitive; `BilingualName` has no `ToString()` beyond the record default; nothing in the range reaches a log or an exception message. `Error.Message` values are fixed English strings with no interpolated user input except `StationCodeAlreadyExists(code)`, which echoes a code the caller just supplied and which is already constrained to `[A-Z0-9]{2,10}` — no injection or disclosure vector. |
| Widened assembly visibility | ✓ (noted) | `[assembly: InternalsVisibleTo("YCR.Infrastructure")]` (`src/YCR.Domain/Properties/AssemblyInfo.cs:3`) exists so `StationCode.From` — the unvalidated EF materialisation path — can be `internal` rather than `public`. That is a **net narrowing**: without it, `From` would have to be public and any caller could bypass R3. The grant is to one first-party assembly in the same solution and the assemblies are unsigned, so there is no strong-name spoofing concern. Worth knowing that it also exposes every future `YCR.Domain` internal to Infrastructure; acceptable, and better than the alternative. Undocumented in the plan — folded into **C-8**. |
| Privilege escalation through the permission constants | ✓ | `Permissions.StationsManage` and `StationsRead` are two `const string` values matching `docs/10` §inventory exactly. **No role→permission grant is seeded anywhere in the range**, which is what spec §2 and contradictions C1/C2 require while OQ28 is open. Nothing in the code implies a role mapping. |

**Open Critical/High findings: none.**

---

## Verdict

**Ready** — for codex's part of stage 4 (plan steps 2–6 and the step-5 review fixes), reviewed at `9106d53`.

**Blocking findings: none.** The code builds with zero warnings under `TreatWarningsAsErrors`; the 45 tests in scope pass with none skipped; the architecture rules are proven to have teeth by fixtures that assert their own names in the failure output; the provisional R3/R4/R8 rules are confined to exactly the three files the waiver permits; and no Critical or High security finding exists.

Twelve non-blocking findings are recorded above. **C-9 and C-10 are already fixed** in later commits and need no action. The remaining ten belong to **T-008** (remediation, owned by the T-004 implementer), of which **C-1** is the one worth doing before the pattern is copied, and **C-11** needs an OPEN QUESTION in `docs/19` rather than code.

### What this verdict does *not* cover

- **Plan steps 1 and 7–13 (claude's work):** the solution and build configuration, the container fixture and migration bundle, the audit ledger and its guard, the least-privilege role, the four handlers, the whole API layer, the trunk-only controls and CI. **T-006 and T-007 remain open** and need a reviewer who is neither claude nor codex, or a further split by author.
- **Stage 5 (T-005), scenario tests from spec §4.** This review was ordered ahead of it. Nothing here asserts that S1–S27 are covered; it asserts that the tests which exist in codex's range pass and mean what they claim.
- **Stage 8 documentation.** `docs/07`, `docs/08`, `docs/20` §3 and `docs/20` §6 are knowingly stale at this SHA, by the approved plan's own scheduling.

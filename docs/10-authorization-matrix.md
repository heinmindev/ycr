# Authorization Matrix

| Capability | Admin | Railway Admin | Station Manager | Operator | Inspector | Finance | Auditor |
|---|---:|---:|---:|---:|---:|---:|---:|
| Manage stations | ✓ | ✓ | - | - | - | - | view |
| Manage fares | ✓ | ✓ | - | - | - | - | view |
| Sell tickets | ✓ | ✓ | ✓ | ✓ | - | - | - |
| Validate tickets | ✓ | ✓ | ✓ | ✓ | ✓ | - | - |
| Cancel tickets | ✓ | ✓ | ✓ | policy | - | - | - |
| Refund | ✓ | policy | - | - | - | ✓ | view |
| Reports | ✓ | ✓ | ✓ | limited | limited | ✓ | ✓ |
| Audit logs | ✓ | ✓ | - | - | - | - | ✓ |

**The table above is a proposal only, and the permission inventory below governs**, except for `stations.manage`/`stations.read`, `routes.manage`/`routes.read`, `services.manage`/`services.read`, `schedules.manage`/`schedules.read` and the identity permissions per the rulings below. Where the two disagree, the inventory wins: a capability shown as granted in the table is still an OPEN QUESTION until the inventory records an approved grant. (ENGINEERING DECISION — tech lead, hein, 2026-09-20, resolving contradictions C1 and C2 in `docs/features/F-001-walking-skeleton/spec.md` §0.3. The underlying role question was OQ28.)

This is a starting proposal and must be approved against actual railway roles.

## Station permission grants — resolved (OQ28)

**Tech-lead ruling (hein, 2026-09-22; T-014) — not a Myanma Railways answer.** Myanma Railways has not confirmed role grants; this project chose not to wait. If Myanma Railways later gives an official, different answer, that supersedes this ruling and needs its own follow-up task.

- `stations.manage` → **`SystemAdministrator` and `RailwayAdministrator` only** (matches the table above; `StationManager`, `TicketOperator`, `TicketInspector`, `FinanceOfficer`, `Auditor` and `ReportingUser` do **not** hold it).
- `stations.read` → **any authenticated operator role — all eight roles:** `SystemAdministrator`, `RailwayAdministrator`, `StationManager`, `TicketOperator`, `TicketInspector`, `FinanceOfficer`, `Auditor` and `ReportingUser`.

**Role names (C3) and `ReportingUser` — provisional tech-lead ruling (hein, 2026-09-23; T-023, OQ12) — not a Myanma Railways answer.** The grants above are T-014's, restated with the canonical role identifiers. The table's compact headings map Admin → `SystemAdministrator`, Railway Admin → `RailwayAdministrator`, Operator → `TicketOperator`, Inspector → `TicketInspector`, Finance → `FinanceOfficer`; Station Manager and Auditor keep their names, and `ReportingUser` (`docs/03`'s Reporting User, which has no column in the table) is the eighth role. `ReportingUser`'s `stations.read` was added on 2026-09-23: hein's T-014 intent was any authenticated operator role, and T-014's list of seven predates the ruling that admits `ReportingUser`. If Myanma Railways later gives an official, different answer, that supersedes this ruling and needs its own follow-up task.

Until F-002 ships, no grant is seeded: F-001 has no identity/role provisioning system (ADR-0020), and its tests mint the permission they need directly on the test principal. F-002 seeds these grants **as data, by migration**; no API edits grants (see §Identity permission grants).

## Route permission grants — resolved (OQ40)

**Provisional tech-lead ruling (hein, 2026-09-24; T-032) — not a Myanma Railways answer.** Myanma Railways has not confirmed role grants for routes; this project chose not to wait. **Still open with Myanma Railways.** If Myanma Railways later gives an official, different answer, that supersedes this ruling and needs its own follow-up task.

- `routes.manage` → **`SystemAdministrator` and `RailwayAdministrator` only** (`StationManager`, `TicketOperator`, `TicketInspector`, `FinanceOfficer`, `Auditor` and `ReportingUser` do **not** hold it).
- `routes.read` → **all eight roles:** `SystemAdministrator`, `RailwayAdministrator`, `StationManager`, `TicketOperator`, `TicketInspector`, `FinanceOfficer`, `Auditor` and `ReportingUser`.

Holding `stations.manage` or `stations.read` gives no route right. The permission name `routes.read` was added on 2026-09-24 (hein, T-032, E2). These grants are data seeded by a reviewed migration, like every other grant; no API edits them (see §Identity permission grants). See `docs/features/F-003-route-management/spec.md` §0.8, R2, R3.

## Service permission grants — resolved (OQ49)

**Provisional tech-lead ruling (hein, 2026-09-25; T-044) — not a Myanma Railways answer.** Myanma Railways has not confirmed role grants for services; this project chose not to wait. **Still open with Myanma Railways.** If Myanma Railways later gives an official, different answer, that supersedes this ruling and needs its own follow-up task.

- `services.manage` → **`SystemAdministrator` and `RailwayAdministrator` only** (`StationManager`, `TicketOperator`, `TicketInspector`, `FinanceOfficer`, `Auditor` and `ReportingUser` do **not** hold it).
- `services.read` → **all eight roles:** `SystemAdministrator`, `RailwayAdministrator`, `StationManager`, `TicketOperator`, `TicketInspector`, `FinanceOfficer`, `Auditor` and `ReportingUser`.

Holding a station or route permission gives no service right. The permission name `services.read` was added on 2026-09-25 (hein, T-044, E6). `trains.manage` is not used in Phase 1 and has no grant (OQ42 ruling); `schedules.manage` and `schedules.read` are resolved for FR-004 by OQ59 (see §Schedule permission grants). These grants are data seeded by a reviewed migration, like every other grant; no API edits them (see §Identity permission grants). See `docs/features/F-004-service-management/spec.md` §0.9, R3, R4.

## Schedule permission grants — resolved (OQ59)

**Resolved by tech-lead ruling (hein, 2026-09-26; T-053) — not a Myanma Railways answer.** Myanma Railways has not confirmed role grants for timetable versions; this project chose not to wait. **Still open with Myanma Railways.** If Myanma Railways later gives an official, different answer, that supersedes this ruling and needs its own follow-up task.

- `schedules.manage` → **`SystemAdministrator` and `RailwayAdministrator` only** (`StationManager`, `TicketOperator`, `TicketInspector`, `FinanceOfficer`, `Auditor` and `ReportingUser` do **not** hold it). It covers creating, discarding, publishing and cancelling timetable versions.
- `schedules.read` → **all eight roles:** `SystemAdministrator`, `RailwayAdministrator`, `StationManager`, `TicketOperator`, `TicketInspector`, `FinanceOfficer`, `Auditor` and `ReportingUser`.

There is no separate `schedules.publish` permission: any holder of `schedules.manage` may publish, including the author of the draft, and there is no second approver in Phase 1 (OQ57 ruling). Holding a station, route or service permission gives no schedule right, and holding a schedule permission gives no station, route or service right. The permission name `schedules.read` was added on 2026-09-26 (hein, T-053, E5). These grants are data seeded by a reviewed migration, like every other grant; no API edits them (see §Identity permission grants). The migration is `20260927071025_Identity_SeedSchedulePermissionGrants` (ten rows; 44 grants in all). See `docs/features/F-005-timetables/spec.md` §0.10, R3–R5.

## Identity permission grants — F-002

**Provisional tech-lead ruling (hein, 2026-09-23; T-023, D8/OQ34) — not a Myanma Railways answer.** Myanma Railways has not said who administers staff accounts; this project chose not to wait. If Myanma Railways later gives an official, different answer, that supersedes this ruling and needs its own follow-up task.

| Permission | Allows | Held by |
|---|---|---|
| `users.read` | list and view staff accounts and their sessions; read the role catalogue | `SystemAdministrator` only |
| `users.manage` | create, disable, enable and unlock accounts; administrator password reset | `SystemAdministrator` only |
| `users.roles.manage` | replace a user's role assignments — never the caller's own (no user may change their own roles) | `SystemAdministrator` only |
| `auth-sessions.revoke` | revoke a user's sign-in session | `SystemAdministrator` only |

No other role holds these. Grants, including these, are data seeded by a reviewed migration; there is no grant-editing API, because it would let its holder grant anything to anyone. No second approver is required in Phase 1. See `docs/features/F-002-staff-authentication/spec.md` R11, R20, §6.2.

## Permission inventory requiring explicit approval

The following permission identifiers are referenced by ADR-0013 and the API proposal. Their role grants are **OPEN QUESTION** until the authorization matrix is approved; agents must not infer grants from the names. (`stations.manage`, `stations.read`, `routes.manage`, `routes.read`, `services.manage`, `services.read`, `schedules.manage`, `schedules.read` and the four identity permissions are the exceptions — see §Station permission grants, §Route permission grants, §Service permission grants, §Schedule permission grants and §Identity permission grants above.)

- `sales.void`
- `sales.sell`
- `sales.read`
- `tickets.cancel`
- `tickets.read`
- `tickets.validate`
- `payments.reverse`
- `refunds.request`
- `refunds.approve`
- `refunds.disburse`
- `refunds.reject`
- `cashier-sessions.recent-operations`
- `cashier-sessions.open`
- `cashier-sessions.close`
- `tickets.reprint`
- `stations.manage` (role grants resolved — see §Station permission grants above)
- `stations.read` (added 2026-09-20 by hein; the matrix table gives Auditor a "view" right over station management but the inventory had no read permission. Role grants resolved — see §Station permission grants above.)
- `routes.manage` (role grants resolved — see §Route permission grants above)
- `routes.read` (added 2026-09-24 by hein, T-032; role grants resolved — see §Route permission grants above)
- `trains.manage` — **not used in Phase 1 (OQ42 ruling, hein, 2026-09-25; T-044):** there is no `Train` concept, no `/trains` endpoint and no grant; `docs/03` use case 3 "Manage trains" is met by services
- `services.manage` (role grants resolved — see §Service permission grants above)
- `services.read` (added 2026-09-25 by hein, T-044, E6; role grants resolved — see §Service permission grants above)
- `fares.manage`
- `schedules.manage` (role grants resolved — see §Schedule permission grants above)
- `schedules.read` (added 2026-09-26 by hein, T-053, E5; role grants resolved — see §Schedule permission grants above). There is no `schedules.publish` permission (OQ59 ruling): publishing is part of `schedules.manage`
- `reports.read`
- `ticket-qr.trust-list.read`
- `idempotency.read`
- `users.read`, `users.manage`, `users.roles.manage`, `auth-sessions.revoke` (added 2026-09-23 by hein, T-023; role grants resolved — see §Identity permission grants above)

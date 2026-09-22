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

**The table above is a proposal only, and the permission inventory below governs**, except for `stations.manage`/`stations.read` per the ruling below. Where the two disagree, the inventory wins: a capability shown as granted in the table is still an OPEN QUESTION until the inventory records an approved grant. (ENGINEERING DECISION — tech lead, hein, 2026-09-20, resolving contradictions C1 and C2 in `docs/features/F-001-walking-skeleton/spec.md` §0.3. The underlying role question was OQ28.)

This is a starting proposal and must be approved against actual railway roles.

## Station permission grants — resolved (OQ28)

**Tech-lead ruling (hein, 2026-09-22; T-014) — not a Myanma Railways answer.** Myanma Railways has not confirmed role grants; this project chose not to wait. If Myanma Railways later gives an official, different answer, that supersedes this ruling and needs its own follow-up task.

- `stations.manage` → **Admin and Railway Admin only** (matches the table above; Station Manager, Operator, Inspector, Finance and Auditor do **not** hold it).
- `stations.read` → **any authenticated operator role** — Admin, Railway Admin, Station Manager, Operator, Inspector, Finance and Auditor.

No grant is seeded in application code. No identity/role provisioning system exists yet — real authentication and role assignment are deferred to the ADR-0016 follow-up feature (ADR-0020). F-001's tests mint the permission they need directly on the test principal.

## Permission inventory requiring explicit approval

The following permission identifiers are referenced by ADR-0013 and the API proposal. Their role grants are **OPEN QUESTION** until the authorization matrix is approved; agents must not infer grants from the names. (`stations.manage` and `stations.read` are the exception — see §Station permission grants above.)

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
- `routes.manage`
- `trains.manage`
- `services.manage`
- `fares.manage`
- `schedules.manage`
- `reports.read`
- `ticket-qr.trust-list.read`
- `idempotency.read`

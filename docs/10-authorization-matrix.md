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

**The table above is a proposal only, and the permission inventory below governs.** Where the two disagree, the inventory wins: a capability shown as granted in the table is still an OPEN QUESTION until the inventory records an approved grant. (ENGINEERING DECISION — tech lead, hein, 2026-09-20, resolving contradictions C1 and C2 in `docs/features/F-001-walking-skeleton/spec.md` §0.3. The underlying role question is OQ28.)

This is a starting proposal and must be approved against actual railway roles.

## Permission inventory requiring explicit approval

The following permission identifiers are referenced by ADR-0013 and the API proposal. Their role grants are **OPEN QUESTION** until the authorization matrix is approved; agents must not infer grants from the names.

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
- `stations.manage`
- `stations.read` (added 2026-09-20 by hein; the matrix table gives Auditor a "view" right over station management but the inventory had no read permission. Role grants remain OPEN QUESTION — OQ28.)
- `routes.manage`
- `trains.manage`
- `services.manage`
- `fares.manage`
- `schedules.manage`
- `reports.read`
- `ticket-qr.trust-list.read`
- `idempotency.read`

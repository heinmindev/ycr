# ADR-0007: Signed Ticket QR with Online Duplicate-Use Check

## Status

Superseded by ADR-0014

## Context

Threats (docs/18): forged tickets, replay and duplicate use. Validation may happen where the network is unreliable (OQ8). Offline validator *synchronization* is out of scope (docs/00), but the ticket format should not rule it out.

## Decision

1. At issuance, the server builds a compact **ticket payload** and signs it with **ECDSA P-256** (built into .NET `System.Security.Cryptography`). The QR carries payload + signature (base64url, with a version byte).
   - Payload (draft, business fields pending OQs): `v`, `kid` (signing key id), `ticketNumber`, `ticketId`, `issuedAtUtc`, `validFrom`, `validUntil`, `origin`, `destination`, `passengerCategory`, `fareRuleVersion`.
   - Ed25519 would give smaller signatures but needs a third-party library. Reconsider only if QR size becomes a problem.
2. **Validation has two steps:**
   - *Authenticity:* verify the signature with the public key for `kid`. This works offline and stops forgery.
   - *Duplicate use and status:* `POST /api/v1/ticket-validations` with the scanned payload. The server checks status (cancelled, refunded, expired) and records the validation atomically. A unique constraint or row lock makes concurrent scans safe.
   - If the network is down, the inspector app can show "signature valid, usage unverified". Whether that is acceptable is a BUSINESS DECISION (OQ8).
3. Keys: the private key lives in secret storage (never in the repo or DB). Public keys are published with their `kid` so they can be rotated. Old public keys stay valid until every ticket signed with them has expired.
4. The printed human-readable `TicketNumber` is for support and lookup only. It is not proof of validity.

## Consequences

- A signature proves the ticket was issued by YCR. It **does not** stop a photocopied ticket from being used twice. Only the online check (or a future offline sync) does that.
- Key management and rotation become an operational duty (docs/15, docs/17).
- The validate API moves from `POST /tickets/{id}/validate` to validation by scanned payload (docs/08 to be updated).

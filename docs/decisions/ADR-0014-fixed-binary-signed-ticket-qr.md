# ADR-0014: Fixed-Binary Signed Ticket QR

## Status

Accepted — 2026-09-19

## Supersedes

ADR-0007.

## Context

**FACT — ADR-0007:** signature authenticity does not prevent photocopy or replay.

**ENGINEERING DECISION (tech lead, cite ADR-0014):** use a fixed binary payload with ECDSA P-256 and an IEEE P1363 64-byte signature. The format must remain compact enough for printed tickets.

**VERIFY:** physical QR readability depends on the actual printer, scanner, Base45 implementation, and ECC settings; CI must include format tests and a physical scan test before production.

## Options considered

1. Fixed binary P-256 payload - compact and supports offline authenticity with a small custom verifier.
2. Opaque online token - smaller and easier to revoke, but no offline authenticity.
3. COSE/CBOR - standardized envelope, but adds dependency and interoperability cost.

## Decision

### Binary layout v1

The signed bytes have a fixed layout with no maps or JSON:

```text
"YCR" + version byte (0x01)             4 bytes
alg id + kid                            2 bytes
ticketId GUID                          16 bytes
validFrom Unix minutes, uint32 BE       4 bytes
validUntil Unix minutes, uint32 BE      4 bytes
origin station short index, uint16 BE   2 bytes
destination station short index, BE     2 bytes
passenger category                      1 byte
printSequence                           1 byte
P1363 signature                         64 bytes
```

**ENGINEERING DECISION (tech lead, cite ADR-0014):** the verifier requires the text prefix `YCR1:`, decodes the following Base45 text, splits the final 64 bytes as the signature, verifies the signature over the exact preceding bytes as received, and only then parses the fixed fields. Verifiers never parse and re-encode before verification.

**ENGINEERING DECISION (tech lead, cite ADR-0014):** station short indices are stable and are never reused. The mapping is authenticated and versioned outside the payload.

The QR text encoding is `YCR1:` followed by RFC 9285 Base45 in alphanumeric mode with ECC level M. The payload contains no fare-rule version or route data; those values remain server-side. The text prefix digit must equal the binary version byte; verifiers reject a missing prefix or a prefix/version mismatch.

### Key lifecycle

Keys have `active`, `retired`, and `revoked` states. A revoked key has a compromise timestamp.

- Offline verification rejects every signature carrying a revoked `kid`.
- Online validation accepts a ticket signed by a compromised key only when the ticket was issued before the compromise timestamp and the ticket exists in the database.
- Keys rotate monthly unless an emergency rotation is required.
- Inspector devices receive the public-key trust list through an authenticated API and cache it for offline signature checks.
- Production key loaders must reject private-key material from `docs/test-vectors/ticket-qr-v1.json` and any key marked test-only; the deterministic vector key is never a deployable signing key.

### Validation results

Validation returns `Valid`, `Invalid(reason)`, or `AuthenticUnverified`.

- Only `Valid` writes a successful `TicketValidation` record.
- `AuthenticUnverified` handling is BLOCKED by OQ8.
- Accepting only the current `printSequence` after reprint is BLOCKED by the reprint question in `docs/19-open-questions.md`.

### Test vectors

All signing and verification implementations must pass `docs/test-vectors/ticket-qr-v1.json`, including valid, tampered, and revoked-key cases. Signing code must pass `DSASignatureFormat.IeeeP1363FixedFieldConcatenation` explicitly. The vector was independently verified with the published `SimpleBase` 5.6.4 byte-array Base45 implementation and BouncyCastle.Cryptography 2.7.0 for ECDSA verification.

## Blocked behaviour

- **OPEN QUESTION — OQ8:** whether `AuthenticUnverified` is admitted during network loss.
- **OPEN QUESTION — OQ19:** validity window and printed ticket content.
- **OPEN QUESTION — OQ23:** whether a reprint invalidates earlier `printSequence` values.
- **VERIFY:** printer/scanner readability and exact Base45 implementation in physical tests.

## Consequences

Positive:
- Fixed bytes remove parser/re-encoder ambiguity and bound QR size.
- Key compromise and revocation behavior is explicit.
- Server-side duplicate-use policy remains separate from authenticity.

Negative:
- Any layout change requires a new version and migration of signers/verifiers.
- Station index allocation is an integrity-sensitive data process.
- Physical printer/scanner verification remains required.

## Dated amendment - 2026-09-19

**ENGINEERING DECISION (tech lead, cite ADR-0014):** QR v1 uses a one-byte binary version. The signed header is `"YCR" + 0x01`, four bytes total; the signed payload is 36 bytes before the 64-byte P1363 signature. The `YCR1:` text prefix digit must equal the binary version byte (`YCR1:` <-> `0x01`). The `YCR` magic remains inside the signed bytes for domain separation. Any mismatch is invalid and must be rejected before ticket fields are accepted.

# ADR-0001: Start as a Modular Monolith

## Status

Proposed

## Decision

Start the YCR ticketing platform as a modular monolith with explicit bounded-context/module boundaries.

## Rationale

Railway ticketing requires strong transactional consistency and auditability. A modular monolith reduces distributed-system complexity while preserving boundaries that can later be extracted if justified.

## Consequences

Positive:
- simpler deployment
- simpler transactions
- easier local development
- lower operational overhead

Negative:
- requires disciplined module boundaries
- future extraction requires explicit integration contracts

# Testing Strategy

## Unit

Domain invariants, fare calculation, ticket state transitions, refund rules.

## Integration

SQL Server, EF Core transactions, authentication, authorization, API behavior.

## Contract

API request/response contracts.

## End-to-end

Purchase → validate → cancel/refund.

## Architecture

Verify dependency boundaries.

## Concurrency

Test duplicate requests, retry behavior, duplicate validation and double refund.

# Non-Functional Requirements

## Security

- OWASP-aligned secure development
- Strong authentication
- Role/policy authorization
- Rate limiting
- Secrets outside source control
- Sensitive logging prohibited
- Audit trails

## Reliability

- Transactional ticket issuance
- Idempotency for retryable commands
- Database constraints for uniqueness
- Health/readiness endpoints

## Performance

Initial design target: support horizontal scaling without relying on process-local state.

Performance targets must be baselined before production capacity commitments.

## Maintainability

- Clear module boundaries
- Automated tests
- ADRs
- API contracts
- Versioned business policies

## Observability

- Structured logs
- Correlation IDs
- Metrics
- Traces where appropriate
- Business audit events

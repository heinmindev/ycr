# Security Architecture

## Controls

- Authentication
- Role/policy-based authorization
- Secure password handling if local accounts are used
- Rate limiting
- Input validation
- CSRF protection where browser cookie auth is used
- Secure headers
- Secret management
- Audit logging
- Least privilege
- Database least privilege

## Threats

- Ticket forgery
- Replay/duplicate validation
- Double refund
- Duplicate payment
- Privilege escalation
- Credential theft
- API abuse
- Data leakage
- Insider misuse
- Audit tampering

Create a formal threat model before production.

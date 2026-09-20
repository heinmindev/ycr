# ADR-0005: API-Only Backend, Separate SPA Frontend

## Status

Accepted — 2026-09-19

## Decision

- The .NET solution is backend only: `YCR.Api` (REST, `/api/v1`) and `YCR.Worker`. **`YCR.Web` is removed** from doc 06.
- Counter, inspector, admin and reporting UIs are a separate single-page application that calls `YCR.Api`. Its framework and repository location are an OPEN QUESTION, decided when UI work starts.
- The API contract is published as OpenAPI (built-in `Microsoft.AspNetCore.OpenApi`) and is the source of truth for the frontend. Contract tests in `YCR.Api.Tests` guard it.
- CORS allows only the configured frontend origins for each environment.

## Consequences

- Frontend and backend can be built and released independently, and agents can work on the API without UI concerns.
- Authentication must be token-based and suitable for an SPA (see ADR-0016).
- UI-only features (print layout, QR rendering on the ticket) must still take their data from API contracts, never from duplicated rules.

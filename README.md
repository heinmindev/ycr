# Yangon Circular Railway Ticketing System

Production-oriented ticketing platform for Yangon Circular Railway (YCR), implemented with C# / ASP.NET Core 10 and SQL Server.

## Guiding principle

Treat railway rules as configurable domain policy. Never hard-code assumptions about current fares, schedules, station operations, or refund rules.

## Technology

- C# / ASP.NET Core 10
- Entity Framework Core
- SQL Server
- REST API
- Docker
- Automated testing
- Structured logging / OpenTelemetry-ready observability

## Repository

See `AGENTS.md` for the engineering constitution.

Start with `docs/00-project-specification.md`, then execute the discovery workflow in `docs/workflows/01-discovery.md`.

## Important

This starter contains architecture and requirements proposals, not verified operational rules for every YCR service. Validate operational details with Myanma Railways before production use.

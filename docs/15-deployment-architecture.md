# Deployment Architecture

Initial production shape:

```text
Clients
   |
Reverse Proxy / Load Balancer
   |
ASP.NET Core API
   |
SQL Server

Optional:
   |---- Background Worker
   |---- Distributed Cache
   |---- Observability stack
```

Use Docker for repeatable environments.

Separate:

- Development
- Test
- Staging
- Production

Secrets must be injected through environment/secret-management mechanisms.

The SPA and API are served same-origin through a reverse proxy; API requests use `/api/*`. The reverse proxy is responsible for routing the separate SPA build and API without creating a cross-site cookie deployment.

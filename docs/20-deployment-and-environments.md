# Deployment and environments

| Environment | Purpose | Configuration and secrets | Database | Logs/monitoring | Approval and migrations |
|---|---|---|---|---|---|
| Local | Individual development | appsettings.Development, user-secrets/untracked `.env` | Docker or local PostgreSQL | console JSON | developer; EF direct |
| Development | Shared integration | environment + secret store | isolated PostgreSQL | centralized verbose | automatic app deploy; reviewed migration job |
| Test | deterministic CI | ephemeral environment values | Testcontainers PostgreSQL | CI artifacts | CI; migrations applied per container |
| UAT | acceptance rehearsal | UAT config + vault | production-like isolated PostgreSQL | production-like alerts | product/engineering approval; idempotent script |
| Production | customer workload | environment references + managed vault/workload identity | managed HA PostgreSQL | centralized retained logs/metrics/traces | change approval; backup then separate migration |

Low-cost MVP: a small Hetzner Germany VM with Docker, externally managed encrypted backups and a TLS reverse proxy; accept the operational burden and test latency to Ethiopia. Managed production: Azure France Central, Germany West Central or South Africa North, or AWS Frankfurt, using managed PostgreSQL, managed secrets, load balancer/WAF, monitoring and PITR. Measure real Ethiopian carrier latency before selection; do not infer proximity alone.

Terminate HTTPS at Nginx/Traefik/Azure Front Door/cloud LB, overwrite forwarded headers, enable HSTS, set upload limits, edge rate limiting and health probes. Keep backend network private. No current WebSockets are required. Containers should have CPU/memory reservations derived from load tests, non-root runtime users, immutable images and persistent database storage.

Deploy: backup, approve migration, apply reviewed SQL, deploy worker/API/Web digests, verify live/ready and smoke tests, monitor. Roll back image digests; roll database forward. See the operational runbook.

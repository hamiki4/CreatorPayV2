# Deployment and Environments

| Environment | Purpose and data |
|---|---|
| Local | Docker PostgreSQL and developer services; synthetic data/secrets |
| Development | continuous integration, shared ephemeral data, mock/sandbox adapters |
| Test | automated integration/E2E and migration verification; resettable synthetic data |
| UAT | production-like configuration for business acceptance; masked/synthetic data |
| Production | isolated accounts/network/data, approvals, monitoring and strict change control |

Build immutable versioned API/worker/Web artifacts with Docker multi-stage builds, non-root runtime, health checks and pinned/scanned dependencies. PostgreSQL is managed where possible, private, encrypted, highly available and version-supported. Keep secrets in a managed secret store, inject at runtime, rotate and never put them in images, repository or client bundles. Environment configuration is validated at startup.

CI/CD runs backend/frontend builds, tests, security scans, IaC/config validation, image SBOM/signing and migration rehearsal. Promotion uses the same artifact with approvals for UAT/Production. Migrations are reviewed, backwards-compatible expand/migrate/contract steps, separately executed and backed up; application startup does not auto-migrate. Deploy rolling/blue-green where practical with readiness and automatic halt. Roll back application safely; database recovery uses a tested forward fix or restore plan, never an assumed down migration.

Use encrypted automated backups plus point-in-time recovery, cross-failure-domain copies, retention policy and scheduled restore drills with measured RPO/RTO. Monitor as in [operations](18-reporting-and-operations.md), centralize tamper-resistant logs and maintain incident/runbooks.

Select a region near Ethiopia only after measuring latency and confirming data residency, provider connectivity, availability and cost; a nearby African/Middle Eastern region is a candidate, not a commitment. UAT/production payment integrations use explicit sandbox/live credentials, verified callbacks, egress controls and a provider abstraction. Provider, hosting, RPO/RTO, data residency, disaster-recovery region and regulatory approvals remain open.

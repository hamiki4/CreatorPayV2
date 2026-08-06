# Milestone 30 production deployment and pilot operations

## Production deployment checklist

- [ ] Promote one reviewed immutable image digest; record approver, UTC time, version and change ticket.
- [ ] Supply database, JWT, encryption, HMAC, provider and monitoring credentials through the platform secret manager. Never place values in images, Compose files, logs or tickets.
- [ ] Confirm HTTPS-only CORS origins, exact `AllowedHosts`, immediate trusted proxies, TLS, WAF, CSP/security headers, authentication and 429 responses on auth and financial routes.
- [ ] Validate API `/health/live` and `/health/ready`, web `/healthz`, PostgreSQL `pg_isready`, worker container health and the admin System view.
- [ ] Run the Release build/test/web/E2E commands, container configuration validation, vulnerability/secret scans and `scripts/operations/readiness-smoke.ps1`.
- [ ] Test the error-monitoring route and paging policy with synthetic data; confirm logs contain correlation/public IDs but no tokens, passwords, connection strings, OTPs, phone numbers or message bodies.
- [ ] Take and checksum a backup, verify PITR, name rollback owner, pause financial traffic during migration, and obtain go/no-go approval.

## Migration and rollback

1. Generate and review an idempotent script with `scripts/database/generate-idempotent-script.ps1`. Verify the migration list against the release artifact.
2. Drain financial writes and workers. Run `scripts/database/backup.ps1`; copy the dump and SHA-256 manifest to encrypted immutable storage.
3. Apply with the least-privilege migration identity using `scripts/database/apply-migrations.ps1`. Start one API instance and verify readiness, schema, authentication, rate limits and financial reconciliation before scaling workers/API.
4. On application regression, stop workers and deploy the previous immutable application digest. Prefer a forward database fix; EF down-migrations are not an automatic production rollback.
5. Restore only with incident/change approval into a verified target using `scripts/database/restore.ps1`. Restore to an isolated database first, validate the checksum, migrations, ledger/wallet/payout/outbox/audit reconciliation, then approve cutover. Record measured RPO/RTO.

## Pilot daily operations checklist

- [ ] Review service health, worker heartbeat/failed jobs, failed checkouts, low wallets, failed notifications, pending/failed payouts, suspicious activity, open support requests and operational alerts.
- [ ] Reconcile purchases, wallet journals, creator earnings, payouts, deposits, reversals and provider evidence; investigate any imbalance before traffic resumes.
- [ ] Triage support by public reference and audit/correlation ID. Do not paste personal data or credentials into search or incident channels.
- [ ] Confirm last successful backup/PITR point, notification backlog age, certificate/secret expiry alerts and on-call coverage.
- [ ] Record UTC handover, unresolved risks, owners and next update. Manual payments require independent external evidence and dual review.

## Incident response workflow

1. **Detect and declare:** page the named incident commander; assign severity, UTC start, affected service/tenant, safe public IDs and correlation IDs.
2. **Contain:** stop only affected traffic/workers or disable the documented feature flag. For financial imbalance, halt financial writes and preserve database/log/provider evidence.
3. **Investigate:** use admin service health, operational alerts, support lookup and audit search. Query read-only replicas where possible; never edit balances or replay money by hand.
4. **Recover:** choose forward fix, immutable application rollback, provider fallback or approved restore. Run readiness, auth/rate-limit checks and financial reconciliation before reopening.
5. **Communicate:** use the approved internal/customer cadence; exclude secrets and personal data. Escalate suspected privacy/security events immediately to security/legal.
6. **Close:** record timeline, impact, evidence, corrective owners/dates, alert/runbook gaps and a blameless review. Rotate potentially exposed credentials.

Production remains blocked until real provider certification, managed secret/telemetry adapters, independent security testing, production infrastructure, tested paging routes, and a measured restore/capacity exercise are complete.

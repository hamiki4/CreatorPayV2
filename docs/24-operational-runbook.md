# CreatorPay operational runbook

## Start, stop, and deploy

Copy `.env.example` to an untracked `.env`, replace every placeholder, apply reviewed migrations, then run `docker compose up -d`. Verify `/health/live` and `/health/ready`. Stop gracefully with `docker compose stop`; workers finish or cancel the current job and release PostgreSQL advisory locks. Deploy immutable API, Worker, and Web images together. Never run migrations automatically at application startup.

Rollback application images to the last known-good digest. Database changes roll forward with a corrective migration; restore only for destructive data incidents. Before every production migration, run `backup.ps1`, verify its checksum and encrypted destination, generate/review the idempotent SQL, obtain approval, apply, and check migration status and readiness.

## Triage

- Logs are JSON and keyed by `CorrelationId`, `RequestId`, endpoint, status, duration, user/merchant identifiers, job and provider. Never paste tokens, OTPs, phones, destinations, or secrets into tickets.
- A live failure means restart or platform investigation. A ready failure usually means database connectivity or pending migrations. Public health output intentionally omits internals.
- Inspect `NotificationOutboxMessages`, `NotificationDeadLetters`, and `BackgroundJobExecutions` for backlog/failures. Fix the provider or data cause before an authorized admin retry.
- Failed payouts remain manual-review operations. Reconcile provider references and journal state before retrying; never force database state.
- For database errors, validate DNS/port/TLS/user/database, run `scripts/database/verify-connection.ps1`, inspect PostgreSQL saturation and locks, then escalate.
- Rotate secrets through the deployment secret store, restart affected services, validate readiness, and record only secret name/version/time/actor. JWT rotation currently requires a coordinated restart and invalidates tokens signed by the old key.

## Incident response

Declare severity and incident lead, preserve logs/audit evidence, contain access, rotate affected secrets, communicate using correlation IDs, recover, validate balances/outbox/payout reconciliation, and complete a blameless review. For data loss, restore the latest tested encrypted backup to an isolated database, replay WAL/PITR if configured, validate, then approve cutover.

# RC migration, restore, reliability and performance evidence

## Migration and restore drill

1. Start a fresh PostgreSQL 17 database with validation-only credentials. Apply the full EF chain and run `dotnet ef migrations has-pending-model-changes`.
2. Load only the explicit local synthetic Pilot seed. `reset-and-reseed.ps1` requires `-ConfirmReset`, accepts only Development/Pilot, rejects Production and now uses `ConnectionStrings__CreatorPayDatabase`. Never schedule it.
3. Exercise representative synthetic role and financial records, reconcile, then run `scripts/database/backup.ps1`. Preserve the dump and SHA-256 manifest.
4. Create a separately named isolated database. Run `restore.ps1` with its exact `-ExpectedDatabaseName` and explicit `-ConfirmRestore`.
5. Verify migrations, row counts, role access, public IDs, checkout/idempotency records, wallet/Creator/Shopper/platform journal balance, payouts, reversals, audit/outbox/support records and API readiness. Record UTC time, RPO/RTO, backup hash and approver. Never restore over the sole Pilot database as a test.

Rollback means previous compatible application artifact or reviewed forward schema correction. EF down migration is not automatic. Restore is the last approved recovery path when forward/application rollback cannot preserve integrity.

## Performance baseline procedure and result record

Install k6 on an operator workstation and run `k6 run scripts/load/private-pilot-baseline.js` with `API_URL`, `WEB_URL` and synthetic Creator/Admin/Cashier credentials supplied as process environment values. Add a synthetic `CAMPAIGN_ID` for QR. The default is five users for three minutes and does not write. `ENABLE_WRITE_PROBES=true` permits synthetic support submissions; financial probes require the separate `ENABLE_FINANCIAL_PROBES=true` plus a disposable `CHECKOUT_BODY`. Never use real personal data or run financial probes against live Pilot balances.

Record p50/p95/p99, failure rate and sample count for live/ready, sign-in, Help, Creator Offers, QR, Pilot Operations, support and cashier checkout. Default RC limits are overall p95 <750 ms, p99 <1500 ms, health p95 <250 ms, sign-in p95 <1000 ms and errors <1%. Capture PostgreSQL slow-query/`EXPLAIN (ANALYZE, BUFFERS)` evidence with synthetic values before adding an index. The repository review found indexes for status/expiry, tenant/time, idempotency/public/token keys and financial relationships; no new index is justified without deployed measurements.

No deployed baseline is claimed by this document. Fill: **environment [ ], candidate SHA [ ], UTC [ ], operator [ ], endpoint results [ ], slow queries [none/attach], decision [pass/block]**.

## Reliability drill

With synthetic transactions only, restart API and Worker independently; interrupt PostgreSQL and confirm readiness fails then recovers; induce a notification-provider failure and observe retry/dead-letter visibility; replay identical and changed idempotency keys concurrently; send SIGTERM and confirm graceful logs; confirm container health transitions; enable maintenance and prove cashier checkout returns 503 while health/admin remain usable; keep external providers disabled. Reconcile before and after every fault. Any duplicate posting, imbalance, authorization bypass, secret/phone leakage, unhandled stack trace or unrecovered readiness failure is a go-live blocker.

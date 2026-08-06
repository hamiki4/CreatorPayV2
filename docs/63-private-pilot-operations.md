# Milestone 31: private pilot deployment and operations

## Scope and reused controls

Pilot is controlled and non-public. It reuses JWT role/status policies, merchant/creator approvals, staff invitations, partnership and Offer eligibility, QR authentication/reuse rules, serializable idempotent checkout posting, the double-entry journal, manual deposit/payout workflows, reversals/disputes, notification outbox, support, health endpoints, masked admin views, audits, PostgreSQL migrations, and backup/restore scripts.

## Configuration and deployment

Create `.env.pilot` from `.env.pilot.example` in the deployment secret store; never commit it. Replace every placeholder and use distinct Pilot credentials and HTTPS origins. Deploy API, Worker, Web, and PostgreSQL 17 (or managed PostgreSQL) with `docker compose -f docker-compose.yml -f docker-compose.pilot.yml --env-file .env.pilot up -d`. Apply migrations first with `pwsh scripts/database/apply-migrations.ps1`. Bootstrap the first admin using `docs/41-pilot-deployment-guide.md`, deliver the one-time credential out of band, and rotate it. Synthetic seeding is explicit via `scripts/pilot/reset-and-reseed.ps1`; verify `ASPNETCORE_ENVIRONMENT=Pilot` and never schedule it in Production.

Health URLs are API `/health/live` and `/health/ready`, Web `/healthz`, and the Worker container health check. Run `pwsh scripts/pilot/smoke-test.ps1 -ApiUrl https://api.pilot.example -WebUrl https://pilot.example`.

## Access and onboarding

Creators and Businesses remain non-active until platform approval. Checkout requires active Business, Creator, partnership, Offer/QR, location, and assigned cashier. Staff creation uses owner/admin authorization. Platform admins can suspend creators, merchants, accounts, partnerships, and Offers; decisions are audited. Admin endpoints require `PlatformAdminOnly` and mask contact data.

Business checklist: identity, approval, Business Type, locations, owner, supervisor, cashier, manual Pilot funding, first Offer, Creator relationship, test checkout. Creator checklist: identity/profile, approval, Offer connection, QR generation/download, earnings dashboard, test checkout. Shopper checklist: phone registration/verification, first checkout, cashback, notification, and history. Use protected status/audit views; never copy confidential documents into checklists.

## Money controls

Label funding `PILOT MANUAL FUNDING`. Automatic payout and external payment default off. Configure purchase, commission, daily Business spend, Shopper cashback, Creator earning, payout hold, low-balance, and participant limits under `Pilot`. Payouts require admin review. Existing suspension freezes activity; disputes, reversals, ledger, reconciliation SQL, and audit trails handle corrections. External providers require security review, dual approval, rollback planning, and fresh smoke testing.

## Operations, incidents, and metrics

Daily review readiness, Worker freshness, balances, payout queues, notification failures, support, disputes, fraud alerts, journal imbalances, backups, and onboarding blockers. Track approved Businesses/Creators, active cashiers, registered Shoppers, Offers, QR scans, successful/failed checkouts, duplicate-use rejections, overrides, cashback, earnings, funding, support, disputes, notification failures, uptime, and latency. Reconcile wallet, Creator, Shopper, platform, and journal totals at close.

For a financial imbalance, fraud suspicion, credential exposure, readiness failure, or uncontrolled provider activity, preserve correlation evidence and escalate. Set `FeatureFlags__MaintenanceMode=true` to block cashier checkout while health/admin access remains available. Follow `docs/62-milestone-30-production-operations.md` and `scripts/database` for backup verification, restore rehearsal, and rollback; never restore over the only copy.

## Authenticated smoke test and exit

Sign in as admin; confirm Pilot/system health; verify approved Business, Creator, partnership, and Offer QR; submit a small synthetic checkout as an assigned cashier; confirm wallet debit, Creator earning, Shopper cashback, notification, balanced journal, and audit share a transaction/correlation reference; submit a support request and confirm the protected queue. Reverse the test if policy requires a zeroed baseline.

Exit only after targets are met, critical security/ledger incidents are resolved, reconciliation stays balanced, support/dispute SLAs are sustainable, restore is rehearsed, and stakeholders approve. Public registration/discovery remain off pending a separate launch decision.

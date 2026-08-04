# Database operations

## Local setup

Docker: copy `.env.example` to `.env`, set `POSTGRES_PASSWORD`, run `docker compose up -d postgres`, and test with `docker compose exec postgres pg_isready -U creatorpay -d CreatorPayV2Db`. If an old volume was initialized with different credentials, changing `.env` does not change the stored role password; deliberately update the role or recreate only the confirmed local volume.

Local PostgreSQL: create login `creatorpay`, create database `CreatorPayV2Db` owned by it, then set `ConnectionStrings__CreatorPayDatabase=Host=localhost;Port=5432;Database=CreatorPayV2Db;Username=creatorpay;Password=...`. Test with `psql` or `scripts/database/verify-connection.ps1`. Never hard-code the rejected credential.

Commands: create with `create-migration.ps1`; list with `dotnet ef migrations list --project src/CreatorPay.Infrastructure --startup-project src/CreatorPay.Api`; apply locally/UAT/production with `apply-migrations.ps1` and the environment secret; generate reviewed idempotent SQL with `generate-idempotent-script.ps1`. Production requires backup, approval, maintenance monitoring and post-apply readiness verification.

Backups use `pg_dump` custom format into encrypted restricted storage. Retain daily/weekly/monthly copies according to legal and business policy, enable managed WAL/PITR, test restore quarterly, and record RPO/RTO. `restore.ps1` requires an explicit switch and a verified target. Migration failures roll forward; restoration is an incident procedure, not a normal rollback.

Concurrency strategy: unique/filtered indexes enforce identity rules; row-version optimistic concurrency protects mutable aggregates; idempotency/payout uniqueness resolves races; wallet and balance transitions use database transactions; notification claims use atomic conditional updates; scheduled jobs use PostgreSQL advisory locks. Avoid process-local/global locks.

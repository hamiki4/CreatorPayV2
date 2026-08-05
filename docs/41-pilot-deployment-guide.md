# Pilot deployment guide

This is a non-production Docker Compose deployment: PostgreSQL, API, Worker, and Web. Copy `.env.example` to an untracked `.env`; generate unique PostgreSQL, JWT (32+ characters), phone HMAC, and encryption secrets. Keep provider credentials and automatic payment flags empty/false. Restrict CORS and reverse-proxy settings to pilot hosts and use TLS at the edge.

Procedure: build images; start PostgreSQL; run `dotnet ef database update` from the API image/environment; create demo actors through supported APIs; start API, Worker, and Web; check `/health/live` and `/health/ready`; run `scripts/operations/readiness-smoke.ps1`; execute the scenario in document 40.

Back up with `scripts/database/backup.ps1` before migrations. Restore into an empty, matching PostgreSQL version using `scripts/database/restore.ps1`, then verify readiness and reconciliation. Rollback means stop traffic, restore the pre-change backup, deploy the prior immutable images, and rerun smoke checks; EF migrations are not automatically reversed.

Secrets checklist: no `.env` in Git, distinct secrets per environment, least-privilege database role, encrypted backups, rotation owner/date, no development OTP reveal outside local development. Smoke check auth, discovery, checkout approval, duplicate rejection, worker outbox, and all role boundaries.


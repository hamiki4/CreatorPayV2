# Pilot operations runbook

Start: copy `.env.example` to ignored `.env`, use pilot-only secrets, start PostgreSQL/services, apply migrations, reset/reseed, then run the verifier. Create merchants/creators through registration, verification and admin approval; create/approve/start campaigns through their lifecycle APIs. Record deposits only after external verification. Run checkout with a live three-minute customer QR and explicit approval.

Creator weekly payouts and 1,000 ETB customer payouts are manual: prepare/reserve, mark processing, confirm external evidence, then mark paid. Never infer payment success. Reversals require reason, actor, correlation ID and reconciliation; already-paid amounts become recovery receivables.

Failed checkout: capture correlation ID, session status/expiry, campaign/partnership state, funding/trial state, API/worker logs and journal presence; never retry by inserting money records. Imbalance: stop financial pilot traffic, preserve evidence, run the read-only SQL pack and escalate—never edit balances. Restore: use `scripts/database/restore.ps1`, verify checksum, migrate, run reconciliation and verifier. Rollback: deploy the prior immutable image, do not downgrade destructively, restore only under approved incident procedure.

Escalate for duplicate posts, imbalance, unauthorized access, privacy exposure, unresolved High/Critical finding, restore failure, or manual-money evidence mismatch. Record UTC time, severity, affected IDs, correlation IDs, containment, owner and next update; exclude passwords/tokens/phone numbers.

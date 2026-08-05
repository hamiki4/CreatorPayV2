# Operational SQL and reconciliation

Run [`scripts/reporting/pilot-operational-reconciliation.sql`](../scripts/reporting/pilot-operational-reconciliation.sql) with a transaction read-only database role against a replica or during a quiet window. It covers wallet derivation, unpaid creator earnings, available customer cashback, revenue by period, trial use, manual payout queues, journal imbalance, stale checkout expiration, and campaigns expiring soon.

The journal imbalance and wallet mismatch queries should return zero rows. Investigate differences through audit events, transaction/reversal records, and idempotency keys. Never repair a discrepancy by editing a balance: use the supported reversal/recovery workflow and retain the incident correlation ID. Exported results contain internal IDs and financial data; restrict them as operational records.


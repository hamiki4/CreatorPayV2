# Pilot support runbook

Ask for the on-screen support/correlation ID, actor role, UTC time, public checkout/transaction ID, and observed state; never request a QR token, password, OTP, or full phone number. Check live/readiness, API/Worker structured logs, outbox/dead letters, checkout expiry, transaction idempotency key, journal balance, and client connectivity in that order.

For “cashier waiting,” have both users stay connected, reload customer confirmations, then reload cashier recent purchases. Never create another financial attempt until the first session is confirmed rejected/expired/failed. For low balance or exhausted trial, merchant submits a manual deposit and admin verifies it. For payout failures, mark failed through the workflow so reserved funds are released; do not edit balances.

Severity 1: suspected duplicate posting, journal imbalance, cross-tenant exposure, leaked secret, or checkout without approval. Stop pilot checkout, preserve logs, rotate exposed secrets, and escalate. Severity 2: notification delay, payout queue delay, or isolated availability. The safe degradation is polling/read-only access; offline checkout stays disabled.


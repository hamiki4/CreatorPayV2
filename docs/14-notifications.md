# Notifications

Events include verification, approval/rejection/suspension, partnership request/decision/expiry, QR/security changes, confirmed/reversed earnings, wallet deposit/low balance/restriction, repeat-use OTP/result, payout scheduled/paid/failed, dispute, fraud review and operational failure. Supported channels are in-app, push, SMS and email; Telegram/WhatsApp are future adapters subject to consent/provider approval.

Business transactions write a versioned notification event to an outbox atomically. A worker claims it, resolves localized templates and consent/channel preference, creates per-channel deliveries, and retries transient failures with exponential backoff/jitter. Permanent failures or exhausted retries enter a dead-letter queue with alerting and replay controls. Provider callbacks update `Queued/Sent/Delivered/Failed/Unknown`; callback processing is authenticated and idempotent.

Templates are Platform Admin managed, versioned, previewed, localized (English/Amharic), variable-allowlisted and audited. They must not contain full phone, identity, wallet, QR or token data. OTP templates have stricter TTL and logging rules. An immediate earning notification means queued in the same transaction and processed promptly—not a guarantee of provider delivery. Metrics cover queue age, attempts, delivery rate and dead letters.

# Reporting and Operations

Platform operations include fraud-alert, dispute, and reversal queues with assignment, timelines, accounting preview, confirmations, and append-only audit events. Recovery receivables and open creator recovery balances require manual review.

Production reporting includes persisted `BackgroundJobExecution` history, notification outbox/dead-letter state, readiness health, correlation-aware structured logs, and OpenTelemetry-compatible metric foundations. See the operational and observability runbooks for response procedures and alerts.

Posted wallet entries and balanced journals are append-only operational records. Platform support views can inspect purchases and pending deposits without editing posted financial history.

Reports use authoritative confirmed/posted records, merchant-local dates for commerce views and UTC internally. Every result identifies time zone, currency, generated time and filters. Role scope from [permissions](05-user-roles-and-permissions.md) applies to views and exports.

| Capability | Measures / operator action |
|---|---|
| Creator performance | gross attributed sales, confirmed/reversed count, commission and conversion proxy by merchant/campaign/location/time |
| Merchant performance | sales, commission spend, active creators, wallet runway, reversals |
| Cashier activity | attempts, confirmations, rejects, offline syncs, repeat-use requests by assigned location |
| Wallet statements | opening/closing/available/held balance and immutable entries |
| Creator payout statements | earning state movements, batches, failures and payments |
| Platform revenue | snapshotted platform share, reversals, reconciliation variance |
| Fraud alerts | severity/rule/evidence/age/owner/resolution; no raw sensitive exports by default |
| Failed notifications/payouts | provider-safe error, attempts, next retry, dead-letter/review action |
| Reconciliation | internal vs deposit/payment provider/bank source, matched/unmatched/variance |
| Audit logs | actor/action/target/scope/time/result/reason/correlation; controlled export |

Background jobs include outbox delivery, partnership/campaign expiry, earning availability, weekly payout creation/submission/status polling, low-balance evaluation, reconciliation, fraud evaluation, retention, token cleanup and projection repair. Jobs use distributed leases, bounded batches, checkpoints, idempotency, retry/backoff, dead-letter/manual replay and metrics; overlapping runs cannot duplicate effects.

Monitor structured logs, traces and metrics for API latency/error rate, database saturation, queue age/depth, job freshness, sync rejection, wallet conflicts, payout/notification/provider failures, fraud volume, disk/backup status and certificate/secret expiry. Alerts have severity, owner, runbook and escalation. Public health is coarse; readiness checks dependencies without exposing credentials. Operator actions are least-privileged and audited.
# Payout operations

Operators mature earnings, create a cutoff-keyed weekly batch, start processing, submit through the manual adapter, and record paid or failed outcomes. Fail/cancel returns funds to Available. Audit events and immutable balance entries support investigation; posted payout journals are never edited.
# Notification operations

Operators can inspect outbox status and safe errors, manually retry or cancel eligible messages, and retry or resolve dead letters. Attempts and audit events remain append-only operational evidence. Provider references are metadata only in Milestone 14.
# Milestone 18 reporting

Platform Admin dashboard/report queries use server-side aggregation, UTC date filters, optional merchant scope, projections, cancellation tokens, bounded pagination, and `AsNoTracking`. See [31-platform-admin-portal.md](31-platform-admin-portal.md).

# Milestone 20 — security hardening and production readiness

Milestone 20 preserves the existing API, clean architecture, and database model. It adds fail-closed production configuration, hardened browser headers, immutable promotion gates, checksum-verified backup/restore tooling, executable HTTPS readiness and baseline load checks, and the evidence gates below. No EF migration is required.

## Penetration-test remediation and security review

The release owner must attach the independent test scope and report. Zero open Critical/High findings is a production gate; Medium findings require a named owner, due date, compensating control, and security acceptance. Re-test authorization boundaries for Platform Admin, merchant, location, creator, offline sync, reporting exports, object ownership, refresh-token replay, rate-limit bypass, CORS, forwarded-header spoofing, mass assignment, and financial idempotency.

Implemented controls: production requires explicit HTTPS CORS origins; invalid rate limits and development reveal flags fail startup; forwarded headers trust only valid IPs in `ReverseProxy__KnownProxies__N`; API responses are non-cacheable and receive restrictive CSP/resource/referrer/permissions headers; the static web host denies framing and active mixed/untrusted content. Edge deployments must overwrite client forwarding headers and configure only the immediate proxy address. Never trust an entire public network.

Run dependency, secret, SAST, container, IaC and authenticated DAST scans against the exact release digest. Store reports outside the repository. Exceptions require security approval and expiry. Rotate all credentials used during testing.

## Promotion and cloud deployment

`promote.yml` accepts only an immutable SHA-256 digest, serializes each environment, and binds approval to GitHub environments (`test`, `uat`, `production`). Configure required reviewers and prevent self-review for UAT/production. It deliberately holds no cloud credentials: a provider-specific adapter must use workload identity, consume the approved digest without rebuilding, and record deployment evidence.

Cloud prerequisites: private managed PostgreSQL with PITR; encrypted immutable cross-account backups; managed secrets/workload identity; TLS 1.2+ load balancer/WAF; explicit trusted-proxy configuration; private API/worker networking; centralized retained logs, metrics and traces; image signature/provenance verification; registry retention; DNS/certificate ownership; egress allow-listing; availability-zone placement; resource limits; autoscaling boundaries; and cost alerts. Separate migration identity from runtime identities.

## Backup, restore, and disaster recovery

`backup.ps1` creates a compressed custom-format dump plus SHA-256 manifest. Move both to encrypted immutable storage and record backup ID, PostgreSQL version, size, checksum, retention class, and operator. `restore.ps1` requires explicit destructive confirmation, validates the manifest, asserts the exact target database, restores with stop-on-error, and analyzes the result.

Quarterly and before launch, restore into an isolated non-production account, apply no unreviewed migration, run schema discovery/readiness plus ledger, wallet, earnings, payout, outbox, audit-count and sampled business reconciliation, and record elapsed time. Target RPO: 15 minutes with managed PITR. Target RTO: 4 hours until a measured drill supports a tighter objective. A restore is not complete until the application checks pass and the incident/change owner approves cutover.

## Production SLOs and alerts

Initial 30-day rolling objectives (excluding approved maintenance): API availability 99.9%; purchase-confirmation availability 99.9%; API p95 latency under 500 ms and p99 under 1 s; purchase-confirmation p95 under 750 ms; 99% of notification outbox items processed within 5 minutes; scheduled operational jobs 99% complete within their window. Error-budget burn alerts: page at 14.4× over 1 hour or 6× over 6 hours; ticket at 1× over 3 days. Page on readiness loss, financial invariant failure, backup/PITR failure, dead-letter growth, payout failure spike, or database saturation. Every alert needs an owner, tested route, runbook link, deployment digest, and correlation context.

The chosen collector/cloud adapter must export the existing structured logs and `CreatorPayTelemetry`, add ingress and PostgreSQL platform metrics, and build dashboards before UAT. Synthetic probes run `/health/live` and `/health/ready` from at least two locations; readiness is operational telemetry, not the public availability SLI.

## Performance test and UAT gates

Run `k6 run -e BASE_URL=https://uat.example scripts/load/health-and-readiness.js` as the safe baseline, then execute an approved authenticated workload containing browse/report, QR validation, purchase idempotency/concurrency, offline batch replay, admin queues and outbox processing. Use synthetic data only. Establish expected peak and test 2× peak for 30 minutes plus a 4-hour soak. Capture application/DB saturation, p50/p95/p99, errors, queue age and invariants. The baseline script is not a substitute for the authenticated workload.

UAT uses production-like topology and the exact candidate digest. Required sign-offs: Product (acceptance flows), Finance (wallet/commission/earnings/payout reconciliation), Operations (alerts/runbooks/support/export), Security/Privacy (findings and data handling), Engineering (capacity/rollback/restore), and release owner. Test role boundaries, Amharic/English, accessibility, offline recovery, duplicate submissions, provider failure, rollback, restore, and incident escalation. Record evidence and defects; no Severity 1/2 defects may remain.

## Final production readiness checklist

- [ ] Exact signed/scanned digest passed CI and was promoted without rebuild.
- [ ] Independent penetration re-test has zero open Critical/High findings.
- [ ] Legal, privacy, tax, banking, hosting, payment and provider approvals are recorded.
- [ ] Production secrets are generated in the vault, access reviewed, and rotation rehearsed.
- [ ] HTTPS/CORS, WAF, trusted proxy, DNS, certificates, private networking and egress are verified.
- [ ] Idempotent migration SQL is reviewed; pre-migration backup and forward-fix plan are approved.
- [ ] PITR and isolated restore meet RPO/RTO; business and financial reconciliation passes.
- [ ] SLO dashboards, multi-window alerts, synthetic checks and 24×7 escalation routes are tested.
- [ ] Authenticated 2× peak and soak tests pass with capacity headroom and no invariant failures.
- [ ] UAT sign-offs and accessibility/localization evidence are complete; Severity 1/2 count is zero.
- [ ] Provider certification, reconciliation, kill switches and manual fallback are proven.
- [ ] Deployment, smoke, rollback, incident, breach and customer-communication rehearsals pass.
- [ ] On-call staffing, support ownership, maintenance window and go/no-go authority are named.

Production is **not ready** until every checkbox has dated evidence and an accountable approver. Configuration and documentation alone do not certify an environment.

## Recommended next milestone

Milestone 21 only: production provider integration and certification—real notification/payment adapters, managed secret and telemetry adapters, deployment-provider implementation, end-to-end reconciliation, and controlled pilot rollout.

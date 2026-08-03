# Implementation Roadmap

Milestones 1–3 establish the foundation, persisted domain skeleton and approved requirements blueprint. Later milestones must link changes/tests to [MVP acceptance criteria](21-complete-acceptance-criteria.md), preserve clean architecture and use PostgreSQL integration tests for persistence behavior.

## Completed

- **Milestone 1 — Foundation:** solution layers, DI seams, health endpoint, frontend shell, Docker PostgreSQL and test projects.
- **Milestone 2 — Core domain and persistence:** current identity/organization/partnership entities, EF/Npgsql mappings, migration and domain/metadata tests.
- **Milestone 3 — Software requirements and implementation blueprint:** documentation only; dual approval, transaction, finance, security, API/UI, operations and release contracts. No application behavior added.

## Recommended implementation order

| Milestone | Deliverable / exit emphasis |
|---|---|
| 4 — Identity and authentication foundation | Password hashing, JWT/refresh rotation, verification/reset, authorization/scoping primitives, audit foundation and PostgreSQL tests |
| 5 — Creator registration and platform approval | Creator onboarding/documents/status review; proves Platform approval boundary |
| 6 — Merchant registration and platform approval | Merchant/Admin onboarding, document review and remediation |
| 7 — Merchant organization, locations, supervisors, cashiers | Invitations, account activation, active merchant/location assignment enforcement |
| 8 — Merchant-creator partnership | Discovery/request/direct add, full state graph, restrictions and dual-approval tests |
| 9 — Creator QR lifecycle | Signed permanent QR, key rotation/revocation and validation contract |
| 10 — Commission engine | Versioned defaults/overrides/campaign precedence, ETB rounding and snapshots |
| 11 — Merchant wallet and deposits | Immutable ledger, manual deposits, holds/restriction/reconciliation |
| 12 — Cashier transaction processing | Server eligibility and atomic wallet/earning/revenue/outbox transaction with idempotency/concurrency |
| 13 — Customer phone and repeat-use approval | Normalization/protection, local-day rule, Supervisor plus OTP exception |
| 14 — Creator earnings and notifications | Earning lifecycle/statements, outbox workers, templates/channels |
| 15 — Weekly payouts | Batch/cutoff/minimum/holds, provider port, failure/retry/reconciliation |
| 16 — Disputes, reversals, and fraud | Cases, compensating entries, configurable alerts and manual review |
| 17 — Offline PWA | IndexedDB queue, service worker, sync/approval/rejection recovery |
| 18 — Platform Admin UI | Approval/config/deposit/payout/fraud/template/audit operations |
| 19 — Reporting and operations | Scoped reports, background-job controls, dashboards/runbooks |
| 20 — Security hardening and deployment | Threat/pentest remediation, CI/CD, backup/restore, monitoring, UAT and production readiness |

Milestone 4 should begin with a threat model and decisions for account-to-role cardinality, token transport/storage, verification providers and audit schema. It must not introduce financial workflows.

## Open decision gates

Before relevant milestones: obtain legal/compliance decisions for identity documents, Ethiopian privacy/data residency, retention, tax/KYC/AML; banking decisions for payout beneficiary/rails/cutoff/failed settlement; payment-provider contracts/callbacks/reconciliation; hosting region and RPO/RTO. Defaults in this blueprint are proposals until versioned configuration or an approved decision records them.

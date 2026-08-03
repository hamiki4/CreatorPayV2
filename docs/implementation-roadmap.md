# Implementation Roadmap

Milestone 11 implements the prepaid ETB wallet, manual deposits, atomic purchases, immutable commission snapshots, idempotency, ledger/journal, APIs, and initial role-specific UI. Milestone 12 should build creator earnings from `CreatorPayable` without mutating posted journals.

## Milestone 8 — complete

Merchant–creator discovery, merchant-specific approval, lifecycle history, location/date eligibility, scoped APIs, audits, and web workspaces are implemented. Milestone 9 should consume the eligibility service for QR validation without duplicating partnership policy.

## Milestone 7 — complete

Merchant locations, Supervisor/Cashier profiles and assignments, secure staff invitations, claim-derived scoping, audit events, role-aware React pages, PostgreSQL constraints, and migration `AddMerchantOrganizationAndStaffInvitations` are implemented. Milestone 8 functionality is intentionally not included.

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
# Milestone 4 status

Identity, authentication, authorization policies, rotation/revocation, reset foundations, migration, tests, and security documentation are implemented. Milestone 5 should build registration/verification workflows on these contracts without weakening the active-only login rule until restricted onboarding access is explicitly designed.

# Milestone 5 status

Creator registration, hashed verification challenges, profile management, approval transitions, audit events, authorization, migration, tests, and onboarding dashboard are implemented. The active-only login rule remains unchanged.

Milestone 6 should add merchant and primary Merchant Admin registration, verification, document metadata/review, and Platform Admin approval while reusing these provider and audit boundaries. It must not add partnerships, QR codes, wallets, commissions, or transactions.
# Milestone 9 — complete

Permanent creator QR issuance, regeneration, revocation/history, on-demand PNG generation, merchant/staff location-scoped validation, audit events, APIs, and React workspaces are implemented. Purchase transactions, customer verification, commissions, wallets, earnings, notifications, payouts, and offline synchronization remain explicitly deferred.
# Milestone 10 — complete

The configurable commission domain, PostgreSQL persistence, version selection hierarchy, deterministic calculator, snapshot schema, scoped APIs, auditing, preview UI, migration, tests, and documentation are implemented. Milestone 11 should consume the engine from merchant wallet/purchase processing and persist one snapshot atomically, without recalculating historical purchases.
# Milestone 12 — complete

Creator earnings, category balances and entries, configurable maturation, weekly payout batches, manual processing, payout attempts, settlement accounting, scoped APIs, React workspaces, PostgreSQL migration, tests, and operational documentation are implemented. Milestone 13 may add customer phone verification and repeat-use approval without changing historical earning or journal records.

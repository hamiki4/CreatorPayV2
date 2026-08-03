# Complete MVP Acceptance Criteria

This checklist is the requirements baseline. `AC-*` identifiers must be referenced by implementation issues and automated tests. “Audited” means the append-only event contract in [roles](05-user-roles-and-permissions.md); financial actions must also preserve ledger invariants.

## Blueprint assumptions and open decisions

Assumptions: MVP currency is ETB; Customer is a transaction participant rather than a `UserAccount`; a partnership without location rows applies to all active merchant locations; the base phone rule spans all locations of one merchant; accounting uses two-decimal ETB and conservation rounding; financial corrections are compensating records; public IDs and permanent QR credentials are distinct; configuration is versioned and prospective. These assumptions require product approval before their implementation milestone.

Open decisions: accepted identity/business documents and retention; Ethiopian privacy, consent, data residency and cross-border rules; tax, invoicing, withholding, KYC/AML and financial-record retention; payout beneficiary and rail; deposit/payment provider APIs, callbacks, fees and settlement; campaign/category scope; platform/merchant configuration bounds; telecom number ranges/SMS sender; hosting/DR region and RPO/RTO. No real-money production launch may treat these as silently resolved.

## Identity and dual approval

- [ ] **AC-ID-01** Creator registers with unique normalized email/phone, verifies required channels/documents and remains unable to transact before Platform Admin approval.
- [ ] **AC-ID-02** Merchant and primary Merchant Admin register, verify required business documents and remain unable to transact before Platform Admin approval.
- [ ] **AC-ID-03** Approval/rejection/resubmission/suspension/reactivation/closure follow valid transitions, require reason where specified, notify and are audited.
- [ ] **AC-ID-04** Password reset, access/refresh rotation, revocation, lockout and account-status enforcement pass security tests.
- [ ] **AC-DUAL-01** Platform-approved creator has no merchant eligibility until that merchant separately approves a partnership.
- [ ] **AC-DUAL-02** Creator request, QR/search/direct add, approve/reject/suspend/reinstate/revoke/block/expire/renew flows follow the canonical seven statuses and retain history.
- [ ] **AC-DUAL-03** Partnership date, active location, campaign and assigned commission restrictions are enforced at confirmation and offline synchronization.

## Merchant organization and transaction

- [ ] **AC-ORG-01** Merchant Admin manages only own locations, supervisors and cashiers; location assignments constrain Supervisor/Cashier access.
- [ ] **AC-QR-01** Permanent QR reveals no PII, has valid signature/version/key/purpose, supports rotation/revocation and rejects forgery or inactive creator.
- [ ] **AC-TXN-01** Cashier signs in/selects an active assigned location and all creator, merchant, cashier, location, partnership, date and campaign validations occur server-side.
- [ ] **AC-PHONE-01** Ethiopian input normalizes to E.164; encrypted/HMAC/masked representations and access restrictions are tested.
- [ ] **AC-PHONE-02** Base once-per-merchant-per-merchant-local-calendar-day rule works across merchant locations.
- [ ] **AC-PHONE-03** Repeat use requires an unexpired assigned-Supervisor approval plus customer OTP; denial/expiry prevents confirmation and all steps are audited.
- [ ] **AC-COM-01** Rule priority is campaign creator, partnership, merchant default, platform default; effective version and all inputs/outputs are snapshotted.
- [ ] **AC-COM-02** Configurable creator/platform split conserves gross commission and ETB rounding tests include boundary/property cases; no split is hard-coded.
- [ ] **AC-WAL-01** Confirmation atomically posts balanced merchant debit, creator pending earning, platform revenue and outbox record; any failure posts none.
- [ ] **AC-WAL-02** Concurrent/idempotent requests neither overspend nor duplicate; insufficient/low-balance state rejects new transactions while sign-in/deposit remain available.
- [ ] **AC-NOT-01** Confirmed earning is immediately queued and visible to creator; delivery retry/dead-letter/status/template localization operate without leaking sensitive data.

## Earnings, payouts and corrections

- [ ] **AC-EARN-01** Earnings progress Pending/Available/Held/Scheduled/Paid/Reversed under versioned hold/minimum/cutoff rules and statements reconcile.
- [ ] **AC-PAY-01** Weekly batch selects only eligible available amounts at cutoff, rolls below-minimum balances, is idempotent and audited.
- [ ] **AC-PAY-02** Submit/callback/retry/failure/manual review/reconciliation through the provider abstraction cannot double-pay or falsely mark Paid.
- [ ] **AC-DIS-01** Authorized dispute captures evidence/status/history; a reversal uses linked compensating ledger/earning/revenue entries and the original commission snapshot.
- [ ] **AC-FRAUD-01** Configurable velocity/amount/device/IP rules create reviewable alerts and holds; disposition is reasoned and audited.

## Offline, reporting, security and release

- [ ] **AC-OFF-01** PWA IndexedDB queue uses client operation and idempotency IDs, shows PendingSync rather than confirmed and survives safe restart.
- [ ] **AC-OFF-02** Sync repeats authoritative validation and handles confirmed, rejected and approval-required outcomes without duplicate financial effects.
- [ ] **AC-REP-01** Role-scoped performance, wallet, payout, revenue, fraud, failure, reconciliation and audit reports reconcile to source records and mask phones.
- [ ] **AC-SEC-01** Least privilege, merchant/location isolation, secure password/token/QR/key controls, rate limits, headers/CORS/input/upload validation and sensitive logging pass tests.
- [ ] **AC-SEC-02** Financial/audit history is immutable, retention/backup access is controlled and legal open items are resolved before production processing.
- [ ] **AC-TEST-01** Required unit, application, PostgreSQL, API, frontend, E2E, security, concurrency, ledger and offline suites pass with traceability to these IDs.
- [ ] **AC-DEP-01** Production artifact/security scans, migrations, secrets, monitoring/alerts/runbooks, backup restore, rollback rehearsal and UAT sign-off are complete.

## Definition of MVP acceptance

All applicable boxes pass in UAT and CI; unresolved severity-1/2 security, privacy, ledger, payout or data-loss defects are zero; every open legal/banking/tax/provider decision that gates real money or personal data has a named owner and recorded approval.

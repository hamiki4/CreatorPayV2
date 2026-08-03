# UI Navigation and Screens

Milestone 8 adds creator merchant-search/status views and merchant creator-search/lifecycle views, with responsive loading, empty, validation, and destructive-confirmation states. English copy is ready to move into Amharic resource dictionaries.

## Merchant organization

Merchant Admin navigation exposes Locations, Supervisors, and Cashiers. Each area includes list/empty/loading/error states and contextual create, invite, edit, assignment, primary-location, activation, and deactivation actions. Invitation acceptance is public and token-based. Supervisor and Cashier navigation is restricted to My profile and Assigned locations. No QR UI is present.

Every authenticated shell includes language, notification center, profile/security and sign-out. All screens use skeleton/progress loading, actionable inline/summary errors with retry, and explicit empty states. Destructive/financial/state-changing actions require confirmation and reason where applicable. Mobile layouts provide 44px-class touch targets, camera permission guidance, keyboard-safe forms, low-bandwidth behavior and accessible focus/labels. English and Amharic (`am-ET`) copy uses translation keys, locale-aware ETB/date/number formatting, flexible layouts for longer text and no text baked into images; terminology must be reviewed by fluent speakers.

| Role / screen | Purpose and main components | Fields/actions and permission notes |
|---|---|---|
| Creator: Register/Verify/Status | Onboard and track platform review | identity/contact/password/consent, OTP, documents; submit/resubmit; public/own only |
| Creator: Dashboard | Earnings and required actions | balances, recent earnings, partnership/payout alerts; links only |
| Creator: My QR | Present permanent code | QR, public ID, status, refresh/rotate with reason; Active creator |
| Creator: Merchant discovery/detail | Find participating merchants | search, type/city, merchant/location summary; request promotion |
| Creator: Partnerships/detail | Track approval and performance | status/reason-safe timeline, dates/locations/campaign, request/renew/stop |
| Creator: Earnings/statements/payouts | Financial history | filters, transaction movements, payout destination masked, download own statement |
| Creator: Notifications/profile/security | Preferences and account | read state, locale/channel consent, mutable profile, password/sessions, closure request |
| Merchant Admin: Register/Verify/Status | Business onboarding | legal/trading/contact/tax/document/location fields; submit/resubmit |
| Merchant Admin: Dashboard | Operational snapshot | wallet/low balance, sales/commission, pending creators, staff/actions |
| Merchant Admin: Locations | Manage merchant sites | name/address/city/time zone/status; create/edit/deactivate own locations |
| Merchant Admin: Staff/detail | Manage supervisors/cashiers | identity/contact/role/location assignments/status; invite/edit/deactivate |
| Merchant Admin: Creator discovery/partnership queue/detail | Establish merchant approval | QR scan/search, request source, evidence, dates/locations/campaign/rule; approve/reject/suspend/reinstate/revoke/block/renew |
| Merchant Admin: Campaigns/commission | Configure permitted rules | rate/split/effective dates/assignment and preview; version, not overwrite |
| Merchant Admin: Wallet/deposit/statement | Fund and reconcile | balance/threshold, amount/reference/proof, deposit status, ledger filters/export; deposit remains available while restricted |
| Merchant Admin: Transactions/reports/disputes | Monitor commerce | scoped filters, safe transaction detail, aggregate performance; open dispute, no customer full phone |
| Supervisor: Dashboard | Assigned-location oversight | location picker, pending repeat-use, cashier/activity alerts |
| Supervisor: Repeat-use detail | Decide exception | masked phone, merchant-local prior-use facts, reason, expiry; approve/deny only assigned location |
| Supervisor: Cashier/activity | View delegated operations | assigned cashier status and transaction summaries; management only if granted |
| Cashier: Sign-in/location | Establish scope | credentials, active assigned location; cannot proceed without assignment |
| Cashier: Scan | Validate creator QR | camera/manual accessibility fallback, eligibility state and safe errors |
| Cashier: Sale | Submit purchase | masked phone input, amount ETB, calculated server preview; confirm once |
| Cashier: Repeat-use request | Request exception | reason, pending supervisor/OTP/expiry state |
| Cashier: Offline queue/receipt | Sync and resolve | client operation status, pending/approval/rejected/confirmed; retry safe operations, never edit confirmed entries |
| Platform Admin: Dashboard/queues | Platform operations | approval, deposit, payout, fraud, failed delivery and health summaries |
| Platform Admin: Creator/Merchant review | Verify and decide | submitted versions/documents, checks/history; approve/reject/resubmit/suspend/reactivate/close |
| Platform Admin: Configuration | Version platform policy | commissions/splits, wallet threshold, payout minimum/day/cutoff/hold, reuse/fraud; publish future-effective version |
| Platform Admin: Deposits/wallet reconciliation | Verify funding | proof/reference/duplicates/ledger; approve/reject/adjust with controls |
| Platform Admin: Payout batches/detail | Run weekly payouts | eligibility/cutoff, holds, submit/status/retry/reconcile |
| Platform Admin: Fraud/disputes | Investigate cases | evidence timeline, linked records, hold/resolve/reverse; reason required |
| Platform Admin: Notifications | Manage templates/delivery | localized preview/version, retries/dead letters; no secret variables |
| Platform Admin: Reports/audit/health | Operate platform | revenue/performance/reconciliation, immutable audit search/export, job/dependency health |

No Customer portal is planned for MVP: customer UI is the minimal OTP/consent confirmation flow opened from a secure expiring challenge.
# Milestone 9 screens

The creator workspace includes My QR, on-demand PNG display/download, issue status/history, and confirmed regeneration/revocation actions. Merchant Admin, Supervisor, and Cashier workspaces include assigned-location selection, manual QR payload entry, and clear validation results. Camera capture remains a future enhancement; no amount or transaction controls are present.

# User Roles and Permissions

## Milestone 7 organization permissions

- Merchant Admin: manages only locations, supervisors, cashiers, assignments, statuses, and invitations for the `MerchantId` in authenticated claims.
- Supervisor: reads only their own profile and assigned locations; cannot manage merchant settings or staff.
- Cashier: reads only their own profile and assigned active locations; cannot manage merchant settings or staff.
- Platform Admin: may inspect organization and audit data for support; the dedicated UI is deferred.
- Creator: has no access to merchant organization APIs.

## Scope rules

Authorization is deny-by-default and enforced server-side. `PlatformAdmin` is platform-scoped; merchant staff are restricted by `MerchantId`; supervisors and cashiers are additionally restricted to active location assignments. A customer is a transaction participant identified by protected phone/OTP data, not an MVP `UserAccount` role. Support impersonation is out of scope.

| Role | View/create | Approve/edit/deactivate | Never access |
|---|---|---|---|
| Platform Admin | All operational records, masked sensitive data, configuration, reports | Platform approvals, status, commission/split defaults, wallet threshold (initial default 1,000 ETB), minimum payout, payout schedule/cutoff, reuse/fraud policies, templates | Passwords, raw secrets, full payment credentials; plaintext phone without an authorized purpose |
| Creator | Own profile, QR, partnerships, earnings, statements; create partnership/closure requests | Edit own mutable profile; stop a partnership | Other creators' private data; merchant wallet, customer phone, platform split internals beyond own statement |
| Merchant Admin | Own merchant, locations, staff, partnerships, campaigns, transactions, wallet/reports | Partnership decisions; create/edit/deactivate own locations/staff/rules within policy | Other merchants; creator identity documents unless explicitly authorized; platform-wide configuration |
| Supervisor | Assigned-location cashiers, transactions, repeat-use requests, limited reports | Approve/deny repeat use; manage cashier assignment only if delegated | Wallet deposits/configuration, payouts, other locations/merchants |
| Cashier | Own account, active assigned locations, scan outcome, transaction result | Create transaction/repeat-use requests; select assigned location | Wallet ledger detail, creator earnings, reports, raw customer phone after submission |
| Customer | OTP prompt and transaction/consent result | Confirm/deny OTP | Operational portals, creator/merchant financial data |

Merchant Admin reads are always merchant-scoped. Supervisor and Cashier reads/writes require both matching merchant and active location assignment; supplied IDs never override claims. Platform Admin sensitive reads require a reason and elevated permission.

## Required audit events

Log actor, effective role, UTC time, action, target/type, merchant/location scope, result, reason, correlation/IP/device metadata, and safe before/after fields for: authentication/security changes; verification and approval decisions; role, staff, location, status and configuration changes; partnership transitions; QR lifecycle; transaction attempts and overrides; phone/OTP/repeat-use access; wallet, deposit, commission, earning, payout, refund/reversal/dispute actions; exports; sensitive-data access; notification-template changes. Audit records are append-only and must not contain secrets or full phone values.

## Configuration ownership

Platform Admin configures versioned platform defaults; Merchant Admin may configure merchant values only where platform policy permits. No creator/platform split (including 50/50 or 70/30) is implicit. Every financial transaction stores the resolved rule and split snapshot.
# Milestone 4 authorization foundation

Server policies are `PlatformAdminOnly`, `CreatorOnly`, `MerchantAdminOnly`, `SupervisorOnly`, `CashierOnly`, `MerchantOperations`, and `AuthenticatedUser`. Future merchant/location use cases must validate persisted ownership and assignments in Application services using `ICurrentUserService`; frontend route visibility is not authorization.

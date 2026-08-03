# QR and Cashier Transaction Workflow

The creator has a permanent opaque QR credential. It contains no personal/financial data and is signed/versioned; rotation or revocation invalidates compromised credentials without changing the public creator identity.

```mermaid
sequenceDiagram
  actor C as Cashier
  participant P as Cashier PWA
  participant A as API
  participant D as PostgreSQL
  participant O as Outbox
  C->>P: Sign in, select assigned location, scan QR
  P->>A: Validate QR and context
  A->>D: Check QR, creator, merchant, cashier/location, partnership/campaign
  A-->>P: Eligible / reason-safe rejection
  C->>P: Enter customer phone and purchase amount
  P->>A: Confirm with idempotency + client operation IDs
  A->>D: Normalize/protect phone; repeat-use check; resolve commission; lock wallet
  D->>D: Atomic transaction, wallet debit, earning credit, platform revenue, snapshots
  D->>O: Queue notifications
  A-->>P: Confirmed transaction receipt
```

The ordered flow is: sign in; select active assigned location; scan; validate signature/version/revocation; validate creator; merchant; cashier and assignment; partnership dates/locations/campaign; collect and normalize/protect customer phone; check merchant-local calendar-day reuse; collect positive ETB purchase amount; calculate on server; validate available wallet balance; atomically confirm; debit wallet; credit pending creator earnings; record platform revenue; queue notifications; return confirmation. Never trust client calculations or cached approval.

Transaction statuses are `Draft`, `PendingSync`, `ApprovalRequired`, `Confirmed`, `Rejected`, `Reversed`, and `Cancelled`. Financial amounts use decimal minor-unit-aware rules from [commission engine](10-commission-engine.md).

## Offline PWA

The installable PWA may store encrypted/minimized pending operations in IndexedDB with a UUID client operation ID, generated idempotency key, captured location, QR token, customer protected input, amount, device time and rule/context version hints. It displays `PendingSync`, never “paid/confirmed.” On reconnect it submits in creation order; the server repeats every validation using authoritative synchronization time and returns the original result for a reused key/payload.

Rejected syncs are immutable locally with a reason-safe code and correction/new-operation route. A duplicate-phone case becomes `ApprovalRequired`; a Supervisor approves/denies and customer confirms OTP before expiry. Conflicting reuse of a key with different content is rejected. Logout/device loss clears or makes encrypted data inaccessible. Service workers do not cache secrets or authenticated API responses indiscriminately.

Acceptance: concurrent confirmations cannot overspend a wallet or double-credit; one logical operation produces at most one transaction/ledger set; failure rolls back all financial and outbox writes.

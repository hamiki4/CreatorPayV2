# Merchant Wallet and Deposits

Each merchant has a prepaid ETB wallet backed by an append-only double-entry-style ledger. `available balance = posted deposits and credits − posted debits − active holds`. Cached balances are projections checked against the ledger.

## Deposits and controls

For MVP, Merchant Admin submits amount, channel/reference and proof. A Platform Admin independent reviewer verifies evidence and posts or rejects it; duplicate provider/reference values are prevented. Future Telebirr or another provider is behind a provider abstraction with signed callback verification, idempotency and reconciliation; provider-specific fields and behavior remain open.

Ledger entry types include DepositPending, DepositPosted, HoldPlaced/Released/Captured, CommissionDebit, RefundCredit, ReversalCredit/Debit and Adjustment (reason plus dual authorization). Entries are immutable, UTC-stamped, currency-specific, correlated to their source and balanced. Never edit/delete a financial entry; compensate it.

The initial suggested low-balance threshold is 1,000 ETB but is Platform Admin configurable per policy/merchant. Crossing it queues a warning. Insufficient funds or `LowBalanceRestricted` blocks new commission transactions but not sign-in, statements, deposit submission, or remediation. Atomic row/version locking prevents the available balance becoming negative. Holds are reserved, expiring amounts and are not spendable.

Daily automated and operator reconciliation compares internal deposits/ledger/provider settlement; discrepancies become cases. Refund/reversal rules use the original transaction snapshot and link all entries. Deposit decisions, threshold changes, holds, restrictions, adjustments and exports require audit events.

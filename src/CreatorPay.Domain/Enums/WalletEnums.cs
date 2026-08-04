namespace CreatorPay.Domain.Enums;

public enum MerchantWalletStatus { Active, LowBalance, Restricted, Suspended, Closed }
public enum MerchantWalletEntryType { Deposit, CommissionDebit, RefundCredit, ReversalCredit, AdjustmentDebit, AdjustmentCredit, Hold, HoldRelease }
public enum MerchantDepositStatus { Initiated, PendingVerification, Completed, Failed, Cancelled, Reversed }
public enum MerchantWalletHoldStatus { Active, Released, Cancelled }
public enum TransactionStatus { Pending, Confirmed, Rejected, Cancelled, Disputed, PartiallyReversed, Reversed, Settled }
public enum JournalAccount { MerchantWalletLiability, CreatorPayable, CreatorRecoveryReceivable, PlatformCommissionRevenue, PaymentClearing, Suspense }
public enum JournalLineType { Debit, Credit }
public enum IdempotencyStatus { Processing, Completed, Failed }

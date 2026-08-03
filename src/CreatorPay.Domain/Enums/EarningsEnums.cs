namespace CreatorPay.Domain.Enums;

public enum CreatorEarningStatus { Pending, Available, OnHold, ScheduledForPayout, Paid, Reversed }
public enum CreatorBalanceCategory { Pending, Available, Held, Scheduled, Paid, Reversed }
public enum CreatorBalanceEntryType { EarningPendingCredit, PendingToAvailable, AvailableToHold, HoldToAvailable, AvailableToScheduled, ScheduledToAvailable, ScheduledToPaid, EarningReversal, ManualAdjustment }
public enum PayoutBatchStatus { Draft, Scheduled, Processing, Completed, PartiallyCompleted, Failed, Cancelled }
public enum CreatorPayoutStatus { Draft, Scheduled, Processing, Submitted, Paid, Failed, Cancelled, Reversed }
public enum PayoutMethodType { Manual, Telebirr, BankTransfer }
public enum PayoutAttemptStatus { Submitted, Paid, Failed, Cancelled }

namespace CreatorPay.Domain.Enums;

public enum CustomerStatus { Active, Suspended, Closed }
public enum CheckoutSessionStatus { Created, Presented, AwaitingCustomerApproval, Approved, Completed, Rejected, Expired, Cancelled }
public enum CustomerCashbackEntryType { Earned, PayoutReserved, PayoutReleased, PayoutPaid, Reversal, Recovery }
public enum CustomerPayoutStatus { Requested, Processing, Paid, Failed, Cancelled }
public enum TrialCreditStatus { Active, Exhausted, Converted }

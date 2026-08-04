namespace CreatorPay.Domain.Enums;

public enum FraudAlertSeverity { Low, Medium, High, Critical }
public enum FraudAlertStatus { Open, UnderReview, Cleared, ConfirmedFraud, ActionTaken, Closed }
public enum FraudEvaluationDecision { Allow, AllowWithAlert, RequireManualReview, Block }
public enum DisputeType { CustomerDidNotPurchase, IncorrectAmount, DuplicateTransaction, UnauthorizedPhoneUse, WrongCreator, CashierError, MerchantComplaint, CreatorComplaint, SuspectedFraud, Other }
public enum DisputeStatus { Open, UnderReview, AwaitingEvidence, Approved, Rejected, Resolved, Cancelled }
public enum ReversalType { Full, Partial }
public enum ReversalStatus { Pending, Approved, Processing, Completed, Failed, Cancelled }
public enum CreatorRecoveryStatus { Open, PartiallyRecovered, Recovered, Waived }

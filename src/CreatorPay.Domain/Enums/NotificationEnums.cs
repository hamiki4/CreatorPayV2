namespace CreatorPay.Domain.Enums;

public enum NotificationStatus { Pending, Processing, PartiallySent, Sent, Delivered, Failed, Cancelled, Expired }
public enum NotificationPriority { Low, Normal, High, Critical }
public enum NotificationChannel { InApp, Email, Sms, Push, Telegram, WhatsApp }
public enum NotificationRecipientStatus { Pending, Processing, Submitted, Delivered, Failed, Cancelled, Read }
public enum NotificationOutboxStatus { Pending, Processing, Completed, Failed, DeadLettered, Cancelled }
public enum DeliveryAttemptStatus { Pending, Processing, Submitted, Delivered, Failed, Cancelled }
public enum NotificationDeadLetterStatus { Open, Retrying, Resolved }
public enum NotificationRecipientType { User, Customer, PlatformOperations }

public enum NotificationType
{
    CreatorRegistrationReceived, CreatorApproved, CreatorRejected, CreatorSuspended, CreatorReactivated,
    MerchantRegistrationReceived, MerchantApproved, MerchantRejected, MerchantSuspended, MerchantReactivated,
    StaffInvitationCreated, StaffInvitationAccepted, PartnershipRequested, PartnershipApproved, PartnershipRejected,
    PartnershipSuspended, PartnershipRevoked, PartnershipBlocked, QrIssued, QrRegenerated, QrRevoked,
    MerchantDepositSubmitted, MerchantDepositApproved, MerchantDepositRejected, MerchantWalletLowBalance,
    MerchantWalletInsufficient, PurchaseConfirmed, PurchaseRejected, CreatorEarningConfirmed, CreatorEarningAvailable,
    RepeatUseApprovalRequested, RepeatUseApproved, RepeatUseDenied, CustomerVerificationCode,
    CustomerConfirmationSucceeded, PayoutScheduled, PayoutSubmitted, PayoutPaid, PayoutFailed,
    CheckoutApprovalRequired, CustomerCashbackEarned, CustomerCashbackThresholdReached, CustomerPayoutRequested, CustomerPayoutPaid,
    PasswordResetRequested, SecurityAlert, SystemOperationalAlert
}

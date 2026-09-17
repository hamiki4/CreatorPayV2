using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CreatorPay.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();
    public DbSet<ExternalProfileLink> ExternalProfileLinks => Set<ExternalProfileLink>();
    public DbSet<ExternalApplicationSession> ExternalApplicationSessions => Set<ExternalApplicationSession>();
    public DbSet<Creator> Creators => Set<Creator>();
    public DbSet<CreatorSocialProfile> CreatorSocialProfiles => Set<CreatorSocialProfile>();
    public DbSet<Customer> Customers => Set<Customer>(); public DbSet<CustomerWallet> CustomerWallets => Set<CustomerWallet>(); public DbSet<CustomerCashbackEntry> CustomerCashbackEntries => Set<CustomerCashbackEntry>(); public DbSet<CustomerRecoveryBalance> CustomerRecoveryBalances => Set<CustomerRecoveryBalance>(); public DbSet<CustomerPayoutRequest> CustomerPayoutRequests => Set<CustomerPayoutRequest>(); public DbSet<CheckoutSession> CheckoutSessions => Set<CheckoutSession>(); public DbSet<SavedPromotion> SavedPromotions => Set<SavedPromotion>(); public DbSet<MerchantStoreQr> MerchantStoreQrs => Set<MerchantStoreQr>(); public DbSet<MerchantPromotionProfile> MerchantPromotionProfiles => Set<MerchantPromotionProfile>(); public DbSet<MerchantTrialCredit> MerchantTrialCredits => Set<MerchantTrialCredit>(); public DbSet<PlatformRevenueEntry> PlatformRevenueEntries => Set<PlatformRevenueEntry>();
    public DbSet<CreatorQrCode> CreatorQrCodes => Set<CreatorQrCode>();
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<MerchantLocation> MerchantLocations => Set<MerchantLocation>();
    public DbSet<Supervisor> Supervisors => Set<Supervisor>();
    public DbSet<Cashier> Cashiers => Set<Cashier>();
    public DbSet<CashierLocationAssignment> CashierLocationAssignments => Set<CashierLocationAssignment>();
    public DbSet<SupervisorLocationAssignment> SupervisorLocationAssignments => Set<SupervisorLocationAssignment>();
    public DbSet<MerchantCreatorPartnership> MerchantCreatorPartnerships => Set<MerchantCreatorPartnership>();
    public DbSet<CreatorMerchantCampaign> CreatorMerchantCampaigns => Set<CreatorMerchantCampaign>();
    public DbSet<CampaignQrCode> CampaignQrCodes => Set<CampaignQrCode>();
    public DbSet<CampaignRenewalRequest> CampaignRenewalRequests => Set<CampaignRenewalRequest>();
    public DbSet<PartnershipLocation> PartnershipLocations => Set<PartnershipLocation>();
    public DbSet<PartnershipStatusHistory> PartnershipStatusHistories => Set<PartnershipStatusHistory>();
    public DbSet<PromotionVideo> PromotionVideos => Set<PromotionVideo>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<LoginAudit> LoginAudits => Set<LoginAudit>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<PinResetAuthorization> PinResetAuthorizations => Set<PinResetAuthorization>();
    public DbSet<PhoneOtpChallenge> PhoneOtpChallenges => Set<PhoneOtpChallenge>();
    public DbSet<CreatorVerificationToken> CreatorVerificationTokens => Set<CreatorVerificationToken>();
    public DbSet<CreatorAuditEvent> CreatorAuditEvents => Set<CreatorAuditEvent>();
    public DbSet<MerchantDocument> MerchantDocuments => Set<MerchantDocument>();
    public DbSet<MerchantVerificationToken> MerchantVerificationTokens => Set<MerchantVerificationToken>();
    public DbSet<MerchantAuditEvent> MerchantAuditEvents => Set<MerchantAuditEvent>();
    public DbSet<StaffInvitation> StaffInvitations => Set<StaffInvitation>();
    public DbSet<CommissionPlan> CommissionPlans => Set<CommissionPlan>();
    public DbSet<CommissionRule> CommissionRules => Set<CommissionRule>();
    public DbSet<CommissionRuleVersion> CommissionRuleVersions => Set<CommissionRuleVersion>();
    public DbSet<PlatformCommissionAssignment> PlatformCommissionAssignments => Set<PlatformCommissionAssignment>();
    public DbSet<MerchantCommissionAssignment> MerchantCommissionAssignments => Set<MerchantCommissionAssignment>();
    public DbSet<PartnershipCommissionAssignment> PartnershipCommissionAssignments => Set<PartnershipCommissionAssignment>();
    public DbSet<CampaignCommissionAssignment> CampaignCommissionAssignments => Set<CampaignCommissionAssignment>();
    public DbSet<CommissionCalculationSnapshot> CommissionCalculationSnapshots => Set<CommissionCalculationSnapshot>();
    public DbSet<CommissionAuditEvent> CommissionAuditEvents => Set<CommissionAuditEvent>();
    public DbSet<PlatformFinancialSetting> PlatformFinancialSettings => Set<PlatformFinancialSetting>();
    public DbSet<BusinessTypeWalletMinimumVersion> BusinessTypeWalletMinimumVersions => Set<BusinessTypeWalletMinimumVersion>();
    public DbSet<PayoutScheduleVersion> PayoutScheduleVersions => Set<PayoutScheduleVersion>();
    public DbSet<MerchantWallet> MerchantWallets => Set<MerchantWallet>();
    public DbSet<MerchantWalletEntry> MerchantWalletEntries => Set<MerchantWalletEntry>();
    public DbSet<MerchantDeposit> MerchantDeposits => Set<MerchantDeposit>();
    public DbSet<MerchantWalletHold> MerchantWalletHolds => Set<MerchantWalletHold>();
    public DbSet<FinancialJournal> FinancialJournals => Set<FinancialJournal>();
    public DbSet<FinancialJournalLine> FinancialJournalLines => Set<FinancialJournalLine>();
    public DbSet<PurchaseTransaction> PurchaseTransactions => Set<PurchaseTransaction>();
    public DbSet<TransactionStatusHistory> TransactionStatusHistories => Set<TransactionStatusHistory>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<CreatorEarning> CreatorEarnings => Set<CreatorEarning>(); public DbSet<CreatorBalanceAccount> CreatorBalanceAccounts => Set<CreatorBalanceAccount>(); public DbSet<CreatorBalanceEntry> CreatorBalanceEntries => Set<CreatorBalanceEntry>();
    public DbSet<PayoutBatch> PayoutBatches => Set<PayoutBatch>(); public DbSet<CreatorPayout> CreatorPayouts => Set<CreatorPayout>(); public DbSet<PayoutItem> PayoutItems => Set<PayoutItem>(); public DbSet<PayoutAttempt> PayoutAttempts => Set<PayoutAttempt>();
    public DbSet<CustomerPhoneReference> CustomerPhoneReferences => Set<CustomerPhoneReference>(); public DbSet<RepeatUseApprovalRequest> RepeatUseApprovalRequests => Set<RepeatUseApprovalRequest>(); public DbSet<CustomerConfirmation> CustomerConfirmations => Set<CustomerConfirmation>(); public DbSet<RepeatUseApprovalHistory> RepeatUseApprovalHistories => Set<RepeatUseApprovalHistory>();
    public DbSet<Notification> Notifications => Set<Notification>(); public DbSet<NotificationRecipient> NotificationRecipients => Set<NotificationRecipient>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>(); public DbSet<NotificationOutboxMessage> NotificationOutboxMessages => Set<NotificationOutboxMessage>();
    public DbSet<NotificationDeliveryAttempt> NotificationDeliveryAttempts => Set<NotificationDeliveryAttempt>(); public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<NotificationDeadLetter> NotificationDeadLetters => Set<NotificationDeadLetter>(); public DbSet<NotificationAuditEvent> NotificationAuditEvents => Set<NotificationAuditEvent>();
    public DbSet<PushDeviceRegistration> PushDeviceRegistrations => Set<PushDeviceRegistration>();
    public DbSet<BackgroundJobExecution> BackgroundJobExecutions => Set<BackgroundJobExecution>();
    public DbSet<FraudRule> FraudRules => Set<FraudRule>(); public DbSet<FraudRuleVersion> FraudRuleVersions => Set<FraudRuleVersion>(); public DbSet<FraudAlert> FraudAlerts => Set<FraudAlert>(); public DbSet<FraudEvidence> FraudEvidence => Set<FraudEvidence>(); public DbSet<FraudReviewHistory> FraudReviewHistories => Set<FraudReviewHistory>();
    public DbSet<Dispute> Disputes => Set<Dispute>(); public DbSet<DisputeEvidence> DisputeEvidence => Set<DisputeEvidence>(); public DbSet<DisputeStatusHistory> DisputeStatusHistories => Set<DisputeStatusHistory>(); public DbSet<DisputeDecision> DisputeDecisions => Set<DisputeDecision>();
    public DbSet<TransactionReversal> TransactionReversals => Set<TransactionReversal>(); public DbSet<ReversalItem> ReversalItems => Set<ReversalItem>(); public DbSet<ReversalJournalReference> ReversalJournalReferences => Set<ReversalJournalReference>(); public DbSet<ReversalStatusHistory> ReversalStatusHistories => Set<ReversalStatusHistory>(); public DbSet<CreatorRecoveryBalance> CreatorRecoveryBalances => Set<CreatorRecoveryBalance>(); public DbSet<OperationalAuditEvent> OperationalAuditEvents => Set<OperationalAuditEvent>();
    public DbSet<OfflineSyncBatch> OfflineSyncBatches => Set<OfflineSyncBatch>(); public DbSet<OfflineSyncItemResult> OfflineSyncItemResults => Set<OfflineSyncItemResult>();
    public DbSet<OperationalAlert> OperationalAlerts => Set<OperationalAlert>(); public DbSet<OperationalAlertHistory> OperationalAlertHistories => Set<OperationalAlertHistory>();
    public DbSet<SupportRequest> SupportRequests => Set<SupportRequest>();
    public DbSet<SavedReportView> SavedReportViews => Set<SavedReportView>(); public DbSet<ReportExportAudit> ReportExportAudits => Set<ReportExportAudit>(); public DbSet<AlertThresholdPolicy> AlertThresholdPolicies => Set<AlertThresholdPolicy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}

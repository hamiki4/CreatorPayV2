using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CreatorPay.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Creator> Creators => Set<Creator>();
    public DbSet<CreatorQrCode> CreatorQrCodes => Set<CreatorQrCode>();
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<MerchantLocation> MerchantLocations => Set<MerchantLocation>();
    public DbSet<Supervisor> Supervisors => Set<Supervisor>();
    public DbSet<Cashier> Cashiers => Set<Cashier>();
    public DbSet<CashierLocationAssignment> CashierLocationAssignments => Set<CashierLocationAssignment>();
    public DbSet<SupervisorLocationAssignment> SupervisorLocationAssignments => Set<SupervisorLocationAssignment>();
    public DbSet<MerchantCreatorPartnership> MerchantCreatorPartnerships => Set<MerchantCreatorPartnership>();
    public DbSet<PartnershipLocation> PartnershipLocations => Set<PartnershipLocation>();
    public DbSet<PartnershipStatusHistory> PartnershipStatusHistories => Set<PartnershipStatusHistory>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<LoginAudit> LoginAudits => Set<LoginAudit>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}

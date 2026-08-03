using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CreatorPay.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Creator> Creators => Set<Creator>();
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<MerchantLocation> MerchantLocations => Set<MerchantLocation>();
    public DbSet<Supervisor> Supervisors => Set<Supervisor>();
    public DbSet<Cashier> Cashiers => Set<Cashier>();
    public DbSet<CashierLocationAssignment> CashierLocationAssignments => Set<CashierLocationAssignment>();
    public DbSet<SupervisorLocationAssignment> SupervisorLocationAssignments => Set<SupervisorLocationAssignment>();
    public DbSet<MerchantCreatorPartnership> MerchantCreatorPartnerships => Set<MerchantCreatorPartnership>();
    public DbSet<PartnershipLocation> PartnershipLocations => Set<PartnershipLocation>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<LoginAudit> LoginAudits => Set<LoginAudit>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<CreatorVerificationToken> CreatorVerificationTokens => Set<CreatorVerificationToken>();
    public DbSet<CreatorAuditEvent> CreatorAuditEvents => Set<CreatorAuditEvent>();
    public DbSet<MerchantDocument> MerchantDocuments => Set<MerchantDocument>();
    public DbSet<MerchantVerificationToken> MerchantVerificationTokens => Set<MerchantVerificationToken>();
    public DbSet<MerchantAuditEvent> MerchantAuditEvents => Set<MerchantAuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}

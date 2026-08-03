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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}

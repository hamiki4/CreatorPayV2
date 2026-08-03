using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

public sealed class CashierLocationAssignmentConfiguration : IEntityTypeConfiguration<CashierLocationAssignment>
{
    public void Configure(EntityTypeBuilder<CashierLocationAssignment> builder)
    {
        builder.ToTable("cashier_location_assignments"); builder.ConfigureEntity();
        builder.HasIndex(x => new { x.CashierId, x.MerchantLocationId }).IsUnique();
        builder.HasIndex(x => x.CashierId).IsUnique().HasFilter("\"IsActive\" = TRUE AND \"IsPrimary\" = TRUE");
        builder.HasOne(x => x.Cashier).WithMany(x => x.LocationAssignments).HasForeignKey(x => x.CashierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MerchantLocation).WithMany(x => x.CashierAssignments).HasForeignKey(x => x.MerchantLocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

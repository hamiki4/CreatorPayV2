using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

public sealed class PartnershipStatusHistoryConfiguration : IEntityTypeConfiguration<PartnershipStatusHistory>
{
    public void Configure(EntityTypeBuilder<PartnershipStatusHistory> b)
    {
        b.ToTable("partnership_status_history"); b.ConfigureEntity();
        b.Property(x => x.PreviousStatus).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.ChangedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.Reason).HasMaxLength(1000); b.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired();
        b.HasOne(x => x.MerchantCreatorPartnership).WithMany(x => x.StatusHistory).HasForeignKey(x => x.MerchantCreatorPartnershipId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.MerchantCreatorPartnershipId, x.ChangedAtUtc });
    }
}

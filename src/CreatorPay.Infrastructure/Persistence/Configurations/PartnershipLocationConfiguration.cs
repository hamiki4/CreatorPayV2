using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

public sealed class PartnershipLocationConfiguration : IEntityTypeConfiguration<PartnershipLocation>
{
    public void Configure(EntityTypeBuilder<PartnershipLocation> builder)
    {
        builder.ToTable("partnership_locations"); builder.ConfigureEntity();
        builder.HasIndex(x => new { x.MerchantCreatorPartnershipId, x.MerchantLocationId }).IsUnique();
        builder.HasOne(x => x.MerchantCreatorPartnership).WithMany(x => x.Locations).HasForeignKey(x => x.MerchantCreatorPartnershipId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MerchantLocation).WithMany(x => x.PartnershipLocations).HasForeignKey(x => x.MerchantLocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

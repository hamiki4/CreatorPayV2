using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

public sealed class MerchantLocationConfiguration : IEntityTypeConfiguration<MerchantLocation>
{
    public void Configure(EntityTypeBuilder<MerchantLocation> builder)
    {
        builder.ToTable("merchant_locations"); builder.ConfigureEntity();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.AddressLine1).HasMaxLength(250).IsRequired();
        builder.Property(x => x.AddressLine2).HasMaxLength(250);
        builder.Property(x => x.City).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Region).HasMaxLength(100);
        builder.Property(x => x.CountryCode).HasMaxLength(2).IsRequired();
        builder.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.MerchantId);
        builder.HasIndex(x => new { x.MerchantId, x.Name }).IsUnique().HasFilter("\"IsActive\" = TRUE");
        builder.HasIndex(x => new { x.MerchantId, x.IsActive });
        builder.HasOne(x => x.Merchant).WithMany(x => x.Locations).HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Restrict);
    }
}

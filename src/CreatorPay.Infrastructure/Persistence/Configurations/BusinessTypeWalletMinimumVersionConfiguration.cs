using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

public sealed class BusinessTypeWalletMinimumVersionConfiguration : IEntityTypeConfiguration<BusinessTypeWalletMinimumVersion>
{
    public void Configure(EntityTypeBuilder<BusinessTypeWalletMinimumVersion> builder)
    {
        builder.ToTable("business_type_wallet_minimum_versions");
        builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(x => x.BusinessType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.MinimumBusinessWalletBalance).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.CurrencyCode, x.BusinessType, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => new { x.CurrencyCode, x.BusinessType, x.EffectiveFromUtc }).IsUnique();
    }
}

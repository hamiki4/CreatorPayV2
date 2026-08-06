using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

public sealed class MerchantConfiguration : IEntityTypeConfiguration<Merchant>
{
    public void Configure(EntityTypeBuilder<Merchant> builder)
    {
        builder.ToTable("merchants", table => table.HasCheckConstraint("CK_merchants_BusinessType_Valid", "\"BusinessType\" IN ('Restaurant / Café','Grocery / Mini-market','Clothing / Boutique','Beauty / Salon','Furniture','Electronics','Hotel / Travel','Professional Services','Other')")); builder.ConfigureEntity();
        builder.Property(x => x.PublicMerchantId).HasMaxLength(20).IsRequired();
        builder.Property(x => x.LegalBusinessName).HasMaxLength(250).IsRequired();
        builder.Property(x => x.TradingName).HasMaxLength(250).IsRequired();
        builder.Property(x => x.BusinessType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PrimaryContactName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(x => x.NormalizedPhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.Property(x => x.TaxRegistrationNumber).HasMaxLength(100);
        builder.Property(x => x.BusinessRegistrationNumber).HasMaxLength(100);
        builder.Property(x => x.PreferredLanguage).HasMaxLength(10).IsRequired();
        builder.Property(x => x.PublicDescription).HasMaxLength(1000);
        builder.Property(x => x.Category).HasMaxLength(100);
        builder.Property(x => x.OpeningHours).HasMaxLength(500);
        builder.Property(x => x.BusinessAddress).HasMaxLength(500).IsRequired();
        builder.Property(x => x.City).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Region).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Country).HasMaxLength(100).IsRequired();
        builder.Property(x => x.TimeZone).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LogoFileName).HasMaxLength(255);
        builder.Property(x => x.LogoContentType).HasMaxLength(100);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.ApprovedAtUtc).HasColumnType("timestamp with time zone");
        builder.HasIndex(x => x.PublicMerchantId).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.NormalizedPhoneNumber);
        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasIndex(x => x.TradingName);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.NoAction);
    }
}

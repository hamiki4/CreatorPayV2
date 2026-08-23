using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

public sealed class PlatformFinancialSettingConfiguration : IEntityTypeConfiguration<PlatformFinancialSetting>
{
    public void Configure(EntityTypeBuilder<PlatformFinancialSetting> builder)
    {
        builder.ToTable("platform_financial_settings");
        builder.HasIndex(x => x.CurrencyCode).IsUnique();
        builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(x => x.MinimumBusinessWalletBalance).HasPrecision(18, 2);
        builder.Property(x => x.MinimumTikTokFollowers).HasColumnType("bigint");
    }
}

public sealed class PayoutScheduleVersionConfiguration : IEntityTypeConfiguration<PayoutScheduleVersion>
{
    public void Configure(EntityTypeBuilder<PayoutScheduleVersion> builder)
    {
        builder.ToTable("payout_schedule_versions");
        builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.HasIndex(x => new { x.CurrencyCode, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => new { x.CurrencyCode, x.EffectiveFromUtc }).IsUnique();
    }
}

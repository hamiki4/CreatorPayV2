using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace CreatorPay.Infrastructure.Persistence.Configurations;

internal sealed class CreatorMerchantCampaignConfiguration : IEntityTypeConfiguration<CreatorMerchantCampaign>
{
    public void Configure(EntityTypeBuilder<CreatorMerchantCampaign> b) { b.ConfigureEntity(); b.Property(x => x.PublicCampaignId).HasMaxLength(40); b.Property(x => x.CampaignCode).HasMaxLength(24); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30); b.Property(x => x.ReuseRule).HasConversion<string>().HasMaxLength(20).HasDefaultValue(OfferReuseRule.OncePerOffer); b.Property(x => x.Conditions).HasMaxLength(1000); b.Property(x => x.RowVersion).IsRowVersion(); b.HasIndex(x => x.PublicCampaignId).IsUnique(); b.HasIndex(x => x.Status); b.HasIndex(x => x.ExpiresAtUtc); b.HasIndex(x => new { x.MerchantCreatorPartnershipId, x.Status }); b.HasOne(x => x.Partnership).WithMany().HasForeignKey(x => x.MerchantCreatorPartnershipId).OnDelete(DeleteBehavior.Restrict); b.HasOne<Creator>().WithMany().HasForeignKey(x => x.CreatorId).OnDelete(DeleteBehavior.Restrict); b.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Restrict); b.HasOne<CommissionRuleVersion>().WithMany().HasForeignKey(x => x.CommissionRuleVersionId).OnDelete(DeleteBehavior.Restrict); b.HasOne<CreatorMerchantCampaign>().WithMany().HasForeignKey(x => x.RenewedFromCampaignId).OnDelete(DeleteBehavior.Restrict); }
}
internal sealed class CampaignQrCodeConfiguration : IEntityTypeConfiguration<CampaignQrCode>
{ public void Configure(EntityTypeBuilder<CampaignQrCode> b) { b.ConfigureEntity(); b.Property(x => x.PublicQrId).HasMaxLength(40); b.Property(x => x.TokenHash).HasMaxLength(64); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20); b.HasIndex(x => x.PublicQrId).IsUnique(); b.HasIndex(x => x.TokenHash).IsUnique(); b.HasIndex(x => x.CampaignId).IsUnique(); b.HasOne(x => x.Campaign).WithOne(x => x.QrCode).HasForeignKey<CampaignQrCode>(x => x.CampaignId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class CampaignRenewalRequestConfiguration : IEntityTypeConfiguration<CampaignRenewalRequest>
{ public void Configure(EntityTypeBuilder<CampaignRenewalRequest> b) { b.ConfigureEntity(); b.Property(x => x.Status).HasConversion<string>(); b.Property(x => x.CreatorNote).HasMaxLength(500); b.HasIndex(x => new { x.ExpiredCampaignId, x.Status }); b.HasOne<CreatorMerchantCampaign>().WithMany().HasForeignKey(x => x.ExpiredCampaignId).OnDelete(DeleteBehavior.Restrict); } }

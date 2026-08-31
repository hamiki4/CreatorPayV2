using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

public sealed class PromotionVideoConfiguration : IEntityTypeConfiguration<PromotionVideo>
{
    public void Configure(EntityTypeBuilder<PromotionVideo> builder)
    {
        builder.ToTable("promotion_videos");
        builder.ConfigureEntity();
        builder.Property(x => x.VideoUrl).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Platform).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.SubmittedAtUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.ReviewedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ExpiredAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.RejectionReason).HasMaxLength(1000);
        builder.HasIndex(x => x.MerchantCreatorPartnershipId);
        builder.HasIndex(x => new { x.MerchantCreatorPartnershipId, x.Status })
            .HasDatabaseName("IX_promotion_videos_pending_per_partnership")
            .IsUnique()
            .HasFilter("\"Status\" = 'Pending'");
        builder.HasIndex(x => new { x.MerchantId, x.Status });
        builder.HasIndex(x => new { x.CreatorId, x.Status });
        builder.HasIndex(x => new { x.Status, x.SubmittedAtUtc });
        builder.HasOne(x => x.MerchantCreatorPartnership).WithMany(x => x.PromotionVideos).HasForeignKey(x => x.MerchantCreatorPartnershipId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Creator>().WithMany().HasForeignKey(x => x.CreatorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.NoAction);
    }
}

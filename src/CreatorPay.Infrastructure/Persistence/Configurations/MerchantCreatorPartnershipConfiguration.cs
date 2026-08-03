using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

public sealed class MerchantCreatorPartnershipConfiguration : IEntityTypeConfiguration<MerchantCreatorPartnership>
{
    public void Configure(EntityTypeBuilder<MerchantCreatorPartnership> builder)
    {
        builder.ToTable("merchant_creator_partnerships"); builder.ConfigureEntity();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.RequestedAtUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.ApprovedAtUtc).HasColumnType("timestamp with time zone"); builder.Property(x => x.RejectedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.SuspendedAtUtc).HasColumnType("timestamp with time zone"); builder.Property(x => x.StartDateUtc).HasColumnType("timestamp with time zone"); builder.Property(x => x.EndDateUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.RejectionReason).HasMaxLength(1000); builder.Property(x => x.SuspensionReason).HasMaxLength(1000);
        builder.Property(x => x.IntroductoryMessage).HasMaxLength(2000);
        builder.Property<uint>("xmin").IsRowVersion();
        builder.HasIndex(x => new { x.MerchantId, x.CreatorId }).IsUnique();
        builder.HasIndex(x => new { x.MerchantId, x.Status }); builder.HasIndex(x => new { x.CreatorId, x.Status }); builder.HasIndex(x => x.EndDateUtc);
        builder.HasOne(x => x.Merchant).WithMany(x => x.CreatorPartnerships).HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Creator).WithMany(x => x.MerchantPartnerships).HasForeignKey(x => x.CreatorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.RejectedByUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.SuspendedByUserId).OnDelete(DeleteBehavior.NoAction);
    }
}

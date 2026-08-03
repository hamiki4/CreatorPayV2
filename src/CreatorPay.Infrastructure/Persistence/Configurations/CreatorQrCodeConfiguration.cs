using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

internal sealed class CreatorQrCodeConfiguration : IEntityTypeConfiguration<CreatorQrCode>
{
    public void Configure(EntityTypeBuilder<CreatorQrCode> b)
    {
        b.ConfigureEntity(); b.ToTable("CreatorQrCodes");
        b.Property(x => x.PublicQrId).HasMaxLength(40).IsRequired();
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.IssuedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.RevokedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.RevocationReason).HasMaxLength(500);
        b.HasIndex(x => x.PublicQrId).IsUnique(); b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.CreatorId, x.IsActive });
        b.HasIndex(x => x.CreatorId).IsUnique().HasFilter("\"IsActive\" = TRUE");
        b.HasIndex(x => x.RevokedAtUtc);
        b.HasOne(x => x.Creator).WithMany(x => x.QrCodes).HasForeignKey(x => x.CreatorId).OnDelete(DeleteBehavior.Restrict);
    }
}

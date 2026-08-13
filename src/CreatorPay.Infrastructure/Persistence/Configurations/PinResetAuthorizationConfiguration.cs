using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

public sealed class PinResetAuthorizationConfiguration : IEntityTypeConfiguration<PinResetAuthorization>
{
    public void Configure(EntityTypeBuilder<PinResetAuthorization> builder)
    {
        builder.ToTable("pin_reset_authorizations");
        builder.ConfigureEntity();
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.FirebaseUid).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ExpiresAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UsedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.RequestedByIp).HasMaxLength(64);
        builder.Property(x => x.UsedByIp).HasMaxLength(64);
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.UserAccountId, x.ExpiresAtUtc });
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

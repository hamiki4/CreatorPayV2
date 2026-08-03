using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

public sealed class StaffInvitationConfiguration : IEntityTypeConfiguration<StaffInvitation>
{
    public void Configure(EntityTypeBuilder<StaffInvitation> builder)
    {
        builder.ToTable("staff_invitations"); builder.ConfigureEntity();
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.Property(x => x.NormalizedEmail).HasMaxLength(320).IsRequired();
        builder.Property(x => x.UserRole).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ExpiresAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.AcceptedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.RevokedAtUtc).HasColumnType("timestamp with time zone");
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.ExpiresAtUtc);
        builder.HasIndex(x => new { x.MerchantId, x.NormalizedEmail });
        builder.HasOne(x => x.Merchant).WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Supervisor>().WithMany().HasForeignKey(x => x.SupervisorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Cashier>().WithMany().HasForeignKey(x => x.CashierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.InvitedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

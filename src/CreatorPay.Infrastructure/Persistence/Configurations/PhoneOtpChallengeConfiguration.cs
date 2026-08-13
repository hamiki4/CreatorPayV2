using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

public sealed class PhoneOtpChallengeConfiguration : IEntityTypeConfiguration<PhoneOtpChallenge>
{
    public void Configure(EntityTypeBuilder<PhoneOtpChallenge> b)
    {
        b.ToTable("phone_otp_challenges"); b.ConfigureEntity();
        b.Property(x => x.Purpose).HasMaxLength(32).IsRequired();
        b.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.RequestedByIp).HasMaxLength(64);
        b.HasIndex(x => new { x.UserAccountId, x.Purpose, x.CreatedAtUtc });
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

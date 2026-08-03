using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens"); builder.ConfigureEntity();
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TokenFamily).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RevokedReason).HasMaxLength(200);
        builder.Property(x => x.ReplacedByTokenHash).HasMaxLength(64);
        builder.Property(x => x.CreatedByIp).HasMaxLength(64); builder.Property(x => x.RevokedByIp).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.HasIndex(x => x.TokenHash).IsUnique(); builder.HasIndex(x => x.TokenFamily);
        builder.HasIndex(x => new { x.UserAccountId, x.ExpiresAtUtc });
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class LoginAuditConfiguration : IEntityTypeConfiguration<LoginAudit>
{
    public void Configure(EntityTypeBuilder<LoginAudit> builder)
    {
        builder.ToTable("login_audits"); builder.ConfigureEntity();
        builder.Property(x => x.NormalizedEmail).HasMaxLength(320); builder.Property(x => x.FailureReason).HasMaxLength(100);
        builder.Property(x => x.IpAddress).HasMaxLength(64); builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.Property(x => x.CorrelationId).HasMaxLength(100);
        builder.HasIndex(x => x.AttemptedAtUtc); builder.HasIndex(x => x.NormalizedEmail);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens"); builder.ConfigureEntity();
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired(); builder.Property(x => x.RequestedByIp).HasMaxLength(64); builder.Property(x => x.UsedByIp).HasMaxLength(64);
        builder.HasIndex(x => x.TokenHash).IsUnique(); builder.HasIndex(x => new { x.UserAccountId, x.ExpiresAtUtc });
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

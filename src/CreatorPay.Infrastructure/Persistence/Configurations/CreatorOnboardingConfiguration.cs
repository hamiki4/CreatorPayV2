using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

public sealed class CreatorVerificationTokenConfiguration : IEntityTypeConfiguration<CreatorVerificationToken>
{
    public void Configure(EntityTypeBuilder<CreatorVerificationToken> builder)
    {
        builder.ToTable("creator_verification_tokens"); builder.ConfigureEntity();
        builder.Property(x => x.Purpose).HasMaxLength(16).IsRequired();
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.UserAccountId, x.Purpose, x.ExpiresAtUtc });
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CreatorAuditEventConfiguration : IEntityTypeConfiguration<CreatorAuditEvent>
{
    public void Configure(EntityTypeBuilder<CreatorAuditEvent> builder)
    {
        builder.ToTable("creator_audit_events"); builder.ConfigureEntity();
        builder.Property(x => x.EventType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Detail).HasMaxLength(1000);
        builder.HasIndex(x => new { x.CreatorId, x.CreatedAtUtc });
        builder.HasOne<Creator>().WithMany().HasForeignKey(x => x.CreatorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorUserAccountId).OnDelete(DeleteBehavior.NoAction);
    }
}

using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

public sealed class ExternalIdentityConfiguration : IEntityTypeConfiguration<ExternalIdentity>
{
    public void Configure(EntityTypeBuilder<ExternalIdentity> builder)
    {
        builder.ToTable("external_identities"); builder.ConfigureEntity();
        builder.Property(x => x.Issuer).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Environment).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => new { x.Issuer, x.Environment, x.ExternalUserId }).IsUnique();
        builder.HasIndex(x => new { x.IdentityBindingId, x.IdentityBindingVersion });
    }
}

public sealed class ExternalProfileLinkConfiguration : IEntityTypeConfiguration<ExternalProfileLink>
{
    public void Configure(EntityTypeBuilder<ExternalProfileLink> builder)
    {
        builder.ToTable("external_profile_links"); builder.ConfigureEntity();
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.ProvisioningKey).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.ProvisioningKey).IsUnique();
        builder.HasIndex(x => new { x.ExternalIdentityId, x.Role, x.ExternalProfileSubjectId }).IsUnique();
        builder.HasIndex(x => new { x.ExternalIdentityId, x.Role }).IsUnique()
            .HasFilter("\"Role\" <> 'MerchantAdmin'");
        builder.HasIndex(x => new { x.ExternalIdentityId, x.Role, x.Status }).IsUnique()
            .HasFilter("\"ExternalProfileSubjectId\" IS NULL");
        builder.HasIndex(x => x.UserAccountId).IsUnique().HasFilter("\"UserAccountId\" IS NOT NULL");
        builder.HasOne<ExternalIdentity>().WithMany().HasForeignKey(x => x.ExternalIdentityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ExternalApplicationSessionConfiguration : IEntityTypeConfiguration<ExternalApplicationSession>
{
    public void Configure(EntityTypeBuilder<ExternalApplicationSession> builder)
    {
        builder.ToTable("external_application_sessions"); builder.ConfigureEntity();
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Purpose).HasMaxLength(40).IsRequired();
        builder.Property(x => x.RevokedReason).HasMaxLength(200);
        builder.HasIndex(x => new { x.ExternalIdentityId, x.ExpiresAtUtc });
        builder.HasIndex(x => new { x.UserAccountId, x.RevokedAtUtc });
        builder.HasOne<ExternalIdentity>().WithMany().HasForeignKey(x => x.ExternalIdentityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExternalProfileLink>().WithMany().HasForeignKey(x => x.ExternalProfileLinkId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

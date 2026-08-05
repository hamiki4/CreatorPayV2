using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

public sealed class CreatorConfiguration : IEntityTypeConfiguration<Creator>
{
    public void Configure(EntityTypeBuilder<Creator> builder)
    {
        builder.ToTable("creators"); builder.ConfigureEntity();
        builder.Property(x => x.PublicCreatorId).HasMaxLength(20).IsRequired();
        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(x => x.NormalizedPhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.Property(x => x.PreferredLanguage).HasMaxLength(10).IsRequired();
        builder.Property(x => x.City).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Zone).HasMaxLength(120);
        builder.Property(x => x.Biography).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ContentCategories).HasMaxLength(500).IsRequired();
        builder.Property(x => x.GovernmentIdReference).HasMaxLength(200);
        builder.Property(x => x.TaxIdentificationNumber).HasMaxLength(100);
        builder.Property(x => x.PreferredPayoutChannel).HasMaxLength(100);
        builder.Property(x => x.PreferredPayoutAccountIdentifier).HasMaxLength(200);
        builder.Property(x => x.ProfileImageFileName).HasMaxLength(255);
        builder.Property(x => x.ProfileImageContentType).HasMaxLength(100);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.ApprovedAtUtc).HasColumnType("timestamp with time zone");
        builder.HasIndex(x => x.PublicCreatorId).IsUnique();
        builder.HasIndex(x => x.NormalizedPhoneNumber).IsUnique().HasFilter("\"NormalizedPhoneNumber\" <> ''");
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Email);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class CreatorSocialProfileConfiguration : IEntityTypeConfiguration<CreatorSocialProfile>
{
    public void Configure(EntityTypeBuilder<CreatorSocialProfile> builder)
    {
        builder.ToTable("creator_social_profiles"); builder.ConfigureEntity();
        builder.Property(x => x.Platform).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Handle).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ProfileUrl).HasMaxLength(500);
        builder.Property(x => x.VerificationStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.CreatorId, x.Platform, x.Handle }).IsUnique();
        builder.HasOne(x => x.Creator).WithMany(x => x.SocialProfiles).HasForeignKey(x => x.CreatorId).OnDelete(DeleteBehavior.Cascade);
    }
}

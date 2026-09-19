using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

public sealed class CreatorRatingConfiguration : IEntityTypeConfiguration<CreatorRating>
{
    public void Configure(EntityTypeBuilder<CreatorRating> builder)
    {
        builder.ConfigureEntity();

        builder.ToTable("creator_ratings", table =>
        {
            table.HasCheckConstraint(
                "CK_creator_ratings_rating",
                "\"Rating\" >= 1 AND \"Rating\" <= 5");
        });

        builder.Property(x => x.Rating).IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        // One Business-Creator partnership contributes one rating.
        builder.HasIndex(x => x.MerchantCreatorPartnershipId)
            .HasDatabaseName("UX_creator_ratings_partnership")
            .IsUnique();

        // Supports Creator aggregate / Top Creators queries.
        builder.HasIndex(x => x.CreatorId)
            .HasDatabaseName("IX_creator_ratings_creator");

        builder.HasIndex(x => new { x.CreatorId, x.Rating })
            .HasDatabaseName("IX_creator_ratings_creator_rating");

        builder.HasIndex(x => x.MerchantId)
            .HasDatabaseName("IX_creator_ratings_merchant");

        builder.HasOne(x => x.Partnership)
            .WithMany()
            .HasForeignKey(x => x.MerchantCreatorPartnershipId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Merchant)
            .WithMany()
            .HasForeignKey(x => x.MerchantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Creator)
            .WithMany()
            .HasForeignKey(x => x.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

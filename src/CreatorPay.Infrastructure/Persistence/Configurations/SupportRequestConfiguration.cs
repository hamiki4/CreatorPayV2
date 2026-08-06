using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

internal sealed class SupportRequestConfiguration : IEntityTypeConfiguration<SupportRequest>
{
    public void Configure(EntityTypeBuilder<SupportRequest> b)
    {
        b.ConfigureEntity();
        b.Property(x => x.PublicReference).HasMaxLength(32); b.HasIndex(x => x.PublicReference).IsUnique();
        b.Property(x => x.Name).HasMaxLength(120); b.Property(x => x.Contact).HasMaxLength(254);
        b.Property(x => x.UserType).HasMaxLength(40); b.Property(x => x.Subject).HasMaxLength(160);
        b.Property(x => x.Message).HasMaxLength(4000); b.Property(x => x.PreferredLanguage).HasMaxLength(2);
        b.Property(x => x.Status).HasMaxLength(20); b.HasIndex(x => new { x.Status, x.CreatedAtUtc });
    }
}

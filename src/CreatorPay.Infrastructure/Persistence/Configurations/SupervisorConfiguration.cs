using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

public sealed class SupervisorConfiguration : IEntityTypeConfiguration<Supervisor>
{
    public void Configure(EntityTypeBuilder<Supervisor> builder)
    {
        builder.ToTable("supervisors"); builder.ConfigureEntity();
        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired(); builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired(); builder.Property(x => x.NormalizedEmail).HasMaxLength(320).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(32).IsRequired(); builder.Property(x => x.NormalizedPhoneNumber).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.MerchantId); builder.HasIndex(x => x.NormalizedEmail); builder.HasIndex(x => x.NormalizedPhoneNumber);
        builder.HasOne(x => x.Merchant).WithMany(x => x.Supervisors).HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Restrict);
    }
}

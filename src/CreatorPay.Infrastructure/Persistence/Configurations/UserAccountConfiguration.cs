using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

public sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("user_accounts");
        builder.ConfigureEntity();
        builder.Property(x => x.DisplayName).HasMaxLength(200);
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.NormalizedEmail).HasMaxLength(320);
        builder.Property(x => x.PhoneNumber).HasMaxLength(16);
        builder.Property(x => x.NormalizedPhoneNumber).HasMaxLength(16);
        builder.Property(x => x.PasswordHash).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.AuthenticationSource).HasConversion<string>().HasMaxLength(32).IsRequired()
            .HasDefaultValue(AuthenticationSource.Local);
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.LastLoginAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.LockoutEndUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.LastFailedLoginAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.BirthDate).HasColumnType("date");
        builder.Property(x => x.FirebaseUid).HasMaxLength(128);
        builder.Property(x => x.RecoveryEmail).HasMaxLength(320);
        builder.Property(x => x.NormalizedRecoveryEmail).HasMaxLength(320);
        builder.Property(x => x.PinHash).HasMaxLength(1000);
        builder.Property(x => x.PinLockedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.PinRetryNotBeforeUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.PinEnrolledAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.PinChangedAtUtc).HasColumnType("timestamp with time zone");
        builder.HasIndex(x => x.NormalizedEmail).IsUnique().HasFilter("\"NormalizedEmail\" <> ''");
        builder.HasIndex(x => x.NormalizedPhoneNumber).IsUnique().HasFilter("\"NormalizedPhoneNumber\" IS NOT NULL");
        builder.HasIndex(x => x.FirebaseUid).IsUnique().HasFilter("\"FirebaseUid\" IS NOT NULL");
        builder.HasIndex(x => x.NormalizedRecoveryEmail).IsUnique().HasFilter("\"NormalizedRecoveryEmail\" IS NOT NULL");
        builder.HasIndex(x => x.CreatorId).IsUnique().HasFilter("\"CreatorId\" IS NOT NULL");
        builder.HasIndex(x => x.CustomerId).IsUnique().HasFilter("\"CustomerId\" IS NOT NULL");
        builder.HasIndex(x => x.MerchantId);
        builder.HasIndex(x => x.MerchantId).IsUnique().HasFilter("\"MerchantId\" IS NOT NULL AND \"Role\" = 'MerchantAdmin'");
        builder.HasIndex(x => x.SupervisorId).IsUnique().HasFilter("\"SupervisorId\" IS NOT NULL");
        builder.HasIndex(x => x.CashierId).IsUnique().HasFilter("\"CashierId\" IS NOT NULL");
        builder.HasOne<Creator>().WithMany().HasForeignKey(x => x.CreatorId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Supervisor>().WithMany().HasForeignKey(x => x.SupervisorId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Cashier>().WithMany().HasForeignKey(x => x.CashierId).OnDelete(DeleteBehavior.NoAction);
    }
}

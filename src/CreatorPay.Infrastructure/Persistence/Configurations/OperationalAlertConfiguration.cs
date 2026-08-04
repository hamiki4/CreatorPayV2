using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

internal sealed class OperationalAlertConfiguration : IEntityTypeConfiguration<OperationalAlert>
{
    public void Configure(EntityTypeBuilder<OperationalAlert> b)
    {
        b.ConfigureEntity();
        b.Property(x => x.AlertType).HasMaxLength(100);
        b.Property(x => x.Severity).HasMaxLength(20);
        b.Property(x => x.Status).HasMaxLength(20);
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.SafeDescription).HasMaxLength(1000);
        b.Property(x => x.CorrelationId).HasMaxLength(100);
        b.Property(x => x.CooldownKey).HasMaxLength(200);
        b.HasIndex(x => new { x.Status, x.Severity, x.DetectedAtUtc });
        b.HasIndex(x => new { x.CooldownKey, x.Status }).IsUnique().HasFilter("\"Status\" <> 'Resolved'");
        b.HasMany(x => x.History).WithOne(x => x.Alert).HasForeignKey(x => x.OperationalAlertId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OperationalAlertHistoryConfiguration : IEntityTypeConfiguration<OperationalAlertHistory>
{
    public void Configure(EntityTypeBuilder<OperationalAlertHistory> b)
    {
        b.ConfigureEntity();
        b.Property(x => x.PreviousStatus).HasMaxLength(20);
        b.Property(x => x.NewStatus).HasMaxLength(20);
        b.HasIndex(x => new { x.OperationalAlertId, x.ChangedAtUtc });
    }
}

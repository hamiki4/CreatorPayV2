using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

internal sealed class SavedReportViewConfiguration : IEntityTypeConfiguration<SavedReportView>
{
    public void Configure(EntityTypeBuilder<SavedReportView> b) { b.ConfigureEntity(); b.Property(x => x.OwnerRole).HasMaxLength(30); b.Property(x => x.Name).HasMaxLength(100); b.Property(x => x.ReportType).HasMaxLength(50); b.Property(x => x.FiltersJson).HasColumnType("jsonb"); b.HasIndex(x => new { x.OwnerUserId, x.ReportType, x.Name }).IsUnique(); }
}
internal sealed class ReportExportAuditConfiguration : IEntityTypeConfiguration<ReportExportAudit>
{
    public void Configure(EntityTypeBuilder<ReportExportAudit> b) { b.ConfigureEntity(); b.Property(x => x.RequesterRole).HasMaxLength(30); b.Property(x => x.ReportType).HasMaxLength(50); b.Property(x => x.Format).HasMaxLength(10); b.Property(x => x.PolicyDecision).HasMaxLength(30); b.Property(x => x.CorrelationId).HasMaxLength(100); b.Property(x => x.FiltersJson).HasColumnType("jsonb"); b.HasIndex(x => new { x.RequestedByUserId, x.RequestedAtUtc }); }
}
internal sealed class AlertThresholdPolicyConfiguration : IEntityTypeConfiguration<AlertThresholdPolicy>
{
    public void Configure(EntityTypeBuilder<AlertThresholdPolicy> b) { b.ConfigureEntity(); b.Property(x => x.AlertType).HasMaxLength(100); b.Property(x => x.Severity).HasMaxLength(20); b.Property(x => x.Status).HasMaxLength(20); b.Property(x => x.Threshold).HasPrecision(18, 2); b.HasIndex(x => new { x.AlertType, x.Version }).IsUnique(); b.HasIndex(x => new { x.AlertType, x.Status }); }
}

using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace CreatorPay.Infrastructure.Persistence.Configurations;

internal sealed class BackgroundJobExecutionConfiguration : IEntityTypeConfiguration<BackgroundJobExecution>
{
    public void Configure(EntityTypeBuilder<BackgroundJobExecution> b)
    {
        b.ConfigureEntity(); b.ToTable("BackgroundJobExecutions");
        b.Property(x => x.JobName).HasMaxLength(150).IsRequired(); b.Property(x => x.InstanceId).HasMaxLength(200).IsRequired();
        b.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired(); b.Property(x => x.ErrorCode).HasMaxLength(100); b.Property(x => x.ErrorMessage).HasMaxLength(1000);
        b.Property(x => x.StartedAtUtc).HasColumnType("timestamp with time zone"); b.Property(x => x.CompletedAtUtc).HasColumnType("timestamp with time zone"); b.Property(x => x.FailedAtUtc).HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.JobName, x.Status }); b.HasIndex(x => x.StartedAtUtc);
    }
}

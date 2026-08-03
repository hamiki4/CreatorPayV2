using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

public sealed class SupervisorLocationAssignmentConfiguration : IEntityTypeConfiguration<SupervisorLocationAssignment>
{
    public void Configure(EntityTypeBuilder<SupervisorLocationAssignment> builder)
    {
        builder.ToTable("supervisor_location_assignments"); builder.ConfigureEntity();
        builder.HasIndex(x => new { x.SupervisorId, x.MerchantLocationId }).IsUnique();
        builder.HasOne(x => x.Supervisor).WithMany(x => x.LocationAssignments).HasForeignKey(x => x.SupervisorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MerchantLocation).WithMany(x => x.SupervisorAssignments).HasForeignKey(x => x.MerchantLocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

using CreatorPay.Domain.Common;

namespace CreatorPay.Domain.Entities;

public sealed class SupervisorLocationAssignment : Entity
{
    public Guid SupervisorId { get; set; }
    public Guid MerchantLocationId { get; set; }
    public bool IsActive { get; set; }
    public Supervisor Supervisor { get; set; } = null!;
    public MerchantLocation MerchantLocation { get; set; } = null!;
}

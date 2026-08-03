using CreatorPay.Domain.Common;

namespace CreatorPay.Domain.Entities;

public sealed class CashierLocationAssignment : Entity
{
    public Guid CashierId { get; set; }
    public Guid MerchantLocationId { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; }
    public Cashier Cashier { get; set; } = null!;
    public MerchantLocation MerchantLocation { get; set; } = null!;
}

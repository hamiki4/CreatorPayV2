using CreatorPay.Domain.Common;

namespace CreatorPay.Domain.Entities;

public sealed class PartnershipLocation : Entity
{
    public Guid MerchantCreatorPartnershipId { get; set; }
    public Guid MerchantLocationId { get; set; }
    public bool IsActive { get; set; }
    public MerchantCreatorPartnership MerchantCreatorPartnership { get; set; } = null!;
    public MerchantLocation MerchantLocation { get; set; } = null!;
}

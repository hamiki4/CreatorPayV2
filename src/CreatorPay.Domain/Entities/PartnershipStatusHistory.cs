using CreatorPay.Domain.Common;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Domain.Entities;

public sealed class PartnershipStatusHistory : Entity
{
    public Guid MerchantCreatorPartnershipId { get; set; }
    public PartnershipStatus PreviousStatus { get; set; }
    public PartnershipStatus NewStatus { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public Guid ChangedByUserId { get; set; }
    public string? Reason { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public MerchantCreatorPartnership MerchantCreatorPartnership { get; set; } = null!;
}

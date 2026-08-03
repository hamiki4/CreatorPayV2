using CreatorPay.Domain.Common;

namespace CreatorPay.Domain.Entities;

public sealed class MerchantAuditEvent : Entity
{
    public Guid MerchantId { get; set; }
    public Guid? ActorUserAccountId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Detail { get; set; }
}

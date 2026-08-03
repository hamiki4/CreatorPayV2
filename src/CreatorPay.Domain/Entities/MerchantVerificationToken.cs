using CreatorPay.Domain.Common;

namespace CreatorPay.Domain.Entities;

public sealed class MerchantVerificationToken : Entity
{
    public Guid UserAccountId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
}

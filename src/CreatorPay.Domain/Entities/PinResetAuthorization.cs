using CreatorPay.Domain.Common;

namespace CreatorPay.Domain.Entities;

public sealed class PinResetAuthorization : Entity
{
    public Guid UserAccountId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string FirebaseUid { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public string? RequestedByIp { get; set; }
    public string? UsedByIp { get; set; }
}

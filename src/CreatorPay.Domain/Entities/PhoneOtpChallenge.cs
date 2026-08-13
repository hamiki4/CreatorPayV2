using CreatorPay.Domain.Common;

namespace CreatorPay.Domain.Entities;

public sealed class PhoneOtpChallenge : Entity
{
    public Guid UserAccountId { get; set; }
    public string Purpose { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime LastSentAtUtc { get; set; }
    public string? RequestedByIp { get; set; }
}

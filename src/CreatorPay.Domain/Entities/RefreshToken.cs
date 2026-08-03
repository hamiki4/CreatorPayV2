using CreatorPay.Domain.Common;

namespace CreatorPay.Domain.Entities;

public sealed class RefreshToken : Entity
{
    public Guid UserAccountId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string TokenFamily { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedReason { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }
    public string? UserAgent { get; set; }
    public bool IsActive(DateTime utcNow) => UsedAtUtc is null && RevokedAtUtc is null && ExpiresAtUtc > utcNow;
}

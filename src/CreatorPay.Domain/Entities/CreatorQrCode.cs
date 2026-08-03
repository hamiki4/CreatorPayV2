using CreatorPay.Domain.Common;

namespace CreatorPay.Domain.Entities;

public sealed class CreatorQrCode : Entity
{
    public Guid CreatorId { get; set; }
    public string PublicQrId { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public DateTime IssuedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public Guid? RevokedByUserId { get; private set; }
    public string? RevocationReason { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Creator Creator { get; set; } = null!;

    public void Revoke(DateTime utcNow, Guid actorUserId, string reason)
    {
        if (!IsActive) return;
        IsActive = false; RevokedAtUtc = utcNow; RevokedByUserId = actorUserId;
        RevocationReason = string.IsNullOrWhiteSpace(reason) ? "Revoked by creator" : reason.Trim();
        UpdatedAtUtc = utcNow; UpdatedBy = actorUserId.ToString();
    }
}

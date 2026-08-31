using CreatorPay.Domain.Common;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Domain.Entities;

public sealed class PromotionVideo : Entity
{
    public Guid MerchantCreatorPartnershipId { get; set; }
    public Guid MerchantId { get; set; }
    public Guid CreatorId { get; set; }
    public string VideoUrl { get; set; } = string.Empty;
    public string Platform { get; set; } = "TikTok";
    public PromotionVideoStatus Status { get; private set; } = PromotionVideoStatus.Pending;
    public DateTime SubmittedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime? ExpiredAtUtc { get; private set; }
    public MerchantCreatorPartnership MerchantCreatorPartnership { get; set; } = null!;

    public void Approve(DateTime now, Guid reviewer)
    {
        EnsureUtc(now);
        Status = PromotionVideoStatus.Approved;
        ReviewedAtUtc = now;
        ReviewedByUserId = reviewer;
        RejectionReason = null;
        UpdatedAtUtc = now;
        UpdatedBy = reviewer.ToString();
    }

    public void Reject(DateTime now, Guid reviewer, string? reason)
    {
        EnsureUtc(now);
        Status = PromotionVideoStatus.Rejected;
        ReviewedAtUtc = now;
        ReviewedByUserId = reviewer;
        RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        UpdatedAtUtc = now;
        UpdatedBy = reviewer.ToString();
    }

    public void Expire(DateTime now)
    {
        EnsureUtc(now);
        if (Status == PromotionVideoStatus.Expired) return;
        Status = PromotionVideoStatus.Expired;
        ExpiredAtUtc = now;
        ReviewedAtUtc ??= now;
        UpdatedAtUtc = now;
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", nameof(value));
    }
}

using CreatorPay.Domain.Common;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Domain.Entities;

public sealed class CreatorMerchantCampaign : Entity
{
    public string PublicCampaignId { get; set; } = "";
    public Guid CreatorId { get; set; }
    public Guid MerchantId { get; set; }
    public Guid MerchantCreatorPartnershipId { get; set; }
    public Guid CommissionRuleVersionId { get; private set; }
    public Guid? RenewedFromCampaignId { get; set; }
    public string CampaignCode { get; private set; } = "";
    public CampaignStatus Status { get; private set; } = CampaignStatus.PendingApproval;
    public int DurationDays { get; private set; } = 30;
    public DateTime? MerchantAllowedStartAtUtc { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public DateTime? StartsAtUtc { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public Guid? ApprovedByMerchantUserId { get; private set; }
    public string? Conditions { get; private set; }
    public DateTime? SuspendedAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public DateTime? ExpirationSevenDayReminderAtUtc { get; set; }
    public DateTime? ExpirationOneDayReminderAtUtc { get; set; }
    public uint RowVersion { get; set; }
    public MerchantCreatorPartnership Partnership { get; set; } = null!;
    public CampaignQrCode? QrCode { get; set; }

    public void Approve(int durationDays, DateTime? allowedStart, Guid ruleVersionId, Guid actor, string campaignCode, string? conditions, DateTime now)
    {
        if (Status != CampaignStatus.PendingApproval) throw new InvalidOperationException("Only a pending campaign can be approved.");
        if (durationDays is < 1 or > 365) throw new ArgumentOutOfRangeException(nameof(durationDays));
        DurationDays = durationDays; MerchantAllowedStartAtUtc = allowedStart; CommissionRuleVersionId = ruleVersionId;
        ApprovedByMerchantUserId = actor; ApprovedAtUtc = now; CampaignCode = campaignCode; Conditions = conditions;
        Status = CampaignStatus.ApprovedAwaitingStart; UpdatedAtUtc = now;
    }

    public void Reject(DateTime now) { if (Status != CampaignStatus.PendingApproval) throw new InvalidOperationException("Only a pending campaign can be rejected."); Status = CampaignStatus.Rejected; UpdatedAtUtc = now; }
    public void Start(DateTime now)
    {
        if (Status != CampaignStatus.ApprovedAwaitingStart) throw new InvalidOperationException("Campaign can only be started once after approval.");
        PublishedAtUtc = now; StartsAtUtc = MerchantAllowedStartAtUtc > now ? MerchantAllowedStartAtUtc : now;
        ExpiresAtUtc = StartsAtUtc.Value.AddDays(DurationDays); Status = StartsAtUtc > now ? CampaignStatus.Scheduled : CampaignStatus.Active; UpdatedAtUtc = now;
    }
    public bool ActivateIfDue(DateTime now) { if (Status != CampaignStatus.Scheduled || StartsAtUtc > now) return false; Status = CampaignStatus.Active; QrCode?.Activate(now, ExpiresAtUtc!.Value); UpdatedAtUtc = now; return true; }
    public bool ExpireIfDue(DateTime now) { if (Status is not (CampaignStatus.Active or CampaignStatus.Scheduled) || ExpiresAtUtc > now) return false; Status = CampaignStatus.Expired; QrCode?.Expire(now); UpdatedAtUtc = now; return true; }
    public void Suspend(DateTime now) { if (Status is not (CampaignStatus.Active or CampaignStatus.Scheduled or CampaignStatus.ApprovedAwaitingStart)) throw new InvalidOperationException("Campaign cannot be suspended."); Status = CampaignStatus.Suspended; SuspendedAtUtc = now; QrCode?.Revoke(now); UpdatedAtUtc = now; }
    public void Cancel(DateTime now) { if (Status is CampaignStatus.Expired or CampaignStatus.Cancelled) throw new InvalidOperationException("Campaign cannot be cancelled."); Status = CampaignStatus.Cancelled; CancelledAtUtc = now; QrCode?.Revoke(now); UpdatedAtUtc = now; }
}

public sealed class CampaignQrCode : Entity
{
    public Guid CampaignId { get; set; }
    public string PublicQrId { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public CampaignQrStatus Status { get; private set; } = CampaignQrStatus.Inactive;
    public DateTime IssuedAtUtc { get; set; }
    public DateTime? ActivatedAtUtc { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public CreatorMerchantCampaign Campaign { get; set; } = null!;
    public void Activate(DateTime now, DateTime expires) { if (Status != CampaignQrStatus.Inactive) throw new InvalidOperationException("QR cannot be reactivated."); Status = CampaignQrStatus.Active; ActivatedAtUtc = now; ExpiresAtUtc = expires; UpdatedAtUtc = now; }
    public void Expire(DateTime now) { if (Status == CampaignQrStatus.Revoked) return; Status = CampaignQrStatus.Expired; ExpiresAtUtc ??= now; UpdatedAtUtc = now; }
    public void Revoke(DateTime now) { Status = CampaignQrStatus.Revoked; RevokedAtUtc = now; UpdatedAtUtc = now; }
}

public sealed class CampaignRenewalRequest : Entity
{
    public Guid ExpiredCampaignId { get; set; }
    public Guid CreatorId { get; set; }
    public CampaignRenewalStatus Status { get; set; } = CampaignRenewalStatus.Requested;
    public DateTime RequestedAtUtc { get; set; }
    public string? CreatorNote { get; set; }
    public Guid? NewCampaignId { get; set; }
}


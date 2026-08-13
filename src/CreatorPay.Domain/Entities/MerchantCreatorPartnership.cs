using CreatorPay.Domain.Common;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Domain.Entities;

public sealed class MerchantCreatorPartnership : Entity
{
    public const int ActivePeriodDays = 30;
    public Guid MerchantId { get; set; }
    public Guid CreatorId { get; set; }
    public PartnershipStatus Status { get; private set; } = PartnershipStatus.Pending;
    public DateTime RequestedAtUtc { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? RejectedAtUtc { get; private set; }
    public Guid? RejectedByUserId { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime? SuspendedAtUtc { get; private set; }
    public Guid? SuspendedByUserId { get; private set; }
    public string? SuspensionReason { get; private set; }
    public string? IntroductoryMessage { get; set; }
    public DateTime? StartDateUtc { get; private set; }
    public DateTime? EndDateUtc { get; private set; }
    public Guid? AssignedCommissionRuleId { get; set; }
    public Guid? AssignedCampaignId { get; set; }
    public Merchant Merchant { get; set; } = null!;
    public Creator Creator { get; set; } = null!;
    public ICollection<PartnershipLocation> Locations { get; } = [];
    public ICollection<PartnershipStatusHistory> StatusHistory { get; } = [];

    public void Approve(DateTime approvedAtUtc, Guid approvedByUserId, DateTime? startDateUtc = null, DateTime? endDateUtc = null)
    {
        EnsureUtc(approvedAtUtc);
        if (Status != PartnershipStatus.Pending) throw new InvalidOperationException("Only a pending partnership can be approved.");
        Status = PartnershipStatus.Approved;
        ApprovedAtUtc = approvedAtUtc;
        ApprovedByUserId = approvedByUserId;
        StartDateUtc = approvedAtUtc;
        EndDateUtc = approvedAtUtc.AddDays(ActivePeriodDays);
    }

    public void Reject(DateTime rejectedAtUtc, Guid rejectedByUserId, string reason)
    {
        EnsureUtc(rejectedAtUtc);
        if (Status != PartnershipStatus.Pending) throw new InvalidOperationException("Only a pending partnership can be rejected.");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A rejection reason is required.", nameof(reason));
        Status = PartnershipStatus.Rejected;
        RejectedAtUtc = rejectedAtUtc;
        RejectedByUserId = rejectedByUserId;
        RejectionReason = reason.Trim();
    }

    public void Suspend(DateTime suspendedAtUtc, Guid suspendedByUserId, string reason)
    {
        EnsureUtc(suspendedAtUtc);
        if (Status != PartnershipStatus.Approved) throw new InvalidOperationException("Only an approved partnership can be suspended.");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A suspension reason is required.", nameof(reason));
        Status = PartnershipStatus.Suspended;
        SuspendedAtUtc = suspendedAtUtc;
        SuspendedByUserId = suspendedByUserId;
        SuspensionReason = reason.Trim();
    }

    public void Revoke(DateTime revokedAtUtc, Guid revokedByUserId)
    {
        EnsureUtc(revokedAtUtc);
        if (Status is not (PartnershipStatus.Pending or PartnershipStatus.Approved or PartnershipStatus.Suspended)) throw new InvalidOperationException("Only a pending, approved, or suspended partnership can be revoked.");
        Status = PartnershipStatus.Revoked;
        UpdatedAtUtc = revokedAtUtc;
        UpdatedBy = revokedByUserId.ToString();
    }

    public void Reactivate(DateTime changedAtUtc, Guid changedByUserId)
    {
        EnsureUtc(changedAtUtc);
        if (Status is not (PartnershipStatus.Suspended or PartnershipStatus.Blocked or PartnershipStatus.Revoked)) throw new InvalidOperationException("Only a suspended, blocked, or deactivated partnership can be reactivated.");
        Status = PartnershipStatus.Approved; SuspendedAtUtc = null; SuspendedByUserId = null; SuspensionReason = null;
        StartDateUtc = changedAtUtc; EndDateUtc = changedAtUtc.AddDays(ActivePeriodDays);
        UpdatedAtUtc = changedAtUtc; UpdatedBy = changedByUserId.ToString();
    }

    public void ActivatePromotion(DateTime changedAtUtc, Guid changedByUserId)
    {
        EnsureUtc(changedAtUtc);
        if (Status != PartnershipStatus.Approved) throw new InvalidOperationException("Only an approved partnership can be activated.");
        StartDateUtc = changedAtUtc; EndDateUtc = changedAtUtc.AddDays(ActivePeriodDays);
        UpdatedAtUtc = changedAtUtc; UpdatedBy = changedByUserId.ToString();
    }

    public void Block(DateTime changedAtUtc, Guid changedByUserId)
    {
        EnsureUtc(changedAtUtc);
        if (Status is not (PartnershipStatus.Approved or PartnershipStatus.Suspended)) throw new InvalidOperationException("Only an approved or suspended partnership can be blocked.");
        Status = PartnershipStatus.Blocked; UpdatedAtUtc = changedAtUtc; UpdatedBy = changedByUserId.ToString();
    }

    public void SetDates(DateTime? startDateUtc, DateTime? endDateUtc, DateTime changedAtUtc, Guid changedByUserId)
    {
        EnsureOptionalUtc(startDateUtc); EnsureOptionalUtc(endDateUtc); EnsureUtc(changedAtUtc);
        if (Status == PartnershipStatus.Approved) throw new InvalidOperationException("An active advertising period cannot be manually extended.");
        if (startDateUtc.HasValue && endDateUtc.HasValue && endDateUtc <= startDateUtc) throw new ArgumentException("End date must be after start date.");
        StartDateUtc = startDateUtc; EndDateUtc = endDateUtc; UpdatedAtUtc = changedAtUtc; UpdatedBy = changedByUserId.ToString();
    }

    public bool IsTransactionEligibleAt(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        return Status == PartnershipStatus.Approved
            && StartDateUtc.HasValue
            && EndDateUtc.HasValue
            && StartDateUtc.Value <= utcNow
            && EndDateUtc.Value > utcNow;
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", nameof(value));
    }

    private static void EnsureOptionalUtc(DateTime? value)
    {
        if (value.HasValue) EnsureUtc(value.Value);
    }
}

using CreatorPay.Domain.Common;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Domain.Entities;

public sealed class Creator : Entity
{
    public string PublicCreatorId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string NormalizedPhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ProfileImageFileName { get; set; }
    public string? ProfileImageContentType { get; set; }
    public long? ProfileImageSizeBytes { get; set; }
    public CreatorStatus Status { get; set; } = CreatorStatus.Draft;
    public DateTime? ApprovedAtUtc { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public ICollection<MerchantCreatorPartnership> MerchantPartnerships { get; } = [];

    public void Approve(DateTime approvedAtUtc, Guid approvedByUserId)
    {
        EnsureUtc(approvedAtUtc);
        if (Status != CreatorStatus.PendingApproval) throw new InvalidOperationException("Only a creator pending approval can be approved.");
        Status = CreatorStatus.Active;
        ApprovedAtUtc = approvedAtUtc;
        ApprovedByUserId = approvedByUserId;
    }

    public void Reject(DateTime rejectedAtUtc, string updatedBy)
    {
        EnsureUtc(rejectedAtUtc);
        if (Status != CreatorStatus.PendingApproval) throw new InvalidOperationException("Only a creator pending approval can be rejected.");
        Status = CreatorStatus.Rejected; UpdatedAtUtc = rejectedAtUtc; UpdatedBy = updatedBy;
    }

    public void Suspend(DateTime suspendedAtUtc, string? updatedBy = null)
    {
        EnsureUtc(suspendedAtUtc);
        if (Status != CreatorStatus.Active) throw new InvalidOperationException("Only an active creator can be suspended.");
        Status = CreatorStatus.Suspended;
        UpdatedAtUtc = suspendedAtUtc;
        UpdatedBy = updatedBy;
    }

    public void Reactivate(DateTime reactivatedAtUtc, string updatedBy)
    {
        EnsureUtc(reactivatedAtUtc);
        if (Status != CreatorStatus.Suspended) throw new InvalidOperationException("Only a suspended creator can be reactivated.");
        Status = CreatorStatus.Active; UpdatedAtUtc = reactivatedAtUtc; UpdatedBy = updatedBy;
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", nameof(value));
    }
}

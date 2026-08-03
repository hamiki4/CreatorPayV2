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

    public void Suspend(DateTime suspendedAtUtc, string? updatedBy = null)
    {
        EnsureUtc(suspendedAtUtc);
        if (Status != CreatorStatus.Active) throw new InvalidOperationException("Only an active creator can be suspended.");
        Status = CreatorStatus.Suspended;
        UpdatedAtUtc = suspendedAtUtc;
        UpdatedBy = updatedBy;
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", nameof(value));
    }
}

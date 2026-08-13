using CreatorPay.Domain.Common;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Domain.Entities;

public sealed class Creator : Entity
{
    public string PublicCreatorId { get; set; } = string.Empty;
    public string CreatorCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string NormalizedPhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PreferredLanguage { get; set; } = "en";
    public string City { get; set; } = string.Empty;
    public string? Zone { get; set; }
    public string Biography { get; set; } = string.Empty;
    public string ContentCategories { get; set; } = string.Empty;
    public string? GovernmentIdReference { get; set; }
    public string? TaxIdentificationNumber { get; set; }
    public string? PreferredPayoutChannel { get; set; }
    public string? PreferredPayoutAccountIdentifier { get; set; }
    public DateTime TermsAcceptedAtUtc { get; set; }
    public string? ProfileImageFileName { get; set; }
    public string? ProfileImageContentType { get; set; }
    public long? ProfileImageSizeBytes { get; set; }
    public CreatorStatus Status { get; set; } = CreatorStatus.Draft;
    public DateTime? ApprovedAtUtc { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public ICollection<MerchantCreatorPartnership> MerchantPartnerships { get; } = [];
    public ICollection<CreatorQrCode> QrCodes { get; } = [];
    public ICollection<CreatorSocialProfile> SocialProfiles { get; } = [];

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

    public void RequestCorrection(DateTime changedAtUtc, string updatedBy)
    {
        EnsureUtc(changedAtUtc);
        if (Status != CreatorStatus.PendingApproval) throw new InvalidOperationException("Only a creator pending review can require correction.");
        Status = CreatorStatus.CorrectionRequested; UpdatedAtUtc = changedAtUtc; UpdatedBy = updatedBy;
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", nameof(value));
    }
}

public sealed class CreatorSocialProfile : Entity
{
    public Guid CreatorId { get; set; }
    public SocialPlatform Platform { get; set; }
    public string Handle { get; set; } = string.Empty;
    public string? ProfileUrl { get; set; }
    public long FollowerCount { get; set; }
    public bool IsPrimary { get; set; }
    public SocialProfileVerificationStatus VerificationStatus { get; set; }
    public Creator Creator { get; set; } = null!;
}

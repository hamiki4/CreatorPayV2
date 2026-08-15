using CreatorPay.Domain.Common;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Domain.Entities;

public sealed class Merchant : Entity
{
    public string PublicMerchantId { get; set; } = string.Empty;
    public string LegalBusinessName { get; set; } = string.Empty;
    public string TradingName { get; set; } = string.Empty;
    public string BusinessType { get; set; } = string.Empty;
    public string PrimaryContactName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string NormalizedPhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? TaxRegistrationNumber { get; set; }
    public string? BusinessRegistrationNumber { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public DateTime TermsAcceptedAtUtc { get; set; }
    public string? PublicDescription { get; set; }
    public string? Category { get; set; }
    public string? OpeningHours { get; set; }
    public string BusinessAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public string? LogoFileName { get; set; }
    public string? LogoContentType { get; set; }
    public long? LogoSizeBytes { get; set; }
    public MerchantStatus Status { get; set; } = MerchantStatus.Draft;
    public DateTime? ApprovedAtUtc { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public ICollection<MerchantLocation> Locations { get; } = [];
    public ICollection<Supervisor> Supervisors { get; } = [];
    public ICollection<Cashier> Cashiers { get; } = [];
    public ICollection<MerchantCreatorPartnership> CreatorPartnerships { get; } = [];
    public ICollection<MerchantDocument> Documents { get; } = [];

    public void Approve(DateTime approvedAtUtc, Guid approvedByUserId)
    {
        EnsureUtc(approvedAtUtc);
        if (Status != MerchantStatus.PendingReview) throw new InvalidOperationException("Only a merchant pending review can be approved.");
        Status = MerchantStatus.Active;
        ApprovedAtUtc = approvedAtUtc;
        ApprovedByUserId = approvedByUserId;
    }

    public void Suspend(DateTime suspendedAtUtc, string? updatedBy = null)
    {
        EnsureUtc(suspendedAtUtc);
        if (Status is not (MerchantStatus.Active or MerchantStatus.LowBalanceRestricted)) throw new InvalidOperationException("Only an eligible merchant can be suspended.");
        Status = MerchantStatus.Suspended;
        UpdatedAtUtc = suspendedAtUtc;
        UpdatedBy = updatedBy;
    }

    public void Reject(DateTime rejectedAtUtc, string? updatedBy = null)
    {
        EnsureUtc(rejectedAtUtc);
        if (Status != MerchantStatus.PendingReview) throw new InvalidOperationException("Only a merchant pending review can be rejected.");
        Status = MerchantStatus.Rejected; UpdatedAtUtc = rejectedAtUtc; UpdatedBy = updatedBy;
    }

    public void Reactivate(DateTime reactivatedAtUtc, string? updatedBy = null)
    {
        EnsureUtc(reactivatedAtUtc);
        if (Status != MerchantStatus.Suspended) throw new InvalidOperationException("Only a suspended merchant can be reactivated.");
        Status = MerchantStatus.Active; UpdatedAtUtc = reactivatedAtUtc; UpdatedBy = updatedBy;
    }

    public string EvaluateFunding(decimal balance, decimal minimum, decimal warning, DateTime now, string? updatedBy = null)
    {
        if (Status is MerchantStatus.Suspended or MerchantStatus.Rejected or MerchantStatus.Closed or MerchantStatus.PendingApproval or MerchantStatus.PendingReview or MerchantStatus.CorrectionRequested) return "Unchanged";
        var previous = Status;
        // Minimum activation balance is not a per-transaction block. A funded
        // merchant below the warning threshold remains operational in LowBalance.
        Status = balance < warning ? MerchantStatus.LowBalance : MerchantStatus.Active;
        UpdatedAtUtc = now; UpdatedBy = updatedBy;
        return previous == Status ? "Unchanged" : $"{previous}->{Status}";
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", nameof(value));
    }
}

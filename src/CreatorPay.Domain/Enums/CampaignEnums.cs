namespace CreatorPay.Domain.Enums;

public enum CampaignStatus
{
    PendingApproval,
    ApprovedAwaitingStart,
    Scheduled,
    Active,
    Expired,
    Rejected,
    Suspended,
    Cancelled
}

public enum CampaignQrStatus { Inactive, Active, Expired, Revoked }
public enum CampaignRenewalStatus { Requested, Approved, Rejected, Cancelled }


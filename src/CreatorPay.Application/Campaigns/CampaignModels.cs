namespace CreatorPay.Application.Campaigns;

public sealed class CampaignOptions
{
    public const string SectionName = "Campaigns";
    public int DefaultDurationDays { get; set; } = 30;
    public decimal MinimumActivationBalance { get; set; } = 1000m;
    public decimal LowBalanceWarningThreshold { get; set; } = 1500m;
    public string CurrencyCode { get; set; } = "ETB";
}

public sealed record RequestCampaignRequest(Guid MerchantCreatorPartnershipId, Guid? RenewedFromCampaignId = null, string? Note = null);
public sealed record ApproveCampaignRequest(int DurationDays, Guid CommissionRuleVersionId, DateTime? MerchantAllowedStartAtUtc, string? Conditions, Guid[]? EligibleLocationIds);
public sealed record RejectCampaignRequest(string Reason);
public sealed record RenewalRequest(string? Note);
public sealed record CampaignDto(Guid Id, string PublicCampaignId, Guid CreatorId, Guid MerchantId, Guid PartnershipId, string Status, int DurationDays, DateTime? AllowedStartAtUtc, DateTime? StartsAtUtc, DateTime? ExpiresAtUtc, string? PublicQrId, string? QrStatus, string? CampaignCode, Guid? RenewedFromCampaignId);
public sealed record CampaignApprovalDto(CampaignDto Campaign, string QrPayload);

public interface ICampaignService
{
    Task<CampaignDto> RequestAsync(Guid creatorId, RequestCampaignRequest request, CancellationToken ct);
    Task<IReadOnlyList<CampaignDto>> GetCreatorCampaignsAsync(Guid creatorId, CancellationToken ct);
    Task<IReadOnlyList<CampaignDto>> GetMerchantCampaignsAsync(Guid merchantId, bool pendingOnly, CancellationToken ct);
    Task<CampaignDto> GetAsync(Guid id, Guid? creatorId, Guid? merchantId, CancellationToken ct);
    Task<CampaignApprovalDto> ApproveAsync(Guid merchantId, Guid actor, Guid id, ApproveCampaignRequest request, CancellationToken ct);
    Task<CampaignDto> RejectAsync(Guid merchantId, Guid actor, Guid id, RejectCampaignRequest request, CancellationToken ct);
    Task<CampaignDto> StartAsync(Guid creatorId, Guid actor, Guid id, CancellationToken ct);
    Task<CampaignDto> SuspendAsync(Guid merchantId, Guid actor, Guid id, CancellationToken ct);
    Task<CampaignDto> CancelAsync(Guid merchantId, Guid actor, Guid id, CancellationToken ct);
    Task<CampaignDto> RequestRenewalAsync(Guid creatorId, Guid id, RenewalRequest request, CancellationToken ct);
    Task<int> ProcessLifecycleAsync(CancellationToken ct);
}


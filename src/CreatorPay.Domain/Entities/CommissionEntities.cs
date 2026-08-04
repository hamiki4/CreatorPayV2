using CreatorPay.Domain.Common;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Domain.Entities;

public sealed class CommissionPlan : Entity { public string Name { get; set; } = ""; public string? Description { get; set; } public bool IsActive { get; set; } = true; public ICollection<CommissionRule> Rules { get; } = []; }
public sealed class CommissionRule : Entity { public Guid CommissionPlanId { get; set; } public string Name { get; set; } = ""; public CommissionScopeType ScopeType { get; set; } public string CurrencyCode { get; set; } = ""; public bool IsActive { get; set; } = true; public CommissionPlan Plan { get; set; } = null!; public ICollection<CommissionRuleVersion> Versions { get; } = []; }
public sealed class CommissionRuleVersion : Entity
{
    public Guid CommissionRuleId { get; set; }
    public int VersionNumber { get; set; }
    public decimal MerchantCommissionRatePercent { get; set; }
    public decimal CreatorSharePercent { get; set; }
    public decimal CustomerCashbackSharePercent { get; set; }
    public decimal PlatformSharePercent { get; set; }
    public decimal MinimumPurchaseAmount { get; set; }
    public decimal? MaximumPurchaseAmount { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public CommissionRoundingMode RoundingMode { get; set; }
    public bool IsActive { get; set; } = true; public Guid CreatedByUserId { get; set; }
    public CommissionRule Rule { get; set; } = null!; public ICollection<CommissionCalculationSnapshot> Snapshots { get; } = [];
    public void Validate()
    {
        if (MerchantCommissionRatePercent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(MerchantCommissionRatePercent), "Commission rate must be between 0 and 100.");
        if (CreatorSharePercent < 0 || CustomerCashbackSharePercent < 0 || PlatformSharePercent < 0 || CreatorSharePercent + CustomerCashbackSharePercent + PlatformSharePercent != 100) throw new ArgumentException("Creator, customer, and platform shares must be non-negative and total 100.");
        if (MinimumPurchaseAmount < 0 || MaximumPurchaseAmount < MinimumPurchaseAmount) throw new ArgumentException("Purchase amount bounds are invalid.");
        if (EffectiveFromUtc.Kind != DateTimeKind.Utc || EffectiveToUtc?.Kind is not (null or DateTimeKind.Utc) || EffectiveToUtc <= EffectiveFromUtc) throw new ArgumentException("Effective dates must be UTC and the end must be after the start.");
    }
}
public abstract class CommissionAssignment : Entity { public Guid CommissionRuleId { get; set; } public DateTime EffectiveFromUtc { get; set; } public DateTime? EffectiveToUtc { get; set; } public bool IsActive { get; set; } = true; public CommissionRule Rule { get; set; } = null!; }
public sealed class PlatformCommissionAssignment : CommissionAssignment { public string CurrencyCode { get; set; } = ""; }
public sealed class MerchantCommissionAssignment : CommissionAssignment { public Guid MerchantId { get; set; } }
public sealed class PartnershipCommissionAssignment : CommissionAssignment { public Guid MerchantCreatorPartnershipId { get; set; } }
public sealed class CampaignCommissionAssignment : CommissionAssignment { public Guid CampaignId { get; set; } public Guid? MerchantCreatorPartnershipId { get; set; } }
public sealed class CommissionCalculationSnapshot : Entity
{
    public Guid CommissionRuleId { get; set; }
    public Guid CommissionRuleVersionId { get; set; }
    public string CurrencyCode { get; set; } = "";
    public decimal PurchaseAmount { get; set; }
    public decimal MerchantCommissionRatePercent { get; set; }
    public decimal TotalCommissionAmount { get; set; }
    public decimal CreatorSharePercent { get; set; }
    public decimal CreatorCommissionAmount { get; set; }
    public decimal CustomerCashbackSharePercent { get; set; }
    public decimal CustomerCashbackAmount { get; set; }
    public decimal PlatformSharePercent { get; set; }
    public decimal PlatformCommissionAmount { get; set; }
    public CommissionRoundingMode RoundingMode { get; set; }
    public DateTime CalculatedAtUtc { get; set; }
    public CommissionRuleSourceType RuleSourceType { get; set; }
    public Guid RuleSourceId { get; set; }
    public int CalculationVersion { get; set; } = 1; public CommissionRuleVersion RuleVersion { get; set; } = null!;
}
public sealed class CommissionAuditEvent : Entity { public Guid? ActorUserAccountId { get; set; } public Guid? MerchantId { get; set; } public string EventType { get; set; } = ""; public string? BeforeValues { get; set; } public string? AfterValues { get; set; } public string? CorrelationId { get; set; } }

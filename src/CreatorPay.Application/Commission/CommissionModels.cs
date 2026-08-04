using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Domain.Services;

namespace CreatorPay.Application.Commission;

public sealed record CommissionSelectionRequest(decimal PurchaseAmount, Guid MerchantId, Guid? CreatorId, Guid? PartnershipId, Guid? CampaignId, string CurrencyCode, DateTime CalculationAtUtc);
public sealed record SelectedCommissionRule(CommissionRule Rule, CommissionRuleVersion Version, CommissionRuleSourceType SourceType, Guid SourceId);
public sealed record CommissionPreviewResult(Guid RuleId, Guid RuleVersionId, CommissionRuleSourceType RuleSource, Guid RuleSourceId, string CurrencyCode, CommissionCalculation Calculation);
public interface ICommissionEngine { Task<SelectedCommissionRule> SelectAsync(CommissionSelectionRequest request, CancellationToken ct); Task<CommissionPreviewResult> PreviewAsync(CommissionSelectionRequest request, bool saveSnapshot, CancellationToken ct); }
public sealed class CommissionConfigurationException(string message) : Exception(message);

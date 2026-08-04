using CreatorPay.Application.Commission;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Domain.Services;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPay.Infrastructure.Commission;

public sealed class CommissionEngine(ApplicationDbContext db) : ICommissionEngine
{
    public async Task<SelectedCommissionRule> SelectAsync(CommissionSelectionRequest r, CancellationToken ct)
    {
        if (r.CalculationAtUtc.Kind != DateTimeKind.Utc) throw new CommissionConfigurationException("CalculationAtUtc must be UTC."); var currency = r.CurrencyCode.Trim().ToUpperInvariant();
        CommissionAssignment? a = null; CommissionRuleSourceType source = default;
        if (r.CampaignId is { } campaign) { a = await db.CampaignCommissionAssignments.Include(x => x.Rule).ThenInclude(x => x.Versions).Where(x => x.CampaignId == campaign && (!x.MerchantCreatorPartnershipId.HasValue || x.MerchantCreatorPartnershipId == r.PartnershipId) && x.IsActive && x.EffectiveFromUtc <= r.CalculationAtUtc && (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc > r.CalculationAtUtc)).OrderByDescending(x => x.EffectiveFromUtc).FirstOrDefaultAsync(ct); source = CommissionRuleSourceType.Campaign; }
        if (a is null && r.PartnershipId is { } partnership) { a = await db.PartnershipCommissionAssignments.Include(x => x.Rule).ThenInclude(x => x.Versions).Where(x => x.MerchantCreatorPartnershipId == partnership && x.IsActive && x.EffectiveFromUtc <= r.CalculationAtUtc && (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc > r.CalculationAtUtc)).OrderByDescending(x => x.EffectiveFromUtc).FirstOrDefaultAsync(ct); source = CommissionRuleSourceType.Partnership; }
        if (a is null) { a = await db.MerchantCommissionAssignments.Include(x => x.Rule).ThenInclude(x => x.Versions).Where(x => x.MerchantId == r.MerchantId && x.IsActive && x.EffectiveFromUtc <= r.CalculationAtUtc && (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc > r.CalculationAtUtc)).OrderByDescending(x => x.EffectiveFromUtc).FirstOrDefaultAsync(ct); source = CommissionRuleSourceType.MerchantDefault; }
        if (a is null) { a = await db.PlatformCommissionAssignments.Include(x => x.Rule).ThenInclude(x => x.Versions).Where(x => x.CurrencyCode == currency && x.IsActive && x.EffectiveFromUtc <= r.CalculationAtUtc && (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc > r.CalculationAtUtc)).OrderByDescending(x => x.EffectiveFromUtc).FirstOrDefaultAsync(ct); source = CommissionRuleSourceType.PlatformDefault; }
        if (a is null || !a.Rule.IsActive || a.Rule.CurrencyCode != currency) throw new CommissionConfigurationException("No eligible commission rule exists for the supplied scope, currency, and UTC time.");
        var versions = a.Rule.Versions.Where(x => x.IsActive && x.EffectiveFromUtc <= r.CalculationAtUtc && (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc > r.CalculationAtUtc)).OrderByDescending(x => x.VersionNumber).ToList(); if (versions.Count != 1) throw new CommissionConfigurationException(versions.Count == 0 ? "No active rule version is effective at the supplied UTC time." : "Multiple active rule versions overlap at the supplied UTC time."); return new(a.Rule, versions[0], source, a.Id);
    }
    public async Task<CommissionPreviewResult> PreviewAsync(CommissionSelectionRequest r, bool saveSnapshot, CancellationToken ct) { var s = await SelectAsync(r, ct); var c = CommissionCalculator.Calculate(r.PurchaseAmount, s.Version); var result = new CommissionPreviewResult(s.Rule.Id, s.Version.Id, s.SourceType, s.SourceId, s.Rule.CurrencyCode, c); if (saveSnapshot) { db.CommissionCalculationSnapshots.Add(new() { Id = Guid.NewGuid(), CommissionRuleId = s.Rule.Id, CommissionRuleVersionId = s.Version.Id, CurrencyCode = s.Rule.CurrencyCode, PurchaseAmount = c.PurchaseAmount, MerchantCommissionRatePercent = c.MerchantCommissionRatePercent, TotalCommissionAmount = c.TotalCommissionAmount, CreatorSharePercent = c.CreatorSharePercent, CreatorCommissionAmount = c.CreatorCommissionAmount, PlatformSharePercent = c.PlatformSharePercent, PlatformCommissionAmount = c.PlatformCommissionAmount, RoundingMode = c.RoundingMode, CalculatedAtUtc = r.CalculationAtUtc, RuleSourceType = s.SourceType, RuleSourceId = s.SourceId, CreatedAtUtc = DateTime.UtcNow }); await db.SaveChangesAsync(ct); } return result; }
}

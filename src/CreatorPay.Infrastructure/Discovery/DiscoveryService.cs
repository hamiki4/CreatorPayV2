using System.Security.Cryptography;
using System.Text.Json;
using CreatorPay.Application.Discovery;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPay.Infrastructure.Discovery;

public sealed class DiscoveryService(ApplicationDbContext db) : IDiscoveryService
{
    public async Task<MerchantDiscoveryDto> ResolveStoreQrAsync(string publicQrId, CancellationToken ct)
    {
        var qr = await db.MerchantStoreQrs.SingleOrDefaultAsync(x => x.PublicQrId == publicQrId && x.IsActive, ct)
            ?? throw new KeyNotFoundException("Merchant discovery QR not found.");
        return await GetMerchantAsync(qr.MerchantId, ct);
    }

    public async Task<MerchantDiscoveryQrDto> IssueOrGetStoreQrAsync(Guid merchantId, CancellationToken ct)
    {
        var qr = await db.MerchantStoreQrs.SingleOrDefaultAsync(x => x.MerchantId == merchantId && x.IsActive, ct);
        if (qr is null)
        {
            var raw = RandomNumberGenerator.GetBytes(32);
            qr = new MerchantStoreQr { Id = Guid.NewGuid(), MerchantId = merchantId, PublicQrId = $"MQR-{Guid.NewGuid():N}", TokenHash = Convert.ToHexString(SHA256.HashData(raw)), IsActive = true, CreatedAtUtc = DateTime.UtcNow };
            db.Add(qr);
            await db.SaveChangesAsync(ct);
        }
        return new(qr.PublicQrId, $"https://creatorpay.example/m/{qr.PublicQrId}", qr.IsActive);
    }

    public async Task<IReadOnlyList<MerchantDiscoveryDto>> SearchAsync(string? q, string? zone, CancellationToken ct)
    {
        var merchants = db.Merchants.Where(x => x.Status == MerchantStatus.Active);
        if (!string.IsNullOrWhiteSpace(q)) merchants = merchants.Where(x => x.TradingName.ToLower().Contains(q.Trim().ToLower()));
        var ids = await merchants.Select(x => x.Id).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(zone)) ids = ids.Intersect(await db.MerchantPromotionProfiles.Where(x => x.ZoneCode == zone).Select(x => x.MerchantId).ToListAsync(ct)).ToList();
        var result = new List<MerchantDiscoveryDto>();
        foreach (var id in ids) result.Add(await GetMerchantAsync(id, ct));
        return result;
    }

    public async Task<MerchantDiscoveryDto> GetMerchantAsync(Guid id, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var merchant = await db.Merchants.SingleOrDefaultAsync(x => x.Id == id && x.Status == MerchantStatus.Active, ct) ?? throw new KeyNotFoundException();
        var profile = await db.MerchantPromotionProfiles.SingleOrDefaultAsync(x => x.MerchantId == id, ct);
        var trialEligible = !await db.MerchantTrialCredits.AnyAsync(x => x.MerchantId == id && x.Status != TrialCreditStatus.Active, ct);
        var hasWalletFunding = await db.MerchantWallets.AnyAsync(x => x.MerchantId == id && x.AvailableBalance > 0, ct);
        var rows = await (from campaign in db.CreatorMerchantCampaigns
                          join creator in db.Creators on campaign.CreatorId equals creator.Id
                          join partnership in db.MerchantCreatorPartnerships on campaign.MerchantCreatorPartnershipId equals partnership.Id
                          where campaign.MerchantId == id && campaign.Status == CampaignStatus.Active && campaign.StartsAtUtc <= now && campaign.ExpiresAtUtc > now
                              && creator.Status == CreatorStatus.Active && partnership.Status == PartnershipStatus.Approved && (trialEligible || hasWalletFunding)
                          select new PromotionDto(campaign.Id, campaign.CampaignCode, merchant.TradingName, creator.DisplayName, profile == null ? null : profile.ZoneCode, profile != null && profile.FeaturedCreatorId == campaign.CreatorId, campaign.ExpiresAtUtc)).ToListAsync(ct);
        var ordered = rows.OrderByDescending(x => x.Featured).ThenBy(x => x.CreatorName).ToList();
        return new(merchant.Id, merchant.PublicMerchantId, merchant.TradingName, profile?.ZoneCode, profile?.FeaturedCreatorId, ordered.FirstOrDefault(x => x.Featured)?.CreatorName ?? "Platform Promotion", ordered);
    }

    public async Task SavePromotionAsync(Guid customer, Guid campaign, CancellationToken ct) { if (!await db.CreatorMerchantCampaigns.AnyAsync(x => x.Id == campaign && x.Status == CampaignStatus.Active, ct)) throw new KeyNotFoundException(); if (!await db.SavedPromotions.AnyAsync(x => x.CustomerId == customer && x.CampaignId == campaign, ct)) { db.Add(new SavedPromotion { Id = Guid.NewGuid(), CustomerId = customer, CampaignId = campaign, CreatedAtUtc = DateTime.UtcNow }); await db.SaveChangesAsync(ct); } }
    public Task RemoveSavedPromotionAsync(Guid customer, Guid campaign, CancellationToken ct) => db.SavedPromotions.Where(x => x.CustomerId == customer && x.CampaignId == campaign).ExecuteDeleteAsync(ct);
    public async Task<IReadOnlyList<PromotionDto>> GetSavedAsync(Guid customer, CancellationToken ct) => await (from s in db.SavedPromotions join c in db.CreatorMerchantCampaigns on s.CampaignId equals c.Id join m in db.Merchants on c.MerchantId equals m.Id join cr in db.Creators on c.CreatorId equals cr.Id join p in db.MerchantPromotionProfiles on m.Id equals p.MerchantId into pp from p in pp.DefaultIfEmpty() where s.CustomerId == customer select new PromotionDto(c.Id, c.CampaignCode, m.TradingName, cr.DisplayName, p.ZoneCode, p.FeaturedCreatorId == c.CreatorId, c.ExpiresAtUtc)).ToListAsync(ct);
    public async Task<MerchantDiscoveryDto> UpdateProfileAsync(Guid merchant, UpdateDiscoveryProfileRequest r, CancellationToken ct) { if (r.FeaturedCreatorId.HasValue && !await db.CreatorMerchantCampaigns.AnyAsync(x => x.CreatorId == r.FeaturedCreatorId && x.MerchantId == merchant && x.Status == CampaignStatus.Active, ct)) throw new ArgumentException("Featured creator must have an active campaign for this merchant."); var p = await db.MerchantPromotionProfiles.SingleOrDefaultAsync(x => x.MerchantId == merchant, ct); if (p == null) { p = new() { Id = Guid.NewGuid(), MerchantId = merchant, CreatedAtUtc = DateTime.UtcNow }; db.Add(p); } p.FeaturedCreatorId = r.FeaturedCreatorId; p.ZoneCode = r.ZoneCode?.Trim(); p.SocialLinksJson = r.SocialLinks == null ? null : JsonSerializer.Serialize(r.SocialLinks.Where(x => Uri.TryCreate(x.Value, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps).ToDictionary()); await db.SaveChangesAsync(ct); return await GetMerchantAsync(merchant, ct); }
}

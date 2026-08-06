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
    public async Task<PublicCreatorSearchDto> SearchCreatorsAsync(string? query, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var now = DateTime.UtcNow;
        var eligibleCreatorIds = EligibleCampaigns(now).Select(x => x.CreatorId).Distinct();
        var creators = db.Creators.AsNoTracking().Where(x => x.Status == CreatorStatus.Active && eligibleCreatorIds.Contains(x.Id));
        var term = query?.Trim();
        if (!string.IsNullOrWhiteSpace(term)) creators = creators.Where(x => EF.Functions.ILike(x.DisplayName, $"%{term}%"));
        var total = await creators.CountAsync(ct);
        var rows = await creators.OrderBy(x => x.DisplayName).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.PublicCreatorId, x.DisplayName }).ToListAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var counts = await EligibleCampaigns(now).Where(x => ids.Contains(x.CreatorId)).GroupBy(x => x.CreatorId)
            .Select(x => new { CreatorId = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.CreatorId, x => x.Count, ct);
        return new(rows.Select(x => new PublicCreatorCardDto(x.PublicCreatorId, x.DisplayName, null, counts[x.Id])).ToArray(), page, pageSize, total);
    }

    public async Task<PublicCreatorProfileDto> GetCreatorAsync(string publicCreatorId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var creator = await db.Creators.AsNoTracking().SingleOrDefaultAsync(x => x.PublicCreatorId == publicCreatorId && x.Status == CreatorStatus.Active, ct)
            ?? throw new KeyNotFoundException("Creator is unavailable.");
        var offerData = await (from campaign in EligibleCampaigns(now)
            where campaign.CreatorId == creator.Id
            join merchant in db.Merchants.AsNoTracking() on campaign.MerchantId equals merchant.Id
            orderby campaign.ExpiresAtUtc
            select new { campaign.Id, campaign.PublicCampaignId, campaign.CampaignCode, campaign.CreatorId, merchant.TradingName, merchant.PublicDescription, campaign.Conditions, ExpiresAtUtc = campaign.ExpiresAtUtc!.Value }).ToListAsync(ct);
        if (offerData.Count == 0) throw new KeyNotFoundException("Creator is unavailable.");
        var offers = offerData.Select(x => new OfferRow(x.Id, x.PublicCampaignId, x.CampaignCode, x.CreatorId, creator.PublicCreatorId, creator.DisplayName, x.TradingName, x.PublicDescription, x.Conditions, x.ExpiresAtUtc)).ToArray();
        var links = await db.CreatorSocialProfiles.AsNoTracking().Where(x => x.CreatorId == creator.Id && x.ProfileUrl != null)
            .Select(x => new { x.Platform, x.ProfileUrl }).ToListAsync(ct);
        return new(creator.PublicCreatorId, creator.DisplayName, null,
            links.Select(x => SafeSocial(x.Platform.ToString(), x.ProfileUrl)).Where(x => x is not null).Cast<PublicSocialLinkDto>().ToArray(),
            offers.Select(MapOffer).ToArray());
    }

    public async Task<PublicOfferDto> GetOfferAsync(string offerCode, CancellationToken ct)
    {
        var data = await (from campaign in EligibleCampaigns(DateTime.UtcNow)
            where campaign.CampaignCode == offerCode || campaign.PublicCampaignId == offerCode
            join creator in db.Creators.AsNoTracking() on campaign.CreatorId equals creator.Id
            join merchant in db.Merchants.AsNoTracking() on campaign.MerchantId equals merchant.Id
            select new { campaign.Id, campaign.PublicCampaignId, campaign.CampaignCode, campaign.CreatorId, creator.PublicCreatorId, creator.DisplayName, merchant.TradingName, merchant.PublicDescription, campaign.Conditions, ExpiresAtUtc = campaign.ExpiresAtUtc!.Value }).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("This Offer has ended or is unavailable.");
        return MapOffer(new(data.Id, data.PublicCampaignId, data.CampaignCode, data.CreatorId, data.PublicCreatorId, data.DisplayName, data.TradingName, data.PublicDescription, data.Conditions, data.ExpiresAtUtc));
    }

    public async Task<PublicOfferDto> ResolveOfferQrAsync(string publicQrId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var code = await (from qr in db.CampaignQrCodes.AsNoTracking()
                          join offer in EligibleCampaigns(now) on qr.CampaignId equals offer.Id
                          where qr.PublicQrId == publicQrId && qr.Status == CampaignQrStatus.Active && qr.ExpiresAtUtc > now
                          select offer.CampaignCode).SingleOrDefaultAsync(ct);
        if (code is null) throw new KeyNotFoundException("This Offer QR has ended or is unavailable.");
        return await GetOfferAsync(code, ct);
    }

    private IQueryable<CreatorMerchantCampaign> EligibleCampaigns(DateTime now) =>
        from campaign in db.CreatorMerchantCampaigns.AsNoTracking()
        join creator in db.Creators.AsNoTracking() on campaign.CreatorId equals creator.Id
        join merchant in db.Merchants.AsNoTracking() on campaign.MerchantId equals merchant.Id
        join partnership in db.MerchantCreatorPartnerships.AsNoTracking() on campaign.MerchantCreatorPartnershipId equals partnership.Id
        where creator.Status == CreatorStatus.Active && merchant.Status == MerchantStatus.Active
            && partnership.Status == PartnershipStatus.Approved
            && (!partnership.StartDateUtc.HasValue || partnership.StartDateUtc <= now)
            && (!partnership.EndDateUtc.HasValue || partnership.EndDateUtc > now)
            && campaign.Status == CampaignStatus.Active && campaign.StartsAtUtc <= now && campaign.ExpiresAtUtc > now
            && db.MerchantLocations.Any(location => location.MerchantId == merchant.Id && location.IsActive)
            && (!db.PartnershipLocations.Any(location => location.MerchantCreatorPartnershipId == partnership.Id && location.IsActive)
                || db.PartnershipLocations.Any(location => location.MerchantCreatorPartnershipId == partnership.Id && location.IsActive
                    && db.MerchantLocations.Any(merchantLocation => merchantLocation.Id == location.MerchantLocationId && merchantLocation.IsActive)))
        select campaign;

    private static PublicOfferDto MapOffer(OfferRow x) => new(x.CampaignCode,
        string.IsNullOrWhiteSpace(x.Conditions) ? $"{x.MerchantName} Offer" : x.Conditions.Split('\n', 2)[0],
        x.MerchantName, null, x.MerchantDescription ?? "Shop this Offer with the selected creator.", x.ExpiresAtUtc,
        x.CreatorPublicId, x.CreatorDisplayName, $"/offers/{Uri.EscapeDataString(x.CampaignCode)}");

    private static PublicSocialLinkDto? SafeSocial(string platform, string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(uri.Host)) return null;
        return new(platform, uri.AbsoluteUri);
    }

    private sealed record OfferRow(Guid Id, string PublicCampaignId, string CampaignCode, Guid CreatorId, string CreatorPublicId,
        string CreatorDisplayName, string MerchantName, string? MerchantDescription, string? Conditions, DateTime ExpiresAtUtc);

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

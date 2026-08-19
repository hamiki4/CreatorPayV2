using System.Security.Cryptography;
using System.Text.Json;
using CreatorPay.Application.Discovery;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using CreatorPay.Application.Checkout;
using CreatorPay.Application.Qr;
using CreatorPay.Infrastructure.Eligibility;
using CreatorPay.Infrastructure.Qr;

namespace CreatorPay.Infrastructure.Discovery;

public sealed class DiscoveryService(ApplicationDbContext db, IOptions<CheckoutOptions> checkoutOptions, IQrTokenService qrTokens,CreatorQrUrlBuilder qrUrls) : IDiscoveryService
{
    private readonly string currencyCode = checkoutOptions.Value.CurrencyCode;
    public async Task<IReadOnlyList<ShopperBusinessDto>> SearchShopperBusinessesAsync(string? query, string? category, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var promotedMerchantIds = EligibleCampaigns(now).Select(x => x.MerchantId).Distinct();
        var merchants = db.Merchants.AsNoTracking().Where(x => x.Status == MerchantStatus.Active
            && promotedMerchantIds.Contains(x.Id)
            && db.UserAccounts.Any(a => a.MerchantId == x.Id && a.Role == UserRole.MerchantAdmin && a.Status == AccountStatus.Active));
        var term = query?.Trim();
        if (!string.IsNullOrWhiteSpace(term)) merchants = merchants.Where(x => EF.Functions.ILike(x.TradingName, $"%{term}%") || EF.Functions.ILike(x.LegalBusinessName, $"%{term}%") || EF.Functions.ILike(x.City, $"%{term}%") || EF.Functions.ILike(x.PublicMerchantId, $"%{term}%"));
        var selectedCategory = category?.Trim();
        if (!string.IsNullOrWhiteSpace(selectedCategory)) merchants = merchants.Where(x => x.Category != null && EF.Functions.ILike(x.Category, selectedCategory));
        var rows = await merchants.OrderBy(x => x.TradingName).Take(50).Select(x => new { x.Id, x.PublicMerchantId, x.TradingName, x.Category, x.City }).ToListAsync(ct);
        var funded = await FundedMerchantIds(rows.Select(x => x.Id).ToArray(), ct);
        return rows.Select(x => new ShopperBusinessDto(x.Id, x.PublicMerchantId, x.TradingName, x.Category, x.City, funded.Contains(x.Id))).ToArray();
    }

    public async Task<ShopperBusinessDetailDto> GetShopperBusinessAsync(Guid merchantId, CancellationToken ct)
    {
        var merchant = await db.Merchants.AsNoTracking().Where(x => x.Id == merchantId && x.Status == MerchantStatus.Active
            && db.UserAccounts.Any(a => a.MerchantId == x.Id && a.Role == UserRole.MerchantAdmin && a.Status == AccountStatus.Active))
            .Select(x => new { x.Id, x.PublicMerchantId, x.TradingName, x.Category, x.City }).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Business is unavailable.");
        var rewardsAvailable = (await FundedMerchantIds([merchantId], ct)).Contains(merchantId);
        var now = DateTime.UtcNow;
        var rows = await (from campaign in EligibleCampaigns(now)
            where campaign.MerchantId == merchantId
            join partnership in db.MerchantCreatorPartnerships.AsNoTracking() on campaign.MerchantCreatorPartnershipId equals partnership.Id
            join creator in db.Creators.AsNoTracking() on campaign.CreatorId equals creator.Id
            select new { campaign.Id, campaign.CreatorId, creator.PublicCreatorId, creator.DisplayName, partnership.EndDateUtc, campaign.ExpiresAtUtc }).ToListAsync(ct);
        var creatorIds = rows.Select(x => x.CreatorId).Distinct().ToArray();
        var socials = await db.CreatorSocialProfiles.AsNoTracking().Where(x => creatorIds.Contains(x.CreatorId))
            .OrderByDescending(x => x.IsPrimary).ThenByDescending(x => x.VerificationStatus).ThenByDescending(x => x.FollowerCount)
            .Select(x => new { x.CreatorId, Platform = x.Platform.ToString(), x.FollowerCount }).ToListAsync(ct);
        var creators = rows.GroupBy(x => x.CreatorId).Select(group => group.OrderBy(x => x.ExpiresAtUtc).First()).Select(x =>
        {
            var social = socials.FirstOrDefault(s => s.CreatorId == x.CreatorId);
            var end = x.EndDateUtc ?? x.ExpiresAtUtc ?? now;
            return new ShopperAdvertisingCreatorDto(x.CreatorId, x.PublicCreatorId, x.DisplayName, social?.Platform, social?.FollowerCount, Math.Max(1, (int)Math.Ceiling((end - now).TotalDays)), x.Id);
        }).OrderBy(x => x.DisplayName).ToArray();
        return new(merchant.Id, merchant.PublicMerchantId, merchant.TradingName, merchant.Category, merchant.City, true, creators);
    }

    private async Task<HashSet<Guid>> FundedMerchantIds(Guid[] ids, CancellationToken ct)
    {
        return await RewardEligibilityQueries.FundedMerchantIdsAsync(db, ids, currencyCode, ct);
    }

    public async Task<IReadOnlyList<ShopperAdvertisingRowDto>> SearchShopperAdvertisingAsync(string? query, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var relationships = from campaign in EligibleCampaigns(now)
            join relationship in db.MerchantCreatorPartnerships.AsNoTracking() on campaign.MerchantCreatorPartnershipId equals relationship.Id
            join merchant in db.Merchants.AsNoTracking() on relationship.MerchantId equals merchant.Id
            join creator in db.Creators.AsNoTracking() on relationship.CreatorId equals creator.Id
            select new { RelationshipId = relationship.Id, MerchantId = merchant.Id, merchant.PublicMerchantId, merchant.TradingName, merchant.City, CreatorId = creator.Id, creator.PublicCreatorId, creator.CreatorCode, creator.DisplayName, relationship.EndDateUtc, campaign.ExpiresAtUtc };
        var term = query?.Trim();
        if (!string.IsNullOrWhiteSpace(term)) relationships = relationships.Where(x => EF.Functions.ILike(x.TradingName, $"%{term}%") || EF.Functions.ILike(x.City, $"%{term}%") || EF.Functions.ILike(x.DisplayName, $"%{term}%") || EF.Functions.ILike(x.PublicMerchantId, $"%{term}%") || EF.Functions.ILike(x.PublicCreatorId, $"%{term}%"));
        var campaignRows = await relationships.OrderBy(x => x.TradingName).ThenBy(x => x.DisplayName).Take(200).ToListAsync(ct);
        var rows = campaignRows.GroupBy(x => x.RelationshipId).Select(x => x.OrderByDescending(y => y.ExpiresAtUtc).First()).Take(100).ToList();
        var funded = await FundedMerchantIds(rows.Select(x => x.MerchantId).Distinct().ToArray(), ct);
        return rows.Select(x => new ShopperAdvertisingRowDto(x.RelationshipId, x.MerchantId, x.PublicMerchantId, x.TradingName, x.City, x.CreatorId, x.PublicCreatorId, x.CreatorCode, x.DisplayName, "Active", Math.Max(1, (int)Math.Ceiling((new[] { x.EndDateUtc, x.ExpiresAtUtc }.Where(v => v.HasValue).Min()!.Value - now).TotalDays)), funded.Contains(x.MerchantId))).ToArray();
    }

    public async Task<ShopperCreatorQrDto> GetShopperCreatorQrAsync(Guid relationshipId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var row = await (from relationship in RewardEligibilityQueries.EligibleRelationships(db, now)
            join merchant in db.Merchants.AsNoTracking() on relationship.MerchantId equals merchant.Id
            join creator in db.Creators.AsNoTracking() on relationship.CreatorId equals creator.Id
            join qr in db.CreatorQrCodes.AsNoTracking() on creator.Id equals qr.CreatorId
            where relationship.Id == relationshipId && qr.IsActive && !qr.RevokedAtUtc.HasValue
            select new { relationship.Id, MerchantId = merchant.Id, merchant.TradingName, creator.DisplayName, qr.PublicQrId, qr.Version }).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Advertising relationship is unavailable.");
        if (!(await FundedMerchantIds([row.MerchantId], ct)).Contains(row.MerchantId)) throw new KeyNotFoundException("Weymela rewards are temporarily unavailable at this Business.");
        var token = qrTokens.CreateToken(row.PublicQrId, row.Version);
        return new(row.Id, row.TradingName, row.DisplayName, qrUrls.Create(row.PublicQrId,row.Version,token));
    }
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
        join partnership in RewardEligibilityQueries.EligibleRelationships(db, now) on campaign.MerchantCreatorPartnershipId equals partnership.Id
        where campaign.CreatorId == partnership.CreatorId && campaign.MerchantId == partnership.MerchantId
            && campaign.Status == CampaignStatus.Active && campaign.StartsAtUtc <= now && campaign.ExpiresAtUtc > now
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

using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPay.Infrastructure.Eligibility;

public static class RewardEligibilityQueries
{
    public static IQueryable<MerchantCreatorPartnership> EligibleRelationships(ApplicationDbContext db, DateTime now) =>
        db.MerchantCreatorPartnerships.AsNoTracking().Where(relationship =>
            relationship.Status == PartnershipStatus.Approved &&
            relationship.StartDateUtc.HasValue && relationship.StartDateUtc <= now &&
            relationship.EndDateUtc.HasValue && relationship.EndDateUtc > now &&
            db.Merchants.Any(merchant => merchant.Id == relationship.MerchantId && (merchant.Status == MerchantStatus.Active || merchant.Status == MerchantStatus.LowBalance)) &&
            db.Creators.Any(creator => creator.Id == relationship.CreatorId && creator.Status == CreatorStatus.Active) &&
            db.UserAccounts.Any(account => account.MerchantId == relationship.MerchantId && account.Role == UserRole.MerchantAdmin && account.Status == AccountStatus.Active) &&
            db.UserAccounts.Any(account => account.CreatorId == relationship.CreatorId && account.Role == UserRole.Creator && account.Status == AccountStatus.Active) &&
            db.MerchantLocations.Any(location => location.MerchantId == relationship.MerchantId && location.IsActive) &&
            (!db.PartnershipLocations.Any(location => location.MerchantCreatorPartnershipId == relationship.Id && location.IsActive) ||
             db.PartnershipLocations.Any(location => location.MerchantCreatorPartnershipId == relationship.Id && location.IsActive &&
                 db.MerchantLocations.Any(merchantLocation => merchantLocation.Id == location.MerchantLocationId && merchantLocation.IsActive))));

    public static async Task<HashSet<Guid>> FundedMerchantIdsAsync(ApplicationDbContext db, IEnumerable<Guid> merchantIds, string currencyCode, CancellationToken ct)
    {
        var ids = merchantIds.Distinct().ToArray();
        if (ids.Length == 0) return [];
        return (await db.MerchantWallets.AsNoTracking()
            .Where(wallet => ids.Contains(wallet.MerchantId) && wallet.CurrencyCode == currencyCode && wallet.AvailableBalance > 0)
            .Select(wallet => wallet.MerchantId).ToListAsync(ct)).ToHashSet();
    }

    public static async Task<bool> HasRequiredFundingAsync(ApplicationDbContext db, Guid merchantId, string currencyCode, decimal transactionAmount, CancellationToken ct)
    {
        return await db.MerchantWallets.AsNoTracking().AnyAsync(wallet => wallet.MerchantId == merchantId && wallet.CurrencyCode == currencyCode &&
            wallet.AvailableBalance >= transactionAmount && (transactionAmount > 0 || wallet.AvailableBalance > 0), ct);
    }

    public static async Task<decimal> CurrentMinimumAsync(ApplicationDbContext db, string currencyCode, CancellationToken ct) =>
        await db.PlatformFinancialSettings.AsNoTracking().Where(setting => setting.CurrencyCode == currencyCode)
            .Select(setting => (decimal?)setting.MinimumBusinessWalletBalance).SingleOrDefaultAsync(ct) ?? 0m;
}

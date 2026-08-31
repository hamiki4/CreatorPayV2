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
            db.Merchants.Any(merchant => merchant.Id == relationship.MerchantId && (merchant.Status == MerchantStatus.Active || merchant.Status == MerchantStatus.LowBalance || merchant.Status == MerchantStatus.ApprovedUnfunded || merchant.Status == MerchantStatus.LowBalanceRestricted || merchant.Status == MerchantStatus.FundingRestricted)) &&
            db.Creators.Any(creator => creator.Id == relationship.CreatorId && creator.Status == CreatorStatus.Active) &&
            db.UserAccounts.Any(account => account.MerchantId == relationship.MerchantId && account.Role == UserRole.MerchantAdmin && account.Status == AccountStatus.Active && (account.LockoutEndUtc == null || account.LockoutEndUtc <= now)) &&
            db.UserAccounts.Any(account => account.CreatorId == relationship.CreatorId && account.Role == UserRole.Creator && account.Status == AccountStatus.Active && (account.LockoutEndUtc == null || account.LockoutEndUtc <= now)) &&
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

    public static Task<decimal> CurrentMinimumAsync(ApplicationDbContext db, string currencyCode, CancellationToken ct) =>
        BusinessWalletMinimumQueries.CurrentMinimumAsync(db, currencyCode, "Other", ct);

    public static Task<decimal> CurrentMinimumAsync(ApplicationDbContext db, string currencyCode, string? businessType, CancellationToken ct) =>
        BusinessWalletMinimumQueries.CurrentMinimumAsync(db, currencyCode, businessType, ct);
}

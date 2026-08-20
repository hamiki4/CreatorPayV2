using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPay.Infrastructure.Eligibility;

public static class BusinessAdvertisingEligibility
{
    public static async Task<(bool Eligible, decimal Available, decimal Minimum)> EvaluateAsync(ApplicationDbContext db, Guid merchantId, string currencyCode, CancellationToken ct)
    {
        var available = await db.MerchantWallets.AsNoTracking().Where(x => x.MerchantId == merchantId && x.CurrencyCode == currencyCode).Select(x => (decimal?)x.AvailableBalance).SingleOrDefaultAsync(ct) ?? 0m;
        var minimum = await RewardEligibilityQueries.CurrentMinimumAsync(db, currencyCode, ct);
        var active = await db.Merchants.AsNoTracking().AnyAsync(x => x.Id == merchantId && x.Status == MerchantStatus.Active, ct);
        return (active && available >= minimum, available, minimum);
    }

    public static async Task<HashSet<Guid>> EligibleMerchantIdsAsync(ApplicationDbContext db, IEnumerable<Guid> merchantIds, string currencyCode, CancellationToken ct)
    {
        var ids = merchantIds.Distinct().ToArray();
        if (ids.Length == 0) return [];

        var minimum = await RewardEligibilityQueries.CurrentMinimumAsync(db, currencyCode, ct);
        var activeMerchantIds = await db.Merchants.AsNoTracking()
            .Where(x => ids.Contains(x.Id) && x.Status == MerchantStatus.Active)
            .Select(x => x.Id)
            .ToListAsync(ct);
        if (activeMerchantIds.Count == 0) return [];

        var walletBalances = await db.MerchantWallets.AsNoTracking()
            .Where(x => activeMerchantIds.Contains(x.MerchantId) && x.CurrencyCode == currencyCode)
            .Select(x => new { x.MerchantId, x.AvailableBalance })
            .ToListAsync(ct);
        var byMerchant = walletBalances.ToDictionary(x => x.MerchantId, x => x.AvailableBalance);

        return activeMerchantIds.Where(id => (byMerchant.TryGetValue(id, out var available) ? available : 0m) >= minimum).ToHashSet();
    }

    public static async Task EnsureEligibleAsync(ApplicationDbContext db, Guid merchantId, string currencyCode, CancellationToken ct)
    {
        var result = await EvaluateAsync(db, merchantId, currencyCode, ct);
        if (!result.Eligible) throw new InvalidOperationException($"Advertising is restricted until the available wallet balance reaches {result.Minimum:0.00} {currencyCode}.");
    }
}

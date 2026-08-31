using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPay.Infrastructure.Eligibility;

public static class BusinessAdvertisingEligibility
{
    public static async Task<(bool Eligible, decimal Available, decimal Minimum)> EvaluateAsync(ApplicationDbContext db, Guid merchantId, string currencyCode, CancellationToken ct)
    {
        var available = await db.MerchantWallets.AsNoTracking().Where(x => x.MerchantId == merchantId && x.CurrencyCode == currencyCode).Select(x => (decimal?)x.AvailableBalance).SingleOrDefaultAsync(ct) ?? 0m;
        var minimum = await BusinessWalletMinimumQueries.CurrentMinimumAsync(db, currencyCode, merchantId, ct);
        var now = DateTime.UtcNow;
        var active = await db.Merchants.AsNoTracking().AnyAsync(x =>
            x.Id == merchantId &&
            (x.Status == MerchantStatus.Active ||
             x.Status == MerchantStatus.LowBalance ||
             x.Status == MerchantStatus.ApprovedUnfunded ||
             x.Status == MerchantStatus.LowBalanceRestricted ||
             x.Status == MerchantStatus.FundingRestricted) &&
            db.UserAccounts.Any(a => a.MerchantId == x.Id && a.Role == UserRole.MerchantAdmin && a.Status == AccountStatus.Active && (a.LockoutEndUtc == null || a.LockoutEndUtc <= now)),
            ct);
        return (active && available >= minimum, available, minimum);
    }

    public static async Task<HashSet<Guid>> EligibleMerchantIdsAsync(ApplicationDbContext db, IEnumerable<Guid> merchantIds, string currencyCode, CancellationToken ct)
    {
        var ids = merchantIds.Distinct().ToArray();
        if (ids.Length == 0) return [];

        var now = DateTime.UtcNow;
        var activeMerchantIds = await db.Merchants.AsNoTracking()
            .Where(x => ids.Contains(x.Id)
                && (x.Status == MerchantStatus.Active ||
                    x.Status == MerchantStatus.LowBalance ||
                    x.Status == MerchantStatus.ApprovedUnfunded ||
                    x.Status == MerchantStatus.LowBalanceRestricted ||
                    x.Status == MerchantStatus.FundingRestricted)
                && db.UserAccounts.Any(a => a.MerchantId == x.Id && a.Role == UserRole.MerchantAdmin && a.Status == AccountStatus.Active && (a.LockoutEndUtc == null || a.LockoutEndUtc <= now)))
            .Select(x => new { x.Id, x.BusinessType })
            .ToListAsync(ct);
        if (activeMerchantIds.Count == 0) return [];

        var activeIds = activeMerchantIds.Select(x => x.Id).ToArray();
        var minimums = await BusinessWalletMinimumQueries.CurrentMinimumsAsync(db, currencyCode, ct);
        var walletBalances = await db.MerchantWallets.AsNoTracking()
            .Where(x => activeIds.Contains(x.MerchantId) && x.CurrencyCode == currencyCode)
            .Select(x => new { x.MerchantId, x.AvailableBalance })
            .ToListAsync(ct);
        var byMerchant = walletBalances.ToDictionary(x => x.MerchantId, x => x.AvailableBalance);
        return activeMerchantIds.Where(row =>
        {
            var minimum = minimums.TryGetValue(BusinessWalletMinimumQueries.NormalizeBusinessType(row.BusinessType), out var value) ? value : minimums["Other"];
            return (byMerchant.TryGetValue(row.Id, out var available) ? available : 0m) >= minimum;
        }).Select(row => row.Id).ToHashSet();
    }

    public static async Task EnsureEligibleAsync(ApplicationDbContext db, Guid merchantId, string currencyCode, CancellationToken ct)
    {
        var result = await EvaluateAsync(db, merchantId, currencyCode, ct);
        if (!result.Eligible) throw new InvalidOperationException($"Advertising is restricted until the available wallet balance reaches {result.Minimum:0.00} {currencyCode}.");
    }
}

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

    public static async Task EnsureEligibleAsync(ApplicationDbContext db, Guid merchantId, string currencyCode, CancellationToken ct)
    {
        var result = await EvaluateAsync(db, merchantId, currencyCode, ct);
        if (!result.Eligible) throw new InvalidOperationException($"Advertising is restricted until the available wallet balance reaches {result.Minimum:0.00} {currencyCode}.");
    }
}

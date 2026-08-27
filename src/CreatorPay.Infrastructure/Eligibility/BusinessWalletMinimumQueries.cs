using CreatorPay.Application.Merchants;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPay.Infrastructure.Eligibility;

public static class BusinessWalletMinimumQueries
{
    public static string NormalizeBusinessType(string? value)
    {
        var trimmed = value?.Trim();
        return !string.IsNullOrWhiteSpace(trimmed) && BusinessTypes.IsSupported(trimmed) ? trimmed : "Other";
    }

    public static async Task<IReadOnlyDictionary<string, decimal>> CurrentMinimumsAsync(ApplicationDbContext db, string currencyCode, CancellationToken ct)
    {
        var rows = await db.BusinessTypeWalletMinimumVersions.AsNoTracking()
            .Where(x => x.CurrencyCode == currencyCode)
            .OrderByDescending(x => x.EffectiveFromUtc)
            .ThenByDescending(x => x.VersionNumber)
            .ToListAsync(ct);

        var minimums = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (!minimums.ContainsKey(row.BusinessType))
            {
                minimums[row.BusinessType] = row.MinimumBusinessWalletBalance;
            }
        }

        foreach (var businessType in BusinessTypes.Values)
        {
            minimums.TryAdd(businessType, 0m);
        }

        if (!minimums.ContainsKey("Other"))
        {
            minimums["Other"] = 0m;
        }

        return minimums;
    }

    public static async Task<decimal> CurrentMinimumAsync(ApplicationDbContext db, string currencyCode, string? businessType, CancellationToken ct)
    {
        var minimums = await CurrentMinimumsAsync(db, currencyCode, ct);
        var normalized = NormalizeBusinessType(businessType);
        return minimums.TryGetValue(normalized, out var minimum) ? minimum : minimums["Other"];
    }

    public static async Task<decimal> CurrentMinimumAsync(ApplicationDbContext db, string currencyCode, Guid merchantId, CancellationToken ct)
    {
        var businessType = await db.Merchants.AsNoTracking().Where(x => x.Id == merchantId).Select(x => x.BusinessType).SingleAsync(ct);
        return await CurrentMinimumAsync(db, currencyCode, businessType, ct);
    }
}

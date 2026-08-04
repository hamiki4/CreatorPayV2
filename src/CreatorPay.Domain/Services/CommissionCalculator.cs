using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Domain.Services;

public sealed record CommissionCalculation(decimal PurchaseAmount, decimal MerchantCommissionRatePercent, decimal TotalCommissionAmount, decimal CreatorSharePercent, decimal CreatorCommissionAmount, decimal PlatformSharePercent, decimal PlatformCommissionAmount, CommissionRoundingMode RoundingMode) { public decimal CustomerCashbackSharePercent { get; init; } public decimal CustomerCashbackAmount { get; init; } }
public static class CommissionCalculator
{
    public static CommissionCalculation Calculate(decimal purchaseAmount, CommissionRuleVersion version)
    {
        version.Validate();
        if (purchaseAmount < version.MinimumPurchaseAmount || (version.MaximumPurchaseAmount is { } max && purchaseAmount > max)) throw new ArgumentOutOfRangeException(nameof(purchaseAmount), "Purchase amount is outside the rule bounds.");
        var total = Round(purchaseAmount * version.MerchantCommissionRatePercent / 100m, version.RoundingMode);
        var creator = Round(total * version.CreatorSharePercent / 100m, version.RoundingMode);
        var cashback = Round(total * version.CustomerCashbackSharePercent / 100m, version.RoundingMode);
        return new(purchaseAmount, version.MerchantCommissionRatePercent, total, version.CreatorSharePercent, creator, version.PlatformSharePercent, total - creator - cashback, version.RoundingMode) { CustomerCashbackSharePercent = version.CustomerCashbackSharePercent, CustomerCashbackAmount = cashback };
    }
    private static decimal Round(decimal value, CommissionRoundingMode mode) => mode switch
    {
        CommissionRoundingMode.AwayFromZero => Math.Round(value, 2, MidpointRounding.AwayFromZero),
        CommissionRoundingMode.ToEven => Math.Round(value, 2, MidpointRounding.ToEven),
        CommissionRoundingMode.Down => Math.Floor(value * 100m) / 100m,
        CommissionRoundingMode.Up => Math.Ceiling(value * 100m) / 100m,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };
}

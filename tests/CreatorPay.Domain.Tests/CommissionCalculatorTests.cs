using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Domain.Services;

namespace CreatorPay.Domain.Tests;

public sealed class CommissionCalculatorTests
{
    static CommissionRuleVersion Rule(decimal rate = 10, decimal creator = 70, decimal platform = 30, CommissionRoundingMode rounding = CommissionRoundingMode.AwayFromZero) => new() { MerchantCommissionRatePercent = rate, CreatorSharePercent = creator, PlatformSharePercent = platform, MinimumPurchaseAmount = 0, EffectiveFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), RoundingMode = rounding };
    [Fact] public void Calculates_etb_and_conserves_total() { var x = CommissionCalculator.Calculate(500, Rule()); Assert.Equal(50, x.TotalCommissionAmount); Assert.Equal(35, x.CreatorCommissionAmount); Assert.Equal(15, x.PlatformCommissionAmount); Assert.Equal(x.TotalCommissionAmount, x.CreatorCommissionAmount + x.PlatformCommissionAmount); }
    [Fact] public void Allows_zero_commission() { var x = CommissionCalculator.Calculate(500, Rule(0)); Assert.Equal(0, x.TotalCommissionAmount); }
    [Theory][InlineData(-1)][InlineData(100.01)] public void Rejects_invalid_rate(decimal rate) => Assert.Throws<ArgumentOutOfRangeException>(() => CommissionCalculator.Calculate(10, Rule(rate)));
    [Fact] public void Rejects_invalid_split() => Assert.Throws<ArgumentException>(() => CommissionCalculator.Calculate(10, Rule(10, 60, 30)));
    [Fact] public void Rejects_negative_minimum() { var r = Rule(); r.MinimumPurchaseAmount = -1; Assert.Throws<ArgumentException>(r.Validate); }
    [Fact] public void Rejects_maximum_below_minimum() { var r = Rule(); r.MinimumPurchaseAmount = 10; r.MaximumPurchaseAmount = 9; Assert.Throws<ArgumentException>(r.Validate); }
    [Fact] public void Rejects_invalid_dates() { var r = Rule(); r.EffectiveToUtc = r.EffectiveFromUtc; Assert.Throws<ArgumentException>(r.Validate); }
    [Fact] public void Rounds_away_from_zero() { var x = CommissionCalculator.Calculate(1, Rule(0.5m, 50, 50)); Assert.Equal(.01m, x.TotalCommissionAmount); Assert.Equal(.01m, x.CreatorCommissionAmount); Assert.Equal(0, x.PlatformCommissionAmount); }
    [Fact] public void Rounds_to_even() { var x = CommissionCalculator.Calculate(1, Rule(0.5m, 50, 50, CommissionRoundingMode.ToEven)); Assert.Equal(0, x.TotalCommissionAmount); }
    [Fact] public void Enforces_purchase_bounds() { var r = Rule(); r.MinimumPurchaseAmount = 10; r.MaximumPurchaseAmount = 20; Assert.Throws<ArgumentOutOfRangeException>(() => CommissionCalculator.Calculate(9, r)); Assert.Throws<ArgumentOutOfRangeException>(() => CommissionCalculator.Calculate(21, r)); }
}

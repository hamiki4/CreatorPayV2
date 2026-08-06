using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Checkout;

namespace CreatorPay.Infrastructure.Tests;

public sealed class OfferReuseWindowTests
{
    private static readonly TimeZoneInfo Addis = TimeZoneInfo.FindSystemTimeZoneById("Africa/Addis_Ababa");

    [Fact] public void Day_uses_business_location_midnight() => Assert.Equal(new DateTime(2026, 8, 5, 21, 0, 0, DateTimeKind.Utc), OfferReuseWindow.StartUtc(OfferReuseRule.OncePerDay, new DateTime(2026, 8, 6, 10, 0, 0, DateTimeKind.Utc), Addis));
    [Fact] public void Week_starts_on_business_local_Monday() => Assert.Equal(new DateTime(2026, 8, 2, 21, 0, 0, DateTimeKind.Utc), OfferReuseWindow.StartUtc(OfferReuseRule.OncePerWeek, new DateTime(2026, 8, 6, 10, 0, 0, DateTimeKind.Utc), Addis));
    [Fact] public void Month_starts_on_business_local_first_day() => Assert.Equal(new DateTime(2026, 7, 31, 21, 0, 0, DateTimeKind.Utc), OfferReuseWindow.StartUtc(OfferReuseRule.OncePerMonth, new DateTime(2026, 8, 6, 10, 0, 0, DateTimeKind.Utc), Addis));
    [Theory]
    [InlineData(OfferReuseRule.OncePerOffer)]
    [InlineData(OfferReuseRule.Unlimited)]
    public void Lifetime_and_unlimited_have_no_window_start(OfferReuseRule rule) => Assert.Null(OfferReuseWindow.StartUtc(rule, DateTime.UtcNow, Addis));

    [Fact]
    public void Day_blocks_same_local_day_and_allows_next_day()
    {
        var previous = new DateTime(2026, 8, 5, 20, 30, 0, DateTimeKind.Utc);
        Assert.True(OfferReuseWindow.Blocks(OfferReuseRule.OncePerDay, previous, new DateTime(2026, 8, 5, 20, 45, 0, DateTimeKind.Utc), Addis));
        Assert.False(OfferReuseWindow.Blocks(OfferReuseRule.OncePerDay, previous, new DateTime(2026, 8, 5, 21, 15, 0, DateTimeKind.Utc), Addis));
    }

    [Theory]
    [InlineData(OfferReuseRule.OncePerWeek, true)]
    [InlineData(OfferReuseRule.OncePerMonth, true)]
    [InlineData(OfferReuseRule.OncePerOffer, true)]
    [InlineData(OfferReuseRule.Unlimited, false)]
    public void Rule_blocks_prior_confirmed_use_as_expected(OfferReuseRule rule, bool expected)
    {
        var now = new DateTime(2026, 8, 6, 10, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expected, OfferReuseWindow.Blocks(rule, now.AddHours(-1), now, Addis));
    }
}

using CreatorPay.Domain.Enums;

namespace CreatorPay.Infrastructure.Checkout;

public static class OfferReuseWindow
{
    public static bool Blocks(OfferReuseRule rule, DateTime previousTransactionUtc, DateTime utcNow, TimeZoneInfo zone)
    {
        if (rule == OfferReuseRule.Unlimited) return false;
        var startUtc = StartUtc(rule, utcNow, zone);
        return !startUtc.HasValue || previousTransactionUtc >= startUtc.Value;
    }

    public static DateTime? StartUtc(OfferReuseRule rule, DateTime utcNow, TimeZoneInfo zone)
    {
        if (rule is OfferReuseRule.OncePerOffer or OfferReuseRule.Unlimited) return null;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone);
        var localStart = rule switch
        {
            OfferReuseRule.OncePerDay => localNow.Date,
            OfferReuseRule.OncePerWeek => localNow.Date.AddDays(-((7 + (int)localNow.DayOfWeek - (int)DayOfWeek.Monday) % 7)),
            OfferReuseRule.OncePerMonth => new DateTime(localNow.Year, localNow.Month, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(rule))
        };
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localStart, DateTimeKind.Unspecified), zone);
    }
}

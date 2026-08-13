namespace CreatorPay.Application.Earnings;

public sealed record PayoutScheduleDto(DayOfWeek CreatorCutoffDay, TimeSpan CreatorCutoffTime, DayOfWeek CreatorPayoutDay, int ShopperCutoffDay, TimeSpan ShopperCutoffTime, int ShopperPayoutDay, DateTime EffectiveFromUtc);

public static class PayoutSchedule
{
    public static DateTime NextWeekly(DateTime now, DayOfWeek day, TimeSpan time)
    { var days=((int)day-(int)now.DayOfWeek+7)%7;var value=now.Date.AddDays(days).Add(time);return value<=now?value.AddDays(7):value; }
    public static DateTime PreviousWeeklyCutoff(DateTime now, DayOfWeek day, TimeSpan time)
    { var days=((int)now.DayOfWeek-(int)day+7)%7;var value=now.Date.AddDays(-days).Add(time);return value>now?value.AddDays(-7):value; }
    public static DateTime NextMonthly(DateTime now,int day,TimeSpan time)
    { for(var add=0;add<14;add++){var month=now.Date.AddMonths(add);var actual=day<=0?DateTime.DaysInMonth(month.Year,month.Month):Math.Min(day,DateTime.DaysInMonth(month.Year,month.Month));var value=new DateTime(month.Year,month.Month,actual,0,0,0,DateTimeKind.Utc).Add(time);if(value>now)return value;}throw new InvalidOperationException("Unable to calculate payout date."); }
    public static DateTime PreviousMonthlyCutoff(DateTime now,int day,TimeSpan time)
    { for(var subtract=0;subtract<14;subtract++){var month=now.Date.AddMonths(-subtract);var actual=day<=0?DateTime.DaysInMonth(month.Year,month.Month):Math.Min(day,DateTime.DaysInMonth(month.Year,month.Month));var value=new DateTime(month.Year,month.Month,actual,0,0,0,DateTimeKind.Utc).Add(time);if(value<=now)return value;}throw new InvalidOperationException("Unable to calculate monthly cutoff."); }
}

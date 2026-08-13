using CreatorPay.Application.Earnings;

namespace CreatorPay.Application.Tests;

public sealed class PayoutScheduleTests
{
    [Fact] public void Configured_weekly_and_monthly_dates_are_actual_upcoming_dates(){var now=new DateTime(2026,8,12,10,0,0,DateTimeKind.Utc);Assert.Equal(new DateTime(2026,8,15,0,0,0,DateTimeKind.Utc),PayoutSchedule.NextWeekly(now,DayOfWeek.Saturday,TimeSpan.Zero));Assert.Equal(new DateTime(2026,8,16,0,0,0,DateTimeKind.Utc),PayoutSchedule.NextMonthly(now,16,TimeSpan.Zero));Assert.Equal(new DateTime(2026,8,7,0,0,0,DateTimeKind.Utc),PayoutSchedule.PreviousWeeklyCutoff(now,DayOfWeek.Friday,TimeSpan.Zero));Assert.Equal(new DateTime(2026,7,31,23,30,0,DateTimeKind.Utc),PayoutSchedule.PreviousMonthlyCutoff(now,0,new TimeSpan(23,30,0)));}

    [Fact] public void Monthly_dates_clamp_to_short_months_and_advance_after_payout_time(){var before=new DateTime(2026,2,27,10,0,0,DateTimeKind.Utc);Assert.Equal(new DateTime(2026,2,28,12,0,0,DateTimeKind.Utc),PayoutSchedule.NextMonthly(before,31,new TimeSpan(12,0,0)));var after=new DateTime(2026,2,28,12,0,0,DateTimeKind.Utc);Assert.Equal(new DateTime(2026,3,31,12,0,0,DateTimeKind.Utc),PayoutSchedule.NextMonthly(after,31,new TimeSpan(12,0,0)));}
}

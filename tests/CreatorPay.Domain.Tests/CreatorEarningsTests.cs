using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Domain.Tests;

public sealed class CreatorEarningsTests
{
    [Fact] public void Balance_transitions_preserve_categories() { var now = DateTime.UtcNow; var a = new CreatorBalanceAccount(); a.CreditPending(250, now); a.Transfer(CreatorBalanceCategory.Pending, CreatorBalanceCategory.Available, 250, now); a.Transfer(CreatorBalanceCategory.Available, CreatorBalanceCategory.Scheduled, 250, now); a.Transfer(CreatorBalanceCategory.Scheduled, CreatorBalanceCategory.Paid, 250, now); Assert.Equal(0, a.PendingBalance); Assert.Equal(0, a.AvailableBalance); Assert.Equal(0, a.ScheduledBalance); Assert.Equal(250, a.PaidLifetimeTotal); }
    [Fact] public void Balance_cannot_become_negative() { var a = new CreatorBalanceAccount(); Assert.Throws<InvalidOperationException>(() => a.Transfer(CreatorBalanceCategory.Pending, CreatorBalanceCategory.Available, 1, DateTime.UtcNow)); }
    [Fact] public void Earning_matures_only_at_available_time() { var at = DateTime.UtcNow; var e = new CreatorEarning { AvailableAtUtc = at }; Assert.Throws<InvalidOperationException>(() => e.MakeAvailable(at.AddTicks(-1))); e.MakeAvailable(at); Assert.Equal(CreatorEarningStatus.Available, e.Status); }
    [Fact] public void Scheduled_earning_cannot_be_paid_twice() { var at = DateTime.UtcNow; var e = new CreatorEarning { AvailableAtUtc = at }; e.MakeAvailable(at); e.Schedule(Guid.NewGuid(), at); e.MarkPaid(at); Assert.Throws<InvalidOperationException>(() => e.MarkPaid(at)); }
}

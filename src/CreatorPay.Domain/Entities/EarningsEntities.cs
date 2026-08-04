using CreatorPay.Domain.Common;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Domain.Entities;

public sealed class CreatorEarning : Entity
{
    public Guid CreatorId { get; set; }
    public Guid PurchaseTransactionId { get; set; }
    public Guid CommissionCalculationSnapshotId { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "ETB"; public CreatorEarningStatus Status { get; private set; } = CreatorEarningStatus.Pending;
    public DateTime EarnedAtUtc { get; set; }
    public DateTime AvailableAtUtc { get; set; }
    public DateTime? HeldAtUtc { get; private set; }
    public DateTime? ScheduledAtUtc { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }
    public DateTime? ReversedAtUtc { get; private set; }
    public string? HoldReason { get; private set; }
    public Guid? PayoutId { get; private set; }
    public string CorrelationId { get; set; } = "";
    public void MakeAvailable(DateTime now) { if (Status != CreatorEarningStatus.Pending || now < AvailableAtUtc) throw new InvalidOperationException("Earning is not ready to mature."); Status = CreatorEarningStatus.Available; UpdatedAtUtc = now; }
    public void Schedule(Guid payoutId, DateTime now) { if (Status != CreatorEarningStatus.Available) throw new InvalidOperationException("Only available earnings can be scheduled."); Status = CreatorEarningStatus.ScheduledForPayout; PayoutId = payoutId; ScheduledAtUtc = now; UpdatedAtUtc = now; }
    public void ReleaseSchedule(DateTime now) { if (Status != CreatorEarningStatus.ScheduledForPayout) throw new InvalidOperationException("Earning is not scheduled."); Status = CreatorEarningStatus.Available; PayoutId = null; ScheduledAtUtc = null; UpdatedAtUtc = now; }
    public void MarkPaid(DateTime now) { if (Status != CreatorEarningStatus.ScheduledForPayout) throw new InvalidOperationException("Only scheduled earnings can be paid."); Status = CreatorEarningStatus.Paid; PaidAtUtc = now; UpdatedAtUtc = now; }
    public void Reverse(DateTime now) { if (Status == CreatorEarningStatus.Paid) throw new InvalidOperationException("Paid earnings require recovery."); if (Status == CreatorEarningStatus.Reversed) throw new InvalidOperationException("Earning is already reversed."); Status = CreatorEarningStatus.Reversed; ReversedAtUtc = now; UpdatedAtUtc = now; }
}

public sealed class CreatorBalanceAccount : Entity
{
    public Guid CreatorId { get; set; }
    public string CurrencyCode { get; set; } = "ETB"; public decimal PendingBalance { get; private set; }
    public decimal AvailableBalance { get; private set; }
    public decimal HeldBalance { get; private set; }
    public decimal ScheduledBalance { get; private set; }
    public decimal PaidLifetimeTotal { get; private set; }
    public decimal ReversedLifetimeTotal { get; private set; }
    public uint ConcurrencyToken { get; set; }
    public (decimal, decimal) CreditPending(decimal amount, DateTime now) => Move(CreatorBalanceCategory.Pending, null, amount, now);
    public (decimal, decimal) Transfer(CreatorBalanceCategory from, CreatorBalanceCategory to, decimal amount, DateTime now) => Move(to, from, amount, now);
    public (decimal, decimal) ReverseFrom(CreatorBalanceCategory from, decimal amount, DateTime now) => Move(CreatorBalanceCategory.Reversed, from, amount, now);
    private (decimal, decimal) Move(CreatorBalanceCategory to, CreatorBalanceCategory? from, decimal amount, DateTime now) { if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount)); if (from.HasValue) { var source = Get(from.Value); if (source < amount) throw new InvalidOperationException("Creator balance cannot become negative."); Set(from.Value, source - amount); } var before = Get(to); Set(to, before + amount); UpdatedAtUtc = now; return (before, before + amount); }
    private decimal Get(CreatorBalanceCategory c) => c switch { CreatorBalanceCategory.Pending => PendingBalance, CreatorBalanceCategory.Available => AvailableBalance, CreatorBalanceCategory.Held => HeldBalance, CreatorBalanceCategory.Scheduled => ScheduledBalance, CreatorBalanceCategory.Paid => PaidLifetimeTotal, CreatorBalanceCategory.Reversed => ReversedLifetimeTotal, _ => throw new ArgumentOutOfRangeException() };
    private void Set(CreatorBalanceCategory c, decimal v) { switch (c) { case CreatorBalanceCategory.Pending: PendingBalance = v; break; case CreatorBalanceCategory.Available: AvailableBalance = v; break; case CreatorBalanceCategory.Held: HeldBalance = v; break; case CreatorBalanceCategory.Scheduled: ScheduledBalance = v; break; case CreatorBalanceCategory.Paid: PaidLifetimeTotal = v; break; case CreatorBalanceCategory.Reversed: ReversedLifetimeTotal = v; break; } }
}
public sealed class CreatorBalanceEntry : Entity { public Guid CreatorBalanceAccountId { get; set; } public Guid CreatorId { get; set; } public CreatorBalanceEntryType EntryType { get; set; } public decimal Amount { get; set; } public string CurrencyCode { get; set; } = "ETB"; public CreatorBalanceCategory BalanceCategory { get; set; } public decimal BalanceBefore { get; set; } public decimal BalanceAfter { get; set; } public Guid? RelatedEarningId { get; set; } public Guid? RelatedPayoutId { get; set; } public Guid? RelatedTransactionId { get; set; } public string IdempotencyKey { get; set; } = ""; public string Description { get; set; } = ""; public string CorrelationId { get; set; } = ""; }
public sealed class PayoutBatch : Entity { public string PublicBatchId { get; set; } = ""; public string CurrencyCode { get; set; } = "ETB"; public PayoutBatchStatus Status { get; set; } public DateTime CutoffAtUtc { get; set; } public DateTime ScheduledForUtc { get; set; } public Guid CreatedByUserId { get; set; } public DateTime? ProcessingStartedAtUtc { get; set; } public DateTime? CompletedAtUtc { get; set; } public DateTime? FailedAtUtc { get; set; } public string? FailureReason { get; set; } public int TotalCreatorCount { get; set; } public decimal TotalAmount { get; set; } public string CorrelationId { get; set; } = ""; public ICollection<CreatorPayout> Payouts { get; } = []; }
public sealed class CreatorPayout : Entity { public string PublicPayoutId { get; set; } = ""; public Guid PayoutBatchId { get; set; } public Guid CreatorId { get; set; } public string CurrencyCode { get; set; } = "ETB"; public decimal Amount { get; set; } public CreatorPayoutStatus Status { get; set; } public PayoutMethodType PayoutMethodType { get; set; } = PayoutMethodType.Manual; public string? PayoutDestinationReference { get; set; } public string? ProviderReference { get; set; } public DateTime ScheduledAtUtc { get; set; } public DateTime? SubmittedAtUtc { get; set; } public DateTime? PaidAtUtc { get; set; } public DateTime? FailedAtUtc { get; set; } public string? FailureReason { get; set; } public int RetryCount { get; set; } public string IdempotencyKey { get; set; } = ""; public string CorrelationId { get; set; } = ""; public PayoutBatch Batch { get; set; } = null!; public ICollection<PayoutItem> Items { get; } = []; public ICollection<PayoutAttempt> Attempts { get; } = []; }
public sealed class PayoutItem : Entity { public Guid CreatorPayoutId { get; set; } public Guid CreatorEarningId { get; set; } public decimal Amount { get; set; } public string CurrencyCode { get; set; } = "ETB"; public CreatorPayout Payout { get; set; } = null!; public CreatorEarning Earning { get; set; } = null!; }
public sealed class PayoutAttempt : Entity { public Guid CreatorPayoutId { get; set; } public int AttemptNumber { get; set; } public PayoutAttemptStatus Status { get; set; } public string? ProviderReference { get; set; } public string? FailureReason { get; set; } public DateTime AttemptedAtUtc { get; set; } }

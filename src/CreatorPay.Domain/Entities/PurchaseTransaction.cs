using CreatorPay.Domain.Common;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Domain.Entities;

public sealed class PurchaseTransaction : Entity
{
    public string PublicTransactionId { get; set; } = ""; public Guid CreatorId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? CheckoutSessionId { get; set; }
    public decimal CustomerCashbackAmount { get; set; }
    public Guid MerchantId { get; set; }
    public Guid? MerchantLocationId { get; set; }
    public Guid CashierId { get; set; }
    public Guid? CreatorQrCodeId { get; set; }
    public Guid MerchantCreatorPartnershipId { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? CampaignQrCodeId { get; set; }
    public Guid? CampaignCommissionRuleVersionId { get; set; }
    public DateTime? CampaignStartsAtUtc { get; set; }
    public DateTime? CampaignExpiresAtUtc { get; set; }
    public decimal PurchaseAmount { get; set; }
    public string CurrencyCode { get; set; } = "ETB"; public Guid CommissionCalculationSnapshotId { get; set; }
    public TransactionStatus Status { get; private set; } = TransactionStatus.Pending; public DateTime TransactionDateUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; private set; }
    public DateTime? RejectedAtUtc { get; private set; }
    public string? RejectionReason { get; private set; }
    public string IdempotencyKey { get; set; } = ""; public string? ClientOperationId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string CorrelationId { get; set; } = ""; public CommissionCalculationSnapshot CommissionSnapshot { get; set; } = null!; public ICollection<TransactionStatusHistory> StatusHistory { get; } = [];
    public Guid? CustomerPhoneReferenceId { get; set; }
    public string? CustomerPhoneHash { get; set; }
    public DateOnly? MerchantLocalDate { get; set; }
    public Guid? RepeatUseApprovalRequestId { get; set; }
    public bool WasRepeatUseApproved { get; set; }
    public Guid? CustomerConfirmationId { get; set; }
    public void Confirm(DateTime now, Guid actor) { var old = Status; Status = TransactionStatus.Confirmed; ConfirmedAtUtc = now; StatusHistory.Add(new() { Id = Guid.NewGuid(), PreviousStatus = old, NewStatus = Status, ChangedAtUtc = now, ChangedByUserId = actor, CreatedAtUtc = now }); }
    public void ApplyReversal(bool full, DateTime now, Guid actor, string correlationId) { if (Status is not (TransactionStatus.Confirmed or TransactionStatus.PartiallyReversed or TransactionStatus.Disputed)) throw new InvalidOperationException("Only a confirmed transaction can be reversed."); var old = Status; Status = full ? TransactionStatus.Reversed : TransactionStatus.PartiallyReversed; UpdatedAtUtc = now; StatusHistory.Add(new() { Id = Guid.NewGuid(), PreviousStatus = old, NewStatus = Status, ChangedAtUtc = now, ChangedByUserId = actor, Reason = "Financial reversal posted", CorrelationId = correlationId, CreatedAtUtc = now }); }
}
public sealed class TransactionStatusHistory : Entity { public Guid PurchaseTransactionId { get; set; } public TransactionStatus PreviousStatus { get; set; } public TransactionStatus NewStatus { get; set; } public DateTime ChangedAtUtc { get; set; } public Guid ChangedByUserId { get; set; } public string? Reason { get; set; } public string? CorrelationId { get; set; } public PurchaseTransaction PurchaseTransaction { get; set; } = null!; }

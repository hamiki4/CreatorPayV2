using CreatorPay.Domain.Common;

namespace CreatorPay.Domain.Entities;

public sealed class OfflineSyncBatch : Entity
{
    public string PublicBatchId { get; set; } = string.Empty;
    public Guid CashierUserId { get; set; }
    public Guid MerchantId { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public int ItemCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int ApprovalRequiredCount { get; set; }
    public int RejectedCount { get; set; }
    public int DuplicateCount { get; set; }
    public int FailedCount { get; set; }
    public string AppVersion { get; set; } = string.Empty;
    public string? DeviceReferenceHash { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public ICollection<OfflineSyncItemResult> Items { get; } = [];
}

public sealed class OfflineSyncItemResult : Entity
{
    public Guid OfflineSyncBatchId { get; set; }
    public Guid CashierUserId { get; set; }
    public string ClientOperationId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string ResultStatus { get; set; } = string.Empty;
    public Guid? PurchaseTransactionId { get; set; }
    public Guid? RepeatUseApprovalRequestId { get; set; }
    public string? ErrorCode { get; set; }
    public string SafeMessage { get; set; } = string.Empty;
    public DateTime ProcessedAtUtc { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public OfflineSyncBatch Batch { get; set; } = null!;
}

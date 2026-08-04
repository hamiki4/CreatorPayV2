namespace CreatorPay.Application.OfflineSync;

public sealed class OfflineSyncOptions
{
    public const string SectionName = "OfflineSync";
    public int OfflineOperationMaximumAgeHours { get; set; } = 24;
    public int OfflineDraftRetentionDays { get; set; } = 7;
    public int CompletedOperationRetentionDays { get; set; } = 7;
    public int MaximumOfflineBatchSize { get; set; } = 25;
    public int MaximumOfflineRetryCount { get; set; } = 8;
    public int SupportedSchemaVersion { get; set; } = 1;
}
public sealed record OfflineSyncRequest(string BatchId,string? DeviceInstallationId,string AppVersion,int SchemaVersion,IReadOnlyList<OfflinePurchaseRequest> Operations);
public sealed record OfflinePurchaseRequest(string ClientOperationId,string IdempotencyKey,Guid MerchantLocationId,string QrPayload,string CustomerPhoneNumber,decimal PurchaseAmount,string CurrencyCode,DateTime ClientCreatedAtUtc);
public sealed record OfflineSyncItemResponse(string ClientOperationId,string IdempotencyKey,string ResultStatus,Guid? TransactionId,string? PublicTransactionId,string? ApprovalRequestId,string? ErrorCode,string SafeMessage,DateTime? ConfirmedAtUtc,bool Retryable,string CorrelationId);
public sealed record OfflineSyncResponse(string BatchId,DateTime ReceivedAtUtc,IReadOnlyList<OfflineSyncItemResponse> Results);
public interface IOfflineSyncService { Task<OfflineSyncResponse> SynchronizeAsync(Guid merchantId,Guid actor,Guid cashierId,string role,string correlationId,OfflineSyncRequest request,CancellationToken ct); }

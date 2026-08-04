using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CreatorPay.Application.CustomerVerification;
using CreatorPay.Application.OfflineSync;
using CreatorPay.Application.Wallet;
using CreatorPay.Domain.Entities;
using CreatorPay.Infrastructure.Persistence;
using CreatorPay.Infrastructure.Wallet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace CreatorPay.Infrastructure.OfflineSync;

public sealed class OfflineSyncService(ApplicationDbContext db, IServiceScopeFactory scopes, IOptions<OfflineSyncOptions> configured) : IOfflineSyncService
{
    readonly OfflineSyncOptions options = configured.Value;
    public async Task<OfflineSyncResponse> SynchronizeAsync(Guid merchantId, Guid actor, Guid cashierId, string role, string correlationId, OfflineSyncRequest request, CancellationToken ct)
    {
        if (request.Operations is null || request.Operations.Count == 0) throw new ArgumentException("At least one operation is required.");
        if (request.Operations.Count > options.MaximumOfflineBatchSize) { Audit(merchantId, actor, "ExcessiveOfflineBatchAttempt", $"ItemCount={request.Operations.Count}"); await db.SaveChangesAsync(ct); throw new ArgumentException($"A batch may contain at most {options.MaximumOfflineBatchSize} operations."); }
        if (request.SchemaVersion != options.SupportedSchemaVersion) { Audit(merchantId, actor, "OfflineSchemaVersionRejected", $"SchemaVersion={request.SchemaVersion}"); await db.SaveChangesAsync(ct); throw new UnsupportedOfflineSchemaException(); }
        if (string.IsNullOrWhiteSpace(request.BatchId) || request.BatchId.Length > 100 || request.AppVersion?.Length > 40) throw new ArgumentException("Batch metadata is invalid.");
        var anyBatch = await db.OfflineSyncBatches.AsNoTracking().SingleOrDefaultAsync(x => x.PublicBatchId == request.BatchId, ct); if (anyBatch is not null && (anyBatch.MerchantId != merchantId || anyBatch.CashierUserId != actor)) { Audit(merchantId, actor, "OfflineSyncCrossMerchantAttempt", $"BatchId={request.BatchId}"); await db.SaveChangesAsync(ct); throw new UnauthorizedAccessException("The batch identifier belongs to a different scope."); }
        var previousBatch = anyBatch;
        if (previousBatch is not null) { var replay = new List<OfflineSyncItemResponse>(request.Operations.Count); foreach (var operation in request.Operations) replay.Add(await Process(previousBatch, merchantId, actor, cashierId, role, operation, ct)); return new(previousBatch.PublicBatchId, previousBatch.ReceivedAtUtc, replay); }
        var now = DateTime.UtcNow; var batch = new OfflineSyncBatch { Id = Guid.NewGuid(), PublicBatchId = request.BatchId, CashierUserId = actor, MerchantId = merchantId, ReceivedAtUtc = now, ItemCount = request.Operations.Count, AppVersion = request.AppVersion ?? "unknown", DeviceReferenceHash = HashDevice(request.DeviceInstallationId), CorrelationId = correlationId, CreatedAtUtc = now, CreatedBy = actor.ToString() }; db.Add(batch); Audit(merchantId, actor, "OfflineSyncBatchReceived", $"BatchId={batch.PublicBatchId};ItemCount={batch.ItemCount}"); await db.SaveChangesAsync(ct);
        var results = new List<OfflineSyncItemResponse>(request.Operations.Count);
        foreach (var operation in request.Operations) { var item = await Process(batch, merchantId, actor, cashierId, role, operation, ct); results.Add(item); Count(batch, item.ResultStatus); }
        await db.SaveChangesAsync(ct); return new(batch.PublicBatchId, batch.ReceivedAtUtc, results);
    }
    async Task<OfflineSyncItemResponse> Process(OfflineSyncBatch batch, Guid merchant, Guid actor, Guid cashier, string role, OfflinePurchaseRequest op, CancellationToken ct)
    {
        var correlation = $"{batch.CorrelationId}:{Guid.NewGuid():N}"; var requestHash = Hash(op);
        if (string.IsNullOrWhiteSpace(op.ClientOperationId) || op.ClientOperationId.Length > 100 || string.IsNullOrWhiteSpace(op.IdempotencyKey) || op.IdempotencyKey.Length > 200) return await Store("Rejected", "InvalidOperation", "The offline operation identifiers are invalid.", false);
        var prior = await db.OfflineSyncItemResults.AsNoTracking().SingleOrDefaultAsync(x => x.CashierUserId == actor && x.ClientOperationId == op.ClientOperationId, ct);
        if (prior is not null) { if (prior.RequestHash != requestHash) return MakeResponse(op, "Conflict", null, null, null, "IdempotencyConflict", "This operation identifier was already used with different details.", false, correlation); var approval = prior.RepeatUseApprovalRequestId.HasValue ? await db.RepeatUseApprovalRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == prior.RepeatUseApprovalRequestId, ct) : null; if (approval?.RelatedPurchaseTransactionId is Guid finalizedId) { var finalized = await db.PurchaseTransactions.AsNoTracking().SingleAsync(x => x.Id == finalizedId, ct); var tracked = await db.OfflineSyncItemResults.SingleAsync(x => x.Id == prior.Id, ct); tracked.ResultStatus = "Confirmed"; tracked.PurchaseTransactionId = finalizedId; tracked.SafeMessage = "Purchase confirmed after approval."; await db.SaveChangesAsync(ct); return MakeResponse(op, "Confirmed", finalizedId, finalized.PublicTransactionId, approval.PublicApprovalRequestId, null, tracked.SafeMessage, false, prior.CorrelationId, finalized.ConfirmedAtUtc); } var purchase = prior.PurchaseTransactionId.HasValue ? await db.PurchaseTransactions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == prior.PurchaseTransactionId, ct) : null; return MakeResponse(op, prior.ResultStatus == "Confirmed" ? "DuplicateConfirmed" : prior.ResultStatus, prior.PurchaseTransactionId, purchase?.PublicTransactionId, approval?.PublicApprovalRequestId, prior.ErrorCode, prior.SafeMessage, false, prior.CorrelationId, purchase?.ConfirmedAtUtc); }
        if (op.ClientCreatedAtUtc > DateTime.UtcNow.AddMinutes(10) || op.ClientCreatedAtUtc < DateTime.UtcNow.AddHours(-options.OfflineOperationMaximumAgeHours)) return await Store("Expired", "OfflineOperationExpired", "This offline operation is too old to process. Create a new purchase request.", false);
        if (op.PurchaseAmount <= 0 || op.QrPayload?.Length is null or > 4096 || op.CustomerPhoneNumber?.Length is null or > 40) return await Store("Rejected", "InvalidPayload", "The purchase details are invalid.", false);
        try
        {
            using var scope = scopes.CreateScope(); var gate = scope.ServiceProvider.GetRequiredService<IRepeatUseApprovalService>(); var wallet = scope.ServiceProvider.GetRequiredService<IWalletService>(); var request = new ConfirmPurchaseRequest(op.QrPayload, op.MerchantLocationId, op.PurchaseAmount, op.CurrencyCode, op.ClientOperationId, op.CustomerPhoneNumber);
            var approval = await gate.GateAsync(merchant, actor, cashier, role, op.IdempotencyKey, request, ct);
            if (approval is not null) { var approvalId = await db.RepeatUseApprovalRequests.AsNoTracking().Where(x => x.PublicApprovalRequestId == approval.ApprovalRequestId).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct); var stored = await Store("ApprovalRequired", null, "Approval is required before this purchase can be confirmed.", false, null, null, approvalId); return stored with { ApprovalRequestId = approval.ApprovalRequestId }; }
            var purchase = await wallet.ConfirmPurchaseAsync(merchant, actor, cashier, role, op.IdempotencyKey, request, ct);
            var duplicate = await db.OfflineSyncItemResults.AnyAsync(x => x.IdempotencyKey == op.IdempotencyKey && x.PurchaseTransactionId == purchase.TransactionId, ct);
            return await Store(duplicate ? "DuplicateConfirmed" : "Confirmed", null, duplicate ? "This purchase was already confirmed." : "Purchase confirmed by the server.", false, purchase.TransactionId, purchase.PublicTransactionId, null, purchase.ConfirmedAtUtc);
        }
        catch (IdempotencyConflictException) { return await Store("Conflict", "IdempotencyConflict", "The idempotency key was already used with different purchase details.", false); }
        catch (PurchaseRejectedException x) { return await Store("Rejected", SafeCode(x.Code), SafeMessage(x.Code), false); }
        catch (ArgumentException) { return await Store("Rejected", "InvalidPayload", "The purchase details could not be validated.", false); }
        catch (KeyNotFoundException) { return await Store("Rejected", "ReferenceNotFound", "A required account, location, or approval record is unavailable.", false); }
        catch (UnauthorizedAccessException) { return await Store("Rejected", "ScopeDenied", "The cashier is not authorized for this operation.", false); }
        catch (DbUpdateException) { return await Store("RetryLater", "ConcurrentProcessing", "The operation is being processed. Retry later.", true); }
        catch (Exception) { return await Store("RetryLater", "TemporaryFailure", "The operation could not be completed safely. Retry later.", true); }
        async Task<OfflineSyncItemResponse> Store(string status, string? code, string message, bool retryable, Guid? transaction = null, string? publicTransaction = null, Guid? approval = null, DateTime? confirmed = null) { var row = new OfflineSyncItemResult { Id = Guid.NewGuid(), OfflineSyncBatchId = batch.Id, CashierUserId = actor, ClientOperationId = op.ClientOperationId ?? "invalid", IdempotencyKey = op.IdempotencyKey ?? "invalid", RequestHash = requestHash, ResultStatus = status, PurchaseTransactionId = transaction, RepeatUseApprovalRequestId = approval, ErrorCode = code, SafeMessage = message, ProcessedAtUtc = DateTime.UtcNow, CorrelationId = correlation, CreatedAtUtc = DateTime.UtcNow, CreatedBy = actor.ToString() }; db.Add(row); Audit(merchant, actor, $"OfflineSyncItem{status}", $"ClientOperationId={row.ClientOperationId};ErrorCode={code};CorrelationId={correlation}"); await db.SaveChangesAsync(ct); return MakeResponse(op, status, transaction, publicTransaction, approval?.ToString(), code, message, retryable, correlation, confirmed); }
    }
    static OfflineSyncItemResponse MakeResponse(OfflinePurchaseRequest op, string s, Guid? t, string? p, string? a, string? e, string m, bool r, string c, DateTime? confirmed = null) => new(op.ClientOperationId, op.IdempotencyKey, s, t, p, a, e, m, confirmed, r, c);
    void Audit(Guid merchant, Guid actor, string type, string detail) => db.MerchantAuditEvents.Add(new() { Id = Guid.NewGuid(), MerchantId = merchant, ActorUserAccountId = actor, EventType = type, Detail = detail, CreatedAtUtc = DateTime.UtcNow, CreatedBy = actor.ToString() });
    static string Hash(OfflinePurchaseRequest x) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(x))));
    static string? HashDevice(string? x) => string.IsNullOrWhiteSpace(x) ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(x)));
    static string SafeCode(string code) => code.Length <= 80 && code.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-') ? code : "PurchaseRejected";
    static string SafeMessage(string code) => code switch { "InsufficientWalletBalance" => "The merchant wallet has insufficient available balance.", _ => "The purchase is no longer eligible and was rejected by the server." };
    static void Count(OfflineSyncBatch b, string s) { if (s == "Confirmed") b.ConfirmedCount++; else if (s == "DuplicateConfirmed") b.DuplicateCount++; else if (s == "ApprovalRequired") b.ApprovalRequiredCount++; else if (s == "RetryLater") b.FailedCount++; else b.RejectedCount++; }
}
public sealed class UnsupportedOfflineSchemaException : Exception { public UnsupportedOfflineSchemaException() : base("The offline operation schema is not supported. Update CreatorPay and try again.") { } }

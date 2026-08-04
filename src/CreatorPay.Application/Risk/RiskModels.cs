using CreatorPay.Domain.Enums;

namespace CreatorPay.Application.Risk;

public sealed record OpenDisputeRequest(Guid PurchaseTransactionId, DisputeType DisputeType, string Description, IReadOnlyList<EvidenceMetadata>? Evidence);
public sealed record EvidenceMetadata(string EvidenceType, string FileName, string? ContentType, string MetadataJson);
public sealed record AssignReviewerRequest(Guid ReviewerUserId);
public sealed record DecisionRequest(string Reason, bool CreateReversal = false, decimal? ReversalAmount = null, string? IdempotencyKey = null);
public sealed record StatusRequest(FraudAlertStatus Status, string? Reason);
public sealed record ResolveFraudRequest(FraudAlertStatus Status, string Resolution);
public sealed record CreateReversalRequest(Guid PurchaseTransactionId, Guid? DisputeId, ReversalType ReversalType, decimal Amount, string Reason, string IdempotencyKey);
public sealed record ReversalPreview(decimal Amount, decimal CreatorAmount, decimal PlatformAmount, string CurrencyCode, bool CreatesRecovery);
public sealed record FraudEvaluationContext(string Stage, Guid? PurchaseTransactionId, Guid MerchantId, Guid? MerchantLocationId, Guid? CreatorId, Guid? CashierId, string? CustomerPhoneHash, decimal? PurchaseAmount, decimal? CreatorCommission, DateTime OccurredAtUtc, string CorrelationId);
public sealed record FraudEvaluationResult(FraudEvaluationDecision Decision, IReadOnlyList<Guid> AlertIds);
public interface IRiskOperationsService
{
    Task<FraudEvaluationResult> EvaluateAsync(FraudEvaluationContext context, CancellationToken ct);
    Task<object> OpenDisputeAsync(Guid actor, string role, Guid? merchantId, Guid? creatorId, OpenDisputeRequest request, string correlationId, CancellationToken ct); Task<IReadOnlyList<object>> ListDisputesAsync(string role, Guid? merchantId, Guid? creatorId, CancellationToken ct); Task<object> GetDisputeAsync(Guid id, string role, Guid? merchantId, Guid? creatorId, CancellationToken ct); Task<object> AssignDisputeAsync(Guid id, Guid actor, AssignReviewerRequest request, CancellationToken ct); Task<object> RequestEvidenceAsync(Guid id, Guid actor, string reason, CancellationToken ct); Task<object> DecideDisputeAsync(Guid id, Guid actor, bool approved, DecisionRequest request, CancellationToken ct); Task<object> CancelDisputeAsync(Guid id, Guid actor, string reason, CancellationToken ct);
    Task<IReadOnlyList<object>> ListFraudAlertsAsync(CancellationToken ct); Task<object> GetFraudAlertAsync(Guid id, CancellationToken ct); Task<object> AssignFraudAlertAsync(Guid id, Guid actor, AssignReviewerRequest request, CancellationToken ct); Task<object> UpdateFraudStatusAsync(Guid id, Guid actor, StatusRequest request, CancellationToken ct); Task<object> ResolveFraudAsync(Guid id, Guid actor, ResolveFraudRequest request, CancellationToken ct);
    Task<object> CreateReversalAsync(Guid actor, CreateReversalRequest request, string correlationId, CancellationToken ct); Task<IReadOnlyList<object>> ListReversalsAsync(CancellationToken ct); Task<object> GetReversalAsync(Guid id, CancellationToken ct); Task<object> ApproveReversalAsync(Guid id, Guid actor, CancellationToken ct); Task<object> ProcessReversalAsync(Guid id, Guid actor, CancellationToken ct); Task<object> CancelReversalAsync(Guid id, Guid actor, string reason, CancellationToken ct);
}

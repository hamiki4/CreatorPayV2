namespace CreatorPay.Application.Wallet;

public sealed class WalletOptions { public const string SectionName = "MerchantWallet"; public decimal LowBalanceThreshold { get; set; } = 1000m; public string CurrencyCode { get; set; } = "ETB"; }
public sealed record SubmitDepositRequest(decimal Amount, string CurrencyCode, string ExternalReference, string? ProofMetadata);
public sealed record RejectDepositRequest(string Reason);
public sealed record ConfirmPurchaseRequest(string QrPayload, Guid MerchantLocationId, decimal PurchaseAmount, string CurrencyCode, string? ClientOperationId, string CustomerPhoneNumber, Guid? RepeatUseApprovalRequestId = null);
public sealed record WalletDto(Guid Id, string CurrencyCode, decimal AvailableBalance, decimal HeldBalance, string Status);
public sealed record DepositDto(Guid Id, decimal Amount, string CurrencyCode, string Status, string ExternalReference, string? ProofMetadata, DateTime SubmittedAtUtc, DateTime? VerifiedAtUtc, string? FailureReason);
public sealed record WalletEntryDto(Guid Id, string EntryType, decimal Amount, decimal BalanceBefore, decimal BalanceAfter, string Description, DateTime CreatedAtUtc);
public sealed record PurchaseDto(Guid TransactionId, string PublicTransactionId, string Status, decimal PurchaseAmount, string CurrencyCode, decimal TotalCommissionAmount, decimal CreatorCommissionAmount, decimal PlatformCommissionAmount, string CreatorDisplayName, string MerchantLocationName, DateTime? ConfirmedAtUtc, decimal? WalletBalanceAfter, string Message);
public interface IWalletService
{
    Task<WalletDto> GetWalletAsync(Guid merchantId, CancellationToken ct); Task<IReadOnlyList<WalletEntryDto>> GetEntriesAsync(Guid merchantId, CancellationToken ct); Task<IReadOnlyList<DepositDto>> GetDepositsAsync(Guid? merchantId, bool pendingOnly, CancellationToken ct); Task<DepositDto> GetDepositAsync(Guid id, Guid? merchantId, CancellationToken ct); Task<DepositDto> SubmitDepositAsync(Guid merchantId, Guid actor, string key, SubmitDepositRequest request, CancellationToken ct); Task<DepositDto> ApproveDepositAsync(Guid id, Guid actor, string key, CancellationToken ct); Task<DepositDto> RejectDepositAsync(Guid id, Guid actor, RejectDepositRequest request, CancellationToken ct); Task<PurchaseDto> ConfirmPurchaseAsync(Guid merchantId, Guid actor, Guid cashierId, string role, string key, ConfirmPurchaseRequest request, CancellationToken ct); Task<PurchaseDto> GetPurchaseAsync(Guid id, Guid? merchantId, Guid? cashierId, CancellationToken ct); Task<IReadOnlyList<PurchaseDto>> GetPurchasesAsync(Guid? merchantId, Guid? cashierId, int take, CancellationToken ct);
}

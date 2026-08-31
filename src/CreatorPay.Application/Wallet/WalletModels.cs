namespace CreatorPay.Application.Wallet;

public sealed class WalletOptions { public const string SectionName = "MerchantWallet"; public decimal MinimumActivationBalance { get; set; } = 1000m; public decimal LowBalanceThreshold { get; set; } = 1000m; public string CurrencyCode { get; set; } = "ETB"; }
public sealed record SubmitDepositRequest(decimal Amount, string CurrencyCode, string ExternalReference, string? ProofMetadata);
public sealed record RejectDepositRequest(string Reason);
public sealed record WalletDto(Guid Id, string CurrencyCode, decimal AvailableBalance, decimal HeldBalance, string Status, decimal MinimumRequiredBalance = 0m, bool AdvertisingEligible = true);
public sealed record DepositDto(Guid Id, Guid MerchantId, string BusinessName, decimal Amount, string CurrencyCode, string Status, string ExternalReference, string? ProofMetadata, DateTime SubmittedAtUtc, DateTime? VerifiedAtUtc, string? FailureReason);
public sealed record DepositProofDescriptor(string StorageKey, string FileName, string ContentType, long SizeBytes);
public interface IDepositProofStorage
{
    Task<DepositProofDescriptor> SaveAsync(Guid merchantId, Stream content, string fileName, string contentType, long sizeBytes, CancellationToken ct);
    Task<(Stream Content, string ContentType, string FileName)> OpenAsync(string storageKey, CancellationToken ct);
}
public sealed record WalletEntryDto(Guid Id, string EntryType, decimal Amount, decimal BalanceBefore, decimal BalanceAfter, string Description, DateTime CreatedAtUtc);
public sealed record PurchaseDto(Guid TransactionId, string PublicTransactionId, string Status, decimal PurchaseAmount, string CurrencyCode, decimal TotalCommissionAmount, decimal CreatorCommissionAmount, decimal PlatformCommissionAmount, string CreatorDisplayName, string CreatorPublicId, string MerchantLocationName, DateTime? ConfirmedAtUtc, decimal? WalletBalanceAfter, string Message);
public sealed record ConfirmedSaleDto(Guid TransactionId, string PublicTransactionId, DateTime ConfirmedAtUtc, decimal SaleAmount, decimal CommissionAmount, string CreatorName, string CreatorId, string CashierName, string Status, string? LocationName);
public sealed record ConfirmedSalesReportDto(int ConfirmedSales, decimal TotalSalesAmount, decimal TotalCommissionAmount, IReadOnlyList<ConfirmedSaleDto> Sales, IReadOnlyList<CreatorPerformanceDto> CreatorPerformance);
public sealed record CreatorPerformanceDto(string CreatorName, string CreatorId, int ConfirmedSales, decimal SalesAmount, decimal CommissionAmount);
public sealed record ConfirmedSalesQuery(string? Search, Guid? CreatorId, Guid? CashierId, DateTime? DateFromUtc, DateTime? DateToUtc, string? Status, int Page = 1, int PageSize = 50, string? CreatorName = null, string? CashierName = null);
public interface IWalletService
{
    Task<WalletDto> GetWalletAsync(Guid merchantId, CancellationToken ct); Task<IReadOnlyList<WalletEntryDto>> GetEntriesAsync(Guid merchantId, CancellationToken ct); Task<IReadOnlyList<DepositDto>> GetDepositsAsync(Guid? merchantId, bool pendingOnly, CancellationToken ct); Task<DepositDto> GetDepositAsync(Guid id, Guid? merchantId, CancellationToken ct); Task<DepositDto> SubmitDepositAsync(Guid merchantId, Guid actor, string key, SubmitDepositRequest request, CancellationToken ct); Task<DepositDto> ApproveDepositAsync(Guid id, Guid actor, string key, CancellationToken ct); Task<DepositDto> RejectDepositAsync(Guid id, Guid actor, RejectDepositRequest request, CancellationToken ct); Task<PurchaseDto> GetPurchaseAsync(Guid id, Guid? merchantId, Guid? cashierId, CancellationToken ct); Task<IReadOnlyList<PurchaseDto>> GetPurchasesAsync(Guid? merchantId, Guid? cashierId, int take, CancellationToken ct); Task<ConfirmedSalesReportDto> GetConfirmedSalesAsync(Guid merchantId, ConfirmedSalesQuery request, CancellationToken ct);
}

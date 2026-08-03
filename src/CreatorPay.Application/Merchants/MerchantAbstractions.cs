using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Application.Merchants;

public interface IMerchantStore
{
    Task<bool> EmailExistsAsync(string email, Guid? excludingUserId, CancellationToken ct); Task<bool> PhoneExistsAsync(string phone, Guid? excludingMerchantId, CancellationToken ct);
    Task<Merchant?> FindMerchantAsync(Guid id, CancellationToken ct); Task<UserAccount?> FindUserByMerchantAsync(Guid id, CancellationToken ct);
    Task<(UserAccount User, Merchant Merchant, IReadOnlyList<MerchantDocument> Documents)?> FindByUserAsync(Guid id, CancellationToken ct);
    Task<(UserAccount User, Merchant Merchant)?> FindByTokenAsync(string hash, string purpose, CancellationToken ct); Task<MerchantVerificationToken?> FindTokenAsync(string hash, string purpose, CancellationToken ct);
    Task<IReadOnlyList<Merchant>> FindByStatusAsync(MerchantStatus status, CancellationToken ct); Task<IReadOnlyList<MerchantDocument>> FindDocumentsAsync(Guid merchantId, CancellationToken ct);
    Task InvalidateTokensAsync(Guid userId, string purpose, DateTime usedAt, CancellationToken ct);
    void Add(UserAccount item); void Add(Merchant item); void Add(MerchantVerificationToken item); void Add(MerchantDocument item); void Add(MerchantAuditEvent item);
    Task<int> SaveAsync(CancellationToken ct); Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
}
public interface IMerchantVerificationProvider { Task SendEmailVerificationAsync(UserAccount user, string token, CancellationToken ct); Task SendPhoneVerificationAsync(Merchant merchant, string token, CancellationToken ct); }
public interface IMerchantDocumentStorage { MerchantDocumentRegistration Register(MerchantDocumentMetadata metadata); }
public sealed record MerchantDocumentRegistration(string Provider, string? StorageKey);

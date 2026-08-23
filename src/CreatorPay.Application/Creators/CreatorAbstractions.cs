using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Application.Creators;

public interface ICreatorStore
{
    Task<bool> EmailExistsAsync(string normalizedEmail, Guid? excludingUserId, CancellationToken ct);
    Task<bool> PhoneExistsAsync(string normalizedPhone, Guid? excludingCreatorId, CancellationToken ct);
    Task<string> AllocateCreatorCodeAsync(CancellationToken ct);
    Task<UserAccount?> FindUserAsync(Guid id, CancellationToken ct);
    Task<UserAccount?> FindUserByCreatorAsync(Guid creatorId, CancellationToken ct);
    Task<Creator?> FindCreatorAsync(Guid id, CancellationToken ct);
    Task<(UserAccount User, Creator Creator)?> FindByUserAsync(Guid userId, CancellationToken ct);
    Task<(UserAccount User, Creator Creator)?> FindByTokenAsync(string hash, string purpose, CancellationToken ct);
    Task<CreatorVerificationToken?> FindTokenAsync(string hash, string purpose, CancellationToken ct);
    Task<long> GetMinimumTikTokFollowersAsync(CancellationToken ct);
    Task<IReadOnlyList<Creator>> FindByStatusAsync(CreatorStatus status, CancellationToken ct);
    Task InvalidateTokensAsync(Guid userId, string purpose, DateTime usedAtUtc, CancellationToken ct);
    void Add(UserAccount user); void Add(Creator creator); void Add(CreatorVerificationToken token); void Add(CreatorAuditEvent audit); void Add(CreatorSocialProfile profile);
    void RemoveSocialProfiles(IEnumerable<CreatorSocialProfile> profiles);
    Task<int> SaveAsync(CancellationToken ct);
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
}

public interface ICreatorVerificationProvider
{
    Task SendEmailVerificationAsync(UserAccount user, string token, CancellationToken ct);
    Task SendPhoneVerificationAsync(Creator creator, string token, CancellationToken ct);
}

public interface ICreatorProfilePhotoStore
{
    Task<(string StorageKey, long SizeBytes)> SaveAsync(Guid creatorId, Stream content, string contentType, long sizeBytes, CancellationToken ct);
    Task<Stream> OpenAsync(string storageKey, CancellationToken ct);
    Task DeleteAsync(string storageKey, CancellationToken ct);
}

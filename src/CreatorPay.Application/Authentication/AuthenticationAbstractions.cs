using CreatorPay.Domain.Entities;

namespace CreatorPay.Application.Authentication;

public interface IPasswordHasher { string Hash(UserAccount user, string password); PasswordVerification Verify(UserAccount user, string hash, string password); }
public enum PasswordVerification { Failed, Success, SuccessRehashNeeded }
public interface ITokenService { (string Token, DateTime ExpiresAtUtc) CreateAccessToken(UserAccount user); string CreateOpaqueToken(); string HashToken(string token); }
public interface IUtcClock { DateTime UtcNow { get; } }
public interface IPasswordResetNotifier { Task NotifyAsync(UserAccount user, string rawToken, CancellationToken cancellationToken); }
public interface ICurrentUserService { bool IsAuthenticated { get; } Guid? UserAccountId { get; } string? Role { get; } Guid? MerchantId { get; } Guid? CreatorId { get; } Guid? SupervisorId { get; } Guid? CashierId { get; } }
public interface IAuthenticationService
{
    Task<Result<TokenPair>> LoginAsync(LoginRequest request, RequestContext context, CancellationToken ct);
    Task<Result<TokenPair>> RefreshAsync(string token, RequestContext context, CancellationToken ct);
    Task<OperationResult> LogoutAsync(string token, RequestContext context, CancellationToken ct);
    Task<OperationResult> LogoutAllAsync(Guid userId, RequestContext context, CancellationToken ct);
    Task<Result<CurrentUser>> GetCurrentUserAsync(Guid userId, CancellationToken ct);
    Task<OperationResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, RequestContext context, CancellationToken ct);
    Task ForgotPasswordAsync(string email, RequestContext context, CancellationToken ct);
    Task<OperationResult> ResetPasswordAsync(ResetPasswordRequest request, RequestContext context, CancellationToken ct);
}
public interface IAuthenticationStore
{
    Task<UserAccount?> FindUserByEmailAsync(string normalizedEmail, CancellationToken ct); Task<UserAccount?> FindUserAsync(Guid id, CancellationToken ct);
    Task<RefreshToken?> FindRefreshAsync(string hash, CancellationToken ct); Task<PasswordResetToken?> FindResetAsync(string hash, CancellationToken ct);
    void AddRefresh(RefreshToken token); void AddReset(PasswordResetToken token); void AddAudit(LoginAudit audit);
    Task RevokeFamilyAsync(string family, DateTime now, string reason, string? ip, CancellationToken ct); Task RevokeAllAsync(Guid userId, DateTime now, string reason, string? ip, CancellationToken ct);
    Task<int> SaveAsync(CancellationToken ct); Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
}

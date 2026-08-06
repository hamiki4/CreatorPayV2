using CreatorPay.Domain.Enums;

namespace CreatorPay.Application.Authentication;

public sealed record RequestContext(string? IpAddress, string? UserAgent, string? CorrelationId);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record TokenRequest(string RefreshToken);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string Confirmation);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string ResetToken, string NewPassword, string Confirmation);
public sealed record CurrentUser(Guid UserAccountId, string Email, UserRole Role, AccountStatus Status, Guid? CreatorId, Guid? MerchantId, Guid? SupervisorId, Guid? CashierId, bool IsEmailVerified, bool IsPhoneVerified);
public sealed record TokenPair(string AccessToken, DateTime AccessTokenExpiresAtUtc, string RefreshToken, DateTime RefreshTokenExpiresAtUtc, CurrentUser User);
public static class AuthenticationClaimTypes
{
    public const string AccountStatus = "account_status";
    public const string EmailVerified = "email_verified";
    public const string PhoneVerified = "phone_verified";
}
public sealed record OperationResult(bool Succeeded, string? Error = null)
{
    public static OperationResult Success() => new(true);
    public static OperationResult Failure(string error) => new(false, error);
}
public sealed record Result<T>(T? Value, string? Error = null)
{
    public bool Succeeded => Error is null;
    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string error) => new(default, error);
}

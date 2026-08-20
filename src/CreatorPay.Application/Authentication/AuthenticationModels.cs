using CreatorPay.Domain.Enums;

namespace CreatorPay.Application.Authentication;

public sealed record RequestContext(string? IpAddress, string? UserAgent, string? CorrelationId);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record TokenRequest(string RefreshToken);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string Confirmation);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string ResetToken, string NewPassword, string Confirmation);
public sealed record FirebaseIdentityProof(string Uid, string Email, bool EmailVerified);
public sealed record LinkFirebaseRequest(string FirebaseIdToken, string RecoveryEmail);
public sealed record PinRequest(string Pin, string Confirmation);
public sealed record PinUnlockRequest(string PhoneNumber, string Pin);
public sealed record PasswordPinResetRequest(string PhoneNumber, string Password, string NewPin, string Confirmation);
public sealed record PinStatus(bool IsEligible, bool IsPinEnrolled, bool IsLocked, int FailedAttemptCount);
public sealed record CurrentUser(Guid UserAccountId, string Email, string? PhoneNumber, UserRole Role, AccountStatus Status, Guid? CreatorId, Guid? MerchantId, Guid? SupervisorId, Guid? CashierId, bool IsEmailVerified, bool IsPhoneVerified);
public sealed record TokenPair(string AccessToken, DateTime AccessTokenExpiresAtUtc, string RefreshToken, DateTime RefreshTokenExpiresAtUtc, CurrentUser User);
public static class AuthenticationClaimTypes
{
    public const string AccountStatus = "account_status";
    public const string EmailVerified = "email_verified";
    public const string PhoneVerified = "phone_verified";
}
public sealed record OperationResult(bool Succeeded, string? Error = null, string? Code = null)
{
    public static OperationResult Success() => new(true);
    public static OperationResult Failure(string error, string? code = null) => new(false, error, code);
}
public sealed record Result<T>(T? Value, string? Error = null, string? Code = null)
{
    public bool Succeeded => Error is null;
    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string error, string? code = null) => new(default, error, code);
}

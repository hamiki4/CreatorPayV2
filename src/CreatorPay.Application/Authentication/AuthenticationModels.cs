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
public sealed record PinRequest(string Pin, string Confirmation, DateOnly? BirthDate = null);
public sealed record PinUnlockRequest(string PhoneNumber, string Pin);
public sealed record PinRecoveryProofRequest(string PhoneNumber, DateOnly BirthDate);
public sealed record PinResetRequest(string ResetAuthorization, string NewPin, string Confirmation);
public sealed record PinStatus(bool IsEligible, bool HasBirthDate, bool IsPinEnrolled, bool IsLocked, int FailedAttemptCount);
public sealed record PinRecoveryAuthorization(string ResetAuthorization, DateTime ExpiresAtUtc);
public sealed record CurrentUser(Guid UserAccountId, string Email, string? PhoneNumber, UserRole Role, AccountStatus Status, Guid? CreatorId, Guid? MerchantId, Guid? SupervisorId, Guid? CashierId, bool IsEmailVerified, bool IsPhoneVerified);
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

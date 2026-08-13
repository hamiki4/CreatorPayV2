using CreatorPay.Domain.Entities;

namespace CreatorPay.Application.Authentication;

public sealed class SmsOtpOptions
{
    public const string SectionName = "SmsOtp";
    public string SmsProvider { get; set; } = "Disabled";
    public string ApiBaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string SenderId { get; set; } = "";
    public int OtpExpiryMinutes { get; set; } = 5;
    public int ResendCooldownSeconds { get; set; } = 60;
    public int MaxAttempts { get; set; } = 5;
    public string HashSecret { get; set; } = "";
    public string TestCode { get; set; } = "";
    public bool PilotRegistrationAutoVerifyEnabled { get; set; }
}

public sealed record VerifyPhoneOtpRequest(string PhoneNumber, string Code);
public sealed record ResendPhoneOtpRequest(string PhoneNumber);
public sealed record ForgotPasswordPhoneRequest(string PhoneNumber);
public sealed record ResetPasswordPhoneRequest(string PhoneNumber, string Code, string NewPassword, string Confirmation);
public sealed record PhoneOtpResult(bool Succeeded, string? Error = null)
{
    public static PhoneOtpResult Success() => new(true);
    public static PhoneOtpResult Failure(string error) => new(false, error);
}

public interface ISmsOtpSender { Task SendAsync(string normalizedPhoneNumber, string code, CancellationToken ct); }
public interface IPhoneOtpService
{
    Task IssueAsync(UserAccount user, string purpose, string? requestedByIp, CancellationToken ct);
    Task<PhoneOtpResult> ResendAsync(string phoneNumber, string purpose, string? requestedByIp, CancellationToken ct);
    Task<PhoneOtpResult> VerifyRegistrationAsync(string phoneNumber, string code, CancellationToken ct);
    Task RequestPasswordResetAsync(string phoneNumber, string? requestedByIp, CancellationToken ct);
    Task<PhoneOtpResult> ResetPasswordAsync(ResetPasswordPhoneRequest request, string? usedByIp, CancellationToken ct);
}

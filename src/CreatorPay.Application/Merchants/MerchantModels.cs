using CreatorPay.Domain.Enums;

namespace CreatorPay.Application.Merchants;

public sealed record RegisterMerchantRequest(string LegalBusinessName, string TradingName, string BusinessType, string? TaxRegistrationNumber, string PhoneNumber, string Email, string Password, string BusinessAddress, string City, string Region, string Country, string TimeZone, IReadOnlyList<MerchantDocumentMetadata>? Documents, string? PrimaryContactName = null, string? BusinessRegistrationNumber = null, string PreferredLanguage = "en", bool TermsAccepted = false);
public sealed record VerifyMerchantRequest(string Token);
public sealed record MerchantFileMetadata(string FileName, string ContentType, long SizeBytes);
public sealed record MerchantDocumentMetadata(string DocumentType, string FileName, string ContentType, long SizeBytes);
public sealed record UpdateMerchantProfileRequest(string LegalBusinessName, string TradingName, string BusinessType, string? TaxRegistrationNumber, string PhoneNumber, string Email, string BusinessAddress, string City, string Region, string Country, string TimeZone, MerchantFileMetadata? Logo, IReadOnlyList<MerchantDocumentMetadata>? Documents);
public sealed record MerchantDecisionRequest(Guid MerchantId, string? Reason);
public sealed record MerchantRegistrationResponse(Guid MerchantId, string PublicMerchantId, string Message);
public sealed record MerchantProfileResponse(Guid MerchantId, string PublicMerchantId, string LegalBusinessName, string TradingName, string BusinessType, string? TaxRegistrationNumber, string PhoneNumber, string Email, string BusinessAddress, string City, string Region, string Country, string TimeZone, bool IsEmailVerified, bool IsPhoneVerified, AccountStatus AccountStatus, MerchantStatus MerchantStatus, MerchantFileMetadata? Logo, IReadOnlyList<MerchantDocumentMetadata> Documents, int RegistrationProgress, string NextStep);
public sealed record PendingMerchantResponse(Guid MerchantId, string PublicMerchantId, string TradingName, string Email, DateTime RegisteredAtUtc);
public record MerchantResult(bool Succeeded, string? Error = null) { public static MerchantResult Success() => new(true); public static MerchantResult Failure(string error) => new(false, error); }
public sealed record MerchantResult<T>(T? Value, string? Error = null) { public bool Succeeded => Error is null; public static MerchantResult<T> Success(T value) => new(value); public static MerchantResult<T> Failure(string error) => new(default, error); }

public interface IMerchantService
{
    Task<MerchantResult<MerchantRegistrationResponse>> RegisterAsync(RegisterMerchantRequest request, CancellationToken ct);
    Task<MerchantResult> VerifyEmailAsync(string token, CancellationToken ct);
    Task<MerchantResult> VerifyPhoneAsync(string token, CancellationToken ct);
    Task<MerchantResult<MerchantProfileResponse>> GetMeAsync(Guid userId, CancellationToken ct);
    Task<MerchantResult<MerchantProfileResponse>> UpdateMeAsync(Guid userId, UpdateMerchantProfileRequest request, CancellationToken ct);
    Task<IReadOnlyList<PendingMerchantResponse>> GetPendingAsync(CancellationToken ct);
    Task<MerchantResult<MerchantProfileResponse>> GetAsync(Guid merchantId, CancellationToken ct);
    Task<MerchantResult> ApproveAsync(Guid merchantId, Guid adminId, CancellationToken ct);
    Task<MerchantResult> RejectAsync(Guid merchantId, Guid adminId, string? reason, CancellationToken ct);
    Task<MerchantResult> RequestCorrectionAsync(Guid merchantId, Guid adminId, string? reason, CancellationToken ct);
    Task<MerchantResult> SuspendAsync(Guid merchantId, Guid adminId, string? reason, CancellationToken ct);
    Task<MerchantResult> ReactivateAsync(Guid merchantId, Guid adminId, string? reason, CancellationToken ct);
}

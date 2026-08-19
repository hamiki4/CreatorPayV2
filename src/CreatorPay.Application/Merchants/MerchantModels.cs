using System.Linq;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Application.Merchants;

public sealed record RegisterMerchantRequest(string? LegalBusinessName, string TradingName, string BusinessType, string? TaxRegistrationNumber, string PhoneNumber, string? Email, string Password, string BusinessAddress, string City, string Region, string Country, string TimeZone, IReadOnlyList<MerchantDocumentMetadata>? Documents, string? PrimaryContactName = null, string? BusinessRegistrationNumber = null, string PreferredLanguage = "en", bool TermsAccepted = false, string? Confirmation = null);
public sealed record VerifyMerchantRequest(string Token);
public sealed record MerchantFileMetadata(string FileName, string ContentType, long SizeBytes);
public sealed record MerchantDocumentMetadata(string DocumentType, string FileName, string ContentType, long SizeBytes);
public sealed record UpdateMerchantProfileRequest(string LegalBusinessName, string TradingName, string BusinessType, string? TaxRegistrationNumber, string PhoneNumber, string Email, string BusinessAddress, string City, string Region, string Country, string TimeZone, MerchantFileMetadata? Logo, IReadOnlyList<MerchantDocumentMetadata>? Documents);
public sealed record MerchantDecisionRequest(Guid MerchantId, string? Reason);
public sealed record MerchantRegistrationResponse(Guid MerchantId, string PublicMerchantId, string Message, bool PhoneVerificationRequired);
public sealed record MerchantProfileResponse(Guid MerchantId, string PublicMerchantId, string LegalBusinessName, string TradingName, string BusinessType, string PrimaryContactName, string? TaxRegistrationNumber, string PhoneNumber, string Email, string BusinessAddress, string City, string Region, string Country, string TimeZone, bool IsEmailVerified, bool IsPhoneVerified, AccountStatus AccountStatus, MerchantStatus MerchantStatus, MerchantFileMetadata? Logo, IReadOnlyList<MerchantDocumentMetadata> Documents, int RegistrationProgress, string NextStep);
public sealed record PendingMerchantResponse(Guid MerchantId, string PublicMerchantId, string TradingName, string? Email, DateTime RegisteredAtUtc);
public record MerchantResult(bool Succeeded, string? Error = null) { public static MerchantResult Success() => new(true); public static MerchantResult Failure(string error) => new(false, error); }
public sealed record MerchantResult<T>(T? Value, string? Error = null) { public bool Succeeded => Error is null; public static MerchantResult<T> Success(T value) => new(value); public static MerchantResult<T> Failure(string error) => new(default, error); }

public sealed record BusinessTypeOption(string Value, string EnglishLabel, string AmharicLabel);

public static class BusinessTypes
{
    public static readonly BusinessTypeOption[] Options =
    {
        new("Restaurant / Café", "Restaurant / Café", "ምግብ ቤት / ካፌ"),
        new("Grocery / Mini-market", "Grocery / Mini-market", "ግሮሰሪ / ሚኒ ማርኬት"),
        new("Clothing / Boutique", "Clothing / Boutique", "ልብስ / ቡቲክ"),
        new("Beauty / Salon", "Beauty / Salon", "ውበት / ሳሎን"),
        new("Furniture", "Furniture", "የቤት ዕቃ"),
        new("Electronics", "Electronics", "ኤሌክትሮኒክስ"),
        new("Hotel / Travel", "Hotel / Travel", "ሆቴል / ጉዞ"),
        new("Professional Services", "Professional Services", "ሙያዊ አገልግሎቶች"),
        new("Other", "Other", "ሌላ")
    };

    public static readonly string[] Values = Options.Select(x => x.Value).ToArray();
    public static bool IsSupported(string value) => Values.Contains(value, StringComparer.Ordinal);
    public static OfferReuseRule? SuggestedReuseRule(string value) => value switch
    {
        "Restaurant / Café" or "Grocery / Mini-market" => OfferReuseRule.OncePerDay,
        "Beauty / Salon" => OfferReuseRule.OncePerWeek,
        "Hotel / Travel" => OfferReuseRule.OncePerMonth,
        "Clothing / Boutique" or "Furniture" or "Electronics" or "Professional Services" => OfferReuseRule.OncePerOffer,
        _ => null
    };
}

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

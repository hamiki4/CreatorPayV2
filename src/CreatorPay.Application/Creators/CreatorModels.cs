using CreatorPay.Domain.Enums;
using System.Text.Json.Serialization;

namespace CreatorPay.Application.Creators;

public sealed record SocialProfileRequest([property: JsonConverter(typeof(JsonStringEnumConverter))] SocialPlatform Platform, string Handle, string? ProfileUrl, long FollowerCount, bool IsPrimary);
public sealed record RegisterCreatorRequest(string FirstName, string LastName, string DisplayName, string PhoneNumber, string? Email, string Password,
    string PreferredLanguage = "en", string City = "", string? Zone = null, string Biography = "", string ContentCategories = "", bool TermsAccepted = false,
    IReadOnlyList<SocialProfileRequest>? SocialProfiles = null, string? GovernmentIdReference = null, string? TaxIdentificationNumber = null,
    string? PreferredPayoutChannel = null, string? PreferredPayoutAccountIdentifier = null, string? Confirmation = null);
public sealed record VerifyCreatorRequest(string Token);
public sealed record UpdateCreatorProfileRequest(string FirstName, string LastName, string DisplayName, string PhoneNumber, string Email, ProfileImageMetadata? ProfileImage,
    string PreferredLanguage = "en", string City = "", string? Zone = null, string Biography = "", string ContentCategories = "", IReadOnlyList<SocialProfileRequest>? SocialProfiles = null,
    string? PreferredPayoutChannel = null, string? PreferredPayoutAccountIdentifier = null);
public sealed record ProfileImageMetadata(string FileName, string ContentType, long SizeBytes);
public sealed record CreatorDecisionRequest(Guid CreatorId, string? Reason);
public sealed record SocialProfileResponse(Guid Id, SocialPlatform Platform, string Handle, string? ProfileUrl, long FollowerCount, bool IsPrimary, SocialProfileVerificationStatus VerificationStatus, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);
public sealed record CreatorRegistrationResponse(Guid CreatorId, string PublicCreatorId, string Message, bool PhoneVerificationRequired);
public sealed record CreatorProfileResponse(Guid CreatorId, string PublicCreatorId, string CreatorCode, string FirstName, string LastName, string DisplayName, string PhoneNumber, string Email, bool IsEmailVerified, bool IsPhoneVerified, AccountStatus AccountStatus, CreatorStatus CreatorStatus, ProfileImageMetadata? ProfileImage, string NextStep, string PreferredLanguage, string City, string? Zone, string Biography, string ContentCategories, IReadOnlyList<SocialProfileResponse> SocialProfiles, string? PreferredPayoutChannel, string? PreferredPayoutAccountIdentifier);
public sealed record PendingCreatorResponse(Guid CreatorId, string PublicCreatorId, string DisplayName, string Email, DateTime RegisteredAtUtc);

public interface ICreatorService
{
    Task<CreatorResult<CreatorRegistrationResponse>> RegisterAsync(RegisterCreatorRequest request, CancellationToken ct);
    Task<CreatorResult> VerifyEmailAsync(string token, CancellationToken ct);
    Task<CreatorResult> VerifyPhoneAsync(string token, CancellationToken ct);
    Task<CreatorResult<CreatorProfileResponse>> GetMeAsync(Guid userId, CancellationToken ct);
    Task<CreatorResult<CreatorProfileResponse>> UpdateMeAsync(Guid userId, UpdateCreatorProfileRequest request, CancellationToken ct);
    Task<IReadOnlyList<PendingCreatorResponse>> GetPendingAsync(CancellationToken ct);
    Task<CreatorResult<CreatorProfileResponse>> GetAsync(Guid creatorId, CancellationToken ct);
    Task<CreatorResult> ApproveAsync(Guid creatorId, Guid adminId, CancellationToken ct);
    Task<CreatorResult> RejectAsync(Guid creatorId, Guid adminId, string? reason, CancellationToken ct);
    Task<CreatorResult> RequestCorrectionAsync(Guid creatorId, Guid adminId, string? reason, CancellationToken ct);
    Task<CreatorResult> SuspendAsync(Guid creatorId, Guid adminId, string? reason, CancellationToken ct);
    Task<CreatorResult> ReactivateAsync(Guid creatorId, Guid adminId, string? reason, CancellationToken ct);
}

public record CreatorResult(bool Succeeded, string? Error = null)
{
    public static CreatorResult Success() => new(true);
    public static CreatorResult Failure(string error) => new(false, error);
}
public sealed record CreatorResult<T>(T? Value, string? Error = null)
{
    public bool Succeeded => Error is null;
    public static CreatorResult<T> Success(T value) => new(value);
    public static CreatorResult<T> Failure(string error) => new(default, error);
}

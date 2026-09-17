using CreatorPay.Domain.Enums;

namespace CreatorPay.Application.Integration;

public static class V3HandoffPurposes
{
    public const string ProfileOnboarding = "PROFILE_ONBOARDING";
    public const string ExistingWorkspace = "EXISTING_WORKSPACE";
}

public sealed record V3IdentityAssertion(string Issuer, string Environment, string Audience, Guid UserId,
    Guid IdentityBindingId, long IdentityBindingVersion, DateTime AuthenticatedAtUtc, string Purpose,
    string Role, Guid? ProfileSubjectId, Guid? BusinessId, string DisplayName, string AuthorityStatus,
    string DeviceAssurance, string AccountEmail, string? AccountPhone, DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc);
public sealed record V3AuthorityRequest(Guid UserId, Guid IdentityBindingId, long IdentityBindingVersion,
    string Role, Guid? ProfileSubjectId, Guid? BusinessId, string Purpose);
public sealed record V3AuthorityResult(bool Active, string Status, long IdentityBindingVersion);
public sealed record V3ProfileSynchronizationRequest(Guid UserId, Guid IdentityBindingId,
    long IdentityBindingVersion, string Role, Guid ExternalSubjectId, Guid? BusinessId, string DisplayName,
    string Lifecycle, string IdempotencyKey);
public sealed record V3ProfileSynchronizationResult(Guid V3ProfileSubjectId, string Lifecycle, bool Created);

public interface IV3AuthorityClient
{
    Task<V3IdentityAssertion> RedeemAsync(string code, string callbackId, CancellationToken ct);
    Task<V3AuthorityResult> RevalidateAsync(V3AuthorityRequest request, CancellationToken ct);
    Task<V3ProfileSynchronizationResult> SynchronizeAsync(V3ProfileSynchronizationRequest request, CancellationToken ct);
}

public sealed record ExternalCustomerRegistration(string DisplayName);
public sealed record ExternalCreatorSocialProfile(SocialPlatform Platform, string? ProfileUrl,
    long? AudienceCount);
public sealed record ExternalCreatorRegistration(string FirstName, string LastName, string DisplayName,
    string City, string? Zone, string Biography, string ContentCategories,
    IReadOnlyList<ExternalCreatorSocialProfile>? SocialProfiles);
public sealed record ExternalBusinessRegistration(string TradingName, string BusinessType,
    string PrimaryContactName, string? BusinessContactPhone, string? BusinessContactEmail,
    string BusinessAddress, string City, string Region, string Country, string TimeZone);
public sealed record ExternalCreatorSocialProfilesUpdate(
    IReadOnlyList<ExternalCreatorSocialProfile>? SocialProfiles);
public sealed record ExternalCreatorSocialProfileResult(SocialPlatform Platform, string ProfileUrl,
    long AudienceCount, SocialProfileVerificationStatus VerificationStatus, bool IsPrimary);
public sealed record ExternalProvisioningResult(Guid UserAccountId, Guid ProfileId, string Lifecycle,
    string Destination);

public sealed record ExternalSessionSnapshot(Guid SessionId, Guid ExternalIdentityId, Guid? ProfileLinkId,
    Guid? UserAccountId, UserRole Role, string Purpose, bool IsOnboarding, DateTime ExpiresAtUtc);

namespace CreatorPay.Application.Discovery;

public sealed record PromotionDto(Guid CampaignId, string CampaignCode, string MerchantName, string CreatorName, string? ZoneCode, bool Featured, DateTime? ExpiresAtUtc);
public sealed record MerchantDiscoveryDto(Guid MerchantId, string PublicMerchantId, string MerchantName, string? ZoneCode, Guid? FeaturedCreatorId, string FeaturedLabel, IReadOnlyList<PromotionDto> Promotions);
public sealed record UpdateDiscoveryProfileRequest(Guid? FeaturedCreatorId, string? ZoneCode, IReadOnlyDictionary<string, string>? SocialLinks);
public sealed record MerchantDiscoveryQrDto(string PublicQrId, string QrPayload, bool IsActive);

// Deliberately small public contracts: no contact, payout, tax, review, risk, or internal user data.
public sealed record PublicSocialLinkDto(string Platform, string Url);
public sealed record PublicOfferDto(string OfferCode, string Title, string BusinessName, string? BusinessImageUrl, string Description, DateTime ExpiresAtUtc, string CreatorPublicId, string CreatorDisplayName, string CanonicalPath);
public sealed record PublicCreatorCardDto(string PublicCreatorId, string DisplayName, string? ProfileImageUrl, int ActiveOfferCount);
public sealed record PublicCreatorProfileDto(string PublicCreatorId, string DisplayName, string? ProfileImageUrl, IReadOnlyList<PublicSocialLinkDto> SocialLinks, IReadOnlyList<PublicOfferDto> ActiveOffers);
public sealed record CreatorPhotoFile(Stream Content, string ContentType);
public sealed record PublicCreatorSearchDto(IReadOnlyList<PublicCreatorCardDto> Items, int Page, int PageSize, int TotalCount);
public sealed record PromotionVideoSummaryDto(Guid Id, string VideoUrl, string Platform, string Status, DateTime SubmittedAtUtc, DateTime? ReviewedAtUtc, string? RejectionReason);
public sealed record ShopperBusinessDto(Guid BusinessId, string PublicBusinessId, string BusinessName, string? Category, string City, bool RewardsAvailable);
public sealed record ShopperAdvertisingCreatorDto(Guid CreatorId, string PublicCreatorId, string DisplayName, string? SocialPlatform, long? FollowerCount, int DaysLeft, Guid CampaignId, string? CreatorProfileImageUrl = null, PromotionVideoSummaryDto? PromotionVideo = null);
public sealed record ShopperBusinessDetailDto(Guid BusinessId, string PublicBusinessId, string BusinessName, string? Category, string City, bool RewardsAvailable, IReadOnlyList<ShopperAdvertisingCreatorDto> Creators);
public sealed record ShopperAdvertisingRowDto(Guid RelationshipId, Guid BusinessId, string PublicBusinessId, string BusinessName, string City, Guid CreatorId, string PublicCreatorId, string CreatorCode, string CreatorName, string Status, int DaysLeft, bool RewardsAvailable, string? CreatorProfileImageUrl = null, string? BusinessType = null, string? AddressLine1 = null, string? AddressLine2 = null, string? Region = null, string? PromotionVideoUrl = null, string? PromotionVideoPlatform = null, string? PromotionVideoStatus = null, string? PromotionVideoRejectionReason = null, DateTime? PromotionVideoSubmittedAtUtc = null, DateTime? PromotionVideoReviewedAtUtc = null, double? DistanceKm = null);
public sealed record ShopperCreatorQrDto(Guid RelationshipId, string BusinessName, string CreatorName, string Payload);

public interface IDiscoveryService
{
    Task<IReadOnlyList<MerchantDiscoveryDto>> SearchAsync(string? query, string? zone, CancellationToken ct);
    Task<MerchantDiscoveryDto> GetMerchantAsync(Guid merchantId, CancellationToken ct);
    Task<MerchantDiscoveryDto> ResolveStoreQrAsync(string publicQrId, CancellationToken ct);
    Task<MerchantDiscoveryQrDto> IssueOrGetStoreQrAsync(Guid merchantId, CancellationToken ct);
    Task SavePromotionAsync(Guid customerId, Guid campaignId, CancellationToken ct);
    Task RemoveSavedPromotionAsync(Guid customerId, Guid campaignId, CancellationToken ct);
    Task<IReadOnlyList<PromotionDto>> GetSavedAsync(Guid customerId, CancellationToken ct);
    Task<MerchantDiscoveryDto> UpdateProfileAsync(Guid merchantId, UpdateDiscoveryProfileRequest request, CancellationToken ct);
    Task<PublicCreatorSearchDto> SearchCreatorsAsync(string? query, int page, int pageSize, CancellationToken ct);
    Task<PublicCreatorProfileDto> GetCreatorAsync(string publicCreatorId, CancellationToken ct);
    Task<CreatorPhotoFile> GetCreatorPhotoAsync(string publicCreatorId, CancellationToken ct);
    Task<PublicOfferDto> GetOfferAsync(string offerCode, CancellationToken ct);
    Task<PublicOfferDto> ResolveOfferQrAsync(string publicQrId, CancellationToken ct);
    Task<IReadOnlyList<ShopperBusinessDto>> SearchShopperBusinessesAsync(string? query, string? category, double? latitude, double? longitude, CancellationToken ct);
    Task<ShopperBusinessDetailDto> GetShopperBusinessAsync(Guid merchantId, CancellationToken ct);
    Task<IReadOnlyList<ShopperAdvertisingRowDto>> SearchShopperAdvertisingAsync(string? query, string? businessType, double? latitude, double? longitude, CancellationToken ct);
    Task<ShopperCreatorQrDto> GetShopperCreatorQrAsync(Guid relationshipId, CancellationToken ct);
}

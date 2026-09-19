namespace CreatorPay.Application.Ratings;

public sealed record RateCreatorRequest(int Rating);

public sealed record CreatorRatingResult(
    Guid CreatorId,
    int Rating,
    double AverageRating,
    int RatingCount);

public sealed record RateableCreatorDto(
    Guid PartnershipId,
    Guid CreatorId,
    string DisplayName,
    int? ExistingRating);

public sealed record TopCreatorDto(
    Guid CreatorId,
    string PublicCreatorId,
    string DisplayName,
    string? ProfileImageFileName,
    double AverageRating,
    int RatingCount,
    string? PrimaryPlatform,
    int? AudienceCount);

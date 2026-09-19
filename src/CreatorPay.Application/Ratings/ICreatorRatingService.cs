namespace CreatorPay.Application.Ratings;

public interface ICreatorRatingService
{
    Task<CreatorRatingResult> RateCreatorAsync(
        Guid actorUserId,
        Guid partnershipId,
        int rating,
        CancellationToken ct);

    Task<IReadOnlyList<RateableCreatorDto>> GetRateableCreatorsAsync(
        Guid actorUserId,
        CancellationToken ct);

    Task<IReadOnlyList<TopCreatorDto>> GetTopCreatorsAsync(
        CancellationToken ct);
}

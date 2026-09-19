using CreatorPay.Application.Ratings;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPay.Infrastructure.Ratings;

public sealed class CreatorRatingService(ApplicationDbContext db)
    : ICreatorRatingService
{
    public async Task<CreatorRatingResult> RateCreatorAsync(
        Guid actorUserId,
        Guid partnershipId,
        int rating,
        CancellationToken ct)
    {
        if (rating is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(rating),
                "Creator rating must be between 1 and 5.");

        var merchantId = await ResolveMerchantIdAsync(actorUserId, ct);

        var partnership = await db.MerchantCreatorPartnerships
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == partnershipId &&
                     x.MerchantId == merchantId,
                ct)
            ?? throw new InvalidOperationException(
                "Business-Creator partnership was not found.");

        var hasApprovedWork = await db.PromotionVideos
            .AsNoTracking()
            .AnyAsync(
                x => x.MerchantCreatorPartnershipId == partnership.Id &&
                     x.Status == PromotionVideoStatus.Approved,
                ct);

        if (!hasApprovedWork)
            throw new InvalidOperationException(
                "Creator can be rated only after approved promotional work.");

        var now = DateTime.UtcNow;

        var creatorRating = await db.CreatorRatings
            .SingleOrDefaultAsync(
                x => x.MerchantCreatorPartnershipId == partnership.Id,
                ct);

        if (creatorRating is null)
        {
            creatorRating = new CreatorRating
            {
                Id = Guid.NewGuid(),
                MerchantCreatorPartnershipId = partnership.Id,
                MerchantId = partnership.MerchantId,
                CreatorId = partnership.CreatorId
            };

            creatorRating.SetRating(rating, now, actorUserId);
            db.CreatorRatings.Add(creatorRating);
        }
        else
        {
            creatorRating.SetRating(rating, now, actorUserId);
        }

        await db.SaveChangesAsync(ct);

        var aggregate = await db.CreatorRatings
            .AsNoTracking()
            .Where(x => x.CreatorId == partnership.CreatorId)
            .GroupBy(x => x.CreatorId)
            .Select(g => new
            {
                Average = g.Average(x => (double)x.Rating),
                Count = g.Count()
            })
            .SingleAsync(ct);

        return new CreatorRatingResult(
            partnership.CreatorId,
            rating,
            Math.Round(aggregate.Average, 1),
            aggregate.Count);
    }

    public async Task<IReadOnlyList<RateableCreatorDto>>
        GetRateableCreatorsAsync(Guid actorUserId, CancellationToken ct)
    {
        var merchantId = await ResolveMerchantIdAsync(actorUserId, ct);

        return await db.MerchantCreatorPartnerships
            .AsNoTracking()
            .Where(p =>
                p.MerchantId == merchantId &&
                db.PromotionVideos.Any(v =>
                    v.MerchantCreatorPartnershipId == p.Id &&
                    v.Status == PromotionVideoStatus.Approved))
            .Select(p => new RateableCreatorDto(
                p.Id,
                p.CreatorId,
                p.Creator.DisplayName,
                db.CreatorRatings
                    .Where(r => r.MerchantCreatorPartnershipId == p.Id)
                    .Select(r => (int?)r.Rating)
                    .FirstOrDefault()))
            .OrderBy(x => x.DisplayName)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TopCreatorDto>>
        GetTopCreatorsAsync(CancellationToken ct)
    {
        var ratings = await db.CreatorRatings
            .AsNoTracking()
            .GroupBy(r => r.CreatorId)
            .Select(g => new
            {
                CreatorId = g.Key,
                Average = g.Average(x => (double)x.Rating),
                Count = g.Count()
            })
            .ToListAsync(ct);

        if (ratings.Count == 0)
            return [];

        var ranked = ratings
            .Select(x => new
            {
                x.CreatorId,
                x.Average,
                x.Count,
                Score = (x.Average * x.Count + 3.5 * 3) /
                        (x.Count + 3)
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Count)
            .ThenByDescending(x => x.Average)
            .ThenBy(x => x.CreatorId)
            .Take(10)
            .ToList();

        var ids = ranked.Select(x => x.CreatorId).ToArray();

        var creators = await db.Creators
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new
            {
                c.Id,
                c.PublicCreatorId,
                c.DisplayName,
                c.ProfileImageFileName,
                PrimarySocial = c.SocialProfiles
                    .OrderByDescending(s => s.IsPrimary)
                    .ThenByDescending(s => s.FollowerCount)
                    .Select(s => new
                    {
                        Platform = s.Platform.ToString(),
                        s.FollowerCount
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var creatorMap = creators.ToDictionary(x => x.Id);

        return ranked
            .Where(x => creatorMap.ContainsKey(x.CreatorId))
            .Select(x =>
            {
                var creator = creatorMap[x.CreatorId];

                return new TopCreatorDto(
                    creator.Id,
                    creator.PublicCreatorId,
                    creator.DisplayName,
                    creator.ProfileImageFileName,
                    Math.Round(x.Average, 1),
                    x.Count,
                    creator.PrimarySocial?.Platform,
                    creator.PrimarySocial is null
                        ? null
                        : (int?)Math.Min(
                            creator.PrimarySocial.FollowerCount,
                            int.MaxValue));
            })
            .ToList();
    }

    private async Task<Guid> ResolveMerchantIdAsync(
        Guid actorUserId,
        CancellationToken ct)
    {
        var merchantId = await db.UserAccounts
            .AsNoTracking()
            .Where(x =>
                x.Id == actorUserId &&
                x.MerchantId != null)
            .Select(x => x.MerchantId)
            .SingleOrDefaultAsync(ct);

        return merchantId
            ?? throw new UnauthorizedAccessException(
                "Authenticated Business account is required.");
    }
}

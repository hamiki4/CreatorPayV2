using CreatorPay.Domain.Common;

namespace CreatorPay.Domain.Entities;

public sealed class CreatorRating : Entity
{
    public Guid MerchantCreatorPartnershipId { get; set; }
    public Guid MerchantId { get; set; }
    public Guid CreatorId { get; set; }

    public int Rating { get; private set; }

    public Guid CreatedByUserId { get; set; }

    public Guid? UpdatedByUserId { get; private set; }

    public MerchantCreatorPartnership Partnership { get; set; } = null!;
    public Merchant Merchant { get; set; } = null!;
    public Creator Creator { get; set; } = null!;

    public void SetRating(int rating, DateTime utcNow, Guid actorUserId)
    {
        if (rating is < 1 or > 5)
            throw new ArgumentOutOfRangeException(
                nameof(rating),
                "Creator rating must be between 1 and 5.");

        if (utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException(
                "Timestamp must be UTC.",
                nameof(utcNow));

        Rating = rating;

        if (CreatedAtUtc == default)
        {
            CreatedAtUtc = utcNow;
            CreatedByUserId = actorUserId;
            return;
        }

        UpdatedAtUtc = utcNow;
        UpdatedByUserId = actorUserId;
    }
}

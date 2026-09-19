using CreatorPay.Application.Authentication;
using CreatorPay.Application.Ratings;

namespace CreatorPay.Api.Ratings;

public static class CreatorRatingEndpoints
{
    public static IEndpointRouteBuilder MapCreatorRatingEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/business/creator-ratings")
            .RequireAuthorization();

        group.MapGet("/eligible", async (
            ICurrentUserService currentUser,
            ICreatorRatingService ratings,
            CancellationToken ct) =>
        {
            if (currentUser.UserAccountId is null)
                return Results.Unauthorized();

            try
            {
                var result = await ratings.GetRateableCreatorsAsync(
                    currentUser.UserAccountId.Value, ct);

                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        });

        group.MapPut("/{partnershipId:guid}", async (
            Guid partnershipId,
            RateCreatorRequest request,
            ICurrentUserService currentUser,
            ICreatorRatingService ratings,
            CancellationToken ct) =>
        {
            if (currentUser.UserAccountId is null)
                return Results.Unauthorized();

            if (request.Rating is < 1 or > 5)
                return Results.BadRequest(new
                {
                    detail = "Rating must be between 1 and 5."
                });

            try
            {
                var result = await ratings.RateCreatorAsync(
                    currentUser.UserAccountId.Value,
                    partnershipId,
                    request.Rating,
                    ct);

                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { detail = ex.Message });
            }
        });

        endpoints.MapGet("/api/business/top-creators", async (
            ICreatorRatingService ratings,
            CancellationToken ct) =>
        {
            var result = await ratings.GetTopCreatorsAsync(ct);
            return Results.Ok(result);
        }).RequireAuthorization();

        return endpoints;
    }
}

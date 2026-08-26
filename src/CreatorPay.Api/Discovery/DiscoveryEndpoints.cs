using CreatorPay.Application.Authentication;
using CreatorPay.Application.Discovery;
using CreatorPay.Application.Qr;
namespace CreatorPay.Api.Discovery;

public static class DiscoveryEndpoints
{
    public static IEndpointRouteBuilder MapDiscoveryEndpoints(this IEndpointRouteBuilder e)
    {
        var creators = e.MapGroup("/api/v1/discovery/creators");
        creators.MapGet("/", (string? q, int? page, int? pageSize, IDiscoveryService s, CancellationToken ct) => Run(() => s.SearchCreatorsAsync(q, page ?? 1, pageSize ?? 12, ct)));
        creators.MapGet("/{publicCreatorId}", (string publicCreatorId, IDiscoveryService s, CancellationToken ct) => Run(() => s.GetCreatorAsync(publicCreatorId, ct)));
        creators.MapGet("/{publicCreatorId}/photo", async (string publicCreatorId, HttpContext http, IDiscoveryService s, CancellationToken ct) =>
        {
            try
            {
                var photo = await s.GetCreatorPhotoAsync(publicCreatorId, ct);
                http.Response.Headers["Cross-Origin-Resource-Policy"] = "cross-origin";
                return Results.File(photo.Content, photo.ContentType);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.Problem(statusCode: 404, detail: ex.Message);
            }
        });
        e.MapGet("/api/v1/discovery/offers/{offerCode}", (string offerCode, IDiscoveryService s, CancellationToken ct) => Run(() => s.GetOfferAsync(offerCode, ct)));
        e.MapGet("/api/v1/discovery/offer-qr/{publicQrId}", (string publicQrId, IDiscoveryService s, CancellationToken ct) => Run(() => s.ResolveOfferQrAsync(publicQrId, ct)));
        var d = e.MapGroup("/api/v1/discovery"); d.MapGet("/merchants", (string? q, string? zone, IDiscoveryService s, CancellationToken ct) => s.SearchAsync(q, zone, ct)); d.MapGet("/merchants/{id:guid}", (Guid id, IDiscoveryService s, CancellationToken ct) => s.GetMerchantAsync(id, ct)); d.MapGet("/merchant-qr/{id}", (string id, IDiscoveryService s, CancellationToken ct) => s.ResolveStoreQrAsync(id, ct));
        var c = e.MapGroup("/api/v1/customer/saved-promotions").RequireAuthorization("CustomerOnly"); c.MapGet("/", (ICurrentUserService u, IDiscoveryService s, CancellationToken ct) => s.GetSavedAsync(u.CustomerId!.Value, ct)); c.MapPost("/{id:guid}", (Guid id, ICurrentUserService u, IDiscoveryService s, CancellationToken ct) => s.SavePromotionAsync(u.CustomerId!.Value, id, ct)); c.MapDelete("/{id:guid}", (Guid id, ICurrentUserService u, IDiscoveryService s, CancellationToken ct) => s.RemoveSavedPromotionAsync(u.CustomerId!.Value, id, ct));
        var shopper = e.MapGroup("/api/v1/customer/discovery/businesses").RequireAuthorization("CustomerOnly");
        shopper.MapGet("/", (string? q, string? category, IDiscoveryService s, CancellationToken ct) => Run(() => s.SearchShopperBusinessesAsync(q, category, ct)));
        shopper.MapGet("/{id:guid}", (Guid id, IDiscoveryService s, CancellationToken ct) => Run(() => s.GetShopperBusinessAsync(id, ct)));
        e.MapGet("/api/v1/customer/discovery/advertising", (string? q, IDiscoveryService s, CancellationToken ct) => Run(() => s.SearchShopperAdvertisingAsync(q, ct))).RequireAuthorization("CustomerOnly");
        e.MapGet("/api/v1/customer/discovery/advertising/{id:guid}/qr-image", async (Guid id, IDiscoveryService s, IQrImageGenerator images, CancellationToken ct) =>
        {
            try { var qr = await s.GetShopperCreatorQrAsync(id, ct); return Results.File(images.GeneratePng(qr.Payload), "image/png", $"weymela-{qr.CreatorName}-qr.png"); }
            catch (KeyNotFoundException ex) { return Results.Problem(statusCode: 404, detail: ex.Message); }
        }).RequireAuthorization("CustomerOnly");
        e.MapGet("/api/v1/merchant/discovery-qr", (ICurrentUserService u, IDiscoveryService s, CancellationToken ct) => s.IssueOrGetStoreQrAsync(u.MerchantId!.Value, ct)).RequireAuthorization("MerchantAdminOnly"); e.MapPut("/api/v1/merchant/discovery-profile", (UpdateDiscoveryProfileRequest r, ICurrentUserService u, IDiscoveryService s, CancellationToken ct) => s.UpdateProfileAsync(u.MerchantId!.Value, r, ct)).RequireAuthorization("MerchantAdminOnly"); return e;
    }

    private static async Task<IResult> Run<T>(Func<Task<T>> action)
    {
        try { return Results.Ok(await action()); }
        catch (KeyNotFoundException ex) { return Results.Problem(statusCode: 404, detail: ex.Message); }
        catch (ArgumentOutOfRangeException ex) { return Results.Problem(statusCode: 400, detail: ex.Message); }
    }
}

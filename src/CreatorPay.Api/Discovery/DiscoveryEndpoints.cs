using CreatorPay.Application.Authentication;
using CreatorPay.Application.Discovery;
namespace CreatorPay.Api.Discovery;

public static class DiscoveryEndpoints
{
    public static IEndpointRouteBuilder MapDiscoveryEndpoints(this IEndpointRouteBuilder e)
    {
        var d = e.MapGroup("/api/v1/discovery"); d.MapGet("/merchants", (string? q, string? zone, IDiscoveryService s, CancellationToken ct) => s.SearchAsync(q, zone, ct)); d.MapGet("/merchants/{id:guid}", (Guid id, IDiscoveryService s, CancellationToken ct) => s.GetMerchantAsync(id, ct)); d.MapGet("/merchant-qr/{id}", (string id, IDiscoveryService s, CancellationToken ct) => s.ResolveStoreQrAsync(id, ct));
        var c = e.MapGroup("/api/v1/customer/saved-promotions").RequireAuthorization("CustomerOnly"); c.MapGet("/", (ICurrentUserService u, IDiscoveryService s, CancellationToken ct) => s.GetSavedAsync(u.CustomerId!.Value, ct)); c.MapPost("/{id:guid}", (Guid id, ICurrentUserService u, IDiscoveryService s, CancellationToken ct) => s.SavePromotionAsync(u.CustomerId!.Value, id, ct)); c.MapDelete("/{id:guid}", (Guid id, ICurrentUserService u, IDiscoveryService s, CancellationToken ct) => s.RemoveSavedPromotionAsync(u.CustomerId!.Value, id, ct));
        e.MapGet("/api/v1/merchant/discovery-qr", (ICurrentUserService u, IDiscoveryService s, CancellationToken ct) => s.IssueOrGetStoreQrAsync(u.MerchantId!.Value, ct)).RequireAuthorization("MerchantAdminOnly"); e.MapPut("/api/v1/merchant/discovery-profile", (UpdateDiscoveryProfileRequest r, ICurrentUserService u, IDiscoveryService s, CancellationToken ct) => s.UpdateProfileAsync(u.MerchantId!.Value, r, ct)).RequireAuthorization("MerchantAdminOnly"); return e;
    }
}

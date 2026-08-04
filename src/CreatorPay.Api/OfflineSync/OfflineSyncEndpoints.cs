using CreatorPay.Application.Authentication;
using CreatorPay.Application.OfflineSync;
namespace CreatorPay.Api.OfflineSync;

public static class OfflineSyncEndpoints
{
    public static IEndpointRouteBuilder MapOfflineSyncEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/cashier/offline-sync", () => Results.Problem(statusCode: StatusCodes.Status410Gone, title: "Legacy purchase flow disabled", detail: "Offline campaign-QR purchases are disabled. Cashiers must scan a temporary checkout QR."))
            .RequireAuthorization("CashierOnly")
            .RequireRateLimiting("financial-sensitive")
            .WithName("SynchronizeOfflineCashierPurchases");
        return endpoints;
    }
}

using CreatorPay.Application.Authentication; using CreatorPay.Application.OfflineSync; using CreatorPay.Infrastructure.OfflineSync;
namespace CreatorPay.Api.OfflineSync;
public static class OfflineSyncEndpoints
{
 public static IEndpointRouteBuilder MapOfflineSyncEndpoints(this IEndpointRouteBuilder endpoints)
 {
  endpoints.MapPost("/api/v1/cashier/offline-sync",async(OfflineSyncRequest request,ICurrentUserService user,IOfflineSyncService service,HttpContext context,CancellationToken ct)=>{try{return Results.Ok(await service.SynchronizeAsync(user.MerchantId!.Value,user.UserAccountId!.Value,user.CashierId!.Value,user.Role!,context.TraceIdentifier,request,ct));}catch(UnsupportedOfflineSchemaException x){return Results.Problem(statusCode:422,title:"UnsupportedSchemaVersion",detail:x.Message);}catch(UnauthorizedAccessException x){return Results.Problem(statusCode:403,title:"ScopeDenied",detail:x.Message);}catch(ArgumentException x){return Results.Problem(statusCode:400,title:"Invalid offline sync batch",detail:x.Message);}}).RequireAuthorization("CashierOnly").RequireRateLimiting("financial-sensitive").WithName("SynchronizeOfflineCashierPurchases");
  return endpoints;
 }
}

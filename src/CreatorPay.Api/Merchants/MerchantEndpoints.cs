using CreatorPay.Api.Authentication;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.Merchants;

namespace CreatorPay.Api.Merchants;

public static class MerchantEndpoints
{
    public static IEndpointRouteBuilder MapMerchantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var merchants=endpoints.MapGroup("/api/v1/merchants").WithTags("Merchants");
        merchants.MapPost("/register",async(RegisterMerchantRequest r,IMerchantService s,CancellationToken ct)=>ToHttp(await s.RegisterAsync(r,ct),201)).RequireRateLimiting("auth-sensitive");
        merchants.MapPost("/verify-email",async(VerifyMerchantRequest r,IMerchantService s,CancellationToken ct)=>ToHttp(await s.VerifyEmailAsync(r.Token,ct))).RequireRateLimiting("auth-sensitive");
        merchants.MapPost("/verify-phone",async(VerifyMerchantRequest r,IMerchantService s,CancellationToken ct)=>ToHttp(await s.VerifyPhoneAsync(r.Token,ct))).RequireRateLimiting("auth-sensitive");
        merchants.MapGet("/me",async(ICurrentUserService u,IMerchantService s,CancellationToken ct)=>ToHttp(await s.GetMeAsync(u.UserAccountId!.Value,ct))).RequireAuthorization("MerchantAdminOnly");
        merchants.MapPut("/me",async(UpdateMerchantProfileRequest r,ICurrentUserService u,IMerchantService s,CancellationToken ct)=>ToHttp(await s.UpdateMeAsync(u.UserAccountId!.Value,r,ct))).RequireAuthorization("MerchantAdminOnly");
        var admin=endpoints.MapGroup("/api/v1/admin/merchants").WithTags("Merchant approval").RequireAuthorization("PlatformAdminOnly");
        admin.MapGet("/pending",async(IMerchantService s,CancellationToken ct)=>Results.Ok(await s.GetPendingAsync(ct)));
        admin.MapGet("/{merchantId:guid}",async(Guid merchantId,IMerchantService s,CancellationToken ct)=>ToHttp(await s.GetAsync(merchantId,ct)));
        admin.MapPost("/approve",async(MerchantDecisionRequest r,ICurrentUserService u,IMerchantService s,CancellationToken ct)=>ToHttp(await s.ApproveAsync(r.MerchantId,u.UserAccountId!.Value,ct)));
        admin.MapPost("/reject",async(MerchantDecisionRequest r,ICurrentUserService u,IMerchantService s,CancellationToken ct)=>ToHttp(await s.RejectAsync(r.MerchantId,u.UserAccountId!.Value,r.Reason,ct)));
        admin.MapPost("/suspend",async(MerchantDecisionRequest r,ICurrentUserService u,IMerchantService s,CancellationToken ct)=>ToHttp(await s.SuspendAsync(r.MerchantId,u.UserAccountId!.Value,r.Reason,ct)));
        admin.MapPost("/reactivate",async(MerchantDecisionRequest r,ICurrentUserService u,IMerchantService s,CancellationToken ct)=>ToHttp(await s.ReactivateAsync(r.MerchantId,u.UserAccountId!.Value,r.Reason,ct)));
        return endpoints;
    }
    private static IResult ToHttp(MerchantResult r)=>r.Succeeded?Results.Ok(new{succeeded=true}):Problem(r.Error!);private static IResult ToHttp<T>(MerchantResult<T> r,int status=200)=>r.Succeeded?Results.Json(r.Value,statusCode:status):Problem(r.Error!);private static IResult Problem(string d)=>Results.Problem(d,statusCode:400,title:"Merchant request failed");
}

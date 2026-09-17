using CreatorPay.Api.Authentication;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.Merchants;
using CreatorPay.Domain.Entities;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CreatorPay.Infrastructure.Integration;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Api.Merchants;

public static class MerchantEndpoints
{
    public static IEndpointRouteBuilder MapMerchantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var merchants = endpoints.MapGroup("/api/v1/merchants").WithTags("Merchants");
        merchants.MapPost("/register", async (RegisterMerchantRequest r, IMerchantService s, CancellationToken ct) => ToHttp(await s.RegisterAsync(r, ct), 201)).RequireRateLimiting("auth-sensitive");
        merchants.MapGet("/business-types", () => Results.Ok(BusinessTypes.Options));
        merchants.MapPost("/verify-email", async (VerifyMerchantRequest r, IMerchantService s, CancellationToken ct) => ToHttp(await s.VerifyEmailAsync(r.Token, ct))).RequireRateLimiting("auth-sensitive");
        merchants.MapPost("/verify-phone", async (VerifyMerchantRequest r, IMerchantService s, CancellationToken ct) => ToHttp(await s.VerifyPhoneAsync(r.Token, ct))).RequireRateLimiting("auth-sensitive");
        merchants.MapGet("/me", async (ICurrentUserService u, IMerchantService s, CancellationToken ct) => ToHttp(await s.GetMeAsync(u.UserAccountId!.Value, ct))).RequireAuthorization("MerchantOnboarding");
        merchants.MapPut("/me", async (UpdateMerchantProfileRequest r, ICurrentUserService u, IMerchantService s, CancellationToken ct) => ToHttp(await s.UpdateMeAsync(u.UserAccountId!.Value, r, ct))).RequireAuthorization("MerchantOnboarding");
        var admin = endpoints.MapGroup("/api/v1/admin/merchants").WithTags("Merchant approval").RequireAuthorization("AdminOperationsOnly");
        admin.MapGet("/pending", async (IMerchantService s, CancellationToken ct) => Results.Ok(await s.GetPendingAsync(ct)));
        admin.MapGet("/{merchantId:guid}", async (Guid merchantId, IMerchantService s, CancellationToken ct) => ToHttp(await s.GetAsync(merchantId, ct)));
        admin.MapPut("/{merchantId:guid}/business-type", async (Guid merchantId, BusinessTypeRequest request, ICurrentUserService u, ApplicationDbContext db, CancellationToken ct) => await UpdateBusinessType(merchantId, request, u, db, ct)).RequireAuthorization("PlatformAdminOnly");
        admin.MapPost("/approve", async (MerchantDecisionRequest r, ICurrentUserService u, IMerchantService s, ExternalIntegrationService integration, CancellationToken ct) =>
        { var result = await s.ApproveAsync(r.MerchantId, u.UserAccountId!.Value, ct); var synchronized = await integration.SynchronizeLifecycleAsync(UserRole.MerchantAdmin, r.MerchantId, "ACTIVE", ct); return synchronized && !result.Succeeded ? Results.Ok(new { succeeded = true }) : ToHttp(result); });
        admin.MapPost("/reject", async (MerchantDecisionRequest r, ICurrentUserService u, IMerchantService s, ExternalIntegrationService integration, CancellationToken ct) =>
        { var result = await s.RejectAsync(r.MerchantId, u.UserAccountId!.Value, r.Reason, ct); var synchronized = await integration.SynchronizeLifecycleAsync(UserRole.MerchantAdmin, r.MerchantId, "REJECTED", ct); return synchronized && !result.Succeeded ? Results.Ok(new { succeeded = true }) : ToHttp(result); });
        admin.MapPost("/request-correction", async (MerchantDecisionRequest r, ICurrentUserService u, IMerchantService s, CancellationToken ct) => ToHttp(await s.RequestCorrectionAsync(r.MerchantId, u.UserAccountId!.Value, r.Reason, ct)));
        admin.MapPost("/suspend", async (MerchantDecisionRequest r, ICurrentUserService u, IMerchantService s, CancellationToken ct) => ToHttp(await s.SuspendAsync(r.MerchantId, u.UserAccountId!.Value, r.Reason, ct)));
        admin.MapPost("/reactivate", async (MerchantDecisionRequest r, ICurrentUserService u, IMerchantService s, CancellationToken ct) => ToHttp(await s.ReactivateAsync(r.MerchantId, u.UserAccountId!.Value, r.Reason, ct)));
        return endpoints;
    }
    public sealed record BusinessTypeRequest(string BusinessType);
    private static async Task<IResult> UpdateBusinessType(Guid merchantId, BusinessTypeRequest request, ICurrentUserService u, ApplicationDbContext db, CancellationToken ct)
    {
        if (u.UserAccountId is null) return Results.Problem(statusCode: 401, detail: "Authentication required.");
        if (string.IsNullOrWhiteSpace(request.BusinessType)) return Results.BadRequest(new { detail = "Business type is required." });
        var businessType = request.BusinessType.Trim();
        if (!BusinessTypes.IsSupported(businessType)) return Results.BadRequest(new { detail = "Select a supported business type." });
        var merchant = await db.Merchants.SingleOrDefaultAsync(x => x.Id == merchantId, ct);
        if (merchant is null) return Results.NotFound(new { detail = "Business not found." });
        var now = DateTime.UtcNow;
        var before = merchant.BusinessType;
        if (string.Equals(before, businessType, StringComparison.Ordinal)) return Results.Ok(new { businessType = merchant.BusinessType });
        merchant.BusinessType = businessType;
        merchant.UpdatedAtUtc = now;
        merchant.UpdatedBy = u.UserAccountId.Value.ToString();
        db.MerchantAuditEvents.Add(new MerchantAuditEvent
        {
            Id = Guid.NewGuid(),
            MerchantId = merchant.Id,
            ActorUserAccountId = u.UserAccountId,
            EventType = "BusinessTypeChanged",
            Detail = $"Before={before};After={businessType}",
            CreatedAtUtc = now,
            CreatedBy = u.UserAccountId.ToString()
        });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { businessType = merchant.BusinessType });
    }
    private static IResult ToHttp(MerchantResult r) => r.Succeeded ? Results.Ok(new { succeeded = true }) : Problem(r.Error!); private static IResult ToHttp<T>(MerchantResult<T> r, int status = 200) => r.Succeeded ? Results.Json(r.Value, statusCode: status) : Problem(r.Error!); private static IResult Problem(string d) => Results.Problem(d, statusCode: d.Contains("already registered", StringComparison.OrdinalIgnoreCase) ? 409 : 400, title: "Merchant request failed");
}

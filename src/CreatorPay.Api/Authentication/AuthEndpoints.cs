using CreatorPay.Application.Authentication;
using CreatorPay.Application.CustomerVerification;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace CreatorPay.Api.Authentication;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth").WithTags("Authentication");
        group.MapPost("/login", async (LoginRequest r, HttpContext h, IAuthenticationService s, CancellationToken ct) => ToHttp(await s.LoginAsync(r, Context(h), ct))).RequireRateLimiting("auth-sensitive");
        group.MapPost("/refresh", async (RefreshRequest r, HttpContext h, IAuthenticationService s, CancellationToken ct) => ToHttp(await s.RefreshAsync(r.RefreshToken, Context(h), ct))).RequireRateLimiting("auth-sensitive");
        group.MapPost("/logout", async (TokenRequest r, HttpContext h, IAuthenticationService s, CancellationToken ct) => ToHttp(await s.LogoutAsync(r.RefreshToken, Context(h), ct)));
        group.MapPost("/logout-all", async (HttpContext h, ICurrentUserService u, IAuthenticationService s, CancellationToken ct) => ToHttp(await s.LogoutAllAsync(u.UserAccountId!.Value, Context(h), ct))).RequireAuthorization("AuthenticatedUser");
        group.MapGet("/me", async (ICurrentUserService u, IAuthenticationService s, CancellationToken ct) => ToHttp(await s.GetCurrentUserAsync(u.UserAccountId!.Value, ct))).RequireAuthorization("AuthenticatedUser");
        group.MapPost("/change-password", async (ChangePasswordRequest r, HttpContext h, ICurrentUserService u, IAuthenticationService s, CancellationToken ct) => ToHttp(await s.ChangePasswordAsync(u.UserAccountId!.Value, r, Context(h), ct))).RequireAuthorization("AuthenticatedUser");
        group.MapPost("/password-reset-requests", RequestPasswordReset).RequireRateLimiting("auth-sensitive");
        group.MapGet("/password-reset-requests/{reference}", PasswordResetStatus).RequireRateLimiting("auth-sensitive");
        group.MapPost("/reset-password", CompletePasswordReset).RequireRateLimiting("auth-sensitive");
        group.MapPost("/verify-phone", async (VerifyPhoneOtpRequest r, IPhoneOtpService s, CancellationToken ct) => ToHttp(await s.VerifyRegistrationAsync(r.PhoneNumber, r.Code, ct))).RequireRateLimiting("auth-sensitive");
        group.MapPost("/resend-phone-code", async (ResendPhoneOtpRequest r, HttpContext h, IPhoneOtpService s, CancellationToken ct) => ToHttp(await s.ResendAsync(r.PhoneNumber, "Registration", h.Connection.RemoteIpAddress?.ToString(), ct))).RequireRateLimiting("auth-sensitive");
        group.MapPost("/forgot-password-phone", async (ForgotPasswordPhoneRequest r, HttpContext h, IPhoneOtpService s, CancellationToken ct) => { await s.RequestPasswordResetAsync(r.PhoneNumber, h.Connection.RemoteIpAddress?.ToString(), ct); return Results.Ok(new { message = "If this phone number is registered, a verification code has been sent." }); }).RequireRateLimiting("auth-sensitive");
        group.MapPost("/reset-password-phone", async (ResetPasswordPhoneRequest r, HttpContext h, IPhoneOtpService s, CancellationToken ct) => ToHttp(await s.ResetPasswordAsync(r, h.Connection.RemoteIpAddress?.ToString(), ct))).RequireRateLimiting("auth-sensitive");
        group.MapGet("/signup-settings", async (ApplicationDbContext db, CancellationToken ct) => Results.Ok(new { minimumTikTokFollowers = await db.PlatformFinancialSettings.AsNoTracking().Where(x => x.CurrencyCode == "ETB").Select(x => (long?)x.MinimumTikTokFollowers).SingleOrDefaultAsync(ct) ?? 0L }));
        var pin = group.MapGroup("/pin");
        pin.MapGet("/status", async (ICurrentUserService u, IAuthenticationService s, CancellationToken ct) => ToHttp(await s.GetPinStatusAsync(u.UserAccountId!.Value, ct))).RequireAuthorization("AuthenticatedUser");
        pin.MapPost("/link-firebase", async (LinkFirebaseRequest r, HttpContext h, ICurrentUserService u, IAuthenticationService s, CancellationToken ct) => ToHttp(await s.LinkFirebaseAsync(u.UserAccountId!.Value, r, Context(h), ct))).RequireAuthorization("AuthenticatedUser").RequireRateLimiting("auth-sensitive");
        pin.MapPost("/enroll", async (PinRequest r, HttpContext h, ICurrentUserService u, IAuthenticationService s, CancellationToken ct) => ToHttp(await s.EnrollPinAsync(u.UserAccountId!.Value, r, Context(h), ct))).RequireAuthorization("AuthenticatedUser").RequireRateLimiting("auth-sensitive");
        pin.MapPost("/unlock", async (PinUnlockRequest r, HttpContext h, IAuthenticationService s, CancellationToken ct) => ToHttp(await s.UnlockWithPinAsync(r, Context(h), ct))).RequireRateLimiting("auth-sensitive");
        pin.MapPost("/reset-with-password", async (PasswordPinResetRequest r, HttpContext h, IAuthenticationService s, CancellationToken ct) => ToHttp(await s.ResetPinWithPasswordAsync(r, Context(h), ct))).RequireRateLimiting("auth-sensitive");
        return endpoints;
    }

    private sealed record PasswordResetHelpRequest(string PhoneNumber);

    private static async Task<IResult> RequestPasswordReset(PasswordResetHelpRequest request, HttpContext h, ApplicationDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        string normalized;
        try { normalized = EthiopianMobileNumber.Normalize(request.PhoneNumber); }
        catch (ArgumentException) { normalized = string.Empty; }
        var user = normalized.Length == 0 ? null : await db.UserAccounts.SingleOrDefaultAsync(x => x.NormalizedPhoneNumber == normalized, ct);
        var eligible = user is not null && user.Role != UserRole.PlatformAdmin && AuthenticationService.CanSignIn(user);
        var reference = $"PWR-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..25].ToUpperInvariant();
        db.LoginAudits.Add(new LoginAudit { Id = Guid.NewGuid(), UserAccountId = eligible ? user!.Id : null, NormalizedEmail = normalized, WasSuccessful = eligible, FailureReason = eligible ? "PasswordResetHelpRequested" : "PasswordResetHelpDetailsMismatch", IpAddress = h.Connection.RemoteIpAddress?.ToString(), UserAgent = h.Request.Headers.UserAgent.ToString(), CorrelationId = h.TraceIdentifier, AttemptedAtUtc = now, CreatedAtUtc = now });
        if (eligible)
        {
            var existing = await db.SupportRequests.SingleOrDefaultAsync(x => x.Subject == "Password Reset" && x.Contact == normalized && x.Status == "Pending", ct);
            if (existing is not null) reference = existing.PublicReference;
            else db.SupportRequests.Add(new SupportRequest { Id = Guid.NewGuid(), PublicReference = reference, Name = await DisplayName(user!, db, ct), Contact = normalized, UserType = user!.Role.ToString(), Subject = "Password Reset", Message = user.Id.ToString(), PreferredLanguage = "en", ConsentAcknowledged = true, Status = "Pending", CreatedAtUtc = now });
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { reference, status = "Pending", message = "Your password reset request is waiting for support approval." });
    }

    private static async Task<IResult> PasswordResetStatus(string reference, ApplicationDbContext db, CancellationToken ct)
    {
        var request = await db.SupportRequests.AsNoTracking().SingleOrDefaultAsync(x => x.PublicReference == reference && x.Subject == "Password Reset", ct);
        var status = request?.Status == "Approved" ? "Approved" : request?.Status == "Completed" ? "Completed" : request?.Status == "Rejected" ? "Rejected" : "Pending";
        return Results.Ok(new { status, message = status == "Approved" ? "Your request was approved. Create a new password." : status == "Completed" ? "Password reset completed." : status == "Rejected" ? "Please contact Weymela Support for help." : "Your password reset request is waiting for support approval." });
    }

    private static async Task<IResult> CompletePasswordReset(ResetPasswordRequest request, HttpContext h, IAuthenticationService service, ApplicationDbContext db, CancellationToken ct)
    {
        var result = await service.ResetPasswordAsync(request, Context(h), ct);
        if (result.Succeeded)
        {
            var support = await db.SupportRequests.SingleOrDefaultAsync(x => x.PublicReference == request.ResetToken && x.Subject == "Password Reset" && x.Status == "Approved", ct);
            if (support is not null) { support.Status = "Completed"; support.UpdatedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(ct); }
        }
        return ToHttp(result);
    }

    private static async Task<string> DisplayName(UserAccount user, ApplicationDbContext db, CancellationToken ct)
    {
        if (user.CustomerId.HasValue) return await db.Customers.Where(x => x.Id == user.CustomerId).Select(x => x.DisplayName).SingleOrDefaultAsync(ct) ?? "Shopper";
        if (user.CreatorId.HasValue) return await db.Creators.Where(x => x.Id == user.CreatorId).Select(x => x.DisplayName).SingleOrDefaultAsync(ct) ?? "Creator";
        if (user.MerchantId.HasValue) return await db.Merchants.Where(x => x.Id == user.MerchantId).Select(x => x.PrimaryContactName).SingleOrDefaultAsync(ct) ?? "Business Owner";
        if (user.CashierId.HasValue) return await db.Cashiers.Where(x => x.Id == user.CashierId).Select(x => (x.FirstName + " " + x.LastName).Trim()).SingleOrDefaultAsync(ct) ?? "Cashier";
        return "Weymela User";
    }
    private static RequestContext Context(HttpContext h) => new(h.Connection.RemoteIpAddress?.ToString(), h.Request.Headers.UserAgent.ToString(), h.TraceIdentifier);
    private static IResult ToHttp<T>(Result<T> result) => result.Succeeded ? Results.Ok(result.Value) : Problem(result.Error!, result.Code);
    private static IResult ToHttp(OperationResult result) => result.Succeeded ? Results.Ok(new { succeeded = true }) : Problem(result.Error!, result.Code);
    private static IResult ToHttp(PhoneOtpResult result) => result.Succeeded ? Results.Ok(new { succeeded = true }) : Problem(result.Error!);
    private static IResult Problem(string detail, string? code = null) => code is null
        ? Results.Problem(detail, statusCode: StatusCodes.Status400BadRequest, title: "Authentication request failed")
        : Results.Problem(detail, statusCode: StatusCodes.Status403Forbidden, title: code);
}

using CreatorPay.Application.Authentication;
using CreatorPay.Application.CustomerVerification;
using CreatorPay.Application.Notifications;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

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

    private sealed record PasswordResetHelpRequest(string? PhoneNumber, string? Email = null);

    private static async Task<IResult> RequestPasswordReset(PasswordResetHelpRequest request, HttpContext h, ApplicationDbContext db, INotificationService notifications, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var rawPhone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        var rawEmail = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        string normalized;
        UserAccount? user;
        if (rawPhone is not null)
        {
            try { normalized = EthiopianMobileNumber.Normalize(rawPhone); }
            catch (ArgumentException) { normalized = string.Empty; }
            user = normalized.Length == 0 ? null : await db.UserAccounts.SingleOrDefaultAsync(x => x.NormalizedPhoneNumber == normalized, ct);
        }
        else if (rawEmail is not null)
        {
            normalized = rawEmail.ToUpperInvariant();
            user = await db.UserAccounts.SingleOrDefaultAsync(x => x.NormalizedEmail == normalized, ct);
        }
        else
        {
            normalized = string.Empty;
            user = null;
        }
        if (user is null)
        {
            db.LoginAudits.Add(new LoginAudit { Id = Guid.NewGuid(), UserAccountId = null, NormalizedEmail = normalized, WasSuccessful = false, FailureReason = "PasswordResetHelpNoAccount", IpAddress = h.Connection.RemoteIpAddress?.ToString(), UserAgent = h.Request.Headers.UserAgent.ToString(), CorrelationId = h.TraceIdentifier, AttemptedAtUtc = now, CreatedAtUtc = now });
            await db.SaveChangesAsync(ct);
            return Results.NotFound(new { detail = "No account was found with that phone number or email. Please create a new account." });
        }
        var eligible = user.Role != UserRole.PlatformAdmin && AuthenticationService.CanSignIn(user);
        var reference = $"PWR-{Guid.NewGuid():N}"[..32].ToUpperInvariant();
        db.LoginAudits.Add(new LoginAudit { Id = Guid.NewGuid(), UserAccountId = eligible ? user!.Id : null, NormalizedEmail = normalized, WasSuccessful = eligible, FailureReason = eligible ? "PasswordResetHelpRequested" : "PasswordResetHelpDetailsMismatch", IpAddress = h.Connection.RemoteIpAddress?.ToString(), UserAgent = h.Request.Headers.UserAgent.ToString(), CorrelationId = h.TraceIdentifier, AttemptedAtUtc = now, CreatedAtUtc = now });
        if (eligible)
        {
            var activeRequest = await db.SupportRequests.AsNoTracking()
                .Where(x => x.Subject == "Password Reset" && x.Contact == normalized && (x.Status == "Pending" || x.Status == "Approved"))
                .OrderByDescending(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);
            if (activeRequest is not null)
            {
                await db.SaveChangesAsync(ct);
                // Never disclose an existing recovery credential to a caller who only knows the phone number.
                return Results.Ok(new { status = "Pending", message = ActiveRequestExistsMessage() });
            }
            await db.SupportRequests.Where(x => x.Subject == "Password Reset" && x.Contact == normalized && (x.Status == "Rejected" || x.Status == "Completed" || x.Status == "Expired")).ExecuteDeleteAsync(ct);
            var displayName = await DisplayName(user!, db, ct);
            var userType = user!.Role.ToString();
            var supportRequest = new SupportRequest { Id = Guid.NewGuid(), PublicReference = reference, Name = displayName, Contact = normalized, UserType = userType, Subject = "Password Reset", Message = user.Id.ToString(), PreferredLanguage = "en", ConsentAcknowledged = true, Status = "Pending", CreatedAtUtc = now };
            db.SupportRequests.Add(supportRequest);
            await db.SaveChangesAsync(ct);
            var admins = await db.UserAccounts.AsNoTracking().Where(x => (x.Role == UserRole.PlatformAdmin || x.Role == UserRole.OperationsAdmin) && x.Status == AccountStatus.Active).Select(x => new { x.Id }).ToListAsync(ct);
            if (admins.Count > 0)
            {
                var title = $"Password reset request pending for {displayName} ({ReadableRole(user.Role)})";
                var body = $"The {ReadableRole(user.Role)} account is waiting for admin approval.";
                await notifications.CreateAsync(new(
                    NotificationType.PasswordResetRequested,
                    $"password-reset-admin:{supportRequest.Id:N}",
                    new Dictionary<string, string>
                    {
                        ["Title"] = title,
                        ["Body"] = body,
                        ["TargetPath"] = "/admin/password-reset-requests#password-reset-requests",
                        ["RequesterName"] = displayName,
                        ["RequesterRole"] = ReadableRole(user.Role),
                    },
                    admins.Select(x => new NotificationRecipientRequest(x.Id, NotificationRecipientType.User, NotificationChannel.InApp, null, null)).ToList(),
                    NotificationPriority.High,
                    h.TraceIdentifier,
                    nameof(SupportRequest),
                    supportRequest.Id.ToString()), ct);
            }
            return Results.Ok(new { reference, status = "Pending", message = PendingCreatedMessage() });
        }
        return Results.Ok(new { reference, status = "Pending", message = PendingMessage() });
    }

    private static async Task<IResult> PasswordResetStatus(string reference, ApplicationDbContext db, ITokenService tokens, CancellationToken ct)
    {
        var request = await db.SupportRequests.SingleOrDefaultAsync(x => x.PublicReference == reference && x.Subject == "Password Reset", ct);
        if (request is not null)
        {
            if (request.Status == "Approved")
            {
                var token = await db.PasswordResetTokens.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == tokens.HashToken(reference), ct);
                if (token is null || token.UsedAtUtc is not null || token.ExpiresAtUtc <= DateTime.UtcNow)
                {
                    var now = DateTime.UtcNow;
                    request.Status = "Expired";
                    request.UpdatedAtUtc = now;
                    AddPasswordResetAudit(db, "PasswordResetExpired", request.Id, request.PublicReference, request.Contact, "Expired", string.Empty, now);
                    await db.SaveChangesAsync(ct);
                }
            }
            var status = request.Status is "Approved" or "Completed" or "Rejected" or "Expired" ? request.Status : "Pending";
            return Results.Ok(new { status, message = PasswordResetMessage(status) });
        }
        var archived = await ArchivedPasswordResetStatus(reference, db, ct);
        return archived is null
            ? Results.NotFound(new { detail = "Password reset request not found." })
            : Results.Ok(new { status = archived, message = PasswordResetMessage(archived) });
    }

    private static async Task<IResult> CompletePasswordReset(ResetPasswordRequest request, HttpContext h, IAuthenticationService service, ApplicationDbContext db, CancellationToken ct)
    {
        var result = await service.ResetPasswordAsync(request, Context(h), ct);
        if (result.Succeeded)
        {
            var now = DateTime.UtcNow;
            var support = await db.SupportRequests.SingleOrDefaultAsync(x => x.PublicReference == request.ResetToken && x.Subject == "Password Reset" && x.Status == "Approved", ct);
            if (support is not null)
            {
                AddPasswordResetAudit(db, "PasswordResetCompleted", support.Id, support.PublicReference, support.Contact, support.Status, h.TraceIdentifier, now);
                db.SupportRequests.Remove(support);
                await db.SaveChangesAsync(ct);
            }
        }
        return ToHttp(result);
    }

    private static async Task<string?> ArchivedPasswordResetStatus(string reference, ApplicationDbContext db, CancellationToken ct)
    {
        var eventType = (await db.OperationalAuditEvents.AsNoTracking()
            .Where(x => x.EventType == "PasswordResetAuthorized" || x.EventType == "PasswordResetRejected" || x.EventType == "PasswordResetCompleted" || x.EventType == "PasswordResetExpired")
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new { x.EventType, x.MetadataJson })
            .ToListAsync(ct))
            .FirstOrDefault(x => x.MetadataJson.Contains(reference))
            ?.EventType;
        return eventType switch
        {
            "PasswordResetAuthorized" => "Approved",
            "PasswordResetRejected" => "Rejected",
            "PasswordResetCompleted" => "Completed",
            "PasswordResetExpired" => "Expired",
            _ => null
        };
    }

    private static void AddPasswordResetAudit(ApplicationDbContext db, string eventType, Guid subjectId, string reference, string contact, string status, string correlationId, DateTime now)
        => db.OperationalAuditEvents.Add(new OperationalAuditEvent
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            SubjectId = subjectId,
            MetadataJson = JsonSerializer.Serialize(new { publicReference = reference, contact, status }),
            CorrelationId = correlationId,
            CreatedAtUtc = now
        });

    private static async Task<string> DisplayName(UserAccount user, ApplicationDbContext db, CancellationToken ct)
    {
        if (user.CustomerId.HasValue) return await db.Customers.Where(x => x.Id == user.CustomerId).Select(x => x.DisplayName).SingleOrDefaultAsync(ct) ?? "Shopper";
        if (user.CreatorId.HasValue) return await db.Creators.Where(x => x.Id == user.CreatorId).Select(x => x.DisplayName).SingleOrDefaultAsync(ct) ?? "Creator";
        if (user.MerchantId.HasValue) return await db.Merchants.Where(x => x.Id == user.MerchantId).Select(x => x.PrimaryContactName).SingleOrDefaultAsync(ct) ?? "Business Owner";
        if (user.CashierId.HasValue) return await db.Cashiers.Where(x => x.Id == user.CashierId).Select(x => (x.FirstName + " " + x.LastName).Trim()).SingleOrDefaultAsync(ct) ?? "Cashier";
        return "Weymela User";
    }
    private static string ReadableRole(UserRole role) => role switch
    {
        UserRole.Customer => "Customer",
        UserRole.Creator => "Creator",
        UserRole.MerchantAdmin => "Business",
        UserRole.Cashier => "Cashier",
        UserRole.Supervisor => "Supervisor",
        UserRole.PlatformAdmin => "Platform Admin",
        _ => role.ToString(),
    };
    private static string PendingMessage() => "Your password reset request is waiting for admin approval.";
    private static string PendingCreatedMessage() => "Password reset request submitted successfully. Your request is waiting for admin approval.";
    private static string ActiveRequestExistsMessage() => "A password reset request is already active. Continue on the device that submitted it or contact Weymela Support.";
    private static string PasswordResetMessage(string status) => status switch
    {
        "Approved" => "Your request was approved. Create a new password.",
        "Completed" => "Password reset completed.",
        "Rejected" => "Your password reset request was rejected. Please contact Weymela Support or submit a new request.",
        "Expired" => "Your password reset approval has expired. Please submit a new request.",
        _ => PendingMessage(),
    };
    private static RequestContext Context(HttpContext h) => new(h.Connection.RemoteIpAddress?.ToString(), h.Request.Headers.UserAgent.ToString(), h.TraceIdentifier);
    private static IResult ToHttp<T>(Result<T> result) => result.Succeeded ? Results.Ok(result.Value) : Problem(result.Error!, result.Code);
    private static IResult ToHttp(OperationResult result) => result.Succeeded ? Results.Ok(new { succeeded = true }) : Problem(result.Error!, result.Code);
    private static IResult ToHttp(PhoneOtpResult result) => result.Succeeded ? Results.Ok(new { succeeded = true }) : Problem(result.Error!);
    private static IResult Problem(string detail, string? code = null) => code is null
        ? Results.Problem(detail, statusCode: StatusCodes.Status400BadRequest, title: "Authentication request failed")
        : Results.Problem(detail, statusCode: StatusCodes.Status403Forbidden, title: code);
}

using System.Security.Cryptography;
using System.Text;
using CreatorPay.Application.Integration;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Integration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;

namespace CreatorPay.Api.Integration;

internal static class ExternalProductEndpoints
{
    public static void MapExternalProductEndpoints(this IEndpointRouteBuilder endpoints, IWebHostEnvironment environment)
    {
        var group = endpoints.MapGroup("/api/v1/integration/v3").WithTags("V3 product integration");
        group.MapGet("/configuration", (ExternalIntegrationService service) => Results.Ok(new
        {
            enabled = service.Enabled,
            authenticationUrl = service.Enabled ? service.V3ProfileSelectionUrl : null
        })).AllowAnonymous();
        group.MapPost("/begin", async (HttpContext context, ExternalIntegrationService service, CancellationToken ct) =>
        {
            if (!service.Enabled) return Results.NotFound();
            var form = await context.Request.ReadFormAsync(ct);
            var role = form["role"].ToString();
            var purpose = form["purpose"].ToString();
            if (role is not ("Customer" or "Creator" or "Business" or "PlatformAdmin")
                || purpose is not (V3HandoffPurposes.ProfileOnboarding or V3HandoffPurposes.ExistingWorkspace)
                || role == "PlatformAdmin" && purpose != V3HandoffPurposes.ExistingWorkspace)
                return Results.BadRequest(new { title = "Invalid workspace request." });
            var state = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            context.Response.Cookies.Append(ExternalProductAuthentication.HandoffCookieName(environment), state,
                TransientCookie(environment, TimeSpan.FromMinutes(2)));
            var target = QueryHelpers.AddQueryString(service.V3ContinuationUrl,
                new Dictionary<string, string?> { ["state"] = state, ["role"] = role, ["purpose"] = purpose });
            if (ProductRequest(context, service)) return Results.Ok(new { state });
            return Results.Redirect(target, permanent: false, preserveMethod: false);
        }).AllowAnonymous().DisableAntiforgery().RequireRateLimiting("auth-sensitive");

        group.MapPost("/callback", async (HttpContext context, ExternalIntegrationService service,
            CancellationToken ct) =>
        {
            if (!service.Enabled) return Results.NotFound();
            var form = await context.Request.ReadFormAsync(ct);
            var state = form["state"].ToString();
            var handoffCookieName = ExternalProductAuthentication.HandoffCookieName(environment);
            var cookieState = context.Request.Cookies[handoffCookieName];
            context.Response.Cookies.Delete(handoffCookieName,
                TransientCookie(environment, TimeSpan.Zero));
            if (!Fixed(state, cookieState)) return Results.Unauthorized();
            try
            {
                var session = await service.RedeemAsync(form["code"].ToString(),
                    form["callbackId"].ToString(), ct);
                await SignInAsync(context, session, environment);
                var destination = session.Session.IsOnboarding
                    ? session.Session.Role switch
                    {
                        UserRole.Customer => "/onboarding/customer",
                        UserRole.Creator => "/onboarding/creator",
                        UserRole.MerchantAdmin => "/onboarding/business",
                        _ => "/"
                    }
                    : Destination(session.Session.Role);
                if (ProductRequest(context, service)) return Results.Ok(new { destination });
                return Results.Redirect(service.ProductWebUrl + destination);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (InvalidOperationException)
            {
                return Results.Problem(statusCode: 503, title: "Weymela workspace is temporarily unavailable.");
            }
        }).AllowAnonymous().DisableAntiforgery().RequireRateLimiting("auth-sensitive");

        group.MapGet("/session", async (HttpContext context, ExternalIntegrationService service,
            CancellationToken ct) =>
        {
            var session = await CurrentAsync(context, service, ct);
            if (session is null) return Results.Unauthorized();
            return Results.Ok(new
            {
                session.Session.Role,
                status = session.User?.Status.ToString() ?? "Onboarding",
                isOnboarding = session.Session.IsOnboarding,
                destination = session.Session.IsOnboarding ? null : Destination(session.Session.Role)
            });
        }).RequireAuthorization("V3ExternalSession");

        group.MapPost("/onboarding/customer", async (ExternalCustomerRegistration input, HttpContext context,
            ExternalIntegrationService service, CancellationToken ct) =>
        {
            if (!ProductRequest(context, service)) return Results.Forbid();
            return await Provision(context, service,
                await service.ProvisionCustomerAsync(SessionId(context), input, ct), environment, ct);
        })
            .RequireAuthorization("V3ExternalSession");
        group.MapPost("/onboarding/creator", async (ExternalCreatorRegistration input, HttpContext context,
            ExternalIntegrationService service, CancellationToken ct) =>
        {
            if (!ProductRequest(context, service)) return Results.Forbid();
            return await Provision(context, service,
                await service.ProvisionCreatorAsync(SessionId(context), input, ct), environment, ct);
        })
            .RequireAuthorization("V3ExternalSession");
        group.MapPost("/onboarding/business", async (ExternalBusinessRegistration input, HttpContext context,
            ExternalIntegrationService service, CancellationToken ct) =>
        {
            if (!ProductRequest(context, service)) return Results.Forbid();
            return await Provision(context, service,
                await service.ProvisionBusinessAsync(SessionId(context), input, ct), environment, ct);
        })
            .RequireAuthorization("V3ExternalSession");

        group.MapPost("/switch-profile", async (HttpContext context, ExternalIntegrationService service,
            CancellationToken ct) =>
        {
            if (!ProductRequest(context, service)) return Results.Forbid();
            await service.RevokeSessionAsync(SessionId(context), "Profile switch", ct);
            await context.SignOutAsync(ExternalProductAuthentication.CookieScheme);
            return Results.Ok(new { redirectUrl = service.V3ProfileSelectionUrl });
        }).RequireAuthorization("V3ExternalSession");
        group.MapPost("/logout", async (HttpContext context, ExternalIntegrationService service,
            CancellationToken ct) =>
        {
            if (!ProductRequest(context, service)) return Results.Forbid();
            await service.RevokeSessionAsync(SessionId(context), "Sign out", ct);
            await context.SignOutAsync(ExternalProductAuthentication.CookieScheme);
            return Results.Ok(new { redirectUrl = service.V3SignOutUrl });
        }).RequireAuthorization("V3ExternalSession");
    }

    private static async Task<IResult> Provision(HttpContext context, ExternalIntegrationService service,
        ExternalProvisioningResult result, IWebHostEnvironment environment, CancellationToken ct)
    {
        var current = await service.ValidateSessionAsync(SessionId(context), ct)
            ?? throw new UnauthorizedAccessException();
        await SignInAsync(context, current, environment);
        return Results.Ok(result);
    }
    private static async Task<ExternalSessionContext?> CurrentAsync(HttpContext context,
        ExternalIntegrationService service, CancellationToken ct) =>
        Guid.TryParse(context.User.FindFirst(ExternalProductAuthentication.SessionClaim)?.Value, out var id)
            ? await service.ValidateSessionAsync(id, ct) : null;
    private static Guid SessionId(HttpContext context) =>
        Guid.TryParse(context.User.FindFirst(ExternalProductAuthentication.SessionClaim)?.Value, out var id)
            ? id : throw new UnauthorizedAccessException();
    private static Task SignInAsync(HttpContext context, ExternalSessionContext session,
        IWebHostEnvironment environment) => context.SignInAsync(ExternalProductAuthentication.CookieScheme,
        ExternalProductAuthentication.Principal(session), new AuthenticationProperties
        {
            IsPersistent = false,
            AllowRefresh = false,
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = session.Session.ExpiresAtUtc
        });
    private static CookieOptions TransientCookie(IWebHostEnvironment environment, TimeSpan lifetime) => new()
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment() && !environment.IsEnvironment("E2E"),
        SameSite = SameSiteMode.Strict,
        Path = "/",
        MaxAge = lifetime > TimeSpan.Zero ? lifetime : null
    };
    private static bool Fixed(string state, string? expected)
    {
        if (state.Length is < 32 or > 160 || expected is null) return false;
        var left = SHA256.HashData(Encoding.UTF8.GetBytes(state));
        var right = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(left, right);
    }
    private static bool ProductRequest(HttpContext context, ExternalIntegrationService service)
    {
        if (context.Request.Headers["X-Weymela-Product-Request"] != "1"
            || !Uri.TryCreate(context.Request.Headers.Origin.ToString(), UriKind.Absolute, out var origin)
            || !Uri.TryCreate(service.ProductWebUrl, UriKind.Absolute, out var expected)) return false;
        return origin.Scheme == expected.Scheme && origin.Host == expected.Host && origin.Port == expected.Port
            && origin.AbsolutePath == "/" && string.IsNullOrEmpty(origin.Query)
            && string.IsNullOrEmpty(origin.Fragment) && string.IsNullOrEmpty(origin.UserInfo);
    }
    private static string Destination(UserRole role) => role switch
    {
        UserRole.Customer => "/shopper",
        UserRole.Creator => "/creator",
        UserRole.MerchantAdmin => "/business",
        UserRole.PlatformAdmin => "/admin",
        UserRole.Cashier => "/cashier",
        _ => "/"
    };
}

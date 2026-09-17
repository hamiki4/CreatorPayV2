using System.Security.Claims;
using CreatorPay.Infrastructure.Integration;

namespace CreatorPay.Api.Integration;

internal static class ExternalProductAuthentication
{
    public const string SmartScheme = "WeymelaProduct";
    public const string CookieScheme = "V3ExternalSession";
    private const string ProductionCookieName = "__Host-Weymela.Product";
    private const string ProductionHandoffCookieName = "__Host-Weymela.ProductHandoff";
    private const string DevelopmentCookieName = "Weymela.Product";
    private const string DevelopmentHandoffCookieName = "Weymela.ProductHandoff";
    public const string SessionClaim = "v3_external_session";
    public const string SourceClaim = "authentication_source";
    public const string PurposeClaim = "handoff_purpose";
    public const string AccountEmailClaim = "v3_account_email";
    public const string AccountPhoneClaim = "v3_account_phone";

    public static string CookieName(IWebHostEnvironment environment) =>
        environment.IsDevelopment() || environment.IsEnvironment("E2E")
            ? DevelopmentCookieName : ProductionCookieName;

    public static string HandoffCookieName(IWebHostEnvironment environment) =>
        environment.IsDevelopment() || environment.IsEnvironment("E2E")
            ? DevelopmentHandoffCookieName : ProductionHandoffCookieName;

    public static ClaimsPrincipal Principal(ExternalSessionContext context, string? accountEmail = null,
        string? accountPhone = null)
    {
        var claims = new List<Claim>
        {
            new(SessionClaim, context.Session.SessionId.ToString()),
            new(SourceClaim, "V3External"),
            new(PurposeClaim, context.Session.Purpose)
        };
        Add(claims, AccountEmailClaim, accountEmail ?? context.AccountEmail);
        Add(claims, AccountPhoneClaim, accountPhone ?? context.AccountPhone);
        if (context.User is { } user)
        {
            claims.Add(new(ClaimTypes.NameIdentifier, user.Id.ToString()));
            claims.Add(new(ClaimTypes.Role, user.Role.ToString()));
            claims.Add(new(CreatorPay.Application.Authentication.AuthenticationClaimTypes.AccountStatus,
                user.Status.ToString()));
            claims.Add(new(CreatorPay.Application.Authentication.AuthenticationClaimTypes.EmailVerified,
                user.IsEmailVerified.ToString().ToLowerInvariant()));
            claims.Add(new(CreatorPay.Application.Authentication.AuthenticationClaimTypes.PhoneVerified,
                user.IsPhoneVerified.ToString().ToLowerInvariant()));
            Add(claims, "creator_id", user.CreatorId); Add(claims, "customer_id", user.CustomerId);
            Add(claims, "merchant_id", user.MerchantId); Add(claims, "supervisor_id", user.SupervisorId);
            Add(claims, "cashier_id", user.CashierId);
        }
        return new(new ClaimsIdentity(claims, CookieScheme));
    }

    private static void Add(List<Claim> claims, string type, Guid? value)
    {
        if (value.HasValue) claims.Add(new(type, value.Value.ToString()));
    }
    private static void Add(List<Claim> claims, string type, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) claims.Add(new(type, value));
    }
}
